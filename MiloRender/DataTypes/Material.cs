// In MiloRender/DataTypes/Material.cs
using Silk.NET.OpenGL;
using System.Numerics; // For Vector4
using Debugger;

namespace MiloRender.DataTypes
{
    public class Material
    {
        // We won't store ShaderProgram here anymore, as the Renderer manages the active shader.
        // The material will primarily define shader *parameters* (uniforms).

        public Vector4 BaseColorTint { get; set; }
        // public uint AlbedoTexture { get; set; } // For later

        // We might add other properties like shininess, metallic, roughness etc.

        public Material()
        {
            BaseColorTint = Vector4.One; // Default to white (1,1,1,1)
            Debug.Log("Material: Default material created.");
        }

        /// <summary>
        /// Applies material properties (uniforms) to the currently bound shader program.
        /// </summary>
        /// <param name="gl">The OpenGL context.</param>
        /// <param name="shaderProgramHandle">The handle of the currently active shader program.</param>
        public virtual void ApplyMaterial(GL gl, uint shaderProgramHandle)
        {
            if (gl == null || shaderProgramHandle == 0)
            {
                Debug.LogWarning("Material.ApplyMaterial: GL context or shader program handle is invalid.");
                return;
            }

            // Example: Set a base color tint uniform if the shader supports it
            // For our current simple shader, this isn't used, but it's good to have the structure.
            // int tintColorLoc = gl.GetUniformLocation(shaderProgramHandle, "u_baseColorTint");
            // if (tintColorLoc != -1)
            // {
            //     gl.Uniform4(tintColorLoc, BaseColorTint.X, BaseColorTint.Y, BaseColorTint.Z, BaseColorTint.W);
            // }
            // else
            // {
            //     // Debug.LogWarning("Material.ApplyMaterial: Uniform 'u_baseColorTint' not found in shader.");
            // }

            // Later, for textures:
            // int albedoTexLoc = gl.GetUniformLocation(shaderProgramHandle, "u_albedoTexture");
            // if (albedoTexLoc != -1 && AlbedoTexture != 0)
            // {
            //     gl.ActiveTexture(TextureUnit.Texture0); // Activate texture unit 0
            //     gl.BindTexture(TextureTarget.Texture2D, AlbedoTexture);
            //     gl.Uniform1(albedoTexLoc, 0); // Tell shader to use texture unit 0 for u_albedoTexture
            // }
        }
    }
}