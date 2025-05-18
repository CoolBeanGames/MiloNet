// In MiloRender/DataTypes/Vertex.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiloRender.DataTypes
{
    /// <summary>
    /// data struture for a single vertex
    /// </summary>
    public class Vertex
    {
        /// <summary>
        /// the position in space for the vertex relative to the model
        /// </summary>
        public float[] position = { 0, 0, 0, };
        /// <summary>
        /// the normal for the vertex
        /// </summary>
        public float[] normal = { 0, 1, 0, }; // Default to up
        /// <summary>
        /// the color for our vertex (RGBA)
        /// </summary>
        public float[] color = { 1, 1, 1, 1 }; // Default to white
        /// <summary>
        /// the UV's for our vertex
        /// </summary>
        public float[] UV = { 0, 0 };
        /// <summary>
        /// the ammount of units in the packed array (Position(3) + Normal(3) + Color(4) + UV(2) = 12 floats)
        /// </summary>
        public const uint stride = 12; // Made const as it's fixed
        /// <summary>
        /// the size in bytes for the array
        /// </summary>
        public static readonly uint dataSize = sizeof(float) * stride; // Made static readonly

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Vertex() { }

        /// <summary>
        /// Constructor for a vertex.
        /// </summary>
        /// <param name="pos">Position (x,y,z)</param>
        /// <param name="norm">Normal (x,y,z)</param>
        /// <param name="col">Color (r,g,b,a)</param>
        /// <param name="uv">UV coordinates (u,v)</param>
        public Vertex(float[] pos, float[] norm, float[] col, float[] uvCoords)
        {
            if (pos != null && pos.Length == 3) position = pos;
            else Debugger.Debug.LogWarning("Vertex Constructor: Invalid position data provided.");

            if (norm != null && norm.Length == 3) normal = norm;
            else Debugger.Debug.LogWarning("Vertex Constructor: Invalid normal data provided.");

            if (col != null && col.Length == 4) color = col;
            else Debugger.Debug.LogWarning("Vertex Constructor: Invalid color data provided.");

            if (uvCoords != null && uvCoords.Length == 2) UV = uvCoords;
            else Debugger.Debug.LogWarning("Vertex Constructor: Invalid UV data provided.");
        }


        /// <summary>
        /// convert from a class structur to a packed array
        /// </summary>
        /// <returns>the packed array of stride size</returns>
        public float[] GetPackedVertex()
        {
            //pack the float into a packed array for the GPU
            float[] vert = new float[12]
            {
                position[0], position[1], position[2],
                normal[0], normal[1], normal[2],
                color[0], color[1], color[2], color[3],
                UV[0], UV[1]
            };
            //return the value
            return vert;
        }
    }
}