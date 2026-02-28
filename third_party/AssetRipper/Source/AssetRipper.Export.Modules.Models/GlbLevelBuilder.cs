using AssetRipper.Assets;
using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Generics;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Numerics;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_18;
using AssetRipper.SourceGenerated.Classes.ClassID_2;
using AssetRipper.SourceGenerated.Classes.ClassID_21;
using AssetRipper.SourceGenerated.Classes.ClassID_25;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_33;
using AssetRipper.SourceGenerated.Classes.ClassID_4;
using AssetRipper.SourceGenerated.Classes.ClassID_43;
using AssetRipper.SourceGenerated.Classes.ClassID_74;
using AssetRipper.SourceGenerated.Classes.ClassID_91;
using AssetRipper.SourceGenerated.Classes.ClassID_95;
using AssetRipper.SourceGenerated.Classes.ClassID_137;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.Keyframe_Quaternionf;
using AssetRipper.SourceGenerated.Subclasses.Keyframe_Vector3f;
using AssetRipper.SourceGenerated.Subclasses.QuaternionCurve;
using AssetRipper.SourceGenerated.Subclasses.Vector3Curve;
using AssetRipper.Import.Logging;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Material;
using AssetRipper.SourceGenerated.Subclasses.SubMesh;
using AssetRipper.SourceGenerated.Subclasses.UnityTexEnv;
using SharpGLTF.Geometry;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;
using System.Buffers;

namespace AssetRipper.Export.Modules.Models;

public static class GlbLevelBuilder
{
	public static SceneBuilder Build(IEnumerable<IUnityObjectBase> assets, bool isScene)
	{
		SceneBuilder sceneBuilder = new();
		BuildParameters parameters = new BuildParameters(isScene);

		HashSet<IUnityObjectBase> exportedAssets = new();

		foreach (IUnityObjectBase asset in assets)
		{
			if (!exportedAssets.Contains(asset) && asset is IGameObject or IComponent)
			{
				IGameObject root = GetRoot(asset);

				AddGameObjectToScene(sceneBuilder, parameters, null, Transformation.Identity, Transformation.Identity, root.GetTransform());

				foreach (IEditorExtension exportedAsset in root.FetchHierarchy())
				{
					exportedAssets.Add(exportedAsset);
				}
			}
		}

		return sceneBuilder;
	}

	private static void AddGameObjectToScene(SceneBuilder sceneBuilder, BuildParameters parameters, NodeBuilder? parentNode, Transformation parentGlobalTransform, Transformation parentGlobalInverseTransform, ITransform transform)
	{
		IGameObject? gameObject = transform.GameObject_C4P;
		if (gameObject is null)
		{
			return;
		}

		Transformation localTransform = transform.ToTransformation();
		Transformation localInverseTransform = transform.ToInverseTransformation();
		Transformation globalTransform = localTransform * parentGlobalTransform;
		Transformation globalInverseTransform = parentGlobalInverseTransform * localInverseTransform;

		NodeBuilder node = parentNode is null ? new NodeBuilder(gameObject.Name) : parentNode.CreateNode(gameObject.Name);
		if (parentNode is not null || parameters.IsScene)
		{
			node.LocalTransform = new SharpGLTF.Transforms.AffineTransform(
				transform.LocalScale_C4.CastToStruct(),//Scaling is the same in both coordinate systems
				GlbCoordinateConversion.ToGltfQuaternionConvert(transform.LocalRotation_C4),
				GlbCoordinateConversion.ToGltfVector3Convert(transform.LocalPosition_C4));
		}
		sceneBuilder.AddNode(node);

		// Handle SkinnedMeshRenderer (bones, skinned meshes)
		if (gameObject.TryGetComponent(out ISkinnedMeshRenderer? skinnedRenderer))
		{
			IMesh? skinnedMesh = skinnedRenderer.MeshP;
			if (skinnedMesh != null && skinnedMesh.IsSet() && parameters.TryGetOrMakeMeshData(skinnedMesh, out MeshData skinnedMeshData))
			{
				AddSkinnedMeshToScene(sceneBuilder, parameters, node, skinnedMesh, skinnedMeshData, skinnedRenderer, globalTransform, globalInverseTransform);
			}
		}
		// Handle static MeshFilter + MeshRenderer
		else if (gameObject.TryGetComponent(out IMeshFilter? meshFilter)
			&& meshFilter.TryGetMesh(out IMesh? mesh)
			&& mesh.IsSet()
			&& parameters.TryGetOrMakeMeshData(mesh, out MeshData meshData))
		{
			if (gameObject.TryGetComponent(out IRenderer? meshRenderer))
			{
				if (ReferencesDynamicMesh(meshRenderer))
				{

					AddDynamicMeshToScene(sceneBuilder, parameters, node, mesh, meshData, new MaterialList(meshRenderer));
				}
				else
				{
					int[] subsetIndices = GetSubsetIndices(meshRenderer);
					AddStaticMeshToScene(sceneBuilder, parameters, node, mesh, meshData, subsetIndices, new MaterialList(meshRenderer), globalTransform, globalInverseTransform);
				}
			}
		}

		foreach (ITransform childTransform in transform.Children_C4P.WhereNotNull())
		{
			AddGameObjectToScene(sceneBuilder, parameters, node, localTransform * parentGlobalTransform, parentGlobalInverseTransform * localInverseTransform, childTransform);
		}
	}

	/// <summary>
	/// Add a skinned mesh with skeleton to the scene.
	/// </summary>
	private static void AddSkinnedMeshToScene(
		SceneBuilder sceneBuilder,
		BuildParameters parameters,
		NodeBuilder node,
		IMesh mesh,
		MeshData meshData,
		ISkinnedMeshRenderer skinnedRenderer,
		Transformation globalTransform,
		Transformation globalInverseTransform)
	{
		// Get bone transforms from SkinnedMeshRenderer
		var boneTransforms = skinnedRenderer.BonesP.ToArray();
		if (boneTransforms.Length == 0)
		{
			// No bones - fall back to static mesh
			Logger.Warning(LogCategory.Export, $"SkinnedMeshRenderer for mesh '{mesh.Name}' has no bones, exporting as static mesh");
			AddDynamicMeshToScene(sceneBuilder, parameters, node, mesh, meshData, new MaterialList(skinnedRenderer));
			return;
		}

		Logger.Info(LogCategory.Export, $"Exporting skinned mesh '{mesh.Name}' with {boneTransforms.Length} bones");

		// Build skeleton hierarchy properly
		// SharpGLTF requires nodes to be created as children using parentNode.CreateNode()
		var jointNodes = new NodeBuilder[boneTransforms.Length];
		var transformToIndex = new Dictionary<ITransform, int>();

		// First pass: map transforms to indices
		for (int i = 0; i < boneTransforms.Length; i++)
		{
			ITransform? boneTransform = boneTransforms[i];
			if (boneTransform != null)
			{
				transformToIndex[boneTransform] = i;
			}
		}

		// Find root bones (those whose parents aren't in the bone list)
		var rootBoneIndices = new List<int>();
		var childrenMap = new Dictionary<int, List<int>>(); // parent index -> child indices

		for (int i = 0; i < boneTransforms.Length; i++)
		{
			ITransform? boneTransform = boneTransforms[i];
			if (boneTransform == null)
			{
				rootBoneIndices.Add(i); // Null bones are treated as roots
				continue;
			}

			ITransform? parentTransform = boneTransform.Father_C4P;
			if (parentTransform != null && transformToIndex.TryGetValue(parentTransform, out int parentIndex))
			{
				// Has a parent in the bone list
				if (!childrenMap.TryGetValue(parentIndex, out var children))
				{
					children = new List<int>();
					childrenMap[parentIndex] = children;
				}
				children.Add(i);
			}
			else
			{
				// Root bone (parent not in bone list)
				rootBoneIndices.Add(i);
			}
		}

		// Create skeleton root node under the mesh node
		NodeBuilder skeletonRoot = node.CreateNode($"{mesh.Name}_Skeleton");

		// Recursively build the skeleton hierarchy
		void BuildBoneNode(int boneIndex, NodeBuilder parent)
		{
			ITransform? boneTransform = boneTransforms[boneIndex];
			string boneName = boneTransform?.GameObject_C4P?.Name ?? $"Bone_{boneIndex}";

			// Create child node under parent
			NodeBuilder boneNode = parent.CreateNode(boneName);

			// Set local transform if we have transform data
			if (boneTransform != null)
			{
				boneNode.LocalTransform = new SharpGLTF.Transforms.AffineTransform(
					boneTransform.LocalScale_C4.CastToStruct(),
					GlbCoordinateConversion.ToGltfQuaternionConvert(boneTransform.LocalRotation_C4),
					GlbCoordinateConversion.ToGltfVector3Convert(boneTransform.LocalPosition_C4));
			}

			jointNodes[boneIndex] = boneNode;

			// Process children
			if (childrenMap.TryGetValue(boneIndex, out var children))
			{
				foreach (int childIndex in children)
				{
					BuildBoneNode(childIndex, boneNode);
				}
			}
		}

		// Build from all root bones
		foreach (int rootIndex in rootBoneIndices)
		{
			BuildBoneNode(rootIndex, skeletonRoot);
		}

		// Add skeleton to scene
		sceneBuilder.AddNode(skeletonRoot);

		// Build the mesh with materials
		AccessListBase<ISubMesh> subMeshes = mesh.SubMeshes;
		var materialList = new MaterialList(skinnedRenderer);

		(ISubMesh, MaterialBuilder)[] subMeshArray = ArrayPool<(ISubMesh, MaterialBuilder)>.Shared.Rent(subMeshes.Count);
		for (int i = 0; i < subMeshes.Count; i++)
		{
			MaterialBuilder materialBuilder = parameters.GetOrMakeMaterial(materialList[i]);
			subMeshArray[i] = (subMeshes[i], materialBuilder);
		}
		ArraySegment<(ISubMesh, MaterialBuilder)> arraySegment = new ArraySegment<(ISubMesh, MaterialBuilder)>(subMeshArray, 0, subMeshes.Count);

		// Build the mesh - use identity transform since joints handle positioning
		IMeshBuilder<MaterialBuilder> meshBuilder = GlbSubMeshBuilder.BuildSubMeshes(
			arraySegment,
			mesh.Is16BitIndices(),
			meshData,
			Transformation.Identity,
			Transformation.Identity);

		ArrayPool<(ISubMesh, MaterialBuilder)>.Shared.Return(subMeshArray);

		// Add as skinned mesh with joints
		// Note: SharpGLTF's AddSkinnedMesh signature: (mesh, worldMatrix, joints...)
		try
		{
			sceneBuilder.AddSkinnedMesh(meshBuilder, System.Numerics.Matrix4x4.Identity, jointNodes);
			Logger.Info(LogCategory.Export, $"Successfully added skinned mesh '{mesh.Name}'");

			// Build a mapping from bone paths to NodeBuilders for animation targeting
			var bonePathToNode = new Dictionary<string, NodeBuilder>();

			// Add root motion mapping - empty path targets the skeleton root or mesh node
			// This handles root motion animations where path is ""
			bonePathToNode[""] = skeletonRoot;

			for (int i = 0; i < boneTransforms.Length; i++)
			{
				ITransform? boneTransform = boneTransforms[i];
				if (boneTransform?.GameObject_C4P != null)
				{
					// Build the path by walking up the hierarchy
					string path = GetTransformPath(boneTransform, skinnedRenderer.GameObject_C2P as IGameObject);
					if (!string.IsNullOrEmpty(path) && !bonePathToNode.ContainsKey(path))
					{
						bonePathToNode[path] = jointNodes[i];
					}
					// Also add by name only for simpler matching
					string boneName = boneTransform.GameObject_C4P.Name;
					if (!bonePathToNode.ContainsKey(boneName))
					{
						bonePathToNode[boneName] = jointNodes[i];
					}
				}
			}

			// Try to find and export animations
			IGameObject? gameObject = skinnedRenderer.GameObject_C2P as IGameObject;
			if (gameObject != null)
			{
				TryExportAnimations(gameObject, bonePathToNode);
			}
		}
		catch (Exception ex)
		{
			Logger.Warning(LogCategory.Export, $"Failed to add skinned mesh '{mesh.Name}': {ex.Message}. Falling back to rigid mesh.");
			sceneBuilder.AddRigidMesh(meshBuilder, node);
		}
	}

	/// <summary>
	/// Get the transform path relative to a root GameObject.
	/// </summary>
	private static string GetTransformPath(ITransform transform, IGameObject? root)
	{
		var pathParts = new List<string>();
		ITransform? current = transform;

		while (current != null)
		{
			IGameObject? go = current.GameObject_C4P;
			if (go == null) break;

			// Stop if we've reached the root
			if (root != null && go == root) break;

			pathParts.Add(go.Name);
			current = current.Father_C4P;
		}

		pathParts.Reverse();
		return string.Join("/", pathParts);
	}

	/// <summary>
	/// Try to find animations on the GameObject or its parent and export them.
	/// </summary>
	private static void TryExportAnimations(IGameObject gameObject, Dictionary<string, NodeBuilder> bonePathToNode)
	{
		// Look for Animator component on this GameObject or parents
		IAnimator? animator = FindAnimatorInHierarchy(gameObject);
		if (animator == null)
		{
			Logger.Info(LogCategory.Export, $"No Animator found for {gameObject.Name}");
			return;
		}

		// Get the AnimatorController
		IAnimatorController? controller = GetAnimatorController(animator);
		if (controller == null)
		{
			Logger.Info(LogCategory.Export, $"No AnimatorController found for {gameObject.Name}");
			return;
		}

		// Export each animation clip
		int clipCount = 0;
		foreach (var clipPPtr in controller.AnimationClips)
		{
			IAnimationClip? clip = clipPPtr.TryGetAsset(controller.Collection);
			if (clip != null)
			{
				try
				{
					ExportAnimationClip(clip, bonePathToNode);
					clipCount++;
				}
				catch (Exception ex)
				{
					Logger.Warning(LogCategory.Export, $"Failed to export animation '{clip.Name}': {ex.Message}");
				}
			}
		}

		if (clipCount > 0)
		{
			Logger.Info(LogCategory.Export, $"Exported {clipCount} animations for {gameObject.Name}");
		}
	}

	private static IAnimator? FindAnimatorInHierarchy(IGameObject gameObject)
	{
		// Check this GameObject
		if (gameObject.TryGetComponent(out IAnimator? animator))
		{
			return animator;
		}

		// Check parent
		ITransform? transform = gameObject.GetTransform();
		ITransform? parent = transform?.Father_C4P;
		if (parent?.GameObject_C4P is IGameObject parentGO)
		{
			return FindAnimatorInHierarchy(parentGO);
		}

		return null;
	}

	private static IAnimatorController? GetAnimatorController(IAnimator animator)
	{
		if (animator.Has_Controller_PPtr_AnimatorController_4())
		{
			return animator.Controller_PPtr_AnimatorController_4P;
		}
		else if (animator.Has_Controller_PPtr_RuntimeAnimatorController_4_3())
		{
			return animator.Controller_PPtr_RuntimeAnimatorController_4_3P as IAnimatorController;
		}
		else if (animator.Has_Controller_PPtr_RuntimeAnimatorController_5())
		{
			return animator.Controller_PPtr_RuntimeAnimatorController_5P as IAnimatorController;
		}
		return null;
	}

	/// <summary>
	/// Export an AnimationClip to GLTF animations on the bone nodes.
	/// </summary>
	private static void ExportAnimationClip(IAnimationClip clip, Dictionary<string, NodeBuilder> bonePathToNode)
	{
		string animName = clip.Name ?? "Animation";
		int keyframeCount = 0;
		int unmatchedCurves = 0;

		// Export position curves (includes root motion if path is "")
		foreach (IVector3Curve curve in clip.PositionCurves_C74)
		{
			string path = curve.Path.String ?? "";
			if (TryFindBoneNode(path, bonePathToNode, out NodeBuilder? boneNode) && boneNode != null)
			{
				var translationCurve = boneNode.UseTranslation(animName);
				foreach (IKeyframe_Vector3f keyframe in curve.Curve.Curve)
				{
					float time = keyframe.Time;
					var value = new System.Numerics.Vector3(
						keyframe.Value.X,
						keyframe.Value.Y,
						-keyframe.Value.Z); // Convert Z for coordinate system
					translationCurve.SetPoint(time, value);
					keyframeCount++;
				}
				if (string.IsNullOrEmpty(path))
				{
					Logger.Info(LogCategory.Export, $"    Root motion position curve: {curve.Curve.Curve.Count} keyframes");
				}
			}
			else
			{
				unmatchedCurves++;
				Logger.Warning(LogCategory.Export, $"    Position curve path '{path}' not found in bone mapping");
			}
		}

		// Export rotation curves (quaternion)
		foreach (IQuaternionCurve curve in clip.RotationCurves_C74)
		{
			string path = curve.Path.String ?? "";
			if (TryFindBoneNode(path, bonePathToNode, out NodeBuilder? boneNode) && boneNode != null)
			{
				var rotationCurve = boneNode.UseRotation(animName);
				foreach (IKeyframe_Quaternionf keyframe in curve.Curve.Curve)
				{
					float time = keyframe.Time;
					// Convert quaternion for coordinate system (negate Z and W)
					var value = new System.Numerics.Quaternion(
						keyframe.Value.X,
						keyframe.Value.Y,
						-keyframe.Value.Z,
						-keyframe.Value.W);
					rotationCurve.SetPoint(time, value);
					keyframeCount++;
				}
				if (string.IsNullOrEmpty(path))
				{
					Logger.Info(LogCategory.Export, $"    Root motion rotation curve: {curve.Curve.Curve.Count} keyframes");
				}
			}
			else
			{
				unmatchedCurves++;
				Logger.Warning(LogCategory.Export, $"    Rotation curve path '{path}' not found in bone mapping");
			}
		}

		// Export Euler rotation curves (convert to quaternion for GLTF)
		if (clip.Has_EulerCurves_C74())
		{
			foreach (IVector3Curve curve in clip.EulerCurves_C74)
			{
				string path = curve.Path.String ?? "";
				if (TryFindBoneNode(path, bonePathToNode, out NodeBuilder? boneNode) && boneNode != null)
				{
					var rotationCurve = boneNode.UseRotation(animName);
					foreach (IKeyframe_Vector3f keyframe in curve.Curve.Curve)
					{
						float time = keyframe.Time;
						// Convert euler angles (degrees) to quaternion
						// Unity uses ZXY rotation order
						var euler = new System.Numerics.Vector3(
							keyframe.Value.X * (MathF.PI / 180f),
							keyframe.Value.Y * (MathF.PI / 180f),
							keyframe.Value.Z * (MathF.PI / 180f));
						var quat = System.Numerics.Quaternion.CreateFromYawPitchRoll(euler.Y, euler.X, euler.Z);
						// Convert for coordinate system
						var value = new System.Numerics.Quaternion(quat.X, quat.Y, -quat.Z, -quat.W);
						rotationCurve.SetPoint(time, value);
						keyframeCount++;
					}
					if (string.IsNullOrEmpty(path))
					{
						Logger.Info(LogCategory.Export, $"    Root motion euler curve: {curve.Curve.Curve.Count} keyframes");
					}
				}
				else
				{
					unmatchedCurves++;
					Logger.Warning(LogCategory.Export, $"    Euler curve path '{path}' not found in bone mapping");
				}
			}
		}

		// Export scale curves
		foreach (IVector3Curve curve in clip.ScaleCurves_C74)
		{
			string path = curve.Path.String ?? "";
			if (TryFindBoneNode(path, bonePathToNode, out NodeBuilder? boneNode) && boneNode != null)
			{
				var scaleCurve = boneNode.UseScale(animName);
				foreach (IKeyframe_Vector3f keyframe in curve.Curve.Curve)
				{
					float time = keyframe.Time;
					var value = new System.Numerics.Vector3(
						keyframe.Value.X,
						keyframe.Value.Y,
						keyframe.Value.Z); // Scale doesn't need coordinate conversion
					scaleCurve.SetPoint(time, value);
					keyframeCount++;
				}
			}
			else
			{
				unmatchedCurves++;
				Logger.Warning(LogCategory.Export, $"    Scale curve path '{path}' not found in bone mapping");
			}
		}

		if (keyframeCount > 0 || unmatchedCurves > 0)
		{
			Logger.Info(LogCategory.Export, $"  Animation '{animName}': {keyframeCount} keyframes exported, {unmatchedCurves} curves unmatched");
		}
	}

	private static bool TryFindBoneNode(string path, Dictionary<string, NodeBuilder> bonePathToNode, out NodeBuilder? boneNode)
	{
		// Try exact path match
		if (bonePathToNode.TryGetValue(path, out boneNode))
		{
			return true;
		}

		// Try just the last part of the path (bone name)
		int lastSlash = path.LastIndexOf('/');
		if (lastSlash >= 0)
		{
			string boneName = path.Substring(lastSlash + 1);
			if (bonePathToNode.TryGetValue(boneName, out boneNode))
			{
				return true;
			}
		}

		// Try matching by name (path might be just the name)
		if (bonePathToNode.TryGetValue(path, out boneNode))
		{
			return true;
		}

		boneNode = null;
		return false;
	}

	private static void AddDynamicMeshToScene(SceneBuilder sceneBuilder, BuildParameters parameters, NodeBuilder node, IMesh mesh, MeshData meshData, MaterialList materialList)
	{
		AccessListBase<ISubMesh> subMeshes = mesh.SubMeshes;
		(ISubMesh, MaterialBuilder)[] subMeshArray = ArrayPool<(ISubMesh, MaterialBuilder)>.Shared.Rent(subMeshes.Count);
		for (int i = 0; i < subMeshes.Count; i++)
		{
			MaterialBuilder materialBuilder = parameters.GetOrMakeMaterial(materialList[i]);
			subMeshArray[i] = (subMeshes[i], materialBuilder);
		}
		ArraySegment<(ISubMesh, MaterialBuilder)> arraySegment = new ArraySegment<(ISubMesh, MaterialBuilder)>(subMeshArray, 0, subMeshes.Count);
		IMeshBuilder<MaterialBuilder> subMeshBuilder = GlbSubMeshBuilder.BuildSubMeshes(arraySegment, mesh.Is16BitIndices(), meshData, Transformation.Identity, Transformation.Identity);
		sceneBuilder.AddRigidMesh(subMeshBuilder, node);
		ArrayPool<(ISubMesh, MaterialBuilder)>.Shared.Return(subMeshArray);
	}

	private static void AddStaticMeshToScene(SceneBuilder sceneBuilder, BuildParameters parameters, NodeBuilder node, IMesh mesh, MeshData meshData, int[] subsetIndices, MaterialList materialList, Transformation globalTransform, Transformation globalInverseTransform)
	{
		(ISubMesh, MaterialBuilder)[] subMeshArray = ArrayPool<(ISubMesh, MaterialBuilder)>.Shared.Rent(subsetIndices.Length);
		AccessListBase<ISubMesh> subMeshes = mesh.SubMeshes;
		for (int i = 0; i < subsetIndices.Length; i++)
		{
			ISubMesh subMesh = subMeshes[subsetIndices[i]];
			MaterialBuilder materialBuilder = parameters.GetOrMakeMaterial(materialList[i]);
			subMeshArray[i] = (subMesh, materialBuilder);
		}
		ArraySegment<(ISubMesh, MaterialBuilder)> arraySegment = new ArraySegment<(ISubMesh, MaterialBuilder)>(subMeshArray, 0, subsetIndices.Length);
		IMeshBuilder<MaterialBuilder> subMeshBuilder = GlbSubMeshBuilder.BuildSubMeshes(arraySegment, mesh.Is16BitIndices(), meshData, globalInverseTransform, globalTransform);
		sceneBuilder.AddRigidMesh(subMeshBuilder, node);
		ArrayPool<(ISubMesh, MaterialBuilder)>.Shared.Return(subMeshArray);
	}

	private static IGameObject GetRoot(IUnityObjectBase asset)
	{
		return asset switch
		{
			IGameObject gameObject => gameObject.GetRoot(),
			IComponent component => component.GameObject_C2P!.GetRoot(),
			_ => throw new InvalidOperationException()
		};
	}

	private static bool ReferencesDynamicMesh(IRenderer renderer)
	{
		return renderer.Has_StaticBatchInfo_C25() && renderer.StaticBatchInfo_C25.SubMeshCount == 0
			|| renderer.Has_SubsetIndices_C25() && renderer.SubsetIndices_C25.Count == 0;
	}

	private static int[] GetSubsetIndices(IRenderer renderer)
	{
		AccessListBase<IPPtr_Material> materials = renderer.Materials_C25;
		if (renderer.Has_SubsetIndices_C25())
		{
			return renderer.SubsetIndices_C25.Select(i => (int)i).ToArray();
		}
		else if (renderer.Has_StaticBatchInfo_C25())
		{
			return Enumerable.Range(renderer.StaticBatchInfo_C25.FirstSubMesh, renderer.StaticBatchInfo_C25.SubMeshCount).ToArray();
		}
		else
		{
			return Array.Empty<int>();
		}
	}

	private readonly record struct BuildParameters(
		MaterialBuilder DefaultMaterial,
		Dictionary<ITexture2D, MemoryImage> ImageCache,
		Dictionary<IMaterial, MaterialBuilder> MaterialCache,
		Dictionary<IMesh, MeshData> MeshCache,
		Dictionary<string, ITexture2D> TextureNameCache,
		bool IsScene)
	{
		public BuildParameters(bool isScene) : this(new MaterialBuilder("DefaultMaterial"), new(), new(), new(), new(), isScene) { }
		public bool TryGetOrMakeMeshData(IMesh mesh, out MeshData meshData)
		{
			if (MeshCache.TryGetValue(mesh, out meshData))
			{
				return true;
			}
			else if (MeshData.TryMakeFromMesh(mesh, out meshData))
			{
				MeshCache.Add(mesh, meshData);
				return true;
			}
			return false;
		}

		public MaterialBuilder GetOrMakeMaterial(IMaterial? material)
		{
			if (material is null)
			{
				return DefaultMaterial;
			}
			if (!MaterialCache.TryGetValue(material, out MaterialBuilder? materialBuilder))
			{
				materialBuilder = MakeMaterialBuilder(material);
				MaterialCache.Add(material, materialBuilder);
			}
			return materialBuilder;
		}

		public bool TryGetOrMakeImage(ITexture2D texture, out MemoryImage image)
		{
			if (!ImageCache.TryGetValue(texture, out image))
			{
				if (TextureConverter.TryConvertToBitmap(texture, out DirectBitmap bitmap))
				{
					using MemoryStream memoryStream = new();
					bitmap.SaveAsPng(memoryStream);
					image = new MemoryImage(memoryStream.ToArray());
					ImageCache.Add(texture, image);
					return true;
				}
				return false;
			}
			else
			{
				return true;
			}
		}

		private MaterialBuilder MakeMaterialBuilder(IMaterial material)
		{
			MaterialBuilder materialBuilder = new MaterialBuilder(material.Name);
			GetTextures(material, out ITexture2D? mainTexture, out ITexture2D? normalTexture);
			if (mainTexture is not null && TryGetOrMakeImage(mainTexture, out MemoryImage mainImage))
			{
				materialBuilder.WithBaseColor(mainImage);
			}
			if (normalTexture is not null && TryGetOrMakeImage(normalTexture, out MemoryImage normalImage))
			{
				materialBuilder.WithNormal(normalImage);
			}
			return materialBuilder;
		}

		private void GetTextures(IMaterial material, out ITexture2D? mainTexture, out ITexture2D? normalTexture)
		{
			mainTexture = null;
			normalTexture = null;
			ITexture2D? mainReplacement = null;
			foreach ((Utf8String utf8Name, IUnityTexEnv textureParameter) in material.GetTextureProperties())
			{
				string name = utf8Name.String;
				if (IsMainTexture(name))
				{
					mainTexture ??= textureParameter.Texture.TryGetAsset(material.Collection) as ITexture2D;
				}
				else if (IsNormalTexture(name))
				{
					normalTexture ??= textureParameter.Texture.TryGetAsset(material.Collection) as ITexture2D;
				}
				else
				{
					mainReplacement ??= textureParameter.Texture.TryGetAsset(material.Collection) as ITexture2D;
				}
			}
			mainTexture ??= mainReplacement;

			// Fallback: if textures weren't resolved via PPtr, try to find them by name in the bundle hierarchy
			// This handles cases where textures are in different asset bundles than the material
			if (mainTexture is null || normalTexture is null)
			{
				string materialName = material.Name;
				if (mainTexture is null)
				{
					mainTexture = TryFindTextureByName(material, materialName + "_BaseMap")
						?? TryFindTextureByName(material, materialName + "_MainTex")
						?? TryFindTextureByName(material, materialName);
				}
				if (normalTexture is null)
				{
					normalTexture = TryFindTextureByName(material, materialName + "_Normal")
						?? TryFindTextureByName(material, materialName + "_BumpMap");
				}
			}
		}

		private ITexture2D? TryFindTextureByName(IMaterial material, string textureName)
		{
			// Check cache first
			if (TextureNameCache.TryGetValue(textureName, out ITexture2D? cached))
			{
				return cached;
			}

			// Build cache if empty (lazy initialization)
			if (TextureNameCache.Count == 0)
			{
				BuildTextureNameCache(material);
			}

			// Try again after cache is built
			TextureNameCache.TryGetValue(textureName, out cached);
			return cached;
		}

		private void BuildTextureNameCache(IMaterial material)
		{
			// Search all assets in the bundle hierarchy for textures
			var bundle = material.Collection.Bundle;
			var root = bundle.GetRoot();
			foreach (var asset in root.FetchAssets())
			{
				if (asset is ITexture2D texture)
				{
					string name = texture.Name;
					if (!string.IsNullOrEmpty(name) && !TextureNameCache.ContainsKey(name))
					{
						TextureNameCache[name] = texture;
					}
				}
			}
		}

		private static bool IsMainTexture(string textureName)
		{
			return textureName is "_MainTex" or "texture" or "Texture" or "_Texture";
		}

		private static bool IsNormalTexture(string textureName)
		{
			return textureName is "_Normal" or "Normal" or "normal";
		}
	}

	private readonly struct MaterialList
	{
		private readonly AccessListBase<IPPtr_Material> materials;
		private readonly AssetCollection file;

		private MaterialList(AccessListBase<IPPtr_Material> materials, AssetCollection file)
		{
			this.materials = materials;
			this.file = file;
		}

		public MaterialList(IRenderer renderer) : this(renderer.Materials_C25, renderer.Collection) { }

		public int Count => materials.Count;

		public IMaterial? this[int index]
		{
			get
			{
				if (index >= materials.Count)
				{
					return null;
				}
				return materials[index].TryGetAsset(file);
			}
		}
	}
}
