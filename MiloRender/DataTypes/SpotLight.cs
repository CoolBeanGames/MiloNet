// In MiloRender/DataTypes/SpotLight.cs
using Silk.NET.Maths;
using System;
using System.Numerics; // For Color

namespace MiloRender.DataTypes
{
    // Helper for Math.Clamp as it's not in .NET Framework 4.7.2's System.Math
    internal static class MathHelper
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
        public float CutOffAngleCosine { get; set; }
        public float OuterCutOffAngleCosine { get; set; }
        public float Range { get; set; }

        public SpotLight() : base()
        {
            Type = LightType.Spot;
            SetCutOffAngles(12.5f, 17.5f);
            Range = 50.0f;
        }

        public void SetCutOffAngles(float innerConeDegrees, float outerConeDegrees)
        {
            // Use System.Math for .NET Framework 4.7.2 and cast to float
            outerConeDegrees = (float)Math.Max(innerConeDegrees, outerConeDegrees);
            CutOffAngleCosine = (float)Math.Cos(Scalar.DegreesToRadians(innerConeDegrees));
            OuterCutOffAngleCosine = (float)Math.Cos(Scalar.DegreesToRadians(outerConeDegrees));
        }

        public override Vector3D<float> GetDirectionToLight(Vector3D<float> worldPosition)
        {
            // For spotlight, direction is from point to light's position
            return Vector3D.Normalize(Transform.WorldPosition - worldPosition);
        }

        public override float GetIntensityAtPoint(Vector3D<float> worldPosition, Vector3D<float> worldNormal)
        {
            Vector3D<float> vectorFromLightToPoint = worldPosition - Transform.WorldPosition;
            float distanceToPoint = vectorFromLightToPoint.Length;

            if (Range > 0 && distanceToPoint > Range) return 0.0f;

            float attenuation = 1.0f;
            if (Range > 0)
            {
                // Use our MathHelper.Clamp
                attenuation = MathHelper.Clamp(1.0f - (distanceToPoint / Range), 0.0f, 1.0f);
                attenuation *= attenuation;
            }
            if (attenuation <= 0.005f) return 0.0f;

            Vector3D<float> lightForwardDir = Transform.Forward; // Uses new Transform.Forward
            Vector3D<float> normalizedDirFromLightToPoint = Vector3D.Normalize(vectorFromLightToPoint);
            float theta = Vector3D.Dot(-normalizedDirFromLightToPoint, lightForwardDir);

            float spotEffect = 0.0f;
            if (theta > OuterCutOffAngleCosine)
            {
                if (theta > CutOffAngleCosine)
                {
                    spotEffect = 1.0f;
                }
                else
                {
                    spotEffect = (theta - OuterCutOffAngleCosine) / (CutOffAngleCosine - OuterCutOffAngleCosine);
                    // Use our MathHelper.Clamp
                    spotEffect = MathHelper.Clamp(spotEffect, 0.0f, 1.0f);
                }
            }
            if (spotEffect <= 0.005f) return 0.0f;

            Vector3D<float> dirToLightSourceNormalized = GetDirectionToLight(worldPosition);
            float basicFacingFactor = Math.Max(0.0f, Vector3D.Dot(worldNormal, dirToLightSourceNormalized));
            if (basicFacingFactor <= 0.005f) return 0.0f;

            return this.Intensity * attenuation * spotEffect;
        }
    }
}