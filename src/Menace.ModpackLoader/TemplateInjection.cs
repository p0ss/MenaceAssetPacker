using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using Menace.SDK;
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

    // Properties that should not be modified
    private static readonly HashSet<string> ReadOnlyProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pointer", "ObjectClass", "WasCollected", "m_CachedPtr",
        "name", "hideFlags", "serializationData"
    };

    // Cache for name -> Object lookups, keyed by element type
    private readonly Dictionary<Type, Dictionary<string, UnityEngine.Object>> _nameLookupCache = new();

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
    /// Write a string value directly to a LocalizedLine/LocalizedMultiLine object's m_DefaultTranslation field.
    /// This bypasses the type system since we can't construct these wrappers from strings.
    /// Based on reverse engineering: m_DefaultTranslation is at offset +0x38.
    /// </summary>
    private bool WriteLocalizedStringValue(object locObject, string value)
    {
        if (locObject == null || !(locObject is Il2CppObjectBase il2cppObj))
            return false;

        try
        {
            var ptr = il2cppObj.Pointer;
            if (ptr == IntPtr.Zero)
                return false;

            // m_DefaultTranslation is at offset +0x38 (0x38 = 56 bytes)
            const int M_DEFAULT_TRANSLATION_OFFSET = 0x38;

            // Convert managed string to IL2CPP string
            IntPtr il2cppStr = IntPtr.Zero;
            if (!string.IsNullOrEmpty(value))
            {
                il2cppStr = IL2CPP.ManagedStringToIl2Cpp(value);
            }

            // Write the string pointer to the field
            Marshal.WriteIntPtr(ptr + M_DEFAULT_TRANSLATION_OFFSET, il2cppStr);

            return true;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    WriteLocalizedStringValue failed: {ex.Message}");
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
                        lookup[obj.name] = obj;
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

                    var childProp = parentObj.GetType().GetProperty(childFieldName,
                        BindingFlags.Public | BindingFlags.Instance);
                    if (childProp == null || !childProp.CanWrite)
                    {
                        SdkLogger.Warning($"    {obj.name}: property '{childFieldName}' not found on {parentObj.GetType().Name}");
                        continue;
                    }

                    if (rawValue is JArray nestedJArray)
                    {
                        var kind = ClassifyCollectionType(childProp.PropertyType, out _);
                        if (kind != CollectionKind.None)
                        {
                            if (TryApplyCollectionValue(parentObj, childProp, nestedJArray))
                                appliedCount++;
                            continue;
                        }
                    }

                    // Incremental list operations via JObject with $op
                    if (rawValue is JObject nestedJObj)
                    {
                        var nestedKind = ClassifyCollectionType(childProp.PropertyType, out var nestedElType);
                        if (nestedKind == CollectionKind.Il2CppList && nestedElType != null)
                        {
                            if (TryApplyIncrementalList(parentObj, childProp, nestedJObj, nestedElType))
                                appliedCount++;
                            continue;
                        }
                    }

                    // Localization types: write directly to m_DefaultTranslation
                    if (IsLocalizationType(childProp.PropertyType))
                    {
                        var stringValue = rawValue is JToken jt ? jt.Value<string>() : rawValue?.ToString();
                        var existingLoc = childProp.GetValue(parentObj);
                        if (existingLoc != null && WriteLocalizedStringValue(existingLoc, stringValue))
                        {
                            SdkLogger.Msg($"    {obj.name}.{fieldName}: set localized text");
                            appliedCount++;
                        }
                        else
                        {
                            SdkLogger.Warning($"    {obj.name}.{fieldName}: localization object is null");
                        }
                        continue;
                    }

                    var nestedConverted = ConvertToPropertyType(rawValue, childProp.PropertyType);
                    childProp.SetValue(parentObj, nestedConverted);
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
                if (IsLocalizationType(prop.PropertyType))
                {
                    var stringValue = rawValue is JToken jt ? jt.Value<string>() : rawValue?.ToString();
                    var existingLoc = prop.GetValue(castObj);
                    if (existingLoc != null && WriteLocalizedStringValue(existingLoc, stringValue))
                    {
                        SdkLogger.Msg($"    {obj.name}.{fieldName}: set localized text");
                        appliedCount++;
                    }
                    else
                    {
                        SdkLogger.Warning($"    {obj.name}.{fieldName}: localization object is null, cannot set text");
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

        // For complex types, fall back to conversion
        return token.ToObject(targetType);
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
                    var castMethod = TryCastMethod.MakeGenericMethod(targetType);
                    return castMethod.Invoke(resolved, null);
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
                if (IsLocalizationType(prop.PropertyType))
                {
                    var stringValue = value is JToken jt ? jt.Value<string>() : value?.ToString();
                    var existingLoc = prop.GetValue(target);
                    if (existingLoc != null && WriteLocalizedStringValue(existingLoc, stringValue))
                    {
                        // Successfully wrote to existing localization object
                    }
                    else
                    {
                        SdkLogger.Warning($"    {targetType.Name}.{fieldName}: localization object is null");
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
