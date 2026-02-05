#nullable disable
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.TwitchSquaddies;

/// <summary>
/// Runtime type discovery and data access for the squad/squaddie object graph.
/// Resolves all types by name from Assembly-CSharp — no hardcoded offsets.
/// Squaddie is a plain IL2CPP object (not a Unity Object), so must be reached
/// via StrategyState.Get().Squaddies / .Roster.
/// </summary>
public class SquaddieExplorer
{
    private readonly MelonLogger.Instance _log;

    // Resolved types
    private Type _strategyStateType;
    private Type _squaddiesType;
    private Type _squaddieType;
    private Type _baseUnitLeaderType;
    private Type _rosterType;
    private Type _homePlanetTypeEnum;

    // StrategyState.Get() static method
    private MethodInfo _strategyStateGet;

    // StrategyState instance properties
    private PropertyInfo _strategyStateSquaddies;
    private PropertyInfo _strategyStateRoster;

    // Squaddies methods
    private MethodInfo _squaddiesGetAllAlive;
    private MethodInfo _squaddiesGetAliveCount;

    // Squaddie accessors
    private MethodInfo _squaddieGetId;
    private MethodInfo _squaddieGetGender;
    private MethodInfo _squaddieGetSkinColor;
    private MethodInfo _squaddieGetMissionsParticipated;
    private MethodInfo _squaddieGetHomePlanetName;
    private MethodInfo _squaddieGetCurrentLeader;
    private PropertyInfo _squaddieName;
    private PropertyInfo _squaddieNickname;
    private FieldInfo _squaddieHomePlanet;

    // Roster methods
    private MethodInfo _rosterGetHiredLeaders;

    // BaseUnitLeader methods
    private MethodInfo _leaderGetSquaddieIds;
    private MethodInfo _leaderGetSquaddie;
    private MethodInfo _leaderGetNickname;
    private MethodInfo _leaderGetHealthStatus;
    private MethodInfo _leaderGetTemplate;

    public bool IsReady { get; private set; }

    // DTOs for managed-side consumption
    public class SquaddieInfo
    {
        public int Id;
        public string Name;
        public string Nickname;
        public string HomePlanetName;
        public string Gender;
        public string SkinColor;
        public int MissionsParticipated;
        public string CurrentLeader;
        public object Proxy; // the IL2CPP managed proxy object
    }

    public class LeaderInfo
    {
        public string Nickname;
        public string HealthStatus;
        public string TemplateName;
        public List<int> SquaddieIds = new();
        public object Proxy;
    }

    public SquaddieExplorer(MelonLogger.Instance log)
    {
        _log = log;
    }

    /// <summary>
    /// Attempt to resolve all types and cache reflection info.
    /// Returns true if the core types needed for data access are available.
    /// </summary>
    public bool TrySetup()
    {
        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            _log.Msg("Assembly-CSharp not loaded yet");
            return false;
        }

        _log.Msg($"Assembly-CSharp found, {gameAssembly.GetTypes().Length} types");

        // Broad search: log all types containing key names
        LogTypesContaining(gameAssembly, "Squad");
        LogTypesContaining(gameAssembly, "Roster");
        LogTypesContaining(gameAssembly, "Leader");

        // Targeted resolution
        _strategyStateType = ResolveType(gameAssembly, "StrategyState");
        _squaddiesType = ResolveType(gameAssembly, "Squaddies");
        _squaddieType = ResolveType(gameAssembly, "Squaddie");
        _baseUnitLeaderType = ResolveType(gameAssembly, "BaseUnitLeader");
        _rosterType = ResolveType(gameAssembly, "Roster");
        _homePlanetTypeEnum = ResolveType(gameAssembly, "HomePlanetType");

        if (_strategyStateType == null || _squaddiesType == null || _squaddieType == null)
        {
            _log.Warning("Core types not found — cannot proceed");
            return false;
        }

        // Log full type info for each resolved type
        LogTypeDetails(_strategyStateType, "StrategyState");
        LogTypeDetails(_squaddiesType, "Squaddies");
        LogTypeDetails(_squaddieType, "Squaddie");
        if (_baseUnitLeaderType != null) LogTypeDetails(_baseUnitLeaderType, "BaseUnitLeader");
        if (_rosterType != null) LogTypeDetails(_rosterType, "Roster");
        if (_homePlanetTypeEnum != null) LogEnumValues(_homePlanetTypeEnum, "HomePlanetType");

        // Cache methods and properties
        if (!CacheReflection())
            return false;

        // Verify StrategyState.Get() is callable (may return null if not in strategy scene)
        try
        {
            var state = _strategyStateGet.Invoke(null, null);
            _log.Msg($"StrategyState.Get() = {(state != null ? "available" : "null (not in strategy scene)")}");
        }
        catch (Exception ex)
        {
            _log.Msg($"StrategyState.Get() threw: {ex.InnerException?.Message ?? ex.Message}");
        }

        IsReady = true;
        return true;
    }

    /// <summary>
    /// Returns info for all alive squaddies, or empty list if not in strategy scene.
    /// </summary>
    public List<SquaddieInfo> GetAllSquaddieInfo()
    {
        var results = new List<SquaddieInfo>();
        if (!IsReady) return results;

        _log.Msg("[diag] GetAllSquaddieInfo: starting");

        var state = _strategyStateGet.Invoke(null, null);
        _log.Msg($"[diag] StrategyState.Get() -> {(state != null ? state.GetType().Name : "null")}");
        if (state == null) return results;

        var squaddiesContainer = _strategyStateSquaddies.GetValue(state);
        _log.Msg($"[diag] .Squaddies -> {(squaddiesContainer != null ? squaddiesContainer.GetType().Name : "null")}");
        if (squaddiesContainer == null) return results;

        // Get count from the Squaddies container (IReadOnlyList proxy lacks Count)
        int aliveCount = _squaddiesGetAliveCount != null
            ? (int)_squaddiesGetAliveCount.Invoke(squaddiesContainer, null)
            : 0;
        _log.Msg($"[diag] GetAliveCount() = {aliveCount}");
        if (aliveCount == 0) return results;

        var allAlive = _squaddiesGetAllAlive.Invoke(squaddiesContainer, null);
        _log.Msg($"[diag] GetAllAlive() -> {(allAlive != null ? allAlive.GetType().FullName : "null")}");
        if (allAlive == null) return results;

        // Use the Item indexer directly on IReadOnlyList
        var listType = allAlive.GetType();
        var indexer = listType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.GetIndexParameters().Length == 1);
        _log.Msg($"[diag] Indexer: {(indexer != null ? $"{indexer.Name}:{indexer.PropertyType.Name}" : "NOT FOUND")}");
        if (indexer == null) return results;

        for (int i = 0; i < aliveCount; i++)
        {
            _log.Msg($"[diag] Reading index {i}...");
            var rawProxy = indexer.GetValue(allAlive, new object[] { i });
            _log.Msg($"[diag] Index {i} -> {(rawProxy != null ? rawProxy.GetType().FullName : "null")}");
            _log.Msg($"[diag] Squaddie [{i}]: raw type = {rawProxy.GetType().FullName}");

            _log.Msg($"[diag] Squaddie [{i}]: creating typed proxy...");
            var proxy = CreateTypedProxy(rawProxy, _squaddieType);
            _log.Msg($"[diag] Squaddie [{i}]: typed proxy type = {proxy.GetType().FullName}");

            var info = new SquaddieInfo { Proxy = proxy };

            _log.Msg($"[diag] Squaddie [{i}]: calling GetId...");
            info.Id = SafeInvokeInt(_squaddieGetId, proxy);
            _log.Msg($"[diag] Squaddie [{i}]: Id = {info.Id}");

            _log.Msg($"[diag] Squaddie [{i}]: reading Name...");
            info.Name = SafeReadString(_squaddieName, proxy);
            _log.Msg($"[diag] Squaddie [{i}]: Name = {info.Name}");

            _log.Msg($"[diag] Squaddie [{i}]: reading Nickname...");
            info.Nickname = SafeReadString(_squaddieNickname, proxy);
            _log.Msg($"[diag] Squaddie [{i}]: Nickname = {info.Nickname}");

            _log.Msg($"[diag] Squaddie [{i}]: calling GetHomePlanetName...");
            info.HomePlanetName = SafeInvokeString(_squaddieGetHomePlanetName, proxy);
            _log.Msg($"[diag] Squaddie [{i}]: HomePlanet = {info.HomePlanetName}");

            _log.Msg($"[diag] Squaddie [{i}]: calling GetGender...");
            info.Gender = SafeInvokeString(_squaddieGetGender, proxy);

            _log.Msg($"[diag] Squaddie [{i}]: calling GetSkinColor...");
            info.SkinColor = SafeInvokeString(_squaddieGetSkinColor, proxy);

            _log.Msg($"[diag] Squaddie [{i}]: calling GetMissionsParticipated...");
            info.MissionsParticipated = SafeInvokeInt(_squaddieGetMissionsParticipated, proxy);

            _log.Msg($"[diag] Squaddie [{i}]: calling GetCurrentLeader...");
            if (_squaddieGetCurrentLeader != null)
            {
                info.CurrentLeader = "?";
                // Skip GetCurrentLeader for now — could crash
            }

            _log.Msg($"[diag] Squaddie [{i}]: done");
            results.Add(info);
        }

        _log.Msg($"[diag] GetAllSquaddieInfo: returning {results.Count} squaddies");
        return results;
    }

    /// <summary>
    /// Returns info for all hired leaders, or empty list if not in strategy scene.
    /// </summary>
    public List<LeaderInfo> GetAllLeaderInfo()
    {
        var results = new List<LeaderInfo>();
        if (!IsReady || _rosterType == null) return results;

        _log.Msg("[diag] GetAllLeaderInfo: starting");

        var state = _strategyStateGet.Invoke(null, null);
        if (state == null) return results;

        var roster = _strategyStateRoster?.GetValue(state);
        if (roster == null) { _log.Msg("[diag] Roster is null"); return results; }

        // Access m_HiredLeaders directly (it's a List<BaseUnitLeader>, has Count)
        var hiredLeadersProp = _rosterType.GetProperty("m_HiredLeaders",
            BindingFlags.Public | BindingFlags.Instance);
        if (hiredLeadersProp == null) { _log.Msg("[diag] m_HiredLeaders property not found"); return results; }

        var hiredLeaders = hiredLeadersProp.GetValue(roster);
        if (hiredLeaders == null) { _log.Msg("[diag] m_HiredLeaders is null"); return results; }

        _log.Msg($"[diag] m_HiredLeaders type: {hiredLeaders.GetType().FullName}");

        // Get Count and indexer from the List
        var listType = hiredLeaders.GetType();
        var countProp = listType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
        var indexer = listType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.GetIndexParameters().Length == 1);

        if (countProp == null) { _log.Msg("[diag] No Count on m_HiredLeaders"); return results; }
        if (indexer == null) { _log.Msg("[diag] No indexer on m_HiredLeaders"); return results; }

        int leaderCount = Convert.ToInt32(countProp.GetValue(hiredLeaders));
        _log.Msg($"[diag] Leader count: {leaderCount}");

        for (int i = 0; i < leaderCount; i++)
        {
            _log.Msg($"[diag] Leader [{i}]: reading...");
            var rawProxy = indexer.GetValue(hiredLeaders, new object[] { i });
            if (rawProxy == null) { _log.Msg($"[diag] Leader [{i}]: null"); continue; }

            _log.Msg($"[diag] Leader [{i}]: type = {rawProxy.GetType().FullName}");
            var proxy = CreateTypedProxy(rawProxy, _baseUnitLeaderType);
            var info = new LeaderInfo { Proxy = proxy };

            if (_leaderGetNickname != null)
            {
                try
                {
                    var nick = _leaderGetNickname.Invoke(proxy, null);
                    info.Nickname = ResolveLocalizedString(nick, $"Leader[{i}].Nickname");
                }
                catch { info.Nickname = "?"; }
            }
            _log.Msg($"[diag] Leader [{i}]: Nickname = {info.Nickname}");

            if (_leaderGetHealthStatus != null)
            {
                try
                {
                    var status = _leaderGetHealthStatus.Invoke(proxy, null);
                    info.HealthStatus = status?.ToString() ?? "?";
                }
                catch { info.HealthStatus = "?"; }
            }

            if (_leaderGetTemplate != null)
            {
                try
                {
                    var template = _leaderGetTemplate.Invoke(proxy, null);
                    info.TemplateName = template?.ToString() ?? "?";
                }
                catch { info.TemplateName = "?"; }
            }

            // Get squaddie IDs — m_SquaddieIds is a List<int> property on BaseUnitLeader
            var squaddieIdsProp = _baseUnitLeaderType.GetProperty("m_SquaddieIds",
                BindingFlags.Public | BindingFlags.Instance);
            if (squaddieIdsProp != null)
            {
                try
                {
                    var idsList = squaddieIdsProp.GetValue(proxy);
                    if (idsList != null)
                    {
                        var idsListType = idsList.GetType();
                        var idsCount = idsListType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                        var idsIndexer = idsListType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(p => p.GetIndexParameters().Length == 1);

                        if (idsCount != null && idsIndexer != null)
                        {
                            int idCount = Convert.ToInt32(idsCount.GetValue(idsList));
                            for (int j = 0; j < idCount; j++)
                            {
                                var idVal = idsIndexer.GetValue(idsList, new object[] { j });
                                if (idVal != null)
                                    info.SquaddieIds.Add(Convert.ToInt32(idVal));
                            }
                        }
                    }
                }
                catch { }
            }
            _log.Msg($"[diag] Leader [{i}]: {info.SquaddieIds.Count} squaddie IDs");

            results.Add(info);
        }

        _log.Msg($"[diag] GetAllLeaderInfo: returning {results.Count} leaders");
        return results;
    }

    /// <summary>
    /// Get the current alive squaddie count, or -1 if unavailable.
    /// </summary>
    public int GetAliveCount()
    {
        if (!IsReady) return -1;

        try
        {
            var state = _strategyStateGet.Invoke(null, null);
            if (state == null) return -1;

            var squaddiesContainer = _strategyStateSquaddies.GetValue(state);
            if (squaddiesContainer == null) return -1;

            if (_squaddiesGetAliveCount != null)
                return (int)_squaddiesGetAliveCount.Invoke(squaddiesContainer, null);

            return -1;
        }
        catch { return -1; }
    }

    /// <summary>
    /// Get the current hired leader count, or -1 if unavailable.
    /// </summary>
    public int GetLeaderCount()
    {
        if (!IsReady || _rosterType == null) return -1;

        try
        {
            var state = _strategyStateGet.Invoke(null, null);
            if (state == null) return -1;

            var roster = _strategyStateRoster?.GetValue(state);
            if (roster == null) return -1;

            // Access m_HiredLeaders directly (List<BaseUnitLeader>, has Count)
            // instead of GetHiredLeaders() which returns IReadOnlyList (no Count)
            var hiredLeadersProp = _rosterType.GetProperty("m_HiredLeaders",
                BindingFlags.Public | BindingFlags.Instance);
            if (hiredLeadersProp == null) return -1;

            var hiredLeaders = hiredLeadersProp.GetValue(roster);
            if (hiredLeaders == null) return -1;

            var countProp = hiredLeaders.GetType().GetProperty("Count",
                BindingFlags.Public | BindingFlags.Instance);
            if (countProp != null)
                return Convert.ToInt32(countProp.GetValue(hiredLeaders));

            return -1;
        }
        catch { return -1; }
    }

    /// <summary>
    /// Returns true if StrategyState.Get() returns non-null (we're in the strategy scene).
    /// </summary>
    public bool IsInStrategyScene()
    {
        if (!IsReady) return false;
        try
        {
            return _strategyStateGet.Invoke(null, null) != null;
        }
        catch { return false; }
    }

    /// <summary>
    /// Returns the Squaddie managed proxy type (needed by SquaddieReadWrite for Harmony patching).
    /// </summary>
    public Type GetSquaddieType() => _squaddieType;

    /// <summary>
    /// Returns the cached MethodInfo for Squaddie.GetHomePlanetName() (for Harmony patching).
    /// </summary>
    public MethodInfo GetHomePlanetNameMethod() => _squaddieGetHomePlanetName;

    /// <summary>
    /// Returns the cached MethodInfo for Squaddie.GetId() (for Harmony patching).
    /// </summary>
    public MethodInfo GetIdMethod() => _squaddieGetId;

    /// <summary>
    /// Returns the cached PropertyInfo for Squaddie.Name (for writing).
    /// </summary>
    public PropertyInfo GetNameProperty() => _squaddieName;

    /// <summary>
    /// Returns the cached PropertyInfo for Squaddie.Nickname (for writing).
    /// </summary>
    public PropertyInfo GetNicknameProperty() => _squaddieNickname;

    // --- Private helpers ---

    /// <summary>
    /// Creates a typed IL2CPP managed proxy from an untyped Il2CppSystem.Object.
    /// IL2CPP collection enumeration returns Il2CppSystem.Object — calling methods
    /// defined on a specific type (e.g. Squaddie.GetId()) on the wrong proxy type
    /// causes a native crash. This re-wraps the pointer in the correct proxy type.
    /// Same pattern as DevMode's LoadEntityTemplates.
    /// </summary>
    private object CreateTypedProxy(object item, Type targetType)
    {
        if (item is Il2CppObjectBase il2cppItem)
        {
            var ptr = il2cppItem.Pointer;
            if (ptr != IntPtr.Zero)
                return Activator.CreateInstance(targetType, new object[] { ptr });
        }
        return item;
    }

    private bool CacheReflection()
    {
        try
        {
            // StrategyState.Get()
            _strategyStateGet = _strategyStateType.GetMethod("Get",
                BindingFlags.Public | BindingFlags.Static);
            if (_strategyStateGet == null)
            {
                _log.Warning("StrategyState.Get() not found");
                return false;
            }

            // StrategyState.Squaddies (public readonly field exposed as property by Il2CppInterop)
            _strategyStateSquaddies = _strategyStateType.GetProperty("Squaddies",
                BindingFlags.Public | BindingFlags.Instance);
            if (_strategyStateSquaddies == null)
            {
                _log.Warning("StrategyState.Squaddies property not found");
                return false;
            }

            // StrategyState.Roster
            _strategyStateRoster = _strategyStateType.GetProperty("Roster",
                BindingFlags.Public | BindingFlags.Instance);
            if (_strategyStateRoster == null)
                _log.Msg("StrategyState.Roster property not found (leader info unavailable)");

            // Squaddies.GetAllAlive()
            _squaddiesGetAllAlive = _squaddiesType.GetMethod("GetAllAlive",
                BindingFlags.Public | BindingFlags.Instance);
            if (_squaddiesGetAllAlive == null)
            {
                _log.Warning("Squaddies.GetAllAlive() not found");
                return false;
            }

            // Squaddies.GetAliveCount()
            _squaddiesGetAliveCount = _squaddiesType.GetMethod("GetAliveCount",
                BindingFlags.Public | BindingFlags.Instance);

            // Squaddie accessors
            _squaddieGetId = _squaddieType.GetMethod("GetId",
                BindingFlags.Public | BindingFlags.Instance);
            _squaddieGetGender = _squaddieType.GetMethod("GetGender",
                BindingFlags.Public | BindingFlags.Instance);
            _squaddieGetSkinColor = _squaddieType.GetMethod("GetSkinColor",
                BindingFlags.Public | BindingFlags.Instance);
            _squaddieGetMissionsParticipated = _squaddieType.GetMethod("GetMissionsParticipated",
                BindingFlags.Public | BindingFlags.Instance);
            _squaddieGetHomePlanetName = _squaddieType.GetMethod("GetHomePlanetName",
                BindingFlags.Public | BindingFlags.Instance);
            _squaddieGetCurrentLeader = _squaddieType.GetMethod("GetCurrentLeader",
                BindingFlags.Public | BindingFlags.Instance);

            // Squaddie.Name, Squaddie.Nickname — public fields exposed as properties by Il2CppInterop
            _squaddieName = _squaddieType.GetProperty("Name",
                BindingFlags.Public | BindingFlags.Instance);
            _squaddieNickname = _squaddieType.GetProperty("Nickname",
                BindingFlags.Public | BindingFlags.Instance);

            if (_squaddieGetId == null)
            {
                _log.Warning("Squaddie.GetId() not found");
                return false;
            }

            _log.Msg($"Squaddie reflection: GetId={_squaddieGetId != null}, Name={_squaddieName != null}, " +
                     $"Nickname={_squaddieNickname != null}, GetHomePlanetName={_squaddieGetHomePlanetName != null}, " +
                     $"GetGender={_squaddieGetGender != null}, GetCurrentLeader={_squaddieGetCurrentLeader != null}");

            // Roster.GetHiredLeaders()
            if (_rosterType != null)
            {
                _rosterGetHiredLeaders = _rosterType.GetMethod("GetHiredLeaders",
                    BindingFlags.Public | BindingFlags.Instance);
                _log.Msg($"Roster.GetHiredLeaders = {_rosterGetHiredLeaders != null}");
            }

            // BaseUnitLeader methods
            if (_baseUnitLeaderType != null)
            {
                _leaderGetSquaddieIds = _baseUnitLeaderType.GetMethod("GetSquaddieIds",
                    BindingFlags.Public | BindingFlags.Instance);
                _leaderGetSquaddie = _baseUnitLeaderType.GetMethod("GetSquaddie",
                    BindingFlags.Public | BindingFlags.Instance);
                _leaderGetNickname = _baseUnitLeaderType.GetMethod("GetNickname",
                    BindingFlags.Public | BindingFlags.Instance);
                _leaderGetHealthStatus = _baseUnitLeaderType.GetMethod("GetHealthStatus",
                    BindingFlags.Public | BindingFlags.Instance);
                _leaderGetTemplate = _baseUnitLeaderType.GetMethod("GetTemplate",
                    BindingFlags.Public | BindingFlags.Instance);

                _log.Msg($"BaseUnitLeader reflection: GetSquaddieIds={_leaderGetSquaddieIds != null}, " +
                         $"GetNickname={_leaderGetNickname != null}, GetHealthStatus={_leaderGetHealthStatus != null}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"CacheReflection error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enumerates an IL2CPP collection using multiple strategies.
    /// Adapted from DevMode's multi-strategy pattern, but returns object
    /// instead of UnityEngine.Object since squaddies are plain IL2CPP objects.
    /// </summary>
    private List<object> EnumerateIl2CppCollection(object collection)
    {
        var results = new List<object>();
        var collType = collection.GetType();

        _log.Msg($"[diag] EnumerateIl2CppCollection: type = {collType.FullName}");

        // Log all properties on the collection type
        var allProps = collType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => $"{p.Name}:{p.PropertyType.Name}{(p.GetIndexParameters().Length > 0 ? "[idx]" : "")}")
            .ToList();
        _log.Msg($"[diag] Collection properties: {string.Join(", ", allProps)}");

        // Strategy 1: Count property + indexer (safest for IReadOnlyList)
        try
        {
            var countProp = collType.GetProperty("Count",
                BindingFlags.Public | BindingFlags.Instance);

            if (countProp != null)
            {
                int count = Convert.ToInt32(countProp.GetValue(collection));
                _log.Msg($"[diag] Count = {count}");

                var indexer = collType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.GetIndexParameters().Length == 1);

                if (indexer != null)
                {
                    _log.Msg($"[diag] Indexer found: {indexer.Name}, return type = {indexer.PropertyType.Name}");

                    for (int i = 0; i < count; i++)
                    {
                        _log.Msg($"[diag] Reading index {i}...");
                        var item = indexer.GetValue(collection, new object[] { i });
                        _log.Msg($"[diag] Index {i} -> {(item != null ? item.GetType().FullName : "null")}");
                        if (item != null)
                            results.Add(item);
                    }

                    if (results.Count > 0)
                    {
                        _log.Msg($"Enumerated via Count+indexer: {results.Count} items");
                        return results;
                    }
                }
                else
                {
                    _log.Msg("[diag] No indexer found on collection type");
                }
            }
            else
            {
                _log.Msg("[diag] No Count property found");
            }
        }
        catch (Exception ex)
        {
            _log.Msg($"Count+indexer strategy failed: {ex.Message}");
        }

        // Strategy 2: managed IEnumerable (fallback)
        if (collection is System.Collections.IEnumerable managedEnumerable)
        {
            _log.Msg("[diag] Trying managed IEnumerable...");
            foreach (var item in managedEnumerable)
            {
                if (item != null)
                    results.Add(item);
            }

            if (results.Count > 0)
                _log.Msg($"Enumerated via managed IEnumerable: {results.Count} items");
        }

        _log.Msg($"[diag] EnumerateIl2CppCollection: returning {results.Count} items");
        return results;
    }

    private Type ResolveType(Assembly assembly, string typeName)
    {
        var type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
        if (type != null)
            _log.Msg($"Resolved type: {typeName} -> {type.FullName}");
        else
            _log.Warning($"Type not found: {typeName}");
        return type;
    }

    private void LogTypesContaining(Assembly assembly, string keyword)
    {
        var matches = assembly.GetTypes()
            .Where(t => t.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Name)
            .Take(30)
            .ToList();
        _log.Msg($"Types containing '{keyword}' ({matches.Count}): {string.Join(", ", matches)}");
    }

    private void LogTypeDetails(Type type, string label)
    {
        _log.Msg($"--- {label} ({type.FullName}) ---");

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(p => $"{p.Name}:{p.PropertyType.Name}")
            .ToList();
        _log.Msg($"  Properties ({props.Count}): {string.Join(", ", props)}");

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(f => !f.Name.StartsWith("NativeFieldInfoPtr") && !f.Name.StartsWith("NativeMethodInfoPtr"))
            .Select(f => $"{(f.IsPublic ? "+" : "-")}{f.Name}:{f.FieldType.Name}")
            .ToList();
        _log.Msg($"  Fields ({fields.Count}): {string.Join(", ", fields)}");

        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => !m.IsSpecialName)
            .Select(m => $"{(m.IsStatic ? "static " : "")}{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
            .ToList();
        _log.Msg($"  Methods ({methods.Count}): {string.Join(", ", methods.Take(40))}");

        var nested = type.GetNestedTypes()
            .Select(t => t.Name)
            .ToList();
        if (nested.Count > 0)
            _log.Msg($"  Nested types: {string.Join(", ", nested)}");
    }

    private void LogEnumValues(Type enumType, string label)
    {
        var names = Enum.GetNames(enumType);
        var values = Enum.GetValues(enumType);
        var entries = new List<string>();
        for (int i = 0; i < names.Length; i++)
            entries.Add($"{names[i]}={Convert.ToInt32(values.GetValue(i))}");
        _log.Msg($"Enum {label}: {string.Join(", ", entries)}");
    }

    private int SafeInvokeInt(MethodInfo method, object target)
    {
        if (method == null) return -1;
        try { return (int)method.Invoke(target, null); }
        catch { return -1; }
    }

    private string SafeInvokeString(MethodInfo method, object target)
    {
        if (method == null) return "?";
        try { return method.Invoke(target, null)?.ToString() ?? "?"; }
        catch { return "?"; }
    }

    private string SafeReadString(PropertyInfo prop, object target)
    {
        if (prop == null) return "?";
        try { return prop.GetValue(target)?.ToString() ?? "?"; }
        catch { return "?"; }
    }

    /// <summary>
    /// Resolves a LocalizedLine (or similar localization wrapper) to its actual string value.
    /// Tries common accessor patterns: Value, Text, Line, GetValue(), Resolve(), then falls back
    /// to logging the type's members for diagnosis.
    /// </summary>
    private string ResolveLocalizedString(object obj, string context)
    {
        if (obj == null) return "?";

        var objType = obj.GetType();
        var typeName = objType.FullName ?? objType.Name;

        // If it's already a string, just return it
        if (obj is string s) return s;

        // Log type details on first encounter
        _log.Msg($"[diag] ResolveLocalizedString({context}): type = {typeName}");

        // Try common string properties: Value, Text, Line, m_Value, m_Text
        string[] propNames = { "Value", "Text", "Line", "m_Value", "m_Text", "LocalizedValue", "English" };
        foreach (var pName in propNames)
        {
            var prop = objType.GetProperty(pName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(string))
            {
                try
                {
                    var val = prop.GetValue(obj);
                    if (val is string str && !string.IsNullOrEmpty(str))
                    {
                        _log.Msg($"[diag] Resolved via property {pName}: '{str}'");
                        return str;
                    }
                }
                catch { }
            }
        }

        // Try common string methods: GetValue(), Resolve(), GetLocalizedString(), GetText()
        string[] methodNames = { "GetValue", "Resolve", "GetLocalizedString", "GetText", "Get" };
        foreach (var mName in methodNames)
        {
            var method = objType.GetMethod(mName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(string))
            {
                try
                {
                    var val = method.Invoke(obj, null);
                    if (val is string str && !string.IsNullOrEmpty(str))
                    {
                        _log.Msg($"[diag] Resolved via method {mName}(): '{str}'");
                        return str;
                    }
                }
                catch { }
            }
        }

        // Log all properties and methods for diagnosis
        var props = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => $"{p.Name}:{p.PropertyType.Name}")
            .ToList();
        _log.Msg($"[diag] LocalizedLine properties: {string.Join(", ", props)}");

        var methods = objType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.GetParameters().Length == 0)
            .Select(m => $"{m.Name}():{m.ReturnType.Name}")
            .ToList();
        _log.Msg($"[diag] LocalizedLine methods (no-arg): {string.Join(", ", methods)}");

        // Try reading all string-typed properties as last resort
        foreach (var prop in objType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.PropertyType == typeof(string) && prop.GetIndexParameters().Length == 0)
            {
                try
                {
                    var val = prop.GetValue(obj);
                    if (val is string str && !string.IsNullOrEmpty(str))
                    {
                        _log.Msg($"[diag] Resolved via fallback property {prop.Name}: '{str}'");
                        return str;
                    }
                }
                catch { }
            }
        }

        return typeName; // Return type name as fallback so user sees something
    }
}
