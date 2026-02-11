using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using SharpGLTF.Schema2;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;
using Quaternion = UnityEngine.Quaternion;
using Color = UnityEngine.Color;
using Matrix4x4 = UnityEngine.Matrix4x4;
using SysNumerics = System.Numerics;

// Disambiguate between GLTF and Unity types
using UnityMesh = UnityEngine.Mesh;
using UnityMaterial = UnityEngine.Material;
using GltfMesh = SharpGLTF.Schema2.Mesh;
using GltfMaterial = SharpGLTF.Schema2.Material;

namespace Menace.ModpackLoader;

/// <summary>
/// Runtime GLB/GLTF loader that converts 3D model files to Unity objects.
/// Creates Mesh, Material, and Texture2D assets that the existing
/// AssetReplacer can use for in-game replacements.
/// </summary>
public static class GlbLoader
{
    private static readonly MelonLogger.Instance _log = new("GlbLoader");

    /// <summary>
    /// Loaded model with all its components.
    /// </summary>
    public class LoadedModel
    {
        public string Name { get; set; } = string.Empty;
        public List<UnityMesh> Meshes { get; set; } = new();
        public List<UnityMaterial> Materials { get; set; } = new();
        public List<Texture2D> Textures { get; set; } = new();
        public GameObject RootPrefab { get; set; }
    }

    private static readonly List<LoadedModel> _loadedModels = new();
    public static int LoadedCount => _loadedModels.Count;

    /// <summary>
    /// Load all GLB files from a modpack's models directory.
    /// Called during modpack initialization.
    /// </summary>
    public static void LoadModpackModels(string modpackPath)
    {
        var modelsDir = Path.Combine(modpackPath, "models");
        if (!Directory.Exists(modelsDir))
            return;

        // Find GLB files (either copied directly or via manifest)
        var glbFiles = Directory.GetFiles(modelsDir, "*.glb", SearchOption.AllDirectories);

        foreach (var glbPath in glbFiles)
        {
            try
            {
                var model = LoadGlb(glbPath);
                if (model != null)
                {
                    _loadedModels.Add(model);

                    // Register meshes, materials, and textures with BundleLoader
                    // so AssetReplacer can find them for in-game replacement
                    foreach (var mesh in model.Meshes)
                    {
                        BundleLoader.RegisterAsset(mesh.name, mesh, "Mesh");
                    }
                    foreach (var mat in model.Materials)
                    {
                        BundleLoader.RegisterAsset(mat.name, mat, "Material");
                    }
                    foreach (var tex in model.Textures)
                    {
                        BundleLoader.RegisterAsset(tex.name, tex, "Texture2D");
                    }
                    if (model.RootPrefab != null)
                    {
                        BundleLoader.RegisterAsset(model.Name, model.RootPrefab, "GameObject");
                    }

                    _log.Msg($"Loaded GLB: {Path.GetFileName(glbPath)} ({model.Meshes.Count} meshes, {model.Materials.Count} materials)");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to load GLB {Path.GetFileName(glbPath)}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Load a single GLB file and create Unity objects.
    /// </summary>
    public static LoadedModel LoadGlb(string glbPath)
    {
        if (!File.Exists(glbPath))
        {
            _log.Warning($"GLB file not found: {glbPath}");
            return null;
        }

        var modelRoot = ModelRoot.Load(glbPath);
        var modelName = Path.GetFileNameWithoutExtension(glbPath);

        var result = new LoadedModel { Name = modelName };

        // Load all textures first (materials reference them)
        var textureMap = new Dictionary<int, Texture2D>();
        foreach (var image in modelRoot.LogicalImages)
        {
            var tex = LoadTexture(image, modelName);
            if (tex != null)
            {
                textureMap[image.LogicalIndex] = tex;
                result.Textures.Add(tex);
            }
        }

        // Load materials
        var materialMap = new Dictionary<int, UnityMaterial>();
        foreach (var gltfMat in modelRoot.LogicalMaterials)
        {
            var mat = CreateMaterial(gltfMat, textureMap, modelName);
            materialMap[gltfMat.LogicalIndex] = mat;
            result.Materials.Add(mat);
        }

        // Create a default material if none exist
        if (materialMap.Count == 0)
        {
            var defaultMat = new UnityMaterial(Shader.Find("Standard"));
            defaultMat.name = $"{modelName}_DefaultMaterial";
            materialMap[-1] = defaultMat;
            result.Materials.Add(defaultMat);
        }

        // Create root prefab GameObject
        var rootGO = new GameObject(modelName);
        UnityEngine.Object.DontDestroyOnLoad(rootGO);
        rootGO.SetActive(false); // Keep inactive until needed

        // Load meshes and create child GameObjects
        foreach (var gltfMesh in modelRoot.LogicalMeshes)
        {
            var mesh = CreateMesh(gltfMesh, modelName);
            if (mesh != null)
            {
                result.Meshes.Add(mesh);

                // Create a child GameObject with the mesh
                var meshGO = new GameObject(mesh.name);
                meshGO.transform.SetParent(rootGO.transform, false);

                // Determine if this is a skinned mesh
                var skin = modelRoot.LogicalSkins.FirstOrDefault(s =>
                    modelRoot.LogicalNodes.Any(n => n.Mesh == gltfMesh && n.Skin == s));

                if (skin != null)
                {
                    var smr = meshGO.AddComponent<SkinnedMeshRenderer>();
                    smr.sharedMesh = mesh;

                    // Get material for first primitive
                    var matIndex = gltfMesh.Primitives.FirstOrDefault()?.Material?.LogicalIndex ?? -1;
                    smr.sharedMaterial = materialMap.GetValueOrDefault(matIndex, materialMap.Values.First());

                    // Set up bones (simplified - full bone setup requires node hierarchy)
                    SetupBones(smr, skin, modelRoot);
                }
                else
                {
                    var mf = meshGO.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;

                    var mr = meshGO.AddComponent<MeshRenderer>();
                    var matIndex = gltfMesh.Primitives.FirstOrDefault()?.Material?.LogicalIndex ?? -1;
                    mr.sharedMaterial = materialMap.GetValueOrDefault(matIndex, materialMap.Values.First());
                }
            }
        }

        result.RootPrefab = rootGO;
        return result;
    }

    /// <summary>
    /// Load a texture from a GLTF image.
    /// </summary>
    private static Texture2D LoadTexture(SharpGLTF.Schema2.Image image, string modelName)
    {
        try
        {
            var content = image.Content.Content.ToArray();
            if (content == null || content.Length == 0)
                return null;

            var tex = new Texture2D(2, 2);
            tex.name = image.Name ?? $"{modelName}_Texture_{image.LogicalIndex}";

            var il2cppBytes = new Il2CppStructArray<byte>(content);
            if (ImageConversion.LoadImage(tex, il2cppBytes))
            {
                return tex;
            }
            else
            {
                _log.Warning($"Failed to decode texture: {tex.name}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Error loading texture from GLB: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Create a Unity Material from a GLTF material.
    /// </summary>
    private static UnityMaterial CreateMaterial(GltfMaterial gltfMat,
        Dictionary<int, Texture2D> textures, string modelName)
    {
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");

        var mat = new UnityMaterial(shader);
        mat.name = gltfMat.Name ?? $"{modelName}_Material_{gltfMat.LogicalIndex}";

        // Base color
        var baseColorChannel = gltfMat.FindChannel("BaseColor");
        if (baseColorChannel.HasValue)
        {
            var color = baseColorChannel.Value.Color;
            mat.color = new Color(color.X, color.Y, color.Z, color.W);

            var tex = baseColorChannel.Value.Texture;
            if (tex != null && textures.TryGetValue(tex.PrimaryImage.LogicalIndex, out var unityTex))
            {
                mat.mainTexture = unityTex;
            }
        }

        // Metallic/Roughness
        var mrChannel = gltfMat.FindChannel("MetallicRoughness");
        if (mrChannel.HasValue && mat.HasProperty("_Metallic"))
        {
            foreach (var param in mrChannel.Value.Parameters)
            {
                if (param.Name == "MetallicFactor" && mat.HasProperty("_Metallic"))
                    mat.SetFloat("_Metallic", (float)param.Value);
                else if (param.Name == "RoughnessFactor" && mat.HasProperty("_Glossiness"))
                    mat.SetFloat("_Glossiness", 1.0f - (float)param.Value);
            }
        }

        // Normal map
        var normalChannel = gltfMat.FindChannel("Normal");
        if (normalChannel.HasValue)
        {
            var tex = normalChannel.Value.Texture;
            if (tex != null && textures.TryGetValue(tex.PrimaryImage.LogicalIndex, out var normalTex))
            {
                if (mat.HasProperty("_BumpMap"))
                    mat.SetTexture("_BumpMap", normalTex);
            }
        }

        // Emission
        var emissiveChannel = gltfMat.FindChannel("Emissive");
        if (emissiveChannel.HasValue)
        {
            var color = emissiveChannel.Value.Color;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(color.X, color.Y, color.Z));
            }
        }

        return mat;
    }

    /// <summary>
    /// Create a Unity Mesh from a GLTF mesh.
    /// </summary>
    private static UnityMesh CreateMesh(GltfMesh gltfMesh, string modelName)
    {
        var mesh = new UnityMesh();
        mesh.name = gltfMesh.Name ?? $"{modelName}_Mesh_{gltfMesh.LogicalIndex}";

        var allVertices = new List<Vector3>();
        var allNormals = new List<Vector3>();
        var allTangents = new List<Vector4>();
        var allUV0 = new List<Vector2>();
        var allColors = new List<Color>();
        var allIndices = new List<int>();
        var allBoneWeights = new List<BoneWeight>();
        var subMeshes = new List<(int start, int count)>();

        int vertexOffset = 0;

        foreach (var primitive in gltfMesh.Primitives)
        {
            var positionAccessor = primitive.GetVertexAccessor("POSITION");
            if (positionAccessor == null) continue;

            var positions = positionAccessor.AsVector3Array();
            foreach (var pos in positions)
            {
                // Convert from GLTF (Y-up, right-handed) to Unity (Y-up, left-handed)
                allVertices.Add(new Vector3(pos.X, pos.Y, -pos.Z));
            }

            // Normals
            var normalAccessor = primitive.GetVertexAccessor("NORMAL");
            if (normalAccessor != null)
            {
                foreach (var n in normalAccessor.AsVector3Array())
                {
                    allNormals.Add(new Vector3(n.X, n.Y, -n.Z));
                }
            }

            // Tangents
            var tangentAccessor = primitive.GetVertexAccessor("TANGENT");
            if (tangentAccessor != null)
            {
                foreach (var t in tangentAccessor.AsVector4Array())
                {
                    allTangents.Add(new Vector4(t.X, t.Y, -t.Z, -t.W));
                }
            }

            // UV0
            var uv0Accessor = primitive.GetVertexAccessor("TEXCOORD_0");
            if (uv0Accessor != null)
            {
                foreach (var uv in uv0Accessor.AsVector2Array())
                {
                    allUV0.Add(new Vector2(uv.X, 1 - uv.Y)); // Flip V
                }
            }

            // Vertex colors
            var colorAccessor = primitive.GetVertexAccessor("COLOR_0");
            if (colorAccessor != null)
            {
                foreach (var c in colorAccessor.AsVector4Array())
                {
                    allColors.Add(new Color(c.X, c.Y, c.Z, c.W));
                }
            }

            // Bone weights
            var weightsAccessor = primitive.GetVertexAccessor("WEIGHTS_0");
            var jointsAccessor = primitive.GetVertexAccessor("JOINTS_0");
            if (weightsAccessor != null && jointsAccessor != null)
            {
                var weights = weightsAccessor.AsVector4Array();
                var joints = jointsAccessor.AsVector4Array();

                for (int i = 0; i < weights.Count; i++)
                {
                    var bw = new BoneWeight
                    {
                        weight0 = weights[i].X,
                        weight1 = weights[i].Y,
                        weight2 = weights[i].Z,
                        weight3 = weights[i].W,
                        boneIndex0 = (int)joints[i].X,
                        boneIndex1 = (int)joints[i].Y,
                        boneIndex2 = (int)joints[i].Z,
                        boneIndex3 = (int)joints[i].W
                    };
                    allBoneWeights.Add(bw);
                }
            }

            // Indices - reverse winding for left-handed
            var indexAccessor = primitive.IndexAccessor;
            int indexStart = allIndices.Count;
            if (indexAccessor != null)
            {
                var indices = indexAccessor.AsIndicesArray();
                for (int i = 0; i < indices.Count; i += 3)
                {
                    allIndices.Add((int)indices[i] + vertexOffset);
                    allIndices.Add((int)indices[i + 2] + vertexOffset);
                    allIndices.Add((int)indices[i + 1] + vertexOffset);
                }
            }

            subMeshes.Add((indexStart, allIndices.Count - indexStart));
            vertexOffset += positions.Count;
        }

        // Assign mesh data - use arrays for IL2CPP compatibility
        mesh.vertices = allVertices.ToArray();

        if (allNormals.Count == allVertices.Count)
            mesh.normals = allNormals.ToArray();

        if (allTangents.Count == allVertices.Count)
            mesh.tangents = allTangents.ToArray();

        if (allUV0.Count == allVertices.Count)
            mesh.uv = allUV0.ToArray();

        if (allColors.Count == allVertices.Count)
            mesh.colors = allColors.ToArray();

        mesh.triangles = allIndices.ToArray();

        // Set up submeshes
        if (subMeshes.Count > 1)
        {
            mesh.subMeshCount = subMeshes.Count;
            for (int i = 0; i < subMeshes.Count; i++)
            {
                mesh.SetSubMesh(i, new UnityEngine.Rendering.SubMeshDescriptor(
                    subMeshes[i].start, subMeshes[i].count));
            }
        }

        // Set bone weights if present
        if (allBoneWeights.Count == allVertices.Count)
        {
            mesh.boneWeights = allBoneWeights.ToArray();
        }

        // Finalize
        if (allNormals.Count != allVertices.Count)
            mesh.RecalculateNormals();

        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Set up bone transforms for a skinned mesh renderer.
    /// </summary>
    private static void SetupBones(SkinnedMeshRenderer smr, Skin skin, ModelRoot model)
    {
        var bones = new Transform[skin.JointsCount];
        var bindPoses = new Matrix4x4[skin.JointsCount];

        var boneRoot = new GameObject("Armature").transform;
        boneRoot.SetParent(smr.transform.parent, false);

        for (int i = 0; i < skin.JointsCount; i++)
        {
            var (joint, inverseBindMatrix) = skin.GetJoint(i);

            var boneGO = new GameObject(joint.Name ?? $"Bone_{i}");
            boneGO.transform.SetParent(boneRoot, false);

            // Set local transform from GLTF
            var localTransform = joint.LocalTransform;
            boneGO.transform.localPosition = ConvertPosition(localTransform.Translation);
            boneGO.transform.localRotation = ConvertRotation(localTransform.Rotation);
            boneGO.transform.localScale = ConvertScale(localTransform.Scale);

            bones[i] = boneGO.transform;

            // Convert inverse bind matrix
            bindPoses[i] = ConvertMatrix(inverseBindMatrix);
        }

        smr.bones = bones;
        smr.sharedMesh.bindposes = bindPoses;
        smr.rootBone = boneRoot;
    }

    // Coordinate conversion helpers (GLTF right-handed → Unity left-handed)

    private static Vector3 ConvertPosition(SysNumerics.Vector3 v)
    {
        return new Vector3(v.X, v.Y, -v.Z);
    }

    private static Quaternion ConvertRotation(SysNumerics.Quaternion q)
    {
        return new Quaternion(q.X, q.Y, -q.Z, -q.W);
    }

    private static Vector3 ConvertScale(SysNumerics.Vector3 v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

    private static Matrix4x4 ConvertMatrix(SysNumerics.Matrix4x4 m)
    {
        // Convert from right-handed to left-handed by negating Z components
        return new Matrix4x4(
            new Vector4(m.M11, m.M12, -m.M13, m.M14),
            new Vector4(m.M21, m.M22, -m.M23, m.M24),
            new Vector4(-m.M31, -m.M32, m.M33, -m.M34),
            new Vector4(m.M41, m.M42, -m.M43, m.M44)
        );
    }
}
