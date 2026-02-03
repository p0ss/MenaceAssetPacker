using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// Template cloning: deep-copies existing game templates (ScriptableObjects) via
/// UnityEngine.Object.Instantiate() and registers them in the DataTemplateLoader
/// registry so the game treats them as first-class templates.
/// </summary>
public partial class ModpackLoaderMod
{
    // Tracks which modpack+templateType clone sets have been applied
    private readonly HashSet<string> _appliedCloneKeys = new();

    /// <summary>
    /// Process all clone definitions in a modpack. Returns true if all types were found.
    /// </summary>
    private bool ApplyClones(Modpack modpack)
    {
        if (modpack.Clones == null || modpack.Clones.Count == 0)
            return true;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            LoggerInstance.Error("Assembly-CSharp not found, cannot apply clones");
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
                    LoggerInstance.Warning($"  Clone: template type '{templateTypeName}' not found");
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
                    LoggerInstance.Warning($"  Clone: no {templateTypeName} instances found — will retry on next scene");
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
                        LoggerInstance.Msg($"  Clone: '{newName}' already exists, skipping");
                        clonedCount++;
                        continue;
                    }

                    if (!lookup.TryGetValue(sourceName, out var source))
                    {
                        LoggerInstance.Warning($"  Clone: source '{sourceName}' not found for clone '{newName}'");
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

                        LoggerInstance.Msg($"  Cloned: {sourceName} -> {newName}");
                        clonedCount++;
                    }
                    catch (Exception ex)
                    {
                        LoggerInstance.Error($"  Clone failed: {sourceName} -> {newName}: {ex.Message}");
                    }
                }

                if (clonedCount > 0)
                {
                    LoggerInstance.Msg($"  Applied {clonedCount} clone(s) for {templateTypeName}");
                    _appliedCloneKeys.Add(cloneKey);
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"  Failed to process clones for {templateTypeName}: {ex.Message}");
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
                LoggerInstance.Warning("  DataTemplateLoader class not found in Assembly-CSharp");
                return;
            }

            var getAllMethod = loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition);

            if (getAllMethod == null)
            {
                LoggerInstance.Warning("  DataTemplateLoader.GetAll method not found");
                return;
            }

            var genericMethod = getAllMethod.MakeGenericMethod(templateType);
            genericMethod.Invoke(null, null);
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"  EnsureTemplatesLoaded({templateType.Name}): {ex.Message}");
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
                LoggerInstance.Warning($"  SetTemplateId: m_ID field not found on {clone.name}");
                return;
            }

            uint offset = IL2CPP.il2cpp_field_get_offset(idField);
            if (offset == 0)
            {
                LoggerInstance.Warning($"  SetTemplateId: m_ID offset is 0 for {clone.name}");
                return;
            }

            // Write the IL2CPP string pointer at the field offset
            IntPtr il2cppString = IL2CPP.ManagedStringToIl2Cpp(id);
            Marshal.WriteIntPtr(objectPointer + (int)offset, il2cppString);
        }
        catch (Exception ex)
        {
            LoggerInstance.Error($"  SetTemplateId failed for {clone.name}: {ex.Message}");
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
    /// Uses managed reflection on the IL2CPP proxy type first, falls back gracefully.
    /// </summary>
    private void RegisterInLoader(Assembly gameAssembly, UnityEngine.Object clone, Type templateType, string name)
    {
        try
        {
            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataTemplateLoader");

            if (loaderType == null)
            {
                LoggerInstance.Warning("  RegisterInLoader: DataTemplateLoader not found");
                return;
            }

            // Strategy 1: Try to use the public TryGet<T> to verify registration isn't needed,
            // and if so, use Add/Register method if available
            // Strategy 2: Access internal dictionaries via reflection

            // Get the singleton instance
            object singleton = null;
            var singletonProp = loaderType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (singletonProp != null)
            {
                singleton = singletonProp.GetValue(null);
            }
            else
            {
                // Try GetSingleton or s_Singleton field
                var getSingleton = loaderType.GetMethod("GetSingleton",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (getSingleton != null)
                {
                    singleton = getSingleton.Invoke(null, null);
                }
                else
                {
                    var singletonField = loaderType.GetField("s_Singleton",
                        BindingFlags.NonPublic | BindingFlags.Static) ??
                        loaderType.GetField("_instance",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    if (singletonField != null)
                        singleton = singletonField.GetValue(null);
                }
            }

            if (singleton == null)
            {
                LoggerInstance.Warning("  RegisterInLoader: could not get DataTemplateLoader singleton");
                return;
            }

            // Look for the template maps dictionary: m_TemplateMaps or similar
            // It's typically Dictionary<Type, Dictionary<string, DataTemplate>> or similar
            var mapsField = FindInstanceField(loaderType,
                "m_TemplateMaps", "_templateMaps", "m_templateMaps", "TemplateMaps");

            if (mapsField != null)
            {
                var maps = mapsField.GetValue(singleton);
                if (maps != null)
                {
                    if (TryAddToTemplateMaps(maps, templateType, clone, name))
                        return;
                }
            }

            // Fallback: look for any Dictionary field and try to add
            var allFields = loaderType.GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in allFields)
            {
                var fieldType = field.FieldType;
                if (!fieldType.IsGenericType) continue;

                var genDef = fieldType.GetGenericTypeDefinition();
                var genArgs = fieldType.GetGenericArguments();

                // Look for Dictionary<Type, Dictionary<string, T>> pattern
                if (genDef.Name.Contains("Dictionary") && genArgs.Length == 2)
                {
                    var maps = field.GetValue(singleton);
                    if (maps != null && TryAddToTemplateMaps(maps, templateType, clone, name))
                    {
                        LoggerInstance.Msg($"    Registered '{name}' via field '{field.Name}'");
                        return;
                    }
                }
            }

            LoggerInstance.Warning($"  RegisterInLoader: could not find template registry to add '{name}' — " +
                "clone exists in memory but may not be findable via DataTemplateLoader.Get()");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"  RegisterInLoader failed for '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Try to add a clone to the outer template maps dictionary.
    /// The maps object is expected to be Dictionary-like with Type keys
    /// and inner Dictionary-like values with string keys.
    /// </summary>
    private bool TryAddToTemplateMaps(object maps, Type templateType, UnityEngine.Object clone, string name)
    {
        try
        {
            var mapsType = maps.GetType();

            // Try to get the inner dictionary for this template type using indexer or TryGetValue
            object innerDict = null;

            // Try indexer: maps[templateType]
            var indexer = mapsType.GetProperty("Item");
            if (indexer != null)
            {
                try
                {
                    innerDict = indexer.GetValue(maps, new object[] { templateType });
                }
                catch
                {
                    // Key doesn't exist yet — try to find the Il2CppType version
                    try
                    {
                        var il2cppType = Il2CppType.From(templateType);
                        innerDict = indexer.GetValue(maps, new object[] { il2cppType });
                    }
                    catch { }
                }
            }

            if (innerDict == null)
                return false;

            // Cast clone to the correct template type for the inner dictionary
            var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
            var castClone = genericTryCast.Invoke(clone, null);
            if (castClone == null)
                return false;

            // Add to inner dictionary: innerDict[name] = castClone
            var innerType = innerDict.GetType();
            var innerIndexer = innerType.GetProperty("Item");
            if (innerIndexer != null)
            {
                innerIndexer.SetValue(innerDict, castClone, new object[] { name });
                return true;
            }

            // Try Add method
            var addMethod = innerType.GetMethod("Add", new[] { typeof(string), templateType }) ??
                            innerType.GetMethod("set_Item");
            if (addMethod != null)
            {
                addMethod.Invoke(innerDict, new object[] { name, castClone });
                return true;
            }
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"    TryAddToTemplateMaps: {ex.Message}");
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
}
