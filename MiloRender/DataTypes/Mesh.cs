using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiloRender.DataTypes
{
    public class Mesh
    {
        public VertexBuffer vertexBuffer; // <---- temporaririly public untwil we can import glb
        //todo: add material
        //todo: add transform (position, rotation, scale matrices)
        public Mesh() 
        { 
        }

        public void Draw()
        {
            Render.instance.Draw(this);
        }
    }
}
