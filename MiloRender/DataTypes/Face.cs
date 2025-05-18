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
    internal class Face
    {
        /// <summary>
        /// the vertices that make up a face
        /// </summary>
        float[] indices = { 0, 0, 0 };
        /// <summary>
        /// the normal for the face
        /// </summary>
        float[] normal = { 0, 0, 0 };
    }
}
