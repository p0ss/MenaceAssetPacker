using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using Menace.SDK;
using Menace.SDK.Internal;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// Template injection via IL2CPP reflection.
/// Used as a fallback for modpacks that don't have compiled asset bundles.
/// Once the bundle compiler (Phase 5) produces real asset bundles, this path
/// becomes unnecessary — bundles apply template changes via Unity's native deserialization.
/// </summary>
public partial class ModpackLoaderMod
{
    private static readonly MethodInfo TryCastMethod = typeof(Il2CppObjectBase).GetMethod("TryCast");

    // Properties that should not be modified (internal Unity/IL2CPP fields)
    private static readonly HashSet<string> ReadOnlyProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pointer", "ObjectClass", "WasCollected", "m_CachedPtr",
        "name", "m_ID", "hideFlags", "serializationData"
    };

    // Computed/translated fields that derive from other editable fields
    // These are skipped with an informative message rather than a warning
    private static readonly HashSet<string> TranslatedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "DisplayTitle", "DisplayShortName", "DisplayDescription",
        // Icon properties computed from internal Sprite reference
        "HasIcon", "IconAssetName"
    };

    // Cache for name -> Object lookups, keyed by element type
    private readonly Dictionary<Type, Dictionary<string, UnityEngine.Object>> _nameLookupCache = new();

    // Keep runtime-created sprites and textures alive to prevent garbage collection
    private readonly List<Sprite> _runtimeSprites = new();
    private readonly List<Texture2D> _runtimeTextures = new();

    /// <summary>
    /// Clear the name lookup cache. Call this after creating clones so that
    /// subsequent patch operations will rebuild the cache and find the new clones.
    /// </summary>
    public void InvalidateNameLookupCache()
    {
        if (_nameLookupCache.Count > 0)
        {
            SdkLogger.Msg($"  Invalidating name lookup cache ({_nameLookupCache.Count} type(s))");
            _nameLookupCache.Clear();
        }
    }

    private enum CollectionKind { None, StructArray, ReferenceArray, Il2CppList, ManagedArray }

    private static CollectionKind ClassifyCollectionType(Type propType, out Type elementType)
    {
        elementType = null;

        // Check generic types first
        if (propType.IsGenericType)
        {
            var genName = propType.GetGenericTypeDefinition().Name;
            var args = propType.GetGenericArguments();

            if (genName.StartsWith("Il2CppStructArray"))
            {
                elementType = args[0];
                return CollectionKind.StructArray;
            }

            if (genName.StartsWith("Il2CppReferenceArray"))
            {
                elementType = args[0];
                return CollectionKind.ReferenceArray;
            }

            // IL2CPP List detection
            if (genName.Contains("List"))
            {
                var isIl2Cpp = propType.FullName?.Contains("Il2Cpp") == true
                               || IsIl2CppType(propType);
                if (isIl2Cpp)
                {
                    elementType = args[0];
                    return CollectionKind.Il2CppList;
                }
            }
        }

        // Il2CppStringArray is non-generic but extends Il2CppReferenceArray<string>
        if (propType.Name == "Il2CppStringArray")
        {
            elementType = typeof(string);
            return CollectionKind.ReferenceArray;
        }

        // Managed arrays
        if (propType.IsArray)
        {
            elementType = propType.GetElementType();
            return CollectionKind.ManagedArray;
        }

        // Walk base types to detect IL2CPP collections on derived types
        var baseType = propType.BaseType;
        while (baseType != null && baseType != typeof(object) && baseType != typeof(Il2CppObjectBase))
        {
            if (baseType.IsGenericType)
            {
                var baseName = baseType.GetGenericTypeDefinition().Name;
                var baseArgs = baseType.GetGenericArguments();

                if (baseName.StartsWith("Il2CppStructArray"))
                {
                    elementType = baseArgs[0];
                    return CollectionKind.StructArray;
                }
                if (baseName.StartsWith("Il2CppReferenceArray"))
                {
                    elementType = baseArgs[0];
                    return CollectionKind.ReferenceArray;
                }
            }
            baseType = baseType.BaseType;
        }

        return CollectionKind.None;
    }

    private static bool IsIl2CppType(Type type)
    {
        var current = type;
        while (current != null)
        {
            if (current == typeof(Il2CppObjectBase))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Check if a type is a localization wrapper (LocalizedLine, LocalizedMultiLine, BaseLocalizedString).
    /// These types store the actual text in m_DefaultTranslation at offset +0x38.
    /// </summary>
    private static bool IsLocalizationType(Type type)
    {
        if (type == null) return false;

        // Check by name (faster than walking inheritance for common cases)
        var name = type.Name;
        if (name == "LocalizedLine" || name == "LocalizedMultiLine" || name == "BaseLocalizedString")
            return true;

        // Walk inheritance chain to find BaseLocalizedString
        var current = type.BaseType;
        while (current != null && current != typeof(object) && current != typeof(Il2CppObjectBase))
        {
            if (current.Name == "BaseLocalizedString")
                return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Field names that are known to be localization fields.
    /// Used as a fallback when type detection fails for IL2CPP wrapped types.
    /// </summary>
    private static readonly HashSet<string> KnownLocalizationFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Title", "ShortName", "Description", "Text", "DisplayText",
        "TooltipText", "Label", "Name", "Message", "Hint"
    };

    /// <summary>
    /// Check if a field is likely a localization field by name.
    /// Used when the C# type doesn't match but we need to handle localization specially.
    /// </summary>
    private static bool IsLikelyLocalizationField(string fieldName)
    {
        return KnownLocalizationFieldNames.Contains(fieldName);
    }

    /// <summary>
    /// Try to detect if an IL2CPP object's property actually holds a localization type at runtime.
    /// This catches cases where the C# property type is wrong but the runtime object is a LocalizedLine.
    /// </summary>
    private static bool IsRuntimeLocalizationType(object value)
    {
        if (value == null) return false;

        // If it's an IL2CPP object, check its actual runtime class
        if (value is Il2CppObjectBase il2cppObj)
        {
            try
            {
                var ptr = il2cppObj.Pointer;
                if (ptr == IntPtr.Zero) return false;

                var klassPtr = IL2CPP.il2cpp_object_get_class(ptr);
                if (klassPtr == IntPtr.Zero) return false;

                var namePtr = IL2CPP.il2cpp_class_get_name(klassPtr);
                if (namePtr == IntPtr.Zero) return false;

                var className = Marshal.PtrToStringAnsi(namePtr);
                return className == "LocalizedLine" || className == "LocalizedMultiLine" ||
                       className == "BaseLocalizedString";
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    // Memory layout offsets for BaseLocalizedString (from reverse engineering)
    private const int LOC_CATEGORY_OFFSET = 0x10;           // int LocaCategory
    private const int LOC_KEY_PART1_OFFSET = 0x18;          // string m_KeyPart1
    private const int LOC_KEY_PART2_OFFSET = 0x20;          // string m_KeyPart2
    private const int LOC_CATEGORY_NAME_OFFSET = 0x28;      // string m_CategoryName
    private const int LOC_IDENTIFIER_OFFSET = 0x30;         // string m_Identifier
    private const int LOC_DEFAULT_TRANSLATION_OFFSET = 0x38; // string m_DefaultTranslation
    private const int LOC_HAS_PLACEHOLDERS_OFFSET = 0x40;   // bool hasPlaceholders

    // Cache for LocalizedLine/LocalizedMultiLine class pointers
    private static IntPtr _localizedLineClass = IntPtr.Zero;
    private static IntPtr _localizedMultiLineClass = IntPtr.Zero;

    /// <summary>
    /// Get the IL2CPP class pointer for LocalizedLine.
    /// </summary>
    private static IntPtr GetLocalizedLineClass()
    {
        if (_localizedLineClass != IntPtr.Zero)
            return _localizedLineClass;

        _localizedLineClass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "Menace.Tools", "LocalizedLine");
        return _localizedLineClass;
    }

    /// <summary>
    /// Get the IL2CPP class pointer for LocalizedMultiLine.
    /// </summary>
    private static IntPtr GetLocalizedMultiLineClass()
    {
        if (_localizedMultiLineClass != IntPtr.Zero)
            return _localizedMultiLineClass;

        _localizedMultiLineClass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "Menace.Tools", "LocalizedMultiLine");
        return _localizedMultiLineClass;
    }

    /// <summary>
    /// Create a new LocalizedLine or LocalizedMultiLine object with the given text.
    /// Creates a FRESH instance to avoid corrupting shared localization objects.
    /// </summary>
    private IntPtr CreateLocalizedObject(IntPtr existingLocPtr, string value)
    {
        try
        {
            // Get the class of the existing object to create the same type
            var existingClass = IL2CPP.il2cpp_object_get_class(existingLocPtr);
            if (existingClass == IntPtr.Zero)
            {
                SdkLogger.Warning("    CreateLocalizedObject: could not get class of existing object");
                return IntPtr.Zero;
            }

            // Get the class name to determine if it's LocalizedLine or LocalizedMultiLine
            var classNamePtr = IL2CPP.il2cpp_class_get_name(existingClass);
            var className = classNamePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(classNamePtr) : "";

            // Get the appropriate class pointer
            IntPtr newClass;
            if (className == "LocalizedMultiLine")
            {
                newClass = GetLocalizedMultiLineClass();
            }
            else
            {
                // Default to LocalizedLine for LocalizedLine or BaseLocalizedString
                newClass = GetLocalizedLineClass();
            }

            if (newClass == IntPtr.Zero)
            {
                SdkLogger.Warning($"    CreateLocalizedObject: could not find class for {className}");
                return IntPtr.Zero;
            }

            // Allocate a new instance
            var newObj = IL2CPP.il2cpp_object_new(newClass);
            if (newObj == IntPtr.Zero)
            {
                SdkLogger.Warning("    CreateLocalizedObject: il2cpp_object_new returned null");
                return IntPtr.Zero;
            }

            // IMPORTANT: Clear the key fields so localization lookup FAILS
            // This forces the game to use m_DefaultTranslation as the actual text
            // If we copy the original key, the game's localization system will
            // look it up and OVERWRITE m_DefaultTranslation with cached text

            // Clear LocaCategory (int at +0x10) - set to 0 (None/Invalid)
            Marshal.WriteInt32(newObj + LOC_CATEGORY_OFFSET, 0);

            // Clear m_KeyPart1 (string at +0x18) - set to null
            Marshal.WriteIntPtr(newObj + LOC_KEY_PART1_OFFSET, IntPtr.Zero);

            // Clear m_KeyPart2 (string at +0x20) - set to null
            Marshal.WriteIntPtr(newObj + LOC_KEY_PART2_OFFSET, IntPtr.Zero);

            // Clear m_CategoryName (string at +0x28) - set to null
            Marshal.WriteIntPtr(newObj + LOC_CATEGORY_NAME_OFFSET, IntPtr.Zero);

            // Clear m_Identifier (string at +0x30) - set to null
            Marshal.WriteIntPtr(newObj + LOC_IDENTIFIER_OFFSET, IntPtr.Zero);

            // Set m_DefaultTranslation to our new value (string at +0x38)
            IntPtr il2cppStr = IntPtr.Zero;
            if (!string.IsNullOrEmpty(value))
            {
                il2cppStr = IL2CPP.ManagedStringToIl2Cpp(value);
            }
            Marshal.WriteIntPtr(newObj + LOC_DEFAULT_TRANSLATION_OFFSET, il2cppStr);

            // Copy hasPlaceholders (bool at +0x40)
            byte hasPlaceholders = Marshal.ReadByte(existingLocPtr + LOC_HAS_PLACEHOLDERS_OFFSET);
            Marshal.WriteByte(newObj + LOC_HAS_PLACEHOLDERS_OFFSET, hasPlaceholders);

            return newObj;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    CreateLocalizedObject failed: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Write a localized string to a template's localization field.
    /// Creates a NEW localization object to avoid corrupting shared instances.
    ///
    /// IMPORTANT: The old approach modified the shared LocalizedLine/LocalizedMultiLine
    /// instances directly, which caused random text corruption across unrelated templates
    /// that shared the same localization key. This new approach creates a fresh object
    /// for each modified field.
    /// </summary>
    private bool WriteLocalizedFieldDirect(Il2CppObjectBase templateObj, string fieldName, string value)
    {
        try
        {
            var templatePtr = templateObj.Pointer;
            if (templatePtr == IntPtr.Zero)
                return false;

            // Get the class pointer for the template
            var klassPtr = IL2CPP.il2cpp_object_get_class(templatePtr);
            if (klassPtr == IntPtr.Zero)
                return false;

            // Find the field offset for the localization property
            var fieldOffset = OffsetCache.GetOrResolve(klassPtr, fieldName);
            if (fieldOffset == 0)
            {
                SdkLogger.Warning($"    {fieldName}: could not find field offset");
                return false;
            }

            // Read the existing localization object pointer
            var existingLocPtr = Marshal.ReadIntPtr(templatePtr + (int)fieldOffset);
            if (existingLocPtr == IntPtr.Zero)
            {
                SdkLogger.Warning($"    {fieldName}: localization object is null");
                return false;
            }

            // Validate the pointer looks reasonable
            if (existingLocPtr.ToInt64() < 0x10000)
            {
                SdkLogger.Warning($"    {fieldName}: invalid localization pointer");
                return false;
            }

            // Create a NEW localization object with our text
            // This avoids corrupting shared localization instances
            var newLocPtr = CreateLocalizedObject(existingLocPtr, value);
            if (newLocPtr == IntPtr.Zero)
            {
                SdkLogger.Warning($"    {fieldName}: failed to create new localization object");
                return false;
            }

            // Write the NEW object pointer to the template's field
            Marshal.WriteIntPtr(templatePtr + (int)fieldOffset, newLocPtr);

            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    WriteLocalizedFieldDirect({fieldName}) failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Write a localized string via reflection, creating a NEW object to avoid corruption.
    /// Used for fallback cases where the parent is not a direct IL2CPP object.
    /// </summary>
    private bool WriteLocalizedFieldViaReflection(object parent, PropertyInfo prop, FieldInfo field, string fieldName, string value)
    {
        try
        {
            // Get the existing localization object
            object existingLoc = prop != null ? prop.GetValue(parent) : field?.GetValue(parent);
            if (existingLoc == null || !(existingLoc is Il2CppObjectBase il2cppLoc))
            {
                SdkLogger.Warning($"    {fieldName}: existing localization is null or not IL2CPP");
                return false;
            }

            var existingPtr = il2cppLoc.Pointer;
            if (existingPtr == IntPtr.Zero)
            {
                SdkLogger.Warning($"    {fieldName}: existing localization pointer is null");
                return false;
            }

            // Create a NEW localization object with our text
            var newLocPtr = CreateLocalizedObject(existingPtr, value);
            if (newLocPtr == IntPtr.Zero)
            {
                SdkLogger.Warning($"    {fieldName}: failed to create new localization object");
                return false;
            }

            // Wrap the new pointer in the appropriate managed type and set it back
            var locType = existingLoc.GetType();
            var wrappedNew = Activator.CreateInstance(locType, newLocPtr);
            if (wrappedNew == null)
            {
                SdkLogger.Warning($"    {fieldName}: failed to wrap new localization object");
                return false;
            }

            if (prop != null && prop.CanWrite)
                prop.SetValue(parent, wrappedNew);
            else if (field != null)
                field.SetValue(parent, wrappedNew);
            else
            {
                SdkLogger.Warning($"    {fieldName}: no writable property or field");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    WriteLocalizedFieldViaReflection({fieldName}) failed: {ex.Message}");
            return false;
        }
    }

    private Dictionary<string, UnityEngine.Object> BuildNameLookup(Type elementType)
    {
        if (_nameLookupCache.TryGetValue(elementType, out var cached))
            return cached;

        var lookup = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // For Sprite type, first add any custom sprites from AssetReplacer
            // These are runtime-created and won't be found by FindObjectsOfTypeAll
            if (elementType == typeof(Sprite))
            {
                // Get all custom sprites and add them to the lookup
                var customSpriteNames = AssetReplacer.GetCustomSpriteNames();
                foreach (var name in customSpriteNames)
                {
                    var sprite = AssetReplacer.GetCustomSprite(name);
                    if (sprite != null)
                        lookup[name] = sprite;
                }
                if (customSpriteNames.Count > 0)
                    SdkLogger.Msg($"    Added {customSpriteNames.Count} custom sprite(s) to lookup");
            }

            // Check BundleLoader for custom assets (GLB models, audio, etc.)
            // These are runtime-loaded and won't be found by FindObjectsOfTypeAll
            // Try multiple type name variants to handle IL2CPP naming differences
            try
            {
                var simpleTypeName = elementType.Name;
                string il2cppTypeName;
                try
                {
                    il2cppTypeName = Il2CppType.From(elementType)?.Name ?? simpleTypeName;
                }
                catch
                {
                    il2cppTypeName = simpleTypeName;
                }

                var typeNamesToTry = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { il2cppTypeName, simpleTypeName };

                var addedCount = 0;
                foreach (var typeName in typeNamesToTry)
                {
                    // Check compiled assets (loaded from manifest via Resources.Load)
                    var compiledAssets = CompiledAssetLoader.GetAssetsByType(typeName);
                    foreach (var asset in compiledAssets)
                    {
                        if (asset != null && !string.IsNullOrEmpty(asset.name) && !lookup.ContainsKey(asset.name))
                        {
                            lookup[asset.name] = asset;
                            addedCount++;
                        }
                    }
                }
                if (addedCount > 0)
                    SdkLogger.Msg($"    Added {addedCount} custom {simpleTypeName}(s) to lookup");

                // For Sprites: create runtime sprites from PNG files in compiled/textures
                // Asset-file sprites have complex vertex/UV data that's hard to generate correctly,
                // and asset-file textures have ColorSpace issues (always washed out)
                // Runtime loading via ImageConversion.LoadImage works correctly for both
                if (elementType == typeof(Sprite))
                {
                    int runtimeSpriteCount = 0;

                    // Scan compiled/textures directory for PNG files
                    var modsPath = Path.Combine(Directory.GetCurrentDirectory(), "Mods");
                    var texturesDir = Path.Combine(modsPath, "compiled", "textures");

                    if (Directory.Exists(texturesDir))
                    {
                        var pngFiles = Directory.GetFiles(texturesDir, "*.png");
                        SdkLogger.Msg($"    Found {pngFiles.Length} PNG file(s) in compiled/textures");

                        foreach (var pngPath in pngFiles)
                        {
                            var textureName = Path.GetFileNameWithoutExtension(pngPath);

                            // Skip if we already have a sprite for this texture
                            if (lookup.ContainsKey(textureName)) continue;

                            try
                            {
                                // Load PNG file as Texture2D using ImageConversion
                                var bytes = File.ReadAllBytes(pngPath);
                                var tex = new Texture2D(2, 2);
                                var il2cppBytes = new Il2CppStructArray<byte>(bytes);

                                if (ImageConversion.LoadImage(tex, il2cppBytes))
                                {
                                    tex.name = textureName;

                                    // Create a runtime sprite from the texture
                                    var rect = new Rect(0, 0, tex.width, tex.height);
                                    var pivot = new Vector2(0.5f, 0.5f);
                                    var sprite = Sprite.Create(tex, rect, pivot, 100f);

                                    if (sprite != null)
                                    {
                                        sprite.name = textureName;
                                        lookup[textureName] = sprite;

                                        // Cache to prevent garbage collection
                                        _runtimeSprites.Add(sprite);
                                        _runtimeTextures.Add(tex);
                                        runtimeSpriteCount++;

                                        SdkLogger.Msg($"      Loaded sprite: '{textureName}' ({tex.width}x{tex.height})");
                                    }
                                }
                                else
                                {
                                    SdkLogger.Warning($"    Failed to load texture from '{pngPath}'");
                                }
                            }
                            catch (Exception ex)
                            {
                                SdkLogger.Warning($"    Failed to create sprite for '{textureName}': {ex.Message}");
                            }
                        }
                    }

                    if (runtimeSpriteCount > 0)
                        SdkLogger.Msg($"    Created {runtimeSpriteCount} sprite(s) from PNG files");
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Warning($"    BundleLoader lookup failed for {elementType.Name}: {ex.Message}");
            }

            // Force-load templates via DataTemplateLoader before FindObjectsOfTypeAll
            // This ensures referenced templates are in memory
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (gameAssembly != null)
                EnsureTemplatesLoaded(gameAssembly, elementType);

            var il2cppType = Il2CppType.From(elementType);
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);

            if (objects != null)
            {
                foreach (var obj in objects)
                {
                    if (obj != null && !string.IsNullOrEmpty(obj.name))
                    {
                        // Don't overwrite custom sprites with game sprites of same name
                        if (!lookup.ContainsKey(obj.name))
                            lookup[obj.name] = obj;
                    }
                }
            }

            SdkLogger.Msg($"    Built name lookup for {elementType.Name}: {lookup.Count} entries");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    Failed to build name lookup for {elementType.Name}: {ex.Message}");
        }

        _nameLookupCache[elementType] = lookup;
        return lookup;
    }

    private void ApplyTemplateModifications(UnityEngine.Object obj, Type templateType, Dictionary<string, object> modifications)
    {
        // Cast to the correct proxy type via TryCast<T>()
        object castObj;
        try
        {
            var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
            castObj = genericTryCast.Invoke(obj, null);
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"    TryCast failed for {obj.name}: {ex.Message}");
            return;
        }

        if (castObj == null)
        {
            SdkLogger.Error($"    TryCast returned null for {obj.name}");
            return;
        }

        // Build property lookup for this type (walk inheritance chain)
        var propertyMap = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        var currentType = templateType;
        while (currentType != null && currentType.Name != "Object" &&
               currentType != typeof(Il2CppObjectBase))
        {
            var props = currentType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var prop in props)
            {
                if (prop.CanWrite && prop.CanRead && !propertyMap.ContainsKey(prop.Name))
                    propertyMap[prop.Name] = prop;
            }
            currentType = currentType.BaseType;
        }

        int appliedCount = 0;
        foreach (var (fieldName, rawValue) in modifications)
        {
            if (ReadOnlyProperties.Contains(fieldName))
                continue;

            // Skip translated/computed fields with informative message
            if (TranslatedFields.Contains(fieldName))
            {
                SdkLogger.Msg($"    {obj.name}: skipping {fieldName} (translated field - edit Title/ShortName/Description instead)");
                continue;
            }

            // Handle dotted paths (e.g., "Properties.HitpointsPerElement")
            // by navigating to the nested object and setting the sub-field.
            var dotIdx = fieldName.IndexOf('.');
            if (dotIdx > 0)
            {
                var parentFieldName = fieldName[..dotIdx];
                var childFieldName = fieldName[(dotIdx + 1)..];

                if (!propertyMap.TryGetValue(parentFieldName, out var parentProp))
                {
                    SdkLogger.Warning($"    {obj.name}: parent property '{parentFieldName}' not found on {templateType.Name}");
                    continue;
                }

                try
                {
                    var parentObj = parentProp.GetValue(castObj);
                    if (parentObj == null)
                    {
                        SdkLogger.Warning($"    {obj.name}.{parentFieldName} is null, cannot set '{childFieldName}'");
                        continue;
                    }

                    // Try to find a property first
                    var childProp = parentObj.GetType().GetProperty(childFieldName,
                        BindingFlags.Public | BindingFlags.Instance);

                    // If no property found, try to find a field (common for value-type structs like OperationResources)
                    FieldInfo childField = null;
                    if (childProp == null || !childProp.CanWrite)
                    {
                        childField = parentObj.GetType().GetField(childFieldName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (childField == null)
                        {
                            SdkLogger.Warning($"    {obj.name}: property/field '{childFieldName}' not found on {parentObj.GetType().Name}");
                            continue;
                        }
                    }

                    // Get the target type from either property or field
                    var childType = childProp?.PropertyType ?? childField.FieldType;

                    if (rawValue is JArray nestedJArray)
                    {
                        var kind = ClassifyCollectionType(childType, out _);
                        if (kind != CollectionKind.None)
                        {
                            if (childProp != null && TryApplyCollectionValue(parentObj, childProp, nestedJArray))
                                appliedCount++;
                            continue;
                        }
                    }

                    // Incremental list operations via JObject with $op
                    if (rawValue is JObject nestedJObj)
                    {
                        var nestedKind = ClassifyCollectionType(childType, out var nestedElType);
                        if (nestedKind == CollectionKind.Il2CppList && nestedElType != null)
                        {
                            if (childProp != null && TryApplyIncrementalList(parentObj, childProp, nestedJObj, nestedElType))
                                appliedCount++;
                            continue;
                        }
                    }

                    // Localization types: write directly to m_DefaultTranslation
                    // Use direct memory access if parentObj is an IL2CPP object to avoid crashes
                    //
                    // Detection strategy (same as ApplyFieldOverrides):
                    // 1. Check C# property type (fastest, catches most cases)
                    // 2. Check runtime type of current value (catches IL2CPP type mismatches)
                    // 3. Check field name for known localization patterns (fallback for polymorphic types)
                    var childCurrentValue = childProp?.CanRead == true ? childProp.GetValue(parentObj) : childField?.GetValue(parentObj);
                    bool isChildLocalization = IsLocalizationType(childType) ||
                                               IsRuntimeLocalizationType(childCurrentValue) ||
                                               (IsLikelyLocalizationField(childFieldName) && rawValue is JValue jVal && jVal.Type == JTokenType.String);

                    if (isChildLocalization)
                    {
                        var stringValue = rawValue is JToken jt ? jt.Value<string>() : rawValue?.ToString();
                        bool success = false;

                        if (parentObj is Il2CppObjectBase il2cppParent)
                        {
                            // Use safe direct memory access
                            success = WriteLocalizedFieldDirect(il2cppParent, childFieldName, stringValue);
                        }
                        else
                        {
                            // Fallback for managed value types - create new object via reflection
                            success = WriteLocalizedFieldViaReflection(parentObj, childProp, childField, childFieldName, stringValue);
                        }

                        if (success)
                        {
                            var detectionMethod = IsLocalizationType(childType) ? "type" :
                                                  IsRuntimeLocalizationType(childCurrentValue) ? "runtime" : "name";
                            SdkLogger.Msg($"    {obj.name}.{fieldName}: set localized text (detected by {detectionMethod})");
                            appliedCount++;
                        }
                        continue;
                    }

                    var nestedConverted = ConvertToPropertyType(rawValue, childType);

                    // For value-type structs (like OperationResources), we need to:
                    // 1. Get a boxed copy of the struct
                    // 2. Modify the field on the boxed copy
                    // 3. Set the modified struct back to the parent property
                    if (childField != null && parentProp.PropertyType.IsValueType)
                    {
                        // parentObj is already a boxed copy of the struct
                        childField.SetValue(parentObj, nestedConverted);
                        // Write the modified struct back to the parent property
                        parentProp.SetValue(castObj, parentObj);
                    }
                    else if (childField != null)
                    {
                        childField.SetValue(parentObj, nestedConverted);
                    }
                    else
                    {
                        childProp.SetValue(parentObj, nestedConverted);
                    }
                    appliedCount++;
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    SdkLogger.Error($"    {obj.name}.{fieldName}: {inner.GetType().Name}: {inner.Message}");
                }
                continue;
            }

            if (!propertyMap.TryGetValue(fieldName, out var prop))
            {
                SdkLogger.Warning($"    {obj.name}: property '{fieldName}' not found on {templateType.Name}");
                continue;
            }

            try
            {
                // Collection/array patch: JArray → full replacement
                if (rawValue is JArray jArray)
                {
                    var kind = ClassifyCollectionType(prop.PropertyType, out _);
                    if (kind != CollectionKind.None)
                    {
                        if (TryApplyCollectionValue(castObj, prop, jArray))
                            appliedCount++;
                        continue;
                    }
                }

                // Incremental list operations: JObject with $remove/$update/$append
                if (rawValue is JObject jObj)
                {
                    var collKind = ClassifyCollectionType(prop.PropertyType, out var elType);
                    if (collKind == CollectionKind.Il2CppList && elType != null)
                    {
                        if (TryApplyIncrementalList(castObj, prop, jObj, elType))
                            appliedCount++;
                        continue;
                    }
                }

                // Localization types (LocalizedLine, LocalizedMultiLine): write directly to m_DefaultTranslation
                // These are wrapper objects that can't be replaced with strings via normal property set
                // Use direct memory access to avoid property getter crashes
                //
                // Detection strategy (same as ApplyFieldOverrides):
                // 1. Check C# property type (fastest, catches most cases)
                // 2. Check runtime type of current value (catches IL2CPP type mismatches)
                // 3. Check field name for known localization patterns (fallback for polymorphic types)
                var topLevelCurrentValue = prop.CanRead ? prop.GetValue(castObj) : null;
                bool isTopLevelLocalization = IsLocalizationType(prop.PropertyType) ||
                                              IsRuntimeLocalizationType(topLevelCurrentValue) ||
                                              (IsLikelyLocalizationField(fieldName) && rawValue is JValue jVal && jVal.Type == JTokenType.String);

                if (isTopLevelLocalization)
                {
                    var stringValue = rawValue is JToken jt ? jt.Value<string>() : rawValue?.ToString();
                    if (castObj is Il2CppObjectBase il2cppCastObj &&
                        WriteLocalizedFieldDirect(il2cppCastObj, fieldName, stringValue))
                    {
                        var detectionMethod = IsLocalizationType(prop.PropertyType) ? "type" :
                                              IsRuntimeLocalizationType(topLevelCurrentValue) ? "runtime" : "name";
                        SdkLogger.Msg($"    {obj.name}.{fieldName}: set localized text (detected by {detectionMethod})");
                        appliedCount++;
                    }
                    continue;
                }

                var convertedValue = ConvertToPropertyType(rawValue, prop.PropertyType);
                prop.SetValue(castObj, convertedValue);
                appliedCount++;
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                SdkLogger.Error($"    {obj.name}.{fieldName}: {inner.GetType().Name}: {inner.Message}");
            }
        }

        if (appliedCount > 0)
        {
            SdkLogger.Msg($"    {obj.name}: set {appliedCount}/{modifications.Count} fields");
        }
    }

    private bool TryApplyCollectionValue(object castObj, PropertyInfo prop, JArray jArray)
    {
        var kind = ClassifyCollectionType(prop.PropertyType, out var elementType);
        if (kind == CollectionKind.None || elementType == null)
            return false;

        switch (kind)
        {
            case CollectionKind.StructArray:
                return ApplyStructArray(castObj, prop, jArray, elementType);
            case CollectionKind.ReferenceArray:
                return ApplyReferenceArray(castObj, prop, jArray, elementType);
            case CollectionKind.Il2CppList:
                return ApplyIl2CppList(castObj, prop, jArray, elementType);
            case CollectionKind.ManagedArray:
                return ApplyManagedArray(castObj, prop, jArray, elementType);
            default:
                return false;
        }
    }

    private bool ApplyStructArray(object castObj, PropertyInfo prop, JArray jArray, Type elementType)
    {
        var arrayType = prop.PropertyType;
        var array = Activator.CreateInstance(arrayType, new object[] { jArray.Count });

        var indexer = arrayType.GetProperty("Item");
        if (indexer == null)
        {
            SdkLogger.Warning($"    {prop.Name}: no indexer found on {arrayType.Name}");
            return false;
        }

        for (int i = 0; i < jArray.Count; i++)
        {
            var converted = ConvertJTokenToType(jArray[i], elementType);
            indexer.SetValue(array, converted, new object[] { i });
        }

        prop.SetValue(castObj, array);
        SdkLogger.Msg($"    {prop.Name}: set StructArray<{elementType.Name}>[{jArray.Count}]");
        return true;
    }

    private bool ApplyReferenceArray(object castObj, PropertyInfo prop, JArray jArray, Type elementType)
    {
        var arrayType = prop.PropertyType;

        // UnityEngine.Object references: resolve by name
        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
        {
            var lookup = BuildNameLookup(elementType);
            var array = Activator.CreateInstance(arrayType, new object[] { jArray.Count });
            var indexer = arrayType.GetProperty("Item");
            if (indexer == null) return false;

            for (int i = 0; i < jArray.Count; i++)
            {
                var name = jArray[i].Value<string>();
                if (name != null && lookup.TryGetValue(name, out var resolved))
                {
                    var castMethod = TryCastMethod.MakeGenericMethod(elementType);
                    var castElement = castMethod.Invoke(resolved, null);
                    indexer.SetValue(array, castElement, new object[] { i });
                }
                else
                {
                    SdkLogger.Warning($"    {prop.Name}[{i}]: could not resolve '{name}'");
                }
            }

            prop.SetValue(castObj, array);
            SdkLogger.Msg($"    {prop.Name}: set ReferenceArray<{elementType.Name}>[{jArray.Count}]");
            return true;
        }

        // String arrays
        if (elementType == typeof(string) || elementType.FullName == "Il2CppSystem.String")
        {
            var array = Activator.CreateInstance(arrayType, new object[] { jArray.Count });
            var indexer = arrayType.GetProperty("Item");
            if (indexer == null) return false;

            for (int i = 0; i < jArray.Count; i++)
                indexer.SetValue(array, jArray[i].Value<string>(), new object[] { i });

            prop.SetValue(castObj, array);
            SdkLogger.Msg($"    {prop.Name}: set string array[{jArray.Count}]");
            return true;
        }

        // Other reference types: convert each element
        var refArray = Activator.CreateInstance(arrayType, new object[] { jArray.Count });
        var refIndexer = arrayType.GetProperty("Item");
        if (refIndexer == null) return false;

        for (int i = 0; i < jArray.Count; i++)
        {
            var converted = ConvertJTokenToType(jArray[i], elementType);
            refIndexer.SetValue(refArray, converted, new object[] { i });
        }

        prop.SetValue(castObj, refArray);
        SdkLogger.Msg($"    {prop.Name}: set ReferenceArray<{elementType.Name}>[{jArray.Count}]");
        return true;
    }

    private bool ApplyIl2CppList(object castObj, PropertyInfo prop, JArray jArray, Type elementType)
    {
        var list = prop.GetValue(castObj);
        if (list == null)
        {
            try
            {
                list = Activator.CreateInstance(prop.PropertyType);
                prop.SetValue(castObj, list);
            }
            catch (Exception ex)
            {
                SdkLogger.Warning($"    {prop.Name}: IL2CPP List is null and construction failed: {ex.Message}");
                return false;
            }
        }

        var listType = list.GetType();

        var clearMethod = listType.GetMethod("Clear");
        if (clearMethod == null)
        {
            SdkLogger.Warning($"    {prop.Name}: List has no Clear method");
            return false;
        }

        var addMethod = listType.GetMethod("Add");
        if (addMethod == null)
        {
            SdkLogger.Warning($"    {prop.Name}: List has no Add method");
            return false;
        }

        clearMethod.Invoke(list, null);

        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
        {
            var lookup = BuildNameLookup(elementType);

            foreach (var item in jArray)
            {
                var name = item.Value<string>();
                if (name != null && lookup.TryGetValue(name, out var resolved))
                {
                    var castMethod = TryCastMethod.MakeGenericMethod(elementType);
                    var castElement = castMethod.Invoke(resolved, null);
                    addMethod.Invoke(list, new[] { castElement });
                }
                else
                {
                    SdkLogger.Warning($"    {prop.Name}: could not resolve '{name}' for List<{elementType.Name}>");
                }
            }
        }
        else
        {
            foreach (var item in jArray)
            {
                var converted = ConvertJTokenToType(item, elementType);
                addMethod.Invoke(list, new[] { converted });
            }
        }

        SdkLogger.Msg($"    {prop.Name}: set List<{elementType.Name}> with {jArray.Count} elements");
        return true;
    }

    private bool ApplyManagedArray(object castObj, PropertyInfo prop, JArray jArray, Type elementType)
    {
        var array = Array.CreateInstance(elementType, jArray.Count);

        for (int i = 0; i < jArray.Count; i++)
        {
            var converted = ConvertJTokenToType(jArray[i], elementType);
            array.SetValue(converted, i);
        }

        prop.SetValue(castObj, array);
        SdkLogger.Msg($"    {prop.Name}: set {elementType.Name}[{jArray.Count}]");
        return true;
    }

    private object ConvertToPropertyType(object value, Type targetType)
    {
        if (value == null)
            return null;

        // Handle JToken from Newtonsoft deserialization
        if (value is JToken jToken)
        {
            return ConvertJTokenToType(jToken, targetType);
        }

        // Direct type match
        if (targetType.IsInstanceOfType(value))
            return value;

        // Enum from integer
        if (targetType.IsEnum)
        {
            var intVal = Convert.ToInt32(value);
            return Enum.ToObject(targetType, intVal);
        }

        // Numeric conversions
        if (targetType == typeof(int)) return Convert.ToInt32(value);
        if (targetType == typeof(float)) return Convert.ToSingle(value);
        if (targetType == typeof(double)) return Convert.ToDouble(value);
        if (targetType == typeof(bool)) return Convert.ToBoolean(value);
        if (targetType == typeof(byte)) return Convert.ToByte(value);
        if (targetType == typeof(short)) return Convert.ToInt16(value);
        if (targetType == typeof(long)) return Convert.ToInt64(value);
        if (targetType == typeof(string)) return value.ToString();

        // String to IL2CPP type: resolve as reference
        if (value is string strValue && IsIl2CppType(targetType))
        {
            return ResolveIl2CppReference(strValue, targetType);
        }

        // Simple value-type structs (e.g., OperationResources with a single int field)
        // These are blittable structs that can be set from a primitive value
        if (targetType.IsValueType && !targetType.IsPrimitive && !targetType.IsEnum)
        {
            var structResult = TryCreateSimpleStruct(targetType, value);
            if (structResult != null)
                return structResult;
        }

        return Convert.ChangeType(value, targetType);
    }

    private object ConvertJTokenToType(JToken token, Type targetType)
    {
        if (token.Type == JTokenType.Null)
            return null;

        if (targetType.IsEnum)
            return Enum.ToObject(targetType, token.Value<int>());

        if (targetType == typeof(int)) return token.Value<int>();
        if (targetType == typeof(float)) return token.Value<float>();
        if (targetType == typeof(double)) return token.Value<double>();
        if (targetType == typeof(bool)) return token.Value<bool>();
        if (targetType == typeof(byte)) return token.Value<byte>();
        if (targetType == typeof(short)) return token.Value<short>();
        if (targetType == typeof(long)) return token.Value<long>();
        if (targetType == typeof(string)) return token.Value<string>();

        // IL2CPP types: resolve by name from string
        // This covers templates, ScriptableObjects, and other Unity assets
        if (token.Type == JTokenType.String && IsIl2CppType(targetType))
        {
            var name = token.Value<string>();
            if (!string.IsNullOrEmpty(name))
                return ResolveIl2CppReference(name, targetType);
            return null;
        }

        // IL2CPP object construction from JObject
        if (token is JObject jObj && IsIl2CppType(targetType))
            return CreateIl2CppObject(targetType, jObj);

        // Simple value-type structs (e.g., OperationResources with a single int field)
        // When a primitive value (int, float) is provided for a struct type,
        // try to construct the struct and set its primary field
        if (targetType.IsValueType && !targetType.IsPrimitive && !targetType.IsEnum)
        {
            // Get the primitive value from the token
            object primitiveValue = token.Type switch
            {
                JTokenType.Integer => token.Value<long>(),
                JTokenType.Float => token.Value<double>(),
                JTokenType.Boolean => token.Value<bool>(),
                _ => null
            };

            if (primitiveValue != null)
            {
                var structResult = TryCreateSimpleStruct(targetType, primitiveValue);
                if (structResult != null)
                    return structResult;
            }
        }

        // For complex types, fall back to conversion
        return token.ToObject(targetType);
    }

    /// <summary>
    /// Tries to create a simple value-type struct from a primitive value.
    /// Handles structs like OperationResources that wrap a single int/float field.
    /// Returns null if the struct cannot be created from the primitive.
    /// </summary>
    private object TryCreateSimpleStruct(Type structType, object primitiveValue)
    {
        try
        {
            // Create default instance of the struct
            var structInstance = Activator.CreateInstance(structType);
            if (structInstance == null)
                return null;

            // Find writable fields on the struct
            var fields = structType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // Common field name patterns for wrapper structs
            string[] preferredNames = { "m_Supplies", "m_Value", "Value", "value", "_value", "m_Amount", "Amount" };

            FieldInfo targetField = null;

            // First, try to find a field by preferred name
            foreach (var name in preferredNames)
            {
                targetField = fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (targetField != null)
                    break;
            }

            // If no preferred name found, use the single field if there's only one
            if (targetField == null && fields.Length == 1)
            {
                targetField = fields[0];
            }

            if (targetField == null)
            {
                // Try properties as fallback
                var props = structType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite && p.CanRead)
                    .ToArray();

                if (props.Length == 1)
                {
                    var prop = props[0];
                    var convertedValue = ConvertPrimitiveToType(primitiveValue, prop.PropertyType);
                    if (convertedValue != null)
                    {
                        prop.SetValue(structInstance, convertedValue);
                        return structInstance;
                    }
                }
                return null;
            }

            // Convert the primitive value to match the field type
            var fieldValue = ConvertPrimitiveToType(primitiveValue, targetField.FieldType);
            if (fieldValue == null)
                return null;

            targetField.SetValue(structInstance, fieldValue);
            return structInstance;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    TryCreateSimpleStruct({structType.Name}): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Converts a primitive value (int, long, float, double, bool) to the target type.
    /// </summary>
    private static object ConvertPrimitiveToType(object value, Type targetType)
    {
        try
        {
            if (targetType == typeof(int)) return Convert.ToInt32(value);
            if (targetType == typeof(float)) return Convert.ToSingle(value);
            if (targetType == typeof(double)) return Convert.ToDouble(value);
            if (targetType == typeof(long)) return Convert.ToInt64(value);
            if (targetType == typeof(short)) return Convert.ToInt16(value);
            if (targetType == typeof(byte)) return Convert.ToByte(value);
            if (targetType == typeof(bool)) return Convert.ToBoolean(value);
            if (targetType == typeof(uint)) return Convert.ToUInt32(value);
            if (targetType == typeof(ulong)) return Convert.ToUInt64(value);
            if (targetType == typeof(ushort)) return Convert.ToUInt16(value);
            if (targetType == typeof(sbyte)) return Convert.ToSByte(value);

            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves a string name to an IL2CPP object reference.
    /// First tries name lookup via Resources.FindObjectsOfTypeAll,
    /// then falls back to constructing wrapper types (like LocalizedLine).
    /// </summary>
    private object ResolveIl2CppReference(string name, Type targetType)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        // Try to look up by name via Resources (works for templates, ScriptableObjects, etc.)
        // Only attempt this for types that extend UnityEngine.Object (can be looked up via Resources)
        if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
        {
            try
            {
                var lookup = BuildNameLookup(targetType);
                if (lookup.TryGetValue(name, out var resolved))
                {
                    SdkLogger.Msg($"    [Debug] Resolved '{name}' -> {targetType.Name} '{resolved.name}'");
                    var castMethod = TryCastMethod.MakeGenericMethod(targetType);
                    return castMethod.Invoke(resolved, null);
                }
                else
                {
                    // Log failed lookups for Sprite type to debug icon issues
                    if (targetType == typeof(Sprite))
                    {
                        SdkLogger.Warning($"    [Debug] Sprite lookup FAILED for '{name}' (lookup has {lookup.Count} entries)");
                    }
                }
            }
            catch (Exception ex)
            {
                // Type can't be looked up via Resources - log only at debug level
                SdkLogger.Msg($"    [Debug] BuildNameLookup failed for {targetType.Name}: {ex.Message}");
            }
        }

        // Try to construct the type if it's a wrapper (like LocalizedLine)
        // that stores a string key/value
        try
        {
            var obj = Activator.CreateInstance(targetType);
            if (obj != null)
            {
                // Common patterns for wrapper types: Key, Value, Name, Id, key, value
                var keyProp = targetType.GetProperty("Key") ??
                              targetType.GetProperty("Value") ??
                              targetType.GetProperty("key") ??
                              targetType.GetProperty("value") ??
                              targetType.GetProperty("Name") ??
                              targetType.GetProperty("Id");

                if (keyProp != null && keyProp.CanWrite)
                {
                    if (keyProp.PropertyType == typeof(string))
                    {
                        keyProp.SetValue(obj, name);
                        SdkLogger.Msg($"    Constructed {targetType.Name} with Key='{name}'");
                        return obj;
                    }
                }

                // If we constructed the object but couldn't set a key property,
                // still return it if it's a valid IL2CPP object (might use default constructor)
                return obj;
            }
        }
        catch
        {
            // Construction failed - type may require special initialization
        }

        SdkLogger.Warning($"    Could not resolve '{name}' as {targetType.Name}");
        return null;
    }

    /// <summary>
    /// Constructs a new IL2CPP proxy object from a JObject and recursively sets its properties.
    /// </summary>
    private object CreateIl2CppObject(Type targetType, JObject jObj, Type skipType = null)
    {
        object newObj;
        try
        {
            newObj = Activator.CreateInstance(targetType);
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    Failed to construct {targetType.Name}: {ex.Message}");
            return null;
        }

        ApplyFieldOverrides(newObj, jObj, skipType);
        return newObj;
    }

    /// <summary>
    /// Sets individual fields on an existing IL2CPP object from a JObject.
    /// Used by both $update incremental operations and CreateIl2CppObject.
    /// </summary>
    private void ApplyFieldOverrides(object target, JObject overrides, Type skipType = null)
    {
        var targetType = target.GetType();

        // Build property map (walk inheritance chain)
        var propertyMap = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        var currentType = targetType;
        while (currentType != null && currentType.Name != "Object" &&
               currentType != typeof(Il2CppObjectBase))
        {
            var props = currentType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var prop in props)
            {
                if (prop.CanWrite && prop.CanRead && !propertyMap.ContainsKey(prop.Name))
                    propertyMap[prop.Name] = prop;
            }
            currentType = currentType.BaseType;
        }

        foreach (var kvp in overrides)
        {
            var fieldName = kvp.Key;
            var value = kvp.Value;

            if (ReadOnlyProperties.Contains(fieldName))
                continue;

            if (!propertyMap.TryGetValue(fieldName, out var prop))
            {
                SdkLogger.Warning($"    {targetType.Name}: property '{fieldName}' not found");
                continue;
            }

            // Skip back-references to avoid circular construction
            if (skipType != null && prop.PropertyType.IsAssignableFrom(skipType))
                continue;

            try
            {
                // Handle collection properties specially
                var kind = ClassifyCollectionType(prop.PropertyType, out var elType);
                if (kind != CollectionKind.None && elType != null)
                {
                    if (value is JArray arr)
                    {
                        // Ensure IL2CPP list exists before full replacement
                        if (kind == CollectionKind.Il2CppList)
                            EnsureListExists(target, prop);
                        TryApplyCollectionValue(target, prop, arr);
                    }
                    else if (value is JObject collOps && kind == CollectionKind.Il2CppList)
                    {
                        EnsureListExists(target, prop);
                        TryApplyIncrementalList(target, prop, collOps, elType);
                    }
                    continue;
                }

                // Localization types (LocalizedLine, LocalizedMultiLine): write directly to m_DefaultTranslation
                // Use direct memory access if target is an IL2CPP object to avoid crashes
                //
                // Detection strategy:
                // 1. Check C# property type (fastest, catches most cases)
                // 2. Check runtime type of current value (catches IL2CPP type mismatches)
                // 3. Check field name for known localization patterns (fallback for polymorphic types)
                var currentValue = prop.CanRead ? prop.GetValue(target) : null;
                bool isLocalization = IsLocalizationType(prop.PropertyType) ||
                                      IsRuntimeLocalizationType(currentValue) ||
                                      (IsLikelyLocalizationField(fieldName) && value is JValue jv && jv.Type == JTokenType.String);

                if (isLocalization)
                {
                    var stringValue = value is JToken jt ? jt.Value<string>() : value?.ToString();
                    bool success = false;

                    if (target is Il2CppObjectBase il2cppTarget)
                    {
                        // Use safe direct memory access
                        success = WriteLocalizedFieldDirect(il2cppTarget, fieldName, stringValue);
                        if (success)
                            SdkLogger.Msg($"    {targetType.Name}.{fieldName}: set localized text (detected by {(IsLocalizationType(prop.PropertyType) ? "type" : IsRuntimeLocalizationType(currentValue) ? "runtime" : "name")})");
                    }
                    else
                    {
                        // Fallback for managed types - create new object via reflection
                        success = WriteLocalizedFieldViaReflection(target, prop, null, fieldName, stringValue);
                    }

                    if (!success)
                    {
                        SdkLogger.Warning($"    {targetType.Name}.{fieldName}: could not set localized text");
                    }
                    continue;
                }

                // For everything else, use ConvertJTokenToType which handles:
                // - Primitives, enums, strings
                // - UnityEngine.Object references (resolved by name)
                // - Nested IL2CPP objects (recursive construction)
                var converted = ConvertJTokenToType(value, prop.PropertyType);
                prop.SetValue(target, converted);
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                SdkLogger.Warning($"    {targetType.Name}.{fieldName}: {inner.GetType().Name}: {inner.Message}");
            }
        }
    }

    /// <summary>
    /// Ensures an IL2CPP List property is non-null, constructing a new instance if needed.
    /// </summary>
    private void EnsureListExists(object owner, PropertyInfo prop)
    {
        var existing = prop.GetValue(owner);
        if (existing != null) return;

        try
        {
            var newList = Activator.CreateInstance(prop.PropertyType);
            prop.SetValue(owner, newList);
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    {prop.Name}: failed to construct list: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies incremental list operations ($remove, $update, $append) to an IL2CPP List.
    /// Operations are applied in order: remove → update → append.
    /// </summary>
    private bool TryApplyIncrementalList(object castObj, PropertyInfo prop, JObject ops, Type elementType)
    {
        var list = prop.GetValue(castObj);
        if (list == null)
        {
            try
            {
                list = Activator.CreateInstance(prop.PropertyType);
                prop.SetValue(castObj, list);
            }
            catch (Exception ex)
            {
                SdkLogger.Warning($"    {prop.Name}: IL2CPP List is null and construction failed: {ex.Message}");
                return false;
            }
        }

        var listType = list.GetType();
        var countProp = listType.GetProperty("Count");
        var getItem = listType.GetMethod("get_Item");
        var removeAt = listType.GetMethod("RemoveAt");
        var addMethod = listType.GetMethod("Add");

        if (countProp == null || getItem == null)
        {
            SdkLogger.Warning($"    {prop.Name}: List missing Count or get_Item");
            return false;
        }

        int opCount = 0;

        // $remove — remove elements by index (highest-first to preserve positions)
        if (ops.TryGetValue("$remove", out var removeToken) && removeToken is JArray removeIndices)
        {
            if (removeAt == null)
            {
                SdkLogger.Warning($"    {prop.Name}: List has no RemoveAt method");
            }
            else
            {
                var indices = removeIndices.Select(t => t.Value<int>()).OrderByDescending(i => i).ToList();
                var count = (int)countProp.GetValue(list);
                foreach (var idx in indices)
                {
                    if (idx >= 0 && idx < count)
                    {
                        removeAt.Invoke(list, new object[] { idx });
                        count--;
                        opCount++;
                    }
                    else
                    {
                        SdkLogger.Warning($"    {prop.Name}.$remove: index {idx} out of range (count={count})");
                    }
                }
            }
        }

        // $update — modify fields on existing elements at specific indices
        if (ops.TryGetValue("$update", out var updateToken) && updateToken is JObject updates)
        {
            var count = (int)countProp.GetValue(list);
            foreach (var kvp in updates)
            {
                if (!int.TryParse(kvp.Key, out var idx))
                {
                    SdkLogger.Warning($"    {prop.Name}.$update: invalid index '{kvp.Key}'");
                    continue;
                }
                if (idx < 0 || idx >= count)
                {
                    SdkLogger.Warning($"    {prop.Name}.$update: index {idx} out of range (count={count})");
                    continue;
                }
                if (kvp.Value is not JObject fieldOverrides)
                {
                    SdkLogger.Warning($"    {prop.Name}.$update[{idx}]: expected object");
                    continue;
                }

                var element = getItem.Invoke(list, new object[] { idx });
                if (element != null)
                {
                    ApplyFieldOverrides(element, fieldOverrides);
                    opCount++;
                }
            }
        }

        // $append — add new elements at the end
        if (ops.TryGetValue("$append", out var appendToken) && appendToken is JArray appendItems)
        {
            if (addMethod == null)
            {
                SdkLogger.Warning($"    {prop.Name}: List has no Add method");
            }
            else
            {
                foreach (var item in appendItems)
                {
                    var converted = ConvertJTokenToType(item, elementType);
                    if (converted != null)
                    {
                        addMethod.Invoke(list, new[] { converted });
                        opCount++;

                        // Enhanced logging for ArmyEntry appends (debugging clone injection)
                        if (elementType.Name == "ArmyEntry")
                        {
                            LogArmyEntryAppend(converted, item);
                        }
                    }
                    else
                    {
                        SdkLogger.Warning($"    {prop.Name}.$append: failed to convert item: {item}");
                    }
                }
            }
        }

        SdkLogger.Msg($"    {prop.Name}: applied {opCount} incremental ops on List<{elementType.Name}>");
        return opCount > 0;
    }

    /// <summary>
    /// Log detailed information about an ArmyEntry that was appended.
    /// Helps diagnose clone injection issues.
    /// </summary>
    private void LogArmyEntryAppend(object armyEntry, JToken sourceItem)
    {
        try
        {
            var entryType = armyEntry.GetType();

            // Get Template property
            var templateProp = entryType.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
            var template = templateProp?.GetValue(armyEntry);
            string templateName = "(null)";
            if (template != null)
            {
                if (template is Il2CppObjectBase il2cppTemplate)
                {
                    var nameField = template.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                    templateName = nameField?.GetValue(template)?.ToString() ?? "(unnamed)";
                }
            }

            // Get Amount/Count property
            var amountProp = entryType.GetProperty("Amount", BindingFlags.Public | BindingFlags.Instance)
                          ?? entryType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            int amount = 1;
            if (amountProp != null)
            {
                amount = (int)amountProp.GetValue(armyEntry);
            }

            // Log the append details
            var sourceJson = sourceItem?.ToString();
            if (sourceJson?.Length > 100) sourceJson = sourceJson.Substring(0, 100) + "...";
            SdkLogger.Msg($"      ArmyEntry appended: Template='{templateName}', Amount={amount}");

            // Verify the template exists in game
            if (template == null && sourceItem is JObject jObj && jObj.TryGetValue("Template", out var templateToken))
            {
                var requestedTemplate = templateToken.Value<string>();
                SdkLogger.Warning($"      WARNING: Template reference '{requestedTemplate}' resolved to null!");
                SdkLogger.Warning($"      This may indicate the clone was not registered before patching.");
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"      LogArmyEntryAppend failed: {ex.Message}");
        }
    }
}
