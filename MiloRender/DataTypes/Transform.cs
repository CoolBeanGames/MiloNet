// In MiloRender/DataTypes/Transform.cs
using SilkMath = Silk.NET.Maths; // Alias for Silk.NET.Maths
using Debugger;

namespace MiloRender.DataTypes
{
    public class Transform
    {
        private SilkMath.Vector3D<float> _localPosition;    // Using Vector3D<float>
        private SilkMath.Quaternion<float> _localRotation; // Using Quaternion<float>
        private SilkMath.Vector3D<float> _localScale;       // Using Vector3D<float>

        private SilkMath.Matrix4X4<float> _localToWorldMatrix; // Using Matrix4X4<float>
        private bool _isDirty;

        public SilkMath.Vector3D<float> LocalPosition
        {
            get => _localPosition;
            set { _localPosition = value; _isDirty = true; }
        }

        public SilkMath.Quaternion<float> LocalRotation
        {
            get => _localRotation;
            set { _localRotation = value; _isDirty = true; }
        }

        public SilkMath.Vector3D<float> LocalScale
        {
            get => _localScale;
            set { _localScale = value; _isDirty = true; }
        }

        // Matrix4X4<T> has a .Translation property which returns Vector3D<T>
        public SilkMath.Vector3D<float> WorldPosition =>
            new SilkMath.Vector3D<float>(ModelMatrix.M41, ModelMatrix.M42, ModelMatrix.M43); // Construct directly

        public SilkMath.Quaternion<float> WorldRotation => SilkMath.Quaternion<float>.CreateFromRotationMatrix(ModelMatrix);

        public Transform()
        {
            _localPosition = SilkMath.Vector3D<float>.Zero;       // Static member on Vector3D<T>
            _localRotation = SilkMath.Quaternion<float>.Identity;  // Static member on Quaternion<T>
            _localScale = SilkMath.Vector3D<float>.One;        // Static member on Vector3D<T>
            _localToWorldMatrix = SilkMath.Matrix4X4<float>.Identity; // Static member on Matrix4X4<T>
            _isDirty = false;
        }

        public SilkMath.Matrix4X4<float> GetLocalTransformMatrix()
        {
            // Static factory methods are on the generic type itself.
            SilkMath.Matrix4X4<float> scaleMatrix = SilkMath.Matrix4X4.CreateScale(_localScale); // CreateScale takes Vector3D<T>
            SilkMath.Matrix4X4<float> rotationMatrix = SilkMath.Matrix4X4.CreateFromQuaternion(_localRotation); // CreateFromQuaternion takes Quaternion<T>
            SilkMath.Matrix4X4<float> translationMatrix = SilkMath.Matrix4X4.CreateTranslation(_localPosition); // CreateTranslation takes Vector3D<T>
            return scaleMatrix * rotationMatrix * translationMatrix;
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

        private void RecalculateModelMatrix()
        {
            _localToWorldMatrix = GetLocalTransformMatrix();
            _isDirty = false;
        }

        public void Translate(SilkMath.Vector3D<float> translation, Space relativeTo = Space.Self)
        {
            if (relativeTo == Space.Self)
            {
                // Vector3D.Transform(Vector3D<T>, Quaternion<T>)
                LocalPosition += SilkMath.Vector3D.Transform(translation, _localRotation);
            }
            else
            {
                LocalPosition += translation;
            }
        }

        public void LookAt(SilkMath.Vector3D<float> target, SilkMath.Vector3D<float> worldUp)
        {
            // CreateLookAt takes Vector3D<T> arguments
            SilkMath.Matrix4X4<float> lookMatrix = SilkMath.Matrix4X4.CreateLookAt(LocalPosition, target, worldUp);
            SilkMath.Matrix4X4<float> objectWorldMatrix;
            // Invert is a static method on Matrix4X4 (non-generic helper) or Matrix4X4<T>
            if (SilkMath.Matrix4X4.Invert(lookMatrix, out objectWorldMatrix)) // This should correctly infer T as float
            {
                LocalRotation = SilkMath.Quaternion<float>.CreateFromRotationMatrix(objectWorldMatrix);
            }
            else
            {
                Debug.LogWarning("Transform.LookAt: Could not invert look-at matrix.");
            }
        }
    }
}