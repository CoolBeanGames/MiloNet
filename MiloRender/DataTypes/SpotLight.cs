// In MiloRender/DataTypes/SpotLight.cs
using Silk.NET.Maths;
using System;
using System.Numerics; // For Color

namespace MiloRender.DataTypes
{
    // Helper for Math.Clamp as it's not in .NET Framework 4.7.2's System.Math
    public static class MathHelper // CHANGED: internal to public
    {
        public static T Clamp<T>(T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }
    }

    public class SpotLight : Light
    {
        public float CutOffAngleCosine { get; set; }      // Cosine of the inner cone half-angle
        public float OuterCutOffAngleCosine { get; set; } // Cosine of the outer cone half-angle
        public float Range { get; set; }

        public SpotLight() : base()
        {
            Type = LightType.Spot;
            // Set default cutoff angles (e.g., inner 12.5 deg, outer 17.5 deg from center)
            SetCutOffAngles(12.5f, 17.5f); // These are half-angles from the center axis
            Range = 50.0f; // Default range
        }

        /// <summary>
        /// Sets the spotlight cone angles.
        /// </summary>
        /// <param name="innerConeHalfAngleDegrees">The half-angle of the inner bright cone, in degrees.</param>
        /// <param name="outerConeHalfAngleDegrees">The half-angle of the outer falloff cone, in degrees.</param>
        public void SetCutOffAngles(float innerConeHalfAngleDegrees, float outerConeHalfAngleDegrees)
        {
            // Ensure outer is not smaller than inner
            outerConeHalfAngleDegrees = (float)Math.Max(innerConeHalfAngleDegrees, outerConeHalfAngleDegrees);

            CutOffAngleCosine = (float)Math.Cos(Scalar.DegreesToRadians(innerConeHalfAngleDegrees));
            OuterCutOffAngleCosine = (float)Math.Cos(Scalar.DegreesToRadians(outerConeHalfAngleDegrees));
        }


        public override Vector3D<float> GetDirectionToLight(Vector3D<float> worldPosition)
        {
            return Vector3D.Normalize(Transform.WorldPosition - worldPosition);
        }

        // This method is more for CPU-side logic if needed, shader handles actual intensity calc.
        public override float GetIntensityAtPoint(Vector3D<float> worldPosition, Vector3D<float> worldNormal)
        {
            Vector3D<float> vectorFromLightToPoint = worldPosition - Transform.WorldPosition;
            float distanceToPoint = vectorFromLightToPoint.Length;

            if (Range > 0 && distanceToPoint > Range) return 0.0f; // Outside range

            float attenuation = 1.0f;
            if (Range > 0)
            {
                attenuation = MathHelper.Clamp(1.0f - (distanceToPoint / Range), 0.0f, 1.0f);
                attenuation *= attenuation; // Squared falloff
            }
            if (attenuation <= 0.005f) return 0.0f;

            Vector3D<float> lightForwardDir = Transform.Forward; // Spotlight's pointing direction
            Vector3D<float> normalizedDirFromLightToPoint = Vector3D.Normalize(vectorFromLightToPoint);

            // Theta is the angle between the vector from light to point and the light's reverse direction.
            // Or, dot product between normalized vector from point to light and light's forward direction.
            float theta = Vector3D.Dot(-normalizedDirFromLightToPoint, lightForwardDir);


            float spotEffect = 0.0f;
            if (theta > OuterCutOffAngleCosine) // Point is within the outer cone
            {
                if (theta > CutOffAngleCosine) // Point is within the inner cone
                {
                    spotEffect = 1.0f;
                }
                else // Point is in the falloff region (penumbra)
                {
                    // Smoothstep or linear interpolation
                    spotEffect = (theta - OuterCutOffAngleCosine) / (CutOffAngleCosine - OuterCutOffAngleCosine);
                    spotEffect = MathHelper.Clamp(spotEffect, 0.0f, 1.0f);
                }
            }
            if (spotEffect <= 0.005f) return 0.0f;

            // Basic diffuse factor (N.L) - this would be part of the shader's job primarily
            // Vector3D<float> dirToLightSourceNormalized = GetDirectionToLight(worldPosition);
            // float NdotL = Math.Max(0.0f, Vector3D.Dot(worldNormal, dirToLightSourceNormalized));
            // if (NdotL <= 0.005f) return 0.0f;

            return this.Intensity * attenuation * spotEffect; // * NdotL if doing full calc here
        }
    }
}