// In MiloRender/DataTypes/Camera.cs
using Silk.NET.Maths;
using Debugger;
using SilkMath = Silk.NET.Maths;
using System; // Alias for consistency

namespace MiloRender.DataTypes
{
    public class Camera
    {
        private Transform _transform;
        public Transform Transform
        {
            get => _transform;
            set
            {
                if (_transform != value)
                {
                    _transform = value ?? throw new ArgumentNullException(nameof(value), "Camera Transform cannot be null.");
                    _isViewDirty = true;
                }
            }
        }

        private float _fieldOfViewDegrees = 60.0f;
        private float _aspectRatio = 320.0f / 240.0f; // Game world resolution aspect ratio
        private float _nearClipPlane = 0.1f;
        private float _farClipPlane = 1000.0f;

        private SilkMath.Matrix4X4<float> _viewMatrix;
        private SilkMath.Matrix4X4<float> _projectionMatrix;

        private bool _isViewDirty = true;
        private bool _isProjectionDirty = true;

        public float FieldOfViewDegrees
        {
            get => _fieldOfViewDegrees;
            set { if (_fieldOfViewDegrees != value) { _fieldOfViewDegrees = value; _isProjectionDirty = true; } }
        }
        public float AspectRatio
        {
            get => _aspectRatio;
            set { if (_aspectRatio != value) { _aspectRatio = value; _isProjectionDirty = true; } }
        }
        public float NearClipPlane
        {
            get => _nearClipPlane;
            set { if (_nearClipPlane != value) { _nearClipPlane = value; _isProjectionDirty = true; } }
        }
        public float FarClipPlane
        {
            get => _farClipPlane;
            set { if (_farClipPlane != value) { _farClipPlane = value; _isProjectionDirty = true; } }
        }

        public Camera(float gameWorldScreenWidth = 320, float gameWorldScreenHeight = 240)
        {
            Transform = new Transform();

            if (gameWorldScreenHeight > 0)
            {
                // Use the property setter to ensure _isProjectionDirty is set
                AspectRatio = gameWorldScreenWidth / gameWorldScreenHeight;
            }
            else
            {
                AspectRatio = 320.0f / 240.0f; // Default fallback
                Debug.LogWarning("Camera: screenHeight was 0, defaulting aspect ratio to 320/240.");
            }

            // Initial calculations will happen on the first Get...() call if dirty flags are true.
            // Or you can force them here:
            // RecalculateProjectionMatrix();
            // RecalculateViewMatrix();
        }

        private void RecalculateViewMatrix()
        {
            if (Transform == null)
            {
                Debug.LogError("Camera.RecalculateViewMatrix: Transform is null! Using identity view matrix.");
                _viewMatrix = SilkMath.Matrix4X4<float>.Identity;
                _isViewDirty = false; // Reset flag even on error to prevent spam
                return;
            }

            SilkMath.Matrix4X4<float> cameraWorldMatrix = Transform.ModelMatrix;
            if (SilkMath.Matrix4X4.Invert(cameraWorldMatrix, out _viewMatrix))
            {
                _isViewDirty = false;
                // Debug.Log("Camera: ViewMatrix recalculated.");
            }
            else
            {
                Debug.LogError("Camera: Failed to invert camera world matrix for view matrix. Using identity.");
                _viewMatrix = SilkMath.Matrix4X4<float>.Identity;
                _isViewDirty = false;
            }
        }

        private void RecalculateProjectionMatrix()
        {
            _projectionMatrix = SilkMath.Matrix4X4.CreatePerspectiveFieldOfView(
                SilkMath.Scalar.DegreesToRadians(_fieldOfViewDegrees),
                _aspectRatio,
                _nearClipPlane,
                _farClipPlane);
            _isProjectionDirty = false;
            // Debug.Log($"Camera: Projection matrix recalculated. FoV: {_fieldOfViewDegrees}, Aspect: {_aspectRatio}");
        }

        public SilkMath.Matrix4X4<float> GetViewMatrix()
        {
            if (Transform == null) // Should not happen if constructor is used properly
            {
                Debug.LogError("Camera.GetViewMatrix: Transform is null! Returning identity.");
                return SilkMath.Matrix4X4<float>.Identity;
            }

            // Check if the Transform object's internal state (pos, rot, scale) has changed OR
            // if the Camera's view has been explicitly marked dirty (e.g., Transform object instance was swapped).
            if (Transform.IsDirty() || _isViewDirty)
            {
                RecalculateViewMatrix();
            }
            return _viewMatrix;
        }

        // THIS IS THE METHOD THE COMPILER IS LOOKING FOR
        public SilkMath.Matrix4X4<float> GetProjectionMatrix()
        {
            if (_isProjectionDirty)
            {
                RecalculateProjectionMatrix();
            }
            return _projectionMatrix;
        }

        /// <summary>
        /// Sets the aspect ratio based on target resolution. Typically for the game world's fixed aspect ratio.
        /// </summary>
        public void SetGameWorldAspectRatio(float width, float height)
        {
            if (height <= 0)
            {
                Debug.LogWarning("Camera: Attempted to set invalid height for game world aspect ratio.");
                return;
            }
            AspectRatio = width / height; // This will set _isProjectionDirty = true via the property setter
        }
    }
}