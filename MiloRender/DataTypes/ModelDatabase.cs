// In MiloNet/ModelDatabase.cs
using Debugger;
using MiloRender.DataTypes; // Required for Scene and Mesh
using System;
using System.Collections.Generic;
using System.IO; // For Path.GetFileNameWithoutExtension

namespace MiloNet
{
    public static class ModelDatabase
    {
        // Key: Scene name (e.g., GLB filename without extension)
        public static Dictionary<string, Scene> Scenes { get; private set; } = new Dictionary<string, Scene>(StringComparer.OrdinalIgnoreCase);

        // Key: Model name (e.g., from Assimp.Mesh.Name, potentially prefixed for uniqueness)
        public static Dictionary<string, Mesh> AllModels { get; private set; } = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);

        public static bool AddScene(Scene scene)
        {
            if (scene == null || string.IsNullOrEmpty(scene.Name))
            {
                Debug.LogError("ModelDatabase.AddScene: Cannot add null scene or scene with no name.");
                return false;
            }

            if (Scenes.ContainsKey(scene.Name))
            {
                Debug.LogWarning($"ModelDatabase.AddScene: Scene with name '{scene.Name}' already exists. Overwriting.");
                Scenes[scene.Name]?.Dispose(); // Dispose the old one before overwriting
            }
            Scenes[scene.Name] = scene;
            Debug.Log($"ModelDatabase: Added scene '{scene.Name}'. Total scenes: {Scenes.Count}");
            return true;
        }

        public static Scene GetScene(string sceneName)
        {
            if (Scenes.TryGetValue(sceneName, out Scene scene))
            {
                return scene;
            }
            Debug.LogWarning($"ModelDatabase.GetScene: Scene '{sceneName}' not found.");
            return null;
        }

        /// <summary>
        /// Adds a model to the global model cache.
        /// The model's name is used as the key. Consider a naming strategy for uniqueness.
        /// </summary>
        public static bool AddModel(Mesh model, string desiredName = null)
        {
            if (model == null)
            {
                Debug.LogError("ModelDatabase.AddModel: Cannot add null model.");
                return false;
            }

            // Use a provided name or attempt to derive one (e.g., from mesh properties if it had a .Name field)
            // For now, we'll require a name or use a placeholder.
            // Let's assume GLBImporter will provide a sensible name.
            string modelKey = desiredName;
            if (string.IsNullOrEmpty(modelKey))
            {
                // If Mesh had a Name property: modelKey = model.Name;
                // For now, let's use a hash code if no name, though not ideal for retrieval by "name"
                modelKey = $"UnnamedMesh_{model.GetHashCode()}";
                Debug.LogWarning($"ModelDatabase.AddModel: Model has no desiredName, using generated key: {modelKey}");
            }


            if (AllModels.ContainsKey(modelKey))
            {
                // If names are not unique, this is problematic.
                // Option 1: Overwrite & Dispose old (as done for scenes)
                // Option 2: Append a number / make unique
                // Option 3: Store a List<Mesh> per name
                // For now, let's log and overwrite for simplicity, assuming user wants unique names.
                Debug.LogWarning($"ModelDatabase.AddModel: Model with name '{modelKey}' already exists in AllModels. Overwriting.");
                AllModels[modelKey]?.Dispose(); // Dispose old one
            }
            AllModels[modelKey] = model;
            Debug.Log($"ModelDatabase: Added model '{modelKey}' to AllModels. Total models in DB: {AllModels.Count}");
            return true;
        }

        public static Mesh GetModel(string modelName)
        {
            if (AllModels.TryGetValue(modelName, out Mesh model))
            {
                return model;
            }
            Debug.LogWarning($"ModelDatabase.GetModel: Model '{modelName}' not found in AllModels.");
            return null;
        }

        /// <summary>
        /// Disposes all loaded scenes (which in turn dispose their models)
        /// and clears the database.
        /// </summary>
        public static void ClearAndDisposeAll()
        {
            Debug.Log("ModelDatabase: Clearing and disposing all scenes and models...");
            foreach (var sceneEntry in Scenes)
            {
                sceneEntry.Value?.Dispose();
            }
            Scenes.Clear();

            // Models within scenes are already disposed.
            // If AllModels contains references that might NOT be in scenes, or if it's the primary owner:
            foreach (var modelEntry in AllModels)
            {
                // Check if already disposed (e.g. via scene), though double dispose is usually safe in our IDisposable
                modelEntry.Value?.Dispose();
            }
            AllModels.Clear();
            Debug.Log("ModelDatabase: All cleared.");
        }
    }
}