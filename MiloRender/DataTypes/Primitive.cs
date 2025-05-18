using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiloRender.DataTypes
{
    /// <summary>
    /// An enum for what type of primitive we want to make.
    /// This was previously outside the class; ensure it's accessible or move it if preferred.
    /// Keeping it here for now as it's closely tied to VertexBuffer construction.
    /// </summary>
    public enum Primitive // Made public for accessibility from other parts of the engine
    {
        Cube,
        Quad,
        Triangle
    }
}
