// In MiloRender/DataTypes/Actor.cs
// (Consider moving to a more general engine namespace later, e.g., MiloEngine.Core)
using Debugger;
using System;

namespace MiloRender.DataTypes // Or a more general namespace
{
    public class Actor : IDisposable
    {
        public string Name { get; set; }
        public Transform Transform { get; private set; }
        public Mesh Mesh { get; set; } // An actor might not have a mesh (e.g., a trigger volume)

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    if (_isActive) OnEnable(); else OnDisable();
                }
            }
        }

        public Actor(string name = "Actor")
        {
            Name = name;
            Transform = new Transform();
            // Mesh is null by default, can be assigned later
            Debug.Log($"Actor: Created '{Name}'.");
        }

        public Actor(string name, Mesh mesh)
        {
            Name = name;
            Transform = new Transform();
            Mesh = mesh;
            Debug.Log($"Actor: Created '{Name}' with a mesh.");
        }

        // Basic lifecycle methods (can be expanded for scripting)
        public virtual void Start()
        {
            // Called once before the first frame update
        }

        public virtual void Update(float deltaTime)
        {
            // Called every frame
            // For example, user input, AI logic, etc.
        }

        public virtual void OnEnable()
        {
            // Called when the actor becomes active
        }

        public virtual void OnDisable()
        {
            // Called when the actor becomes inactive
        }

        /// <summary>
        /// Requests the renderer to draw this actor's mesh, if it has one and is active.
        /// </summary>
        public virtual void Draw()
        {
            if (!IsActive || Mesh == null) return;

            // The Render instance will use mesh.Transform.ModelMatrix and mesh.Material
            Mesh.Draw(); // Mesh.Draw() delegates to Render.Instance.DrawMesh(this.Mesh)
        }

        public void Dispose()
        {
            // If the Actor owns the Mesh, it might dispose of it here.
            // However, Meshes might be shared, so care is needed.
            // For now, assuming Mesh disposal is handled elsewhere or by who creates it.
            Mesh?.Dispose(); // If actor is responsible for its mesh lifecycle
            Debug.Log($"Actor: Disposed '{Name}'.");
        }
    }
}