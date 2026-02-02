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
            LoggerInstance.Warning($"    {prop.Name}: IL2CPP List is null, skipping");
            return false;
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

        // For complex types, fall back to conversion
        return token.ToObject(targetType);
    }
}
