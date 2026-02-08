using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Export.Modules.Models;
using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_137;
using AssetRipper.SourceGenerated.Classes.ClassID_21;
using AssetRipper.SourceGenerated.Classes.ClassID_25;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_33;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Extensions;
using SharpGLTF.Scenes;
using System.Text.RegularExpressions;

namespace AssetRipper.Export.PrimaryContent.Models;

public sealed class GlbMeshExporter : IContentExtractor
{
	// Common suffixes to strip when matching names
	private static readonly Regex LodSuffixPattern = new(@"_LOD\d+(_\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly string[] PartSuffixes = { "_chassis", "_chasis", "_turret", "_barrel", "_body", "_head", "_arm", "_leg", "_wheel", "_track", "_pack", "_mesh", "_ready", "_tripod", "_camera" };

	public bool TryCreateCollection(IUnityObjectBase asset, [NotNullWhen(true)] out ExportCollectionBase? exportCollection)
	{
		if (asset is IMesh mesh && mesh.IsSet())
		{
			exportCollection = new GlbExportCollection(this, asset);
			return true;
		}
		else
		{
			exportCollection = null;
			return false;
		}
	}

	public bool Export(IUnityObjectBase asset, string path, FileSystem fileSystem)
	{
		IMesh mesh = (IMesh)asset;

		// Try multiple strategies to find material/textures for this mesh
		var result = FindMaterialOrTextures(mesh);

		SceneBuilder sceneBuilder = result.Textures != null
			? GlbMeshBuilder.Build(mesh, result.Textures.Value.BaseColor, result.Textures.Value.Normal, result.Textures.Value.Mask)
			: GlbMeshBuilder.Build(mesh, result.Material);

		using Stream fileStream = fileSystem.File.Create(path);
		if (GlbWriter.TryWrite(sceneBuilder, fileStream, out string? errorMessage))
		{
			return true;
		}
		else
		{
			Logger.Error(LogCategory.Export, errorMessage);
			return false;
		}
	}

	private readonly record struct MaterialOrTextures(
		IMaterial? Material,
		(ITexture2D? BaseColor, ITexture2D? Normal, ITexture2D? Mask)? Textures);

	/// <summary>
	/// Multi-strategy approach to find material or textures for a mesh.
	/// </summary>
	private static MaterialOrTextures FindMaterialOrTextures(IMesh mesh)
	{
		// Strategy 0: Use pre-built lookup (uses actual Unity asset references)
		var cachedMaterial = MeshMaterialLookup.GetMaterial(mesh.Name);
		if (cachedMaterial != null)
		{
			Logger.Info(LogCategory.Export, $"[Lookup] Found material '{cachedMaterial.Name}' for mesh '{mesh.Name}'");
			return new(cachedMaterial, null);
		}

		var cachedTextures = MeshMaterialLookup.GetTextures(mesh.Name);
		if (cachedTextures != null && cachedTextures.Value.BaseColor != null)
		{
			Logger.Info(LogCategory.Export, $"[Lookup] Found textures for mesh '{mesh.Name}' (base: {cachedTextures.Value.BaseColor.Name})");
			return new(null, cachedTextures);
		}

		Bundle? bundle = mesh.Collection?.Bundle;
		if (bundle == null)
			return new(null, null);

		Bundle root = bundle.GetRoot();

		// Strategy 1: Direct reference lookup (exact mesh name match) - fallback if lookup missed it
		var material = FindMaterialByDirectReference(root, mesh.Name);
		if (material != null)
		{
			Logger.Info(LogCategory.Export, $"[Direct] Found material '{material.Name}' for mesh '{mesh.Name}'");
			return new(material, null);
		}

		// Strategy 2: Fuzzy name matching on materials
		string meshBaseName = GetBaseName(mesh.Name);
		material = FindMaterialByFuzzyName(root, meshBaseName);
		if (material != null)
		{
			Logger.Info(LogCategory.Export, $"[Fuzzy] Found material '{material.Name}' for mesh '{mesh.Name}'");
			return new(material, null);
		}

		// Strategy 3: Find textures directly by name pattern (synthetic material)
		var textures = FindTexturesByNamePattern(root, meshBaseName);
		if (textures.BaseColor != null)
		{
			Logger.Info(LogCategory.Export, $"[Texture] Found textures for mesh '{mesh.Name}' (base: {textures.BaseColor.Name})");
			return new(null, textures);
		}

		Logger.Warning(LogCategory.Export, $"No material or textures found for mesh '{mesh.Name}' (base: {meshBaseName})");
		return new(null, null);
	}

	/// <summary>
	/// Strategy 1: Find material via direct mesh reference in MeshFilter/SkinnedMeshRenderer.
	/// </summary>
	private static IMaterial? FindMaterialByDirectReference(Bundle root, string meshName)
	{
		foreach (IUnityObjectBase asset in root.FetchAssets())
		{
			// Check MeshFilter -> MeshRenderer combo
			if (asset is IMeshFilter meshFilter)
			{
				if (meshFilter.TryGetMesh(out IMesh? filterMesh) && filterMesh.Name == meshName)
				{
					IGameObject? gameObject = meshFilter.GameObject_C2P as IGameObject;
					if (gameObject != null)
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
					}
				}
			}

			// Check SkinnedMeshRenderer directly
			if (asset is ISkinnedMeshRenderer skinnedRenderer)
			{
				IMesh? skinnedMesh = skinnedRenderer.MeshP;
				if (skinnedMesh != null && skinnedMesh.Name == meshName)
				{
					var materials = skinnedRenderer.Materials_C25P;
					if (materials.Any())
					{
						return materials.FirstOrDefault();
					}
				}
			}
		}
		return null;
	}

	/// <summary>
	/// Strategy 2: Find material by fuzzy name matching.
	/// </summary>
	private static IMaterial? FindMaterialByFuzzyName(Bundle root, string meshBaseName)
	{
		IMaterial? bestMatch = null;
		int bestScore = 0;

		foreach (IUnityObjectBase asset in root.FetchAssets())
		{
			if (asset is IMaterial material)
			{
				string materialBaseName = GetBaseName(material.Name);
				int score = GetNameMatchScore(meshBaseName, materialBaseName);
				if (score > bestScore)
				{
					bestScore = score;
					bestMatch = material;
				}
			}
		}

		// Require minimum score to avoid false positives
		return bestScore >= 50 ? bestMatch : null;
	}

	/// <summary>
	/// Strategy 3: Find textures directly by mesh name pattern.
	/// </summary>
	private static (ITexture2D? BaseColor, ITexture2D? Normal, ITexture2D? Mask) FindTexturesByNamePattern(Bundle root, string meshBaseName)
	{
		ITexture2D? baseColor = null;
		ITexture2D? normal = null;
		ITexture2D? mask = null;

		var possiblePrefixes = GetPossibleTexturePrefixes(meshBaseName);

		foreach (IUnityObjectBase asset in root.FetchAssets())
		{
			if (asset is ITexture2D texture)
			{
				string texName = texture.Name;

				foreach (string prefix in possiblePrefixes)
				{
					if (texName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
					{
						string suffix = texName.Substring(prefix.Length);

						if (suffix.Contains("BaseMap", StringComparison.OrdinalIgnoreCase) ||
							suffix.Contains("MainTex", StringComparison.OrdinalIgnoreCase) ||
							suffix.Equals("", StringComparison.OrdinalIgnoreCase))
						{
							baseColor ??= texture;
						}
						else if (suffix.Contains("Normal", StringComparison.OrdinalIgnoreCase) ||
								 suffix.Contains("Bump", StringComparison.OrdinalIgnoreCase))
						{
							normal ??= texture;
						}
						else if (suffix.Contains("Mask", StringComparison.OrdinalIgnoreCase) ||
								 suffix.Contains("Metallic", StringComparison.OrdinalIgnoreCase))
						{
							mask ??= texture;
						}

						// Found a match with this prefix, don't check other prefixes for this texture
						break;
					}
				}

				// Early exit if we found all textures
				if (baseColor != null && normal != null && mask != null)
					break;
			}
		}

		return (baseColor, normal, mask);
	}

	/// <summary>
	/// Get base name by stripping LOD suffix and part names.
	/// E.g., "pirate_light_truck_chassis_LOD0" -> "pirate_light_truck"
	/// </summary>
	private static string GetBaseName(string name)
	{
		// Strip LOD suffix
		name = LodSuffixPattern.Replace(name, "");

		// Strip part suffixes
		foreach (var suffix in PartSuffixes)
		{
			if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
			{
				name = name.Substring(0, name.Length - suffix.Length);
			}
		}

		return name;
	}

	/// <summary>
	/// Get possible texture name prefixes for a mesh base name.
	/// Handles abbreviations like local_forces -> lf_
	/// </summary>
	private static List<string> GetPossibleTexturePrefixes(string baseName)
	{
		var prefixes = new List<string>();

		// Exact name
		prefixes.Add(baseName + "_");
		prefixes.Add(baseName);

		// Common prefixes
		prefixes.Add("auto_" + baseName);
		prefixes.Add("rmc_" + baseName);

		// Abbreviation: local_forces -> lf
		var words = baseName.Split('_');
		if (words.Length >= 2)
		{
			// First letters of first N-1 words + last word
			// local_forces_soldier -> lf_soldier
			string abbrev = string.Concat(words.Take(words.Length - 1).Select(w => w.Length > 0 ? w[0].ToString() : ""));
			string lastWord = words[words.Length - 1];
			prefixes.Add(abbrev + "_" + lastWord);
			prefixes.Add(abbrev.ToLower() + "_" + lastWord);

			// Just abbreviation
			prefixes.Add(abbrev + "_");
			prefixes.Add(abbrev.ToLower() + "_");
		}

		// Without common mesh prefixes
		foreach (var prefix in new[] { "pf_", "prefab_", "mesh_" })
		{
			if (baseName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				var stripped = baseName.Substring(prefix.Length);
				prefixes.Add(stripped + "_");
				prefixes.Add(stripped);
			}
		}

		return prefixes.Distinct().ToList();
	}

	/// <summary>
	/// Score how well two names match (0-100).
	/// </summary>
	private static int GetNameMatchScore(string meshBase, string materialBase)
	{
		if (string.IsNullOrEmpty(meshBase) || string.IsNullOrEmpty(materialBase))
			return 0;

		// Exact match
		if (meshBase.Equals(materialBase, StringComparison.OrdinalIgnoreCase))
			return 100;

		// One contains the other
		if (meshBase.Contains(materialBase, StringComparison.OrdinalIgnoreCase))
			return 80;
		if (materialBase.Contains(meshBase, StringComparison.OrdinalIgnoreCase))
			return 75;

		// Check abbreviation match (local_forces -> lf)
		var meshWords = meshBase.Split('_');
		if (meshWords.Length >= 2)
		{
			string abbrev = string.Concat(meshWords.Select(w => w.Length > 0 ? w[0].ToString() : ""));
			if (materialBase.StartsWith(abbrev, StringComparison.OrdinalIgnoreCase))
				return 60;
		}

		// Partial word overlap
		var materialWords = materialBase.Split('_');
		int commonWords = meshWords.Intersect(materialWords, StringComparer.OrdinalIgnoreCase).Count();
		if (commonWords > 0)
		{
			return Math.Min(50, commonWords * 15);
		}

		return 0;
	}
}
