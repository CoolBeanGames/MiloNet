// In MiloRender/DataTypes/DirectionalLight.cs
using Silk.NET.Maths;
using System.Numerics; // For Color

namespace MiloRender.DataTypes
{
    public class DirectionalLight : Light
    {
        public DirectionalLight() : base()
        {
            Type = LightType.Directional;
        }

        public override Vector3D<float> GetDirectionToLight(Vector3D<float> worldPosition)
        {
            // Transform.Forward gives the direction the light is pointing *towards*.
            // The direction *from which* the light rays come (vector TO the light source)
            // is the negative of that.
            return Vector3D.Normalize(-Transform.Forward); // Uses the new Transform.Forward
        }

        public override float GetIntensityAtPoint(Vector3D<float> worldPosition, Vector3D<float> worldNormal)
        {
            Vector3D<float> dirToLight = GetDirectionToLight(worldPosition);
            float dot = Vector3D.Dot(worldNormal, dirToLight);
            return dot > 0.0f ? this.Intensity : 0.0f;
        }
    }
}