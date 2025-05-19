// In MiloRender/Render.cs
using Debugger;
using MiloRender.DataTypes; // For Camera
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using System;
using System.IO; // <--- Added for File and Path operations
// System.Numerics might still be used by Material.cs, check there if it can be removed.

namespace MiloRender
{
    public class Render : IDisposable
    {
        public static Render instance;
        private GL _gl;
        private Camera _mainCamera;
        private uint _shaderProgram;

        // Remove old hardcoded shader strings
        // private const string VertexShaderSource = @"...";
        // private const string FragmentShaderSource = @"...";

        // Define paths for external shader files
        private const string ShaderDirectory = "Shaders"; // Relative to execution directory
        private const string VertexShaderFileName = "Vertex.glsl";
        private const string FragmentShaderFileName = "fragment.glsl";


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
            if (!InitShaders()) // This will now load from files
            {
                Debug.LogError("CRITICAL: Failed to initialize shaders from files. Rendering will not work.");
                // Consider throwing an exception or setting a "failed state"
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
            Debug.Log("Render.InitOpenGL: States initialized.");
        }

        private string LoadShaderSource(string fileName)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string shaderFilePath = Path.Combine(baseDirectory, ShaderDirectory, fileName);

            try
            {
                Debug.Log($"Render.LoadShaderSource: Attempting to load shader file: {shaderFilePath}");
                string shaderSource = File.ReadAllText(shaderFilePath);
                if (string.IsNullOrWhiteSpace(shaderSource))
                {
                    Debug.LogError($"Render.LoadShaderSource: Shader file '{shaderFilePath}' is empty or whitespace.");
                    return null;
                }
                Debug.Log($"Render.LoadShaderSource: Successfully loaded shader file: {fileName}");
                return shaderSource;
            }
            catch (FileNotFoundException)
            {
                Debug.LogError($"Render.LoadShaderSource: Shader file not found: {shaderFilePath}");
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                Debug.LogError($"Render.LoadShaderSource: Shader directory not found for path: {shaderFilePath}. Ensure '{ShaderDirectory}' exists in output.");
                return null;
            }
            catch (IOException ex)
            {
                Debug.LogError($"Render.LoadShaderSource: IO Error reading shader file '{shaderFilePath}': {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Render.LoadShaderSource: Unexpected error loading shader '{shaderFilePath}': {ex.Message}");
                return null;
            }
        }

        public bool InitShaders()
        {
            Debug.Log("Render.InitShaders: Initializing shaders from files...");

            string vertexShaderSource = LoadShaderSource(VertexShaderFileName);
            string fragmentShaderSource = LoadShaderSource(FragmentShaderFileName);

            if (string.IsNullOrEmpty(vertexShaderSource) || string.IsNullOrEmpty(fragmentShaderSource))
            {
                Debug.LogError("Render.InitShaders: Failed to load vertex or fragment shader source from files.");
                return false;
            }

            uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vertexShader, vertexShaderSource);
            _gl.CompileShader(vertexShader);
            _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
            {
                Debug.LogError($"Vertex shader compilation failed: {_gl.GetShaderInfoLog(vertexShader)}");
                _gl.DeleteShader(vertexShader);
                return false;
            }

            uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fragmentShader, fragmentShaderSource);
            _gl.CompileShader(fragmentShader);
            _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
            {
                Debug.LogError($"Fragment shader compilation failed: {_gl.GetShaderInfoLog(fragmentShader)}");
                _gl.DeleteShader(vertexShader); // Clean up vertex shader if fragment fails
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
                // Detach and delete everything on failure
                _gl.DetachShader(_shaderProgram, vertexShader);
                _gl.DetachShader(_shaderProgram, fragmentShader);
                _gl.DeleteShader(vertexShader);
                _gl.DeleteShader(fragmentShader);
                _gl.DeleteProgram(_shaderProgram);
                _shaderProgram = 0;
                return false;
            }

            // Detach and delete individual shaders after successful linking
            _gl.DetachShader(_shaderProgram, vertexShader);
            _gl.DetachShader(_shaderProgram, fragmentShader);
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            _gl.ValidateProgram(_shaderProgram);
            _gl.GetProgram(_shaderProgram, ProgramPropertyARB.ValidateStatus, out int valStatus);
            if (valStatus != (int)GLEnum.True)
            {
                // This is often not a critical error but good to know
                Debug.LogWarning($"Shader program validation failed: {_gl.GetProgramInfoLog(_shaderProgram)}");
            }
            else
            {
                Debug.Log($"Shader program (ID: {_shaderProgram}) compiled, linked, and validated successfully from files.");
            }

            LoadShaders(); // Effectively activates the program
            return true;
        }

        public void LoadShaders() // This method now just activates the program
        {
            if (_shaderProgram == 0)
            {
                Debug.LogError("Render.LoadShaders: Shader program is not initialized (ID is 0).");
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

        public unsafe void Draw(Mesh m)
        {
            if (_gl == null || m == null || _shaderProgram == 0 || _mainCamera == null)
            {
                // Debug.LogWarning("Render.Draw: Prerequisite missing (GL, Mesh, ShaderProgram, or MainCamera is null). Cannot draw.");
                if (_shaderProgram == 0) Debug.LogWarning("Render.Draw: Shader program is 0.");
                return;
            }

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

            _gl.UseProgram(_shaderProgram); // Ensure program is used before setting uniforms
            m.Material?.ApplyMaterial(_gl, _shaderProgram);

            Matrix4X4<float> modelMatrix = m.Transform.ModelMatrix;
            Matrix4X4<float> viewMatrix = _mainCamera.GetViewMatrix();
            Matrix4X4<float> projectionMatrix = _mainCamera.GetProjectionMatrix();

            int modelLoc = _gl.GetUniformLocation(_shaderProgram, "model");
            int viewLoc = _gl.GetUniformLocation(_shaderProgram, "view");
            int projLoc = _gl.GetUniformLocation(_shaderProgram, "projection");

            if (modelLoc != -1) _gl.UniformMatrix4(modelLoc, 1, false, modelMatrix.ToFloatArray());
            //else Debug.LogWarning("Render.Draw: Uniform 'model' not found in shader."); // Can be noisy

            if (viewLoc != -1) _gl.UniformMatrix4(viewLoc, 1, false, viewMatrix.ToFloatArray());
            //else Debug.LogWarning("Render.Draw: Uniform 'view' not found in shader.");

            if (projLoc != -1) _gl.UniformMatrix4(projLoc, 1, false, projectionMatrix.ToFloatArray());
            //else Debug.LogWarning("Render.Draw: Uniform 'projection' not found in shader.");


            _gl.BindVertexArray(m.GetVAO());

            if (m.GetIndexCount() > 0)
            {
                _gl.DrawElements(PrimitiveType.Triangles, (uint)m.GetIndexCount(), DrawElementsType.UnsignedInt, null);
            }
            else
            {
                Debug.LogWarning($"Render.Draw: Mesh (HashCode: {m.GetHashCode()}) has no indices to draw.");
            }

            _gl.BindVertexArray(0); // Unbind VAO
            // _gl.UseProgram(0); // Unbind shader program - Optional, good practice if mixing shaders often
        }

        public Camera GetCurrentCamera() // New method
        {
            return _mainCamera;
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
                // Dispose managed resources if any
            }

            if (_gl != null && _shaderProgram != 0)
            {
                _gl.DeleteProgram(_shaderProgram);
                Debug.Log($"Render: Shader program (ID: {_shaderProgram}) deleted.");
                _shaderProgram = 0;
            }
            _gl = null; // Release GL context reference

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

    // MatrixExtensions class remains the same
    public static class MatrixExtensions
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