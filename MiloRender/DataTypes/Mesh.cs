// In MiloRender/DataTypes/Mesh.cs
using Debugger;
using MiloRender.DataTypes;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic; // For List<T>
using System.IO; // For Path operations
using System.Linq; // For .ToArray()

// Add Assimp using statements
using Assimp;
using Assimp.Configs;
// It's good practice to use the Silk.NET maths types if Assimp's vector types
// don't directly map or if you want consistency. However, Assimp's Vector3D/Color4D are fine for data extraction.
using SilkNetVector3D = Silk.NET.Maths.Vector3D<float>; // Alias to avoid confusion if Assimp has Vector3D
using SilkNetColor4D = Silk.NET.Maths.Vector4D<float>; // Alias if needed


namespace MiloRender.DataTypes
{
    public class Mesh : IDisposable
    {
        public VertexBuffer vertexBuffer { get; private set; }
        public Material Material { get; set; }
        public Transform Transform { get; private set; }

        private uint _vao = 0;
        private uint _vbo = 0;
        private uint _ebo = 0;

        private GL _gl;

        private bool _isUploaded = false;
        private bool _isDisposed = false;

        // Constructor for primitives
        public Mesh(GL gl, Primitive primitiveType, Material material = null)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl), "OpenGL context cannot be null for Mesh creation.");
            vertexBuffer = new VertexBuffer(primitiveType); // This populates vertexBuffer.vertices and .indices
            Transform = new Transform();
            Material = material ?? new Material(); // Assign a default material if none provided
            Debug.Log($"Mesh: Created with primitive type {primitiveType}.");
            // UploadToGPU(); // Consider if primitives should auto-upload
        }

        // Constructor for raw data (typically used by GLB loader now)
        public Mesh(GL gl, float[] vertices, uint[] indices, Material material = null)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl), "OpenGL context cannot be null for Mesh creation.");
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (indices == null) throw new ArgumentNullException(nameof(indices));

            vertexBuffer = new VertexBuffer(vertices, indices); // This directly uses the provided arrays
            Transform = new Transform();
            Material = material ?? new Material();
            Debug.Log($"Mesh: Created from raw vertex/index data. Vertices: {vertices.Length / Vertex.stride}, Indices: {indices.Length}.");
            // UploadToGPU(); // Consider if meshes from raw data should auto-upload
        }

        // Constructor from Vertex and Face objects (less direct for GLB, but could be used by tools)
        public Mesh(GL gl, Vertex[] vertices, Face[] faces, Material material = null)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl), "OpenGL context cannot be null for Mesh creation.");
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (faces == null) throw new ArgumentNullException(nameof(faces));

            vertexBuffer = new VertexBuffer(vertices, faces); // This processes Vertex/Face to float[]/uint[]
            Transform = new Transform();
            Material = material ?? new Material();
            Debug.Log($"Mesh: Created from {vertices.Length} Vertex objects and {faces.Length} Face objects.");
        }


        /// <summary>
        /// Loads a mesh from the first mesh found in a GLB file using Assimp.
        /// </summary>
        /// <param name="gl">The OpenGL context.</param>
        /// <param name="filePath">Path to the .glb file.</param>
        /// <param name="material">Optional material to assign to the loaded mesh.</param>
        /// <returns>A new Mesh object if loading is successful, otherwise null.</returns>
        public static Mesh LoadFromGlbFile(GL gl, string filePath, Material material = null)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("Mesh.LoadFromGlbFile: File path is null or empty.");
                return null;
            }
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Mesh.LoadFromGlbFile: File not found at '{filePath}'.");
                return null;
            }
            if (gl == null)
            {
                Debug.LogError("Mesh.LoadFromGlbFile: GL context is null.");
                return null;
            }


            Debug.Log($"Mesh.LoadFromGlbFile: Attempting to load GLB: {filePath}");
            AssimpContext importer = new AssimpContext();

            // Configure post-processing steps
            // Triangulate: Converts all faces to triangles. Essential.
            // GenerateNormals: If normals are missing. (GenerateSmoothNormals for smooth shading)
            // FlipUVs: Often needed as OpenGL UV origin (bottom-left) differs from many model formats (top-left).
            // JoinIdenticalVertices: Optimizes vertex count.
            // CalculateTangentSpace: For normal mapping later.
            // FlipWindingOrder: If faces are inside out (try without first).
            const PostProcessSteps steps = PostProcessSteps.Triangulate |
                                           PostProcessSteps.GenerateSmoothNormals | // Or GenerateNormals for flat shading if intended
                                           PostProcessSteps.FlipUVs |
                                           PostProcessSteps.JoinIdenticalVertices |
                                           PostProcessSteps.CalculateTangentSpace; // Good to have for future lighting

            Scene scene;
            try
            {
                scene = importer.ImportFile(filePath, steps);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Mesh.LoadFromGlbFile: Assimp import failed for '{filePath}'. Exception: {ex.Message}");
                importer.Dispose();
                return null;
            }

            if (scene == null || !scene.HasMeshes || scene.MeshCount == 0)
            {
                Debug.LogError($"Mesh.LoadFromGlbFile: No meshes found in file '{filePath}' or scene is null.");
                importer.Dispose();
                return null;
            }

            // For simplicity, we'll load the FIRST mesh in the file.
            // A more complete importer would handle multiple meshes, materials, and scene hierarchy.
            Assimp.Mesh assimpMesh = scene.Meshes[0];
            Debug.Log($"Mesh.LoadFromGlbFile: Loaded Assimp mesh '{assimpMesh.Name}' with {assimpMesh.VertexCount} vertices and {assimpMesh.FaceCount} faces.");

            List<float> packedVertexDataList = new List<float>();
            List<uint> indexList = new List<uint>();

            bool hasNormals = assimpMesh.HasNormals;
            bool hasTexCoords = assimpMesh.HasTextureCoords(0); // Check first UV channel
            bool hasVertexColors = assimpMesh.HasVertexColors(0); // Check first vertex color channel

            if (!hasNormals) Debug.LogWarning("Mesh.LoadFromGlbFile: Source mesh has no normals. Smooth normals were requested from Assimp.");
            if (!hasTexCoords) Debug.LogWarning("Mesh.LoadFromGlbFile: Source mesh has no texture coordinates (UVs). Using default (0,0).");
            if (!hasVertexColors) Debug.LogWarning("Mesh.LoadFromGlbFile: Source mesh has no vertex colors. Using default white (1,1,1,1).");


            for (int i = 0; i < assimpMesh.VertexCount; i++)
            {
                // Position (always present)
                packedVertexDataList.Add(assimpMesh.Vertices[i].X);
                packedVertexDataList.Add(assimpMesh.Vertices[i].Y);
                packedVertexDataList.Add(assimpMesh.Vertices[i].Z);

                // Normals
                if (hasNormals)
                {
                    packedVertexDataList.Add(assimpMesh.Normals[i].X);
                    packedVertexDataList.Add(assimpMesh.Normals[i].Y);
                    packedVertexDataList.Add(assimpMesh.Normals[i].Z);
                }
                else // Should have been generated by Assimp if missing, but as a fallback:
                {
                    packedVertexDataList.Add(0.0f); packedVertexDataList.Add(1.0f); packedVertexDataList.Add(0.0f); // Default Up
                }

                // Vertex Colors
                if (hasVertexColors)
                {
                    Color4D color = assimpMesh.VertexColorChannels[0][i];
                    packedVertexDataList.Add(color.R);
                    packedVertexDataList.Add(color.G);
                    packedVertexDataList.Add(color.B);
                    packedVertexDataList.Add(color.A);
                }
                else
                {
                    packedVertexDataList.Add(1.0f); packedVertexDataList.Add(1.0f); packedVertexDataList.Add(1.0f); packedVertexDataList.Add(1.0f); // Default White
                }

                // Texture Coordinates (UVs)
                if (hasTexCoords)
                {
                    Vector3D texCoord = assimpMesh.TextureCoordinateChannels[0][i]; // Assimp uses Vector3D for UVs, we only need X,Y
                    packedVertexDataList.Add(texCoord.X);
                    packedVertexDataList.Add(texCoord.Y);
                }
                else
                {
                    packedVertexDataList.Add(0.0f); packedVertexDataList.Add(0.0f); // Default (0,0)
                }
            }

            // Indices (Assimp ensures faces are triangulated if PostProcessSteps.Triangulate is used)
            foreach (Assimp.Face assimpFace in assimpMesh.Faces) // Corrected type to Assimp.Face
            {
                if (assimpFace.IndexCount == 3) // Now correctly calls Assimp.Face.IndexCount
                {
                    indexList.Add((uint)assimpFace.Indices[0]); // Now correctly calls Assimp.Face.Indices
                    indexList.Add((uint)assimpFace.Indices[1]);
                    indexList.Add((uint)assimpFace.Indices[2]);
                }
                else
                {
                    // This should not happen if Triangulate post-process step is used.
                    // If it does, you might need to handle non-triangle faces or log an error.
                    Debug.LogWarning($"Mesh.LoadFromGlbFile: Encountered a face with {assimpFace.IndexCount} indices. Expected 3. Skipping this face.");
                }
            }

            importer.Dispose(); // Dispose Assimp context once done

            if (packedVertexDataList.Count == 0 || indexList.Count == 0)
            {
                Debug.LogError($"Mesh.LoadFromGlbFile: Failed to extract any vertex or index data from mesh '{assimpMesh.Name}' in '{filePath}'.");
                return null;
            }

            Debug.Log($"Mesh.LoadFromGlbFile: Successfully processed mesh data. Packed Vertices Floats: {packedVertexDataList.Count}, Indices: {indexList.Count}.");

            // Use the constructor that takes raw float[] vertices and uint[] indices
            Mesh newMesh = new Mesh(gl, packedVertexDataList.ToArray(), indexList.ToArray(), material);
            // newMesh.UploadToGPU(); // The user can call this after loading, or Draw() will handle it.
            return newMesh;
        }


        public unsafe void UploadToGPU()
        {
            if (_gl == null) { Debug.LogError("Mesh.UploadToGPU: GL context is not set."); return; }
            if (_isUploaded) { DeleteGLBuffers(); } // Re-upload implies deleting old buffers

            if (vertexBuffer == null || vertexBuffer.vertices == null || vertexBuffer.indices == null)
            { Debug.LogError("Mesh.UploadToGPU: VertexBuffer or its data is null."); return; }

            if (vertexBuffer.vertices.Length == 0 || vertexBuffer.indices.Length == 0)
            { Debug.LogWarning("Mesh.UploadToGPU: Vertex or index data is empty. Uploading empty buffers (will likely render nothing)."); }

            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            fixed (float* v = vertexBuffer.vertices)
            { _gl.BufferData(BufferTargetARB.ArrayBuffer, (uint)(vertexBuffer.vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw); }

            _ebo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            fixed (uint* i = vertexBuffer.indices)
            { _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (uint)(vertexBuffer.indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw); }

            uint strideInBytes = Vertex.stride * sizeof(float);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, strideInBytes, (void*)0); // Position
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, strideInBytes, (void*)(3 * sizeof(float))); // Normal
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, strideInBytes, (void*)(6 * sizeof(float))); // Color
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, strideInBytes, (void*)(10 * sizeof(float))); // UV
            _gl.EnableVertexAttribArray(3);

            _gl.BindVertexArray(0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

            _isUploaded = true;
            Debug.Log($"Mesh (VAO: {_vao}): Data upload and VAO setup complete. Vertices: {vertexBuffer.vertices.Length / Vertex.stride}, Indices: {vertexBuffer.indices.Length}");
        }

        public uint GetVAO()
        {
            if (!_isUploaded) Debug.LogWarning($"Mesh.GetVAO: Mesh data (HashCode {this.GetHashCode()}) has not been uploaded. VAO is {_vao}.");
            return _vao;
        }

        public int GetIndexCount() { return vertexBuffer?.indices?.Length ?? 0; }

        public void Draw()
        {
            if (!_isUploaded)
            {
                Debug.LogWarning($"Mesh.Draw (HashCode: {this.GetHashCode()}): Not uploaded. Attempting UploadToGPU().");
                UploadToGPU();
                if (!_isUploaded) { Debug.LogError($"Mesh.Draw (HashCode: {this.GetHashCode()}): GPU upload failed. Cannot draw."); return; }
            }
            Render.instance.Draw(this);
        }

        private void DeleteGLBuffers()
        {
            if (_gl == null) return;
            if (_vbo != 0) { _gl.DeleteBuffer(_vbo); _vbo = 0; }
            if (_ebo != 0) { _gl.DeleteBuffer(_ebo); _ebo = 0; }
            if (_vao != 0) { _gl.DeleteVertexArray(_vao); _vao = 0; }
            _isUploaded = false; // Mark as not uploaded after deleting buffers
            Debug.Log($"Mesh (Old VAO: {_vao}): GL buffers deleted.");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing) { /* Dispose managed state if any specific to Mesh itself */ }
            DeleteGLBuffers(); // This handles GPU resources
            _gl = null; // Important to prevent use after dispose
            _isDisposed = true;
            Debug.Log($"Mesh (HashCode: {this.GetHashCode()}): Disposed.");
        }

        ~Mesh() { Dispose(disposing: false); }
    }
}