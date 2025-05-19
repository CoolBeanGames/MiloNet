// In MiloRender/DataTypes/Material.cs
using Silk.NET.OpenGL;
using System.Numerics;
using Debugger;

namespace MiloRender.DataTypes
{
    public class Material
    {
        public Vector4 BaseColorTint { get; set; }
        public Texture2D AlbedoTexture { get; set; } // This property remains

        public Material()
        {
            BaseColorTint = Vector4.One;
            AlbedoTexture = null;
            // Debug.Log("Material: Default material created."); // Less verbose
        }

        // Constructor to easily assign a texture
        public Material(Texture2D albedoTexture) : this()
        {
            AlbedoTexture = albedoTexture;
        }

        public virtual void ApplyMaterial(GL gl, uint shaderProgramHandle)
        {
            if (gl == null || shaderProgramHandle == 0)
            {
                // Debug.LogWarning("Material.ApplyMaterial: GL context or shader program handle is invalid.");
                return;
            }

            // Tint (example, shader doesn't use u_baseColorTint yet)
            int tintColorLoc = gl.GetUniformLocation(shaderProgramHandle, "u_baseColorTint");
            if (tintColorLoc != -1)
            {
                gl.Uniform4(tintColorLoc, BaseColorTint.X, BaseColorTint.Y, BaseColorTint.Z, BaseColorTint.W);
            }

            // Apply Albedo Texture
            if (AlbedoTexture != null)
            {
                if (!AlbedoTexture.IsUploaded)
                {
                    // Option 1: Try to upload it now (might introduce stutter if called mid-frame for new textures)
                    // Debug.LogWarning($"Material.ApplyMaterial: AlbedoTexture '{AlbedoTexture.Name}' not uploaded. Attempting upload.");
                    // AlbedoTexture.UploadToGPU();

                    // Option 2: Log a warning and skip (safer for performance during draw calls)
                    Debug.LogWarning($"Material.ApplyMaterial: AlbedoTexture '{AlbedoTexture.Name}' (Handle: {AlbedoTexture.Handle}) is not uploaded to GPU. Skipping bind.");
                    return; // Or proceed without texture
                }

                if (AlbedoTexture.Handle == 0) // Double check after IsUploaded potentially
                {
                    Debug.LogWarning($"Material.ApplyMaterial: AlbedoTexture '{AlbedoTexture.Name}' is marked uploaded but Handle is 0. Skipping bind.");
                    return;
                }

                int albedoTexUniformLoc = gl.GetUniformLocation(shaderProgramHandle, "u_albedoTexture");
                if (albedoTexUniformLoc != -1)
                {
                    AlbedoTexture.Bind(TextureUnit.Texture0); // Bind to texture unit 0
                    gl.Uniform1(albedoTexUniformLoc, 0);      // Tell shader sampler to use texture unit 0
                }
                // else
                // {
                //     Debug.LogWarning($"Material.ApplyMaterial: Uniform 'u_albedoTexture' not found in shader for texture '{AlbedoTexture.Name}'.");
                // }
            }
        }
    }
}