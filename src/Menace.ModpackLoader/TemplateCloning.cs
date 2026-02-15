using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Menace.SDK;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// Template cloning: deep-copies existing game templates (ScriptableObjects) via
/// UnityEngine.Object.Instantiate() and registers them in the DataTemplateLoader
/// registry so the game treats them as first-class templates.
/// </summary>
public partial class ModpackLoaderMod
{
    // Set to true to disable runtime cloning fallback (relies on native assets only)
    // This is used to verify that native asset creation is working correctly.
    private const bool DISABLE_RUNTIME_CLONING = true;

    // Tracks which modpack+templateType clone sets have been applied
    private readonly HashSet<string> _appliedCloneKeys = new();

    /// <summary>
    /// Process all clone definitions in a modpack. Returns true if all types were found.
    /// </summary>
    private bool ApplyClones(Modpack modpack)
    {
        if (DISABLE_RUNTIME_CLONING)
        {
            SdkLogger.Msg($"[TemplateCloning] Runtime cloning DISABLED - relying on native assets only");
            return true;
        }

        if (modpack.Clones == null || modpack.Clones.Count == 0)
            return true;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            SdkLogger.Error("Assembly-CSharp not found, cannot apply clones");
            return false;
        }

        var allFound = true;

        foreach (var (templateTypeName, cloneMap) in modpack.Clones)
        {
            var cloneKey = $"{modpack.Name}:clones:{templateTypeName}";
            if (_appliedCloneKeys.Contains(cloneKey))
                continue;

            if (cloneMap == null || cloneMap.Count == 0)
                continue;

            try
            {
                var templateType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == templateTypeName && !t.IsAbstract);

                if (templateType == null)
                {
                    SdkLogger.Warning($"  Clone: template type '{templateTypeName}' not found");
                    allFound = false;
                    continue;
                }

                // Ensure templates are loaded by calling GetAll<T>() on the game's DataTemplateLoader
                EnsureTemplatesLoaded(gameAssembly, templateType);

                // Find all existing instances of this type
                var il2cppType = Il2CppType.From(templateType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);

                if (objects == null || objects.Length == 0)
                {
                    SdkLogger.Warning($"  Clone: no {templateTypeName} instances found — will retry on next scene");
                    allFound = false;
                    continue;
                }

                // Build name → object lookup
                var lookup = new Dictionary<string, UnityEngine.Object>();
                foreach (var obj in objects)
                {
                    if (obj != null && !string.IsNullOrEmpty(obj.name))
                        lookup[obj.name] = obj;
                }

                int clonedCount = 0;
                foreach (var (newName, sourceName) in cloneMap)
                {
                    // Skip if a template with this name already exists (already cloned or native)
                    if (lookup.ContainsKey(newName))
                    {
                        SdkLogger.Msg($"  Clone: '{newName}' already exists, skipping");
                        clonedCount++;
                        continue;
                    }

                    if (!lookup.TryGetValue(sourceName, out var source))
                    {
                        SdkLogger.Warning($"  Clone: source '{sourceName}' not found for clone '{newName}'");
                        continue;
                    }

                    try
                    {
                        // Deep-copy via Instantiate — copies all serialized fields
                        var clone = UnityEngine.Object.Instantiate(source);
                        clone.name = newName;
                        clone.hideFlags = HideFlags.DontUnloadUnusedAsset;

                        // Set m_ID on the DataTemplate base class via IL2CPP field write
                        SetTemplateId(clone, newName);

                        // Register in DataTemplateLoader's internal dictionaries
                        RegisterInLoader(gameAssembly, clone, templateType, newName);

                        // Add to our local lookup so subsequent clones can reference this one
                        lookup[newName] = clone;

                        // Verify registration was successful by trying to look it up
                        var verifyLookup = Resources.FindObjectsOfTypeAll(il2cppType);
                        var verified = verifyLookup?.Any(o => o.name == newName) ?? false;
                        var verifyStatus = verified ? "verified in Resources" : "NOT in Resources (may still work via DataTemplateLoader)";

                        SdkLogger.Msg($"  Cloned: {sourceName} -> {newName} ({verifyStatus})");
                        clonedCount++;
                    }
                    catch (Exception ex)
                    {
                        SdkLogger.Error($"  Clone failed: {sourceName} -> {newName}: {ex.Message}");
                    }
                }

                if (clonedCount > 0)
                {
                    SdkLogger.Msg($"  Applied {clonedCount} clone(s) for {templateTypeName}");
                    _appliedCloneKeys.Add(cloneKey);
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"  Failed to process clones for {templateTypeName}: {ex.Message}");
            }
        }

        return allFound;
    }

    /// <summary>
    /// Call DataTemplateLoader.GetAll&lt;T&gt;() to ensure the type's templates are loaded
    /// into the internal registry before we try to register clones.
    /// </summary>
    private void EnsureTemplatesLoaded(Assembly gameAssembly, Type templateType)
    {
        try
        {
            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataTemplateLoader");

            if (loaderType == null)
            {
                SdkLogger.Warning("  DataTemplateLoader class not found in Assembly-CSharp");
                return;
            }

            var getAllMethod = loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition);

            if (getAllMethod == null)
            {
                SdkLogger.Warning("  DataTemplateLoader.GetAll method not found");
                return;
            }

            var genericMethod = getAllMethod.MakeGenericMethod(templateType);
            genericMethod.Invoke(null, null);
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"  EnsureTemplatesLoaded({templateType.Name}): {ex.Message}");
        }
    }

    /// <summary>
    /// Write the m_ID field on a DataTemplate-derived ScriptableObject via IL2CPP field offset.
    /// m_ID is not serialized by Instantiate (it's decorated with [NonSerialized] or similar),
    /// so we must set it manually.
    /// </summary>
    private void SetTemplateId(UnityEngine.Object clone, string id)
    {
        try
        {
            if (clone is not Il2CppObjectBase il2cppObj)
                return;

            IntPtr objectPointer = il2cppObj.Pointer;
            if (objectPointer == IntPtr.Zero)
                return;

            IntPtr klass = IL2CPP.il2cpp_object_get_class(objectPointer);
            if (klass == IntPtr.Zero)
                return;

            // Walk the class hierarchy to find m_ID (defined on DataTemplate base class)
            IntPtr idField = FindField(klass, "m_ID");
            if (idField == IntPtr.Zero)
            {
                SdkLogger.Warning($"  SetTemplateId: m_ID field not found on {clone.name}");
                return;
            }

            uint offset = IL2CPP.il2cpp_field_get_offset(idField);
            if (offset == 0)
            {
                SdkLogger.Warning($"  SetTemplateId: m_ID offset is 0 for {clone.name}");
                return;
            }

            // Write the IL2CPP string pointer at the field offset
            IntPtr il2cppString = IL2CPP.ManagedStringToIl2Cpp(id);
            Marshal.WriteIntPtr(objectPointer + (int)offset, il2cppString);
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"  SetTemplateId failed for {clone.name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Walk class hierarchy to find a field by name.
    /// Same pattern used in DataExtractor and CombinedArms.
    /// </summary>
    private static IntPtr FindField(IntPtr klass, string fieldName)
    {
        IntPtr searchKlass = klass;
        while (searchKlass != IntPtr.Zero)
        {
            IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(searchKlass, fieldName);
            if (field != IntPtr.Zero)
                return field;
            searchKlass = IL2CPP.il2cpp_class_get_parent(searchKlass);
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Register a cloned template in DataTemplateLoader's internal registry.
    /// DataTemplateLoader has two dictionaries:
    /// - Offset 0x10: Dictionary&lt;Type, DataTemplate[]&gt; - all templates array
    /// - Offset 0x18: Dictionary&lt;Type, Dictionary&lt;string, DataTemplate&gt;&gt; - name lookup
    /// </summary>
    private void RegisterInLoader(Assembly gameAssembly, UnityEngine.Object clone, Type templateType, string name)
    {
        try
        {
            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.FullName == "Menace.Tools.DataTemplateLoader" ||
                                     t.Name == "DataTemplateLoader");

            if (loaderType == null)
            {
                // Not a fatal error - clone still exists in Resources
                return;
            }

            // Get singleton via GetSingleton() method
            var getSingleton = loaderType.GetMethod("GetSingleton",
                BindingFlags.Public | BindingFlags.Static);

            if (getSingleton == null)
            {
                SdkLogger.Warning("  RegisterInLoader: GetSingleton method not found");
                return;
            }

            var singleton = getSingleton.Invoke(null, null);
            if (singleton == null)
            {
                SdkLogger.Warning("  RegisterInLoader: GetSingleton returned null");
                return;
            }

            // Access the name lookup dictionary at offset 0x18
            // It's Dictionary<Type, Dictionary<string, DataTemplate>>
            var singletonPtr = ((Il2CppObjectBase)singleton).Pointer;
            if (singletonPtr == IntPtr.Zero)
            {
                SdkLogger.Warning("  RegisterInLoader: singleton pointer is null");
                return;
            }

            // Read the dictionary pointer at offset 0x18
            var nameLookupPtr = Marshal.ReadIntPtr(singletonPtr + 0x18);
            if (nameLookupPtr == IntPtr.Zero)
            {
                SdkLogger.Warning("  RegisterInLoader: name lookup dictionary is null");
                return;
            }

            // Cast clone to the template type
            var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
            var castClone = genericTryCast.Invoke(clone, null);
            if (castClone == null)
            {
                SdkLogger.Warning($"  RegisterInLoader: failed to cast clone to {templateType.Name}");
                return;
            }

            // Try using reflection on the outer dictionary to get/create inner dictionary
            // and add the clone
            var outerDictType = singleton.GetType().GetField("m_NameLookup",
                BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType;

            // Get inner dictionary for this type via indexer or TryGetValue
            bool registered = TryRegisterViaReflection(gameAssembly, singleton, templateType, castClone, name);

            if (registered)
            {
                SdkLogger.Msg($"    Registered '{name}' in DataTemplateLoader");
            }
            else
            {
                SdkLogger.Warning($"  RegisterInLoader: could not register '{name}' — " +
                    "clone exists in memory but may not be findable via DataTemplateLoader.Get()");
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"  RegisterInLoader failed for '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Try to register a clone using reflection on the DataTemplateLoader fields.
    /// The DataTemplateLoader has a field m_TemplateMaps of type
    /// Dictionary&lt;Type, Dictionary&lt;string, DataTemplate&gt;&gt;
    /// We need to add our clone to the inner dictionary for the template type.
    /// </summary>
    private bool TryRegisterViaReflection(Assembly gameAssembly, object singleton, Type templateType, object castClone, string name)
    {
        try
        {
            var loaderType = singleton.GetType();

            // First approach: try to find the field by known names
            var mapField = FindInstanceField(loaderType,
                "m_TemplateMaps", "m_NameLookup", "_templateMaps", "_nameLookup",
                "TemplateMaps", "NameLookup", "templateMaps", "nameLookup");

            if (mapField != null)
            {
                var outerDict = mapField.GetValue(singleton);
                if (outerDict != null)
                {
                    var il2cppType = Il2CppType.From(templateType);
                    if (TryAddToOuterDictionary(gameAssembly, templateType, outerDict, il2cppType, castClone, name))
                        return true;
                }
            }

            // Second approach: scan all dictionary fields for compatible types
            var fields = loaderType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                if (!fieldType.IsGenericType) continue;

                // Look for Dictionary<Type, Dictionary<string, T>>
                // In IL2CPP, the type names will be different, so check by structure
                var genArgs = fieldType.GetGenericArguments();
                if (genArgs.Length != 2) continue;

                // Check if first arg is Type-like (System.Type or Il2CppSystem.Type)
                var firstArgName = genArgs[0].Name;
                if (!firstArgName.Contains("Type")) continue;

                // Check if second arg is also a Dictionary
                if (!genArgs[1].IsGenericType) continue;
                var innerGenDef = genArgs[1].GetGenericTypeDefinition();
                var innerDefName = innerGenDef.Name;
                if (!innerDefName.StartsWith("Dictionary")) continue;

                var outerDict = field.GetValue(singleton);
                if (outerDict == null) continue;

                var il2cppType = Il2CppType.From(templateType);
                if (TryAddToOuterDictionary(gameAssembly, templateType, outerDict, il2cppType, castClone, name))
                    return true;
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    TryRegisterViaReflection: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Try to add a clone to an outer dictionary (Type -> inner dict) and then
    /// to the inner dictionary (string -> DataTemplate).
    /// If the inner dictionary doesn't exist, calls GetAll&lt;T&gt;() to force load
    /// templates and retries once.
    /// </summary>
    private bool TryAddToOuterDictionary(Assembly gameAssembly, Type templateType, object outerDict, object il2cppType, object castClone, string name)
    {
        try
        {
            var outerDictType = outerDict.GetType();
            var tryGetMethod = outerDictType.GetMethod("TryGetValue");

            if (tryGetMethod == null)
                return false;

            // Check if inner dict exists for this template type
            var parameters = new object[] { il2cppType, null };
            var exists = (bool)tryGetMethod.Invoke(outerDict, parameters);

            object innerDict;
            if (exists)
            {
                innerDict = parameters[1];
            }
            else
            {
                // Inner dictionary doesn't exist for this type
                // Force load templates via GetAll<T>() and retry
                SdkLogger.Msg($"    No inner dictionary for {templateType.Name}, forcing GetAll<T>()...");
                EnsureTemplatesLoaded(gameAssembly, templateType);

                // Retry the lookup
                parameters = new object[] { il2cppType, null };
                exists = (bool)tryGetMethod.Invoke(outerDict, parameters);

                if (!exists)
                {
                    SdkLogger.Warning($"    Still no inner dictionary after GetAll<T>() - template type may not be registered");
                    return false;
                }

                innerDict = parameters[1];
            }

            if (innerDict == null)
                return false;

            // Add to inner dictionary using indexer: innerDict[name] = castClone
            var innerDictType = innerDict.GetType();
            var innerIndexer = innerDictType.GetProperty("Item");
            if (innerIndexer != null)
            {
                innerIndexer.SetValue(innerDict, castClone, new object[] { name });
                return true;
            }

            // Try Add method as fallback
            var addMethod = innerDictType.GetMethod("Add");
            if (addMethod != null)
            {
                addMethod.Invoke(innerDict, new object[] { name, castClone });
                return true;
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    TryAddToOuterDictionary: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Find an instance field by trying multiple name variants.
    /// </summary>
    private static FieldInfo FindInstanceField(Type type, params string[] names)
    {
        foreach (var name in names)
        {
            var field = type.GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field;
        }
        return null;
    }

    /// <summary>
    /// Register clone templates that were loaded from compiled bundles with DataTemplateLoader.
    /// This handles clones created by BundleCompiler's raw binary cloning approach.
    /// </summary>
    private void RegisterBundleClones()
    {
        // Collect all clone names from all loaded modpacks
        var clonesByType = new Dictionary<string, HashSet<string>>();

        foreach (var modpack in _loadedModpacks.Values)
        {
            if (modpack.Clones == null) continue;
            foreach (var (templateTypeName, cloneMap) in modpack.Clones)
            {
                if (!clonesByType.TryGetValue(templateTypeName, out var cloneNames))
                {
                    cloneNames = new HashSet<string>();
                    clonesByType[templateTypeName] = cloneNames;
                }
                foreach (var cloneName in cloneMap.Keys)
                {
                    cloneNames.Add(cloneName);
                }
            }
        }

        if (clonesByType.Count == 0)
            return;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            SdkLogger.Warning("RegisterBundleClones: Assembly-CSharp not found");
            return;
        }

        int registered = 0;

        foreach (var (templateTypeName, cloneNames) in clonesByType)
        {
            var templateType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == templateTypeName && !t.IsAbstract);

            if (templateType == null)
            {
                SdkLogger.Warning($"  RegisterBundleClones: type '{templateTypeName}' not found");
                continue;
            }

            // Ensure the game has loaded templates of this type
            EnsureTemplatesLoaded(gameAssembly, templateType);

            foreach (var cloneName in cloneNames)
            {
                // Skip if already registered via runtime cloning
                var cloneKey = $"bundle:{templateTypeName}:{cloneName}";
                if (_appliedCloneKeys.Contains(cloneKey))
                    continue;

                // Try to find this clone in BundleLoader
                var bundleAsset = BundleLoader.GetAsset(cloneName);
                if (bundleAsset == null)
                {
                    // Not in bundle - will be created via runtime cloning
                    continue;
                }

                try
                {
                    // Cast to the template type
                    var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
                    var castClone = genericTryCast.Invoke(bundleAsset, null);
                    if (castClone == null)
                    {
                        SdkLogger.Warning($"  RegisterBundleClones: cast failed for '{cloneName}'");
                        continue;
                    }

                    // Register in DataTemplateLoader
                    RegisterInLoader(gameAssembly, bundleAsset, templateType, cloneName);
                    _appliedCloneKeys.Add(cloneKey);
                    registered++;

                    SdkLogger.Msg($"  Registered bundle clone: {cloneName} ({templateTypeName})");
                }
                catch (Exception ex)
                {
                    SdkLogger.Warning($"  RegisterBundleClones '{cloneName}': {ex.Message}");
                }
            }
        }

        if (registered > 0)
        {
            SdkLogger.Msg($"Registered {registered} clone(s) from compiled bundle");
            InvalidateNameLookupCache();
        }
    }
}
