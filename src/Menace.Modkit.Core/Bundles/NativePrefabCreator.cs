using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Creates native prefab structures (GameObject + Transform + MeshFilter + MeshRenderer)
/// using raw binary manipulation. Works with Unity 6 which doesn't embed type trees.
///
/// A prefab structure consists of:
/// - GameObject: Root object with component array
/// - Transform: Hierarchy management (position, rotation, scale, parent/children)
/// - MeshFilter: References the Mesh asset
/// - MeshRenderer: References Materials for rendering
///
/// All components are linked via PPtrs (FileID=0 for same file, PathID=target asset ID).
/// </summary>
public static class NativePrefabCreator
{
    /// <summary>
    /// Result of prefab creation.
    /// </summary>
    public class PrefabCreationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string PrefabName { get; set; } = string.Empty;

        // PathIDs of created assets
        public long GameObjectPathId { get; set; }
        public long TransformPathId { get; set; }
        public long MeshFilterPathId { get; set; }
        public long MeshRendererPathId { get; set; }

        // Resource path for loading via Resources.Load
        public string ResourcePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Template for creating GameObjects.
    /// </summary>
    public class GameObjectTemplate
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public int ComponentCountOffset { get; set; }
        public int ComponentArrayOffset { get; set; }
        public int NameOffset { get; set; }
        public int LayerOffset { get; set; }
        public int IsActiveOffset { get; set; }
    }

    /// <summary>
    /// Template for creating Transforms.
    /// </summary>
    public class TransformTemplate
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public int GameObjectPtrOffset { get; set; }
        public int LocalRotationOffset { get; set; }  // Quaternion (16 bytes)
        public int LocalPositionOffset { get; set; }  // Vector3 (12 bytes)
        public int LocalScaleOffset { get; set; }     // Vector3 (12 bytes)
        public int ChildrenCountOffset { get; set; }
        public int ChildrenArrayOffset { get; set; }
        public int FatherPtrOffset { get; set; }
    }

    /// <summary>
    /// Template for creating MeshFilters.
    /// </summary>
    public class MeshFilterTemplate
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public int GameObjectPtrOffset { get; set; }
        public int MeshPtrOffset { get; set; }
    }

    /// <summary>
    /// Template for creating MeshRenderers.
    /// </summary>
    public class MeshRendererTemplate
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public int GameObjectPtrOffset { get; set; }
        public int MaterialsArrayOffset { get; set; }
    }

    /// <summary>
    /// Find a GameObject to use as template.
    /// </summary>
    public static GameObjectTemplate? FindGameObjectTemplate(AssetsFile afile)
    {
        var reader = afile.Reader;

        // Look for a simple GameObject (3 components: Transform, MeshFilter, MeshRenderer)
        foreach (var info in afile.GetAssetsOfType(AssetClassID.GameObject))
        {
            try
            {
                var absOffset = info.GetAbsoluteByteOffset(afile);
                reader.BaseStream.Position = absOffset;
                var bytes = reader.ReadBytes((int)info.ByteSize);

                var template = TryParseGameObject(bytes);
                if (template != null)
                    return template;
            }
            catch
            {
                // Try next
            }
        }

        return null;
    }

    /// <summary>
    /// Find a Transform to use as template.
    /// </summary>
    public static TransformTemplate? FindTransformTemplate(AssetsFile afile)
    {
        var reader = afile.Reader;

        foreach (var info in afile.GetAssetsOfType(AssetClassID.Transform))
        {
            try
            {
                var absOffset = info.GetAbsoluteByteOffset(afile);
                reader.BaseStream.Position = absOffset;
                var bytes = reader.ReadBytes((int)info.ByteSize);

                var template = TryParseTransform(bytes);
                if (template != null)
                    return template;
            }
            catch
            {
                // Try next
            }
        }

        return null;
    }

    /// <summary>
    /// Find a MeshFilter to use as template.
    /// </summary>
    public static MeshFilterTemplate? FindMeshFilterTemplate(AssetsFile afile)
    {
        var reader = afile.Reader;

        foreach (var info in afile.GetAssetsOfType(AssetClassID.MeshFilter))
        {
            try
            {
                var absOffset = info.GetAbsoluteByteOffset(afile);
                reader.BaseStream.Position = absOffset;
                var bytes = reader.ReadBytes((int)info.ByteSize);

                var template = TryParseMeshFilter(bytes);
                if (template != null)
                    return template;
            }
            catch
            {
                // Try next
            }
        }

        return null;
    }

    /// <summary>
    /// Find a MeshRenderer to use as template.
    /// </summary>
    public static MeshRendererTemplate? FindMeshRendererTemplate(AssetsFile afile)
    {
        var reader = afile.Reader;

        foreach (var info in afile.GetAssetsOfType(AssetClassID.MeshRenderer))
        {
            try
            {
                var absOffset = info.GetAbsoluteByteOffset(afile);
                reader.BaseStream.Position = absOffset;
                var bytes = reader.ReadBytes((int)info.ByteSize);

                var template = TryParseMeshRenderer(bytes);
                if (template != null)
                    return template;
            }
            catch
            {
                // Try next
            }
        }

        return null;
    }

    /// <summary>
    /// Parse a GameObject to detect field offsets.
    /// GameObject binary structure:
    /// - m_Component (array of ComponentPair)
    ///   - 4 bytes: array size (component count)
    ///   - For each component:
    ///     - 4 bytes: FileID (usually 0)
    ///     - 8 bytes: PathID
    /// - m_Layer (int32)
    /// - m_Name (string: 4-byte length + chars + padding)
    /// - m_Tag (uint16)
    /// - m_IsActive (bool/byte)
    /// </summary>
    private static GameObjectTemplate? TryParseGameObject(byte[] bytes)
    {
        if (bytes.Length < 40) return null;

        try
        {
            int offset = 0;

            // m_Component array
            int componentCount = BitConverter.ToInt32(bytes, offset);
            if (componentCount < 1 || componentCount > 100) return null;
            int componentCountOffset = offset;
            int componentArrayOffset = offset + 4;
            offset += 4;

            // Each component is: FileID (4) + PathID (8) = 12 bytes
            offset += componentCount * 12;

            // m_Layer (int32)
            int layerOffset = offset;
            int layer = BitConverter.ToInt32(bytes, offset);
            if (layer < 0 || layer > 31) return null;
            offset += 4;

            // m_Name (string)
            int nameOffset = offset;
            int nameLen = BitConverter.ToInt32(bytes, offset);
            if (nameLen < 0 || nameLen > 500) return null;
            offset += 4 + nameLen;
            offset = Align4(offset);

            // m_Tag (uint16)
            offset += 2;
            offset = Align4(offset);

            // m_IsActive (byte)
            int isActiveOffset = offset;

            return new GameObjectTemplate
            {
                Bytes = bytes,
                ComponentCountOffset = componentCountOffset,
                ComponentArrayOffset = componentArrayOffset,
                NameOffset = nameOffset,
                LayerOffset = layerOffset,
                IsActiveOffset = isActiveOffset
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse a Transform to detect field offsets.
    /// Transform binary structure:
    /// - m_GameObject (PPtr: FileID 4 + PathID 8)
    /// - m_LocalRotation (Quaternion: 16 bytes x,y,z,w)
    /// - m_LocalPosition (Vector3: 12 bytes)
    /// - m_LocalScale (Vector3: 12 bytes)
    /// - m_ConstrainProportionsScale (bool) [Unity 2021+]
    /// - m_Children (array of PPtr<Transform>)
    ///   - 4 bytes: count
    ///   - count * 12 bytes: PPtrs
    /// - m_Father (PPtr<Transform>)
    /// </summary>
    private static TransformTemplate? TryParseTransform(byte[] bytes)
    {
        if (bytes.Length < 60) return null;

        try
        {
            int offset = 0;

            // m_GameObject PPtr
            int gameObjectPtrOffset = offset;
            int fileId = BitConverter.ToInt32(bytes, offset);
            long pathId = BitConverter.ToInt64(bytes, offset + 4);
            if (fileId != 0 || pathId <= 0) return null; // Must reference same file
            offset += 12;

            // m_LocalRotation (Quaternion: x, y, z, w)
            int localRotationOffset = offset;
            offset += 16;

            // m_LocalPosition (Vector3)
            int localPositionOffset = offset;
            offset += 12;

            // m_LocalScale (Vector3)
            int localScaleOffset = offset;
            offset += 12;

            // m_ConstrainProportionsScale (bool, aligned) - Unity 2021+
            // This may or may not be present, check by looking at children count location
            int childrenCountOffset = offset;
            int childrenCount = BitConverter.ToInt32(bytes, offset);

            // If children count looks invalid, try skipping the bool+alignment
            if (childrenCount < 0 || childrenCount > 1000)
            {
                offset += 4; // bool + padding
                childrenCountOffset = offset;
                childrenCount = BitConverter.ToInt32(bytes, offset);

                if (childrenCount < 0 || childrenCount > 1000) return null;
            }

            int childrenArrayOffset = offset + 4;
            offset += 4 + childrenCount * 12;

            // m_Father PPtr
            int fatherPtrOffset = offset;

            return new TransformTemplate
            {
                Bytes = bytes,
                GameObjectPtrOffset = gameObjectPtrOffset,
                LocalRotationOffset = localRotationOffset,
                LocalPositionOffset = localPositionOffset,
                LocalScaleOffset = localScaleOffset,
                ChildrenCountOffset = childrenCountOffset,
                ChildrenArrayOffset = childrenArrayOffset,
                FatherPtrOffset = fatherPtrOffset
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse a MeshFilter to detect field offsets.
    /// MeshFilter binary structure:
    /// - m_GameObject (PPtr: 12 bytes)
    /// - m_Mesh (PPtr: 12 bytes)
    /// </summary>
    private static MeshFilterTemplate? TryParseMeshFilter(byte[] bytes)
    {
        if (bytes.Length < 24) return null;

        try
        {
            // MeshFilter is simple: just two PPtrs
            int gameObjectPtrOffset = 0;
            int meshPtrOffset = 12;

            // Validate: both should have FileID=0 for same-file references
            int fileId1 = BitConverter.ToInt32(bytes, 0);
            int fileId2 = BitConverter.ToInt32(bytes, 12);

            // FileID of 0 means same file
            if (fileId1 != 0) return null;

            return new MeshFilterTemplate
            {
                Bytes = bytes,
                GameObjectPtrOffset = gameObjectPtrOffset,
                MeshPtrOffset = meshPtrOffset
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse a MeshRenderer to detect field offsets.
    /// MeshRenderer is complex with many fields. We focus on key ones:
    /// - m_GameObject (PPtr)
    /// - m_Materials (array of PPtr<Material>)
    /// </summary>
    private static MeshRendererTemplate? TryParseMeshRenderer(byte[] bytes)
    {
        if (bytes.Length < 50) return null;

        try
        {
            // MeshRenderer inherits from Renderer which has many fields
            // Structure varies by Unity version, we'll detect key offsets
            int offset = 0;

            // m_GameObject PPtr (first field from Component base)
            int gameObjectPtrOffset = offset;
            int fileId = BitConverter.ToInt32(bytes, offset);
            if (fileId != 0) return null;
            offset += 12;

            // Skip through Renderer base class fields to find m_Materials
            // This is complex - scan for an array that looks like material ptrs
            int materialsArrayOffset = -1;

            for (int scan = 12; scan < bytes.Length - 16; scan += 4)
            {
                int count = BitConverter.ToInt32(bytes, scan);
                if (count >= 1 && count <= 10) // Reasonable material count
                {
                    // Check if following bytes look like PPtrs
                    bool looksLikePtrs = true;
                    for (int i = 0; i < count && scan + 4 + i * 12 + 12 <= bytes.Length; i++)
                    {
                        int ptrFileId = BitConverter.ToInt32(bytes, scan + 4 + i * 12);
                        if (ptrFileId != 0 && ptrFileId != 4) // FileID 4 is common external ref
                        {
                            looksLikePtrs = false;
                            break;
                        }
                    }

                    if (looksLikePtrs)
                    {
                        materialsArrayOffset = scan;
                        break;
                    }
                }
            }

            if (materialsArrayOffset < 0) return null;

            return new MeshRendererTemplate
            {
                Bytes = bytes,
                GameObjectPtrOffset = gameObjectPtrOffset,
                MaterialsArrayOffset = materialsArrayOffset
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create a complete prefab structure for a mesh.
    /// Returns connected GameObject + Transform + MeshFilter + MeshRenderer.
    /// </summary>
    /// <param name="afile">The assets file to add to.</param>
    /// <param name="prefabName">Name for the prefab/GameObject.</param>
    /// <param name="meshPathId">PathID of the Mesh asset to reference.</param>
    /// <param name="materialPathIds">PathIDs of Material assets to use.</param>
    /// <param name="nextPathId">Next available PathID (will be incremented).</param>
    /// <param name="goTemplate">GameObject template.</param>
    /// <param name="trTemplate">Transform template.</param>
    /// <param name="mfTemplate">MeshFilter template.</param>
    /// <param name="mrTemplate">MeshRenderer template.</param>
    /// <returns>Result containing created asset PathIDs.</returns>
    public static PrefabCreationResult CreateMeshPrefab(
        AssetsFile afile,
        string prefabName,
        long meshPathId,
        List<long> materialPathIds,
        ref long nextPathId,
        GameObjectTemplate goTemplate,
        TransformTemplate trTemplate,
        MeshFilterTemplate mfTemplate,
        MeshRendererTemplate mrTemplate)
    {
        var result = new PrefabCreationResult { PrefabName = prefabName };

        try
        {
            // Allocate PathIDs for the new assets
            long gameObjectPathId = nextPathId++;
            long transformPathId = nextPathId++;
            long meshFilterPathId = nextPathId++;
            long meshRendererPathId = nextPathId++;

            result.GameObjectPathId = gameObjectPathId;
            result.TransformPathId = transformPathId;
            result.MeshFilterPathId = meshFilterPathId;
            result.MeshRendererPathId = meshRendererPathId;

            // Create Transform
            var transformBytes = CreateTransformBytes(
                trTemplate, gameObjectPathId, transformPathId);
            var transformInfo = AssetFileInfo.Create(afile, transformPathId, (int)AssetClassID.Transform, 0);
            if (transformInfo == null)
            {
                result.ErrorMessage = "Failed to create Transform asset - Unity 6 without type trees";
                return result;
            }
            transformInfo.SetNewData(transformBytes);
            afile.Metadata.AddAssetInfo(transformInfo);

            // Create MeshFilter
            var meshFilterBytes = CreateMeshFilterBytes(
                mfTemplate, gameObjectPathId, meshPathId);
            var meshFilterInfo = AssetFileInfo.Create(afile, meshFilterPathId, (int)AssetClassID.MeshFilter, 0);
            if (meshFilterInfo == null)
            {
                result.ErrorMessage = "Failed to create MeshFilter asset - Unity 6 without type trees";
                return result;
            }
            meshFilterInfo.SetNewData(meshFilterBytes);
            afile.Metadata.AddAssetInfo(meshFilterInfo);

            // Create MeshRenderer
            var meshRendererBytes = CreateMeshRendererBytes(
                mrTemplate, gameObjectPathId, materialPathIds);
            var meshRendererInfo = AssetFileInfo.Create(afile, meshRendererPathId, (int)AssetClassID.MeshRenderer, 0);
            if (meshRendererInfo == null)
            {
                result.ErrorMessage = "Failed to create MeshRenderer asset - Unity 6 without type trees";
                return result;
            }
            meshRendererInfo.SetNewData(meshRendererBytes);
            afile.Metadata.AddAssetInfo(meshRendererInfo);

            // Create GameObject (must be last to reference components)
            var gameObjectBytes = CreateGameObjectBytes(
                goTemplate, prefabName,
                transformPathId, meshFilterPathId, meshRendererPathId);
            var gameObjectInfo = AssetFileInfo.Create(afile, gameObjectPathId, (int)AssetClassID.GameObject, 0);
            if (gameObjectInfo == null)
            {
                result.ErrorMessage = "Failed to create GameObject asset - Unity 6 without type trees";
                return result;
            }
            gameObjectInfo.SetNewData(gameObjectBytes);
            afile.Metadata.AddAssetInfo(gameObjectInfo);

            result.Success = true;
            result.ResourcePath = $"prefabs/{prefabName.ToLower()}";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to create prefab: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Create GameObject bytes with specified components.
    /// </summary>
    private static byte[] CreateGameObjectBytes(
        GameObjectTemplate template,
        string name,
        long transformPathId,
        long meshFilterPathId,
        long meshRendererPathId)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Component array with 3 components
        bw.Write(3); // component count

        // Transform component (ClassID 4)
        bw.Write(0);              // FileID
        bw.Write(transformPathId);// PathID

        // MeshFilter component (ClassID 33)
        bw.Write(0);
        bw.Write(meshFilterPathId);

        // MeshRenderer component (ClassID 23)
        bw.Write(0);
        bw.Write(meshRendererPathId);

        // Layer
        bw.Write(0); // Default layer

        // Name (string)
        var nameBytes = Encoding.UTF8.GetBytes(name);
        bw.Write(nameBytes.Length);
        bw.Write(nameBytes);
        AlignWriter(bw, 4);

        // Tag (uint16)
        bw.Write((ushort)0);
        AlignWriter(bw, 4);

        // IsActive (byte)
        bw.Write((byte)1);

        return ms.ToArray();
    }

    /// <summary>
    /// Create Transform bytes referencing its GameObject.
    /// </summary>
    private static byte[] CreateTransformBytes(
        TransformTemplate template,
        long gameObjectPathId,
        long selfPathId)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // m_GameObject PPtr
        bw.Write(0);              // FileID
        bw.Write(gameObjectPathId);

        // m_LocalRotation (identity quaternion)
        bw.Write(0f);  // x
        bw.Write(0f);  // y
        bw.Write(0f);  // z
        bw.Write(1f);  // w

        // m_LocalPosition (origin)
        bw.Write(0f);  // x
        bw.Write(0f);  // y
        bw.Write(0f);  // z

        // m_LocalScale (1,1,1)
        bw.Write(1f);  // x
        bw.Write(1f);  // y
        bw.Write(1f);  // z

        // m_ConstrainProportionsScale (bool + alignment)
        bw.Write((byte)0);
        AlignWriter(bw, 4);

        // m_Children (empty array)
        bw.Write(0);   // count

        // m_Father (null - root transform)
        bw.Write(0);   // FileID
        bw.Write(0L);  // PathID

        return ms.ToArray();
    }

    /// <summary>
    /// Create MeshFilter bytes referencing its GameObject and Mesh.
    /// </summary>
    private static byte[] CreateMeshFilterBytes(
        MeshFilterTemplate template,
        long gameObjectPathId,
        long meshPathId)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // m_GameObject PPtr
        bw.Write(0);
        bw.Write(gameObjectPathId);

        // m_Mesh PPtr
        bw.Write(0);  // FileID (same file)
        bw.Write(meshPathId);

        return ms.ToArray();
    }

    /// <summary>
    /// Create MeshRenderer bytes with materials.
    /// MeshRenderer is complex - we clone the template and patch key fields.
    /// </summary>
    private static byte[] CreateMeshRendererBytes(
        MeshRendererTemplate template,
        long gameObjectPathId,
        List<long> materialPathIds)
    {
        // Clone template bytes
        var bytes = (byte[])template.Bytes.Clone();

        // Patch m_GameObject PPtr
        WriteInt32(bytes, template.GameObjectPtrOffset, 0);
        WriteInt64(bytes, template.GameObjectPtrOffset + 4, gameObjectPathId);

        // Patch m_Materials array
        // First, update count
        WriteInt32(bytes, template.MaterialsArrayOffset, materialPathIds.Count);

        // If material count differs from template, we need to resize
        // For now, assume same count or fewer
        int ptrOffset = template.MaterialsArrayOffset + 4;
        for (int i = 0; i < materialPathIds.Count; i++)
        {
            if (ptrOffset + 12 <= bytes.Length)
            {
                WriteInt32(bytes, ptrOffset, 0);     // FileID
                WriteInt64(bytes, ptrOffset + 4, materialPathIds[i]);
                ptrOffset += 12;
            }
        }

        return bytes;
    }

    // Helper methods
    private static int Align4(int offset) => (offset + 3) & ~3;

    private static void AlignWriter(BinaryWriter bw, int alignment)
    {
        long pos = bw.BaseStream.Position;
        int padding = (int)((alignment - (pos % alignment)) % alignment);
        for (int i = 0; i < padding; i++)
            bw.Write((byte)0);
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        buffer[offset + 0] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteInt64(byte[] buffer, int offset, long value)
    {
        buffer[offset + 0] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 4] = (byte)((value >> 32) & 0xFF);
        buffer[offset + 5] = (byte)((value >> 40) & 0xFF);
        buffer[offset + 6] = (byte)((value >> 48) & 0xFF);
        buffer[offset + 7] = (byte)((value >> 56) & 0xFF);
    }
}
