// In MiloRender/DataTypes/Material.cs
using Silk.NET.OpenGL; // For later shader program handle
using System.Numerics; // For Vector4
using Debugger;

namespace MiloRender.DataTypes
{
    public class Material
    {
        // Placeholder for a shader program handle (ID from OpenGL)
        public uint ShaderProgram { get; set; }

        // Placeholder for a base color tint
        public Vector4 BaseColorTint { get; set; }

        // Placeholder for texture IDs (you might have multiple textures)
        // public uint AlbedoTexture { get; set; }

        public Material()
        {
            BaseColorTint = Vector4.One; // Default to white (1,1,1,1)
            ShaderProgram = 0; // No shader by default
            // AlbedoTexture = 0; // No texture by default
            Debug.Log("Material: Default material created.");
        }

        // A method the Renderer might call to set up this material for drawing
        public virtual void ApplyMaterial(GL gl)
        {
            // In a real scenario:
            // gl.UseProgram(ShaderProgram);
            // gl.ActiveTexture(TextureUnit.Texture0);
            // gl.BindTexture(TextureTarget.Texture2D, AlbedoTexture);
            // Set other uniforms like BaseColorTint, etc.
            // Debug.Log($"Material: Applying material (Shader: {ShaderProgram}, Tint: {BaseColorTint})");
        }
    }
}