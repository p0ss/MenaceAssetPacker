using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

[assembly: MelonInfo(typeof(Menace.DataExtractor.DataExtractorMod), "Menace Data Extractor", "4.0.0", "MenaceModkit")]
[assembly: MelonGame(null, null)]

namespace Menace.DataExtractor
{
    public class DataExtractorMod : MelonMod
    {
        private string _outputPath = "";
        private string _debugLogPath = "";
        private bool _hasSaved = false;

        // Properties to skip during extraction
        private static readonly HashSet<string> SkipProperties = new(StringComparer.Ordinal)
        {
            "Pointer", "ObjectClass", "WasCollected", "m_CachedPtr",
            "hideFlags", "serializationData", "SerializationData",
            "SerializedBytesString", "UnitySerializedFields",
            "PrefabModificationsReapplied"
        };

        // Base types where we stop walking the inheritance chain
        private static readonly HashSet<string> StopBaseTypes = new(StringComparer.Ordinal)
        {
            "Object", "Il2CppObjectBase", "Il2CppSystem.Object",
            "ScriptableObject", "SerializedScriptableObject"
        };

        private MethodInfo _tryCastMethod;

        // Sentinel value indicating TryReadFieldDirect can't handle this type
        private static readonly object _skipSentinel = new object();

        // Cache: templateTypeName -> { propName -> (fieldPtr, offset) }
        // Avoids redundant il2cpp_class_get_field_from_name + il2cpp_field_get_offset calls
        private readonly Dictionary<string, Dictionary<string, (IntPtr field, uint offset)>> _fieldInfoCache = new();

        // Cache: IL2CPP class name -> managed Type (for ScriptableObject-based discovery)
        private readonly Dictionary<string, Type> _il2cppNameToType = new(StringComparer.Ordinal);

        // Cache: IL2CPP class name -> native class pointer (captured during classification, avoids
        // il2cpp_object_get_class on data pointers which can SIGSEGV on stale/garbage pointers)
        private readonly Dictionary<string, IntPtr> _il2cppClassPtrCache = new(StringComparer.Ordinal);

        public override void OnInitializeMelon()
        {
            var modsDir = Path.GetDirectoryName(typeof(DataExtractorMod).Assembly.Location) ?? "";
            var rootDir = Directory.GetParent(modsDir)?.FullName ?? "";
            _outputPath = Path.Combine(rootDir, "UserData", "ExtractedData");
            _debugLogPath = Path.Combine(_outputPath, "_extraction_debug.log");
            Directory.CreateDirectory(_outputPath);

            // Clear previous debug log
            try { if (File.Exists(_debugLogPath)) File.Delete(_debugLogPath); } catch { }

            _tryCastMethod = typeof(Il2CppObjectBase).GetMethod("TryCast");

            LoggerInstance.Msg("===========================================");
            LoggerInstance.Msg("Menace Data Extractor v4.0.0 (Full Direct-Read)");
            LoggerInstance.Msg($"Output path: {_outputPath}");
            LoggerInstance.Msg("===========================================");

            MelonCoroutines.Start(RunExtractionCoroutine());
        }

        // Write directly to a file and flush — survives native crashes
        private void DebugLog(string message)
        {
            try
            {
                File.AppendAllText(_debugLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
            catch { }
        }

        // Shared state between PrepareExtraction and the phase loops
        private List<Type> _pendingTemplateTypes;
        private Dictionary<string, List<UnityEngine.Object>> _pendingObjectsByType;

        private System.Collections.IEnumerator RunExtractionCoroutine()
        {
            LoggerInstance.Msg("Waiting for game to load...");

            // Wait ~8 seconds on the main thread (yield each frame)
            float waited = 0f;
            while (waited < 8f)
            {
                yield return null;
                waited += Time.deltaTime;
            }

            for (int attempt = 1; attempt <= 60; attempt++)
            {
                if (PrepareExtraction())
                {
                    // Extraction is ready — run phases with yields between types
                    // so the game loop isn't starved

                    // ========== PHASE 1: Primitives ==========
                    DebugLog("=== PHASE 1: Primitive properties only ===");
                    var allTypeContexts = new List<TypeContext>();
                    int phase1Success = 0;

                    foreach (var templateType in _pendingTemplateTypes)
                    {
                        if (!_pendingObjectsByType.TryGetValue(templateType.Name, out var objects) || objects.Count == 0)
                        {
                            DebugLog($">>> P1 SKIP {templateType.Name} (no instances found)");
                            continue;
                        }

                        DebugLog($">>> P1 START {templateType.Name}");
                        try
                        {
                            var typeCtx = ExtractTypePhase1(templateType, objects);
                            if (typeCtx != null && typeCtx.Instances.Count > 0)
                            {
                                allTypeContexts.Add(typeCtx);
                                var dataList = typeCtx.Instances.Select(i => (object)i.Data).ToList();
                                SaveSingleTemplateType(templateType.Name, dataList);
                                phase1Success++;
                                DebugLog($"  SAVED {typeCtx.Instances.Count} instances (primitives)");
                                LoggerInstance.Msg($"  {templateType.Name}: {typeCtx.Instances.Count} instances (primitives)");
                            }
                            else
                            {
                                DebugLog($"  No instances extracted");
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLog($"  P1 EXCEPTION: {ex.Message}");
                        }
                        DebugLog($"<<< P1 END {templateType.Name}");

                        yield return null; // Give game a frame between types
                    }

                    DebugLog($"=== Phase 1 complete: {phase1Success} types saved ===");
                    LoggerInstance.Msg($"Phase 1 (primitives): {phase1Success} types saved");

                    // ========== PHASE 2: References ==========
                    DebugLog("=== PHASE 2: Reference properties ===");
                    int phase2Success = 0;

                    foreach (var typeCtx in allTypeContexts)
                    {
                        DebugLog($">>> P2 START {typeCtx.TemplateType.Name}");
                        try
                        {
                            bool anyRefs = false;
                            foreach (var inst in typeCtx.Instances)
                            {
                                if (FillReferenceProperties(inst, typeCtx.TemplateType))
                                    anyRefs = true;
                            }

                            if (anyRefs)
                            {
                                var dataList = typeCtx.Instances.Select(i => (object)i.Data).ToList();
                                SaveSingleTemplateType(typeCtx.TemplateType.Name, dataList);
                                DebugLog($"  UPDATED with references");
                            }
                            else
                            {
                                DebugLog($"  No reference properties to fill");
                            }
                            phase2Success++;
                        }
                        catch (Exception ex)
                        {
                            DebugLog($"  P2 EXCEPTION: {ex.Message}");
                        }
                        DebugLog($"<<< P2 END {typeCtx.TemplateType.Name}");

                        yield return null;
                    }

                    DebugLog($"=== Phase 2 complete: {phase2Success}/{allTypeContexts.Count} types updated ===");
                    LoggerInstance.Msg($"Phase 2 (references): {phase2Success}/{allTypeContexts.Count} types updated");

                    // ========== PHASE 3: Fix unknown names ==========
                    DebugLog("=== PHASE 3: Unity .name for remaining unknown names ===");
                    int phase3Fixed = 0;

                    foreach (var typeCtx in allTypeContexts)
                    {
                        bool anyFixed = false;
                        foreach (var inst in typeCtx.Instances)
                        {
                            if (inst.Name != null && inst.Name.StartsWith("unknown_") && inst.Pointer != IntPtr.Zero)
                            {
                                try
                                {
                                    var obj = new UnityEngine.Object(inst.Pointer);
                                    string unityName = obj.name;
                                    if (!string.IsNullOrEmpty(unityName))
                                    {
                                        inst.Name = unityName;
                                        inst.Data["name"] = unityName;
                                        anyFixed = true;
                                        phase3Fixed++;
                                    }
                                }
                                catch { }
                            }
                        }

                        if (anyFixed)
                        {
                            var dataList = typeCtx.Instances.Select(i => (object)i.Data).ToList();
                            SaveSingleTemplateType(typeCtx.TemplateType.Name, dataList);
                        }

                        yield return null;
                    }

                    DebugLog($"=== Phase 3 complete: fixed {phase3Fixed} names ===");
                    LoggerInstance.Msg($"Phase 3 (names): fixed {phase3Fixed} unknown names");

                    _hasSaved = phase1Success > 0;
                    LoggerInstance.Msg($"Extraction completed successfully on attempt {attempt}");
                    yield break;
                }

                // Wait ~2 seconds before retrying
                float retryWait = 0f;
                while (retryWait < 2f)
                {
                    yield return null;
                    retryWait += Time.deltaTime;
                }
            }

            LoggerInstance.Warning("Could not extract templates after 60 attempts");
        }

        // Holds context for a single template instance between phases
        private class InstanceContext
        {
            public Dictionary<string, object> Data;
            public object CastObj;
            public IntPtr Pointer; // IL2CPP native object pointer
            public string Name;
        }

        // Holds context for a template type between phases
        private class TypeContext
        {
            public Type TemplateType;
            public List<InstanceContext> Instances = new();
        }

        /// <summary>
        /// Get the IL2CPP class name for a native object pointer by walking up
        /// to find the most-derived class name.
        /// </summary>
        private string GetIl2CppClassName(IntPtr objectPointer)
        {
            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(objectPointer);
                if (klass == IntPtr.Zero) return null;
                IntPtr namePtr = IL2CPP.il2cpp_class_get_name(klass);
                if (namePtr == IntPtr.Zero) return null;
                return Marshal.PtrToStringAnsi(namePtr);
            }
            catch { return null; }
        }

        /// <summary>
        /// Enumerates an IL2CPP collection returned by DataTemplateLoader.GetAll&lt;T&gt;().
        /// IL2CPP collections don't implement managed System.Collections.IEnumerable,
        /// so we use Il2CppInterop's TryCast and reflection-based fallbacks.
        /// </summary>
        private List<UnityEngine.Object> EnumerateIl2CppCollection(object collection)
        {
            var results = new List<UnityEngine.Object>();

            // Strategy 1: TryCast to Il2CppSystem.Collections.IEnumerable (IL2CPP-level cast)
            if (collection is Il2CppObjectBase il2cppObj)
            {
                try
                {
                    var il2cppEnumerable = il2cppObj.TryCast<Il2CppSystem.Collections.IEnumerable>();
                    if (il2cppEnumerable != null)
                    {
                        var enumerator = il2cppEnumerable.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            var current = enumerator.Current;
                            if (current == null) continue;
                            var unityObj = current.TryCast<UnityEngine.Object>();
                            if (unityObj != null)
                                results.Add(unityObj);
                        }
                        if (results.Count > 0) return results;
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"    Il2Cpp IEnumerable strategy failed: {ex.Message}");
                }
            }

            // Strategy 2: Reflection-based GetEnumerator on the IL2CPP proxy type
            try
            {
                var collType = collection.GetType();
                var getEnumeratorMethod = collType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetEnumerator" && m.GetParameters().Length == 0);

                if (getEnumeratorMethod != null)
                {
                    var enumerator = getEnumeratorMethod.Invoke(collection, null);
                    if (enumerator != null)
                    {
                        var enumType = enumerator.GetType();
                        var moveNext = enumType.GetMethod("MoveNext",
                            BindingFlags.Public | BindingFlags.Instance);
                        var currentProp = enumType.GetProperty("Current",
                            BindingFlags.Public | BindingFlags.Instance);

                        if (moveNext != null && currentProp != null)
                        {
                            while ((bool)moveNext.Invoke(enumerator, null))
                            {
                                var item = currentProp.GetValue(enumerator);
                                AddAsUnityObject(results, item);
                            }
                            if (results.Count > 0) return results;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"    Reflection GetEnumerator strategy failed: {ex.Message}");
            }

            // Strategy 3: Count property + indexer
            try
            {
                var collType = collection.GetType();
                var countProp = collType.GetProperty("Count",
                    BindingFlags.Public | BindingFlags.Instance);

                if (countProp != null)
                {
                    int count = Convert.ToInt32(countProp.GetValue(collection));
                    var indexer = collType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p => p.GetIndexParameters().Length == 1);

                    if (indexer != null)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var item = indexer.GetValue(collection, new object[] { i });
                            AddAsUnityObject(results, item);
                        }
                        if (results.Count > 0) return results;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"    Count+indexer strategy failed: {ex.Message}");
            }

            // Strategy 4: managed IEnumerable (last resort)
            if (collection is System.Collections.IEnumerable managedEnumerable)
            {
                foreach (var item in managedEnumerable)
                    AddAsUnityObject(results, item);
            }

            return results;
        }

        private void AddAsUnityObject(List<UnityEngine.Object> results, object item)
        {
            if (item == null) return;

            if (item is UnityEngine.Object unityObj)
            {
                results.Add(unityObj);
                return;
            }

            if (item is Il2CppObjectBase il2cppItem)
            {
                var cast = il2cppItem.TryCast<UnityEngine.Object>();
                if (cast != null)
                    results.Add(cast);
            }
        }

        /// <summary>
        /// Quick preparation: finds assemblies, enumerates types, loads templates via
        /// DataTemplateLoader, checks readiness. Stores results in _pendingTemplateTypes
        /// and _pendingObjectsByType for the phase loops to consume.
        /// Returns true if ready to extract, false to retry later.
        /// </summary>
        private bool PrepareExtraction()
        {
            try
            {
                var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (gameAssembly == null)
                    return false;

                var templateTypes = gameAssembly.GetTypes()
                    .Where(t => t.Name.EndsWith("Template") && !t.IsAbstract)
                    .OrderBy(t => t.Name)
                    .ToList();

                if (templateTypes.Count == 0)
                    return false;

                // Build lookup: IL2CPP class name -> managed Type
                _il2cppNameToType.Clear();
                foreach (var t in templateTypes)
                    _il2cppNameToType[t.Name] = t;

                LoggerInstance.Msg($"Found {templateTypes.Count} template types, extracting...");

                // Clean previous extraction results
                CleanOutputDirectory();

                DebugLog($"=== Extraction started: {templateTypes.Count} types ===");
                for (int i = 0; i < templateTypes.Count; i++)
                    DebugLog($"  [{i}] {templateTypes[i].Name}");

                // Use DataTemplateLoader.GetAll<T>() — the game's own data pipeline.
                // Replaces FindObjectsOfTypeAll which crashes on Unity 6000.0.63+
                var loaderType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "DataTemplateLoader");

                if (loaderType == null)
                {
                    DebugLog("  DataTemplateLoader not found — cannot extract");
                    return false;
                }

                var getAllMethod = loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition);

                if (getAllMethod == null)
                {
                    DebugLog("  DataTemplateLoader.GetAll<T> not found");
                    return false;
                }

                DebugLog("=== Loading templates via DataTemplateLoader.GetAll<T> ===");
                var objectsByType = new Dictionary<string, List<UnityEngine.Object>>();
                _il2cppClassPtrCache.Clear();

                foreach (var templateType in templateTypes)
                {
                    try
                    {
                        var getAllGeneric = getAllMethod.MakeGenericMethod(templateType);
                        var collection = getAllGeneric.Invoke(null, null);
                        if (collection == null) continue;

                        var objects = EnumerateIl2CppCollection(collection);
                        if (objects.Count == 0) continue;

                        // Cache IL2CPP class pointer from first valid object
                        foreach (var obj in objects)
                        {
                            if (obj is Il2CppObjectBase il2cppBase && il2cppBase.Pointer != IntPtr.Zero)
                            {
                                try
                                {
                                    IntPtr klass = IL2CPP.il2cpp_object_get_class(il2cppBase.Pointer);
                                    if (klass != IntPtr.Zero)
                                    {
                                        _il2cppClassPtrCache[templateType.Name] = klass;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }

                        objectsByType[templateType.Name] = objects;
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"  GetAll<{templateType.Name}> failed: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                DebugLog($"  Loaded {objectsByType.Count} template types via DataTemplateLoader");
                foreach (var kvp in objectsByType.OrderBy(k => k.Key))
                    DebugLog($"    {kvp.Key}: {kvp.Value.Count} instances");

                if (objectsByType.Count == 0)
                {
                    DebugLog("  No templates loaded yet — will retry");
                    return false;
                }

                // Check if key templates have m_ID populated
                string[] keyTypes = { "WeaponTemplate", "ArmorTemplate", "AccessoryTemplate",
                                      "SkillTemplate", "PerkTemplate", "EntityTemplate" };
                bool allKeyTypesReady = true;
                int keyTypesChecked = 0;

                foreach (var keyType in keyTypes)
                {
                    if (!objectsByType.TryGetValue(keyType, out var keyObjects) || keyObjects.Count == 0)
                        continue;
                    if (!_il2cppClassPtrCache.TryGetValue(keyType, out var keyClass))
                        continue;

                    var sampleObj = keyObjects[0] as Il2CppObjectBase;
                    if (sampleObj == null || sampleObj.Pointer == IntPtr.Zero) continue;

                    IntPtr idField = FindNativeField(keyClass, "m_ID");
                    if (idField == IntPtr.Zero) continue;

                    uint idOffset = IL2CPP.il2cpp_field_get_offset(idField);
                    if (idOffset == 0) continue;

                    keyTypesChecked++;
                    IntPtr strPtr = Marshal.ReadIntPtr(sampleObj.Pointer + (int)idOffset);
                    if (strPtr != IntPtr.Zero)
                    {
                        string id = IL2CPP.Il2CppStringToManaged(strPtr);
                        DebugLog($"  {keyType}[0] m_ID={id}");
                        if (string.IsNullOrEmpty(id))
                            allKeyTypesReady = false;
                    }
                    else
                    {
                        DebugLog($"  {keyType}[0] m_ID=null (not yet initialized)");
                        allKeyTypesReady = false;
                    }
                }

                if (keyTypesChecked == 0 || !allKeyTypesReady)
                {
                    DebugLog("=== Key templates not yet initialized (m_ID is null on some), will retry ===");
                    return false;
                }

                // Ready — store for the phase loops
                _pendingTemplateTypes = templateTypes;
                _pendingObjectsByType = objectsByType;
                return true;
            }
            catch (Exception ex)
            {
                DebugLog($"PREPARE FATAL: {ex.Message}\n{ex.StackTrace}");
                LoggerInstance.Error($"PrepareExtraction error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Phase 1: Extract only primitive/safe properties from pre-collected objects.
        /// No per-type FindObjectsOfTypeAll calls. No IL2CPP property getters.
        /// </summary>
        private TypeContext ExtractTypePhase1(Type templateType, List<UnityEngine.Object> objects)
        {
            DebugLog($"  {objects.Count} instances to extract");
            var typeCtx = new TypeContext { TemplateType = templateType };

            // Get the cached IL2CPP class pointer (captured during classification)
            // This avoids calling il2cpp_object_get_class on potentially-stale object pointers
            IntPtr cachedKlass = IntPtr.Zero;
            _il2cppClassPtrCache.TryGetValue(templateType.Name, out cachedKlass);

            for (int i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                if (obj == null) { DebugLog($"  [{i}] null, skip"); continue; }

                // Get the IL2CPP pointer — if zero, object is invalid
                var il2cppCheck = obj as Il2CppObjectBase;
                if (il2cppCheck == null || il2cppCheck.Pointer == IntPtr.Zero)
                {
                    DebugLog($"  [{i}] zero pointer, skip");
                    continue;
                }

                // Check m_CachedPtr using cached class (no il2cpp_object_get_class call)
                IntPtr objPointer = il2cppCheck.Pointer;
                if (cachedKlass != IntPtr.Zero)
                {
                    if (!IsUnityObjectAliveWithClass(objPointer, cachedKlass))
                    {
                        DebugLog($"  [{i}] destroyed (m_CachedPtr=0), skip");
                        continue;
                    }
                }

                // Read the name via direct memory using cached class
                string objName = null;
                if (cachedKlass != IntPtr.Zero)
                    objName = ReadObjectNameWithClass(objPointer, cachedKlass);

                if (objName == null) objName = $"unknown_{i}";
                DebugLog($"  [{i}] {objName}");

                try
                {
                    var instCtx = ExtractPrimitives(obj, templateType, objName);
                    if (instCtx != null)
                    {
                        // Use m_ID as the canonical name if available (more reliable
                        // than ReadObjectNameDirect which can't read Unity native properties)
                        if (instCtx.Data.TryGetValue("m_ID", out var idVal) && idVal is string idStr && !string.IsNullOrEmpty(idStr))
                        {
                            instCtx.Name = idStr;
                            instCtx.Data["name"] = idStr;
                        }
                        typeCtx.Instances.Add(instCtx);
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"  [{i}] P1 FAILED: {ex.Message}");
                }
            }

            return typeCtx;
        }

        /// <summary>
        /// Extract only non-reference properties from a template instance.
        /// Uses direct memory reads via IL2CPP field offsets — completely bypasses
        /// property getters to avoid native SIGSEGV crashes.
        /// Returns an InstanceContext with the data dict and cast object for Phase 2.
        /// </summary>
        private InstanceContext ExtractPrimitives(UnityEngine.Object obj, Type templateType, string objName)
        {
            DebugLog($"    TryCast<{templateType.Name}>...");
            object castObj = null;
            try
            {
                var genericTryCast = _tryCastMethod.MakeGenericMethod(templateType);
                castObj = genericTryCast.Invoke(obj, null);
            }
            catch (Exception ex)
            {
                DebugLog($"    TryCast failed: {ex.Message}");
                return null;
            }

            if (castObj == null)
            {
                DebugLog($"    TryCast returned null");
                return null;
            }

            var il2cppBase = castObj as Il2CppObjectBase;
            if (il2cppBase == null || il2cppBase.Pointer == IntPtr.Zero)
            {
                DebugLog($"    Invalid pointer after TryCast");
                return null;
            }

            // Use cached class pointer from classification (no il2cpp_object_get_class on data pointer)
            IntPtr klass = IntPtr.Zero;
            _il2cppClassPtrCache.TryGetValue(templateType.Name, out klass);
            DebugLog($"    TryCast OK ptr={il2cppBase.Pointer} klass={klass}, reading primitives (direct)...");

            var data = new Dictionary<string, object>();
            data["name"] = objName;

            var currentType = templateType;
            while (currentType != null && !StopBaseTypes.Contains(currentType.Name))
            {
                PropertyInfo[] props;
                try
                {
                    props = currentType.GetProperties(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                }
                catch
                {
                    currentType = currentType.BaseType;
                    continue;
                }

                foreach (var prop in props)
                {
                    if (!prop.CanRead) continue;
                    if (SkipProperties.Contains(prop.Name)) continue;
                    if (data.ContainsKey(prop.Name)) continue;
                    if (ShouldSkipPropertyType(prop)) continue;

                    // Skip IL2CPP reference properties — Phase 2 handles these
                    if (IsIl2CppReferenceProperty(prop))
                        continue;

                    DebugLog($"    .{prop.Name} ({prop.PropertyType.Name})");

                    // Read field value directly from memory (no property getter calls)
                    if (klass != IntPtr.Zero)
                    {
                        object directValue = TryReadFieldDirect(
                            klass, templateType.Name, prop.Name, prop.PropertyType, il2cppBase.Pointer);
                        if (directValue != _skipSentinel)
                        {
                            data[prop.Name] = directValue;
                            continue;
                        }
                    }

                    // Field not found or type not supported for direct read — skip
                    // (Do NOT fall back to property getter — that's what causes crashes)
                    DebugLog($"      -> skipped (no direct read)");
                }

                currentType = currentType.BaseType;
            }

            DebugLog($"    Done: {data.Count} primitive fields");
            return new InstanceContext
            {
                Data = data,
                CastObj = castObj,
                Pointer = il2cppBase.Pointer,
                Name = objName
            };
        }

        /// <summary>
        /// Look up a native field by name (trying multiple naming conventions)
        /// and cache the result. Returns (fieldPtr, offset).
        /// </summary>
        private (IntPtr field, uint offset) GetCachedFieldInfo(IntPtr klass, string typeName, string propName)
        {
            if (!_fieldInfoCache.TryGetValue(typeName, out var typeCache))
            {
                typeCache = new Dictionary<string, (IntPtr, uint)>();
                _fieldInfoCache[typeName] = typeCache;
            }

            if (typeCache.TryGetValue(propName, out var cached))
                return cached;

            // Look up the field in the native class (tries multiple naming conventions)
            IntPtr field = FindNativeField(klass, propName);
            uint offset = 0;

            if (field != IntPtr.Zero)
            {
                DebugLog($"      [cache] il2cpp_field_get_offset({typeName}.{propName})...");
                offset = IL2CPP.il2cpp_field_get_offset(field);
                DebugLog($"      [cache] offset={offset}");
            }
            else
            {
                DebugLog($"      [cache] field not found for {typeName}.{propName}");
            }

            var result = (field, offset);
            typeCache[propName] = result;
            return result;
        }

        /// <summary>
        /// Find a native IL2CPP field by name, trying several naming conventions.
        /// Walks parent classes because fields may be defined on a base type.
        /// </summary>
        private IntPtr FindNativeField(IntPtr klass, string propName)
        {
            string[] namesToTry = new[]
            {
                propName,
                propName.Length > 0 && char.IsUpper(propName[0])
                    ? char.ToLower(propName[0]) + propName.Substring(1) : null,
                "_" + propName,
                "m_" + propName,
                $"<{propName}>k__BackingField"
            };

            IntPtr searchKlass = klass;
            while (searchKlass != IntPtr.Zero)
            {
                foreach (var name in namesToTry)
                {
                    if (name == null) continue;
                    IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(searchKlass, name);
                    if (field != IntPtr.Zero) return field;
                }
                searchKlass = IL2CPP.il2cpp_class_get_parent(searchKlass);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Read a field value directly from the object's memory at the field's offset.
        /// Completely bypasses IL2CPP interop property getters.
        /// Returns _skipSentinel if the field can't be found or the type isn't supported.
        /// </summary>
        private object TryReadFieldDirect(IntPtr klass, string typeName, string propName, Type propType, IntPtr objectPointer)
        {
            try
            {
                var (field, offset) = GetCachedFieldInfo(klass, typeName, propName);

                if (field == IntPtr.Zero || offset == 0)
                    return _skipSentinel;

                IntPtr addr = objectPointer + (int)offset;

                // Primitive types — direct Marshal reads
                if (propType == typeof(int))
                    return Marshal.ReadInt32(addr);
                if (propType == typeof(uint))
                    return (int)(uint)Marshal.ReadInt32(addr); // store as int for JSON
                if (propType == typeof(float))
                    return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(addr)), 0);
                if (propType == typeof(bool))
                    return Marshal.ReadByte(addr) != 0;
                if (propType == typeof(byte))
                    return (int)Marshal.ReadByte(addr);
                if (propType == typeof(short))
                    return (int)Marshal.ReadInt16(addr);
                if (propType == typeof(ushort))
                    return (int)(ushort)Marshal.ReadInt16(addr);
                if (propType == typeof(long))
                    return Marshal.ReadInt64(addr);
                if (propType == typeof(ulong))
                    return (long)(ulong)Marshal.ReadInt64(addr);
                if (propType == typeof(double))
                    return BitConverter.Int64BitsToDouble(Marshal.ReadInt64(addr));

                // String — read the Il2CppString pointer, then convert
                if (propType == typeof(string))
                {
                    IntPtr strPtr = Marshal.ReadIntPtr(addr);
                    if (strPtr == IntPtr.Zero) return null;
                    return IL2CPP.Il2CppStringToManaged(strPtr);
                }

                // Enum — read based on underlying type
                if (propType.IsEnum)
                {
                    var underlying = Enum.GetUnderlyingType(propType);
                    if (underlying == typeof(int))
                        return Marshal.ReadInt32(addr);
                    if (underlying == typeof(byte))
                        return (int)Marshal.ReadByte(addr);
                    if (underlying == typeof(short))
                        return (int)Marshal.ReadInt16(addr);
                    if (underlying == typeof(long))
                        return Marshal.ReadInt64(addr);
                    return Marshal.ReadInt32(addr); // default
                }

                // Unity struct types — read component floats directly
                string typeName2 = propType.Name;
                if (typeName2 == "Vector2")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", ReadFloat(addr) },
                        { "y", ReadFloat(addr + 4) }
                    };
                }
                if (typeName2 == "Vector3")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", ReadFloat(addr) },
                        { "y", ReadFloat(addr + 4) },
                        { "z", ReadFloat(addr + 8) }
                    };
                }
                if (typeName2 == "Vector4" || typeName2 == "Quaternion")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", ReadFloat(addr) },
                        { "y", ReadFloat(addr + 4) },
                        { "z", ReadFloat(addr + 8) },
                        { "w", ReadFloat(addr + 12) }
                    };
                }
                if (typeName2 == "Color")
                {
                    return new Dictionary<string, object>
                    {
                        { "r", ReadFloat(addr) },
                        { "g", ReadFloat(addr + 4) },
                        { "b", ReadFloat(addr + 8) },
                        { "a", ReadFloat(addr + 12) }
                    };
                }
                if (typeName2 == "Rect")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", ReadFloat(addr) },
                        { "y", ReadFloat(addr + 4) },
                        { "width", ReadFloat(addr + 8) },
                        { "height", ReadFloat(addr + 12) }
                    };
                }
                if (typeName2 == "Vector2Int")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", Marshal.ReadInt32(addr) },
                        { "y", Marshal.ReadInt32(addr + 4) }
                    };
                }
                if (typeName2 == "Vector3Int")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", Marshal.ReadInt32(addr) },
                        { "y", Marshal.ReadInt32(addr + 4) },
                        { "z", Marshal.ReadInt32(addr + 8) }
                    };
                }
                if (typeName2 == "Color32")
                {
                    return new Dictionary<string, object>
                    {
                        { "r", (int)Marshal.ReadByte(addr) },
                        { "g", (int)Marshal.ReadByte(addr + 1) },
                        { "b", (int)Marshal.ReadByte(addr + 2) },
                        { "a", (int)Marshal.ReadByte(addr + 3) }
                    };
                }

                // Type not supported for direct read
                return _skipSentinel;
            }
            catch (Exception ex)
            {
                DebugLog($"      direct-read error: {ex.GetType().Name}: {ex.Message}");
                return _skipSentinel;
            }
        }

        private static float ReadFloat(IntPtr addr)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(addr)), 0);
        }

        /// <summary>
        /// Check if a Unity Object is still alive by reading its m_CachedPtr field.
        /// Unity sets m_CachedPtr to zero when an object is destroyed.
        /// If m_CachedPtr is zero, ANY property access (including .name) will crash.
        /// </summary>
        private bool IsUnityObjectAlive(IntPtr objectPointer)
        {
            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(objectPointer);
                if (klass == IntPtr.Zero) return false;

                IntPtr cachedPtrField = IL2CPP.il2cpp_class_get_field_from_name(klass, "m_CachedPtr");
                if (cachedPtrField == IntPtr.Zero) return true; // can't check, assume alive

                uint offset = IL2CPP.il2cpp_field_get_offset(cachedPtrField);
                if (offset == 0) return true; // can't check, assume alive

                IntPtr nativePtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                return nativePtr != IntPtr.Zero;
            }
            catch
            {
                return false; // error reading = treat as dead
            }
        }

        /// <summary>
        /// Read a Menace template object's name directly from memory.
        /// Unity's Object.name has no backing field in IL2CPP (it's a native engine property),
        /// so we use m_ID from Menace.Tools.DataTemplate which all templates inherit.
        /// </summary>
        private string ReadObjectNameDirect(IntPtr objectPointer)
        {
            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(objectPointer);
                if (klass == IntPtr.Zero) return null;

                // m_ID is on Menace.Tools.DataTemplate — FindNativeField walks parents
                IntPtr idField = FindNativeField(klass, "m_ID");
                if (idField != IntPtr.Zero)
                {
                    uint offset = IL2CPP.il2cpp_field_get_offset(idField);
                    if (offset != 0)
                    {
                        IntPtr strPtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                        if (strPtr != IntPtr.Zero)
                        {
                            string id = IL2CPP.Il2CppStringToManaged(strPtr);
                            if (!string.IsNullOrEmpty(id))
                                return id;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Phase 2: Fill in IL2CPP reference properties for an already-extracted instance.
        /// Uses direct memory reads — NO property getters are called.
        /// Returns true if any reference properties were added.
        /// </summary>
        private bool FillReferenceProperties(InstanceContext inst, Type templateType)
        {
            if (inst.Pointer == IntPtr.Zero)
                return false;

            // Use cached class pointer (no il2cpp_object_get_class on data pointer)
            IntPtr klass = IntPtr.Zero;
            _il2cppClassPtrCache.TryGetValue(templateType.Name, out klass);
            if (klass == IntPtr.Zero) return false;

            bool addedAny = false;
            var currentType = templateType;

            while (currentType != null && !StopBaseTypes.Contains(currentType.Name))
            {
                PropertyInfo[] props;
                try
                {
                    props = currentType.GetProperties(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                }
                catch
                {
                    currentType = currentType.BaseType;
                    continue;
                }

                foreach (var prop in props)
                {
                    if (!prop.CanRead) continue;
                    if (SkipProperties.Contains(prop.Name)) continue;
                    if (inst.Data.ContainsKey(prop.Name)) continue;
                    if (ShouldSkipPropertyType(prop)) continue;

                    // Only handle reference properties in this phase
                    if (!IsIl2CppReferenceProperty(prop))
                        continue;

                    DebugLog($"    [{inst.Name}].{prop.Name} ({prop.PropertyType.Name})");

                    // Get the IL2CPP field + offset
                    var (field, offset) = GetCachedFieldInfo(klass, templateType.Name, prop.Name);
                    if (field == IntPtr.Zero || offset == 0)
                    {
                        DebugLog($"      -> field not found, skip");
                        continue;
                    }

                    // Get field type from IL2CPP METADATA (safe — reads type tables, not object memory)
                    IntPtr fieldType = IL2CPP.il2cpp_field_get_type(field);
                    if (fieldType == IntPtr.Zero) { DebugLog($"      -> no type info"); continue; }
                    int typeEnum = IL2CPP.il2cpp_type_get_type(fieldType);

                    // Read the raw pointer stored in the field
                    IntPtr refPtr = Marshal.ReadIntPtr(inst.Pointer + (int)offset);
                    if (refPtr == IntPtr.Zero)
                    {
                        inst.Data[prop.Name] = null;
                        DebugLog($"      -> null");
                        addedAny = true;
                        continue;
                    }

                    DebugLog($"      typeEnum={typeEnum} refPtr={refPtr}");

                    // Route based on IL2CPP type enum — all classification from metadata, no pointer dereference
                    if (typeEnum == 29) // IL2CPP_TYPE_SZARRAY — single-dimension array
                    {
                        object arrayVal = ReadArrayFromFieldMetadata(refPtr, fieldType, 0);
                        if (arrayVal != null)
                        {
                            inst.Data[prop.Name] = arrayVal;
                            addedAny = true;
                        }
                        DebugLog($"      -> array ({(arrayVal is List<object> l ? l.Count + " items" : "error")})");
                    }
                    else if (typeEnum == 18 || typeEnum == 21) // IL2CPP_TYPE_CLASS or IL2CPP_TYPE_GENERICINST
                    {
                        // Get the expected class from metadata (safe)
                        IntPtr expectedClass = IL2CPP.il2cpp_class_from_type(fieldType);
                        if (expectedClass == IntPtr.Zero) { DebugLog($"      -> no class"); continue; }

                        string className = GetClassNameSafe(expectedClass);
                        DebugLog($"      expected class: {className}");

                        if (IsLocalizationClass(className, expectedClass))
                        {
                            // Localization string — read m_DefaultTranslation using known class
                            string locText = ReadLocalizedStringWithClass(refPtr, expectedClass);
                            if (locText != null)
                            {
                                inst.Data[prop.Name] = locText;
                                if (prop.Name == "Title" && !inst.Data.ContainsKey("DisplayTitle"))
                                    inst.Data["DisplayTitle"] = locText;
                                else if (prop.Name == "ShortName" && !inst.Data.ContainsKey("DisplayShortName"))
                                    inst.Data["DisplayShortName"] = locText;
                                else if (prop.Name == "Description" && !inst.Data.ContainsKey("DisplayDescription"))
                                    inst.Data["DisplayDescription"] = locText;
                                addedAny = true;
                                DebugLog($"      -> localized: {(locText.Length > 60 ? locText[..60] + "..." : locText)}");
                            }
                            else
                            {
                                DebugLog($"      -> localization read failed");
                            }
                        }
                        else if (IsUnityObjectClass(expectedClass))
                        {
                            // Unity Object — check alive using known class, then read name
                            // Uses graduated name-reading: m_ID -> m_Name -> Unity .name
                            if (IsUnityObjectAliveWithClass(refPtr, expectedClass))
                            {
                                string assetName = ReadUnityAssetNameWithClass(refPtr, expectedClass, className);
                                if (prop.Name == "Icon")
                                {
                                    inst.Data["HasIcon"] = true;
                                    if (assetName != null)
                                        inst.Data["IconAssetName"] = assetName;
                                    DebugLog($"      -> HasIcon=true, name={assetName ?? "(unknown)"}");
                                }
                                else
                                {
                                    inst.Data[prop.Name] = assetName ?? $"({className})";
                                    DebugLog($"      -> {inst.Data[prop.Name]}");
                                }
                                addedAny = true;
                            }
                            else
                            {
                                if (prop.Name == "Icon")
                                    inst.Data["HasIcon"] = false;
                                else
                                    inst.Data[prop.Name] = null;
                                addedAny = true;
                                DebugLog($"      -> dead object");
                            }
                        }
                        else
                        {
                            // Nested non-Unity object — read fields using metadata-known class
                            object nested = ReadNestedObjectDirect(refPtr, expectedClass, className, 0);
                            inst.Data[prop.Name] = nested;
                            addedAny = true;
                            DebugLog($"      -> nested ({className})");
                        }
                    }
                    else
                    {
                        DebugLog($"      -> unhandled typeEnum {typeEnum}");
                    }
                }

                currentType = currentType.BaseType;
            }

            return addedAny;
        }

        // ── Metadata-driven helpers (never call il2cpp_object_get_class on data pointers) ──

        /// <summary>
        /// Get a class name from IL2CPP metadata (safe, no object memory reads).
        /// </summary>
        private string GetClassNameSafe(IntPtr klass)
        {
            if (klass == IntPtr.Zero) return "?";
            IntPtr namePtr = IL2CPP.il2cpp_class_get_name(klass);
            return namePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(namePtr) : "?";
        }

        /// <summary>
        /// Check if a class (from metadata) is a localization type by walking its parent chain.
        /// </summary>
        private bool IsLocalizationClass(string className, IntPtr klass)
        {
            if (className == "LocalizedLine" || className == "LocalizedMultiLine" ||
                className == "BaseLocalizedString")
                return true;

            // Check parent chain
            IntPtr parent = IL2CPP.il2cpp_class_get_parent(klass);
            while (parent != IntPtr.Zero)
            {
                string pName = GetClassNameSafe(parent);
                if (pName == "BaseLocalizedString")
                    return true;
                parent = IL2CPP.il2cpp_class_get_parent(parent);
            }
            return false;
        }

        /// <summary>
        /// Read m_DefaultTranslation from a localization object using a known class (from metadata).
        /// Does NOT call il2cpp_object_get_class — the class comes from field type metadata.
        /// </summary>
        private string ReadLocalizedStringWithClass(IntPtr objPtr, IntPtr klass)
        {
            try
            {
                IntPtr field = FindNativeField(klass, "m_DefaultTranslation");
                if (field == IntPtr.Zero) return null;

                uint offset = IL2CPP.il2cpp_field_get_offset(field);
                if (offset == 0) return null;

                IntPtr strPtr = Marshal.ReadIntPtr(objPtr + (int)offset);
                if (strPtr == IntPtr.Zero) return null;

                return IL2CPP.Il2CppStringToManaged(strPtr);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a Unity Object's native pointer is alive, using a known class (from metadata).
        /// Does NOT call il2cpp_object_get_class — the class comes from field type metadata.
        /// </summary>
        private bool IsUnityObjectAliveWithClass(IntPtr objectPointer, IntPtr klass)
        {
            try
            {
                IntPtr cachedPtrField = FindNativeField(klass, "m_CachedPtr");
                if (cachedPtrField == IntPtr.Zero) return true; // can't check, assume alive

                uint offset = IL2CPP.il2cpp_field_get_offset(cachedPtrField);
                if (offset == 0) return true;

                IntPtr nativePtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                return nativePtr != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Read a template object's name (m_ID) using a known class (from metadata).
        /// Only reads m_ID — does NOT call Unity's .name property (which can SIGSEGV).
        /// For Unity asset names (Sprites etc.), use ReadUnityAssetNameWithClass separately.
        /// </summary>
        private string ReadObjectNameWithClass(IntPtr objectPointer, IntPtr klass)
        {
            try
            {
                IntPtr idField = FindNativeField(klass, "m_ID");
                if (idField != IntPtr.Zero)
                {
                    uint offset = IL2CPP.il2cpp_field_get_offset(idField);
                    if (offset != 0)
                    {
                        IntPtr strPtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                        if (strPtr != IntPtr.Zero)
                        {
                            string id = IL2CPP.Il2CppStringToManaged(strPtr);
                            if (!string.IsNullOrEmpty(id))
                                return id;
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Read a Unity asset's name using multiple strategies:
        /// 1. m_ID field (for DataTemplate-derived objects)
        /// 2. m_Name field via IL2CPP metadata (for standard Unity Objects like Sprite, Texture2D)
        /// 3. Unity .name property as last resort (wrapped in try-catch for SIGSEGV safety)
        /// </summary>
        private string ReadUnityAssetNameWithClass(IntPtr objectPointer, IntPtr klass, string className)
        {
            // Strategy 1: m_ID (works for DataTemplate-derived objects)
            string name = ReadObjectNameWithClass(objectPointer, klass);
            if (!string.IsNullOrEmpty(name))
                return name;

            // Strategy 2: m_Name field via IL2CPP metadata
            // Unity Objects store their name in a managed m_Name field in some builds
            try
            {
                IntPtr nameField = FindNativeField(klass, "m_Name");
                if (nameField != IntPtr.Zero)
                {
                    uint offset = IL2CPP.il2cpp_field_get_offset(nameField);
                    if (offset != 0)
                    {
                        IntPtr strPtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                        if (strPtr != IntPtr.Zero)
                        {
                            string mName = IL2CPP.Il2CppStringToManaged(strPtr);
                            if (!string.IsNullOrEmpty(mName))
                            {
                                DebugLog($"        m_Name strategy: {mName}");
                                return mName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"        m_Name strategy failed: {ex.Message}");
            }

            // Strategy 3: Unity .name property (last resort, can SIGSEGV on some objects)
            try
            {
                var obj = new UnityEngine.Object(objectPointer);
                string unityName = obj.name;
                if (!string.IsNullOrEmpty(unityName))
                {
                    DebugLog($"        Unity .name strategy: {unityName}");
                    return unityName;
                }
            }
            catch (Exception ex)
            {
                DebugLog($"        Unity .name strategy failed: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Read an IL2CPP array using field type metadata for element classification.
        /// The element class comes from metadata (safe), not from il2cpp_object_get_class on elements.
        /// Il2CppArray layout: [Il2CppObject header (2*IntPtr)] [IntPtr bounds] [int32 max_length] [elements...]
        /// </summary>
        private object ReadArrayFromFieldMetadata(IntPtr arrayPtr, IntPtr fieldType, int depth)
        {
            if (depth > 2) return null;

            try
            {
                // Get array class and element class from METADATA (safe)
                IntPtr arrayClass = IL2CPP.il2cpp_class_from_type(fieldType);
                if (arrayClass == IntPtr.Zero) return null;

                IntPtr elemClass = IL2CPP.il2cpp_class_get_element_class(arrayClass);
                if (elemClass == IntPtr.Zero) return null;

                // Read array length from known Il2CppArray layout
                int headerSize = IntPtr.Size * 3; // object header (2 ptrs) + bounds ptr
                int length = Marshal.ReadInt32(arrayPtr + headerSize);

                DebugLog($"        array length={length}");

                // Sanity check the length
                if (length < 0 || length > 10000) return null;
                if (length == 0) return new List<object>();
                if (length > 100) length = 100; // cap for safety

                int elementsOffset = headerSize + IntPtr.Size; // length is padded to IntPtr alignment
                bool elemIsValueType = IL2CPP.il2cpp_class_is_valuetype(elemClass);
                string elemClassName = GetClassNameSafe(elemClass);

                var result = new List<object>();

                if (elemIsValueType)
                {
                    int elemSize = IL2CPP.il2cpp_class_instance_size(elemClass) - IntPtr.Size * 2;
                    if (elemSize <= 0) elemSize = 4;

                    for (int i = 0; i < length; i++)
                    {
                        IntPtr addr = arrayPtr + elementsOffset + i * elemSize;
                        result.Add(ReadValueTypeElement(addr, elemClassName, elemSize));
                    }
                }
                else
                {
                    // Classify element type from metadata (safe, no data pointer dereference)
                    bool elemIsUnityObject = IsUnityObjectClass(elemClass);
                    bool elemIsLocalization = IsLocalizationClass(elemClassName, elemClass);

                    for (int i = 0; i < length; i++)
                    {
                        IntPtr elemPtr = Marshal.ReadIntPtr(arrayPtr + elementsOffset + i * IntPtr.Size);
                        if (elemPtr == IntPtr.Zero)
                        {
                            result.Add(null);
                            continue;
                        }

                        if (elemIsUnityObject)
                        {
                            if (IsUnityObjectAliveWithClass(elemPtr, elemClass))
                                result.Add(ReadUnityAssetNameWithClass(elemPtr, elemClass, elemClassName) ?? $"({elemClassName})");
                            else
                                result.Add(null);
                        }
                        else if (elemIsLocalization)
                        {
                            result.Add(ReadLocalizedStringWithClass(elemPtr, elemClass) ?? "");
                        }
                        else
                        {
                            result.Add(ReadNestedObjectDirect(elemPtr, elemClass, elemClassName, depth + 1));
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                DebugLog($"        array read error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Read an IL2CPP reference object via direct memory. Handles arrays,
        /// Unity Object references, and nested objects — no property getters.
        /// </summary>
        private object ReadReferenceDirect(IntPtr objPtr, Type expectedType, int depth)
        {
            if (objPtr == IntPtr.Zero) return null;
            if (depth > 3) return "(max depth)";

            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(objPtr);
                if (klass == IntPtr.Zero) return null;

                IntPtr classNamePtr = IL2CPP.il2cpp_class_get_name(klass);
                string className = classNamePtr != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(classNamePtr) : "?";

                // Check if this is an IL2CPP array (native Il2CppArray)
                // Il2CppArray layout: [Il2CppObject header] [IntPtr bounds] [int32 max_length] [elements...]
                // The class will have a rank > 0, or the type name ends with "[]"
                if (IsIl2CppArrayClass(klass))
                {
                    return ReadIl2CppArrayDirect(objPtr, klass, depth);
                }

                // Unity Object reference -> return its name (check alive first)
                if (IsUnityObjectClass(klass))
                {
                    if (!IsUnityObjectAlive(objPtr))
                        return "(destroyed)";
                    string name = ReadObjectNameDirect(objPtr);
                    return name ?? "(unnamed)";
                }

                // Nested IL2CPP object -> read its primitive fields
                return ReadNestedObjectDirect(objPtr, klass, className, depth);
            }
            catch (Exception ex)
            {
                DebugLog($"      ReadReferenceDirect error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if an IL2CPP class represents an array type.
        /// </summary>
        private bool IsIl2CppArrayClass(IntPtr klass)
        {
            try
            {
                int rank = IL2CPP.il2cpp_class_get_rank(klass);
                return rank > 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if an IL2CPP class derives from UnityEngine.Object
        /// by walking the parent chain and checking class names.
        /// </summary>
        private bool IsUnityObjectClass(IntPtr klass)
        {
            try
            {
                IntPtr check = klass;
                while (check != IntPtr.Zero)
                {
                    IntPtr namePtr = IL2CPP.il2cpp_class_get_name(check);
                    if (namePtr != IntPtr.Zero)
                    {
                        string name = Marshal.PtrToStringAnsi(namePtr);
                        if (name == "Object")
                        {
                            // Verify it's UnityEngine.Object, not System.Object
                            IntPtr nsPtr = IL2CPP.il2cpp_class_get_namespace(check);
                            string ns = nsPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(nsPtr) : "";
                            if (ns == "UnityEngine")
                                return true;
                        }
                    }
                    check = IL2CPP.il2cpp_class_get_parent(check);
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Read an IL2CPP array directly from memory.
        /// Il2CppArray layout: [object header (2*IntPtr)] [IntPtr bounds] [int32 max_length] [elements...]
        /// </summary>
        private object ReadIl2CppArrayDirect(IntPtr arrayPtr, IntPtr arrayKlass, int depth)
        {
            try
            {
                // Get element class to determine element size and type
                IntPtr elemKlass = IL2CPP.il2cpp_class_get_element_class(arrayKlass);

                // Read array length: offset = 2*IntPtr (object header) + IntPtr (bounds)
                int headerSize = IntPtr.Size * 3; // object header (2 ptrs) + bounds ptr
                int length = Marshal.ReadInt32(arrayPtr + headerSize);

                if (length <= 0) return new List<object>();
                if (length > 100) length = 100; // safety cap

                // Elements start after header + length field (aligned)
                int elementsOffset = headerSize + IntPtr.Size; // length is padded to IntPtr alignment

                // Determine if elements are value types or references
                bool elemIsValueType = IL2CPP.il2cpp_class_is_valuetype(elemKlass);

                var result = new List<object>();

                if (!elemIsValueType)
                {
                    // Reference array: each element is an IntPtr
                    for (int i = 0; i < length; i++)
                    {
                        IntPtr elemPtr = Marshal.ReadIntPtr(arrayPtr + elementsOffset + i * IntPtr.Size);
                        if (elemPtr == IntPtr.Zero)
                        {
                            result.Add(null);
                            continue;
                        }

                        // Check if element is a Unity Object (return name) or nested object
                        IntPtr elemObjKlass = IL2CPP.il2cpp_object_get_class(elemPtr);
                        if (IsUnityObjectClass(elemObjKlass))
                        {
                            result.Add(ReadObjectNameDirect(elemPtr));
                        }
                        else
                        {
                            result.Add(ReadReferenceDirect(elemPtr, null, depth + 1));
                        }
                    }
                }
                else
                {
                    // Value type array: each element is inline at class instance size
                    int elemSize = IL2CPP.il2cpp_class_instance_size(elemKlass) - IntPtr.Size * 2;
                    if (elemSize <= 0) elemSize = 4; // fallback

                    IntPtr elemNamePtr = IL2CPP.il2cpp_class_get_name(elemKlass);
                    string elemName = elemNamePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(elemNamePtr) : "?";

                    for (int i = 0; i < length; i++)
                    {
                        IntPtr addr = arrayPtr + elementsOffset + i * elemSize;
                        // Try to read as common value types
                        object val = ReadValueTypeElement(addr, elemName, elemSize);
                        result.Add(val);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                DebugLog($"      ReadIl2CppArrayDirect error: {ex.Message}");
                return new List<object>();
            }
        }

        /// <summary>
        /// Read a value type array element at the given address.
        /// </summary>
        private object ReadValueTypeElement(IntPtr addr, string elemTypeName, int elemSize)
        {
            try
            {
                switch (elemTypeName)
                {
                    case "Int32": return Marshal.ReadInt32(addr);
                    case "Single": return ReadFloat(addr);
                    case "Boolean": return Marshal.ReadByte(addr) != 0;
                    case "Byte": return (int)Marshal.ReadByte(addr);
                    case "Int16": return (int)Marshal.ReadInt16(addr);
                    case "Int64": return Marshal.ReadInt64(addr);
                    case "Double": return BitConverter.Int64BitsToDouble(Marshal.ReadInt64(addr));
                    case "UInt32": return (int)(uint)Marshal.ReadInt32(addr);
                    default:
                        // For structs, try to read as a dict of fields
                        return $"({elemTypeName})";
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Read a nested IL2CPP object's fields directly from memory.
        /// Returns a Dictionary of field name -> value for JSON serialization.
        /// </summary>
        private int _nestedDebugCount = 0;

        private object ReadNestedObjectDirect(IntPtr objPtr, IntPtr klass, string className, int depth)
        {
            bool doLog = _nestedDebugCount < 3;
            if (doLog) _nestedDebugCount++;

            try
            {
                var result = new Dictionary<string, object>();

                // Iterate all fields of this class (and parent classes)
                IntPtr walkKlass = klass;
                while (walkKlass != IntPtr.Zero)
                {
                    if (doLog)
                    {
                        IntPtr cn = IL2CPP.il2cpp_class_get_name(walkKlass);
                        string cname = cn != IntPtr.Zero ? Marshal.PtrToStringAnsi(cn) : "?";
                        DebugLog($"        [nested] class={cname}");
                    }

                    IntPtr iter = IntPtr.Zero;
                    IntPtr field;
                    while ((field = IL2CPP.il2cpp_class_get_fields(walkKlass, ref iter)) != IntPtr.Zero)
                    {
                        IntPtr fieldNamePtr = IL2CPP.il2cpp_field_get_name(field);
                        if (fieldNamePtr == IntPtr.Zero) continue;
                        string fieldName = Marshal.PtrToStringAnsi(fieldNamePtr);
                        if (string.IsNullOrEmpty(fieldName)) continue;

                        uint fieldOffset = IL2CPP.il2cpp_field_get_offset(field);
                        if (fieldOffset == 0) continue;

                        IntPtr fieldType = IL2CPP.il2cpp_field_get_type(field);
                        if (fieldType == IntPtr.Zero) continue;

                        int typeEnum = IL2CPP.il2cpp_type_get_type(fieldType);
                        IntPtr addr = objPtr + (int)fieldOffset;

                        if (doLog) DebugLog($"        [nested]   {fieldName} typeEnum={typeEnum} offset={fieldOffset}");

                        object value = typeEnum switch
                        {
                            1 => Marshal.ReadByte(addr) != 0,           // IL2CPP_TYPE_BOOLEAN
                            2 => (int)Marshal.ReadByte(addr),            // IL2CPP_TYPE_CHAR
                            3 => (int)(sbyte)Marshal.ReadByte(addr),     // IL2CPP_TYPE_I1
                            4 => (int)Marshal.ReadByte(addr),            // IL2CPP_TYPE_U1
                            5 => (int)Marshal.ReadInt16(addr),           // IL2CPP_TYPE_I2
                            6 => (int)(ushort)Marshal.ReadInt16(addr),   // IL2CPP_TYPE_U2
                            7 => Marshal.ReadInt32(addr),                // IL2CPP_TYPE_I4
                            8 => (int)(uint)Marshal.ReadInt32(addr),     // IL2CPP_TYPE_U4
                            9 => Marshal.ReadInt64(addr),                // IL2CPP_TYPE_I8
                            10 => (long)(ulong)Marshal.ReadInt64(addr),  // IL2CPP_TYPE_U8
                            11 => ReadFloat(addr),                       // IL2CPP_TYPE_R4
                            12 => BitConverter.Int64BitsToDouble(Marshal.ReadInt64(addr)), // IL2CPP_TYPE_R8
                            14 => ReadIl2CppStringAt(addr),              // IL2CPP_TYPE_STRING
                            _ => null
                        };

                        if (doLog && value != null) DebugLog($"        [nested]     -> {value}");

                        if (value != null)
                            result[fieldName] = value;
                    }

                    walkKlass = IL2CPP.il2cpp_class_get_parent(walkKlass);
                }

                return result.Count > 0 ? result : $"({className})";
            }
            catch
            {
                return $"({className})";
            }
        }

        /// <summary>
        /// Read an IL2CPP string pointer at the given address.
        /// </summary>
        private string ReadIl2CppStringAt(IntPtr addr)
        {
            try
            {
                IntPtr strPtr = Marshal.ReadIntPtr(addr);
                if (strPtr == IntPtr.Zero) return null;
                return IL2CPP.Il2CppStringToManaged(strPtr);
            }
            catch { return null; }
        }

        /// <summary>
        /// Check if a property returns an IL2CPP reference type (array, list, object)
        /// that could cause a native crash if the backing field is null.
        /// </summary>
        private bool IsIl2CppReferenceProperty(PropertyInfo prop)
        {
            var pt = prop.PropertyType;
            try
            {
                // Il2CppReferenceArray<T>, Il2CppStructArray<T>
                if (pt.IsGenericType)
                {
                    var genDef = pt.GetGenericTypeDefinition();
                    if (genDef != null)
                    {
                        var genName = genDef.Name;
                        if (genName.StartsWith("Il2CppReferenceArray") ||
                            genName.StartsWith("Il2CppStructArray"))
                            return true;
                    }
                }
                // Any Il2CppObjectBase-derived type (including Il2Cpp collections, nested objects)
                if (typeof(Il2CppObjectBase).IsAssignableFrom(pt))
                    return true;
            }
            catch
            {
                // If we can't determine the type, treat as reference for safety
                return true;
            }
            return false;
        }

        private bool ShouldSkipPropertyType(PropertyInfo prop)
        {
            var propType = prop.PropertyType;

            // Skip delegates/actions
            if (typeof(Delegate).IsAssignableFrom(propType))
                return true;

            // Skip IntPtr/UIntPtr
            if (propType == typeof(IntPtr) || propType == typeof(UIntPtr))
                return true;

            // Skip indexer properties
            if (prop.GetIndexParameters().Length > 0)
                return true;

            return false;
        }

        private void SaveSingleTemplateType(string typeName, List<object> templates)
        {
            try
            {
                string filePath = Path.Combine(_outputPath, $"{typeName}.json");
                var json = JsonConvert.SerializeObject(templates, Formatting.Indented, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include,
                    MaxDepth = 10
                });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                DebugLog($"  Failed to save {typeName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete all previous .json files in the output directory so stale template
        /// types (abstract base classes with no instances) don't persist across runs.
        /// </summary>
        private void CleanOutputDirectory()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_outputPath, "*.json"))
                {
                    try { File.Delete(file); } catch { }
                }
                DebugLog("Cleaned output directory");
            }
            catch { }
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
