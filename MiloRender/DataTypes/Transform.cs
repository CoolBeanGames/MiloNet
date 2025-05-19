// In MiloRender/DataTypes/Transform.cs
using Silk.NET.Maths;
using Debugger; // If you use Debug for anything here
// SilkMath alias is fine if you only use it, or qualify if mixed
using SilkMath = Silk.NET.Maths;
using System;

namespace MiloRender.DataTypes
{
    public class Transform
    {
        private SilkMath.Vector3D<float> _localPosition;
        private SilkMath.Quaternion<float> _localRotation;
        private SilkMath.Vector3D<float> _localScale;

        private SilkMath.Matrix4X4<float> _localToWorldMatrix;
        private bool _isDirty = true;

        public SilkMath.Vector3D<float> LocalPosition
        {
            get => _localPosition;
            set { _localPosition = value; _isDirty = true; OnTransformChanged(); }
        }

        public SilkMath.Quaternion<float> LocalRotation
        {
            get => _localRotation;
            set { _localRotation = SilkMath.Quaternion<float>.Normalize(value); _isDirty = true; OnTransformChanged(); }
        }

        public SilkMath.Vector3D<float> LocalScale
        {
            get => _localScale;
            set { _localScale = value; _isDirty = true; OnTransformChanged(); }
        }

        public SilkMath.Matrix4X4<float> ModelMatrix
        {
            get
            {
                if (_isDirty)
                {
                    RecalculateModelMatrix();
                }
                return _localToWorldMatrix;
            }
        }

        // World properties derived from the ModelMatrix
        public SilkMath.Vector3D<float> WorldPosition => new SilkMath.Vector3D<float>(ModelMatrix.M41, ModelMatrix.M42, ModelMatrix.M43);

        // Getting world rotation from matrix is complex; usually, it's better if parent transforms also provide world rotation.
        // For a simple case where this transform is a root or only has uniform scaling parents:
        public SilkMath.Quaternion<float> WorldRotation => SilkMath.Quaternion<float>.CreateFromRotationMatrix(ModelMatrix); // Note: This can be problematic if there's non-uniform scaling in parents

        // --- NEW DIRECTIONAL PROPERTIES ---
        public SilkMath.Vector3D<float> Forward
        {
            get
            {
                // Standard forward is -Z in a right-handed system if you want to look "into" the screen
                // Or +Z if that's your convention. Let's assume +Z is forward for model space.
                // This is local +Z axis transformed into world space by the rotation part of the model matrix.
                // A simpler way is to rotate Vector3D.UnitZ by the WorldRotation.
                // return SilkMath.Vector3D.Transform(SilkMath.Vector3D<float>.UnitZ, WorldRotation);
                // Or from the matrix columns (assuming standard OpenGL view where -Z is forward out of camera)
                // If +Z is model forward:
                return SilkMath.Vector3D.Normalize(new SilkMath.Vector3D<float>(ModelMatrix.M31, ModelMatrix.M32, ModelMatrix.M33));
            }
        }

        public SilkMath.Vector3D<float> Up
        {
            get
            {
                // Local +Y axis transformed into world space
                // return SilkMath.Vector3D.Transform(SilkMath.Vector3D<float>.UnitY, WorldRotation);
                return SilkMath.Vector3D.Normalize(new SilkMath.Vector3D<float>(ModelMatrix.M21, ModelMatrix.M22, ModelMatrix.M23));
            }
        }

        public SilkMath.Vector3D<float> Right
        {
            get
            {
                // Local +X axis transformed into world space
                // return SilkMath.Vector3D.Transform(SilkMath.Vector3D<float>.UnitX, WorldRotation);
                return SilkMath.Vector3D.Normalize(new SilkMath.Vector3D<float>(ModelMatrix.M11, ModelMatrix.M12, ModelMatrix.M13));
            }
        }
        // --- END NEW ---


        public Transform()
        {
            _localPosition = SilkMath.Vector3D<float>.Zero;
            _localRotation = SilkMath.Quaternion<float>.Identity;
            _localScale = SilkMath.Vector3D<float>.One;
            // _localToWorldMatrix = SilkMath.Matrix4X4<float>.Identity; // Recalculate will set it
            RecalculateModelMatrix(); // Ensure matrix is initialized
        }

        private void RecalculateModelMatrix()
        {
            SilkMath.Matrix4X4<float> scaleMatrix = SilkMath.Matrix4X4.CreateScale(_localScale);
            SilkMath.Matrix4X4<float> rotationMatrix = SilkMath.Matrix4X4.CreateFromQuaternion(_localRotation);
            SilkMath.Matrix4X4<float> translationMatrix = SilkMath.Matrix4X4.CreateTranslation(_localPosition);

            // Standard TRS order: Scale -> Rotate -> Translate
            _localToWorldMatrix = scaleMatrix * rotationMatrix * translationMatrix;
            _isDirty = false;
        }

        public bool IsDirty() => _isDirty;

        // Optional: A way to notify if transform changed (for things like camera view matrix)
        public event Action TransformChanged;
        protected virtual void OnTransformChanged() => TransformChanged?.Invoke();


        public void Translate(SilkMath.Vector3D<float> translation, Space relativeTo = Space.Self)
        {
            if (relativeTo == Space.Self)
            {
                // To translate in local space, transform the translation vector by the current rotation
                LocalPosition += SilkMath.Vector3D.Transform(translation, _localRotation);
            }
            else // World space
            {
                LocalPosition += translation;
            }
        }

        public void Rotate(SilkMath.Quaternion<float> additionalRotation, Space relativeTo = Space.Self)
        {
            if (relativeTo == Space.Self) // Post-multiply: newRotation = oldRotation * additionalRotation
            {
                LocalRotation = _localRotation * additionalRotation;
            }
            else // Pre-multiply: newRotation = additionalRotation * oldRotation (applies rotation in world axes)
            {
                LocalRotation = additionalRotation * _localRotation;
            }
        }

        public void Rotate(SilkMath.Vector3D<float> axis, float angleDegrees, Space relativeTo = Space.Self)
        {
            Rotate(SilkMath.Quaternion<float>.CreateFromAxisAngle(axis, Scalar.DegreesToRadians(angleDegrees)), relativeTo);
        }


        public void LookAt(SilkMath.Vector3D<float> worldTarget, SilkMath.Vector3D<float> worldUp)
        {
            // This object's world position
            SilkMath.Vector3D<float> worldPosition = this.WorldPosition; // Requires ModelMatrix to be up-to-date

            // CreateLookAt makes a VIEW matrix. We need to invert it to get a MODEL matrix's rotation.
            SilkMath.Matrix4X4<float> lookAtViewMatrix = SilkMath.Matrix4X4.CreateLookAt(worldPosition, worldTarget, worldUp);

            // Invert the view matrix to get the model's world orientation matrix
            if (SilkMath.Matrix4X4.Invert(lookAtViewMatrix, out SilkMath.Matrix4X4<float> modelWorldOrientationMatrix))
            {
                // This modelWorldOrientationMatrix now represents the desired world rotation.
                // We need to set our _localRotation based on this.
                // If this Transform has a parent, this gets more complex as we'd need to convert
                // the desired world rotation into a local rotation relative to the parent.
                // For a root object, its world rotation is its local rotation.
                // Assuming this transform is effectively a root for its rotation, or we simplify for now:
                LocalRotation = SilkMath.Quaternion<float>.CreateFromRotationMatrix(modelWorldOrientationMatrix);
            }
            else
            {
                Debug.LogWarning($"Transform.LookAt for '{this.GetHashCode()}' at {worldPosition}: Could not invert look-at view matrix.");
            }
        }
    }
}