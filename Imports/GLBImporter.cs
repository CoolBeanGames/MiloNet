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
using MiloNet;
using System.Numerics;

// Using aliases
using MiloMesh = MiloRender.DataTypes.Mesh;
using MiloMaterial = MiloRender.DataTypes.Material;
using MiloTexture2D = MiloRender.DataTypes.Texture2D;
using MiloScene = MiloRender.DataTypes.Scene;
using MiloEngineCamera = MiloRender.DataTypes.Camera;
using AssimpNETScene = Assimp.Scene;
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

        private static Silk.NET.Maths.Matrix4X4<float> CalculateWorldTransform(Node node)
        {
            if (node == null)
                return Silk.NET.Maths.Matrix4X4<float>.Identity;

            AssimpNETMatrix4x4 transform = node.Transform;
            Node parent = node.Parent;
            // Traverse up to the root node to accumulate transforms
            // The RootNode.Transform itself is the transform of the entire scene relative to the world (often identity).
            // Individual node transforms are relative to their parent.
            Stack<AssimpNETMatrix4x4> transformStack = new Stack<AssimpNETMatrix4x4>();
            transformStack.Push(node.Transform);
            Node currentParent = node.Parent;
            while (currentParent != null && currentParent.Parent != null) // Go up to the direct child of the root.
            {
                transformStack.Push(currentParent.Transform);
                currentParent = currentParent.Parent;
            }
            // If currentParent is now the root (or null if node was root), apply root transform if it exists
            if (currentParent != null) // This should be the root node
            {
                transformStack.Push(currentParent.Transform);
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
            if (string.IsNullOrEmpty(filePath)) { Debug.LogError("..."); return null; }
            if (!File.Exists(filePath)) { Debug.LogError("..."); return null; }
            if (gl == null) { Debug.LogError("..."); return null; }

            Debug.Log($"GLBImporter.LoadGlbAsScene: Attempting to load GLB: {filePath}");
            AssimpContext importer = new AssimpContext();
            const PostProcessSteps steps = PostProcessSteps.Triangulate | PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.FlipUVs | PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.CalculateTangentSpace;
            AssimpNETScene assimpSceneData;
            try { assimpSceneData = importer.ImportFile(filePath, steps); }
            catch (Exception ex) { Debug.LogError($"GLBImporter.LoadGlbAsScene: Assimp import failed. Exception: {ex.Message} {ex.StackTrace}"); importer.Dispose(); return null; }

            if (assimpSceneData == null || !assimpSceneData.HasMeshes || assimpSceneData.MeshCount == 0) { Debug.LogError("..."); importer.Dispose(); return null; }

            string sceneName = Path.GetFileNameWithoutExtension(filePath);
            MiloScene newMiloScene = new MiloScene(sceneName, gl);
            Debug.Log($"GLBImporter: Created MiloScene: '{sceneName}'. Processing {assimpSceneData.MeshCount} meshes.");

            for (int meshIdx = 0; meshIdx < assimpSceneData.MeshCount; meshIdx++)
            {
                Assimp.Mesh assimpMesh = assimpSceneData.Meshes[meshIdx];
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
                    if (assimpMaterial.HasColorDiffuse) { miloMaterial.BaseColorTint = new System.Numerics.Vector4(assimpMaterial.ColorDiffuse.R, assimpMaterial.ColorDiffuse.G, assimpMaterial.ColorDiffuse.B, assimpMaterial.ColorDiffuse.A); }
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
                List<uint> indexList = new List<uint>(); foreach (Assimp.Face f in assimpMesh.Faces) { if (f.IndexCount == 3) { indexList.Add((uint)f.Indices[0]); indexList.Add((uint)f.Indices[1]); indexList.Add((uint)f.Indices[2]); } }

                if (packedVertexDataList.Count > 0 && indexList.Count > 0)
                {
                    MiloMesh newMiloMesh = new MiloMesh(gl, packedVertexDataList.ToArray(), indexList.ToArray(), miloMaterial);
                    newMiloScene.AddModel(newMiloMesh);
                    ModelDatabase.AddModel(newMiloMesh, assimpMeshName);
                }
            }

            if (assimpSceneData.HasCameras && assimpSceneData.CameraCount > 0)
            {
                for (int camIdx = 0; camIdx < assimpSceneData.CameraCount; camIdx++)
                {
                    AssimpNETCamera currentAssimpCamera = assimpSceneData.Cameras[camIdx];
                    Debug.Log($"GLBImporter: Found Assimp camera '{currentAssimpCamera.Name}' (Index: {camIdx}).");

                    Node cameraNode = FindNodeForCameraByGlTFExtension(assimpSceneData.RootNode, camIdx);
                    if (cameraNode == null)
                    {
                        cameraNode = FindNodeForCameraByName(assimpSceneData.RootNode, currentAssimpCamera.Name);
                    }

                    if (cameraNode != null)
                    {
                        Debug.Log($"GLBImporter: Camera '{currentAssimpCamera.Name}' is associated with node '{cameraNode.Name}'.");
                        Silk.NET.Maths.Matrix4X4<float> worldTransformMatrixSilk = CalculateWorldTransform(cameraNode);

                        MiloEngineCamera miloCam = new MiloEngineCamera();

                        miloCam.FieldOfViewDegrees = Scalar.RadiansToDegrees(currentAssimpCamera.FieldOfview); // Corrected: FieldOfview
                        miloCam.NearClipPlane = currentAssimpCamera.ClipPlaneNear;
                        miloCam.FarClipPlane = currentAssimpCamera.ClipPlaneFar;
                        miloCam.AspectRatio = currentAssimpCamera.AspectRatio;

                        System.Numerics.Matrix4x4 worldTransformSystem = worldTransformMatrixSilk.ToSystem();
                        System.Numerics.Vector3 positionSystem = worldTransformSystem.Translation;
                        Silk.NET.Maths.Vector3D<float> positionSilk = new Silk.NET.Maths.Vector3D<float>(positionSystem.X, positionSystem.Y, positionSystem.Z);
                        Silk.NET.Maths.Quaternion<float> rotationSilk = Silk.NET.Maths.Quaternion<float>.CreateFromRotationMatrix(worldTransformMatrixSilk);

                        miloCam.Transform.LocalPosition = positionSilk;
                        miloCam.Transform.LocalRotation = rotationSilk;

                        Debug.Log($"GLBImporter: Created MiloCamera '{currentAssimpCamera.Name}' from GLB. Pos: {positionSilk}, Rot Quat: {rotationSilk}");

                        if (newMiloScene.ActiveCamera == null || camIdx == 0 || newMiloScene.ActiveCamera.Transform.LocalPosition == new Silk.NET.Maths.Vector3D<float>(0, 1, 5)) // Prioritize GLB camera over scene's default
                        {
                            newMiloScene.ActiveCamera = miloCam;
                            Debug.Log($"GLBImporter: Set '{currentAssimpCamera.Name}' as ActiveCamera for scene '{newMiloScene.Name}'.");
                        }
                    }
                    else { Debug.LogWarning($"GLBImporter: Could not find a node for Assimp camera '{currentAssimpCamera.Name}'. Skipping."); }
                }
            }

            if (newMiloScene.ActiveCamera == null)
            { // This case should be rare if scene constructor default works
                Debug.LogWarning($"GLBImporter: No cameras processed from GLB and scene has no default. Scene '{newMiloScene.Name}' will have a null ActiveCamera.");
            }
            else if (newMiloScene.ActiveCamera.Transform.LocalPosition == new Silk.NET.Maths.Vector3D<float>(0, 1, 5) && !(assimpSceneData.HasCameras && assimpSceneData.CameraCount > 0))
            {
                Debug.Log($"GLBImporter: No cameras found in GLB. Scene '{newMiloScene.Name}' is using its own default camera.");
            }


            importer.Dispose();
            Debug.Log($"GLBImporter: Finished GLB. Scene '{newMiloScene.Name}' Models: {newMiloScene.Models.Count}, ActiveCam: {(newMiloScene.ActiveCamera != null ? newMiloScene.ActiveCamera.Transform.GetHashCode().ToString() : "None")}.");
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
                    if (assimpSceneData.Textures == null) { Debug.LogError("..."); return null; }
                    if (embeddedTextureIndex < 0 || embeddedTextureIndex >= assimpSceneData.TextureCount) { Debug.LogWarning("..."); return null; }
                    Assimp.EmbeddedTexture embeddedTex = assimpSceneData.Textures[embeddedTextureIndex];
                    if (embeddedTex == null) { Debug.LogError("..."); return null; }

                    string dbgFilename = "N/A"; bool dbgIsCompressed = false; bool propertyAccessError = false;
                    byte[] pixelData = null; int texWidth = 0; int texHeight = 0;
                    try
                    {
                        dbgFilename = embeddedTex.Filename ?? "null_filename_val";
                        dbgIsCompressed = embeddedTex.IsCompressed;
                    }
                    catch (Exception ex) { Debug.LogError($"GLBImporter.ProcessTexSlot: Initial Property access error for *{embeddedTextureIndex} ('{dbgFilename}'): {ex.Message}"); propertyAccessError = true; }

                    if (!propertyAccessError)
                    {
                        if (dbgIsCompressed)
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
                            else { Debug.LogError($"GLBImporter.ProcessTexSlot: '{dbgFilename}' compressed but no data."); }
                        }
                        else if (embeddedTex.NonCompressedData != null && embeddedTex.NonCompressedData.Length > 0 && embeddedTex.Width > 0 && embeddedTex.Height > 0)
                        {
                            texWidth = embeddedTex.Width; texHeight = embeddedTex.Height;
                            pixelData = new byte[texWidth * texHeight * 4];
                            for (int i = 0; i < embeddedTex.NonCompressedData.Length; i++)
                            {
                                Texel t = embeddedTex.NonCompressedData[i];
                                pixelData[i * 4 + 0] = t.R; pixelData[i * 4 + 1] = t.G; pixelData[i * 4 + 2] = t.B; pixelData[i * 4 + 3] = t.A;
                            }
                        }
                        else { Debug.LogWarning($"GLBImporter.ProcessTexSlot: '{dbgFilename}' no usable pixel data."); }
                    }

                    if (pixelData != null && texWidth > 0 && texHeight > 0)
                    {
                        loadedTexture = new MiloTexture2D(gl, $"EmbeddedTexture_*{embeddedTextureIndex}_{dbgFilename}");
                        loadedTexture.SetPixelData(texWidth, texHeight, pixelData);
                        loadedTexture.UploadToGPU();
                    }
                }
                else { Debug.LogWarning($"GLBImporter.ProcessTexSlot: Could not parse embedded texture index from '{textureSlot.FilePath}'."); }
            }
            else if (!string.IsNullOrEmpty(textureSlot.FilePath))
            {
                string potentialExternalPath = Path.Combine(Path.GetDirectoryName(glbFilePath), textureSlot.FilePath);
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
                else { Debug.LogWarning($"GLBImporter.ProcessTexSlot: External texture file not found: '{potentialExternalPath}'."); }
            }
            return loadedTexture;
        }

        private static Node FindNodeForCameraByGlTFExtension(Node currentNode, int cameraIndexToFind)
        {
            if (currentNode == null) return null;

            if (currentNode.Metadata != null && currentNode.Metadata.Count > 0)
            {
                if (currentNode.Metadata.TryGetValue("camera", out Metadata.Entry entry))
                {
                    // Corrected: Use MetaDataType.Int32 or MetaDataType.UnsignedInt for integer indices
                    if (entry.DataType == MetaDataType.Int32)
                    {
                        if (entry.DataAs<int>() == cameraIndexToFind)
                        {
                            Debug.Log($"FindNodeForCameraByGlTFExtension: Found node '{currentNode.Name}' with metadata 'camera' (Int32) pointing to index {cameraIndexToFind}.");
                            return currentNode;
                        }
                    }
                    else if (entry.DataType == MetaDataType.UInt64) // glTF indices are often unsigned
                    {
                        if (entry.DataAs<uint>() == cameraIndexToFind) // Assumes cameraIndexToFind can be compared to uint
                        {
                            Debug.Log($"FindNodeForCameraByGlTFExtension: Found node '{currentNode.Name}' with metadata 'camera' (Uint32) pointing to index {cameraIndexToFind}.");
                            return currentNode;
                        }
                    }
                    // Add other integer types if necessary, e.g., Int64, Uint64, though less common for indices.
                }
            }

            foreach (Node childNode in currentNode.Children)
            {
                Node found = FindNodeForCameraByGlTFExtension(childNode, cameraIndexToFind);
                if (found != null) return found;
            }
            return null;
        }

        private static Node FindNodeForCameraByName(Node currentNode, string cameraNameToFind)
        {
            if (currentNode == null || string.IsNullOrEmpty(cameraNameToFind)) return null;

            if (currentNode.Name == cameraNameToFind)
            {
                Debug.Log($"GLBImporter.FindNodeForCameraByName: Found node '{currentNode.Name}' matching camera name '{cameraNameToFind}'.");
                return currentNode;
            }

            foreach (Node childNode in currentNode.Children)
            {
                Node found = FindNodeForCameraByName(childNode, cameraNameToFind);
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