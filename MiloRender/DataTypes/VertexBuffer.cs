// In MiloRender/DataTypes/VertexBuffer.cs
using Debugger; // Make sure your Debugger namespace is accessible
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiloRender.DataTypes
{
    /// <summary>
    /// Holds all of the geometry data for a single model, 
    /// both in human-readable format (Vertex objects, Face objects)
    /// and GPU-ready format (packed float arrays for vertices and uint arrays for indices).
    /// </summary>
    public class VertexBuffer
    {
        /// <summary>
        /// An array of GPU readable packed vertex data (Position, Normal, Color, UV).
        /// </summary>
        public float[] vertices; // Feed this data into the GPU for Rendering

        /// <summary>
        /// An array of GPU readable indices that define the geometry's triangles.
        /// </summary>
        public uint[] indices;  // Feed this data into the GPU for Rendering

        /// <summary>
        /// An array of human-readable Face objects.
        /// </summary>
        public Face[] faces;

        /// <summary>
        /// An array of human-readable Vertex objects.
        /// </summary>
        public Vertex[] vertexData;

        /// <summary>
        /// Generates a vertex buffer for a selected unit scale primitive.
        /// </summary>
        /// <param name="type">The type of primitive to generate.</param>
        public VertexBuffer(Primitive type)
        {
            Debug.Log($"VertexBuffer: Generating primitive type: {type}");
            switch (type)
            {
                case Primitive.Triangle:
                    GenTriangle();
                    break;
                case Primitive.Cube:
                    GenCube();
                    break;
                case Primitive.Quad:
                    GenQuad();
                    break;
                default:
                    Debug.LogError($"VertexBuffer: Unknown primitive type requested: {type}");
                    vertexData = new Vertex[0];
                    faces = new Face[0];
                    break;
            }
            FlushData(); // Convert Vertex/Face data to GPU-ready arrays
        }

        /// <summary>
        /// Generates a vertex buffer from raw float arrays for vertices and uint arrays for indices.
        /// Note: This constructor does not populate the human-readable 'vertexData' and 'faces' arrays.
        /// </summary>
        /// <param name="rawVertices">The packed vertex data (stride of 12 floats: pos(3), norm(3), col(4), uv(2)).</param>
        /// <param name="rawIndices">The index data defining triangles.</param>
        public VertexBuffer(float[] rawVertices, uint[] rawIndices)
        {
            if (rawVertices == null || rawIndices == null)
            {
                Debug.LogError("VertexBuffer Constructor: Raw vertices or indices cannot be null.");
                // Initialize to empty to prevent null reference errors downstream
                this.vertices = new float[0];
                this.indices = new uint[0];
                this.vertexData = new Vertex[0]; // Explicitly state they are empty
                this.faces = new Face[0];       // Explicitly state they are empty
                return;
            }

            if (rawVertices.Length % Vertex.stride != 0)
            {
                Debug.LogWarning($"VertexBuffer Constructor: Raw vertices array length ({rawVertices.Length}) is not a multiple of vertex stride ({Vertex.stride}). Data might be misinterpreted.");
            }

            this.vertices = rawVertices;
            this.indices = rawIndices;
            // Human-readable arrays are not populated by this constructor.
            // We could try to reconstruct them, but it's complex and lossy (e.g., shared vertices).
            this.vertexData = new Vertex[0]; // Or null, decide on convention
            this.faces = new Face[0];       // Or null
            Debug.Log("VertexBuffer: Created from raw vertex and index data. Human-readable arrays (vertexData, faces) are not populated.");
        }

        /// <summary>
        /// Generates a vertex buffer from lists of Vertex and Face objects.
        /// </summary>
        /// <param name="sourceVertices">The list of Vertex objects.</param>
        /// <param name="sourceFaces">The list of Face objects.</param>
        public VertexBuffer(Vertex[] sourceVertices, Face[] sourceFaces)
        {
            if (sourceVertices == null || sourceFaces == null)
            {
                Debug.LogError("VertexBuffer Constructor: Source vertices or faces cannot be null.");
                // Initialize to empty to prevent null reference errors downstream
                this.vertexData = new Vertex[0];
                this.faces = new Face[0];
                this.vertices = new float[0];
                this.indices = new uint[0];
                return;
            }

            this.vertexData = sourceVertices;
            this.faces = sourceFaces;
            FlushData(); // Convert to GPU-ready arrays
            Debug.Log($"VertexBuffer: Created from {vertexData.Length} vertices and {faces.Length} faces.");
        }

        /// <summary>
        /// Converts the 'vertexData' and 'faces' arrays into the GPU-ready
        /// 'vertices' (packed float array) and 'indices' (uint array).
        /// </summary>
        public void FlushData()
        {
            if (vertexData == null || faces == null)
            {
                Debug.LogWarning("FlushData: vertexData or faces is null. Cannot flush.");
                // Ensure vertices and indices are not null if they haven't been initialized
                vertices = vertices ?? new float[0];
                indices = indices ?? new uint[0];
                return;
            }
            if (vertexData.Length == 0)
            {
                Debug.LogWarning("FlushData: No vertex data to flush.");
                vertices = new float[0];
                indices = new uint[0];
                return;
            }


            // Allocate memory for the GPU-ready arrays
            // Each vertex has 'stride' float components.
            vertices = new float[vertexData.Length * Vertex.stride];
            // Each face defines one triangle (3 indices).
            indices = new uint[faces.Length * 3];

            // Populate the 'vertices' array
            for (int i = 0; i < vertexData.Length; i++)
            {
                if (vertexData[i] == null)
                {
                    Debug.LogError($"FlushData: Vertex at index {i} is null. Skipping.");
                    // Fill with zeros or a default vertex to avoid downstream issues, and ensure correct array length
                    float[] defaultPackedVert = new Vertex().GetPackedVertex();
                    Array.Copy(defaultPackedVert, 0, vertices, i * (int)Vertex.stride, (int)Vertex.stride);
                    continue;
                }
                float[] packedVert = vertexData[i].GetPackedVertex();
                Array.Copy(packedVert, 0, vertices, i * (int)Vertex.stride, (int)Vertex.stride);
            }

            // Populate the 'indices' array
            int currentIndex = 0;
            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] == null || faces[i].indices == null)
                {
                    Debug.LogError($"FlushData: Face or its indices at index {i} is null. Skipping face.");
                    // Potentially fill with degenerate triangle indices if strict array length needed for all faces
                    // For now, this will result in a shorter indices array than expected if faces are skipped
                    continue;
                }
                if (faces[i].indices.Length != 3)
                {
                    Debug.LogWarning($"FlushData: Face at index {i} does not have 3 indices. Skipping face.");
                    continue;
                }

                indices[currentIndex++] = faces[i].indices[0];
                indices[currentIndex++] = faces[i].indices[1];
                indices[currentIndex++] = faces[i].indices[2];
            }

            // If faces were skipped, the indices array might be oversized. Resize if necessary,
            // though this is inefficient. Better to ensure valid data upstream.
            if (currentIndex < indices.Length)
            {
                Array.Resize(ref indices, currentIndex);
                Debug.LogWarning($"FlushData: Indices array resized due to skipped faces. Original: {faces.Length * 3}, Final: {currentIndex}");
            }

            Debug.Log($"FlushData: Data flushed. Vertices: {vertices.Length / Vertex.stride}, Indices: {indices.Length}");
        }

        /// <summary>
        /// Fills the vertexData and faces with a single, flat-shaded triangle.
        /// </summary>
        private void GenTriangle()
        {
            vertexData = new Vertex[3];
            faces = new Face[1];

            // Define vertices
            // Positions for a simple triangle
            // UVs to map a texture simply
            // Normals are all pointing up (0,1,0) for this basic example or forward (0,0,1)
            // Colors are distinct for each vertex
            vertexData[0] = new Vertex( // Bottom-left
                new float[] { -0.5f, -0.5f, 0.0f },
                new float[] { 0.0f, 0.0f, 1.0f },  // Normal facing camera
                new float[] { 1.0f, 0.0f, 0.0f, 1.0f }, // Red
                new float[] { 0.0f, 0.0f }            // UV
            );
            vertexData[1] = new Vertex( // Bottom-right
                new float[] { 0.5f, -0.5f, 0.0f },
                new float[] { 0.0f, 0.0f, 1.0f },
                new float[] { 0.0f, 1.0f, 0.0f, 1.0f }, // Green
                new float[] { 1.0f, 0.0f }
            );
            vertexData[2] = new Vertex( // Top-middle
                new float[] { 0.0f, 0.5f, 0.0f },
                new float[] { 0.0f, 0.0f, 1.0f },
                new float[] { 0.0f, 0.0f, 1.0f, 1.0f }, // Blue
                new float[] { 0.5f, 1.0f }
            );

            // Define faces (indices)
            faces[0] = new Face(0, 1, 2); // Points to the vertices defined above
        }

        /// <summary>
        /// Fills the vertexData and faces with a single, flat-shaded quad (composed of two triangles).
        /// </summary>
        private void GenQuad()
        {
            vertexData = new Vertex[4]; // 4 vertices for a quad
            faces = new Face[2];        // 2 triangles for a quad

            // Define vertices for a quad
            // Normals facing camera, distinct colors, standard UVs
            vertexData[0] = new Vertex( // Bottom-left
                new float[] { -0.5f, -0.5f, 0.0f },
                new float[] { 0.0f, 0.0f, 1.0f },
                new float[] { 1.0f, 0.0f, 0.0f, 1.0f }, // Red
                new float[] { 0.0f, 0.0f }
            );
            vertexData[1] = new Vertex( // Bottom-right
                new float[] { 0.5f, -0.5f, 0.0f },
                new float[] { 0.0f, 0.0f, 1.0f },
                new float[] { 0.0f, 1.0f, 0.0f, 1.0f }, // Green
                new float[] { 1.0f, 0.0f }
            );
            vertexData[2] = new Vertex( // Top-right
                new float[] { 0.5f, 0.5f, 0.0f },
                new float[] { 0.0f, 0.0f, 1.0f },
                new float[] { 0.0f, 0.0f, 1.0f, 1.0f }, // Blue
                new float[] { 1.0f, 1.0f }
            );
            vertexData[3] = new Vertex( // Top-left
                new float[] { -0.5f, 0.5f, 0.0f },
                new float[] { 0.0f, 0.0f, 1.0f },
                new float[] { 1.0f, 1.0f, 0.0f, 1.0f }, // Yellow
                new float[] { 0.0f, 1.0f }
            );

            // Define faces (triangles)
            faces[0] = new Face(0, 1, 2); // First triangle (bottom-left, bottom-right, top-right)
            faces[1] = new Face(0, 2, 3); // Second triangle (bottom-left, top-right, top-left)
        }

        /// <summary>
        /// Fills the vertexData and faces with a flat-shaded cube.
        /// For flat shading, vertices are duplicated for each face to allow unique normals per face.
        /// This means 24 vertices (4 vertices per face * 6 faces).
        /// </summary>
        private void GenCube()
        {
            vertexData = new Vertex[24]; // 4 vertices per face * 6 faces = 24 vertices
            faces = new Face[12];       // 2 triangles per face * 6 faces = 12 faces

            float s = 0.5f; // Half-size for a unit cube centered at origin

            // Define vertices and faces for each side of the cube
            // Each face will have 4 vertices, and its own normal.
            // UVs are set for each face to cover 0,0 to 1,1.
            // Colors are set per-face for distinct face colors.

            // Front face (+Z)
            float[] frontNormal = { 0, 0, 1 };
            float[] frontColor = { 1, 0, 0, 1 }; // Red
            vertexData[0] = new Vertex(new float[] { -s, -s, s }, frontNormal, frontColor, new float[] { 0, 0 });
            vertexData[1] = new Vertex(new float[] { s, -s, s }, frontNormal, frontColor, new float[] { 1, 0 });
            vertexData[2] = new Vertex(new float[] { s, s, s }, frontNormal, frontColor, new float[] { 1, 1 });
            vertexData[3] = new Vertex(new float[] { -s, s, s }, frontNormal, frontColor, new float[] { 0, 1 });
            faces[0] = new Face(0, 1, 2);
            faces[1] = new Face(0, 2, 3);

            // Back face (-Z)
            float[] backNormal = { 0, 0, -1 };
            float[] backColor = { 0, 1, 0, 1 }; // Green
            vertexData[4] = new Vertex(new float[] { -s, -s, -s }, backNormal, backColor, new float[] { 1, 0 }); // Note UVs for back
            vertexData[5] = new Vertex(new float[] { -s, s, -s }, backNormal, backColor, new float[] { 1, 1 });
            vertexData[6] = new Vertex(new float[] { s, s, -s }, backNormal, backColor, new float[] { 0, 1 });
            vertexData[7] = new Vertex(new float[] { s, -s, -s }, backNormal, backColor, new float[] { 0, 0 });
            faces[2] = new Face(4, 5, 6);
            faces[3] = new Face(4, 6, 7);

            // Left face (-X)
            float[] leftNormal = { -1, 0, 0 };
            float[] leftColor = { 0, 0, 1, 1 }; // Blue
            vertexData[8] = new Vertex(new float[] { -s, -s, -s }, leftNormal, leftColor, new float[] { 0, 0 });
            vertexData[9] = new Vertex(new float[] { -s, s, -s }, leftNormal, leftColor, new float[] { 0, 1 });
            vertexData[10] = new Vertex(new float[] { -s, s, s }, leftNormal, leftColor, new float[] { 1, 1 });
            vertexData[11] = new Vertex(new float[] { -s, -s, s }, leftNormal, leftColor, new float[] { 1, 0 });
            faces[4] = new Face(8, 9, 10);
            faces[5] = new Face(8, 10, 11);

            // Right face (+X)
            float[] rightNormal = { 1, 0, 0 };
            float[] rightColor = { 1, 1, 0, 1 }; // Yellow
            vertexData[12] = new Vertex(new float[] { s, -s, s }, rightNormal, rightColor, new float[] { 0, 0 });
            vertexData[13] = new Vertex(new float[] { s, s, s }, rightNormal, rightColor, new float[] { 0, 1 });
            vertexData[14] = new Vertex(new float[] { s, s, -s }, rightNormal, rightColor, new float[] { 1, 1 });
            vertexData[15] = new Vertex(new float[] { s, -s, -s }, rightNormal, rightColor, new float[] { 1, 0 });
            faces[6] = new Face(12, 13, 14);
            faces[7] = new Face(12, 14, 15);

            // Top face (+Y)
            float[] topNormal = { 0, 1, 0 };
            float[] topColor = { 1, 0, 1, 1 }; // Magenta
            vertexData[16] = new Vertex(new float[] { -s, s, s }, topNormal, topColor, new float[] { 0, 0 });
            vertexData[17] = new Vertex(new float[] { s, s, s }, topNormal, topColor, new float[] { 1, 0 });
            vertexData[18] = new Vertex(new float[] { s, s, -s }, topNormal, topColor, new float[] { 1, 1 });
            vertexData[19] = new Vertex(new float[] { -s, s, -s }, topNormal, topColor, new float[] { 0, 1 });
            faces[8] = new Face(16, 17, 18);
            faces[9] = new Face(16, 18, 19);

            // Bottom face (-Y)
            float[] bottomNormal = { 0, -1, 0 };
            float[] bottomColor = { 0, 1, 1, 1 }; // Cyan
            vertexData[20] = new Vertex(new float[] { -s, -s, -s }, bottomNormal, bottomColor, new float[] { 0, 0 });
            vertexData[21] = new Vertex(new float[] { s, -s, -s }, bottomNormal, bottomColor, new float[] { 1, 0 });
            vertexData[22] = new Vertex(new float[] { s, -s, s }, bottomNormal, bottomColor, new float[] { 1, 1 });
            vertexData[23] = new Vertex(new float[] { -s, -s, s }, bottomNormal, bottomColor, new float[] { 0, 1 });
            faces[10] = new Face(20, 21, 22);
            faces[11] = new Face(20, 22, 23);
        }
    }

    
}