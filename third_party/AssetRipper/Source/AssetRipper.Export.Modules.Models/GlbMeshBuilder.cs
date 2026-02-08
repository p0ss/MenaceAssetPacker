using AssetRipper.Export.Modules.Textures;
using AssetRipper.Numerics;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated.Classes.ClassID_21;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.SubMesh;
using AssetRipper.SourceGenerated.Subclasses.UnityTexEnv;
using SharpGLTF.Geometry;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;

namespace AssetRipper.Export.Modules.Models;

public static class GlbMeshBuilder
{
	/// <summary>
	/// Build a GLB scene from a mesh with default material (no textures).
	/// </summary>
	public static SceneBuilder Build(IMesh mesh)
	{
		return Build(mesh, null);
	}

	/// <summary>
	/// Build a GLB scene from a mesh with the specified material and its textures.
	/// </summary>
	public static SceneBuilder Build(IMesh mesh, IMaterial? unityMaterial)
	{
		SceneBuilder sceneBuilder = new();
		MaterialBuilder material = CreateMaterialBuilder(unityMaterial, mesh);

		AddMeshToScene(sceneBuilder, material, mesh);

		return sceneBuilder;
	}

	/// <summary>
	/// Build a GLB scene from a mesh with directly specified textures (synthetic material).
	/// Used when no Unity material is found but textures are discovered by name pattern.
	/// </summary>
	public static SceneBuilder Build(IMesh mesh, ITexture2D? baseColor, ITexture2D? normal, ITexture2D? mask)
	{
		SceneBuilder sceneBuilder = new();
		MaterialBuilder materialBuilder = new MaterialBuilder(mesh.Name + "_Material");

		// Convert and embed textures
		if (baseColor != null && TryConvertTexture(baseColor, out MemoryImage baseImage))
		{
			materialBuilder.WithBaseColor(baseImage);
		}

		if (normal != null && TryConvertTexture(normal, out MemoryImage normalImage))
		{
			materialBuilder.WithNormal(normalImage);
		}

		if (mask != null && TryConvertTexture(mask, out MemoryImage maskImage))
		{
			materialBuilder.WithMetallicRoughness(maskImage);
		}

		AddMeshToScene(sceneBuilder, materialBuilder, mesh);

		return sceneBuilder;
	}

	/// <summary>
	/// Create a MaterialBuilder from a Unity material, extracting textures if available.
	/// </summary>
	private static MaterialBuilder CreateMaterialBuilder(IMaterial? unityMaterial, IMesh mesh)
	{
		if (unityMaterial == null)
		{
			return new MaterialBuilder("DefaultMaterial");
		}

		string materialName = unityMaterial.Name;
		MaterialBuilder materialBuilder = new MaterialBuilder(materialName);

		// Extract textures from the Unity material
		ITexture2D? mainTexture = null;
		ITexture2D? normalTexture = null;
		ITexture2D? maskTexture = null;

		foreach ((Utf8String utf8Name, IUnityTexEnv textureParameter) in unityMaterial.GetTextureProperties())
		{
			string name = utf8Name.String;
			ITexture2D? texture = textureParameter.Texture.TryGetAsset(unityMaterial.Collection) as ITexture2D;

			if (texture == null)
				continue;

			if (IsMainTexture(name))
			{
				mainTexture ??= texture;
			}
			else if (IsNormalTexture(name))
			{
				normalTexture ??= texture;
			}
			else if (IsMaskTexture(name))
			{
				maskTexture ??= texture;
			}
			else if (mainTexture == null)
			{
				// Use any texture as fallback for main texture
				mainTexture = texture;
			}
		}

		// Convert and embed textures
		if (mainTexture != null && TryConvertTexture(mainTexture, out MemoryImage mainImage))
		{
			materialBuilder.WithBaseColor(mainImage);
		}

		if (normalTexture != null && TryConvertTexture(normalTexture, out MemoryImage normalImage))
		{
			materialBuilder.WithNormal(normalImage);
		}

		if (maskTexture != null && TryConvertTexture(maskTexture, out MemoryImage maskImage))
		{
			materialBuilder.WithMetallicRoughness(maskImage);
		}

		return materialBuilder;
	}

	private static bool TryConvertTexture(ITexture2D texture, out MemoryImage image)
	{
		try
		{
			if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
			{
				using MemoryStream memoryStream = new();
				bitmap.SaveAsPng(memoryStream);
				image = new MemoryImage(memoryStream.ToArray());
				return true;
			}
		}
		catch
		{
			// Ignore conversion errors
		}

		image = default;
		return false;
	}

	private static bool IsMainTexture(string textureName)
	{
		return textureName is "_MainTex" or "_BaseMap" or "_BaseColor" or "texture" or "Texture" or "_Texture";
	}

	private static bool IsNormalTexture(string textureName)
	{
		return textureName is "_Normal" or "_NormalMap" or "_BumpMap" or "Normal" or "normal";
	}

	private static bool IsMaskTexture(string textureName)
	{
		return textureName is "_MaskMap" or "_MetallicGlossMap" or "_OcclusionMap";
	}

	private static bool AddMeshToScene(SceneBuilder sceneBuilder, MaterialBuilder material, IMesh mesh)
	{
		if (MeshData.TryMakeFromMesh(mesh, out MeshData meshData))
		{
			NodeBuilder rootNodeForMesh = new NodeBuilder(mesh.Name);
			sceneBuilder.AddNode(rootNodeForMesh);

			(ISubMesh, MaterialBuilder)[] subMeshes = new (ISubMesh, MaterialBuilder)[1];

			for (int submeshIndex = 0; submeshIndex < mesh.SubMeshes.Count; submeshIndex++)
			{
				ISubMesh subMesh = mesh.SubMeshes[submeshIndex];
				subMeshes[0] = (subMesh, material);

				IMeshBuilder<MaterialBuilder> subMeshBuilder = GlbSubMeshBuilder.BuildSubMeshes(subMeshes, mesh.Is16BitIndices(), meshData, Transformation.Identity, Transformation.Identity);
				NodeBuilder subMeshNode = rootNodeForMesh.CreateNode($"SubMesh_{submeshIndex}");
				sceneBuilder.AddRigidMesh(subMeshBuilder, subMeshNode);
			}
			return true;
		}
		return false;
	}
}
