// In MiloRender/DataTypes/Transform.cs
using Silk.NET.Maths; // Ensure Silk.NET.Maths NuGet package is referenced in MiloRender.csproj
using Debugger;
using SilkMath = Silk.NET.Maths; // Alias

namespace MiloRender.DataTypes
{
    public class Transform
    {
        private SilkMath.Vector3D<float> _localPosition;
        private SilkMath.Quaternion<float> _localRotation;
        private SilkMath.Vector3D<float> _localScale;

        // THIS IS THE FIELD THE COMPILER IS MISSING
        private SilkMath.Matrix4X4<float> _localToWorldMatrix;
        private bool _isDirty = true;

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

        public SilkMath.Matrix4X4<float> ModelMatrix
        {
            get
            {
                if (_isDirty)
                {
                    // THIS IS THE METHOD THE COMPILER IS MISSING
                    RecalculateModelMatrix();
                }
                return _localToWorldMatrix;
            }
        }

        public SilkMath.Vector3D<float> WorldPosition =>
            new SilkMath.Vector3D<float>(ModelMatrix.M41, ModelMatrix.M42, ModelMatrix.M43);

        public SilkMath.Quaternion<float> WorldRotation => SilkMath.Quaternion<float>.CreateFromRotationMatrix(ModelMatrix);

        public Transform()
        {
            _localPosition = SilkMath.Vector3D<float>.Zero;
            _localRotation = SilkMath.Quaternion<float>.Identity;
            _localScale = SilkMath.Vector3D<float>.One;
            _localToWorldMatrix = SilkMath.Matrix4X4<float>.Identity; // Initialize the matrix
            _isDirty = true;
        }

        // THIS IS THE METHOD THE COMPILER IS MISSING
        private void RecalculateModelMatrix()
        {
            SilkMath.Matrix4X4<float> scaleMatrix = SilkMath.Matrix4X4.CreateScale(_localScale);
            SilkMath.Matrix4X4<float> rotationMatrix = SilkMath.Matrix4X4.CreateFromQuaternion(_localRotation);
            SilkMath.Matrix4X4<float> translationMatrix = SilkMath.Matrix4X4.CreateTranslation(_localPosition);

            _localToWorldMatrix = scaleMatrix * rotationMatrix * translationMatrix;
            _isDirty = false;
        }

        public bool IsDirty() => _isDirty; // Public accessor for the dirty flag

        public void Translate(SilkMath.Vector3D<float> translation, Space relativeTo = Space.Self)
        {
            if (relativeTo == Space.Self)
            {
                LocalPosition += SilkMath.Vector3D.Transform(translation, _localRotation);
            }
            else
            {
                LocalPosition += translation;
            }
        }

        public void Rotate(SilkMath.Quaternion<float> rotation, Space relativeTo = Space.Self)
        {
            if (relativeTo == Space.Self)
            {
                LocalRotation = _localRotation * rotation;
            }
            else
            {
                LocalRotation = rotation * _localRotation;
            }
        }

        public void LookAt(SilkMath.Vector3D<float> target, SilkMath.Vector3D<float> worldUp)
        {
            SilkMath.Matrix4X4<float> lookAtViewMatrix = SilkMath.Matrix4X4.CreateLookAt(LocalPosition, target, worldUp);
            if (SilkMath.Matrix4X4.Invert(lookAtViewMatrix, out SilkMath.Matrix4X4<float> modelLookMatrix))
            {
                LocalRotation = SilkMath.Quaternion<float>.CreateFromRotationMatrix(modelLookMatrix);
            }
            else
            {
                Debug.LogWarning("Transform.LookAt: Could not invert look-at matrix to derive rotation.");
            }
        }
    }
}