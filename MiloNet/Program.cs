using System;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Debugger;
using MiloRender;
using MiloRender.DataTypes;
using System.IO;
using Imports; // <--- Added this using directive

namespace MiloNet
{
    internal class Program
    {
        private static IWindow _window;
        private static GL _gl;
        private static Render _render;
        public static MiloRender.DataTypes.Camera _camera;

        private static Mesh _loadedModel;

        static void Main(string[] args)
        {
            Debug.OpenConsole();
            Debug.Log("MiloNet Engine Startup Sequence Initiated...");

            WindowOptions options = WindowOptions.Default;
            options.Size = new Vector2D<int>(640, 480);
            options.Title = "MiloNet Engine - [PlayStation Resolution Test]";
            options.VSync = true;
            options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));
            options.PreferredDepthBufferBits = 24;

            _window = Window.Create(options);

            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRenderFrame;
            _window.Resize += OnResize;
            _window.Closing += OnClose;

            Debug.Log("MiloNet: Starting window run loop.");
            _window.Run();

            _window?.Dispose();
            Debug.Log("MiloNet: Window disposed. Application will now exit.");
            Debug.End();
        }

        static void OnLoad()
        {
            Debug.Log("MiloNet: Window Loaded. Initializing OpenGL and game resources...");

            _gl = _window.CreateOpenGL();
            if (_gl == null)
            {
                Debug.LogError("MiloNet: CRITICAL - Failed to create OpenGL context. Application cannot continue.");
                _window.Close();
                return;
            }
            Debug.Log($"MiloNet: OpenGL context created. Version: {_gl.GetStringS(StringName.Version)}");

            _camera = new MiloRender.DataTypes.Camera(320, 240);
            _camera.Transform.LocalPosition = new Vector3D<float>(0, 0.5f, 3.0f);
            Debug.Log("MiloNet: Game camera initialized.");

            try
            {
                _render = new Render(_gl, _camera);
                Debug.Log("MiloNet: Renderer initialized.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"MiloNet: CRITICAL - Failed to initialize Renderer: {ex.Message}. Closing.");
                _gl?.Dispose();
                _window.Close();
                return;
            }

            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);
            _camera.SetGameWorldAspectRatio(320, 240);

            try
            {
                string modelFileName = "your_model.glb"; // <--- REPLACE THIS WITH YOUR FILENAME
                string executableLocation = AppDomain.CurrentDomain.BaseDirectory;
                // Ensure "Assets" folder is a subdirectory of your executable's location
                // For example, if your .exe is in "bin/Debug", Assets should be "bin/Debug/Assets"
                string modelPath = Path.Combine(executableLocation, "Assets", modelFileName);

                Debug.Log($"MiloNet: Attempting to load model from: {modelPath}");

                Material defaultMaterial = new Material { BaseColorTint = new System.Numerics.Vector4(0.8f, 0.8f, 0.8f, 1.0f) };

                // --- MODIFIED LINE ---
                _loadedModel = GLBImporter.LoadGlb(_gl, modelPath, defaultMaterial); // <--- Changed from Mesh.LoadFromGlbFile

                if (_loadedModel != null)
                {
                    // _loadedModel.Transform.LocalPosition = new Vector3D<float>(0, 0, -10); // Example position
                    _loadedModel.Transform.LocalScale = new Vector3D<float>(1.0f, 1.0f, 1.0f);
                    Debug.Log($"MiloNet: Model '{modelFileName}' loaded successfully via GLBImporter.");
                }
                else
                {
                    Debug.LogError($"MiloNet: Failed to load model '{modelFileName}' via GLBImporter. A fallback cube will be created.");
                    _loadedModel = new Mesh(_gl, Primitive.Cube, new Material { BaseColorTint = new System.Numerics.Vector4(1, 0, 0, 1) });
                    _loadedModel.Transform.LocalPosition = new Vector3D<float>(0, 0.5f, 0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"MiloNet: Exception during asset loading: {ex.Message} - {ex.StackTrace}");
                if (_loadedModel == null && _gl != null)
                {
                    _loadedModel = new Mesh(_gl, Primitive.Cube, new Material { BaseColorTint = new System.Numerics.Vector4(0, 1, 0, 1) });
                    _loadedModel.Transform.LocalPosition = new Vector3D<float>(0, 0.5f, 0);
                    Debug.Log("MiloNet: Created emergency fallback cube due to exception.");
                }
            }
            Debug.Log("MiloNet: OnLoad complete.");
        }

        static void OnUpdate(double deltaTime)
        {
            if (_loadedModel != null)
            {
                float rotationSpeed = 20.0f * (float)deltaTime;
                _loadedModel.Transform.LocalRotation *= Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitY, Scalar.DegreesToRadians(rotationSpeed));
            }
        }

        static void OnRenderFrame(double delta)
        {
            if (_render == null || _gl == null) return;

            _render.BeginDraw();

            if (_loadedModel != null)
            {
                _loadedModel.Draw();
            }

            _render.EndDraw();
        }

        static void OnResize(Vector2D<int> newFramebufferSize)
        {
            if (_gl == null) return;
            Debug.Log($"MiloNet: Window Framebuffer resized to {newFramebufferSize.X}x{newFramebufferSize.Y}. Updating GL viewport.");
            _gl.Viewport(0, 0, (uint)newFramebufferSize.X, (uint)newFramebufferSize.Y);
            _camera?.SetGameWorldAspectRatio(320, 240);
        }

        static void OnClose()
        {
            Debug.Log("MiloNet: Window Closing event triggered. Cleaning up game resources...");
            _loadedModel?.Dispose();
            _render?.Dispose();
            _gl?.Dispose();
            Debug.Log("MiloNet: Game resources disposed.");
        }
    }
}