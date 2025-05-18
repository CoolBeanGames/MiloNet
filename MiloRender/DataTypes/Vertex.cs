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
        public float[] normal = { 0, -1, 0, };
        /// <summary>
        /// the normal for our vertex
        /// </summary>
        public float[] color = { 0, 0, -1, 0 };
        /// <summary>
        /// the UV's for our vertex
        /// </summary>
        public float[] UV = { 0, 0 };
        /// <summary>
        /// the ammount of units in the packed array
        /// </summary>
        public uint stride = 12;
        /// <summary>
        /// the size in bytes for the array
        /// </summary>
        public uint dataSize = sizeof(float) * 12;

        /// <summary>
        /// convert from a class structur to a packed array
        /// </summary>
        /// <returns>the packed array of stride size</returns>
        public float[] GetPackedVertex()
        {
            //pack the float into a packed array for the GPU
            float[] vert = new float[12] // Correct C# array creation and initialization
            {
                position[0],
                position[1],
                position[2],
                normal[0],
                normal[1],
                normal[2],
                color[0],
                color[1],
                color[2],
                color[3],
                UV[0],
                UV[1]
            };
            //return the value
            return vert;
        }
    }
}
