// In MiloRender/DataTypes/VertexBuffer.cs
using Debugger;
using System;
using System.Collections.Generic; // For List if you switch, but Linq is for Array.Empty and .Any
using System.Linq; // For .Any() and Array.Empty

namespace MiloRender.DataTypes
{
    public class VertexBuffer // Make sure this is public
    {
        public float[] vertices;
        public uint[] indices;
        public Face[] faces; // Human-readable, optional after flush
        public Vertex[] vertexData; // Human-readable, optional after flush

        public VertexBuffer(Primitive type)
        {
            Debug.Log($"VertexBuffer: Generating primitive type: {type}");
            switch (type)
            {
                case Primitive.Triangle: GenTriangle(); break;
                case Primitive.Cube: GenCube(); break;
                case Primitive.Quad: GenQuad(); break;
                default:
                    Debug.LogError($"VertexBuffer: Unknown primitive type: {type}");
                    vertexData = Array.Empty<Vertex>();
                    faces = Array.Empty<Face>();
                    break;
            }
            FlushData();
        }

        public VertexBuffer(float[] rawVertices, uint[] rawIndices)
        {
            // ... (constructor as before)
            if (rawVertices == null || rawIndices == null)
            {
                Debug.LogError("VertexBuffer Constructor: Raw vertices or indices cannot be null.");
                this.vertices = Array.Empty<float>();
                this.indices = Array.Empty<uint>();
                this.vertexData = Array.Empty<Vertex>();
                this.faces = Array.Empty<Face>();
                return;
            }

            if (rawVertices.Length % Vertex.stride != 0)
            {
                Debug.LogWarning($"VertexBuffer Constructor: Raw vertices array length ({rawVertices.Length}) is not a multiple of vertex stride ({Vertex.stride}).");
            }
            this.vertices = rawVertices;
            this.indices = rawIndices;
            this.vertexData = Array.Empty<Vertex>();
            this.faces = Array.Empty<Face>();
            Debug.Log("VertexBuffer: Created from raw vertex/index data.");
        }

        public VertexBuffer(Vertex[] sourceVertices, Face[] sourceFaces)
        {
            // ... (constructor as before)
            if (sourceVertices == null || sourceFaces == null)
            {
                Debug.LogError("VertexBuffer Constructor: Source vertices or faces cannot be null.");
                this.vertexData = Array.Empty<Vertex>();
                this.faces = Array.Empty<Face>();
                this.vertices = Array.Empty<float>();
                this.indices = Array.Empty<uint>();
                return;
            }
            this.vertexData = sourceVertices;
            this.faces = sourceFaces;
            FlushData();
            Debug.Log($"VertexBuffer: Created from {vertexData.Length}V, {faces.Length}F.");
        }

        /// <summary>
        /// Checks if the GPU-ready vertex and index data is available and non-empty.
        /// </summary>
        /// <returns>True if data is ready, false otherwise.</returns>
        public bool IsDataReady() // Ensure this is public
        {
            bool ready = vertices != null && vertices.Length > 0 &&
                         indices != null && indices.Length > 0;
            if (!ready)
            {
                // Add a debug log if data isn't ready when expected
                // Debug.LogWarning($"VertexBuffer.IsDataReady: Data is not ready. Vertices: {vertices?.Length ?? -1}, Indices: {indices?.Length ?? -1}");
            }
            return ready;
        }

        public void FlushData()
        {
            if (vertexData == null || faces == null)
            {
                Debug.LogWarning("FlushData: vertexData or faces is null. Initializing to empty.");
                vertices = Array.Empty<float>();
                indices = Array.Empty<uint>();
                return;
            }
            if (vertexData.Length == 0)
            {
                Debug.LogWarning("FlushData: No vertex data. Initializing to empty.");
                vertices = Array.Empty<float>();
                indices = Array.Empty<uint>();
                return;
            }
            if (faces.Length == 0) // If no faces, no indices. Still pack vertices if they exist.
            {
                Debug.LogWarning("FlushData: No faces defined. Indices array will be empty.");
                vertices = new float[vertexData.Length * Vertex.stride];
                for (int i = 0; i < vertexData.Length; i++)
                {
                    if (vertexData[i] == null)
                    {
                        Debug.LogError($"FlushData: Vertex at index {i} is null while packing vertices for no-face scenario.");
                        new Vertex().GetPackedVertex().CopyTo(vertices, i * (int)Vertex.stride); // Copy default
                        continue;
                    }
                    vertexData[i].GetPackedVertex().CopyTo(vertices, i * (int)Vertex.stride);
                }
                indices = Array.Empty<uint>();
                // Debug.Log($"FlushData completed: {vertices.Length / Vertex.stride} vertices, 0 indices.");
                return;
            }

            vertices = new float[vertexData.Length * Vertex.stride];
            List<uint> tempIndices = new List<uint>(); // Use a list for dynamic sizing

            for (int i = 0; i < vertexData.Length; i++)
            {
                if (vertexData[i] == null)
                {
                    Debug.LogError($"FlushData: Vertex at index {i} is null. Using default vertex.");
                    float[] defaultPackedVert = new Vertex().GetPackedVertex();
                    Array.Copy(defaultPackedVert, 0, vertices, i * (int)Vertex.stride, (int)Vertex.stride);
                    continue;
                }
                vertexData[i].GetPackedVertex().CopyTo(vertices, i * (int)Vertex.stride);
            }

            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] == null || faces[i].indices == null || faces[i].indices.Length != 3)
                {
                    Debug.LogWarning($"FlushData: Face at index {i} is invalid or has != 3 indices. Skipping.");
                    continue;
                }
                if (faces[i].indices.Any(idx => idx >= vertexData.Length))
                {
                    Debug.LogError($"FlushData: Face at index {i} has out-of-bounds vertex index. Max: {vertexData.Length - 1}, Indices: {string.Join(",", faces[i].indices)}. Skipping.");
                    continue;
                }
                tempIndices.AddRange(faces[i].indices);
            }
            indices = tempIndices.ToArray();

            if (!IsDataReady() && (vertexData.Length > 0 && faces.Length > 0 && indices.Length == 0))
            {
                Debug.LogError("FlushData: CRITICAL - Data flush seems to have failed to produce indices despite source data.");
            }
            else
            {
                Debug.Log($"FlushData: Completed. Vertices: {vertices.Length / Vertex.stride}, Triangles: {indices.Length / 3}.");
            }
        }
        // ... GenTriangle, GenQuad, GenCube, AddFace methods as before ...
        // Ensure AddFace correctly calculates baseVertexIndex for GenCube
        // For GenCube (24 vertices total, 4 per face, 6 faces)
        // faceStartIndex for faces array: 0, 2, 4, 6, 8, 10
        // vertex array index for those: 0, 4, 8, 12, 16, 20
        private void AddFace(int faceArrIdx, float[] p1, float[] p2, float[] p3, float[] p4, float[] normal, float[] color)
        {
            uint vtxBaseIdx = (uint)(faceArrIdx / 2 * 4);

            vertexData[vtxBaseIdx + 0] = new Vertex(p1, normal, color, new float[] { 0, 0 });
            vertexData[vtxBaseIdx + 1] = new Vertex(p2, normal, color, new float[] { 1, 0 });
            vertexData[vtxBaseIdx + 2] = new Vertex(p3, normal, color, new float[] { 1, 1 });
            vertexData[vtxBaseIdx + 3] = new Vertex(p4, normal, color, new float[] { 0, 1 });

            faces[faceArrIdx + 0] = new Face(vtxBaseIdx + 0, vtxBaseIdx + 1, vtxBaseIdx + 2);
            faces[faceArrIdx + 1] = new Face(vtxBaseIdx + 0, vtxBaseIdx + 2, vtxBaseIdx + 3);
        }


        private void GenTriangle()
        {
            vertexData = new Vertex[3];
            faces = new Face[1];
            vertexData[0] = new Vertex(new float[] { -0.5f, -0.5f, 0.0f }, new float[] { 0.0f, 0.0f, 1.0f }, new float[] { 1.0f, 0.0f, 0.0f, 1.0f }, new float[] { 0.0f, 0.0f });
            vertexData[1] = new Vertex(new float[] { 0.5f, -0.5f, 0.0f }, new float[] { 0.0f, 0.0f, 1.0f }, new float[] { 0.0f, 1.0f, 0.0f, 1.0f }, new float[] { 1.0f, 0.0f });
            vertexData[2] = new Vertex(new float[] { 0.0f, 0.5f, 0.0f }, new float[] { 0.0f, 0.0f, 1.0f }, new float[] { 0.0f, 0.0f, 1.0f, 1.0f }, new float[] { 0.5f, 1.0f });
            faces[0] = new Face(0, 1, 2);
        }

        private void GenQuad()
        {
            vertexData = new Vertex[4];
            faces = new Face[2];
            vertexData[0] = new Vertex(new float[] { -0.5f, -0.5f, 0.0f }, new float[] { 0.0f, 0.0f, 1.0f }, new float[] { 1.0f, 0.0f, 0.0f, 1.0f }, new float[] { 0.0f, 0.0f });
            vertexData[1] = new Vertex(new float[] { 0.5f, -0.5f, 0.0f }, new float[] { 0.0f, 0.0f, 1.0f }, new float[] { 0.0f, 1.0f, 0.0f, 1.0f }, new float[] { 1.0f, 0.0f });
            vertexData[2] = new Vertex(new float[] { 0.5f, 0.5f, 0.0f }, new float[] { 0.0f, 0.0f, 1.0f }, new float[] { 0.0f, 0.0f, 1.0f, 1.0f }, new float[] { 1.0f, 1.0f });
            vertexData[3] = new Vertex(new float[] { -0.5f, 0.5f, 0.0f }, new float[] { 0.0f, 0.0f, 1.0f }, new float[] { 1.0f, 1.0f, 0.0f, 1.0f }, new float[] { 0.0f, 1.0f });
            faces[0] = new Face(0, 1, 2);
            faces[1] = new Face(0, 2, 3);
        }

        private void GenCube()
        {
            vertexData = new Vertex[24]; // 4 vertices per face * 6 faces
            faces = new Face[12];       // 2 triangles per face * 6 faces
            float s = 0.5f;

            AddFace(0, new float[] { -s, -s, s }, new float[] { s, -s, s }, new float[] { s, s, s }, new float[] { -s, s, s }, new float[] { 0, 0, 1 }, new float[] { 1, 0, 0, 1 }); // Front - Red
            AddFace(2, new float[] { s, -s, -s }, new float[] { -s, -s, -s }, new float[] { -s, s, -s }, new float[] { s, s, -s }, new float[] { 0, 0, -1 }, new float[] { 0, 1, 0, 1 }); // Back - Green
            AddFace(4, new float[] { -s, -s, -s }, new float[] { -s, -s, s }, new float[] { -s, s, s }, new float[] { -s, s, -s }, new float[] { -1, 0, 0 }, new float[] { 0, 0, 1, 1 }); // Left - Blue
            AddFace(6, new float[] { s, -s, s }, new float[] { s, -s, -s }, new float[] { s, s, -s }, new float[] { s, s, s }, new float[] { 1, 0, 0 }, new float[] { 1, 1, 0, 1 }); // Right - Yellow
            AddFace(8, new float[] { -s, s, s }, new float[] { s, s, s }, new float[] { s, s, -s }, new float[] { -s, s, -s }, new float[] { 0, 1, 0 }, new float[] { 1, 0, 1, 1 }); // Top - Magenta
            AddFace(10, new float[] { -s, -s, -s }, new float[] { s, -s, -s }, new float[] { s, -s, s }, new float[] { -s, -s, s }, new float[] { 0, -1, 0 }, new float[] { 0, 1, 1, 1 }); // Bottom - Cyan
        }
    }
}