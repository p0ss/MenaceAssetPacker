using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Il2CppInterop.Runtime;

namespace Menace.DataExtractor
{
    /// <summary>
    /// Parses polymorphic effect handlers from Unity SerializeReference data.
    /// Unity embeds full type names as UTF-16 strings in serialized MonoBehaviour data.
    /// </summary>
    public class EventHandlerParser
    {
        // UTF-16 encoded marker that appears after type names in SerializeReference
        private static readonly byte[] AssemblyMarker = Encoding.Unicode.GetBytes(", Assembly-CSharp");

        /// <summary>
        /// Represents a detected type reference in serialized data.
        /// </summary>
        public class TypeReference
        {
            public int Offset { get; set; }
            public string TypeName { get; set; }
            public string ShortName { get; set; }
        }

        /// <summary>
        /// Schema definition for an effect handler type.
        /// </summary>
        public class EffectHandlerSchema
        {
            public string Name { get; set; }
            public string TypeName { get; set; }
            public string BaseClass { get; set; }
            public List<string> Aliases { get; set; } = new();
            public List<EffectHandlerField> Fields { get; set; } = new();
        }

        /// <summary>
        /// Field definition within an effect handler.
        /// </summary>
        public class EffectHandlerField
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public uint Offset { get; set; }
            public string Category { get; set; }
        }

        private readonly Dictionary<string, EffectHandlerSchema> _schemaByTypeName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EffectHandlerSchema> _schemaByShortName = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Register a handler schema for type detection and parsing.
        /// </summary>
        public void RegisterSchema(EffectHandlerSchema schema)
        {
            if (!string.IsNullOrEmpty(schema.TypeName))
            {
                _schemaByTypeName[schema.TypeName] = schema;
                // Also register by short name (last part after dot)
                var shortName = GetShortTypeName(schema.TypeName);
                _schemaByShortName[shortName] = schema;
            }

            _schemaByShortName[schema.Name] = schema;

            foreach (var alias in schema.Aliases)
            {
                _schemaByShortName[alias] = schema;
            }
        }

        /// <summary>
        /// Detect the handler type from an IL2CPP object pointer by examining its class name.
        /// </summary>
        public string DetectHandlerType(IntPtr handlerPtr)
        {
            if (handlerPtr == IntPtr.Zero) return null;

            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(handlerPtr);
                if (klass == IntPtr.Zero) return null;

                IntPtr namePtr = IL2CPP.il2cpp_class_get_name(klass);
                if (namePtr == IntPtr.Zero) return null;

                string className = Marshal.PtrToStringAnsi(namePtr);
                return className;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the schema for a handler type by its class name.
        /// </summary>
        public EffectHandlerSchema GetSchema(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            // Try full type name first
            if (_schemaByTypeName.TryGetValue(typeName, out var schema))
                return schema;

            // Try short name
            if (_schemaByShortName.TryGetValue(typeName, out schema))
                return schema;

            // Try short name from full type
            var shortName = GetShortTypeName(typeName);
            if (_schemaByShortName.TryGetValue(shortName, out schema))
                return schema;

            return null;
        }

        /// <summary>
        /// Parse a handler object's fields using the schema.
        /// </summary>
        public Dictionary<string, object> ParseHandler(IntPtr handlerPtr, EffectHandlerSchema schema, Func<IntPtr, string> readAssetName)
        {
            var result = new Dictionary<string, object>();

            if (handlerPtr == IntPtr.Zero || schema == null)
                return result;

            // Add handler type
            result["_type"] = schema.Name;

            foreach (var field in schema.Fields)
            {
                try
                {
                    object value = ReadField(handlerPtr, field, readAssetName);
                    if (value != null)
                        result[field.Name] = value;
                }
                catch
                {
                    // Skip fields that fail to read
                }
            }

            return result;
        }

        private object ReadField(IntPtr objPtr, EffectHandlerField field, Func<IntPtr, string> readAssetName)
        {
            IntPtr fieldPtr = objPtr + (int)field.Offset;

            switch (field.Category)
            {
                case "primitive":
                    return ReadPrimitive(fieldPtr, field.Type);

                case "enum":
                    return Marshal.ReadInt32(fieldPtr);

                case "string":
                    return ReadString(fieldPtr);

                case "reference":
                case "unity_asset":
                    IntPtr refPtr = Marshal.ReadIntPtr(fieldPtr);
                    if (refPtr == IntPtr.Zero) return null;
                    return readAssetName?.Invoke(refPtr);

                default:
                    return null;
            }
        }

        private object ReadPrimitive(IntPtr ptr, string typeName)
        {
            return typeName.ToLowerInvariant() switch
            {
                "float" => ReadFloat(ptr),
                "int" => Marshal.ReadInt32(ptr),
                "int32" => Marshal.ReadInt32(ptr),
                "bool" => Marshal.ReadByte(ptr) != 0,
                "byte" => Marshal.ReadByte(ptr),
                "double" => ReadDouble(ptr),
                "long" => Marshal.ReadInt64(ptr),
                "int64" => Marshal.ReadInt64(ptr),
                _ => Marshal.ReadInt32(ptr)
            };
        }

        private float ReadFloat(IntPtr ptr)
        {
            int bits = Marshal.ReadInt32(ptr);
            return BitConverter.Int32BitsToSingle(bits);
        }

        private double ReadDouble(IntPtr ptr)
        {
            long bits = Marshal.ReadInt64(ptr);
            return BitConverter.Int64BitsToDouble(bits);
        }

        private string ReadString(IntPtr fieldPtr)
        {
            try
            {
                IntPtr strPtr = Marshal.ReadIntPtr(fieldPtr);
                if (strPtr == IntPtr.Zero) return null;

                // IL2CPP string layout: [object header] [length int32] [chars...]
                int headerSize = IntPtr.Size * 2;
                int length = Marshal.ReadInt32(strPtr + headerSize);
                if (length <= 0 || length > 10000) return null;

                IntPtr charsPtr = strPtr + headerSize + 4;
                return Marshal.PtrToStringUni(charsPtr, length);
            }
            catch
            {
                return null;
            }
        }

        private static string GetShortTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return fullTypeName;
            int lastDot = fullTypeName.LastIndexOf('.');
            return lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;
        }

        /// <summary>
        /// Find all embedded type references in raw serialized MonoBehaviour data.
        /// Used for analyzing serialized asset bundles (not IL2CPP runtime data).
        /// </summary>
        public static List<TypeReference> FindTypeReferences(byte[] bytes)
        {
            var results = new List<TypeReference>();

            for (int i = 0; i < bytes.Length - AssemblyMarker.Length; i++)
            {
                if (MatchesPattern(bytes, i, AssemblyMarker))
                {
                    // Found ", Assembly-CSharp" - scan backwards to find type name start
                    int typeStart = FindTypeNameStart(bytes, i);
                    if (typeStart >= 0)
                    {
                        try
                        {
                            var typeName = Encoding.Unicode.GetString(bytes, typeStart, i - typeStart);
                            var shortName = GetShortTypeName(typeName);
                            results.Add(new TypeReference
                            {
                                Offset = typeStart,
                                TypeName = typeName,
                                ShortName = shortName
                            });
                        }
                        catch
                        {
                            // Invalid string encoding, skip
                        }
                    }
                }
            }

            return results;
        }

        private static bool MatchesPattern(byte[] bytes, int offset, byte[] pattern)
        {
            if (offset + pattern.Length > bytes.Length) return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (bytes[offset + i] != pattern[i]) return false;
            }

            return true;
        }

        private static int FindTypeNameStart(byte[] bytes, int markerOffset)
        {
            // Scan backwards to find the start of the UTF-16 type name
            // Type names are alphanumeric with dots, in UTF-16
            int pos = markerOffset - 2; // Skip last char before marker

            while (pos >= 2)
            {
                ushort charCode = (ushort)(bytes[pos] | (bytes[pos + 1] << 8));

                // Valid type name characters: A-Z, a-z, 0-9, ., _
                bool isValidChar = (charCode >= 'A' && charCode <= 'Z') ||
                                   (charCode >= 'a' && charCode <= 'z') ||
                                   (charCode >= '0' && charCode <= '9') ||
                                   charCode == '.' || charCode == '_';

                if (!isValidChar)
                {
                    // Found the start boundary
                    return pos + 2;
                }

                pos -= 2;
            }

            return 0; // Type name starts at beginning
        }
    }
}
