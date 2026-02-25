using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Creates native Mesh assets from GLB/GLTF extracted mesh data.
/// Handles vertex buffer packing with proper interleaving, channel descriptors,
/// index buffer type selection (uint16 vs uint32), and SubMesh byte offsets.
/// These assets load naturally via AssetBundle.LoadFromFile().
/// </summary>
public class MeshAssetCreator
{
    /// <summary>
    /// Result of mesh asset creation.
    /// </summary>
    public class MeshCreationResult
    {
        public bool Success { get; set; }
        public long PathId { get; set; }
        public string? ErrorMessage { get; set; }
        public int VertexCount { get; set; }
        public int IndexCount { get; set; }
        public int SubMeshCount { get; set; }
        public int VertexBufferSize { get; set; }
        public int IndexBufferSize { get; set; }
        public int Stride { get; set; }
        public bool Uses16BitIndices { get; set; }
    }

    /// <summary>
    /// Vertex channel format constants (Unity's VertexAttributeFormat enum).
    /// </summary>
    private static class ChannelFormat
    {
        public const byte Float32 = 0;    // 4 bytes per component
        public const byte Float16 = 1;    // 2 bytes per component
        public const byte UNorm8 = 2;     // 1 byte, normalized 0-1
        public const byte SNorm8 = 3;     // 1 byte, normalized -1 to 1
        public const byte UNorm16 = 4;    // 2 bytes, normalized 0-1
        public const byte SNorm16 = 5;    // 2 bytes, normalized -1 to 1
        public const byte UInt8 = 6;      // 1 byte unsigned
        public const byte SInt8 = 7;      // 1 byte signed
        public const byte UInt16 = 8;     // 2 bytes unsigned
        public const byte SInt16 = 9;     // 2 bytes signed
        public const byte UInt32 = 10;    // 4 bytes unsigned
        public const byte SInt32 = 11;    // 4 bytes signed
    }

    /// <summary>
    /// Vertex channel indices (Unity's VertexAttribute enum).
    /// </summary>
    private static class ChannelIndex
    {
        public const int Position = 0;
        public const int Normal = 1;
        public const int Tangent = 2;
        public const int Color = 3;
        public const int TexCoord0 = 4;
        public const int TexCoord1 = 5;
        public const int TexCoord2 = 6;
        public const int TexCoord3 = 7;
        public const int BlendWeight = 8;   // Bone weights
        public const int BlendIndices = 9;  // Bone indices
        public const int ChannelCount = 14; // Total channel slots
    }

    /// <summary>
    /// Create a native Mesh asset from extracted GLB mesh data.
    /// </summary>
    /// <param name="am">The AssetsManager instance.</param>
    /// <param name="afileInst">The assets file instance to add the mesh to.</param>
    /// <param name="extractedMesh">The extracted mesh data from GlbBundler.</param>
    /// <param name="pathId">PathID to assign to the new asset.</param>
    /// <returns>Result containing success status and asset info.</returns>
    public static MeshCreationResult CreateMesh(
        AssetsManager am,
        AssetsFileInstance afileInst,
        GlbBundler.ExtractedMesh extractedMesh,
        long pathId)
    {
        var result = new MeshCreationResult { PathId = pathId };

        try
        {
            var afile = afileInst.file;

            // Calculate vertex count from position data (float3)
            int vertexCount = extractedMesh.Vertices.Length / 3;
            result.VertexCount = vertexCount;
            result.IndexCount = extractedMesh.Indices.Length;
            result.SubMeshCount = extractedMesh.SubMeshes.Count;

            if (vertexCount == 0)
            {
                result.ErrorMessage = "Mesh has no vertices";
                return result;
            }

            // Determine which channels are present
            bool hasNormals = extractedMesh.Normals.Length == vertexCount * 3;
            bool hasTangents = extractedMesh.Tangents.Length == vertexCount * 4;
            bool hasColors = extractedMesh.Colors.Length == vertexCount * 4;
            bool hasUV0 = extractedMesh.UV0.Length == vertexCount * 2;
            bool hasUV1 = extractedMesh.UV1.Length == vertexCount * 2;
            bool hasSkinning = extractedMesh.HasSkinning &&
                               extractedMesh.BoneWeights.Length == vertexCount * 4 &&
                               extractedMesh.BoneIndices.Length == vertexCount * 4;

            // Calculate stride based on present channels
            int stride = CalculateStride(hasNormals, hasTangents, hasColors, hasUV0, hasUV1, hasSkinning);
            result.Stride = stride;

            // Pack vertex data into interleaved buffer
            byte[] vertexBuffer = PackVertexBuffer(
                extractedMesh, vertexCount, stride,
                hasNormals, hasTangents, hasColors, hasUV0, hasUV1, hasSkinning);
            result.VertexBufferSize = vertexBuffer.Length;

            // Generate channel descriptors
            var channels = GenerateChannelDescriptors(
                hasNormals, hasTangents, hasColors, hasUV0, hasUV1, hasSkinning);

            // Determine index buffer format and pack indices
            bool use16Bit = vertexCount <= 65535;
            result.Uses16BitIndices = use16Bit;
            byte[] indexBuffer = PackIndexBuffer(extractedMesh.Indices, use16Bit);
            result.IndexBufferSize = indexBuffer.Length;

            // Find an existing Mesh to use as template and deep copy it
            var templateField = GetMeshTemplate(am, afileInst);
            if (templateField == null)
            {
                result.ErrorMessage = "No existing Mesh found to use as template";
                return result;
            }

            var meshField = DeepCopyField(templateField);
            if (meshField == null)
            {
                result.ErrorMessage = "Failed to copy Mesh template";
                return result;
            }

            // Set basic mesh properties
            SetField(meshField, "m_Name", extractedMesh.Name);

            // SubMeshes - critical: use BYTE offsets, not index offsets!
            SetSubMeshes(meshField, extractedMesh.SubMeshes, use16Bit);

            // Blend shapes (none for now)
            ClearArrayField(meshField, "m_Shapes.vertices");
            ClearArrayField(meshField, "m_Shapes.shapes");
            ClearArrayField(meshField, "m_Shapes.channels");
            ClearArrayField(meshField, "m_Shapes.fullWeights");

            // Bind poses for skinned meshes
            if (hasSkinning && extractedMesh.BindPoses.Length > 0)
            {
                SetBindPoses(meshField, extractedMesh.BindPoses);
            }
            else
            {
                ClearArrayField(meshField, "m_BindPose");
            }

            // Bone name hashes (empty for now - runtime can resolve)
            ClearArrayField(meshField, "m_BoneNameHashes");
            SetField(meshField, "m_RootBoneNameHash", 0U);

            // Bones AABB (empty)
            ClearArrayField(meshField, "m_BonesAABB");

            // Variable bone count per vertex (empty)
            ClearArrayField(meshField, "m_VariableBoneCountWeights.m_Data");

            // Mesh compression (none)
            SetField(meshField, "m_MeshCompression", (byte)0);
            SetField(meshField, "m_IsReadable", true);
            SetField(meshField, "m_KeepVertices", true);
            SetField(meshField, "m_KeepIndices", true);

            // Index format: 0 = UInt16, 1 = UInt32
            SetField(meshField, "m_IndexFormat", use16Bit ? 0 : 1);

            // Index buffer
            SetField(meshField, "m_IndexBuffer", indexBuffer);

            // Vertex data
            SetVertexData(meshField, vertexBuffer, vertexCount, channels, stride);

            // Calculate and set bounds
            SetMeshBounds(meshField, extractedMesh.Vertices);

            // Mesh metrics (calculate approximate values)
            SetField(meshField, "m_MeshMetrics[0]", 1.0f);  // Average edge length ratio
            SetField(meshField, "m_MeshMetrics[1]", 1.0f);  // Average aspect ratio

            // Streaming info (not streaming)
            SetNestedField(meshField, "m_StreamData", "offset", 0UL);
            SetNestedField(meshField, "m_StreamData", "size", 0U);
            SetNestedField(meshField, "m_StreamData", "path", "");

            // Create the asset info
            var info = AssetFileInfo.Create(
                afile,
                pathId,
                (int)AssetClassID.Mesh,
                0);

            // Unity 6 files without type trees return null from Create
            if (info == null)
            {
                result.ErrorMessage = "AssetFileInfo.Create returned null - Unity 6 without type trees may not support mesh creation via TypeTree";
                return result;
            }

            info.SetNewData(meshField);
            afile.Metadata.AddAssetInfo(info);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to create mesh: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Calculate the vertex stride based on which channels are present.
    /// </summary>
    private static int CalculateStride(
        bool hasNormals, bool hasTangents, bool hasColors,
        bool hasUV0, bool hasUV1, bool hasSkinning)
    {
        int stride = 12; // Position (float3) always present

        if (hasNormals) stride += 12;  // Normal (float3)
        if (hasTangents) stride += 16; // Tangent (float4)
        if (hasColors) stride += 4;    // Color (Color32 = 4 bytes)
        if (hasUV0) stride += 8;       // TexCoord0 (float2)
        if (hasUV1) stride += 8;       // TexCoord1 (float2)

        // Skinning data is stored separately in Unity, not interleaved
        // So we don't add it to stride

        return stride;
    }

    /// <summary>
    /// Pack vertex data into an interleaved vertex buffer.
    /// </summary>
    private static byte[] PackVertexBuffer(
        GlbBundler.ExtractedMesh mesh,
        int vertexCount,
        int stride,
        bool hasNormals, bool hasTangents, bool hasColors,
        bool hasUV0, bool hasUV1, bool hasSkinning)
    {
        byte[] buffer = new byte[stride * vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            int vertexOffset = i * stride;
            int channelOffset = 0;

            // Position (float3) - always at offset 0
            WriteFloat3(buffer, vertexOffset + channelOffset,
                mesh.Vertices[i * 3 + 0],
                mesh.Vertices[i * 3 + 1],
                mesh.Vertices[i * 3 + 2]);
            channelOffset += 12;

            // Normal (float3)
            if (hasNormals)
            {
                WriteFloat3(buffer, vertexOffset + channelOffset,
                    mesh.Normals[i * 3 + 0],
                    mesh.Normals[i * 3 + 1],
                    mesh.Normals[i * 3 + 2]);
                channelOffset += 12;
            }

            // Tangent (float4)
            if (hasTangents)
            {
                WriteFloat4(buffer, vertexOffset + channelOffset,
                    mesh.Tangents[i * 4 + 0],
                    mesh.Tangents[i * 4 + 1],
                    mesh.Tangents[i * 4 + 2],
                    mesh.Tangents[i * 4 + 3]);
                channelOffset += 16;
            }

            // Color (Color32 = RGBA bytes)
            if (hasColors)
            {
                WriteColor32(buffer, vertexOffset + channelOffset,
                    mesh.Colors[i * 4 + 0],  // R
                    mesh.Colors[i * 4 + 1],  // G
                    mesh.Colors[i * 4 + 2],  // B
                    mesh.Colors[i * 4 + 3]); // A
                channelOffset += 4;
            }

            // TexCoord0 (float2)
            if (hasUV0)
            {
                WriteFloat2(buffer, vertexOffset + channelOffset,
                    mesh.UV0[i * 2 + 0],
                    mesh.UV0[i * 2 + 1]);
                channelOffset += 8;
            }

            // TexCoord1 (float2)
            if (hasUV1)
            {
                WriteFloat2(buffer, vertexOffset + channelOffset,
                    mesh.UV1[i * 2 + 0],
                    mesh.UV1[i * 2 + 1]);
                channelOffset += 8;
            }
        }

        return buffer;
    }

    /// <summary>
    /// Generate channel descriptors for the vertex data.
    /// Each channel descriptor is 4 bytes: stream, offset, format, dimension.
    /// </summary>
    private static List<ChannelDescriptor> GenerateChannelDescriptors(
        bool hasNormals, bool hasTangents, bool hasColors,
        bool hasUV0, bool hasUV1, bool hasSkinning)
    {
        var channels = new List<ChannelDescriptor>();
        byte offset = 0;

        // Channel 0: Position (always present)
        channels.Add(new ChannelDescriptor
        {
            Channel = ChannelIndex.Position,
            Stream = 0,
            Offset = offset,
            Format = ChannelFormat.Float32,
            Dimension = 3
        });
        offset += 12;

        // Channel 1: Normal
        if (hasNormals)
        {
            channels.Add(new ChannelDescriptor
            {
                Channel = ChannelIndex.Normal,
                Stream = 0,
                Offset = offset,
                Format = ChannelFormat.Float32,
                Dimension = 3
            });
            offset += 12;
        }

        // Channel 2: Tangent
        if (hasTangents)
        {
            channels.Add(new ChannelDescriptor
            {
                Channel = ChannelIndex.Tangent,
                Stream = 0,
                Offset = offset,
                Format = ChannelFormat.Float32,
                Dimension = 4
            });
            offset += 16;
        }

        // Channel 3: Color
        if (hasColors)
        {
            channels.Add(new ChannelDescriptor
            {
                Channel = ChannelIndex.Color,
                Stream = 0,
                Offset = offset,
                Format = ChannelFormat.UNorm8, // Color32 is 4 normalized bytes
                Dimension = 4
            });
            offset += 4;
        }

        // Channel 4: TexCoord0
        if (hasUV0)
        {
            channels.Add(new ChannelDescriptor
            {
                Channel = ChannelIndex.TexCoord0,
                Stream = 0,
                Offset = offset,
                Format = ChannelFormat.Float32,
                Dimension = 2
            });
            offset += 8;
        }

        // Channel 5: TexCoord1
        if (hasUV1)
        {
            channels.Add(new ChannelDescriptor
            {
                Channel = ChannelIndex.TexCoord1,
                Stream = 0,
                Offset = offset,
                Format = ChannelFormat.Float32,
                Dimension = 2
            });
            offset += 8;
        }

        // Note: Skinning data (BlendWeight, BlendIndices) is stored in a separate
        // stream/buffer in Unity, not interleaved with vertex data.
        // We'd need a second vertex stream for that.

        return channels;
    }

    /// <summary>
    /// Pack index buffer as either uint16 or uint32 array.
    /// </summary>
    private static byte[] PackIndexBuffer(int[] indices, bool use16Bit)
    {
        if (use16Bit)
        {
            var buffer = new byte[indices.Length * 2];
            for (int i = 0; i < indices.Length; i++)
            {
                ushort index = (ushort)indices[i];
                buffer[i * 2 + 0] = (byte)(index & 0xFF);
                buffer[i * 2 + 1] = (byte)((index >> 8) & 0xFF);
            }
            return buffer;
        }
        else
        {
            var buffer = new byte[indices.Length * 4];
            for (int i = 0; i < indices.Length; i++)
            {
                uint index = (uint)indices[i];
                buffer[i * 4 + 0] = (byte)(index & 0xFF);
                buffer[i * 4 + 1] = (byte)((index >> 8) & 0xFF);
                buffer[i * 4 + 2] = (byte)((index >> 16) & 0xFF);
                buffer[i * 4 + 3] = (byte)((index >> 24) & 0xFF);
            }
            return buffer;
        }
    }

    /// <summary>
    /// Set the SubMeshes array with proper byte offsets (NOT index offsets).
    /// </summary>
    private static void SetSubMeshes(
        AssetTypeValueField meshField,
        List<GlbBundler.SubMeshInfo> subMeshes,
        bool use16Bit)
    {
        var subMeshArray = meshField["m_SubMeshes"];
        if (subMeshArray == null || subMeshArray.IsDummy) return;

        // Clear existing children
        subMeshArray.Children?.Clear();
        if (subMeshArray.Children == null)
            subMeshArray.Children = new List<AssetTypeValueField>();

        int bytesPerIndex = use16Bit ? 2 : 4;

        foreach (var sm in subMeshes)
        {
            // Calculate byte offset (CRITICAL: Unity expects bytes, not indices!)
            uint firstByte = (uint)(sm.IndexStart * bytesPerIndex);

            var subMeshItem = CreateSubMeshField(subMeshArray, firstByte, (uint)sm.IndexCount);
            subMeshArray.Children.Add(subMeshItem);
        }
    }

    /// <summary>
    /// Create a SubMesh field entry.
    /// </summary>
    private static AssetTypeValueField CreateSubMeshField(
        AssetTypeValueField arrayField,
        uint firstByte,
        uint indexCount)
    {
        // Try to get template from array
        AssetTypeValueField? template = null;
        if (arrayField.Children != null && arrayField.Children.Count > 0)
        {
            template = arrayField.Children[0];
        }

        AssetTypeValueField subMesh;
        if (template != null)
        {
            subMesh = DeepCopyField(template);
        }
        else
        {
            // Create minimal structure if no template
            subMesh = new AssetTypeValueField
            {
                Children = new List<AssetTypeValueField>()
            };
        }

        // Set SubMesh fields
        SetField(subMesh, "firstByte", firstByte);
        SetField(subMesh, "indexCount", indexCount);
        SetField(subMesh, "topology", 0U); // 0 = Triangles
        SetField(subMesh, "baseVertex", 0U);
        SetField(subMesh, "firstVertex", 0U);
        SetField(subMesh, "vertexCount", 0U); // Will be calculated by Unity

        // Bounding box (local bounds, will be recalculated)
        SetNestedField(subMesh, "localAABB.m_Center", "x", 0f);
        SetNestedField(subMesh, "localAABB.m_Center", "y", 0f);
        SetNestedField(subMesh, "localAABB.m_Center", "z", 0f);
        SetNestedField(subMesh, "localAABB.m_Extent", "x", 1f);
        SetNestedField(subMesh, "localAABB.m_Extent", "y", 1f);
        SetNestedField(subMesh, "localAABB.m_Extent", "z", 1f);

        return subMesh;
    }

    /// <summary>
    /// Set the vertex data fields.
    /// </summary>
    private static void SetVertexData(
        AssetTypeValueField meshField,
        byte[] vertexBuffer,
        int vertexCount,
        List<ChannelDescriptor> channels,
        int stride)
    {
        var vertexData = meshField["m_VertexData"];
        if (vertexData == null || vertexData.IsDummy) return;

        // Vertex count
        SetField(vertexData, "m_VertexCount", (uint)vertexCount);

        // Channel array - Unity expects 14 channels (even if not all used)
        var channelArray = vertexData["m_Channels"];
        if (channelArray != null && !channelArray.IsDummy)
        {
            // Build the full 14-channel array, with unused channels having stream=255
            SetChannelArray(channelArray, channels);
        }

        // Data size
        SetField(vertexData, "m_DataSize", (uint)vertexBuffer.Length);

        // Vertex data bytes
        SetField(vertexData, "m_Data", vertexBuffer);
    }

    /// <summary>
    /// Set the channel array with proper descriptors.
    /// Unity expects exactly 14 channel slots.
    /// </summary>
    private static void SetChannelArray(
        AssetTypeValueField channelArray,
        List<ChannelDescriptor> activeChannels)
    {
        // Clear and rebuild
        channelArray.Children?.Clear();
        if (channelArray.Children == null)
            channelArray.Children = new List<AssetTypeValueField>();

        // Build lookup of active channels
        var channelByIndex = new Dictionary<int, ChannelDescriptor>();
        foreach (var ch in activeChannels)
        {
            channelByIndex[ch.Channel] = ch;
        }

        // Create all 14 channel slots
        for (int i = 0; i < ChannelIndex.ChannelCount; i++)
        {
            AssetTypeValueField channelField;

            if (channelByIndex.TryGetValue(i, out var active))
            {
                // Active channel
                channelField = CreateChannelField(
                    channelArray,
                    active.Stream,
                    active.Offset,
                    active.Format,
                    active.Dimension);
            }
            else
            {
                // Inactive channel (stream = 0, all zeros, or stream = 255 depending on Unity version)
                // Using stream=0, offset=0, format=0, dim=0 for unused
                channelField = CreateChannelField(channelArray, 0, 0, 0, 0);
            }

            channelArray.Children.Add(channelField);
        }
    }

    /// <summary>
    /// Create a channel descriptor field.
    /// </summary>
    private static AssetTypeValueField CreateChannelField(
        AssetTypeValueField arrayField,
        byte stream, byte offset, byte format, byte dimension)
    {
        // Try to get template from array
        AssetTypeValueField? template = null;
        if (arrayField.Children != null && arrayField.Children.Count > 0)
        {
            template = arrayField.Children[0];
        }

        AssetTypeValueField channelField;
        if (template != null)
        {
            channelField = DeepCopyField(template);
        }
        else
        {
            channelField = new AssetTypeValueField
            {
                Children = new List<AssetTypeValueField>()
            };
        }

        SetField(channelField, "stream", stream);
        SetField(channelField, "offset", offset);
        SetField(channelField, "format", format);
        SetField(channelField, "dimension", dimension);

        return channelField;
    }

    /// <summary>
    /// Set bind poses for skinned meshes.
    /// </summary>
    private static void SetBindPoses(
        AssetTypeValueField meshField,
        System.Numerics.Matrix4x4[] bindPoses)
    {
        var bindPoseArray = meshField["m_BindPose"];
        if (bindPoseArray == null || bindPoseArray.IsDummy) return;

        bindPoseArray.Children?.Clear();
        if (bindPoseArray.Children == null)
            bindPoseArray.Children = new List<AssetTypeValueField>();

        foreach (var pose in bindPoses)
        {
            var poseField = CreateMatrix4x4Field(bindPoseArray, pose);
            bindPoseArray.Children.Add(poseField);
        }
    }

    /// <summary>
    /// Create a Matrix4x4 field from System.Numerics.Matrix4x4.
    /// </summary>
    private static AssetTypeValueField CreateMatrix4x4Field(
        AssetTypeValueField arrayField,
        System.Numerics.Matrix4x4 matrix)
    {
        // Try to get template
        AssetTypeValueField? template = null;
        if (arrayField.Children != null && arrayField.Children.Count > 0)
        {
            template = arrayField.Children[0];
        }

        AssetTypeValueField matrixField;
        if (template != null)
        {
            matrixField = DeepCopyField(template);
        }
        else
        {
            matrixField = new AssetTypeValueField
            {
                Children = new List<AssetTypeValueField>()
            };
        }

        // Unity uses column-major matrices, System.Numerics uses row-major
        // So we need to transpose
        SetField(matrixField, "e00", matrix.M11);
        SetField(matrixField, "e01", matrix.M21);
        SetField(matrixField, "e02", matrix.M31);
        SetField(matrixField, "e03", matrix.M41);
        SetField(matrixField, "e10", matrix.M12);
        SetField(matrixField, "e11", matrix.M22);
        SetField(matrixField, "e12", matrix.M32);
        SetField(matrixField, "e13", matrix.M42);
        SetField(matrixField, "e20", matrix.M13);
        SetField(matrixField, "e21", matrix.M23);
        SetField(matrixField, "e22", matrix.M33);
        SetField(matrixField, "e23", matrix.M43);
        SetField(matrixField, "e30", matrix.M14);
        SetField(matrixField, "e31", matrix.M24);
        SetField(matrixField, "e32", matrix.M34);
        SetField(matrixField, "e33", matrix.M44);

        return matrixField;
    }

    /// <summary>
    /// Calculate and set mesh bounds from vertex positions.
    /// </summary>
    private static void SetMeshBounds(AssetTypeValueField meshField, float[] vertices)
    {
        if (vertices.Length < 3) return;

        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        for (int i = 0; i < vertices.Length; i += 3)
        {
            float x = vertices[i + 0];
            float y = vertices[i + 1];
            float z = vertices[i + 2];

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);
        }

        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;
        float centerZ = (minZ + maxZ) / 2f;
        float extentX = (maxX - minX) / 2f;
        float extentY = (maxY - minY) / 2f;
        float extentZ = (maxZ - minZ) / 2f;

        // Set local bounds
        SetNestedField(meshField, "m_LocalAABB.m_Center", "x", centerX);
        SetNestedField(meshField, "m_LocalAABB.m_Center", "y", centerY);
        SetNestedField(meshField, "m_LocalAABB.m_Center", "z", centerZ);
        SetNestedField(meshField, "m_LocalAABB.m_Extent", "x", extentX);
        SetNestedField(meshField, "m_LocalAABB.m_Extent", "y", extentY);
        SetNestedField(meshField, "m_LocalAABB.m_Extent", "z", extentZ);
    }

    /// <summary>
    /// Clear an array field.
    /// </summary>
    private static void ClearArrayField(AssetTypeValueField root, string path)
    {
        var parts = path.Split('.');
        var current = root;

        foreach (var part in parts)
        {
            if (current == null || current.IsDummy) return;
            current = current[part];
        }

        current?.Children?.Clear();
    }

    /// <summary>
    /// Find an existing Mesh and get its base field to use as template.
    /// </summary>
    private static AssetTypeValueField? GetMeshTemplate(
        AssetsManager am,
        AssetsFileInstance afileInst)
    {
        foreach (var info in afileInst.file.GetAssetsOfType(AssetClassID.Mesh))
        {
            try
            {
                var baseField = am.GetBaseField(afileInst, info);
                if (baseField != null)
                    return baseField;
            }
            catch
            {
                // Try next one
            }
        }

        return null;
    }

    /// <summary>
    /// Create a deep copy of an AssetTypeValueField, preserving all type information.
    /// </summary>
    private static AssetTypeValueField DeepCopyField(AssetTypeValueField source)
    {
        if (source == null) return null!;

        var copy = new AssetTypeValueField
        {
            TemplateField = source.TemplateField
        };

        if (source.Value != null)
        {
            var valueType = source.Value.ValueType;
            object? rawValue = valueType switch
            {
                AssetValueType.Bool => source.Value.AsBool,
                AssetValueType.Int8 => source.Value.AsInt,
                AssetValueType.UInt8 => source.Value.AsUInt,
                AssetValueType.Int16 => source.Value.AsInt,
                AssetValueType.UInt16 => source.Value.AsUInt,
                AssetValueType.Int32 => source.Value.AsInt,
                AssetValueType.UInt32 => source.Value.AsUInt,
                AssetValueType.Int64 => source.Value.AsLong,
                AssetValueType.UInt64 => source.Value.AsULong,
                AssetValueType.Float => source.Value.AsFloat,
                AssetValueType.Double => source.Value.AsDouble,
                AssetValueType.String => source.Value.AsString,
                AssetValueType.ByteArray => source.Value.AsByteArray?.ToArray(),
                _ => source.Value.AsObject
            };
            copy.Value = new AssetTypeValue(valueType, rawValue);
        }

        if (source.Children != null && source.Children.Count > 0)
        {
            copy.Children = new List<AssetTypeValueField>(source.Children.Count);
            foreach (var child in source.Children)
            {
                copy.Children.Add(DeepCopyField(child));
            }
        }

        return copy;
    }

    private static void SetField(AssetTypeValueField root, string fieldName, object value)
    {
        var field = root[fieldName];
        if (field == null || field.IsDummy) return;

        switch (value)
        {
            case string s:
                field.AsString = s;
                break;
            case int i:
                field.AsInt = i;
                break;
            case uint u:
                field.AsUInt = u;
                break;
            case long l:
                field.AsLong = l;
                break;
            case ulong ul:
                field.AsULong = ul;
                break;
            case float f:
                field.AsFloat = f;
                break;
            case bool b:
                field.AsBool = b;
                break;
            case byte by:
                field.AsInt = by;
                break;
            case byte[] bytes:
                field.AsByteArray = bytes;
                break;
        }
    }

    private static void SetNestedField(AssetTypeValueField root, string path, string fieldName, object value)
    {
        var parts = path.Split('.');
        var current = root;

        foreach (var part in parts)
        {
            if (current == null || current.IsDummy) return;
            current = current[part];
        }

        if (current != null && !current.IsDummy)
        {
            SetField(current, fieldName, value);
        }
    }

    // Binary writing helpers
    private static void WriteFloat3(byte[] buffer, int offset, float x, float y, float z)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(x), 0, buffer, offset + 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(y), 0, buffer, offset + 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(z), 0, buffer, offset + 8, 4);
    }

    private static void WriteFloat4(byte[] buffer, int offset, float x, float y, float z, float w)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(x), 0, buffer, offset + 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(y), 0, buffer, offset + 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(z), 0, buffer, offset + 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(w), 0, buffer, offset + 12, 4);
    }

    private static void WriteFloat2(byte[] buffer, int offset, float x, float y)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(x), 0, buffer, offset + 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(y), 0, buffer, offset + 4, 4);
    }

    private static void WriteColor32(byte[] buffer, int offset, float r, float g, float b, float a)
    {
        // Convert from 0-1 float to 0-255 byte
        buffer[offset + 0] = (byte)Math.Clamp((int)(r * 255f), 0, 255);
        buffer[offset + 1] = (byte)Math.Clamp((int)(g * 255f), 0, 255);
        buffer[offset + 2] = (byte)Math.Clamp((int)(b * 255f), 0, 255);
        buffer[offset + 3] = (byte)Math.Clamp((int)(a * 255f), 0, 255);
    }

    /// <summary>
    /// Channel descriptor data structure.
    /// </summary>
    private class ChannelDescriptor
    {
        public int Channel { get; set; }
        public byte Stream { get; set; }
        public byte Offset { get; set; }
        public byte Format { get; set; }
        public byte Dimension { get; set; }
    }
}
