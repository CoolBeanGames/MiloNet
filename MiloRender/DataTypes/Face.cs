// In MiloRender/DataTypes/Face.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiloRender.DataTypes
{
    /// <summary>
    /// Data Structure holds info for the indices pointing to packed vertex data
    /// </summary>
    public class Face
    {
        /// <summary>
        /// The vertex indices that make up this face (typically a triangle).
        /// </summary>
        public uint[] indices = { 0, 0, 0 }; // Changed from float[] to uint[]
        /// <summary>
        /// The normal for the face. This can be calculated or pre-defined.
        /// For flat shading, all vertices of this face would share this normal.
        /// </summary>
        public float[] normal = { 0, 1, 0 }; // Default to up

        /// <summary>
        /// Constructor for a face.
        /// </summary>
        /// <param name="idx1">First vertex index.</param>
        /// <param name="idx2">Second vertex index.</param>
        /// <param name="idx3">Third vertex index.</param>
        public Face(uint idx1, uint idx2, uint idx3)
        {
            indices = new uint[] { idx1, idx2, idx3 };
        }

        /// <summary>
        /// Constructor for a face with a specific normal.
        /// </summary>
        /// <param name="idx1">First vertex index.</param>
        /// <param name="idx2">Second vertex index.</param>
        /// <param name="idx3">Third vertex index.</param>
        /// <param name="faceNormal">The normal vector for this face.</param>
        public Face(uint idx1, uint idx2, uint idx3, float[] faceNormal)
        {
            indices = new uint[] { idx1, idx2, idx3 };
            if (faceNormal != null && faceNormal.Length == 3)
            {
                normal = faceNormal;
            }
            else
            {
                // Default normal or calculate later
                normal = new float[] { 0, 1, 0 }; // Default to up
                Debugger.Debug.LogWarning("Face constructor: Invalid or null faceNormal provided. Defaulting to Y-up.");
            }
        }
    }
}