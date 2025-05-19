// In MiloNet/Program.cs
using System;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Debugger;
using MiloRender;
using MiloRender.DataTypes; // For Camera (fallback)

namespace MiloNet
{
    internal class Program
    {
        private static IWindow _window;
        private static GL _gl;
        private static Render _render;
        private static Camera _fallbackCamera;
        private static Scene _mainGameScene; // Program.cs can hold a reference if needed, or get it from Game

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
            _window.Closing += OnWindowClosing;

            Debug.Log("MiloNet: Starting window run loop.");
            _window.Run();

            Debug.Log("MiloNet: Window run loop exited.");

            ModelDatabase.ClearAndDisposeAll();
            Debug.Log("MiloNet: ModelDatabase cleared and disposed.");
            _render?.Dispose();
            Debug.Log("MiloNet: Renderer disposed.");
            _window?.Dispose();
            Debug.Log("MiloNet: Window disposed.");
            Debug.End();
        }

        static void OnLoad()
        {
            Debug.Log("MiloNet: Window Loaded. Initializing OpenGL and Render subsystem...");

            _gl = _window.CreateOpenGL();
            if (_gl == null) { Debug.LogError("MiloNet: Failed to create OpenGL context!"); _window.Close(); return; }
            Debug.Log($"MiloNet: OpenGL context created. Version: {_gl.GetStringS(StringName.Version)}");

            _fallbackCamera = new MiloRender.DataTypes.Camera();
            _fallbackCamera.Transform.LocalPosition = new Vector3D<float>(0, 1f, 5.0f);
            Debug.Log("MiloNet: Engine fallback camera created.");

            try
            {
                _render = new Render(_gl, _fallbackCamera); // Initialize renderer with the fallback
                Debug.Log("MiloNet: Renderer initialized with fallback camera.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"MiloNet: CRITICAL - Renderer initialization failed: {ex.Message} - {ex.StackTrace}");
                _gl?.Dispose(); _window.Close(); return;
            }

            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);

            // Initialize the Game module. Game.Init now returns the loaded scene.
            Scene gameSceneFromInit = null;
            try
            {
                gameSceneFromInit = Game.Init(_gl); // Game.Init only needs GL for GLBImporter
                _mainGameScene = gameSceneFromInit; // Program can keep a reference if needed
            }
            catch (Exception ex)
            {
                Debug.LogError($"MiloNet: Exception during Game.Init(): {ex.Message} - {ex.StackTrace}");
            }

            // Program.cs decides which camera the renderer uses based on Game.Init's result.
            if (gameSceneFromInit != null && gameSceneFromInit.ActiveCamera != null)
            {
                _render.SetCamera(gameSceneFromInit.ActiveCamera);
                Debug.Log("MiloNet.OnLoad: Renderer camera set to game scene's active camera.");
            }
            else
            {
                // If gameScene is null or has no camera, renderer continues using _fallbackCamera.
                Debug.LogWarning("MiloNet.OnLoad: Game scene is null or has no camera. Renderer continues with fallback camera.");
                // _render.SetCamera(_fallbackCamera); // Already set during _render init, but can be explicit
            }

            Debug.Log("MiloNet: OnLoad complete.");
        }

        static void OnUpdate(double deltaTime)
        {
            Game.Update((float)deltaTime);
        }

        static void OnRenderFrame(double delta)
        {
            if (_render == null || _gl == null) return;

            // Ensure a camera is active on the renderer.
            // This handles edge cases where Game.Init might have failed or scene camera became null.
            if (_render.GetCurrentCamera() == null)
            {
                _render.SetCamera(_fallbackCamera);
                Debug.LogWarning("Program.OnRenderFrame: Renderer camera was null, set to fallback.");
            }
            // If _mainGameScene exists, and its camera configuration changed, update renderer camera
            else if (_mainGameScene != null && _mainGameScene.ActiveCamera != null && _render.GetCurrentCamera() != _mainGameScene.ActiveCamera)
            {
                _render.SetCamera(_mainGameScene.ActiveCamera);
                Debug.Log("Program.OnRenderFrame: Updated renderer camera to game scene's active camera.");
            }
            else if (_mainGameScene != null && _mainGameScene.ActiveCamera == null && _render.GetCurrentCamera() != _fallbackCamera)
            {
                _render.SetCamera(_fallbackCamera); // Scene exists but lost its camera, use fallback
                Debug.LogWarning("Program.OnRenderFrame: Game scene has no active camera, renderer set to fallback.");
            }


            _render.BeginDraw();
            Game.RenderFrame();
            _render.EndDraw();
        }

        static void OnResize(Vector2D<int> newFramebufferSize)
        {
            if (_gl == null) return;
            Debug.Log($"MiloNet: Window Framebuffer resized to {newFramebufferSize.X}x{newFramebufferSize.Y}. Updating GL viewport.");
            _gl.Viewport(0, 0, (uint)newFramebufferSize.X, (uint)newFramebufferSize.Y);
        }

        static void OnWindowClosing()
        {
            Debug.Log("MiloNet: Window Closing event. Resources will be cleaned up in Main after loop exits.");
        }
    }
}