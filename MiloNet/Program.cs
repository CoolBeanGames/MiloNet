// In MiloNet/Program.cs
using System;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Debugger;
using MiloRender;
using MiloRender.DataTypes; // For Camera (fallback), Scene, Light
using System.Linq;          // For .Any()

namespace MiloNet
{
    internal class Program
    {
        private static IWindow _window;
        private static GL _gl;
        private static Render _render;
        private static Camera _fallbackCamera;
        private static Scene _mainGameScene; // Program.cs holds the reference to the scene returned by Game.Init

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
                _render = new Render(_gl, _fallbackCamera);
                Debug.Log("MiloNet: Renderer initialized with fallback camera.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"MiloNet: CRITICAL - Renderer initialization failed: {ex.Message} - {ex.StackTrace}");
                _gl?.Dispose(); _window.Close(); return;
            }

            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);

            Scene gameSceneFromInit = null;
            try
            {
                gameSceneFromInit = Game.Init(_gl);
                _mainGameScene = gameSceneFromInit; // Store the returned scene
            }
            catch (Exception ex)
            {
                Debug.LogError($"MiloNet: Exception during Game.Init(): {ex.Message} - {ex.StackTrace}");
            }

            if (_mainGameScene != null) // Check if Game.Init succeeded in loading a scene
            {
                // Set Renderer Camera
                if (_mainGameScene.ActiveCamera != null)
                {
                    _render.SetCamera(_mainGameScene.ActiveCamera);
                    Debug.Log("MiloNet.OnLoad: Renderer camera set to game scene's active camera.");
                }
                else
                {
                    _render.SetCamera(_fallbackCamera); // Ensure fallback if scene has no camera
                    Debug.LogWarning("MiloNet.OnLoad: Game scene has no active camera. Renderer set to fallback camera.");
                }

                // --- ACTIVATE THE FIRST LIGHT IN THE SCENE ---
                if (_mainGameScene.Lights != null && _mainGameScene.Lights.Any())
                {
                    _mainGameScene.ActiveLight = _mainGameScene.Lights[0]; // Activate the first light found
                    var activeLightInfo = _mainGameScene.ActiveLight;
                    Debug.Log($"Program.OnLoad: Activated first light in scene '{_mainGameScene.Name}'. Type: {activeLightInfo.GetType().Name}, Color: {activeLightInfo.Color}, Intensity: {activeLightInfo.Intensity}, Pos: {activeLightInfo.Transform.LocalPosition}");
                }
                else
                {
                    Debug.LogWarning($"Program.OnLoad: Game scene '{_mainGameScene.Name}' loaded but has no lights. Lighting will be based on 'NoLight' shader path.");
                    _mainGameScene.ActiveLight = null; // Explicitly set to null
                }
                // --- END LIGHT ACTIVATION ---
            }
            else
            {
                // Game.Init failed to return a scene, ensure renderer uses fallback.
                _render.SetCamera(_fallbackCamera);
                Debug.LogWarning("MiloNet.OnLoad: Game.Init did not return a scene. Renderer continues with fallback camera. No game lights to activate.");
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

            // Camera management during render frame:
            // Ensure the renderer has a valid camera. This might be the scene's or the fallback.
            if (_mainGameScene != null)
            {
                if (_mainGameScene.ActiveCamera != null && _render.GetCurrentCamera() != _mainGameScene.ActiveCamera)
                {
                    _render.SetCamera(_mainGameScene.ActiveCamera);
                }
                else if (_mainGameScene.ActiveCamera == null && _render.GetCurrentCamera() != _fallbackCamera)
                {
                    // Scene exists but its camera is null, use fallback.
                    _render.SetCamera(_fallbackCamera);
                }
            }
            else if (_render.GetCurrentCamera() == null) // No game scene, and renderer has no camera
            {
                _render.SetCamera(_fallbackCamera);
            }
            // If _render.GetCurrentCamera() is already set correctly (either to a scene camera or fallback by Init),
            // these checks might be redundant but are safe.

            _render.BeginDraw();
            Game.RenderFrame(); // Game.RenderFrame simply calls _gameScene.Draw()
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