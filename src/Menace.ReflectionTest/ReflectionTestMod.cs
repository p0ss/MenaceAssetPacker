using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

[assembly: MelonInfo(typeof(Menace.ReflectionTest.ReflectionTestMod), "Menace Reflection Test", "3.0.0", "MenaceModkit")]
[assembly: MelonGame(null, null)]

namespace Menace.ReflectionTest
{
    public class ReflectionTestMod : MelonMod
    {
        private string _outputPath = "";
        private StringBuilder _log = new StringBuilder();

        public override void OnInitializeMelon()
        {
            var modsDir = Path.GetDirectoryName(typeof(ReflectionTestMod).Assembly.Location) ?? "";
            var rootDir = Directory.GetParent(modsDir)?.FullName ?? "";
            _outputPath = Path.Combine(rootDir, "UserData", "ReflectionTest");
            Directory.CreateDirectory(_outputPath);

            LoggerInstance.Msg("===========================================");
            LoggerInstance.Msg("Menace Reflection Test v3.0.0");
            LoggerInstance.Msg($"Output: {_outputPath}");
            LoggerInstance.Msg("===========================================");

            RunTestAsync();
        }

        private void Log(string msg)
        {
            _log.AppendLine(msg);
            LoggerInstance.Msg(msg);
            // Flush after every line so we can see what happened before a crash
            try { File.WriteAllText(Path.Combine(_outputPath, "v3-progress.txt"), _log.ToString()); }
            catch { }
        }

        private async void RunTestAsync()
        {
            Log("Waiting for game to load...");
            await Task.Delay(10000);

            for (int attempt = 1; attempt <= 30; attempt++)
            {
                if (RunTests())
                {
                    Log($"Tests completed on attempt {attempt}");
                    return;
                }
                Log($"Attempt {attempt}: not ready yet, waiting...");
                await Task.Delay(2000);
            }

            Log("Tests failed after 30 attempts");
        }

        private bool RunTests()
        {
            try
            {
                var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (gameAssembly == null)
                {
                    Log("Assembly-CSharp not found yet");
                    return false;
                }

                var weaponType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "WeaponTemplate" && !t.IsAbstract);

                if (weaponType == null)
                {
                    Log("WeaponTemplate type not found");
                    return false;
                }

                Log($"Found WeaponTemplate: {weaponType.FullName}");

                var il2cppType = Il2CppType.From(weaponType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);

                if (objects == null || objects.Length == 0)
                {
                    Log("No WeaponTemplate instances found yet");
                    return false;
                }

                Log($"Found {objects.Length} WeaponTemplate instances");

                // Pick cannon_long for testing
                UnityEngine.Object testObj = null;
                foreach (var obj in objects)
                {
                    if (obj != null && obj.name != null && obj.name.Contains("cannon_long"))
                    {
                        testObj = obj;
                        break;
                    }
                }
                if (testObj == null) testObj = objects[0];
                Log($"Test object: {testObj.name}");

                IntPtr ptr = IntPtr.Zero;
                if (testObj is Il2CppObjectBase il2cppObj)
                    ptr = il2cppObj.Pointer;
                Log($"IL2CPP pointer: 0x{ptr.ToInt64():X}");

                // ============================================================
                // APPROACH 1: Direct property call via Il2CppInterop proxy
                // Use Il2CppObjectBase.Cast<T>() via reflection
                // ============================================================
                Log("");
                Log("=== APPROACH 1: Il2CppObjectBase.TryCast via reflection ===");
                try
                {
                    var tryCastMethod = typeof(Il2CppObjectBase).GetMethod("TryCast");
                    if (tryCastMethod != null)
                    {
                        var genericTryCast = tryCastMethod.MakeGenericMethod(weaponType);
                        Log($"Calling TryCast<{weaponType.Name}>()...");
                        var castResult = genericTryCast.Invoke(testObj, null);
                        if (castResult != null)
                        {
                            Log($"TryCast succeeded: {castResult.GetType().FullName}");
                            ReadPropertiesFromObject(castResult, weaponType);
                        }
                        else
                        {
                            Log("TryCast returned null");
                        }
                    }
                    else
                    {
                        Log("TryCast method not found on Il2CppObjectBase");
                    }
                }
                catch (Exception ex)
                {
                    Log($"APPROACH 1 FAILED: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                        Log($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }

                // ============================================================
                // APPROACH 2: Read properties from the base UnityEngine.Object
                // (only works for properties defined on Object itself)
                // ============================================================
                Log("");
                Log("=== APPROACH 2: Properties on UnityEngine.Object (baseline) ===");
                try
                {
                    ReadPropertiesFromObject(testObj, typeof(UnityEngine.Object));
                }
                catch (Exception ex)
                {
                    Log($"APPROACH 2 FAILED: {ex.GetType().Name}: {ex.Message}");
                }

                // ============================================================
                // APPROACH 3: Read known fields at hardcoded offsets (baseline)
                // Compare against reflection results
                // ============================================================
                Log("");
                Log("=== APPROACH 3: Direct Marshal.Read at known offsets (baseline) ===");
                try
                {
                    if (ptr != IntPtr.Zero)
                    {
                        // These offsets are from the DataExtractor for WeaponTemplate
                        // They may be wrong for the current game version but serve as
                        // a comparison if approach 1 or 2 gives the same values
                        for (int offset = 0x18; offset <= 0x80; offset += 4)
                        {
                            var rawInt = Marshal.ReadInt32(ptr + offset);
                            var asFloat = BitConverter.ToSingle(BitConverter.GetBytes(rawInt), 0);
                            Log($"  ptr+0x{offset:X2}: int={rawInt}, float={asFloat:F4}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"APPROACH 3 FAILED: {ex.GetType().Name}: {ex.Message}");
                }

                // ============================================================
                // APPROACH 4: Constructor-based cast (the one that may crash)
                // Only try this LAST since it might segfault
                // ============================================================
                Log("");
                Log("=== APPROACH 4: Constructor(IntPtr) cast ===");
                try
                {
                    var ctor = weaponType.GetConstructor(new[] { typeof(IntPtr) });
                    if (ctor != null)
                    {
                        Log("Found IntPtr constructor, invoking...");
                        var castObj = ctor.Invoke(new object[] { ptr });
                        Log($"Constructor succeeded: {castObj?.GetType().FullName}");
                        if (castObj != null)
                        {
                            ReadPropertiesFromObject(castObj, weaponType);
                        }
                    }
                    else
                    {
                        Log("No IntPtr constructor found");
                    }
                }
                catch (Exception ex)
                {
                    Log($"APPROACH 4 FAILED: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                        Log($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }

                // ============================================================
                // Write final results
                // ============================================================
                Log("");
                Log("=== ALL TESTS COMPLETE ===");
                File.WriteAllText(
                    Path.Combine(_outputPath, "v3-results.txt"),
                    _log.ToString());

                return true;
            }
            catch (Exception ex)
            {
                Log($"FATAL ERROR: {ex}");
                return true; // Don't retry
            }
        }

        private void ReadPropertiesFromObject(object obj, Type declaredType)
        {
            var currentType = declaredType;
            int okCount = 0, errCount = 0;

            while (currentType != null && currentType != typeof(object) &&
                   currentType != typeof(Il2CppObjectBase))
            {
                var props = currentType.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (var p in props.Where(p => p.CanRead &&
                    p.Name != "Pointer" && p.Name != "ObjectClass" &&
                    p.Name != "WasCollected"))
                {
                    try
                    {
                        var value = p.GetValue(obj);
                        Log($"  OK  {p.Name} ({p.PropertyType.Name}) = {FormatValue(value)}");
                        okCount++;
                    }
                    catch (Exception ex)
                    {
                        var inner = ex.InnerException ?? ex;
                        Log($"  ERR {p.Name} ({p.PropertyType.Name}) => {inner.GetType().Name}: {inner.Message}");
                        errCount++;
                    }
                }

                currentType = currentType.BaseType;
            }

            Log($"  Results: {okCount} OK, {errCount} errors");
        }

        private string FormatValue(object value)
        {
            if (value == null) return "(null)";

            if (value is Il2CppObjectBase)
            {
                try
                {
                    if (value is UnityEngine.Object unityObj)
                    {
                        var n = unityObj.name;
                        if (!string.IsNullOrEmpty(n))
                            return $"[{value.GetType().Name}: {n}]";
                    }
                }
                catch { }
                return $"[{value.GetType().Name}]";
            }

            if (value is float f) return f.ToString("F4");
            if (value is double d) return d.ToString("F4");
            if (value is bool b) return b.ToString();
            if (value is int || value is long || value is short || value is byte)
                return value.ToString();
            if (value is Enum e) return $"{e} ({Convert.ToInt32(e)})";
            if (value is string s) return s;

            return value.ToString();
        }
    }
}
