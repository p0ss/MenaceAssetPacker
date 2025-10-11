using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Menace.DataExtractor
{
    // Helper class to dump IL2CPP field offsets
    public static class OffsetDumper
    {
        public static string DumpTemplateOffsets()
        {
            var sb = new StringBuilder();
            sb.AppendLine("// IL2CPP Field Offsets - Generated for Unity 6000.0.56f1");
            sb.AppendLine($"// Generated: {DateTime.Now}");
            sb.AppendLine();

            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly == null)
            {
                return "// ERROR: Could not find Assembly-CSharp";
            }

            var templateTypes = new[] { "WeaponTemplate", "ArmorTemplate", "EntityTemplate", "AccessoryTemplate" };

            foreach (var typeName in templateTypes)
            {
                var type = gameAssembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type == null) continue;

                sb.AppendLine($"// {typeName}");
                DumpType(type, sb);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void DumpType(Type type, StringBuilder sb)
        {
            sb.AppendLine($"// Base class: {type.BaseType?.Name ?? "None"}");

            // Get all fields including private ones from base classes
            var allFields = type.GetFields(System.Reflection.BindingFlags.Public |
                                          System.Reflection.BindingFlags.NonPublic |
                                          System.Reflection.BindingFlags.Instance)
                .ToList();

            // Also get fields from base class
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object) && baseType != typeof(UnityEngine.Object))
            {
                allFields.AddRange(baseType.GetFields(System.Reflection.BindingFlags.Public |
                                                      System.Reflection.BindingFlags.NonPublic |
                                                      System.Reflection.BindingFlags.Instance));
                baseType = baseType.BaseType;
            }

            foreach (var field in allFields.Where(f => !f.Name.StartsWith("NativeFieldInfoPtr") &&
                                                       !f.Name.StartsWith("Il2Cpp") &&
                                                       !f.Name.Contains("k__BackingField")))
            {
                try
                {
                    // Try to get offset using Marshal.OffsetOf
                    var offset = Marshal.OffsetOf(type, field.Name);
                    sb.AppendLine($"// {field.Name}: offset 0x{offset.ToInt64():X} ({field.FieldType.Name})");
                }
                catch
                {
                    sb.AppendLine($"// {field.Name}: offset unknown ({field.FieldType.Name})");
                }
            }
        }
    }
}
