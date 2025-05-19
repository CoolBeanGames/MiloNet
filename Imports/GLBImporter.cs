// In Imports/GLBImporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assimp;
using Assimp.Configs;
using Debugger;
using MiloRender.DataTypes;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using StbImageSharp;
using MiloNet; // For ModelDatabase
using System.Numerics;
using System.Globalization; // For float.TryParse with InvariantCulture

// Using aliases
using MiloMesh = MiloRender.DataTypes.Mesh;
using MiloMaterial = MiloRender.DataTypes.Material;
using MiloTexture2D = MiloRender.DataTypes.Texture2D;
using MiloScene = MiloRender.DataTypes.Scene;
using MiloEngineCamera = MiloRender.DataTypes.Camera;
using MiloLight = MiloRender.DataTypes.Light;
using MiloDirectionalLight = MiloRender.DataTypes.DirectionalLight;
using MiloSpotLight = MiloRender.DataTypes.SpotLight;

using AssimpNETScene = Assimp.Scene;
using AssimpNETLight = Assimp.Light;
using AssimpNETCamera = Assimp.Camera;
using AssimpNETVector3D = Assimp.Vector3D;
using AssimpNETMatrix4x4 = Assimp.Matrix4x4;

namespace Imports
{
    public static class GLBImporter
    {
        private static MiloTexture2D _defaultPinkTexture = null;

        private static MiloTexture2D GetOrCreateDefaultPinkTexture(GL gl)
        {
            if (_defaultPinkTexture == null || _defaultPinkTexture.Handle == 0 || !_defaultPinkTexture.IsUploaded)
            {
                Debug.Log("GLBImporter: Creating/Recreating default 1x1 pink texture.");
                _defaultPinkTexture?.Dispose();
                _defaultPinkTexture = new MiloTexture2D(gl, "DefaultPinkTexture");
                byte[] pinkPixelData = { 255, 0, 255, 255 };
                _defaultPinkTexture.SetPixelData(1, 1, pinkPixelData);
                _defaultPinkTexture.UploadToGPU();
            }
            return _defaultPinkTexture;
        }

        private static Silk.NET.Maths.Matrix4X4<float> ToSilkMatrix(AssimpNETMatrix4x4 assimpMatrix)
        {
            return new Silk.NET.Maths.Matrix4X4<float>(
                assimpMatrix.A1, assimpMatrix.B1, assimpMatrix.C1, assimpMatrix.D1,
                assimpMatrix.A2, assimpMatrix.B2, assimpMatrix.C2, assimpMatrix.D2,
                assimpMatrix.A3, assimpMatrix.B3, assimpMatrix.C3, assimpMatrix.D3,
                assimpMatrix.A4, assimpMatrix.B4, assimpMatrix.C4, assimpMatrix.D4
            );
        }

        private static Silk.NET.Maths.Vector3D<float> ToSilkVector3D(AssimpNETVector3D assimpVec)
        {
            return new Silk.NET.Maths.Vector3D<float>(assimpVec.X, assimpVec.Y, assimpVec.Z);
        }

        private static Silk.NET.Maths.Matrix4X4<float> CalculateWorldTransform(Node node)
        {
            if (node == null)
                return Silk.NET.Maths.Matrix4X4<float>.Identity;

            Stack<AssimpNETMatrix4x4> transformStack = new Stack<AssimpNETMatrix4x4>();
            Node currentNode = node;
            while (currentNode != null)
            {
                transformStack.Push(currentNode.Transform);
                currentNode = currentNode.Parent;
            }

            AssimpNETMatrix4x4 finalTransform = AssimpNETMatrix4x4.Identity;
            while (transformStack.Count > 0)
            {
                finalTransform = finalTransform * transformStack.Pop();
            }
            return ToSilkMatrix(finalTransform);
        }

        public static MiloScene LoadGlbAsScene(GL gl, string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) { Debug.LogError("GLBImporter: FilePath is null or empty."); return null; }
            if (!File.Exists(filePath)) { Debug.LogError($"GLBImporter: File not found: {filePath}"); return null; }
            if (gl == null) { Debug.LogError("GLBImporter: GL context is null."); return null; }

            Debug.Log($"GLBImporter.LoadGlbAsScene: Attempting to load GLB: {filePath}");
            AssimpContext importer = new AssimpContext();
            importer.SetConfig(new NormalSmoothingAngleConfig(66.0f));
            const PostProcessSteps steps = PostProcessSteps.Triangulate |
                                           PostProcessSteps.GenerateSmoothNormals |
                                           PostProcessSteps.FlipUVs |
                                           PostProcessSteps.JoinIdenticalVertices |
                                           PostProcessSteps.CalculateTangentSpace |
                                           PostProcessSteps.SortByPrimitiveType |
                                           PostProcessSteps.EmbedTextures |
                                           PostProcessSteps.ImproveCacheLocality;

            AssimpNETScene assimpSceneData;
            try
            {
                assimpSceneData = importer.ImportFile(filePath, steps);
            }
            catch (Exception ex)
            {
                Debug.LogError($"GLBImporter.LoadGlbAsScene: Assimp import failed. Exception: {ex.Message} {ex.StackTrace}");
                importer.Dispose();
                return null;
            }

            if (assimpSceneData == null)
            {
                Debug.LogError("GLBImporter.LoadGlbAsScene: Assimp scene data is null after import.");
                importer.Dispose();
                return null;
            }

            string sceneName = Path.GetFileNameWithoutExtension(filePath);
            MiloScene newMiloScene = new MiloScene(sceneName, gl);
            Debug.Log($"GLBImporter: Created MiloScene: '{sceneName}'. Processing...");

            if (assimpSceneData.HasMeshes)
            {
                Debug.Log($"GLBImporter: Processing {assimpSceneData.MeshCount} meshes.");
                for (int meshIdx = 0; meshIdx < assimpSceneData.MeshCount; meshIdx++)
                {
                    Assimp.Mesh assimpMesh = assimpSceneData.Meshes[meshIdx];
                    if (assimpMesh.PrimitiveType != Assimp.PrimitiveType.Triangle)
                    {
                        Debug.LogWarning($"GLBImporter: Skipping mesh '{assimpMesh.Name}' as it's not made of triangles (Type: {assimpMesh.PrimitiveType}).");
                        continue;
                    }

                    string assimpMeshName = string.IsNullOrEmpty(assimpMesh.Name) ? $"UnnamedMesh_{sceneName}_{meshIdx}" : assimpMesh.Name;
                    MiloMaterial miloMaterial = new MiloMaterial();
                    if (assimpSceneData.HasMaterials && assimpMesh.MaterialIndex >= 0 && assimpMesh.MaterialIndex < assimpSceneData.MaterialCount)
                    {
                        Assimp.Material assimpMaterial = assimpSceneData.Materials[assimpMesh.MaterialIndex];
                        if (assimpMaterial.GetMaterialTexture(TextureType.Diffuse, 0, out TextureSlot textureSlot))
                        {
                            MiloTexture2D loadedTexture = ProcessAssimpTextureSlot(gl, textureSlot, assimpSceneData, filePath);
                            miloMaterial.AlbedoTexture = (loadedTexture != null && loadedTexture.IsUploaded) ? loadedTexture : GetOrCreateDefaultPinkTexture(gl);
                        }
                        else { miloMaterial.AlbedoTexture = GetOrCreateDefaultPinkTexture(gl); }

                        if (assimpMaterial.HasColorDiffuse)
                        {
                            miloMaterial.BaseColorTint = new System.Numerics.Vector4(assimpMaterial.ColorDiffuse.R, assimpMaterial.ColorDiffuse.G, assimpMaterial.ColorDiffuse.B, assimpMaterial.ColorDiffuse.A);
                        }
                    }
                    else { miloMaterial.AlbedoTexture = GetOrCreateDefaultPinkTexture(gl); }

                    List<float> packedVertexDataList = new List<float>();
                    bool hasNormals = assimpMesh.HasNormals; bool hasTexCoords = assimpMesh.HasTextureCoords(0); bool hasVertexColors = assimpMesh.HasVertexColors(0);
                    for (int i = 0; i < assimpMesh.VertexCount; i++)
                    {
                        packedVertexDataList.Add(assimpMesh.Vertices[i].X); packedVertexDataList.Add(assimpMesh.Vertices[i].Y); packedVertexDataList.Add(assimpMesh.Vertices[i].Z);
                        if (hasNormals) { packedVertexDataList.Add(assimpMesh.Normals[i].X); packedVertexDataList.Add(assimpMesh.Normals[i].Y); packedVertexDataList.Add(assimpMesh.Normals[i].Z); } else { packedVertexDataList.Add(0.0f); packedVertexDataList.Add(1.0f); packedVertexDataList.Add(0.0f); }
                        if (hasVertexColors) { Assimp.Color4D color = assimpMesh.VertexColorChannels[0][i]; packedVertexDataList.Add(color.R); packedVertexDataList.Add(color.G); packedVertexDataList.Add(color.B); packedVertexDataList.Add(color.A); } else { packedVertexDataList.Add(1.0f); packedVertexDataList.Add(1.0f); packedVertexDataList.Add(1.0f); packedVertexDataList.Add(1.0f); }
                        if (hasTexCoords) { AssimpNETVector3D tc = assimpMesh.TextureCoordinateChannels[0][i]; packedVertexDataList.Add(tc.X); packedVertexDataList.Add(tc.Y); } else { packedVertexDataList.Add(0.0f); packedVertexDataList.Add(0.0f); }
                    }
                    List<uint> indexList = new List<uint>();
                    foreach (Assimp.Face f in assimpMesh.Faces) { if (f.IndexCount == 3) { indexList.Add((uint)f.Indices[0]); indexList.Add((uint)f.Indices[1]); indexList.Add((uint)f.Indices[2]); } }

                    if (packedVertexDataList.Count > 0 && indexList.Count > 0)
                    {
                        MiloMesh newMiloMesh = new MiloMesh(gl, packedVertexDataList.ToArray(), indexList.ToArray(), miloMaterial);
                        newMiloScene.AddModel(newMiloMesh);
                        ModelDatabase.AddModel(newMiloMesh, assimpMeshName);
                    }
                    else { Debug.LogWarning($"GLBImporter: Mesh '{assimpMeshName}' had no vertex or index data after processing. Skipped."); }
                }
            }
            else { Debug.Log("GLBImporter: No meshes found in the scene."); }

            if (assimpSceneData.HasCameras && assimpSceneData.CameraCount > 0)
            {
                Debug.Log($"GLBImporter: Processing {assimpSceneData.CameraCount} cameras.");
                for (int camIdx = 0; camIdx < assimpSceneData.CameraCount; camIdx++)
                {
                    AssimpNETCamera currentAssimpCamera = assimpSceneData.Cameras[camIdx];
                    Node cameraNode = FindNodeForCameraByGlTFExtension(assimpSceneData.RootNode, camIdx) ?? FindNodeByName(assimpSceneData.RootNode, currentAssimpCamera.Name);

                    if (cameraNode != null)
                    {
                        Silk.NET.Maths.Matrix4X4<float> worldTransformMatrixSilk = CalculateWorldTransform(cameraNode);
                        MiloEngineCamera miloCam = new MiloEngineCamera
                        {
                            FieldOfViewDegrees = Scalar.RadiansToDegrees(currentAssimpCamera.FieldOfview),
                            NearClipPlane = currentAssimpCamera.ClipPlaneNear,
                            FarClipPlane = currentAssimpCamera.ClipPlaneFar,
                            AspectRatio = currentAssimpCamera.AspectRatio > 0 ? currentAssimpCamera.AspectRatio : (320f / 240f)
                        };

                        System.Numerics.Matrix4x4 cameraNodeWorldTransformSystem = worldTransformMatrixSilk.ToSystem();
                        miloCam.Transform.LocalPosition = new Silk.NET.Maths.Vector3D<float>(cameraNodeWorldTransformSystem.Translation.X, cameraNodeWorldTransformSystem.Translation.Y, cameraNodeWorldTransformSystem.Translation.Z);
                        miloCam.Transform.LocalRotation = Silk.NET.Maths.Quaternion<float>.CreateFromRotationMatrix(worldTransformMatrixSilk);

                        Debug.Log($"GLBImporter: Created MiloCamera '{currentAssimpCamera.Name}' from node '{cameraNode.Name}'. Pos: {miloCam.Transform.LocalPosition}");
                        newMiloScene.AddCamera(miloCam, currentAssimpCamera.Name);

                        if (newMiloScene.ActiveCamera == null || (newMiloScene.ActiveCamera.Transform.LocalPosition == new Silk.NET.Maths.Vector3D<float>(0, 1, 5) && newMiloScene.ActiveCamera.FieldOfViewDegrees == 60f))
                        {
                            newMiloScene.ActiveCamera = miloCam;
                            Debug.Log($"GLBImporter: Set '{currentAssimpCamera.Name}' as ActiveCamera for scene '{newMiloScene.Name}'.");
                        }
                    }
                    else { Debug.LogWarning($"GLBImporter: Could not find a node for Assimp camera '{currentAssimpCamera.Name}'. Skipping."); }
                }
            }
            else { Debug.Log("GLBImporter: No cameras found in Assimp scene data."); }

            if (assimpSceneData.HasLights && assimpSceneData.LightCount > 0)
            {
                Debug.Log($"GLBImporter: Processing {assimpSceneData.LightCount} lights.");
                for (int lightIdx = 0; lightIdx < assimpSceneData.LightCount; lightIdx++)
                {
                    AssimpNETLight assimpLight = assimpSceneData.Lights[lightIdx];
                    Debug.Log($"GLBImporter: Found Assimp light '{assimpLight.Name}' (Type: {assimpLight.LightType}).");
                    Node lightNode = FindNodeByName(assimpSceneData.RootNode, assimpLight.Name);
                    if (lightNode == null)
                    {
                        Debug.LogWarning($"GLBImporter: Could not find a transform node for light '{assimpLight.Name}'. Skipping this light.");
                        continue;
                    }

                    Silk.NET.Maths.Matrix4X4<float> lightWorldTransformSilk = CalculateWorldTransform(lightNode);
                    MiloLight newMiloLight = null;

                    if (assimpLight.LightType == LightSourceType.Directional)
                    {
                        MiloDirectionalLight miloDirLight = new MiloDirectionalLight();
                        // Clamp color components to 0-1 range for safety before assigning
                        miloDirLight.Color = new System.Numerics.Vector4(
                            (float)Math.Min(assimpLight.ColorDiffuse.R, 1.0f),
                            (float)Math.Min(assimpLight.ColorDiffuse.G, 1.0f),
                            (float)Math.Min(assimpLight.ColorDiffuse.B, 1.0f),
                            1.0f);
                        miloDirLight.Intensity = 1.0f; // Force intensity for testing
                        newMiloLight = miloDirLight;
                        Debug.Log($"GLBImporter: Created MiloDirectionalLight '{assimpLight.Name}'. Forced Color: {miloDirLight.Color}, Forced Intensity: {miloDirLight.Intensity}");
                    }
                    else if (assimpLight.LightType == LightSourceType.Spot)
                    {
                        MiloSpotLight miloSpotLight = new MiloSpotLight();
                        miloSpotLight.Color = new System.Numerics.Vector4(
                            (float)Math.Min(assimpLight.ColorDiffuse.R, 1.0f),
                            (float)Math.Min(assimpLight.ColorDiffuse.G, 1.0f),
                            (float)Math.Min(assimpLight.ColorDiffuse.B, 1.0f),
                            1.0f);
                        miloSpotLight.Intensity = 1.0f; // Force intensity for testing

                        miloSpotLight.SetCutOffAngles(
                            Scalar.RadiansToDegrees(assimpLight.AngleInnerCone),
                            Scalar.RadiansToDegrees(assimpLight.AngleOuterCone)
                        );

                        float spotRange = 20.0f;
                        Metadata.Entry rangeMetaEntry;
                        if (lightNode.Metadata != null && lightNode.Metadata.TryGetValue("range", out rangeMetaEntry))
                        {
                            try
                            {
                                if (rangeMetaEntry.Data is float fVal) spotRange = fVal;
                                else if (rangeMetaEntry.Data is double dVal) spotRange = (float)dVal;
                                else if (rangeMetaEntry.Data is int iVal) spotRange = (float)iVal;
                                else if (rangeMetaEntry.Data is long lVal) spotRange = (float)lVal;
                                else if (rangeMetaEntry.Data is string sVal && float.TryParse(sVal, NumberStyles.Any, CultureInfo.InvariantCulture, out float pVal)) spotRange = pVal;
                                else Debug.LogWarning($"GLBImporter: Light '{assimpLight.Name}' 'range' metadata type '{rangeMetaEntry.DataType}' / actual type '{rangeMetaEntry.Data?.GetType()}' not directly handled as float. Using default.");

                                Debug.Log($"GLBImporter: Light '{assimpLight.Name}' attempt to read 'range' metadata resulted in: {spotRange}");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"GLBImporter: Could not convert 'range' metadata for light '{assimpLight.Name}'. Error: {ex.Message}. Using default.");
                                spotRange = 20.0f; // Fallback to default if conversion fails
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"GLBImporter: Light '{assimpLight.Name}' no 'range' metadata found. Using default range: {spotRange}.");
                        }

                        if (spotRange <= 0 || float.IsNaN(spotRange) || float.IsInfinity(spotRange))
                        {
                            Debug.LogWarning($"GLBImporter: Light '{assimpLight.Name}' had invalid range value ({spotRange}). Resetting to default 20.0f.");
                            spotRange = 20.0f;
                        }
                        miloSpotLight.Range = spotRange; // This is the line that was around 270

                        newMiloLight = miloSpotLight;
                        Debug.Log($"GLBImporter: Created MiloSpotLight '{assimpLight.Name}'. Forced Color: {miloSpotLight.Color}, Forced Intensity: {miloSpotLight.Intensity}, Range: {miloSpotLight.Range}, InnerDeg: {Scalar.RadiansToDegrees(assimpLight.AngleInnerCone)}, OuterDeg: {Scalar.RadiansToDegrees(assimpLight.AngleOuterCone)}");
                    }

                    if (newMiloLight != null)
                    {
                        System.Numerics.Matrix4x4 lightNodeWorldTransformSystem = lightWorldTransformSilk.ToSystem();
                        newMiloLight.Transform.LocalPosition = new Silk.NET.Maths.Vector3D<float>(
                            lightNodeWorldTransformSystem.Translation.X,
                            lightNodeWorldTransformSystem.Translation.Y,
                            lightNodeWorldTransformSystem.Translation.Z
                        );
                        newMiloLight.Transform.LocalRotation = Silk.NET.Maths.Quaternion<float>.CreateFromRotationMatrix(lightWorldTransformSilk);
                        newMiloScene.AddLight(newMiloLight);
                        Debug.Log($"GLBImporter: Added light '{assimpLight.Name}' to scene. Transform Pos: {newMiloLight.Transform.LocalPosition}");
                    }
                }
            }
            else { Debug.Log("GLBImporter: No lights found in Assimp scene data."); }

            if (newMiloScene.ActiveCamera == null)
            {
                Debug.LogWarning($"GLBImporter: Scene '{newMiloScene.Name}' will use its default constructor camera as no camera was loaded from GLB or none was suitable to override the default.");
            }

            importer.Dispose();
            Debug.Log($"GLBImporter: Finished GLB. Scene '{newMiloScene.Name}' - Models: {newMiloScene.Models.Count}, Cameras: {newMiloScene.Cameras.Count}, Lights: {newMiloScene.Lights.Count}. ActiveCam Set: {newMiloScene.ActiveCamera != null}.");
            return newMiloScene;
        }

        private static MiloTexture2D ProcessAssimpTextureSlot(GL gl, TextureSlot textureSlot, AssimpNETScene assimpSceneData, string glbFilePath)
        {
            MiloTexture2D loadedTexture = null;
            if (textureSlot.FilePath.StartsWith("*"))
            {
                string indexString = textureSlot.FilePath.Substring(1);
                if (int.TryParse(indexString, out int embeddedTextureIndex))
                {
                    if (assimpSceneData.HasTextures && embeddedTextureIndex >= 0 && embeddedTextureIndex < assimpSceneData.TextureCount)
                    {
                        Assimp.EmbeddedTexture embeddedTex = assimpSceneData.Textures[embeddedTextureIndex];
                        byte[] pixelData = null; int texWidth = 0; int texHeight = 0;
                        string dbgFilename = embeddedTex.Filename ?? $"embedded_{embeddedTextureIndex}";

                        if (embeddedTex.IsCompressed)
                        {
                            if (embeddedTex.CompressedData != null && embeddedTex.CompressedData.Length > 0)
                            {
                                try
                                {
                                    ImageResult imgRes = ImageResult.FromStream(new MemoryStream(embeddedTex.CompressedData), ColorComponents.RedGreenBlueAlpha);
                                    pixelData = imgRes.Data; texWidth = imgRes.Width; texHeight = imgRes.Height;
                                }
                                catch (Exception e) { Debug.LogError($"GLBImporter.ProcessTexSlot: Decode compressed failed for '{dbgFilename}': {e.Message}"); }
                            }
                            else { Debug.LogWarning($"GLBImporter: Embedded texture '{dbgFilename}' is compressed but has no data."); }
                        }
                        else if (embeddedTex.NonCompressedData != null && embeddedTex.NonCompressedData.Length > 0 && embeddedTex.Width > 0 && embeddedTex.Height > 0)
                        {
                            texWidth = embeddedTex.Width; texHeight = embeddedTex.Height;
                            pixelData = new byte[texWidth * texHeight * 4];
                            for (int i = 0; i < texWidth * texHeight; i++)
                            {
                                Texel texel = embeddedTex.NonCompressedData[i];
                                pixelData[i * 4 + 0] = texel.R;
                                pixelData[i * 4 + 1] = texel.G;
                                pixelData[i * 4 + 2] = texel.B;
                                pixelData[i * 4 + 3] = texel.A;
                            }
                        }
                        else { Debug.LogWarning($"GLBImporter: Embedded texture '{dbgFilename}' no usable non-compressed pixel data."); }

                        if (pixelData != null && texWidth > 0 && texHeight > 0)
                        {
                            loadedTexture = new MiloTexture2D(gl, $"Embedded_{Path.GetFileNameWithoutExtension(dbgFilename)}_{embeddedTextureIndex}");
                            loadedTexture.SetPixelData(texWidth, texHeight, pixelData);
                            loadedTexture.UploadToGPU();
                        }
                    }
                    else { Debug.LogWarning($"GLBImporter: Embedded texture index {embeddedTextureIndex} out of range or no textures in scene."); }
                }
                else { Debug.LogWarning($"GLBImporter.ProcessTexSlot: Could not parse embedded texture index from '{textureSlot.FilePath}'."); }
            }
            else if (!string.IsNullOrEmpty(textureSlot.FilePath))
            {
                string currentDirectory = Path.GetDirectoryName(glbFilePath);
                string textureFilename = Path.GetFileName(textureSlot.FilePath);
                string potentialExternalPath = Path.Combine(currentDirectory, textureFilename);
                potentialExternalPath = Path.GetFullPath(potentialExternalPath);

                if (File.Exists(potentialExternalPath))
                {
                    try
                    {
                        ImageResult imageResult = ImageResult.FromStream(File.OpenRead(potentialExternalPath), ColorComponents.RedGreenBlueAlpha);
                        if (imageResult.Data != null && imageResult.Width > 0 && imageResult.Height > 0)
                        {
                            loadedTexture = new MiloTexture2D(gl, Path.GetFileName(potentialExternalPath));
                            loadedTexture.SetPixelData(imageResult.Width, imageResult.Height, imageResult.Data);
                            loadedTexture.UploadToGPU();
                        }
                    }
                    catch (Exception ex) { Debug.LogError($"GLBImporter.ProcessTexSlot: Failed to load external texture '{potentialExternalPath}'. Ex: {ex.Message}"); }
                }
                else { Debug.LogWarning($"GLBImporter.ProcessTexSlot: External texture file not found: '{potentialExternalPath}'. Original in GLB: '{textureSlot.FilePath}'"); }
            }
            return loadedTexture;
        }

        private static Node FindNodeByName(Node currentNode, string nameToFind)
        {
            if (currentNode == null || string.IsNullOrEmpty(nameToFind)) return null;
            if (currentNode.Name == nameToFind) return currentNode;
            foreach (Node childNode in currentNode.Children)
            {
                Node found = FindNodeByName(childNode, nameToFind);
                if (found != null) return found;
            }
            return null;
        }

        private static Node FindNodeForCameraByGlTFExtension(Node currentNode, int cameraIndexToFind)
        {
            if (currentNode == null) return null;

            Metadata.Entry camMetaEntry;
            if (currentNode.Metadata != null && currentNode.Metadata.TryGetValue("camera", out camMetaEntry))
            {
                try
                {
                    long indexValue = -1;
                    object data = camMetaEntry.Data;

                    if (data is int iVal) indexValue = iVal;
                    else if (data is uint uiVal) indexValue = uiVal;
                    else if (data is long lVal) indexValue = lVal;
                    else if (data is ulong ulVal)
                    {
                        if (ulVal <= int.MaxValue) indexValue = (long)ulVal;
                    }
                    else if (data is short shVal) indexValue = shVal;
                    else if (data is ushort ushVal) indexValue = ushVal;
                    else if (data is byte bVal) indexValue = bVal;
                    else if (data is sbyte sbVal) indexValue = sbVal;
                    else if (data is string sVal && long.TryParse(sVal, NumberStyles.Any, CultureInfo.InvariantCulture, out long pVal)) indexValue = pVal;

                    if (indexValue != -1 && indexValue == cameraIndexToFind)
                    {
                        return currentNode;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"GLBImporter: Could not convert 'camera' metadata value '{camMetaEntry.Data}' of type '{camMetaEntry.DataType}' to integer. Error: {ex.Message}");
                }
            }

            foreach (Node childNode in currentNode.Children)
            {
                Node found = FindNodeForCameraByGlTFExtension(childNode, cameraIndexToFind);
                if (found != null) return found;
            }
            return null;
        }
    }

    public static class MatrixConversionExtensions
    {
        public static System.Numerics.Matrix4x4 ToSystem(this Silk.NET.Maths.Matrix4X4<float> silkMatrix)
        {
            return new System.Numerics.Matrix4x4(
                silkMatrix.M11, silkMatrix.M12, silkMatrix.M13, silkMatrix.M14,
                silkMatrix.M21, silkMatrix.M22, silkMatrix.M23, silkMatrix.M24,
                silkMatrix.M31, silkMatrix.M32, silkMatrix.M33, silkMatrix.M34,
                silkMatrix.M41, silkMatrix.M42, silkMatrix.M43, silkMatrix.M44
            );
        }
    }
}