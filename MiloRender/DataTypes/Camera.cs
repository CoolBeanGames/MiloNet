// In MiloRender/DataTypes/Camera.cs
using SilkMath = Silk.NET.Maths; // Alias for Silk.NET.Maths
using Debugger;

namespace MiloRender.DataTypes
{
    public class Camera
    {
        public Transform Transform { get; private set; }

        private float _fieldOfViewDegrees = 60.0f;
        private float _aspectRatio = 4.0f / 3.0f;
        private float _nearClipPlane = 0.1f;
        private float _farClipPlane = 1000.0f;

        private SilkMath.Matrix4X4<float> _viewMatrix;
        private SilkMath.Matrix4X4<float> _projectionMatrix;

        private bool _isViewDirty = true;
        private bool _isProjectionDirty = true;

        public float FieldOfViewDegrees
        {
            get => _fieldOfViewDegrees;
            set { _fieldOfViewDegrees = value; _isProjectionDirty = true; }
        }
        public float AspectRatio
        {
            get => _aspectRatio;
            set { _aspectRatio = value; _isProjectionDirty = true; }
        }
        public float NearClipPlane
        {
            get => _nearClipPlane;
            set { _nearClipPlane = value; _isProjectionDirty = true; }
        }
        public float FarClipPlane
        {
            get => _farClipPlane;
            set { _farClipPlane = value; _isProjectionDirty = true; }
        }

        public Camera(float screenWidth = 320, float screenHeight = 240)
        {
            Transform = new Transform();
            if (screenHeight > 0) { _aspectRatio = screenWidth / screenHeight; }
            else { _aspectRatio = 4.0f / 3.0f; Debug.LogWarning("Camera: screenHeight was 0, defaulting aspect ratio."); }

            RecalculateProjectionMatrix();
            RecalculateViewMatrix();
        }

        private void RecalculateViewMatrix()
        {
            SilkMath.Matrix4X4<float> cameraWorldMatrix = Transform.ModelMatrix;
            if (SilkMath.Matrix4X4.Invert(cameraWorldMatrix, out _viewMatrix)) // Should infer T for Invert as float
            {
                _isViewDirty = false;
            }
            else
            {
                Debug.LogError("Camera: Failed to invert camera world matrix for view matrix calculation.");
                _viewMatrix = SilkMath.Matrix4X4<float>.Identity; // Fallback
            }
        }

        private void RecalculateProjectionMatrix()
        {
            // CreatePerspectiveFieldOfView is a static helper method that takes float arguments
            // and returns Matrix4X4<float>
            _projectionMatrix = SilkMath.Matrix4X4.CreatePerspectiveFieldOfView(
                SilkMath.Scalar.DegreesToRadians(_fieldOfViewDegrees), // Scalar is non-generic
                _aspectRatio,
                _nearClipPlane,
                _farClipPlane);
            _isProjectionDirty = false;
        }

        public SilkMath.Matrix4X4<float> GetViewMatrix()
        {
            RecalculateViewMatrix();
            return _viewMatrix;
        }

        public SilkMath.Matrix4X4<float> GetProjectionMatrix()
        {
            if (_isProjectionDirty)
            {
                RecalculateProjectionMatrix();
            }
            return _projectionMatrix;
        }

        public void SetRenderTargetResolution(float width, float height)
        {
            if (height <= 0)
            {
                Debug.LogWarning("Camera: Attempted to set invalid height for render target resolution.");
                return;
            }
            AspectRatio = width / height;
        }
    }
}