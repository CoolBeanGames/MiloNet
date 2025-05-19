// In MiloNet/Game.cs
using System;
using System.IO;
using System.Linq;
using Debugger;
using Imports;             // For GLBImporter
using MiloRender.DataTypes;  // For Scene
using Silk.NET.OpenGL;     // For GL (needed by GLBImporter)
using Silk.NET.Maths;      // For Vector3D, Quaternion, Scalar

namespace MiloNet
{
    internal static class Game
    {
        private static Scene _gameScene; // The game's current scene

        /// <summary>
        /// Game-specific initialization. Loads game assets and returns the main game scene.
        /// Program.cs will handle setting the camera on the renderer based on this scene.
        /// </summary>
        /// <returns>The loaded game scene, or null if loading failed.</returns>
        public static Scene Init(GL glContext)
        {
            Debug.Log("Game.Init: Starting game-specific asset loading.");

            string modelFileName = "your_model.glb"; // Game-specific asset
            string executableLocation = AppDomain.CurrentDomain.BaseDirectory;
            string modelPath = Path.Combine(executableLocation, "Assets", modelFileName);

            Debug.Log($"Game.Init: Attempting to load GLB as scene from: {modelPath}");

            // GLBImporter needs the GL context.
            Scene loadedScene = GLBImporter.LoadGlbAsScene(glContext, modelPath);

            if (loadedScene != null)
            {
                _gameScene = loadedScene; // Store reference for Game.Update and Game.RenderFrame
                ModelDatabase.AddScene(_gameScene); // Register with ModelDatabase
                Debug.Log($"Game.Init: Scene '{_gameScene.Name}' loaded successfully. Models: {_gameScene.Models.Count}");
            }
            else
            {
                Debug.LogError($"Game.Init: Failed to load GLB from '{modelPath}'. Game scene is null.");
                _gameScene = null;
            }
            Debug.Log("Game.Init: Game-specific asset loading complete.");
            return _gameScene; // Return the loaded scene (or null) to Program.cs
        }

        /// <summary>
        /// Game-specific update logic.
        /// </summary>
        public static void Update(float deltaTime)
        {
            if (_gameScene == null || !_gameScene.Models.Any())
            {
                return;
            }

            var firstModel = _gameScene.Models[0];
            if (firstModel != null)
            {
                float rotationSpeed = 20.0f * deltaTime;
                firstModel.Transform.Rotate(Vector3D<float>.UnitY, rotationSpeed, Space.Self);
            }
        }

        /// <summary>
        /// Game-specific rendering call.
        /// </summary>
        public static void RenderFrame()
        {
            if (_gameScene != null)
            {
                _gameScene.Draw();
            }
        }
    }
}