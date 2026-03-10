using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK APIs for creating and modifying strategic layer content at runtime.
///
/// NOTE: Runtime content creation has limitations - the game's systems expect
/// content to come from templates loaded at startup. These APIs work within
/// those constraints by leveraging existing game methods.
///
/// Strategic Content Types:
/// - Missions: Added to operations via OperationsManager
/// - Dilemmas: Strategic conversations, managed via ConversationManager
/// - Operations: Started via OperationsManager
/// - Events: Triggered via game's event system
/// </summary>
public static class StrategicContent
{
    // Cached types
    private static GameType _operationsManagerType;
    private static GameType _operationType;
    private static GameType _missionType;
    private static GameType _missionTemplateType;
    private static GameType _conversationManagerType;
    private static GameType _conversationTemplateType;
    private static GameType _strategyStateType;
    private static GameType _eventManagerType;

    // ═══════════════════════════════════════════════════════════════════
    //  Mission Injection
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Add a mission to an operation using a mission template.
    /// </summary>
    /// <param name="operation">Operation to add mission to.</param>
    /// <param name="missionTemplate">Mission template to use.</param>
    /// <returns>True if mission was added.</returns>
    public static bool AddMissionToOperation(GameObj operation, GameObj missionTemplate)
    {
        if (operation.IsNull || missionTemplate.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var opType = _operationType?.ManagedType;
            if (opType == null) return false;

            var opProxy = GetManagedProxy(operation, opType);
            if (opProxy == null) return false;

            var templateType = _missionTemplateType?.ManagedType;
            if (templateType == null) return false;

            var templateProxy = GetManagedProxy(missionTemplate, templateType);
            if (templateProxy == null) return false;

            // Try AddMission method
            var addMethod = opType.GetMethod("AddMission",
                BindingFlags.Public | BindingFlags.Instance);
            if (addMethod != null)
            {
                addMethod.Invoke(opProxy, new[] { templateProxy });
                SdkLogger.Msg($"[StrategicContent] Added mission to operation");
                return true;
            }

            // Fallback: Try to access missions list directly
            var missionsField = opType.GetField("m_Missions",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (missionsField != null)
            {
                var missions = missionsField.GetValue(opProxy);
                if (missions != null)
                {
                    // Create mission instance from template
                    var missionInstance = CreateMissionFromTemplate(missionTemplate);
                    if (!missionInstance.IsNull)
                    {
                        var missionProxy = GetManagedProxy(missionInstance, _missionType?.ManagedType);
                        if (missionProxy != null)
                        {
                            var addMethod2 = missions.GetType().GetMethod("Add");
                            addMethod2?.Invoke(missions, new[] { missionProxy });
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategicContent.AddMissionToOperation", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Create a mission instance from a template.
    /// </summary>
    public static GameObj CreateMissionFromTemplate(GameObj missionTemplate)
    {
        if (missionTemplate.IsNull) return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            var missionType = _missionType?.ManagedType;
            var templateType = _missionTemplateType?.ManagedType;
            if (missionType == null || templateType == null) return GameObj.Null;

            var templateProxy = GetManagedProxy(missionTemplate, templateType);
            if (templateProxy == null) return GameObj.Null;

            // Try CreateMission static method
            var createMethod = missionType.GetMethod("Create",
                BindingFlags.Public | BindingFlags.Static);
            if (createMethod != null)
            {
                var mission = createMethod.Invoke(null, new[] { templateProxy });
                if (mission != null)
                    return new GameObj(((Il2CppObjectBase)mission).Pointer);
            }

            // Try constructor
            var ctor = missionType.GetConstructor(new[] { templateType });
            if (ctor != null)
            {
                var mission = ctor.Invoke(new[] { templateProxy });
                if (mission != null)
                    return new GameObj(((Il2CppObjectBase)mission).Pointer);
            }

            return GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategicContent.CreateMissionFromTemplate", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Find a mission template by name.
    /// </summary>
    public static GameObj FindMissionTemplate(string name)
    {
        if (string.IsNullOrEmpty(name)) return GameObj.Null;
        return GameQuery.FindByName("GenericMissionTemplate", name);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Operation Management
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Start an operation from a template.
    /// </summary>
    /// <param name="operationTemplate">Operation template to start.</param>
    /// <returns>The started operation, or GameObj.Null on failure.</returns>
    public static GameObj StartOperation(GameObj operationTemplate)
    {
        if (operationTemplate.IsNull) return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            var om = Operation.GetOperationsManager();
            if (om.IsNull) return GameObj.Null;

            var omType = _operationsManagerType?.ManagedType;
            if (omType == null) return GameObj.Null;

            var omProxy = GetManagedProxy(om, omType);
            if (omProxy == null) return GameObj.Null;

            // Find StartOperation method
            var startMethod = omType.GetMethod("StartOperation",
                BindingFlags.Public | BindingFlags.Instance);
            if (startMethod == null) return GameObj.Null;

            var templateType = startMethod.GetParameters()[0].ParameterType;
            var templateProxy = GetManagedProxy(operationTemplate, templateType);
            if (templateProxy == null) return GameObj.Null;

            var result = startMethod.Invoke(omProxy, new[] { templateProxy });
            if (result != null)
            {
                SdkLogger.Msg($"[StrategicContent] Started operation: {operationTemplate.GetName()}");
                return new GameObj(((Il2CppObjectBase)result).Pointer);
            }

            return GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategicContent.StartOperation", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Find an operation template by name.
    /// </summary>
    public static GameObj FindOperationTemplate(string name)
    {
        if (string.IsNullOrEmpty(name)) return GameObj.Null;
        return GameQuery.FindByName("OperationTemplate", name);
    }

    /// <summary>
    /// End the current operation with a result.
    /// </summary>
    /// <param name="success">Whether operation was successful.</param>
    public static bool EndCurrentOperation(bool success)
    {
        try
        {
            EnsureTypesLoaded();

            var om = Operation.GetOperationsManager();
            if (om.IsNull) return false;

            var omType = _operationsManagerType?.ManagedType;
            if (omType == null) return false;

            var omProxy = GetManagedProxy(om, omType);
            if (omProxy == null) return false;

            var endMethod = omType.GetMethod("EndCurrentOperation",
                BindingFlags.Public | BindingFlags.Instance);
            if (endMethod == null) return false;

            endMethod.Invoke(omProxy, new object[] { success });
            SdkLogger.Msg($"[StrategicContent] Ended operation: success={success}");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategicContent.EndCurrentOperation", "Failed", ex);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Strategic Dilemmas (Conversations)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Trigger a strategic dilemma (conversation) by template name.
    /// Strategic dilemmas are ConversationTemplates with strategic triggers.
    /// </summary>
    public static bool TriggerDilemma(string templateName)
    {
        return Conversation.TriggerConversation(templateName);
    }

    /// <summary>
    /// Find dilemma templates that can be triggered at specific strategy moments.
    /// </summary>
    public static List<Conversation.ConversationInfo> GetStrategicDilemmas()
    {
        var all = Conversation.GetAllConversationTemplates();
        var dilemmas = new List<Conversation.ConversationInfo>();

        foreach (var c in all)
        {
            // Strategic dilemmas typically have type > 0 or specific triggers
            // This is heuristic - actual filtering depends on game content
            if (c.TemplateName?.Contains("Dilemma", StringComparison.OrdinalIgnoreCase) == true ||
                c.TemplateName?.Contains("Strategic", StringComparison.OrdinalIgnoreCase) == true ||
                c.TemplateName?.Contains("Event", StringComparison.OrdinalIgnoreCase) == true)
            {
                dilemmas.Add(c);
            }
        }

        return dilemmas;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Strategic Events
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fire a strategic event by name.
    /// </summary>
    public static bool FireStrategicEvent(string eventName)
    {
        if (string.IsNullOrEmpty(eventName)) return false;

        try
        {
            EnsureTypesLoaded();

            var emType = _eventManagerType?.ManagedType;
            if (emType == null)
            {
                SdkLogger.Warning("[StrategicContent] EventManager type not found");
                return false;
            }

            // Try to get EventManager instance
            var getMethod = emType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var em = getMethod?.Invoke(null, null);
            if (em == null)
            {
                // Try from StrategyState
                var ssType = _strategyStateType?.ManagedType;
                if (ssType != null)
                {
                    var ssGetMethod = ssType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
                    var ss = ssGetMethod?.Invoke(null, null);
                    if (ss != null)
                    {
                        var emProp = ssType.GetProperty("EventManager",
                            BindingFlags.Public | BindingFlags.Instance);
                        em = emProp?.GetValue(ss);
                    }
                }
            }

            if (em == null)
            {
                SdkLogger.Warning("[StrategicContent] Could not get EventManager");
                return false;
            }

            // Fire event
            var fireMethod = emType.GetMethod("FireEvent",
                BindingFlags.Public | BindingFlags.Instance);
            if (fireMethod != null)
            {
                fireMethod.Invoke(em, new object[] { eventName });
                SdkLogger.Msg($"[StrategicContent] Fired event: {eventName}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategicContent.FireStrategicEvent", "Failed", ex);
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Biome and Planet Queries
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all planet templates.
    /// </summary>
    public static List<PlanetInfo> GetAllPlanets()
    {
        var result = new List<PlanetInfo>();

        try
        {
            var planets = GameQuery.FindAll("PlanetTemplate");
            foreach (var p in planets)
            {
                var info = GetPlanetInfo(p);
                if (info != null)
                    result.Add(info);
            }
            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategicContent.GetAllPlanets", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get info about a planet template.
    /// </summary>
    public static PlanetInfo GetPlanetInfo(GameObj planetTemplate)
    {
        if (planetTemplate.IsNull) return null;

        try
        {
            var info = new PlanetInfo
            {
                Pointer = planetTemplate.Pointer,
                TemplateName = planetTemplate.GetName()
            };

            // Get localized name
            var planetType = GameType.Find("Menace.Strategy.PlanetTemplate")?.ManagedType;
            if (planetType != null)
            {
                var proxy = GetManagedProxy(planetTemplate, planetType);
                if (proxy != null)
                {
                    var getNameMethod = planetType.GetMethod("GetName",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (getNameMethod != null)
                        info.DisplayName = Il2CppUtils.ToManagedString(getNameMethod.Invoke(proxy, null));

                    // Get biomes
                    var biomesProp = planetType.GetProperty("Biomes",
                        BindingFlags.Public | BindingFlags.Instance);
                    var biomes = biomesProp?.GetValue(proxy);
                    if (biomes != null)
                    {
                        var lengthProp = biomes.GetType().GetProperty("Length") ??
                                       biomes.GetType().GetProperty("Count");
                        info.BiomeCount = (int)(lengthProp?.GetValue(biomes) ?? 0);
                    }
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategicContent.GetPlanetInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Find a planet template by name.
    /// </summary>
    public static GameObj FindPlanet(string name)
    {
        if (string.IsNullOrEmpty(name)) return GameObj.Null;
        return GameQuery.FindByName("PlanetTemplate", name);
    }

    /// <summary>
    /// Get all biome templates.
    /// </summary>
    public static List<BiomeInfo> GetAllBiomes()
    {
        var result = new List<BiomeInfo>();

        try
        {
            var biomes = GameQuery.FindAll("BiomeTemplate");
            foreach (var b in biomes)
            {
                var info = GetBiomeInfo(b);
                if (info != null)
                    result.Add(info);
            }
            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategicContent.GetAllBiomes", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get info about a biome template.
    /// </summary>
    public static BiomeInfo GetBiomeInfo(GameObj biomeTemplate)
    {
        if (biomeTemplate.IsNull) return null;

        try
        {
            var info = new BiomeInfo
            {
                Pointer = biomeTemplate.Pointer,
                TemplateName = biomeTemplate.GetName()
            };

            // Get localized name
            var biomeType = GameType.Find("Menace.Tactical.BiomeTemplate")?.ManagedType;
            if (biomeType != null)
            {
                var proxy = GetManagedProxy(biomeTemplate, biomeType);
                if (proxy != null)
                {
                    var getNameMethod = biomeType.GetMethod("GetName",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (getNameMethod != null)
                        info.DisplayName = Il2CppUtils.ToManagedString(getNameMethod.Invoke(proxy, null));
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategicContent.GetBiomeInfo", "Failed", ex);
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Info Classes
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Planet information.
    /// </summary>
    public class PlanetInfo
    {
        public string TemplateName { get; set; }
        public string DisplayName { get; set; }
        public int BiomeCount { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Biome information.
    /// </summary>
    public class BiomeInfo
    {
        public string TemplateName { get; set; }
        public string DisplayName { get; set; }
        public IntPtr Pointer { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Console Commands
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Register console commands for StrategicContent SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // planets - List all planets
        DevConsole.RegisterCommand("planets", "", "List all planet templates", args =>
        {
            var planets = GetAllPlanets();
            if (planets.Count == 0)
                return "No planets found";

            var lines = new List<string> { $"Planets ({planets.Count}):" };
            foreach (var p in planets)
            {
                var name = !string.IsNullOrEmpty(p.DisplayName) ? p.DisplayName : p.TemplateName;
                lines.Add($"  {name} ({p.BiomeCount} biomes)");
            }
            return string.Join("\n", lines);
        });

        // biomes - List all biomes
        DevConsole.RegisterCommand("biomes", "", "List all biome templates", args =>
        {
            var biomes = GetAllBiomes();
            if (biomes.Count == 0)
                return "No biomes found";

            var lines = new List<string> { $"Biomes ({biomes.Count}):" };
            foreach (var b in biomes)
            {
                var name = !string.IsNullOrEmpty(b.DisplayName) ? b.DisplayName : b.TemplateName;
                lines.Add($"  {name}");
            }
            return string.Join("\n", lines);
        });

        // dilemmas - List strategic dilemmas
        DevConsole.RegisterCommand("dilemmas", "", "List strategic dilemma conversations", args =>
        {
            var dilemmas = GetStrategicDilemmas();
            if (dilemmas.Count == 0)
                return "No dilemmas found";

            var lines = new List<string> { $"Strategic Dilemmas ({dilemmas.Count}):" };
            foreach (var d in dilemmas)
            {
                var once = d.IsOnlyOnce ? " [once]" : "";
                lines.Add($"  {d.TemplateName}{once}");
            }
            return string.Join("\n", lines);
        });

        // triggerdilemma <name> - Trigger a strategic dilemma
        DevConsole.RegisterCommand("triggerdilemma", "<name>", "Trigger a strategic dilemma", args =>
        {
            if (args.Length == 0)
                return "Usage: triggerdilemma <name>";

            var name = string.Join(" ", args);
            return TriggerDilemma(name)
                ? $"Triggered dilemma: {name}"
                : $"Failed to trigger dilemma: {name}";
        });

        // startoperation <name> - Start an operation
        DevConsole.RegisterCommand("startoperation", "<name>", "Start an operation from template", args =>
        {
            if (args.Length == 0)
                return "Usage: startoperation <template_name>";

            var name = string.Join(" ", args);
            var template = FindOperationTemplate(name);
            if (template.IsNull)
                return $"Operation template '{name}' not found";

            var op = StartOperation(template);
            return !op.IsNull
                ? $"Started operation: {name}"
                : $"Failed to start operation: {name}";
        });

        // endoperation <success|fail> - End current operation
        DevConsole.RegisterCommand("endoperation", "<success|fail>", "End the current operation", args =>
        {
            if (args.Length == 0)
                return "Usage: endoperation <success|fail>";

            var success = args[0].ToLowerInvariant() switch
            {
                "success" or "true" or "1" or "win" => true,
                "fail" or "false" or "0" or "lose" => false,
                _ => (bool?)null
            };

            if (!success.HasValue)
                return "Argument must be 'success' or 'fail'";

            return EndCurrentOperation(success.Value)
                ? $"Operation ended with {(success.Value ? "success" : "failure")}"
                : "Failed to end operation";
        });

        // missiontemplates - List mission templates
        DevConsole.RegisterCommand("missiontemplates", "[filter]", "List mission templates", args =>
        {
            var templates = GameQuery.FindAll("GenericMissionTemplate");
            if (templates.Length == 0)
                return "No mission templates found";

            var filter = args.Length > 0 ? args[0] : null;
            var lines = new List<string>();

            int count = 0;
            foreach (var t in templates)
            {
                var name = t.GetName() ?? "Unknown";
                if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                lines.Add($"  {name}");
                count++;
                if (count >= 50)
                {
                    lines.Add("  ... (truncated)");
                    break;
                }
            }

            lines.Insert(0, $"Mission Templates ({count}{(count >= 50 ? "+" : "")}):");
            return string.Join("\n", lines);
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Internal Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static void EnsureTypesLoaded()
    {
        _operationsManagerType ??= GameType.Find("Menace.Strategy.OperationsManager");
        _operationType ??= GameType.Find("Menace.Strategy.Operation");
        _missionType ??= GameType.Find("Menace.Strategy.Mission");
        _missionTemplateType ??= GameType.Find("Menace.Strategy.GenericMissionTemplate");
        _conversationManagerType ??= GameType.Find("Menace.Conversations.BaseConversationManager");
        _conversationTemplateType ??= GameType.Find("Menace.Conversations.ConversationTemplate");
        _strategyStateType ??= GameType.Find("Menace.States.StrategyState");
        _eventManagerType ??= GameType.Find("Menace.Strategy.EventManager");
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
        => Il2CppUtils.GetManagedProxy(obj, managedType);
}
