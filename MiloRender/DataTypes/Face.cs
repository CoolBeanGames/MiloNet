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
        /// the vertices that make up a face
        /// </summary>
        public float[] indices = { 0, 0, 0 };
        /// <summary>
        /// the normal for the face
        /// </summary>
        public float[] normal = { 0, 0, 0 };
    }
}
