// In Imports/GLBImporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assimp; // Assimp's root namespace
using Assimp.Configs;
using Debugger;
using Silk.NET.OpenGL;

// Using aliases to resolve naming conflicts
using MiloMesh = MiloRender.DataTypes.Mesh;
using MiloMaterial = MiloRender.DataTypes.Material;
using MiloVertexBuffer = MiloRender.DataTypes.VertexBuffer; // If needed, though VertexBuffer is not ambiguous here
using MiloVertex = MiloRender.DataTypes.Vertex;         // If needed

// Assimp also has a Material class, so we've aliased ours.
// Assimp.Mesh is used directly or could be aliased as AssimpMesh if preferred.

namespace Imports
{
    public static class GLBImporter
    {
        /// <summary>
        /// Loads a mesh from the first mesh found in a GLB file using Assimp.
        /// </summary>
        /// <param name="gl">The OpenGL context.</param>
        /// <param name="filePath">Path to the .glb file.</param>
        /// <param name="material">Optional MiloMaterial to assign to the loaded mesh.</param>
        /// <returns>A new MiloMesh object if loading is successful, otherwise null.</returns>
        public static MiloMesh LoadGlb(GL gl, string filePath, MiloMaterial material = null) // Using alias for return type and parameter
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("GLBImporter.LoadGlb: File path is null or empty.");
                return null;
            }
            if (!File.Exists(filePath))
            {
                Debug.LogError($"GLBImporter.LoadGlb: File not found at '{filePath}'.");
                return null;
            }
            if (gl == null)
            {
                Debug.LogError("GLBImporter.LoadGlb: GL context is null.");
                return null;
            }

            Debug.Log($"GLBImporter.LoadGlb: Attempting to load GLB: {filePath}");
            AssimpContext importer = new AssimpContext();

            const PostProcessSteps steps = PostProcessSteps.Triangulate |
                                           PostProcessSteps.GenerateSmoothNormals |
                                           PostProcessSteps.FlipUVs |
                                           PostProcessSteps.JoinIdenticalVertices |
                                           PostProcessSteps.CalculateTangentSpace;

            Scene scene;
            try
            {
                scene = importer.ImportFile(filePath, steps);
            }
            catch (Exception ex)
            {
                Debug.LogError($"GLBImporter.LoadGlb: Assimp import failed for '{filePath}'. Exception: {ex.Message}");
                importer.Dispose();
                return null;
            }

            if (scene == null || !scene.HasMeshes || scene.MeshCount == 0)
            {
                Debug.LogError($"GLBImporter.LoadGlb: No meshes found in file '{filePath}' or scene is null.");
                importer.Dispose();
                return null;
            }

            // Here, Assimp.Mesh is the specific type from the Assimp library
            Assimp.Mesh assimpMesh = scene.Meshes[0];
            Debug.Log($"GLBImporter.LoadGlb: Loaded Assimp mesh '{assimpMesh.Name}' with {assimpMesh.VertexCount} vertices and {assimpMesh.FaceCount} faces.");

            List<float> packedVertexDataList = new List<float>();
            List<uint> indexList = new List<uint>();

            bool hasNormals = assimpMesh.HasNormals;
            bool hasTexCoords = assimpMesh.HasTextureCoords(0);
            bool hasVertexColors = assimpMesh.HasVertexColors(0);

            if (!hasNormals) Debug.LogWarning("GLBImporter.LoadGlb: Source mesh has no normals. Smooth normals were requested from Assimp.");
            if (!hasTexCoords) Debug.LogWarning("GLBImporter.LoadGlb: Source mesh has no texture coordinates (UVs). Using default (0,0).");
            if (!hasVertexColors) Debug.LogWarning("GLBImporter.LoadGlb: Source mesh has no vertex colors. Using default white (1,1,1,1).");

            for (int i = 0; i < assimpMesh.VertexCount; i++)
            {
                packedVertexDataList.Add(assimpMesh.Vertices[i].X);
                packedVertexDataList.Add(assimpMesh.Vertices[i].Y);
                packedVertexDataList.Add(assimpMesh.Vertices[i].Z);

                if (hasNormals && assimpMesh.Normals.Count > i)
                {
                    packedVertexDataList.Add(assimpMesh.Normals[i].X);
                    packedVertexDataList.Add(assimpMesh.Normals[i].Y);
                    packedVertexDataList.Add(assimpMesh.Normals[i].Z);
                }
                else
                {
                    packedVertexDataList.Add(0.0f); packedVertexDataList.Add(1.0f); packedVertexDataList.Add(0.0f);
                }

                if (hasVertexColors && assimpMesh.VertexColorChannels[0].Count > i)
                {
                    Color4D color = assimpMesh.VertexColorChannels[0][i];
                    packedVertexDataList.Add(color.R);
                    packedVertexDataList.Add(color.G);
                    packedVertexDataList.Add(color.B);
                    packedVertexDataList.Add(color.A);
                }
                else
                {
                    packedVertexDataList.Add(1.0f); packedVertexDataList.Add(1.0f); packedVertexDataList.Add(1.0f); packedVertexDataList.Add(1.0f);
                }

                if (hasTexCoords && assimpMesh.TextureCoordinateChannels[0].Count > i)
                {
                    Vector3D texCoord = assimpMesh.TextureCoordinateChannels[0][i];
                    packedVertexDataList.Add(texCoord.X);
                    packedVertexDataList.Add(texCoord.Y);
                }
                else
                {
                    packedVertexDataList.Add(0.0f); packedVertexDataList.Add(0.0f);
                }
            }

            foreach (Assimp.Face assimpFace in assimpMesh.Faces)
            {
                if (assimpFace.IndexCount == 3)
                {
                    indexList.Add((uint)assimpFace.Indices[0]);
                    indexList.Add((uint)assimpFace.Indices[1]);
                    indexList.Add((uint)assimpFace.Indices[2]);
                }
                else
                {
                    Debug.LogWarning($"GLBImporter.LoadGlb: Encountered a face with {assimpFace.IndexCount} indices. Expected 3. Skipping this face.");
                }
            }

            importer.Dispose();

            if (packedVertexDataList.Count == 0 || indexList.Count == 0)
            {
                Debug.LogError($"GLBImporter.LoadGlb: Failed to extract any vertex or index data from mesh '{assimpMesh.Name}' in '{filePath}'.");
                return null;
            }

            Debug.Log($"GLBImporter.LoadGlb: Successfully processed mesh data. Packed Vertices Floats: {packedVertexDataList.Count}, Indices: {indexList.Count}.");

            // Use the alias for your engine's Mesh type
            MiloMesh newMesh = new MiloMesh(gl, packedVertexDataList.ToArray(), indexList.ToArray(), material);
            return newMesh;
        }
    }
}