using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiloRender.DataTypes
{
    /// <summary>
    /// holds all of the geometry data for a single model
    /// </summary>
    public class VertexBuffer
    {
        /// <summary>
        /// generate a vertex buffer of the selected unit scale primitive
        /// </summary>
        /// <param name="type">the type of primitive to generate</param>
        public VertexBuffer(Primitive type)
        {
            //call the appropriate function based on 
            //supplied primitive type to fill all of the
            //data with the correct values to
            //spawn a 1x1 unit cube, quad, or triangle
            //complete with proper normals positions
            //and a unique color per corner
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
            }
            FlushData();
        }

        /// <summary>
        /// generate a vertex buffer from a list of vertex positions, colors,normals, uvs and 
        /// indices (assumes a stride of 12)
        /// </summary>
        /// <param name="vertices">the vertex data</param>
        /// <param name="indices">the index data</param>
        public VertexBuffer(float[] vertices, float[] indices)
        {

        }

        /// <summary>
        /// generate a vertex buffer from vertex and face lists
        /// </summary>
        /// <param name="vertices">the list of vertices</param>
        /// <param name="faces">the list of faces</param>
        public VertexBuffer(Vertex[] vertices, Face[] faces)
        {

        }
        /// <summary>
        /// an array of GPU readable vertices
        /// </summary>
        public float [] vertices; // <---- Feed this data into the GPU for Rendering
        /// <summary>
        /// an array of GPU readable indices
        /// </summary>
        public float [] indices;  // <------ Feed this data into the GPU for Rendering
        /// <summary>
        /// an array of human readable faces
        /// </summary>
        public Face [] faces;
        /// <summary>
        /// an array of human readable vertex data
        /// </summary>
        public Vertex [] vertexData;

        /// <summary>
        /// move data from VertexData and faces to vertices and indices
        /// </summary>
        public void FlushData()
        {

        }

        /// <summary>
        /// fill the data with a Cube
        /// </summary>
        private void GenCube()
        {

        }

        /// <summary>
        /// fill the data with a quad
        /// </summary>
        private void GenQuad()
        {

        }

        /// <summary>
        /// fill the data with a triangle
        /// </summary>
        private void GenTriangle()
        {
        }
    }
}

/// <summary>
/// an enum for what type of primative we want to make
/// </summary>
public enum Primitive
{
    Cube,
    Quad,
    Triangle
}