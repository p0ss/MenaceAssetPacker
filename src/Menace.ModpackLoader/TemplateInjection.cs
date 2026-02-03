using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
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
    private static readonly HashSet<string> ReadOnlyProperties = new(StringComparer.Ordinal)
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

    private Dictionary<string, UnityEngine.Object> BuildNameLookup(Type elementType)
    {
        if (_nameLookupCache.TryGetValue(elementType, out var cached))
            return cached;

        var lookup = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

        try
        {
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

            LoggerInstance.Msg($"    Built name lookup for {elementType.Name}: {lookup.Count} entries");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"    Failed to build name lookup for {elementType.Name}: {ex.Message}");
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
            LoggerInstance.Error($"    TryCast failed for {obj.name}: {ex.Message}");
            return;
        }

        if (castObj == null)
        {
            LoggerInstance.Error($"    TryCast returned null for {obj.name}");
            return;
        }

        // Build property lookup for this type (walk inheritance chain)
        var propertyMap = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
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
                    LoggerInstance.Warning($"    {obj.name}: parent property '{parentFieldName}' not found on {templateType.Name}");
                    continue;
                }

                try
                {
                    var parentObj = parentProp.GetValue(castObj);
                    if (parentObj == null)
                    {
                        LoggerInstance.Warning($"    {obj.name}.{parentFieldName} is null, cannot set '{childFieldName}'");
                        continue;
                    }

                    var childProp = parentObj.GetType().GetProperty(childFieldName,
                        BindingFlags.Public | BindingFlags.Instance);
                    if (childProp == null || !childProp.CanWrite)
                    {
                        LoggerInstance.Warning($"    {obj.name}: property '{childFieldName}' not found on {parentObj.GetType().Name}");
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

                    var nestedConverted = ConvertToPropertyType(rawValue, childProp.PropertyType);
                    childProp.SetValue(parentObj, nestedConverted);
                    appliedCount++;
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    LoggerInstance.Error($"    {obj.name}.{fieldName}: {inner.GetType().Name}: {inner.Message}");
                }
                continue;
            }

            if (!propertyMap.TryGetValue(fieldName, out var prop))
            {
                LoggerInstance.Warning($"    {obj.name}: property '{fieldName}' not found on {templateType.Name}");
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

                var convertedValue = ConvertToPropertyType(rawValue, prop.PropertyType);
                prop.SetValue(castObj, convertedValue);
                appliedCount++;
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                LoggerInstance.Error($"    {obj.name}.{fieldName}: {inner.GetType().Name}: {inner.Message}");
            }
        }

        if (appliedCount > 0)
        {
            LoggerInstance.Msg($"    {obj.name}: set {appliedCount}/{modifications.Count} fields");
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
            LoggerInstance.Warning($"    {prop.Name}: no indexer found on {arrayType.Name}");
            return false;
        }

        for (int i = 0; i < jArray.Count; i++)
        {
            var converted = ConvertJTokenToType(jArray[i], elementType);
            indexer.SetValue(array, converted, new object[] { i });
        }

        prop.SetValue(castObj, array);
        LoggerInstance.Msg($"    {prop.Name}: set StructArray<{elementType.Name}>[{jArray.Count}]");
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
                    LoggerInstance.Warning($"    {prop.Name}[{i}]: could not resolve '{name}'");
                }
            }

            prop.SetValue(castObj, array);
            LoggerInstance.Msg($"    {prop.Name}: set ReferenceArray<{elementType.Name}>[{jArray.Count}]");
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
            LoggerInstance.Msg($"    {prop.Name}: set string array[{jArray.Count}]");
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
        LoggerInstance.Msg($"    {prop.Name}: set ReferenceArray<{elementType.Name}>[{jArray.Count}]");
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
                LoggerInstance.Warning($"    {prop.Name}: IL2CPP List is null and construction failed: {ex.Message}");
                return false;
            }
        }

        var listType = list.GetType();

        var clearMethod = listType.GetMethod("Clear");
        if (clearMethod == null)
        {
            LoggerInstance.Warning($"    {prop.Name}: List has no Clear method");
            return false;
        }

        var addMethod = listType.GetMethod("Add");
        if (addMethod == null)
        {
            LoggerInstance.Warning($"    {prop.Name}: List has no Add method");
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
                    LoggerInstance.Warning($"    {prop.Name}: could not resolve '{name}' for List<{elementType.Name}>");
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

        LoggerInstance.Msg($"    {prop.Name}: set List<{elementType.Name}> with {jArray.Count} elements");
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
        LoggerInstance.Msg($"    {prop.Name}: set {elementType.Name}[{jArray.Count}]");
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

        // UnityEngine.Object references: resolve by name from string
        if (token.Type == JTokenType.String && typeof(UnityEngine.Object).IsAssignableFrom(targetType))
        {
            var name = token.Value<string>();
            if (name != null)
            {
                var lookup = BuildNameLookup(targetType);
                if (lookup.TryGetValue(name, out var resolved))
                {
                    var castMethod = TryCastMethod.MakeGenericMethod(targetType);
                    return castMethod.Invoke(resolved, null);
                }
                LoggerInstance.Warning($"    Could not resolve '{name}' as {targetType.Name}");
            }
            return null;
        }

        // IL2CPP object construction from JObject
        if (token is JObject jObj && IsIl2CppType(targetType))
            return CreateIl2CppObject(targetType, jObj);

        // For complex types, fall back to conversion
        return token.ToObject(targetType);
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
            LoggerInstance.Warning($"    Failed to construct {targetType.Name}: {ex.Message}");
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
        var propertyMap = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
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
                LoggerInstance.Warning($"    {targetType.Name}: property '{fieldName}' not found");
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
                LoggerInstance.Warning($"    {targetType.Name}.{fieldName}: {inner.GetType().Name}: {inner.Message}");
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
            LoggerInstance.Warning($"    {prop.Name}: failed to construct list: {ex.Message}");
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
                LoggerInstance.Warning($"    {prop.Name}: IL2CPP List is null and construction failed: {ex.Message}");
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
            LoggerInstance.Warning($"    {prop.Name}: List missing Count or get_Item");
            return false;
        }

        int opCount = 0;

        // $remove — remove elements by index (highest-first to preserve positions)
        if (ops.TryGetValue("$remove", out var removeToken) && removeToken is JArray removeIndices)
        {
            if (removeAt == null)
            {
                LoggerInstance.Warning($"    {prop.Name}: List has no RemoveAt method");
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
                        LoggerInstance.Warning($"    {prop.Name}.$remove: index {idx} out of range (count={count})");
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
                    LoggerInstance.Warning($"    {prop.Name}.$update: invalid index '{kvp.Key}'");
                    continue;
                }
                if (idx < 0 || idx >= count)
                {
                    LoggerInstance.Warning($"    {prop.Name}.$update: index {idx} out of range (count={count})");
                    continue;
                }
                if (kvp.Value is not JObject fieldOverrides)
                {
                    LoggerInstance.Warning($"    {prop.Name}.$update[{idx}]: expected object");
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
                LoggerInstance.Warning($"    {prop.Name}: List has no Add method");
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
                    }
                }
            }
        }

        LoggerInstance.Msg($"    {prop.Name}: applied {opCount} incremental ops on List<{elementType.Name}>");
        return opCount > 0;
    }
}
