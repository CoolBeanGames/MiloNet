// In MiloNet/Program.cs
using System;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Debugger;
using MiloRender;
using MiloRender.DataTypes;
using System.IO;
using Imports;
using System.Linq;

namespace MiloNet
{
    internal class Program
    {
        private static IWindow _window;
        private static GL _gl;
        private static Render _render;
        private static Scene _currentScene;

        static void Main(string[] args)
        {
            Debug.OpenConsole();
            Debug.Log("MiloNet Engine Startup Sequence Initiated...");

            WindowOptions options = WindowOptions.Default;
            options.Size = new Vector2D<int>(640, 480);
            options.Title = "MiloNet Engine";
            options.VSync = true;
            options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3));
            options.PreferredDepthBufferBits = 24;

            _window = Window.Create(options);

            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRenderFrame;
            _window.Resize += OnResize;
            _window.Closing += OnWindowClosing; // This event is key for GL cleanup

            Debug.Log("MiloNet: Starting window run loop.");
            _window.Run(); // This blocks until the window is closed

            // After Run() exits, the window object might still exist, but its GL context might be gone or non-current.
            // Most GL cleanup should have happened in OnWindowClosing.
            Debug.Log("MiloNet: Window run loop exited.");

            _window?.Dispose(); // Dispose the window object itself
            Debug.Log("MiloNet: Window disposed.");

            Debug.End();
        }

        static void OnLoad()
        {
            Debug.Log("MiloNet: Window Loaded. Initializing OpenGL and game resources...");

            _gl = _window.CreateOpenGL();
            if (_gl == null) { /* ... error handling ... */ _window.Close(); return; }
            Debug.Log($"MiloNet: OpenGL context created. Version: {_gl.GetStringS(StringName.Version)}");

            var initialCamera = new MiloRender.DataTypes.Camera();
            initialCamera.Transform.LocalPosition = new Vector3D<float>(0, 1f, 5.0f);
            Debug.Log("MiloNet: Initial fallback camera created for renderer.");

            try
            {
                _render = new Render(_gl, initialCamera);
                Debug.Log("MiloNet: Renderer initialized.");
            }
            catch (Exception ex) { /* ... error handling ... */ _gl?.Dispose(); _window.Close(); return; }

            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);

            try
            {
                string modelFileName = "your_model.glb";
                string executableLocation = AppDomain.CurrentDomain.BaseDirectory;
                string modelPath = Path.Combine(executableLocation, "Assets", modelFileName);

                Debug.Log($"MiloNet: Attempting to load GLB as scene from: {modelPath}");

                Scene loadedScene = GLBImporter.LoadGlbAsScene(_gl, modelPath);

                if (loadedScene != null)
                {
                    ModelDatabase.AddScene(loadedScene);
                    _currentScene = loadedScene;
                    Debug.Log($"MiloNet: Scene '{loadedScene.Name}' loaded. Models: {loadedScene.Models.Count}");
                    if (_currentScene.ActiveCamera != null)
                    {
                        _render.SetCamera(_currentScene.ActiveCamera);
                        Debug.Log($"MiloNet: Renderer camera updated to scene's camera (Hash: '{_currentScene.ActiveCamera.GetHashCode()}').");
                    }
                    else
                    {
                        Debug.LogWarning($"MiloNet: Loaded scene '{_currentScene.Name}' has NO active camera. Renderer will use its fallback.");
                    }
                }
                else { Debug.LogError($"MiloNet: Failed to load GLB as scene from '{modelPath}'."); }
            }
            catch (Exception ex) { Debug.LogError($"MiloNet: Exception during scene loading: {ex.Message} - {ex.StackTrace}"); }
            Debug.Log("MiloNet: OnLoad complete.");
        }

        static void OnUpdate(double deltaTime)
        {
            if (_currentScene != null && _currentScene.Models != null && _currentScene.Models.Any())
            {
                var firstModel = _currentScene.Models[0];
                float rotationSpeed = 20.0f * (float)deltaTime;
                firstModel.Transform.LocalRotation *= Quaternion<float>.CreateFromAxisAngle(Vector3D<float>.UnitY, Scalar.DegreesToRadians(rotationSpeed));
            }
        }

        static void OnRenderFrame(double delta)
        {
            if (_render == null || _gl == null) return;
            _render.BeginDraw();
            if (_currentScene != null)
            {
                if (_currentScene.ActiveCamera != null && _render.GetCurrentCamera() != _currentScene.ActiveCamera)
                {
                    _render.SetCamera(_currentScene.ActiveCamera);
                }
                _currentScene.Draw();
            }
            _render.EndDraw();
        }

        static void OnResize(Vector2D<int> newFramebufferSize)
        {
            if (_gl == null) return;
            Debug.Log($"MiloNet: Window Framebuffer resized to {newFramebufferSize.X}x{newFramebufferSize.Y}. Updating GL viewport.");
            _gl.Viewport(0, 0, (uint)newFramebufferSize.X, (uint)newFramebufferSize.Y);
            var camToUpdate = _currentScene?.ActiveCamera ?? _render?.GetCurrentCamera();
            camToUpdate?.SetGameWorldAspectRatio(320, 240);
        }

        // This event is triggered when the user attempts to close the window,
        // while the window and its GL context are typically still valid and current.
        static void OnWindowClosing()
        {
            Debug.Log("MiloNet: Window Closing event triggered. Cleaning up OpenGL resources NOW.");

            // Dispose game-specific resources that use OpenGL
            ModelDatabase.ClearAndDisposeAll();
            Debug.Log("MiloNet: ModelDatabase cleared and disposed.");

            // Dispose the Renderer (which disposes shader programs)
            _render?.Dispose();
            Debug.Log("MiloNet: Renderer disposed.");

            // The _gl context itself will be cleaned up when the window is disposed after _window.Run() finishes.
            // No need to call _gl.Dispose() here, as the context needs to remain for the above disposals.
        }
    }
}