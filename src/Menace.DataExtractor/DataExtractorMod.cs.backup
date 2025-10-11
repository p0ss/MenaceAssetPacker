using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.InteropTypes.Fields;
using UnityEngine;

[assembly: MelonInfo(typeof(Menace.DataExtractor.DataExtractorMod), "Menace Data Extractor", "2.0.0", "MenaceModkit")]
[assembly: MelonGame(null, null)]

namespace Menace.DataExtractor
{
    public class DataExtractorMod : MelonMod
    {
        private string _outputPath = "";
        private bool _hasSaved = false;

        public override void OnInitializeMelon()
        {
            var modsDir = Path.GetDirectoryName(typeof(DataExtractorMod).Assembly.Location) ?? "";
            var rootDir = Directory.GetParent(modsDir)?.FullName ?? "";
            _outputPath = Path.Combine(rootDir, "UserData", "ExtractedData");
            Directory.CreateDirectory(_outputPath);

            LoggerInstance.Msg("===========================================");
            LoggerInstance.Msg("Menace Data Extractor v2.0.0");
            LoggerInstance.Msg($"Output path: {_outputPath}");
            LoggerInstance.Msg("Using Resources.FindObjectsOfTypeAll approach");
            LoggerInstance.Msg("===========================================");

            RunExtractionAsync();
        }

        private async void RunExtractionAsync()
        {
            LoggerInstance.Msg("Waiting for game to load...");
            await Task.Delay(5000);

            for (int attempt = 1; attempt <= 60; attempt++)
            {
                if (TryExtractAllTemplates())
                {
                    LoggerInstance.Msg($"✓ Extraction completed successfully on attempt {attempt}");
                    return;
                }
                await Task.Delay(1000);
            }

            LoggerInstance.Warning("Could not extract templates after 60 attempts");
        }

        private bool TryExtractAllTemplates()
        {
            try
            {
                var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (gameAssembly == null)
                {
                    return false;
                }

                var templateTypes = gameAssembly.GetTypes()
                    .Where(t => t.Name.EndsWith("Template") && !t.IsAbstract)
                    .ToList();

                if (templateTypes.Count == 0)
                {
                    return false;
                }

                LoggerInstance.Msg($"Found {templateTypes.Count} template types, extracting...");

                int successCount = 0;
                Dictionary<string, List<object>> extractedData = new Dictionary<string, List<object>>();

                foreach (var templateType in templateTypes)
                {
                    try
                    {
                        var il2cppType = Il2CppType.From(templateType);
                        var objects = Resources.FindObjectsOfTypeAll(il2cppType);

                        if (objects == null || objects.Length == 0)
                        {
                            continue;
                        }

                        LoggerInstance.Msg($"✓ {templateType.Name}: {objects.Length} instances");

                        var templates = new List<object>();

                        foreach (var obj in objects)
                        {
                            if (obj != null)
                            {
                                // Extract using direct memory reading for known types
                                var extracted = ExtractTemplateDataDirect(obj, templateType);
                                templates.Add(extracted);
                            }
                        }

                        if (templates.Count > 0)
                        {
                            extractedData[templateType.Name] = templates;
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerInstance.Warning($"Failed to extract {templateType.Name}: {ex.Message}");
                    }
                }

                if (successCount > 0)
                {
                    LoggerInstance.Msg($"");
                    LoggerInstance.Msg($"Successfully extracted {successCount} template types");
                    SaveExtractedData(extractedData);
                    _hasSaved = true;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Error: {ex.Message}");
                return false;
            }
        }

        private void LogDebugFieldsForType(object obj)
        {
            var type = obj.GetType();
            LoggerInstance.Msg($"=== DEBUG: {type.Name} fields ===");

            LoggerInstance.Msg("PUBLIC PROPERTIES:");
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                LoggerInstance.Msg($"  {prop.PropertyType.Name} {prop.Name}");
            }

            LoggerInstance.Msg("ALL PROPERTIES (inc inherited):");
            var allProps = GetAllProperties(type);
            foreach (var prop in allProps.Take(30))
            {
                LoggerInstance.Msg($"  [{prop.DeclaringType?.Name}] {prop.PropertyType.Name} {prop.Name}");
            }

            LoggerInstance.Msg("ALL FIELDS (inc inherited):");
            var allFields = GetAllFields(type);
            foreach (var field in allFields.Take(30))
            {
                LoggerInstance.Msg($"  [{field.DeclaringType?.Name}] {field.FieldType.Name} {field.Name}");
            }

            LoggerInstance.Msg("ALL METHODS:");
            var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in allMethods.Where(m => !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_") && !m.Name.StartsWith("add_") && !m.Name.StartsWith("remove_")).Take(50))
            {
                var paramStr = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                LoggerInstance.Msg($"  {method.ReturnType.Name} {method.Name}({paramStr})");
            }
        }

        private object ExtractTemplateDataDirect(UnityEngine.Object obj, Type templateType)
        {
            var data = new Dictionary<string, object>();
            data["name"] = obj.name;

            try
            {
                // Read directly from memory using known offsets from IL2CPP dump
                if (templateType.Name == "WeaponTemplate")
                {
                    data["MinRange"] = Marshal.ReadInt32(obj.Pointer + 0x13C);
                    data["IdealRange"] = Marshal.ReadInt32(obj.Pointer + 0x140);
                    data["MaxRange"] = Marshal.ReadInt32(obj.Pointer + 0x144);
                    data["AccuracyBonus"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x148)), 0);
                    data["AccuracyDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x14C)), 0);
                    data["Damage"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x150)), 0);
                    data["DamageDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x154)), 0);
                    data["ArmorPenetration"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x168)), 0);
                    data["ArmorPenetrationDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x16C)), 0);
                }
                else if (templateType.Name == "ArmorTemplate" || templateType.Name == "AccessoryTemplate")
                {
                    // Both have same structure
                    data["Armor"] = Marshal.ReadInt32(obj.Pointer + 0x198);
                    data["DurabilityPerElement"] = Marshal.ReadInt32(obj.Pointer + 0x19C);
                    data["DamageResistance"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x1A0)), 0);
                    data["HitpointsPerElement"] = Marshal.ReadInt32(obj.Pointer + 0x1A4);
                    data["Accuracy"] = Marshal.ReadInt32(obj.Pointer + 0x1AC);
                    data["AccuracyMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x1B0)), 0);
                    data["DefenseMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x1B4)), 0);
                    data["Discipline"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(obj.Pointer + 0x1B8)), 0);
                    data["Vision"] = Marshal.ReadInt32(obj.Pointer + 0x1C0);
                    data["Detection"] = Marshal.ReadInt32(obj.Pointer + 0x1C8);
                }
                else if (templateType.Name == "EntityTemplate")
                {
                    // Read base entity info
                    data["ElementsMin"] = Marshal.ReadInt32(obj.Pointer + 0xA0);
                    data["ElementsMax"] = Marshal.ReadInt32(obj.Pointer + 0xA4);
                    data["ArmyPointCost"] = Marshal.ReadInt32(obj.Pointer + 0xB4);

                    // EntityProperties is at offset 0x2E0, read its stats
                    IntPtr propsPtr = Marshal.ReadIntPtr(obj.Pointer + 0x2E0);
                    if (propsPtr != IntPtr.Zero)
                    {
                        var props = new Dictionary<string, object>();
                        props["HitpointsPerElement"] = Marshal.ReadInt32(propsPtr + 0x14);
                        props["Armor"] = Marshal.ReadInt32(propsPtr + 0x1C);
                        props["ArmorSide"] = Marshal.ReadInt32(propsPtr + 0x20);
                        props["ArmorBack"] = Marshal.ReadInt32(propsPtr + 0x24);
                        props["ArmorDurabilityPerElement"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(propsPtr + 0x2C)), 0);
                        props["ActionPoints"] = Marshal.ReadInt32(propsPtr + 0x34);
                        props["Accuracy"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(propsPtr + 0x68)), 0);
                        props["AccuracyDropoff"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(propsPtr + 0x70)), 0);
                        props["DefenseMult"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(propsPtr + 0x78)), 0);
                        props["Discipline"] = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(propsPtr + 0x90)), 0);
                        props["Vision"] = Marshal.ReadInt32(propsPtr + 0xB0);
                        props["Detection"] = Marshal.ReadInt32(propsPtr + 0xB8);
                        props["Concealment"] = Marshal.ReadInt32(propsPtr + 0xC0);
                        data["Properties"] = props;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"Failed to read {templateType.Name} memory: {ex.Message}");
            }

            return data;
        }

        private object ExtractTemplateData(object template)
        {
            var data = new Dictionary<string, object>();
            var type = template.GetType();

            try
            {
                // Get ALL properties including inherited ones, public and non-public
                var allProperties = GetAllProperties(type);
                foreach (var prop in allProperties)
                {
                    try
                    {
                        if (prop.Name == "Pointer" || prop.Name == "m_CachedPtr" || prop.Name == "ObjectClass") continue;
                        if (!prop.CanRead) continue;

                        var value = prop.GetValue(template, null);
                        data[prop.Name] = ConvertValue(value, 0);
                    }
                    catch
                    {
                        // Skip properties that can't be read
                    }
                }

                // Get ALL fields including inherited ones, public and non-public
                var allFields = GetAllFields(type);
                foreach (var field in allFields)
                {
                    try
                    {
                        if (field.Name.StartsWith("NativeFieldInfoPtr") || field.Name.StartsWith("Il2Cpp")) continue;
                        if (data.ContainsKey(field.Name)) continue; // Skip if property already added it

                        var value = field.GetValue(template);
                        data[field.Name] = ConvertValue(value, 0);
                    }
                    catch
                    {
                        // Skip fields that can't be read
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"Extract error: {ex.Message}");
            }

            return data;
        }

        private IEnumerable<PropertyInfo> GetAllProperties(Type type)
        {
            var properties = new List<PropertyInfo>();
            while (type != null && type != typeof(object))
            {
                properties.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                type = type.BaseType;
            }
            return properties.GroupBy(p => p.Name).Select(g => g.First());
        }

        private IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            var fields = new List<FieldInfo>();
            while (type != null && type != typeof(object))
            {
                fields.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                type = type.BaseType;
            }
            return fields.GroupBy(f => f.Name).Select(g => g.First());
        }

        private object ConvertValue(object value, int depth)
        {
            if (value == null) return null;
            if (depth > 3) return value.ToString(); // Prevent infinite recursion

            var type = value.GetType();

            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
                return value;

            if (value is UnityEngine.Object unityObj)
            {
                try
                {
                    var nameProp = unityObj.GetType().GetProperty("name");
                    return nameProp?.GetValue(unityObj, null)?.ToString() ?? unityObj.ToString();
                }
                catch
                {
                    return unityObj.ToString();
                }
            }

            if (type.IsArray)
            {
                var array = (Array)value;
                var list = new List<object>();
                int maxItems = Math.Min(array.Length, 100); // Limit array size
                for (int i = 0; i < maxItems; i++)
                {
                    list.Add(ConvertValue(array.GetValue(i), depth + 1));
                }
                return list;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = new List<object>();
                int count = 0;
                foreach (var item in (System.Collections.IEnumerable)value)
                {
                    if (count++ >= 100) break; // Limit list size
                    list.Add(ConvertValue(item, depth + 1));
                }
                return list;
            }

            // If it's a complex IL2CPP object, try to extract its data
            if (type.Namespace != null && (type.Namespace.StartsWith("Il2Cpp") || type.Namespace.StartsWith("UnityEngine")))
            {
                try
                {
                    var nested = new Dictionary<string, object>();
                    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        if (prop.Name == "Pointer" || prop.Name == "m_CachedPtr") continue;
                        if (!prop.CanRead) continue;
                        try
                        {
                            var val = prop.GetValue(value, null);
                            nested[prop.Name] = ConvertValue(val, depth + 1);
                        }
                        catch { }
                    }
                    return nested.Count > 0 ? nested : value.ToString();
                }
                catch
                {
                    return value.ToString();
                }
            }

            return value.ToString();
        }

        private void SaveExtractedData(Dictionary<string, List<object>> extractedData)
        {
            LoggerInstance.Msg("===========================================");
            LoggerInstance.Msg("Saving data to JSON files...");
            LoggerInstance.Msg("===========================================");

            foreach (var kvp in extractedData)
            {
                try
                {
                    string fileName = $"{kvp.Key}.json";
                    string filePath = Path.Combine(_outputPath, fileName);

                    var json = JsonConvert.SerializeObject(kvp.Value, Formatting.Indented, new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Include,
                        MaxDepth = 10
                    });

                    File.WriteAllText(filePath, json);
                    LoggerInstance.Msg($"✓ Saved {kvp.Value.Count} {kvp.Key} instances");
                }
                catch (Exception ex)
                {
                    LoggerInstance.Error($"✗ Failed to save {kvp.Key}: {ex.Message}");
                }
            }

            LoggerInstance.Msg("");
            LoggerInstance.Msg("===========================================");
            LoggerInstance.Msg("DATA EXTRACTION COMPLETE!");
            LoggerInstance.Msg($"Location: {_outputPath}");
            LoggerInstance.Msg("===========================================");
        }

        public override void OnApplicationQuit()
        {
            if (!_hasSaved)
            {
                LoggerInstance.Warning("Extraction did not complete before quit");
            }
        }
    }
}
