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
    /// Note: "Name" is excluded as it's too generic (many plain string fields use this name).
    /// </summary>
    private static readonly HashSet<string> KnownLocalizationFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Title", "ShortName", "Description", "Text", "DisplayText",
        "TooltipText", "Label", "Message", "Hint"
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
    /// If existingLocPtr is null/zero, creates a LocalizedLine by default.
    /// </summary>
    private IntPtr CreateLocalizedObject(IntPtr existingLocPtr, string value)
    {
        try
        {
            IntPtr newClass;
            byte hasPlaceholders = 0;

            if (existingLocPtr != IntPtr.Zero)
            {
                // Get the class of the existing object to create the same type
                var existingClass = IL2CPP.il2cpp_object_get_class(existingLocPtr);
                if (existingClass == IntPtr.Zero)
                {
                    SdkLogger.Warning("    CreateLocalizedObject: could not get class of existing object, defaulting to LocalizedLine");
                    newClass = GetLocalizedLineClass();
                }
                else
                {
                    // Get the class name to determine if it's LocalizedLine or LocalizedMultiLine
                    var classNamePtr = IL2CPP.il2cpp_class_get_name(existingClass);
                    var className = classNamePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(classNamePtr) : "";

                    // Get the appropriate class pointer
                    if (className == "LocalizedMultiLine")
                    {
                        newClass = GetLocalizedMultiLineClass();
                    }
                    else
                    {
                        // Default to LocalizedLine for LocalizedLine or BaseLocalizedString
                        newClass = GetLocalizedLineClass();
                    }

                    // Copy hasPlaceholders from existing object
                    hasPlaceholders = Marshal.ReadByte(existingLocPtr + LOC_HAS_PLACEHOLDERS_OFFSET);
                }
            }
            else
            {
                // No existing object - default to LocalizedLine (most common for names/titles)
                newClass = GetLocalizedLineClass();
            }

            if (newClass == IntPtr.Zero)
            {
                SdkLogger.Warning("    CreateLocalizedObject: could not find LocalizedLine class");
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

            // Set hasPlaceholders (bool at +0x40)
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

            // Validate the pointer if non-null
            if (existingLocPtr != IntPtr.Zero && existingLocPtr.ToInt64() < 0x10000)
            {
                SdkLogger.Warning($"    {fieldName}: invalid localization pointer, treating as null");
                existingLocPtr = IntPtr.Zero;
            }

            // Create a NEW localization object with our text
            // If existingLocPtr is null (e.g., on cloned templates), CreateLocalizedObject
            // will create a fresh LocalizedLine object
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
            // Get the existing localization object (may be null for cloned templates)
            object existingLoc = prop != null ? prop.GetValue(parent) : field?.GetValue(parent);
            IntPtr existingPtr = IntPtr.Zero;
            Type locType = null;

            if (existingLoc is Il2CppObjectBase il2cppLoc)
            {
                existingPtr = il2cppLoc.Pointer;
                locType = existingLoc.GetType();
            }

            // If no existing object, try to determine the type from the property/field declaration
            if (locType == null)
            {
                locType = prop?.PropertyType ?? field?.FieldType;
                if (locType == null)
                {
                    SdkLogger.Warning($"    {fieldName}: could not determine localization type");
                    return false;
                }
            }

            // Create a NEW localization object with our text
            // CreateLocalizedObject handles null existingPtr by creating a fresh LocalizedLine
            var newLocPtr = CreateLocalizedObject(existingPtr, value);
            if (newLocPtr == IntPtr.Zero)
            {
                SdkLogger.Warning($"    {fieldName}: failed to create new localization object");
                return false;
            }

            // Wrap the new pointer in the appropriate managed type and set it back
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
                    // 3. Name-based fallback: only if current value is an IL2CPP object
                    var childCurrentValue = childProp?.CanRead == true ? childProp.GetValue(parentObj) : childField?.GetValue(parentObj);
                    bool isChildLocalization = IsLocalizationType(childType) ||
                                               IsRuntimeLocalizationType(childCurrentValue) ||
                                               (IsLikelyLocalizationField(childFieldName) &&
                                                childCurrentValue is Il2CppObjectBase &&
                                                rawValue is JValue jVal && jVal.Type == JTokenType.String);

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
                            continue; // Localization handled successfully
                        }
                        // Fall through to normal assignment if localization write failed
                        SdkLogger.Msg($"    {obj.name}.{fieldName}: localization write failed, trying normal assignment");
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

                // Incremental list/array operations: JObject with $remove/$update/$append
                if (rawValue is JObject jObj)
                {
                    var collKind = ClassifyCollectionType(prop.PropertyType, out var elType);
                    if (elType != null)
                    {
                        if (collKind == CollectionKind.Il2CppList)
                        {
                            if (TryApplyIncrementalList(castObj, prop, jObj, elType))
                                appliedCount++;
                            continue;
                        }
                        else if (collKind == CollectionKind.StructArray || collKind == CollectionKind.ReferenceArray)
                        {
                            if (TryApplyIncrementalArray(castObj, prop, jObj, elType, collKind))
                                appliedCount++;
                            continue;
                        }
                    }
                }

                // Localization types (LocalizedLine, LocalizedMultiLine): write directly to m_DefaultTranslation
                // These are wrapper objects that can't be replaced with strings via normal property set
                // Use direct memory access to avoid property getter crashes
                //
                // Detection strategy (same as ApplyFieldOverrides):
                // 1. Check C# property type (fastest, catches most cases)
                // 2. Check runtime type of current value (catches IL2CPP type mismatches)
                // 3. Name-based fallback: only if current value is an IL2CPP object
                var topLevelCurrentValue = prop.CanRead ? prop.GetValue(castObj) : null;
                bool isTopLevelLocalization = IsLocalizationType(prop.PropertyType) ||
                                              IsRuntimeLocalizationType(topLevelCurrentValue) ||
                                              (IsLikelyLocalizationField(fieldName) &&
                                               topLevelCurrentValue is Il2CppObjectBase &&
                                               rawValue is JValue jVal && jVal.Type == JTokenType.String);

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
                        continue; // Localization handled successfully
                    }
                    // Fall through to normal assignment if localization write failed
                    SdkLogger.Msg($"    {obj.name}.{fieldName}: localization write failed, trying normal assignment");
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

        // Check if elements are string references (asset names) or embedded objects (JObjects with data)
        // EventHandlers are embedded objects with _type field, not string references
        bool hasEmbeddedObjects = jArray.Any(item => item is JObject);

        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType) && !hasEmbeddedObjects)
        {
            // String references to existing assets (templates, ScriptableObjects, etc.)
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
            int successCount = 0;
            for (int i = 0; i < jArray.Count; i++)
            {
                var item = jArray[i];
                try
                {
                    var converted = ConvertJTokenToType(item, elementType);
                    if (converted == null)
                    {
                        // Log details about what failed to convert
                        var typeHint = item is JObject jObj && jObj.TryGetValue("_type", out var typeToken)
                            ? typeToken.Value<string>() : "unknown";
                        SdkLogger.Warning($"    {prop.Name}[{i}]: conversion returned null (item type: {typeHint}, target: {elementType.Name})");
                        continue;
                    }
                    addMethod.Invoke(list, new[] { converted });
                    successCount++;
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    var typeHint = item is JObject jObj && jObj.TryGetValue("_type", out var typeToken)
                        ? typeToken.Value<string>() : "unknown";
                    SdkLogger.Error($"    {prop.Name}[{i}]: failed to add item (type: {typeHint}): {inner.GetType().Name}: {inner.Message}");
                }
            }
            SdkLogger.Msg($"    {prop.Name}: set List<{elementType.Name}> with {successCount}/{jArray.Count} elements");
        }

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

    // Cache for polymorphic type resolution (base type name + _type value → concrete Type)
    private static readonly Dictionary<string, Type> _polymorphicTypeCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Constructs a new IL2CPP proxy object from a JObject and recursively sets its properties.
    /// Handles polymorphic types by checking for _type field and resolving the concrete type.
    /// </summary>
    private object CreateIl2CppObject(Type targetType, JObject jObj, Type skipType = null)
    {
        // Check for polymorphic _type field
        Type actualType = targetType;
        string typeDiscriminator = null;

        if (jObj.TryGetValue("_type", out var typeToken))
        {
            typeDiscriminator = typeToken.Value<string>();
            if (!string.IsNullOrEmpty(typeDiscriminator))
            {
                actualType = ResolvePolymorphicType(targetType, typeDiscriminator);
                if (actualType == null)
                {
                    SdkLogger.Warning($"    Failed to resolve polymorphic type '{typeDiscriminator}' (base: {targetType.Name})");
                    return null;
                }
            }
        }

        object newObj;
        try
        {
            newObj = Activator.CreateInstance(actualType);
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    Failed to construct {actualType.Name}: {ex.Message}");
            return null;
        }

        // Create a copy of jObj without the _type field for property application
        var propsToApply = new JObject();
        foreach (var kvp in jObj)
        {
            if (kvp.Key != "_type")
                propsToApply[kvp.Key] = kvp.Value;
        }

        ApplyFieldOverrides(newObj, propsToApply, skipType);
        return newObj;
    }

    /// <summary>
    /// Resolves a polymorphic type from a _type discriminator string.
    /// Searches for concrete implementations in the game assembly.
    /// </summary>
    private Type ResolvePolymorphicType(Type baseType, string typeDiscriminator)
    {
        // Build cache key from base type and discriminator
        var cacheKey = $"{baseType.FullName}:{typeDiscriminator}";
        if (_polymorphicTypeCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Get the game assembly
        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            SdkLogger.Warning($"    ResolvePolymorphicType: Assembly-CSharp not found");
            return null;
        }

        // For EventHandlers, the naming pattern is: discriminator + "Handler"
        // e.g., "ChangeProperty" → "ChangePropertyHandler"
        // e.g., "Attack" → "AttackHandler"
        // The base types may be SkillEventHandlerTemplate (schema) or SkillEventHandler (runtime)
        var candidateNames = new List<string>
        {
            // Primary pattern for EventHandlers (most common)
            $"{typeDiscriminator}Handler",
            // Direct match
            typeDiscriminator,
            // Other possible suffixes
            $"{typeDiscriminator}EventHandler",
            $"{typeDiscriminator}Template",
            // With "Skill" prefix (for skill event handlers)
            $"Skill{typeDiscriminator}Handler",
            $"Skill{typeDiscriminator}",
        };

        // Also try with base type's namespace
        var baseNamespace = baseType.Namespace;
        if (!string.IsNullOrEmpty(baseNamespace))
        {
            foreach (var name in candidateNames.ToArray())
            {
                candidateNames.Add($"{baseNamespace}.{name}");
            }
        }

        // Search for the type - check both direct inheritance and via intermediate base classes
        Type resolvedType = null;
        foreach (var candidate in candidateNames)
        {
            resolvedType = gameAssembly.GetType(candidate, throwOnError: false, ignoreCase: true);
            if (resolvedType != null && !resolvedType.IsAbstract)
            {
                // Check if it's assignable from baseType OR shares a common ancestor
                // (handles cases where schema uses SkillEventHandlerTemplate but runtime uses SkillEventHandler)
                if (baseType.IsAssignableFrom(resolvedType) || IsCompatibleHandlerType(resolvedType, baseType))
                {
                    SdkLogger.Msg($"    Resolved polymorphic type: '{typeDiscriminator}' → {resolvedType.FullName}");
                    _polymorphicTypeCache[cacheKey] = resolvedType;
                    return resolvedType;
                }
            }
        }

        // Fallback: search all types in assembly that might be handlers
        // and match the discriminator (case-insensitive, partial match)
        try
        {
            var allTypes = gameAssembly.GetTypes();
            foreach (var type in allTypes)
            {
                if (type.IsAbstract)
                    continue;

                // Check if type name matches the discriminator pattern
                var typeName = type.Name;
                bool nameMatches = typeName.Equals($"{typeDiscriminator}Handler", StringComparison.OrdinalIgnoreCase) ||
                                   typeName.Equals(typeDiscriminator, StringComparison.OrdinalIgnoreCase);

                if (nameMatches && (baseType.IsAssignableFrom(type) || IsCompatibleHandlerType(type, baseType)))
                {
                    SdkLogger.Msg($"    Resolved polymorphic type (fallback): '{typeDiscriminator}' → {type.FullName}");
                    _polymorphicTypeCache[cacheKey] = type;
                    return type;
                }
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    ResolvePolymorphicType fallback failed: {ex.Message}");
        }

        // Cache null result to avoid repeated lookups
        SdkLogger.Warning($"    ResolvePolymorphicType: could not find type for '{typeDiscriminator}' (base: {baseType.Name})");
        _polymorphicTypeCache[cacheKey] = null;
        return null;
    }

    /// <summary>
    /// Checks if a candidate type is compatible with the expected base type for handlers.
    /// Handles the case where the schema uses SkillEventHandlerTemplate but the runtime
    /// classes inherit from SkillEventHandler.
    /// </summary>
    private static bool IsCompatibleHandlerType(Type candidateType, Type expectedBaseType)
    {
        // Walk up the inheritance chain looking for handler base types
        var current = candidateType;
        while (current != null && current != typeof(object))
        {
            var name = current.Name;
            // Check for known handler base types
            if (name == "SkillEventHandlerTemplate" ||
                name == "SkillEventHandler" ||
                name == "TileEffectHandler" ||
                name == "SerializedScriptableObject")
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
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
                // 3. Name-based fallback: only if current value is an IL2CPP object (to avoid false positives
                //    on plain string fields named "Name", "Description", etc.)
                var currentValue = prop.CanRead ? prop.GetValue(target) : null;
                bool isLocalization = IsLocalizationType(prop.PropertyType) ||
                                      IsRuntimeLocalizationType(currentValue) ||
                                      (IsLikelyLocalizationField(fieldName) &&
                                       currentValue is Il2CppObjectBase &&
                                       value is JValue jv && jv.Type == JTokenType.String);

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

                    if (success)
                    {
                        continue; // Localization handled successfully
                    }
                    // Fall through to normal assignment if localization write failed
                    SdkLogger.Msg($"    {targetType.Name}.{fieldName}: localization write failed, trying normal assignment");
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

        // IMPORTANT: Operation order matters for index semantics!
        // UI sends all indices as ORIGINAL indices (before any modifications).
        // We apply in this order:
        // 1. $update — uses original indices on original array
        // 2. $remove — uses original indices, applied highest-first
        // 3. $append — adds to end (indices don't matter)

        // $update — modify fields on existing elements at specific indices (ORIGINAL indices)
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

        // $remove — remove elements by index (highest-first to preserve positions during removal)
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
    /// Applies incremental operations ($remove, $update, $append) to IL2CPP arrays (StructArray/ReferenceArray).
    /// Since arrays are immutable, this creates a new array with the modifications applied.
    /// Operations are applied in order: update → remove → append.
    /// </summary>
    private bool TryApplyIncrementalArray(object castObj, PropertyInfo prop, JObject ops, Type elementType, CollectionKind arrayKind)
    {
        var currentArray = prop.GetValue(castObj);
        if (currentArray == null)
        {
            SdkLogger.Warning($"    {prop.Name}: array is null, cannot apply incremental operations");
            return false;
        }

        var arrayType = currentArray.GetType();
        var lengthProp = arrayType.GetProperty("Length");
        var indexer = arrayType.GetProperty("Item");

        if (lengthProp == null || indexer == null)
        {
            SdkLogger.Warning($"    {prop.Name}: array missing Length or Item property");
            return false;
        }

        int currentLength = (int)lengthProp.GetValue(currentArray);
        var elements = new List<object>();

        // Read existing elements into list for manipulation
        for (int i = 0; i < currentLength; i++)
        {
            elements.Add(indexer.GetValue(currentArray, new object[] { i }));
        }

        int opCount = 0;

        // $update — modify fields on existing elements at specific indices
        if (ops.TryGetValue("$update", out var updateToken) && updateToken is JObject updates)
        {
            foreach (var kvp in updates)
            {
                if (!int.TryParse(kvp.Key, out var idx))
                {
                    SdkLogger.Warning($"    {prop.Name}.$update: invalid index '{kvp.Key}'");
                    continue;
                }
                if (idx < 0 || idx >= elements.Count)
                {
                    SdkLogger.Warning($"    {prop.Name}.$update: index {idx} out of range (count={elements.Count})");
                    continue;
                }
                if (kvp.Value is not JObject fieldOverrides)
                {
                    SdkLogger.Warning($"    {prop.Name}.$update[{idx}]: expected object");
                    continue;
                }

                var element = elements[idx];
                if (element != null)
                {
                    ApplyFieldOverrides(element, fieldOverrides);
                    opCount++;
                }
            }
        }

        // $remove — remove elements by index (highest-first to preserve positions during removal)
        if (ops.TryGetValue("$remove", out var removeToken) && removeToken is JArray removeIndices)
        {
            var indices = removeIndices.Select(t => t.Value<int>()).OrderByDescending(i => i).ToList();
            foreach (var idx in indices)
            {
                if (idx >= 0 && idx < elements.Count)
                {
                    elements.RemoveAt(idx);
                    opCount++;
                }
                else
                {
                    SdkLogger.Warning($"    {prop.Name}.$remove: index {idx} out of range (count={elements.Count})");
                }
            }
        }

        // $append — add new elements at the end
        if (ops.TryGetValue("$append", out var appendToken) && appendToken is JArray appendItems)
        {
            foreach (var item in appendItems)
            {
                var converted = ConvertJTokenToType(item, elementType);
                if (converted != null)
                {
                    elements.Add(converted);
                    opCount++;

                    // Enhanced logging for ArmyEntry appends
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

        if (opCount == 0)
            return false;

        // Create new array with modified elements
        try
        {
            var newArray = Activator.CreateInstance(arrayType, new object[] { elements.Count });
            var newIndexer = arrayType.GetProperty("Item");

            for (int i = 0; i < elements.Count; i++)
            {
                newIndexer.SetValue(newArray, elements[i], new object[] { i });
            }

            prop.SetValue(castObj, newArray);

            var arrayTypeName = arrayKind == CollectionKind.StructArray ? "StructArray" : "ReferenceArray";
            SdkLogger.Msg($"    {prop.Name}: applied {opCount} incremental ops on {arrayTypeName}<{elementType.Name}> ({currentLength} → {elements.Count} elements)");
            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"    {prop.Name}: failed to create modified array: {ex.Message}");
            return false;
        }
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
