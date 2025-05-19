// In MiloRender/DataTypes/Scene.cs
using Debugger;
using Silk.NET.OpenGL; // Potentially needed if Scene directly interacts with GL context for its camera
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiloRender.DataTypes
{
    public class Scene : IDisposable
    {
        public string Name { get; private set; }
        public List<Mesh> Models { get; private set; }
        public Camera ActiveCamera { get; set; } // The scene's primary camera

        private bool _isDisposed = false;

        public Scene(string name, GL glContext) // GL context might be needed for default camera
        {
            Name = name;
            Models = new List<Mesh>();

            // For now, create a default camera if one isn't provided later.
            // This camera would be local to the scene.
            // Alternatively, camera can be set by the loader or Program.cs
            if (ActiveCamera == null && glContext != null) // glContext check might not be strictly needed if Camera doesn't require GL on construction
            {
                Debug.Log($"Scene '{Name}': No active camera set, creating a default new camera for this scene.");
                ActiveCamera = new Camera(); // Uses default 320x240 aspect
                // Position it somewhere reasonable, e.g., looking at origin
                ActiveCamera.Transform.LocalPosition = new Silk.NET.Maths.Vector3D<float>(0, 1, 5);
                // ActiveCamera.Transform.LookAt(Silk.NET.Maths.Vector3D<float>.Zero, Silk.NET.Maths.Vector3D<float>.UnitY);
            }
            else if (ActiveCamera == null)
            {
                Debug.LogWarning($"Scene '{Name}': No active camera and no GL context to create a default. Camera will be null.");
            }
        }

        public Scene(string name, Camera initialCamera)
        {
            Name = name;
            Models = new List<Mesh>();
            ActiveCamera = initialCamera ?? throw new ArgumentNullException(nameof(initialCamera));
            Debug.Log($"Scene '{Name}': Created with an initial camera.");
        }


        public void AddModel(Mesh model)
        {
            if (model != null && !Models.Contains(model))
            {
                Models.Add(model);
                Debug.Log($"Scene '{Name}': Added model '{model.Transform.GetHashCode()}' (Material: {model.Material?.AlbedoTexture?.Name ?? "N/A"}). Total models: {Models.Count}");
            }
        }

        /// <summary>
        /// Draws all models in this scene.
        /// Assumes the renderer's main camera has been set appropriately
        /// (e.g., to this scene's ActiveCamera) before calling this.
        /// </summary>
        public void Draw()
        {
            if (Render.instance == null)
            {
                Debug.LogError($"Scene '{Name}'.Draw: Render.instance is null. Cannot draw models.");
                return;
            }
            if (Render.instance.GetCurrentCamera() == null) // Assuming Render class has a way to get its current camera
            {
                Debug.LogWarning($"Scene '{Name}'.Draw: Render.instance has no active camera set. Models might not render correctly or at all.");
                // Optionally, if this scene's camera should always be used for its own draw call:
                // Render.instance.SetCamera(this.ActiveCamera);
            }


            // Debug.Log($"Scene '{Name}': Drawing {Models.Count} model(s).");
            foreach (Mesh model in Models)
            {
                if (model != null)
                {
                    // The model's Transform is relative to the scene's origin (world origin for now)
                    // If the scene itself had a transform, we'd combine it here.
                    model.Draw(); // This calls Render.instance.Draw(model)
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
                Debug.Log($"Scene '{Name}': Disposing {Models.Count} models.");
                foreach (Mesh model in Models)
                {
                    model?.Dispose();
                }
                Models.Clear();

                // If the Scene "owns" this camera, it should dispose it.
                // For now, let's assume cameras might be shared or managed by Program.cs
                // ActiveCamera?.Dispose(); // If Camera implements IDisposable and Scene owns it.
                // Our current Camera class doesn't implement IDisposable.
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