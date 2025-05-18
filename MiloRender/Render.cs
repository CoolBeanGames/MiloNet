using Debugger;
using MiloRender.DataTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiloRender
{
    public class Render
    {
        public static Render instance;
        public Render() 
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Debug.LogError("Render already created");
                return;
            }
                Debug.Log("Render Created");
            InitOpenGL();
            InitShaders();
        }

        public void InitOpenGL()
        {
            Debug.Log("OpenGL Initialized");
        }

        public void InitShaders()
        {
            Debug.Log("Shaders Initialized");
        }

        public void LoadShaders()
        {
            Debug.Log("Shaders Loaded");
        }

        public void BeginDraw()
        {

        }

        public void Draw(Mesh m)
        {

        }

        public void EndDraw()
        {

        }

    }
}
