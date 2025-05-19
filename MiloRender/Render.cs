// In MiloRender/Render.cs
using Debugger;
using MiloRender.DataTypes; // For Camera, Light, LightType etc.
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using System;
using System.IO;
using System.Numerics; // For System.Numerics.Vector3 used by Light.Color for convenience

namespace MiloRender
{
    public class Render : IDisposable
    {
        public static Render instance;
        private GL _gl;
        private Camera _mainCamera;
        private uint _shaderProgram;

        // Shader file names - Assuming you renamed them or will use these new names
        private const string ShaderDirectory = "Shaders";
        private const string VertexShaderFileName = "Vertex.glsl";
        private const string FragmentShaderFileName = "fragment.glsl";

        // Uniform locations
        private int _modelMatrixLocation;
        private int _viewMatrixLocation;
        private int _projectionMatrixLocation;
        private int _albedoTextureLocation;
        private int _baseColorTintLocation;

        // New Lighting Uniform Locations
        private int _lightTypeLocation;
        private int _lightPositionWorldLocation;
        private int _lightDirectionWorldLocation;
        private int _lightColorLocation;
        private int _lightIntensityLocation;
        private int _lightRangeLocation;
        private int _spotCutOffCosineLocation;
        private int _spotOuterCutOffCosineLocation;
        private int _ambientLightFactorLocation;
        // private int _cameraPositionWorldLocation; // For later if needed

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
            if (!InitShadersAndGetUniformLocations())
            {
                Debug.LogError("CRITICAL: Failed to initialize shaders or get uniform locations. Rendering will not work.");
                // Consider throwing an exception or setting a "failed state"
            }
        }

        public void SetCamera(Camera camera)
        {
            _mainCamera = camera ?? throw new ArgumentNullException(nameof(camera));
            Debug.Log($"Render: Main camera updated (Pos: {_mainCamera.Transform.LocalPosition}).");
        }

        public Camera GetCurrentCamera()
        {
            return _mainCamera;
        }

        public void InitOpenGL()
        {
            _gl.ClearColor(0.1f, 0.1f, 0.2f, 1.0f);
            _gl.Enable(EnableCap.DepthTest);
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(GLEnum.Back); // CORRECTED: Use GLEnum.Back
            Debug.Log("Render.InitOpenGL: States initialized (DepthTest, CullFace).");
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

        private bool InitShadersAndGetUniformLocations()
        {
            Debug.Log("Render.InitShadersAndGetUniformLocations: Initializing shaders...");
            string vertexShaderSource = LoadShaderSource(VertexShaderFileName);
            string fragmentShaderSource = LoadShaderSource(FragmentShaderFileName);

            if (string.IsNullOrEmpty(vertexShaderSource) || string.IsNullOrEmpty(fragmentShaderSource))
            {
                Debug.LogError("Render.InitShadersAndGetUniformLocations: Failed to load vertex or fragment shader source.");
                return false;
            }

            uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vertexShader, vertexShaderSource);
            _gl.CompileShader(vertexShader);
            _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
            {
                Debug.LogError($"Vertex shader ('{VertexShaderFileName}') compilation failed: {_gl.GetShaderInfoLog(vertexShader)}");
                _gl.DeleteShader(vertexShader); return false;
            }

            uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fragmentShader, fragmentShaderSource);
            _gl.CompileShader(fragmentShader);
            _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
            {
                Debug.LogError($"Fragment shader ('{FragmentShaderFileName}') compilation failed: {_gl.GetShaderInfoLog(fragmentShader)}");
                _gl.DeleteShader(vertexShader); _gl.DeleteShader(fragmentShader); return false;
            }

            _shaderProgram = _gl.CreateProgram();
            _gl.AttachShader(_shaderProgram, vertexShader);
            _gl.AttachShader(_shaderProgram, fragmentShader);
            _gl.LinkProgram(_shaderProgram);
            _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out int lStatus);
            if (lStatus != (int)GLEnum.True)
            {
                Debug.LogError($"Shader program linking failed: {_gl.GetProgramInfoLog(_shaderProgram)}");
                _gl.DetachShader(_shaderProgram, vertexShader); _gl.DetachShader(_shaderProgram, fragmentShader);
                _gl.DeleteShader(vertexShader); _gl.DeleteShader(fragmentShader);
                _gl.DeleteProgram(_shaderProgram); _shaderProgram = 0; return false;
            }

            _gl.DetachShader(_shaderProgram, vertexShader);
            _gl.DetachShader(_shaderProgram, fragmentShader);
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            _gl.ValidateProgram(_shaderProgram);
            _gl.GetProgram(_shaderProgram, ProgramPropertyARB.ValidateStatus, out int valStatus);
            if (valStatus != (int)GLEnum.True) Debug.LogWarning($"Shader program validation failed: {_gl.GetProgramInfoLog(_shaderProgram)}");
            else Debug.Log($"Shader program (ID: {_shaderProgram}) compiled, linked, and validated successfully.");

            _gl.UseProgram(_shaderProgram);

            _modelMatrixLocation = _gl.GetUniformLocation(_shaderProgram, "model");
            _viewMatrixLocation = _gl.GetUniformLocation(_shaderProgram, "view");
            _projectionMatrixLocation = _gl.GetUniformLocation(_shaderProgram, "projection");
            _albedoTextureLocation = _gl.GetUniformLocation(_shaderProgram, "u_albedoTexture");
            _baseColorTintLocation = _gl.GetUniformLocation(_shaderProgram, "u_baseColorTint");

            _lightTypeLocation = _gl.GetUniformLocation(_shaderProgram, "u_lightType");
            _lightPositionWorldLocation = _gl.GetUniformLocation(_shaderProgram, "u_lightPosition_world");
            _lightDirectionWorldLocation = _gl.GetUniformLocation(_shaderProgram, "u_lightDirection_world");
            _lightColorLocation = _gl.GetUniformLocation(_shaderProgram, "u_lightColor");
            _lightIntensityLocation = _gl.GetUniformLocation(_shaderProgram, "u_lightIntensity");
            _lightRangeLocation = _gl.GetUniformLocation(_shaderProgram, "u_lightRange");
            _spotCutOffCosineLocation = _gl.GetUniformLocation(_shaderProgram, "u_spotCutOffCosine");
            _spotOuterCutOffCosineLocation = _gl.GetUniformLocation(_shaderProgram, "u_spotOuterCutOffCosine");
            _ambientLightFactorLocation = _gl.GetUniformLocation(_shaderProgram, "u_ambientLightFactor");
            // _cameraPositionWorldLocation = _gl.GetUniformLocation(_shaderProgram, "u_cameraPosition_world");

            // Log uniform locations (using corrected direct call)
            DebugLogUniformLocation("model", _modelMatrixLocation);
            DebugLogUniformLocation("view", _viewMatrixLocation);
            DebugLogUniformLocation("projection", _projectionMatrixLocation);
            DebugLogUniformLocation("u_albedoTexture", _albedoTextureLocation);
            DebugLogUniformLocation("u_baseColorTint", _baseColorTintLocation);
            DebugLogUniformLocation("u_lightType", _lightTypeLocation);
            DebugLogUniformLocation("u_lightPosition_world", _lightPositionWorldLocation);
            DebugLogUniformLocation("u_lightDirection_world", _lightDirectionWorldLocation);
            DebugLogUniformLocation("u_lightColor", _lightColorLocation);
            DebugLogUniformLocation("u_lightIntensity", _lightIntensityLocation);
            DebugLogUniformLocation("u_lightRange", _lightRangeLocation);
            DebugLogUniformLocation("u_spotCutOffCosine", _spotCutOffCosineLocation);
            DebugLogUniformLocation("u_spotOuterCutOffCosine", _spotOuterCutOffCosineLocation);
            DebugLogUniformLocation("u_ambientLightFactor", _ambientLightFactorLocation);

            _gl.UseProgram(0);
            return true;
        }

        // Helper for logging uniform locations (this method is static within Render.cs)
        private static void DebugLogUniformLocation(string name, int location)
        {
            if (location == -1)
                Debug.LogWarning($"Render: Uniform '{name}' not found in shader or inactive.");
            // else
            //    Debug.Log($"Render: Uniform '{name}' location: {location}"); // Optional: for verbose logging
        }

        public void BeginDraw()
        {
            if (_gl == null) return;
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        }

        public unsafe void Draw(Mesh m, MiloRender.DataTypes.Light activeLight) // Fully qualify Light type or use 'using MiloLight = ...'
        {
            if (_gl == null || m == null || _shaderProgram == 0 || _mainCamera == null)
            {
                if (_shaderProgram == 0) Debug.LogWarning("Render.Draw: Shader program is 0.");
                return;
            }
            if (m.vertexBuffer == null || !m.vertexBuffer.IsDataReady())
            {
                Debug.LogWarning($"Render.Draw: Mesh (HC:{m.GetHashCode()}) vertex buffer data not ready. Skip.");
                return;
            }
            if (m.GetVAO() == 0)
            {
                m.UploadToGPU();
                if (m.GetVAO() == 0) { Debug.LogError($"Render.Draw: Mesh VAO still 0 (HC:{m.GetHashCode()}). Cannot draw."); return; }
            }

            _gl.UseProgram(_shaderProgram);

            Matrix4X4<float> modelMatrix = m.Transform.ModelMatrix;
            Matrix4X4<float> viewMatrix = _mainCamera.GetViewMatrix();
            Matrix4X4<float> projectionMatrix = _mainCamera.GetProjectionMatrix();

            if (_modelMatrixLocation != -1) _gl.UniformMatrix4(_modelMatrixLocation, 1, false, modelMatrix.ToFloatArray());
            if (_viewMatrixLocation != -1) _gl.UniformMatrix4(_viewMatrixLocation, 1, false, viewMatrix.ToFloatArray());
            if (_projectionMatrixLocation != -1) _gl.UniformMatrix4(_projectionMatrixLocation, 1, false, projectionMatrix.ToFloatArray());

            m.Material?.ApplyMaterial(_gl, _shaderProgram);
            if (_albedoTextureLocation != -1) _gl.Uniform1(_albedoTextureLocation, 0); // Ensure sampler uses texture unit 0

            // Assuming u_baseColorTint is for material tint, if material doesn't set it or it's unused, send white
            if (_baseColorTintLocation != -1 && m.Material != null)
            {
                _gl.Uniform4(_baseColorTintLocation, m.Material.BaseColorTint.X, m.Material.BaseColorTint.Y, m.Material.BaseColorTint.Z, m.Material.BaseColorTint.W);
            }
            else if (_baseColorTintLocation != -1)
            {
                _gl.Uniform4(_baseColorTintLocation, 1.0f, 1.0f, 1.0f, 1.0f); // Default if no material or tint
            }


            // --- SET LIGHTING UNIFORMS ---
            if (activeLight != null && activeLight.IsActive)
            {
                if (_lightTypeLocation != -1) _gl.Uniform1(_lightTypeLocation, (int)activeLight.Type);
                if (_lightColorLocation != -1) _gl.Uniform3(_lightColorLocation, activeLight.Color.X, activeLight.Color.Y, activeLight.Color.Z);
                if (_lightIntensityLocation != -1) _gl.Uniform1(_lightIntensityLocation, activeLight.Intensity);
                if (_ambientLightFactorLocation != -1) _gl.Uniform3(_ambientLightFactorLocation, 0.25f, 0.25f, 0.25f); // "75% dark" ambient when lit

                if (activeLight.Type == LightType.Directional)
                {
                    var dirLight = (DirectionalLight)activeLight;
                    Vector3D<float> lightTravelDirection = dirLight.Transform.Forward;
                    if (_lightDirectionWorldLocation != -1) _gl.Uniform3(_lightDirectionWorldLocation, lightTravelDirection.X, lightTravelDirection.Y, lightTravelDirection.Z);
                }
                else if (activeLight.Type == LightType.Spot)
                {
                    var spotLight = (SpotLight)activeLight;
                    if (_lightPositionWorldLocation != -1) _gl.Uniform3(_lightPositionWorldLocation, spotLight.Transform.WorldPosition.X, spotLight.Transform.WorldPosition.Y, spotLight.Transform.WorldPosition.Z);
                    Vector3D<float> spotPointingDirection = spotLight.Transform.Forward;
                    if (_lightDirectionWorldLocation != -1) _gl.Uniform3(_lightDirectionWorldLocation, spotPointingDirection.X, spotPointingDirection.Y, spotPointingDirection.Z);
                    if (_lightRangeLocation != -1) _gl.Uniform1(_lightRangeLocation, spotLight.Range);
                    if (_spotCutOffCosineLocation != -1) _gl.Uniform1(_spotCutOffCosineLocation, spotLight.CutOffAngleCosine);
                    if (_spotOuterCutOffCosineLocation != -1) _gl.Uniform1(_spotOuterCutOffCosineLocation, spotLight.OuterCutOffAngleCosine);
                }
            }
            else // No active light
            {
                if (_lightTypeLocation != -1) _gl.Uniform1(_lightTypeLocation, 0); // 0 for NoLight
                // When u_lightType is 0, shader gives full illumination, so ambient factor here is more of a default for type > 0
                // but if type is 0, the shader logic for type==0 overrides this.
                if (_ambientLightFactorLocation != -1) _gl.Uniform3(_ambientLightFactorLocation, 1.0f, 1.0f, 1.0f); // Not strictly needed if type 0 means full bright
            }
            // --- END LIGHTING UNIFORMS ---

            _gl.BindVertexArray(m.GetVAO());
            if (m.GetIndexCount() > 0)
            {
                _gl.DrawElements(PrimitiveType.Triangles, (uint)m.GetIndexCount(), DrawElementsType.UnsignedInt, null);
            }
            _gl.BindVertexArray(0);
            _gl.UseProgram(0); // Good practice to unbind program
        }

        // This overload is called by Mesh.Draw() if no light is passed explicitly.
        public unsafe void Draw(Mesh m)
        {
            Draw(m, null); // Default to no active light for this specific mesh draw call.
        }

        public void EndDraw() { /* Buffer swapping handled by windowing system */ }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) { /* Dispose managed resources */ }
            if (_gl != null && _shaderProgram != 0)
            {
                _gl.DeleteProgram(_shaderProgram);
                Debug.Log($"Render: Shader program (ID: {_shaderProgram}) deleted.");
                _shaderProgram = 0;
            }
            _gl = null;
            if (instance == this) instance = null;
            Debug.Log("Render: Disposed.");
        }
        ~Render() { Dispose(false); }
    }

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