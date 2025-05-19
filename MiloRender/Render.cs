using Debugger;
using MiloRender.DataTypes;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using System;
using System.IO;
using System.Numerics; // Keep for Material's Vector4 if it still uses it, otherwise can remove if material switches to Silk.NET.Maths

namespace MiloRender
{
    public class Render : IDisposable
    {
        public static Render instance;
        private GL _gl;
        private Camera _mainCamera;
        private uint _shaderProgram;

        private const string VertexShaderSource = @"#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec4 aColor;
layout (location = 3) in vec2 aTexCoords;

out vec4 vertexColor;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    gl_Position = projection * view * model * vec4(aPos, 1.0);
    vertexColor = aColor;
}";

        private const string FragmentShaderSource = @"#version 330 core
out vec4 FragColor;
in vec4 vertexColor;

void main()
{
    FragColor = vertexColor;
}";

        public Render(GL gl, Camera camera)
        {
            if (instance != null)
            {
                Debug.LogError("Render instance already created.");
                throw new InvalidOperationException("Render instance already exists.");
            }
            instance = this;

            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _mainCamera = camera ?? throw new ArgumentNullException(nameof(camera));

            Debug.Log("Render Created.");
            InitOpenGL();
            if (!InitShaders())
            {
                Debug.LogError("CRITICAL: Failed to initialize shaders. Rendering will not work.");
                // Consider throwing an exception here or setting a "failed state"
            }
        }

        public void SetCamera(Camera camera)
        {
            _mainCamera = camera ?? throw new ArgumentNullException(nameof(camera));
            Debug.Log("Render: Main camera updated.");
        }

        public void InitOpenGL()
        {
            _gl.ClearColor(0.1f, 0.1f, 0.2f, 1.0f);
            _gl.Enable(EnableCap.DepthTest);
            // _gl.Enable(EnableCap.CullFace); // Keep commented for now unless definitely needed
            Debug.Log("Render.InitOpenGL: States initialized.");
        }

        public bool InitShaders()
        {
            Debug.Log("Render.InitShaders: Initializing shaders...");
            uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vertexShader, VertexShaderSource);
            _gl.CompileShader(vertexShader);
            _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
            {
                Debug.LogError($"Vertex shader compilation failed: {_gl.GetShaderInfoLog(vertexShader)}");
                _gl.DeleteShader(vertexShader);
                return false;
            }

            uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fragmentShader, FragmentShaderSource);
            _gl.CompileShader(fragmentShader);
            _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
            {
                Debug.LogError($"Fragment shader compilation failed: {_gl.GetShaderInfoLog(fragmentShader)}");
                _gl.DeleteShader(vertexShader);
                _gl.DeleteShader(fragmentShader);
                return false;
            }

            _shaderProgram = _gl.CreateProgram();
            _gl.AttachShader(_shaderProgram, vertexShader);
            _gl.AttachShader(_shaderProgram, fragmentShader);
            _gl.LinkProgram(_shaderProgram);
            _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int lStatus);
            if (lStatus != (int)GLEnum.True)
            {
                Debug.LogError($"Shader program linking failed: {_gl.GetProgramInfoLog(_shaderProgram)}");
                _gl.DetachShader(_shaderProgram, vertexShader);
                _gl.DetachShader(_shaderProgram, fragmentShader);
                _gl.DeleteShader(vertexShader);
                _gl.DeleteShader(fragmentShader);
                _gl.DeleteProgram(_shaderProgram);
                _shaderProgram = 0;
                return false;
            }

            _gl.DetachShader(_shaderProgram, vertexShader);
            _gl.DetachShader(_shaderProgram, fragmentShader);
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            _gl.ValidateProgram(_shaderProgram);
            _gl.GetProgram(_shaderProgram, ProgramPropertyARB.ValidateStatus, out int valStatus);
            if (valStatus != (int)GLEnum.True)
            {
                Debug.LogWarning($"Shader program validation failed: {_gl.GetProgramInfoLog(_shaderProgram)}");
            }
            else
            {
                Debug.Log($"Shader program (ID: {_shaderProgram}) compiled, linked, and validated successfully.");
            }

            LoadShaders(); // Effectively activates the program
            return true;
        }

        public void LoadShaders()
        {
            if (_shaderProgram == 0)
            {
                Debug.LogError("Render.LoadShaders: Shader program is not initialized.");
                return;
            }
            _gl.UseProgram(_shaderProgram);
            Debug.Log($"Render.LoadShaders: Using shader program (ID: {_shaderProgram}).");
        }

        public void BeginDraw()
        {
            if (_gl == null) return;
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        }

        // This method now handles potentially unsafe direct matrix data.
        public unsafe void Draw(Mesh m) // Added unsafe keyword
        {
            if (_gl == null || m == null || _shaderProgram == 0 || _mainCamera == null)
            {
                Debug.LogWarning("Render.Draw: Prerequisite missing. Cannot draw.");
                return;
            }

            // Check IsDataReady from VertexBuffer - CS1061 fix depends on VertexBuffer.cs having this public method
            if (m.vertexBuffer == null || !m.vertexBuffer.IsDataReady())
            {
                Debug.LogWarning($"Render.Draw: Mesh (HashCode: {m.GetHashCode()}) vertex buffer data is not ready. Skipping draw.");
                return;
            }

            if (m.GetVAO() == 0)
            {
                Debug.LogWarning($"Render.Draw: Mesh VAO is 0 for mesh (HashCode: {m.GetHashCode()}). Attempting to upload.");
                m.UploadToGPU();
                if (m.GetVAO() == 0)
                {
                    Debug.LogError($"Render.Draw: Mesh VAO is still 0 after upload attempt for mesh (HashCode: {m.GetHashCode()}). Cannot draw.");
                    return;
                }
            }

            _gl.UseProgram(_shaderProgram);
            m.Material?.ApplyMaterial(_gl, _shaderProgram);

            Matrix4X4<float> modelMatrix = m.Transform.ModelMatrix;
            Matrix4X4<float> viewMatrix = _mainCamera.GetViewMatrix();
            Matrix4X4<float> projectionMatrix = _mainCamera.GetProjectionMatrix();

            int modelLoc = _gl.GetUniformLocation(_shaderProgram, "model");
            int viewLoc = _gl.GetUniformLocation(_shaderProgram, "view");
            int projLoc = _gl.GetUniformLocation(_shaderProgram, "projection");

            // Using the ToFloatArray extension method, which is safe.
            // If you were passing pointers directly from matrices, that part would be unsafe.
            if (modelLoc != -1) _gl.UniformMatrix4(modelLoc, 1, false, modelMatrix.ToFloatArray());
            else Debug.LogWarning("Render.Draw: Uniform 'model' not found.");

            if (viewLoc != -1) _gl.UniformMatrix4(viewLoc, 1, false, viewMatrix.ToFloatArray());
            else Debug.LogWarning("Render.Draw: Uniform 'view' not found.");

            if (projLoc != -1) _gl.UniformMatrix4(projLoc, 1, false, projectionMatrix.ToFloatArray());
            else Debug.LogWarning("Render.Draw: Uniform 'projection' not found.");

            _gl.BindVertexArray(m.GetVAO());

            if (m.GetIndexCount() > 0)
            {
                _gl.DrawElements(PrimitiveType.Triangles, (uint)m.GetIndexCount(), DrawElementsType.UnsignedInt, null); // Changed (void*)0 to null for modern GL
            }
            else
            {
                Debug.LogWarning($"Render.Draw: Mesh (HashCode: {m.GetHashCode()}) has no indices to draw.");
            }

            _gl.BindVertexArray(0);
        }

        public void EndDraw()
        {
            // Buffer swapping usually handled by the windowing system in main loop
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Managed resources
            }

            if (_gl != null && _shaderProgram != 0)
            {
                _gl.DeleteProgram(_shaderProgram);
                Debug.Log($"Render: Shader program (ID: {_shaderProgram}) deleted.");
                _shaderProgram = 0;
            }
            _gl = null;

            if (instance == this)
            {
                instance = null;
            }
            Debug.Log("Render: Disposed.");
        }
        ~Render()
        {
            Dispose(false);
        }
    }

    public static class MatrixExtensions // Keep this extension method
    {
        public static float[] ToFloatArray(this Matrix4X4<float> matrix)
        {
            return new float[] {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            };
        }
    }
}