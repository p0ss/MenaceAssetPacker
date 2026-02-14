using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Code-level API for reading, writing, and cloning game templates
/// (ScriptableObjects managed by DataTemplateLoader).
/// </summary>
public static class Templates
{
    private static readonly MethodInfo TryCastMethod =
        typeof(Il2CppObjectBase).GetMethod("TryCast");

    // Cache of template types we've already ensured are loaded
    private static readonly HashSet<string> _loadedTypes = new();

    /// <summary>
    /// Find a specific template instance by type name and instance name.
    /// </summary>
    public static GameObj Find(string templateTypeName, string instanceName)
    {
        if (string.IsNullOrEmpty(templateTypeName) || string.IsNullOrEmpty(instanceName))
            return GameObj.Null;

        // Ensure templates are loaded into memory via DataTemplateLoader
        EnsureTemplatesLoaded(templateTypeName);

        return GameQuery.FindByName(templateTypeName, instanceName);
    }

    /// <summary>
    /// Find a specific template instance and return it as the managed IL2CPP type.
    /// Returns null if not found or conversion fails.
    /// </summary>
    public static T Get<T>(string templateTypeName, string instanceName) where T : class
    {
        var obj = Find(templateTypeName, instanceName);
        return obj.IsNull ? null : obj.As<T>();
    }

    /// <summary>
    /// Find all template instances of a given type.
    /// </summary>
    public static GameObj[] FindAll(string templateTypeName)
    {
        if (string.IsNullOrEmpty(templateTypeName))
            return Array.Empty<GameObj>();

        // Ensure templates are loaded into memory via DataTemplateLoader
        EnsureTemplatesLoaded(templateTypeName);

        return GameQuery.FindAll(templateTypeName);
    }

    /// <summary>
    /// Find all template instances of a given type and return them as managed IL2CPP types.
    /// Items that fail conversion are skipped.
    /// </summary>
    public static List<T> GetAll<T>(string templateTypeName) where T : class
    {
        var objects = FindAll(templateTypeName);
        var result = new List<T>(objects.Length);
        foreach (var obj in objects)
        {
            var managed = obj.As<T>();
            if (managed != null)
                result.Add(managed);
        }
        return result;
    }

    /// <summary>
    /// Find all template instances and return them as managed IL2CPP proxy objects.
    /// Use this when you need to pass objects to reflection or IL2CPP APIs but don't have
    /// compile-time access to the specific type.
    /// </summary>
    public static object[] FindAllManaged(string templateTypeName)
    {
        if (string.IsNullOrEmpty(templateTypeName))
            return Array.Empty<object>();

        return GameQuery.FindAllManaged(templateTypeName);
    }

    /// <summary>
    /// Find a specific template and return it as a managed IL2CPP proxy object.
    /// Use this when you need to pass the object to reflection but don't have
    /// compile-time access to the specific type.
    /// </summary>
    public static object GetManaged(string templateTypeName, string instanceName)
    {
        var obj = Find(templateTypeName, instanceName);
        return obj.IsNull ? null : obj.ToManaged();
    }

    /// <summary>
    /// Check if a template with the given type and name exists.
    /// </summary>
    public static bool Exists(string templateTypeName, string instanceName)
    {
        return !Find(templateTypeName, instanceName).IsNull;
    }

    /// <summary>
    /// Read a field value from a template object using managed reflection.
    /// Returns null on failure.
    /// </summary>
    public static object ReadField(GameObj template, string fieldName)
    {
        if (template.IsNull || string.IsNullOrEmpty(fieldName))
            return null;

        try
        {
            var gameType = template.GetGameType();
            var managedType = gameType?.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("Templates.ReadField",
                    $"No managed type for {gameType?.FullName}");
                return null;
            }

            // Get managed proxy wrapper
            var obj = GetManagedProxy(template, managedType);
            if (obj == null) return null;

            // Handle dotted path
            var parts = fieldName.Split('.');
            object current = obj;
            foreach (var part in parts)
            {
                if (current == null) return null;
                var prop = current.GetType().GetProperty(part,
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanRead)
                {
                    ModError.WarnInternal("Templates.ReadField",
                        $"Property '{part}' not found on {current.GetType().Name}");
                    return null;
                }
                current = prop.GetValue(current);
            }

            return current;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Templates.ReadField", $"Failed '{fieldName}'", ex);
            return null;
        }
    }

    /// <summary>
    /// Write a field value on a template object using managed reflection.
    /// Returns false on failure.
    /// </summary>
    public static bool WriteField(GameObj template, string fieldName, object value)
    {
        if (template.IsNull || string.IsNullOrEmpty(fieldName))
            return false;

        try
        {
            var gameType = template.GetGameType();
            var managedType = gameType?.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("Templates.WriteField",
                    $"No managed type for {gameType?.FullName}");
                return false;
            }

            var obj = GetManagedProxy(template, managedType);
            if (obj == null) return false;

            // Handle dotted path — navigate to parent, then set leaf
            var parts = fieldName.Split('.');
            object current = obj;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (current == null) return false;
                var prop = current.GetType().GetProperty(parts[i],
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanRead) return false;
                current = prop.GetValue(current);
            }

            if (current == null) return false;

            var leafProp = current.GetType().GetProperty(parts[^1],
                BindingFlags.Public | BindingFlags.Instance);
            if (leafProp == null || !leafProp.CanWrite)
            {
                ModError.WarnInternal("Templates.WriteField",
                    $"Property '{parts[^1]}' not writable on {current.GetType().Name}");
                return false;
            }

            var converted = ConvertValue(value, leafProp.PropertyType);
            leafProp.SetValue(current, converted);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Templates.WriteField", $"Failed '{fieldName}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Write multiple fields on a template object. Returns the number successfully written.
    /// </summary>
    public static int WriteFields(GameObj template, Dictionary<string, object> fields)
    {
        if (template.IsNull || fields == null) return 0;

        int count = 0;
        foreach (var (name, value) in fields)
        {
            if (WriteField(template, name, value))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Clone an existing template via UnityEngine.Object.Instantiate and return
    /// the new instance. Does NOT register in DataTemplateLoader (the main
    /// ModpackLoaderMod cloning pipeline handles that).
    /// </summary>
    public static GameObj Clone(string templateTypeName, string sourceName, string newName)
    {
        if (string.IsNullOrEmpty(templateTypeName) || string.IsNullOrEmpty(sourceName)
            || string.IsNullOrEmpty(newName))
            return GameObj.Null;

        try
        {
            // Ensure templates are loaded into memory via DataTemplateLoader
            EnsureTemplatesLoaded(templateTypeName);

            var gameType = GameType.Find(templateTypeName);
            var managedType = gameType?.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("Templates.Clone",
                    $"No managed type for '{templateTypeName}'");
                return GameObj.Null;
            }

            var il2cppType = Il2CppType.From(managedType);
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);
            if (objects == null || objects.Length == 0)
            {
                ModError.WarnInternal("Templates.Clone",
                    $"No instances of '{templateTypeName}' found");
                return GameObj.Null;
            }

            UnityEngine.Object source = null;
            foreach (var obj in objects)
            {
                if (obj != null && obj.name == sourceName)
                {
                    source = obj;
                    break;
                }
            }

            if (source == null)
            {
                ModError.WarnInternal("Templates.Clone",
                    $"Source '{sourceName}' not found");
                return GameObj.Null;
            }

            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = newName;
            clone.hideFlags = HideFlags.DontUnloadUnusedAsset;

            return new GameObj(clone.Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Templates.Clone",
                $"Failed to clone {sourceName} -> {newName}", ex);
            return GameObj.Null;
        }
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
    {
        try
        {
            // Create a managed wrapper via the IL2CPP pointer constructor
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor != null)
                return ptrCtor.Invoke(new object[] { obj.Pointer });

            ModError.WarnInternal("Templates",
                $"No IntPtr constructor on {managedType.Name}");
            return null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Templates",
                $"Failed to create proxy for {managedType.Name}", ex);
            return null;
        }
    }

    private static object ConvertValue(object value, Type targetType)
    {
        if (value == null) return null;
        if (targetType.IsInstanceOfType(value)) return value;

        if (targetType.IsEnum)
            return Enum.ToObject(targetType, Convert.ToInt32(value));
        if (targetType == typeof(int)) return Convert.ToInt32(value);
        if (targetType == typeof(float)) return Convert.ToSingle(value);
        if (targetType == typeof(double)) return Convert.ToDouble(value);
        if (targetType == typeof(bool)) return Convert.ToBoolean(value);
        if (targetType == typeof(string)) return value.ToString();

        return Convert.ChangeType(value, targetType);
    }

    /// <summary>
    /// Ensures templates of the given type are loaded into memory by calling
    /// DataTemplateLoader.GetAll&lt;T&gt;(). Templates loaded this way become
    /// findable via Resources.FindObjectsOfTypeAll().
    /// </summary>
    private static void EnsureTemplatesLoaded(string templateTypeName)
    {
        // Only try once per type to avoid repeated reflection overhead
        if (_loadedTypes.Contains(templateTypeName))
            return;

        try
        {
            var gameType = GameType.Find(templateTypeName);
            var managedType = gameType?.ManagedType;
            if (managedType == null)
                return;

            // Find DataTemplateLoader in Assembly-CSharp
            var gameAssembly = GameState.GameAssembly;
            if (gameAssembly == null)
                return;

            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataTemplateLoader");

            if (loaderType == null)
                return;

            // Get the GetAll<T>() method
            var getAllMethod = loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition);

            if (getAllMethod == null)
                return;

            // Call GetAll<T>() to force templates into memory
            var genericMethod = getAllMethod.MakeGenericMethod(managedType);
            genericMethod.Invoke(null, null);

            _loadedTypes.Add(templateTypeName);
        }
        catch (Exception ex)
        {
            // Don't fail hard - just log and continue, FindAll will return empty
            ModError.WarnInternal("Templates.EnsureTemplatesLoaded",
                $"Failed for {templateTypeName}: {ex.Message}");
        }
    }
}
