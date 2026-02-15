using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpGLTF.Schema2;
using SharpGLTF.Animations;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Converts GLB/GLTF files into Unity AssetBundles at deploy time.
/// Handles meshes, textures, materials, skeletons, and animations.
/// </summary>
public class GlbBundler
{
    /// <summary>
    /// Result of converting a GLB to bundle.
    /// </summary>
    public class GlbConvertResult
    {
        public bool Success { get; set; }
        public string? OutputPath { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
        public List<string> ConvertedAssets { get; set; } = new();
    }

    /// <summary>
    /// Extracted mesh data ready for Unity conversion.
    /// </summary>
    public class ExtractedMesh
    {
        public string Name { get; set; } = string.Empty;
        public float[] Vertices { get; set; } = Array.Empty<float>();
        public float[] Normals { get; set; } = Array.Empty<float>();
        public float[] Tangents { get; set; } = Array.Empty<float>();
        public float[] UV0 { get; set; } = Array.Empty<float>();
        public float[] UV1 { get; set; } = Array.Empty<float>();
        public float[] Colors { get; set; } = Array.Empty<float>();
        public int[] Indices { get; set; } = Array.Empty<int>();
        public List<SubMeshInfo> SubMeshes { get; set; } = new();

        // Skinning data
        public float[] BoneWeights { get; set; } = Array.Empty<float>();
        public int[] BoneIndices { get; set; } = Array.Empty<int>();
        public Matrix4x4[] BindPoses { get; set; } = Array.Empty<Matrix4x4>();
        public bool HasSkinning => BoneWeights.Length > 0;
    }

    public class SubMeshInfo
    {
        public int IndexStart { get; set; }
        public int IndexCount { get; set; }
        public int MaterialIndex { get; set; }
    }

    /// <summary>
    /// Extracted material data.
    /// </summary>
    public class ExtractedMaterial
    {
        public string Name { get; set; } = string.Empty;
        public float[] BaseColor { get; set; } = { 1, 1, 1, 1 };
        public float Metallic { get; set; }
        public float Roughness { get; set; } = 1;
        public float[] EmissiveFactor { get; set; } = { 0, 0, 0 };
        public string? BaseColorTextureName { get; set; }
        public string? NormalTextureName { get; set; }
        public string? MetallicRoughnessTextureName { get; set; }
        public string? EmissiveTextureName { get; set; }
        public byte[]? BaseColorTextureData { get; set; }
        public byte[]? NormalTextureData { get; set; }
        public byte[]? MetallicRoughnessTextureData { get; set; }
        public byte[]? EmissiveTextureData { get; set; }
    }

    /// <summary>
    /// Extracted skeleton/armature data.
    /// </summary>
    public class ExtractedSkeleton
    {
        public string Name { get; set; } = string.Empty;
        public List<ExtractedBone> Bones { get; set; } = new();
    }

    public class ExtractedBone
    {
        public string Name { get; set; } = string.Empty;
        public int ParentIndex { get; set; } = -1;
        public Vector3 LocalPosition { get; set; }
        public Quaternion LocalRotation { get; set; } = Quaternion.Identity;
        public Vector3 LocalScale { get; set; } = Vector3.One;
    }

    /// <summary>
    /// Extracted animation data.
    /// </summary>
    public class ExtractedAnimation
    {
        public string Name { get; set; } = string.Empty;
        public float Duration { get; set; }
        public List<ExtractedAnimationTrack> Tracks { get; set; } = new();
    }

    public class ExtractedAnimationTrack
    {
        public string BoneName { get; set; } = string.Empty;
        public List<(float Time, Vector3 Value)> PositionKeys { get; set; } = new();
        public List<(float Time, Quaternion Value)> RotationKeys { get; set; } = new();
        public List<(float Time, Vector3 Value)> ScaleKeys { get; set; } = new();
    }

    /// <summary>
    /// Parse a GLB file and extract all asset data.
    /// </summary>
    public static GlbParseResult ParseGlb(string glbPath)
    {
        var result = new GlbParseResult();

        try
        {
            var model = ModelRoot.Load(glbPath);
            result.SourceName = Path.GetFileNameWithoutExtension(glbPath);

            // Extract meshes
            foreach (var mesh in model.LogicalMeshes)
            {
                var extracted = ExtractMesh(mesh, model);
                if (extracted != null)
                    result.Meshes.Add(extracted);
            }

            // Extract materials and textures
            foreach (var material in model.LogicalMaterials)
            {
                var extracted = ExtractMaterial(material, model);
                result.Materials.Add(extracted);
            }

            // Extract skeleton from skins
            foreach (var skin in model.LogicalSkins)
            {
                var extracted = ExtractSkeleton(skin);
                if (extracted != null)
                    result.Skeletons.Add(extracted);
            }

            // Extract animations
            foreach (var anim in model.LogicalAnimations)
            {
                var extracted = ExtractAnimation(anim, model);
                if (extracted != null)
                    result.Animations.Add(extracted);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private static ExtractedMesh? ExtractMesh(Mesh mesh, ModelRoot model)
    {
        var extracted = new ExtractedMesh
        {
            Name = mesh.Name ?? $"Mesh_{mesh.LogicalIndex}"
        };

        var allVertices = new List<float>();
        var allNormals = new List<float>();
        var allTangents = new List<float>();
        var allUV0 = new List<float>();
        var allUV1 = new List<float>();
        var allColors = new List<float>();
        var allIndices = new List<int>();
        var allBoneWeights = new List<float>();
        var allBoneIndices = new List<int>();

        int vertexOffset = 0;

        foreach (var primitive in mesh.Primitives)
        {
            var positionAccessor = primitive.GetVertexAccessor("POSITION");
            if (positionAccessor == null) continue;

            var positions = positionAccessor.AsVector3Array();
            foreach (var pos in positions)
            {
                // Convert from GLTF coordinate system (Y-up, right-handed) to Unity (Y-up, left-handed)
                allVertices.Add(pos.X);
                allVertices.Add(pos.Y);
                allVertices.Add(-pos.Z); // Flip Z for left-handed
            }

            // Normals
            var normalAccessor = primitive.GetVertexAccessor("NORMAL");
            if (normalAccessor != null)
            {
                foreach (var normal in normalAccessor.AsVector3Array())
                {
                    allNormals.Add(normal.X);
                    allNormals.Add(normal.Y);
                    allNormals.Add(-normal.Z);
                }
            }

            // Tangents
            var tangentAccessor = primitive.GetVertexAccessor("TANGENT");
            if (tangentAccessor != null)
            {
                foreach (var tangent in tangentAccessor.AsVector4Array())
                {
                    allTangents.Add(tangent.X);
                    allTangents.Add(tangent.Y);
                    allTangents.Add(-tangent.Z);
                    allTangents.Add(-tangent.W); // Flip handedness
                }
            }

            // UV0
            var uv0Accessor = primitive.GetVertexAccessor("TEXCOORD_0");
            if (uv0Accessor != null)
            {
                foreach (var uv in uv0Accessor.AsVector2Array())
                {
                    allUV0.Add(uv.X);
                    allUV0.Add(1 - uv.Y); // Flip V for Unity
                }
            }

            // UV1
            var uv1Accessor = primitive.GetVertexAccessor("TEXCOORD_1");
            if (uv1Accessor != null)
            {
                foreach (var uv in uv1Accessor.AsVector2Array())
                {
                    allUV1.Add(uv.X);
                    allUV1.Add(1 - uv.Y);
                }
            }

            // Vertex colors
            var colorAccessor = primitive.GetVertexAccessor("COLOR_0");
            if (colorAccessor != null)
            {
                foreach (var color in colorAccessor.AsVector4Array())
                {
                    allColors.Add(color.X);
                    allColors.Add(color.Y);
                    allColors.Add(color.Z);
                    allColors.Add(color.W);
                }
            }

            // Bone weights and indices (skinning)
            var weightsAccessor = primitive.GetVertexAccessor("WEIGHTS_0");
            var jointsAccessor = primitive.GetVertexAccessor("JOINTS_0");
            if (weightsAccessor != null && jointsAccessor != null)
            {
                var weights = weightsAccessor.AsVector4Array();
                var joints = jointsAccessor.AsVector4Array();

                for (int i = 0; i < weights.Count; i++)
                {
                    allBoneWeights.Add(weights[i].X);
                    allBoneWeights.Add(weights[i].Y);
                    allBoneWeights.Add(weights[i].Z);
                    allBoneWeights.Add(weights[i].W);

                    allBoneIndices.Add((int)joints[i].X);
                    allBoneIndices.Add((int)joints[i].Y);
                    allBoneIndices.Add((int)joints[i].Z);
                    allBoneIndices.Add((int)joints[i].W);
                }
            }

            // Indices
            var indexAccessor = primitive.IndexAccessor;
            int indexStart = allIndices.Count;
            if (indexAccessor != null)
            {
                var indices = indexAccessor.AsIndicesArray();
                // Reverse winding order for left-handed coordinate system
                for (int i = 0; i < indices.Count; i += 3)
                {
                    allIndices.Add((int)indices[i] + vertexOffset);
                    allIndices.Add((int)indices[i + 2] + vertexOffset);
                    allIndices.Add((int)indices[i + 1] + vertexOffset);
                }
            }

            extracted.SubMeshes.Add(new SubMeshInfo
            {
                IndexStart = indexStart,
                IndexCount = allIndices.Count - indexStart,
                MaterialIndex = primitive.Material?.LogicalIndex ?? 0
            });

            vertexOffset += positions.Count;
        }

        extracted.Vertices = allVertices.ToArray();
        extracted.Normals = allNormals.ToArray();
        extracted.Tangents = allTangents.ToArray();
        extracted.UV0 = allUV0.ToArray();
        extracted.UV1 = allUV1.ToArray();
        extracted.Colors = allColors.ToArray();
        extracted.Indices = allIndices.ToArray();
        extracted.BoneWeights = allBoneWeights.ToArray();
        extracted.BoneIndices = allBoneIndices.ToArray();

        return extracted;
    }

    private static ExtractedMaterial ExtractMaterial(Material material, ModelRoot model)
    {
        var extracted = new ExtractedMaterial
        {
            Name = material.Name ?? $"Material_{material.LogicalIndex}"
        };

        // PBR Metallic Roughness
        var pbr = material.FindChannel("BaseColor");
        if (pbr.HasValue)
        {
            var color = pbr.Value.Color;
            extracted.BaseColor = new[] { color.X, color.Y, color.Z, color.W };

            var texture = pbr.Value.Texture;
            if (texture != null)
            {
                extracted.BaseColorTextureName = texture.PrimaryImage?.Name ?? $"BaseColor_{material.LogicalIndex}";
                extracted.BaseColorTextureData = texture.PrimaryImage?.Content.Content.ToArray();
            }
        }

        var metallicChannel = material.FindChannel("MetallicRoughness");
        if (metallicChannel.HasValue)
        {
            // Get metallic and roughness factors
            foreach (var param in metallicChannel.Value.Parameters)
            {
                if (param.Name == "MetallicFactor")
                    extracted.Metallic = (float)param.Value;
                else if (param.Name == "RoughnessFactor")
                    extracted.Roughness = (float)param.Value;
            }

            var texture = metallicChannel.Value.Texture;
            if (texture != null)
            {
                extracted.MetallicRoughnessTextureName = texture.PrimaryImage?.Name ?? $"MetallicRoughness_{material.LogicalIndex}";
                extracted.MetallicRoughnessTextureData = texture.PrimaryImage?.Content.Content.ToArray();
            }
        }

        var normalChannel = material.FindChannel("Normal");
        if (normalChannel.HasValue)
        {
            var texture = normalChannel.Value.Texture;
            if (texture != null)
            {
                extracted.NormalTextureName = texture.PrimaryImage?.Name ?? $"Normal_{material.LogicalIndex}";
                extracted.NormalTextureData = texture.PrimaryImage?.Content.Content.ToArray();
            }
        }

        var emissiveChannel = material.FindChannel("Emissive");
        if (emissiveChannel.HasValue)
        {
            var color = emissiveChannel.Value.Color;
            extracted.EmissiveFactor = new[] { color.X, color.Y, color.Z };

            var texture = emissiveChannel.Value.Texture;
            if (texture != null)
            {
                extracted.EmissiveTextureName = texture.PrimaryImage?.Name ?? $"Emissive_{material.LogicalIndex}";
                extracted.EmissiveTextureData = texture.PrimaryImage?.Content.Content.ToArray();
            }
        }

        return extracted;
    }

    private static ExtractedSkeleton? ExtractSkeleton(Skin skin)
    {
        if (skin.JointsCount == 0)
            return null;

        var skeleton = new ExtractedSkeleton
        {
            Name = skin.Name ?? "Armature"
        };

        // Build bone list
        var jointNodes = new List<Node>();
        for (int i = 0; i < skin.JointsCount; i++)
        {
            var (joint, ibm) = skin.GetJoint(i);
            jointNodes.Add(joint);
        }

        // Map nodes to indices
        var nodeToIndex = new Dictionary<Node, int>();
        for (int i = 0; i < jointNodes.Count; i++)
        {
            nodeToIndex[jointNodes[i]] = i;
        }

        // Extract bone data
        for (int i = 0; i < jointNodes.Count; i++)
        {
            var node = jointNodes[i];
            var bone = new ExtractedBone
            {
                Name = node.Name ?? $"Bone_{i}",
                LocalPosition = node.LocalTransform.Translation,
                LocalRotation = node.LocalTransform.Rotation,
                LocalScale = node.LocalTransform.Scale
            };

            // Find parent
            if (node.VisualParent != null && nodeToIndex.TryGetValue(node.VisualParent, out int parentIdx))
            {
                bone.ParentIndex = parentIdx;
            }

            skeleton.Bones.Add(bone);
        }

        return skeleton;
    }

    private static ExtractedAnimation? ExtractAnimation(Animation animation, ModelRoot model)
    {
        var extracted = new ExtractedAnimation
        {
            Name = animation.Name ?? $"Animation_{animation.LogicalIndex}",
            Duration = animation.Duration
        };

        // Group channels by target node
        var tracksByNode = new Dictionary<string, ExtractedAnimationTrack>();

        foreach (var channel in animation.Channels)
        {
            var targetNode = channel.TargetNode;
            if (targetNode == null) continue;

            var nodeName = targetNode.Name ?? $"Node_{targetNode.LogicalIndex}";

            if (!tracksByNode.TryGetValue(nodeName, out var track))
            {
                track = new ExtractedAnimationTrack { BoneName = nodeName };
                tracksByNode[nodeName] = track;
            }

            // Use SharpGLTF animation sampling API
            var sampler = channel.GetTranslationSampler();
            var rotSampler = channel.GetRotationSampler();
            var scaleSampler = channel.GetScaleSampler();

            // Extract translation keyframes
            if (sampler != null)
            {
                var keys = sampler.GetLinearKeys().ToList();
                foreach (var (time, value) in keys)
                {
                    // Convert coordinate system
                    track.PositionKeys.Add((time, new Vector3(value.X, value.Y, -value.Z)));
                }
            }

            // Extract rotation keyframes
            if (rotSampler != null)
            {
                var keys = rotSampler.GetLinearKeys().ToList();
                foreach (var (time, value) in keys)
                {
                    // Convert coordinate system (flip Z and W for left-handed)
                    track.RotationKeys.Add((time, new Quaternion(value.X, value.Y, -value.Z, -value.W)));
                }
            }

            // Extract scale keyframes
            if (scaleSampler != null)
            {
                var keys = scaleSampler.GetLinearKeys().ToList();
                foreach (var (time, value) in keys)
                {
                    track.ScaleKeys.Add((time, value));
                }
            }
        }

        extracted.Tracks = tracksByNode.Values.ToList();
        return extracted.Tracks.Count > 0 ? extracted : null;
    }

    /// <summary>
    /// Convert a parsed GLB to a Unity AssetBundle.
    /// </summary>
    public static async Task<GlbConvertResult> ConvertToBundleAsync(
        string glbPath,
        string outputPath,
        string unityVersion,
        CancellationToken ct = default)
    {
        return await Task.Run(() => ConvertToBundleCore(glbPath, outputPath, unityVersion), ct);
    }

    /// <summary>
    /// Convert a GLB file to native Unity assets within an existing assets file.
    /// Creates Mesh assets using MeshAssetCreator for proper native asset handling.
    /// </summary>
    /// <param name="glbPath">Path to the GLB file.</param>
    /// <param name="am">The AssetsManager instance.</param>
    /// <param name="afileInst">The assets file instance to add assets to.</param>
    /// <param name="nextPathId">Reference to the next available PathID (will be incremented).</param>
    /// <param name="unityVersion">Unity version string for the bundle.</param>
    /// <returns>Result containing created assets and any warnings.</returns>
    public static GlbNativeConvertResult ConvertToNativeAssets(
        string glbPath,
        AssetsTools.NET.Extra.AssetsManager am,
        AssetsTools.NET.Extra.AssetsFileInstance afileInst,
        ref long nextPathId,
        string unityVersion)
    {
        var result = new GlbNativeConvertResult();

        try
        {
            // Parse GLB
            var parseResult = ParseGlb(glbPath);
            if (!parseResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to parse GLB: {parseResult.Error}";
                return result;
            }

            result.SourceName = parseResult.SourceName;

            // Create Mesh assets for each extracted mesh
            foreach (var extractedMesh in parseResult.Meshes)
            {
                var meshResult = MeshAssetCreator.CreateMesh(
                    am, afileInst, extractedMesh, nextPathId++);

                if (meshResult.Success)
                {
                    result.CreatedMeshes.Add(new GlbNativeConvertResult.MeshInfo
                    {
                        Name = extractedMesh.Name,
                        PathId = meshResult.PathId,
                        VertexCount = meshResult.VertexCount,
                        IndexCount = meshResult.IndexCount,
                        SubMeshCount = meshResult.SubMeshCount,
                        VertexBufferSize = meshResult.VertexBufferSize,
                        IndexBufferSize = meshResult.IndexBufferSize,
                        Stride = meshResult.Stride,
                        Uses16BitIndices = meshResult.Uses16BitIndices
                    });
                }
                else
                {
                    result.Warnings.Add($"Failed to create mesh '{extractedMesh.Name}': {meshResult.ErrorMessage}");
                }
            }

            // TODO: Create Texture2D assets from GLB materials
            // This would use TextureAssetCreator for each material's textures
            foreach (var material in parseResult.Materials)
            {
                // For now, just track material info in result
                result.ExtractedMaterials.Add(new GlbNativeConvertResult.MaterialInfo
                {
                    Name = material.Name,
                    BaseColorTexture = material.BaseColorTextureName,
                    NormalTexture = material.NormalTextureName,
                    MetallicRoughnessTexture = material.MetallicRoughnessTextureName,
                    HasTextureData = material.BaseColorTextureData != null
                });
            }

            // Create prefab structures for each mesh
            // This creates GameObject + Transform + MeshFilter + MeshRenderer
            if (result.CreatedMeshes.Count > 0)
            {
                var afile = afileInst.file;

                // Find templates for prefab components
                var goTemplate = NativePrefabCreator.FindGameObjectTemplate(afile);
                var trTemplate = NativePrefabCreator.FindTransformTemplate(afile);
                var mfTemplate = NativePrefabCreator.FindMeshFilterTemplate(afile);
                var mrTemplate = NativePrefabCreator.FindMeshRendererTemplate(afile);

                if (goTemplate != null && trTemplate != null && mfTemplate != null && mrTemplate != null)
                {
                    foreach (var meshInfo in result.CreatedMeshes)
                    {
                        // Use empty material list for now (renderer will use default)
                        var materialPathIds = new List<long>();

                        var prefabResult = NativePrefabCreator.CreateMeshPrefab(
                            afile,
                            meshInfo.Name,
                            meshInfo.PathId,
                            materialPathIds,
                            ref nextPathId,
                            goTemplate,
                            trTemplate,
                            mfTemplate,
                            mrTemplate);

                        if (prefabResult.Success)
                        {
                            result.CreatedPrefabs.Add(new GlbNativeConvertResult.PrefabInfo
                            {
                                Name = meshInfo.Name,
                                GameObjectPathId = prefabResult.GameObjectPathId,
                                TransformPathId = prefabResult.TransformPathId,
                                MeshFilterPathId = prefabResult.MeshFilterPathId,
                                MeshRendererPathId = prefabResult.MeshRendererPathId,
                                ResourcePath = prefabResult.ResourcePath
                            });
                        }
                        else
                        {
                            result.Warnings.Add($"Failed to create prefab for mesh '{meshInfo.Name}': {prefabResult.ErrorMessage}");
                        }
                    }
                }
                else
                {
                    result.Warnings.Add("Could not find templates for prefab creation");
                }
            }

            // Track skeleton info
            if (parseResult.Skeletons.Count > 0)
            {
                var skeleton = parseResult.Skeletons[0];
                result.SkeletonInfo = new GlbNativeConvertResult.SkeletonData
                {
                    Name = skeleton.Name,
                    BoneCount = skeleton.Bones.Count,
                    BoneNames = skeleton.Bones.Select(b => b.Name).ToList()
                };
            }

            // Track animation info
            foreach (var anim in parseResult.Animations)
            {
                result.AnimationInfo.Add(new GlbNativeConvertResult.AnimationData
                {
                    Name = anim.Name,
                    Duration = anim.Duration,
                    TrackCount = anim.Tracks.Count
                });
            }

            result.Success = result.CreatedMeshes.Count > 0 ||
                             result.Warnings.Count < parseResult.Meshes.Count;
            result.Message = $"Created {result.CreatedMeshes.Count} mesh(es), " +
                            $"{result.CreatedPrefabs.Count} prefab(s), " +
                            $"extracted {result.ExtractedMaterials.Count} material(s), " +
                            $"{result.AnimationInfo.Count} animation(s)";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"GLB native conversion failed: {ex.Message}";
        }

        return result;
    }

    private static GlbConvertResult ConvertToBundleCore(string glbPath, string outputPath, string unityVersion)
    {
        var result = new GlbConvertResult();

        try
        {
            // Parse GLB
            var parseResult = ParseGlb(glbPath);
            if (!parseResult.Success)
            {
                result.Success = false;
                result.Message = $"Failed to parse GLB: {parseResult.Error}";
                return result;
            }

            // Create output directory
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // For now, write a manifest JSON + extracted texture files
            // Full AssetBundle creation requires Unity's serialization format
            // which is complex to generate from scratch.
            //
            // TODO: Use AssetsTools.NET to create proper Unity assets:
            // - Mesh with vertex buffers
            // - Texture2D with compressed data
            // - Material with shader references
            // - AnimationClip with curve data
            // - Avatar for humanoid rigs
            //
            // For now, we'll create a "glb-manifest.json" that the runtime
            // can use alongside the original GLB file, and use a runtime
            // GLB loader instead.

            var manifest = new GlbManifest
            {
                SourceFile = Path.GetFileName(glbPath),
                Meshes = parseResult.Meshes.Select(m => m.Name).ToList(),
                Materials = parseResult.Materials.Select(m => new GlbManifest.MaterialEntry
                {
                    Name = m.Name,
                    BaseColorTexture = m.BaseColorTextureName,
                    NormalTexture = m.NormalTextureName,
                    MetallicRoughnessTexture = m.MetallicRoughnessTextureName
                }).ToList(),
                HasSkeleton = parseResult.Skeletons.Count > 0,
                SkeletonBoneCount = parseResult.Skeletons.FirstOrDefault()?.Bones.Count ?? 0,
                Animations = parseResult.Animations.Select(a => new GlbManifest.AnimationEntry
                {
                    Name = a.Name,
                    Duration = a.Duration,
                    TrackCount = a.Tracks.Count
                }).ToList()
            };

            // Write manifest
            var manifestPath = Path.ChangeExtension(outputPath, ".glb-manifest.json");
            var json = System.Text.Json.JsonSerializer.Serialize(manifest,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(manifestPath, json);

            // Extract embedded textures to files
            var textureDir = Path.Combine(dir!, "textures");
            Directory.CreateDirectory(textureDir);

            foreach (var mat in parseResult.Materials)
            {
                if (mat.BaseColorTextureData != null && mat.BaseColorTextureName != null)
                {
                    var texPath = Path.Combine(textureDir, mat.BaseColorTextureName + ".png");
                    File.WriteAllBytes(texPath, mat.BaseColorTextureData);
                    result.ConvertedAssets.Add(texPath);
                }
                if (mat.NormalTextureData != null && mat.NormalTextureName != null)
                {
                    var texPath = Path.Combine(textureDir, mat.NormalTextureName + ".png");
                    File.WriteAllBytes(texPath, mat.NormalTextureData);
                    result.ConvertedAssets.Add(texPath);
                }
                if (mat.MetallicRoughnessTextureData != null && mat.MetallicRoughnessTextureName != null)
                {
                    var texPath = Path.Combine(textureDir, mat.MetallicRoughnessTextureName + ".png");
                    File.WriteAllBytes(texPath, mat.MetallicRoughnessTextureData);
                    result.ConvertedAssets.Add(texPath);
                }
            }

            // Copy original GLB for runtime loading
            var glbOutputPath = Path.Combine(dir!, Path.GetFileName(glbPath));
            if (glbPath != glbOutputPath)
                File.Copy(glbPath, glbOutputPath, true);

            result.Success = true;
            result.OutputPath = manifestPath;
            result.Message = $"Extracted {parseResult.Meshes.Count} mesh(es), {parseResult.Materials.Count} material(s), {parseResult.Animations.Count} animation(s)";
            result.ConvertedAssets.Add(glbOutputPath);
            result.ConvertedAssets.Add(manifestPath);

            // Add warning about runtime loader requirement
            result.Warnings.Add("GLB files require runtime loading - ensure ModpackLoader has GLB support enabled");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"GLB conversion failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Manifest describing a converted GLB file.
    /// </summary>
    public class GlbManifest
    {
        public string SourceFile { get; set; } = string.Empty;
        public List<string> Meshes { get; set; } = new();
        public List<MaterialEntry> Materials { get; set; } = new();
        public bool HasSkeleton { get; set; }
        public int SkeletonBoneCount { get; set; }
        public List<AnimationEntry> Animations { get; set; } = new();

        public class MaterialEntry
        {
            public string Name { get; set; } = string.Empty;
            public string? BaseColorTexture { get; set; }
            public string? NormalTexture { get; set; }
            public string? MetallicRoughnessTexture { get; set; }
        }

        public class AnimationEntry
        {
            public string Name { get; set; }  = string.Empty;
            public float Duration { get; set; }
            public int TrackCount { get; set; }
        }
    }
}

/// <summary>
/// Result of parsing a GLB file.
/// </summary>
public class GlbParseResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public List<GlbBundler.ExtractedMesh> Meshes { get; set; } = new();
    public List<GlbBundler.ExtractedMaterial> Materials { get; set; } = new();
    public List<GlbBundler.ExtractedSkeleton> Skeletons { get; set; } = new();
    public List<GlbBundler.ExtractedAnimation> Animations { get; set; } = new();
}

/// <summary>
/// Result of converting GLB to native Unity assets.
/// </summary>
public class GlbNativeConvertResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Message { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Information about created Mesh assets.
    /// </summary>
    public List<MeshInfo> CreatedMeshes { get; set; } = new();

    /// <summary>
    /// Information about extracted materials (textures not yet converted to native assets).
    /// </summary>
    public List<MaterialInfo> ExtractedMaterials { get; set; } = new();

    /// <summary>
    /// Skeleton/armature information if present.
    /// </summary>
    public SkeletonData? SkeletonInfo { get; set; }

    /// <summary>
    /// Animation information.
    /// </summary>
    public List<AnimationData> AnimationInfo { get; set; } = new();

    /// <summary>
    /// Information about created Prefab structures.
    /// </summary>
    public List<PrefabInfo> CreatedPrefabs { get; set; } = new();

    public class MeshInfo
    {
        public string Name { get; set; } = string.Empty;
        public long PathId { get; set; }
        public int VertexCount { get; set; }
        public int IndexCount { get; set; }
        public int SubMeshCount { get; set; }
        public int VertexBufferSize { get; set; }
        public int IndexBufferSize { get; set; }
        public int Stride { get; set; }
        public bool Uses16BitIndices { get; set; }
    }

    public class MaterialInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? BaseColorTexture { get; set; }
        public string? NormalTexture { get; set; }
        public string? MetallicRoughnessTexture { get; set; }
        public bool HasTextureData { get; set; }
    }

    public class SkeletonData
    {
        public string Name { get; set; } = string.Empty;
        public int BoneCount { get; set; }
        public List<string> BoneNames { get; set; } = new();
    }

    public class AnimationData
    {
        public string Name { get; set; } = string.Empty;
        public float Duration { get; set; }
        public int TrackCount { get; set; }
    }

    public class PrefabInfo
    {
        public string Name { get; set; } = string.Empty;
        public long GameObjectPathId { get; set; }
        public long TransformPathId { get; set; }
        public long MeshFilterPathId { get; set; }
        public long MeshRendererPathId { get; set; }
        public string ResourcePath { get; set; } = string.Empty;
    }
}
