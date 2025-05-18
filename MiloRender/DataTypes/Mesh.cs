using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiloRender.DataTypes
{
    public class Mesh : IDisposable
    {
        public VertexBuffer vertexBuffer; // <---- temporaririly public untwil we can import glb
                                          //todo: add material
                                          //todo: add transform (position, rotation, scale matrices)
        private bool _isDisposed = false; // Ensure this field exists

        public void Dispose() // <<< This is the public method Actor.cs is looking for
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
        }
        
            public Mesh(Primitive primitiveType) 
        {
            vertexBuffer = new VertexBuffer(primitiveType);
        }

        public Mesh(float[] vertices, uint[] indices)
        {
            vertexBuffer = new VertexBuffer(vertices, indices);
        }

        public Mesh(Vertex[] vertices, Face[] faces)
        {
            vertexBuffer = new VertexBuffer(vertices, faces);
        }

        //draw this mesh to the screen
        public void Draw()
        {
            Render.instance.Draw(this);
        }

        public void UploadToGPU()
        {
            //upload vertex data to the gpu
        }
    }
}
