using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_137;
using AssetRipper.SourceGenerated.Classes.ClassID_21;
using AssetRipper.SourceGenerated.Classes.ClassID_25;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_33;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Extensions;
using System.Collections.Concurrent;

namespace AssetRipper.Export.PrimaryContent.Models;

/// <summary>
/// Builds and caches mesh → material/texture lookups using actual Unity asset references.
/// This uses the real metadata from MeshFilter/Renderer/SkinnedMeshRenderer components
/// rather than relying on naming conventions.
/// </summary>
public static class MeshMaterialLookup
{
	private static readonly ConcurrentDictionary<string, IMaterial?> MeshToMaterial = new();
	private static readonly ConcurrentDictionary<string, (ITexture2D? BaseColor, ITexture2D? Normal, ITexture2D? Mask)> MeshToTextures = new();
	private static bool _isBuilt = false;

	/// <summary>
	/// Build the lookup tables from a GameBundle. Should be called once before export.
	/// </summary>
	public static void Build(Bundle bundle)
	{
		if (_isBuilt)
			return;

		Bundle root = bundle.GetRoot();
		int meshCount = 0;
		int materialCount = 0;
		int textureCount = 0;

		foreach (IUnityObjectBase asset in root.FetchAssets())
		{
			// Handle MeshFilter → MeshRenderer combo
			if (asset is IMeshFilter meshFilter)
			{
				if (meshFilter.TryGetMesh(out IMesh? mesh) && mesh != null)
				{
					string meshName = mesh.Name;
					if (!MeshToMaterial.ContainsKey(meshName))
					{
						IGameObject? gameObject = meshFilter.GameObject_C2P as IGameObject;
						if (gameObject != null)
						{
							IMaterial? material = FindMaterialOnGameObject(gameObject);
							if (material != null)
							{
								MeshToMaterial[meshName] = material;
								materialCount++;

								// Also extract textures from the material
								var textures = ExtractTexturesFromMaterial(material);
								if (textures.BaseColor != null)
								{
									MeshToTextures[meshName] = textures;
									textureCount++;
								}
							}
						}
					}
					meshCount++;
				}
			}

			// Handle SkinnedMeshRenderer directly
			if (asset is ISkinnedMeshRenderer skinnedRenderer)
			{
				IMesh? mesh = skinnedRenderer.MeshP;
				if (mesh != null)
				{
					string meshName = mesh.Name;
					if (!MeshToMaterial.ContainsKey(meshName))
					{
						var materials = skinnedRenderer.Materials_C25P;
						IMaterial? material = materials.FirstOrDefault();
						if (material != null)
						{
							MeshToMaterial[meshName] = material;
							materialCount++;

							var textures = ExtractTexturesFromMaterial(material);
							if (textures.BaseColor != null)
							{
								MeshToTextures[meshName] = textures;
								textureCount++;
							}
						}
					}
					meshCount++;
				}
			}
		}

		_isBuilt = true;
		Logger.Info(LogCategory.Export, $"MeshMaterialLookup: Built lookup for {meshCount} meshes, found {materialCount} materials, {textureCount} texture sets");
	}

	/// <summary>
	/// Get the material for a mesh by name.
	/// </summary>
	public static IMaterial? GetMaterial(string meshName)
	{
		MeshToMaterial.TryGetValue(meshName, out IMaterial? material);
		return material;
	}

	/// <summary>
	/// Get textures for a mesh by name.
	/// </summary>
	public static (ITexture2D? BaseColor, ITexture2D? Normal, ITexture2D? Mask)? GetTextures(string meshName)
	{
		if (MeshToTextures.TryGetValue(meshName, out var textures))
			return textures;
		return null;
	}

	/// <summary>
	/// Clear the lookup tables (for reuse across multiple exports).
	/// </summary>
	public static void Clear()
	{
		MeshToMaterial.Clear();
		MeshToTextures.Clear();
		_isBuilt = false;
	}

	private static IMaterial? FindMaterialOnGameObject(IGameObject gameObject)
	{
		foreach (var component in gameObject.FetchComponents())
		{
			if (component is IRenderer renderer)
			{
				var materials = renderer.Materials_C25P;
				if (materials.Any())
				{
					return materials.FirstOrDefault();
				}
			}
		}
		return null;
	}

	private static (ITexture2D? BaseColor, ITexture2D? Normal, ITexture2D? Mask) ExtractTexturesFromMaterial(IMaterial material)
	{
		ITexture2D? baseColor = null;
		ITexture2D? normal = null;
		ITexture2D? mask = null;

		foreach (var (utf8Name, textureParameter) in material.GetTextureProperties())
		{
			string name = utf8Name.String;
			ITexture2D? texture = textureParameter.Texture.TryGetAsset(material.Collection) as ITexture2D;

			if (texture == null)
				continue;

			if (IsMainTexture(name))
			{
				baseColor ??= texture;
			}
			else if (IsNormalTexture(name))
			{
				normal ??= texture;
			}
			else if (IsMaskTexture(name))
			{
				mask ??= texture;
			}
			else if (baseColor == null)
			{
				// Use any texture as fallback for base color
				baseColor = texture;
			}
		}

		return (baseColor, normal, mask);
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
}
