using System;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL; // REQUIRED for GL instance
using Debugger;        // Your custom debugger
using MiloRender;      // For the Render class
using MiloRender.DataTypes; // For Camera, Mesh, Material, Transform, Primitive etc.
using System.IO; // For Path.Combine if needed for assets, though direct string is fine too

namespace MiloNet
{
    internal class Program
    {
        private static IWindow _window;
        private static GL _gl;
        private static Render _render;
        public static MiloRender.DataTypes.Camera _camera; // Public static for now, can be refactored later

        private static Mesh _loadedModel; // To store our loaded GLB model

        static void Main(string[] args)
        {
            Debug.OpenConsole();
            Debug.Log("MiloNet Engine Startup Sequence Initiated...");

            WindowOptions options = WindowOptions.Default;
            options.Size = new Vector2D<int>(640, 480); // UI Resolution
            options.Title = "MiloNet Engine - [PlayStation Resolution Test]";
            options.VSync = true;
            options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));
            options.PreferredDepthBufferBits = 24; // Good for 3D

            _window = Window.Create(options);

            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRenderFrame; // Renamed to avoid conflict with MiloRender.Render
            _window.Resize += OnResize;
            _window.Closing += OnClose;

            Debug.Log("MiloNet: Starting window run loop.");
            _window.Run(); // This blocks until the window is closed

            // Clean up resources after the window has closed and Run() has exited.
            // OnClose handles most, but disposing the window itself is good practice here.
            _window?.Dispose();
            Debug.Log("MiloNet: Window disposed. Application will now exit.");
            Debug.End(); // Ensure debugger writes its log if it hasn't already in OnClose
        }

        static void OnLoad()
        {
            Debug.Log("MiloNet: Window Loaded. Initializing OpenGL and game resources...");

            // 1. Create OpenGL Context
            _gl = _window.CreateOpenGL();
            if (_gl == null)
            {
                Debug.LogError("MiloNet: CRITICAL - Failed to create OpenGL context. Application cannot continue.");
                _window.Close();
                return;
            }
            Debug.Log($"MiloNet: OpenGL context created. Version: {_gl.GetStringS(StringName.Version)}");

            // 2. Initialize Game Camera
            _camera = new MiloRender.DataTypes.Camera(320, 240); // Game world resolution
            _camera.Transform.LocalPosition = new Vector3D<float>(0, 0.5f, 3.0f); // Slightly up and back
            // Optional: Make camera look at a point slightly below origin if models are origin-centered
            // _camera.Transform.LookAt(new Vector3D<float>(0, 0, 0), Vector3D<float>.UnitY);
            Debug.Log("MiloNet: Game camera initialized.");

            // 3. Initialize Renderer
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

            // 4. Set Initial Viewport & Call OnResize to handle initial camera aspect for window
            // OnResize(_window.FramebufferSize); // This will set viewport and camera aspect based on window
            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);
            _camera.SetGameWorldAspectRatio(320, 240); // Keep game aspect correct

            // 5. Load Game Assets (e.g., GLB model)
            try
            {
                // --- Path to your GLB model ---
                // Option 1: Relative path from executable (e.g., in "Assets" folder next to .exe)
                // Make sure the GLB file's "Copy to Output Directory" property in VS is "Copy if newer" or "Copy always".
                string modelFileName = "your_model.glb"; // <--- REPLACE THIS WITH YOUR FILENAME
                string executableLocation = AppDomain.CurrentDomain.BaseDirectory;
                string modelPath = Path.Combine(executableLocation, "Assets", modelFileName);

                // Option 2: Absolute path (less flexible, good for quick testing)
                // string modelPath = @"C:\Path\To\Your\Assets\your_model.glb"; // <--- REPLACE OR USE RELATIVE PATH

                Debug.Log($"MiloNet: Attempting to load model from: {modelPath}");

                Material defaultMaterial = new Material { BaseColorTint = new System.Numerics.Vector4(0.8f, 0.8f, 0.8f, 1.0f) };
                _loadedModel = Mesh.LoadFromGlbFile(_gl, modelPath, defaultMaterial);

                if (_loadedModel != null)
                {
                    _loadedModel.Transform.LocalPosition = new Vector3D<float>(0, 0, -10);
                    // Adjust scale if your model is too big or too small
                    _loadedModel.Transform.LocalScale = new Vector3D<float>(1.0f, 1.0f, 1.0f);
                    // _loadedModel.UploadToGPU(); // Or let Draw() handle it
                    Debug.Log($"MiloNet: Model '{modelFileName}' loaded successfully.");
                }
                else
                {
                    Debug.LogError($"MiloNet: Failed to load model '{modelFileName}'. A fallback cube will be created.");
                    // Fallback: Create a primitive cube if model loading fails
                    _loadedModel = new Mesh(_gl, Primitive.Cube, new Material { BaseColorTint = new System.Numerics.Vector4(1, 0, 0, 1) });
                    _loadedModel.Transform.LocalPosition = new Vector3D<float>(0, 0.5f, 0); // Place it
                    //_loadedModel.UploadToGPU();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"MiloNet: Exception during asset loading: {ex.Message} - {ex.StackTrace}");
                // Create a fallback cube if any exception occurs during loading
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
            // Game logic, input handling
            if (_loadedModel != null)
            {
                // Simple rotation for testing
                float rotationSpeed = 20.0f * (float)deltaTime; // Degrees per second
                _loadedModel.Transform.LocalRotation *= Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitY, Scalar.DegreesToRadians(rotationSpeed));
            }

            // Example: Close window on ESC key press
            // This requires setting up an InputContext in OnLoad:
            // var inputContext = _window.CreateInput();
            // For now, this is commented out.
            // if (inputContext.Keyboards[0].IsKeyPressed(Silk.NET.Input.Key.Escape))
            // {
            //     _window.Close();
            // }
        }

        static void OnRenderFrame(double delta) // Renamed from OnRender
        {
            if (_render == null || _gl == null) return; // Not initialized yet or failed

            _render.BeginDraw();

            if (_loadedModel != null)
            {
                _loadedModel.Draw();
            }

            _render.EndDraw();
            // Silk.NET handles buffer swapping automatically when using the Render event.
        }

        static void OnResize(Vector2D<int> newFramebufferSize)
        {
            if (_gl == null) return;

            Debug.Log($"MiloNet: Window Framebuffer resized to {newFramebufferSize.X}x{newFramebufferSize.Y}. Updating GL viewport.");
            _gl.Viewport(0, 0, (uint)newFramebufferSize.X, (uint)newFramebufferSize.Y);

            // Keep the game camera's aspect ratio tied to the 320x240 target
            // The final image will be scaled to fit the window maintaining this aspect.
            _camera?.SetGameWorldAspectRatio(320, 240);
        }

        static void OnClose()
        {
            Debug.Log("MiloNet: Window Closing event triggered. Cleaning up game resources...");

            // Dispose resources in reverse order of creation (roughly)
            _loadedModel?.Dispose();
            _render?.Dispose();
            _gl?.Dispose(); // Dispose the GL context
            // Note: _window.Dispose() will be called after _window.Run() finishes,
            // or you can call it here if you manage the window lifetime more explicitly.
            // However, calling it in OnClose can sometimes interfere with Silk.NET's own shutdown if Run is still unwinding.
            // It's generally safer to let Run() complete and dispose _window after that, as shown in Main().

            Debug.Log("MiloNet: Game resources disposed.");
            // Debug.End() should ideally be the very last thing before application exit,
            // or ensure it can be called multiple times if OnClose is not the true final exit point.
            // For safety, it's in Main() after Run() completes. If you need it here, make sure it's robust.
        }
    }
}