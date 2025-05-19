// In MiloRender/DataTypes/Light.cs
using Silk.NET.Maths; // For Vector3D<float>
using System.Numerics; // For Vector4 Color (System.Numerics is often convenient for color)

namespace MiloRender.DataTypes
{
    public abstract class Light
    {
        public LightType Type { get; protected set; }
        public Vector4 Color { get; set; } // RGBA, Alpha often used for intensity or ignored
        public float Intensity { get; set; }
        public bool IsActive { get; set; } // To easily toggle lights on/off

        // Every light will have a transform to define its position and orientation
        // For directional lights, only rotation (direction) matters.
        // For spotlights, position and direction matter.
        public Transform Transform { get; protected set; }

        protected Light()
        {
            Type = LightType.None;
            Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // Default white light
            Intensity = 1.0f;
            IsActive = true;
            Transform = new Transform(); // Each light gets its own transform
        }

        // Method to get light direction (world space) - will be overridden
        public abstract Vector3D<float> GetDirectionToLight(Vector3D<float> worldPosition);

        // Method to get light intensity at a point (considering falloff for point/spot)
        // For directional, it's constant if lit. For spot, depends on angle and distance.
        public abstract float GetIntensityAtPoint(Vector3D<float> worldPosition, Vector3D<float> worldNormal);
    }
}