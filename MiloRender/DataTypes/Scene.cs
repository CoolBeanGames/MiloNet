// In MiloRender/DataTypes/Scene.cs
using Debugger;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.Maths; // Required for Vector3D for default camera position

// Alias our own Light type to avoid conflicts if any system usings are added.
using MiloLight = MiloRender.DataTypes.Light;
using MiloEngineCamera = MiloRender.DataTypes.Camera;


namespace MiloRender.DataTypes
{
    public class Scene : IDisposable
    {
        public string Name { get; private set; }
        public List<Mesh> Models { get; private set; }
        public List<MiloLight> Lights { get; private set; } // List to store all lights in the scene
        public List<MiloEngineCamera> Cameras { get; private set; } // List to store all cameras in the scene

        public MiloLight ActiveLight { get; set; } // The single active light for rendering
        public MiloEngineCamera ActiveCamera { get; set; } // The scene's primary camera

        private bool _isDisposed = false;

        // Constructor when GL context is available (e.g., for default camera)
        public Scene(string name, GL glContext)
        {
            Name = name;
            Models = new List<Mesh>();
            Lights = new List<MiloLight>();     // Initialize Lights list
            Cameras = new List<MiloEngineCamera>(); // Initialize Cameras list

            if (ActiveCamera == null && glContext != null)
            {
                Debug.Log($"Scene '{Name}': No active camera set during construction, creating a default new camera for this scene.");
                ActiveCamera = new MiloEngineCamera(); // Uses default 320x240 aspect
                ActiveCamera.Transform.LocalPosition = new Vector3D<float>(0, 1, 5);
                Cameras.Add(ActiveCamera); // Add default camera to the list too
            }
            else if (ActiveCamera == null)
            {
                Debug.LogWarning($"Scene '{Name}': No active camera during construction and no GL context to create a default. ActiveCamera will be null initially.");
            }
        }

        // Constructor with an initial camera
        public Scene(string name, MiloEngineCamera initialCamera)
        {
            Name = name;
            Models = new List<Mesh>();
            Lights = new List<MiloLight>();     // Initialize Lights list
            Cameras = new List<MiloEngineCamera>(); // Initialize Cameras list

            ActiveCamera = initialCamera ?? throw new ArgumentNullException(nameof(initialCamera));
            Cameras.Add(ActiveCamera); // Add the initial camera to the list
            Debug.Log($"Scene '{Name}': Created with an initial camera.");
        }

        public void AddModel(Mesh model)
        {
            if (model != null && !Models.Contains(model))
            {
                Models.Add(model);
                // Debug.Log($"Scene '{Name}': Added model (HC: {model.GetHashCode()}). Total models: {Models.Count}");
            }
        }

        public void AddLight(MiloLight light)
        {
            if (light != null && !Lights.Contains(light))
            {
                Lights.Add(light);
                Debug.Log($"Scene '{Name}': Added light of type '{light.GetType().Name}'. Total lights: {Lights.Count}");
            }
        }

        public void AddCamera(MiloEngineCamera camera, string cameraName = "UnnamedCamera") // cameraName for potential dictionary lookup
        {
            if (camera != null && !Cameras.Contains(camera))
            {
                Cameras.Add(camera);
                Debug.Log($"Scene '{Name}': Added camera '{cameraName}'. Total cameras: {Cameras.Count}");
            }
        }

        /// <summary>
        /// Draws all models in this scene, passing the scene's ActiveLight.
        /// </summary>
        public void Draw()
        {
            if (Render.instance == null)
            {
                Debug.LogError($"Scene '{Name}'.Draw: Render.instance is null. Cannot draw models.");
                return;
            }
            // Camera check/setting is now primarily handled by Program.cs or Game.cs before calling Scene.Draw

            foreach (Mesh model in Models)
            {
                if (model != null)
                {
                    // Pass the scene's ActiveLight to each model's draw call.
                    // Mesh.Draw will then pass it to Render.instance.Draw(mesh, light).
                    model.Draw(this.ActiveLight);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                Debug.Log($"Scene '{Name}': Disposing resources...");
                Debug.Log($"Scene '{Name}': Disposing {Models.Count} models.");
                foreach (Mesh model in Models)
                {
                    model?.Dispose();
                }
                Models.Clear();

                // Lights and Cameras are IDisposable if their owned resources (like Transforms) are.
                // Our Light/Camera classes don't directly own GL resources, their Transforms do not.
                // So, clearing the lists should be sufficient unless Light/Camera implement IDisposable for other reasons.
                Debug.Log($"Scene '{Name}': Clearing {Lights.Count} lights and {Cameras.Count} cameras references.");
                Lights.Clear();
                Cameras.Clear();

                ActiveCamera = null; // Release reference
                ActiveLight = null;  // Release reference
            }
            _isDisposed = true;
            Debug.Log($"Scene '{Name}': Disposed.");
        }

        ~Scene()
        {
            Dispose(false);
        }
    }
}