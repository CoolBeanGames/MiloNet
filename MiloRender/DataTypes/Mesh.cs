// In MiloRender/DataTypes/Mesh.cs
using Debugger;
using MiloRender.DataTypes; // This might already be implicitly available due to namespace
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic; // For List<T>
// using System.IO; // No longer needed here for Path operations
using System.Linq; // For .ToArray()

// Assimp using statements are removed from here

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
            vertexBuffer = new VertexBuffer(primitiveType);
            Transform = new Transform();
            Material = material ?? new Material();
            Debug.Log($"Mesh: Created with primitive type {primitiveType}.");
        }

        // Constructor for raw data (typically used by GLB loader now)
        public Mesh(GL gl, float[] vertices, uint[] indices, Material material = null)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl), "OpenGL context cannot be null for Mesh creation.");
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (indices == null) throw new ArgumentNullException(nameof(indices));

            vertexBuffer = new VertexBuffer(vertices, indices);
            Transform = new Transform();
            Material = material ?? new Material();
            Debug.Log($"Mesh: Created from raw vertex/index data. Vertices: {vertices.Length / Vertex.stride}, Indices: {indices.Length}.");
        }

        // Constructor from Vertex and Face objects
        public Mesh(GL gl, Vertex[] vertices, Face[] faces, Material material = null)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl), "OpenGL context cannot be null for Mesh creation.");
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (faces == null) throw new ArgumentNullException(nameof(faces));

            vertexBuffer = new VertexBuffer(vertices, faces);
            Transform = new Transform();
            Material = material ?? new Material();
            Debug.Log($"Mesh: Created from {vertices.Length} Vertex objects and {faces.Length} Face objects.");
        }

        // LoadFromGlbFile method has been removed and moved to GLBImporter.cs

        public unsafe void UploadToGPU()
        {
            if (_gl == null) { Debug.LogError("Mesh.UploadToGPU: GL context is not set."); return; }
            if (_isUploaded) { DeleteGLBuffers(); }

            if (vertexBuffer == null || vertexBuffer.vertices == null || vertexBuffer.indices == null)
            { Debug.LogError("Mesh.UploadToGPU: VertexBuffer or its data is null."); return; }

            if (vertexBuffer.vertices.Length == 0 || vertexBuffer.indices.Length == 0)
            { Debug.LogWarning("Mesh.UploadToGPU: Vertex or index data is empty. Uploading empty buffers."); }

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
            // if (!_isUploaded) Debug.LogWarning($"Mesh.GetVAO: Mesh data (HashCode {this.GetHashCode()}) has not been uploaded. VAO is {_vao}.");
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
            // Ensure Render.instance is valid before calling Draw on it
            if (Render.instance != null)
            {
                Render.instance.Draw(this);
            }
            else
            {
                Debug.LogError("Mesh.Draw: Render.instance is null. Cannot delegate drawing.");
            }
        }

        private void DeleteGLBuffers()
        {
            if (_gl == null) return;
            if (_vbo != 0) { _gl.DeleteBuffer(_vbo); _vbo = 0; }
            if (_ebo != 0) { _gl.DeleteBuffer(_ebo); _ebo = 0; }
            if (_vao != 0) { _gl.DeleteVertexArray(_vao); _vao = 0; }
            _isUploaded = false;
            Debug.Log($"Mesh (VAO before deletion was used to identify, now it's 0): GL buffers deleted."); // Adjusted log message
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
            DeleteGLBuffers();
            _gl = null;
            _isDisposed = true;
            Debug.Log($"Mesh (HashCode: {this.GetHashCode()}): Disposed.");
        }

        ~Mesh() { Dispose(disposing: false); }
    }
}