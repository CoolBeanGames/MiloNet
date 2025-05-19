// In MiloRender/DataTypes/Texture2D.cs
using Debugger;
using Silk.NET.OpenGL;
using System;

namespace MiloRender.DataTypes
{
    public class Texture2D : IDisposable
    {
        private GL _gl; // Keep a reference to the GL context for uploading and disposal

        public uint Handle { get; private set; }
        public string Name { get; private set; } // Optional name for debugging
        public int Width { get; private set; }
        public int Height { get; private set; }
        public byte[] PixelData { get; private set; } // Raw pixel data (e.g., RGBA)

        public bool IsUploaded { get; private set; } = false;
        private bool _isDisposed = false;

        /// <summary>
        /// Initializes a new Texture2D container.
        /// The GL context is required for later uploading data to the GPU and for disposal.
        /// </summary>
        /// <param name="gl">The OpenGL context.</param>
        /// <param name="name">An optional name for the texture (e.g., from GLB metadata).</param>
        public Texture2D(GL gl, string name = "Unnamed Texture")
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            Name = name;
            Handle = 0; // Will be set after uploading to GPU
        }

        /// <summary>
        /// Sets the pixel data for this texture. This should be called before UploadToGPU.
        /// </summary>
        /// <param name="width">Width of the texture.</param>
        /// <param name="height">Height of the texture.</param>
        /// <param name="pixelData">The raw pixel data (e.g., RGBA byte array).</param>
        public void SetPixelData(int width, int height, byte[] pixelData)
        {
            if (IsUploaded)
            {
                Debug.LogWarning($"Texture2D '{Name}': Cannot set pixel data after texture has been uploaded to GPU. Please Dispose and recreate if changes are needed.");
                return;
            }
            if (pixelData == null)
            {
                Debug.LogError($"Texture2D '{Name}': Provided pixelData is null.");
                return;
            }
            // Basic validation, assuming RGBA for now (4 bytes per pixel)
            if (pixelData.Length != width * height * 4)
            {
                Debug.LogWarning($"Texture2D '{Name}': Pixel data length ({pixelData.Length}) does not match dimensions ({width}x{height}x4). Ensure data is RGBA.");
                // Allow proceeding, but it might cause issues during upload if format is unexpected.
            }

            Width = width;
            Height = height;
            PixelData = pixelData; // Store a reference to the data
            Debug.Log($"Texture2D '{Name}': Pixel data set. Dimensions: {Width}x{Height}. Ready for GPU upload.");
        }

        /// <summary>
        /// Uploads the stored PixelData to an OpenGL texture.
        /// Assumes PixelData is in RGBA format.
        /// </summary>
        public unsafe void UploadToGPU()
        {
            if (_gl == null)
            {
                Debug.LogError($"Texture2D '{Name}': GL context is null. Cannot upload to GPU.");
                return;
            }
            if (IsUploaded)
            {
                Debug.LogWarning($"Texture2D '{Name}': Already uploaded to GPU (Handle: {Handle}). Skipping re-upload.");
                return;
            }
            if (PixelData == null || PixelData.Length == 0)
            {
                Debug.LogError($"Texture2D '{Name}': No pixel data to upload.");
                return;
            }
            if (Width <= 0 || Height <= 0)
            {
                Debug.LogError($"Texture2D '{Name}': Invalid dimensions ({Width}x{Height}). Cannot upload.");
                return;
            }

            Handle = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, Handle);

            // Set texture parameters for PS1 style
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            // No mipmaps for PS1 style by default.

            fixed (byte* pData = PixelData)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)Width, (uint)Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pData);
            }

            _gl.BindTexture(TextureTarget.Texture2D, 0); // Unbind

            IsUploaded = true;
            Debug.Log($"Texture2D '{Name}': Successfully uploaded to GPU. Handle: {Handle}, Dimensions: {Width}x{Height}.");

            // Optional: Clear CPU-side pixel data after upload to save memory,
            // but only if you are sure it won't be needed again (e.g., for re-uploads or modifications).
            // For PS1 resolutions, this might not be a critical memory save.
            // PixelData = null;
            // Debug.Log($"Texture2D '{Name}': CPU-side pixel data cleared after GPU upload.");
        }


        public void Bind(TextureUnit textureUnit = TextureUnit.Texture0)
        {
            if (!IsUploaded || Handle == 0 || _gl == null) return;
            _gl.ActiveTexture(textureUnit);
            _gl.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void Unbind(TextureUnit textureUnit = TextureUnit.Texture0)
        {
            if (_gl == null) return;
            _gl.ActiveTexture(textureUnit);
            _gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                // No other managed resources to dispose explicitly here
            }

            if (Handle != 0 && _gl != null)
            {
                try
                {
                    _gl.DeleteTexture(Handle);
                    Debug.Log($"Texture2D '{Name}': Deleted texture Handle {Handle}.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Texture2D '{Name}': Error deleting texture Handle {Handle}. GL context might be lost. {ex.Message}");
                }
                Handle = 0;
            }
            IsUploaded = false;
            _isDisposed = true;
            PixelData = null; // Release reference to pixel data
        }

        ~Texture2D()
        {
            Dispose(false);
        }
    }
}