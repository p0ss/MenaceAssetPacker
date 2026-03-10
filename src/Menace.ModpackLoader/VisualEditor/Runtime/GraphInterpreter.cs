#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Menace.ModpackLoader.VisualEditor.Models;
using Menace.SDK;
using Menace.SDK.Entities;

namespace Menace.ModpackLoader.VisualEditor.Runtime;

/// <summary>
/// Runtime interpreter for visual mod graphs.
///
/// Loads ModGraphFile from JSON and registers graphs with the appropriate
/// game hook points. When hooks fire, executes the graph by traversing
/// from event nodes through conditions to actions.
///
/// Usage:
///   var interpreter = GraphInterpreter.Instance;
///   interpreter.LoadModGraph("/path/to/mod.modgraph.json");
///
///   // Graphs auto-register with TacticalEventHooks
///   // When skill_used fires, graphs with that hookPoint execute
///
/// Supported node types:
///   - Event: skill_used, damage_received, actor_killed, round_start, round_end, turn_end
///   - Condition: property_check (IsAttack, IsSilent, etc.)
///   - Action: add_effect, damage, heal, log
///   - Logic: and, or, not
///   - Value: constant
/// </summary>
public sealed class GraphInterpreter
{
    private static GraphInterpreter _instance;
    private static readonly object _lock = new();

    private readonly List<LoadedMod> _loadedMods = new();
    private bool _hooksRegistered;

    /// <summary>
    /// Gets the singleton instance of the graph interpreter.
    /// </summary>
    public static GraphInterpreter Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new GraphInterpreter();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// All loaded mods.
    /// </summary>
    public IReadOnlyList<LoadedMod> LoadedMods => _loadedMods;

    /// <summary>
    /// Whether hooks have been registered with TacticalEventHooks.
    /// </summary>
    public bool HooksRegistered => _hooksRegistered;

    private GraphInterpreter() { }

    /// <summary>
    /// Register console commands for visual mod management.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("visualmods", "", "List loaded visual mods", args =>
        {
            var mods = Instance.LoadedMods;
            if (mods.Count == 0)
                return "No visual mods loaded";

            var lines = new List<string> { $"Loaded visual mods: {mods.Count}" };
            foreach (var mod in mods)
            {
                var meta = mod.ModFile.Metadata;
                lines.Add($"  {meta.Name} v{meta.Version} by {meta.Author}");
                lines.Add($"    ID: {meta.Id}");
                lines.Add($"    Graphs: {mod.CompiledGraphs.Count}");
                foreach (var graph in mod.CompiledGraphs)
                {
                    lines.Add($"      - {graph.Graph.Name} @ {graph.Graph.HookPoint}");
                }
            }
            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("loadvisualmod", "<path>", "Load a visual mod from file", args =>
        {
            if (args.Length == 0)
                return "Usage: loadvisualmod <path>";

            var path = string.Join(" ", args);
            if (Instance.LoadModGraph(path))
                return $"Loaded visual mod from: {path}";
            else
                return $"Failed to load visual mod from: {path}";
        });

        DevConsole.RegisterCommand("unloadvisualmod", "<modId>", "Unload a visual mod by ID", args =>
        {
            if (args.Length == 0)
                return "Usage: unloadvisualmod <modId>";

            var modId = args[0];
            if (Instance.UnloadMod(modId))
                return $"Unloaded visual mod: {modId}";
            else
                return $"Visual mod not found: {modId}";
        });
    }

    /// <summary>
    /// Initialize the interpreter and register with game hooks.
    /// Call after TacticalEventHooks.Initialize().
    /// </summary>
    public void Initialize()
    {
        if (_hooksRegistered) return;

        // Register with tactical event hooks
        TacticalEventHooks.OnSkillUsed += OnSkillUsed;
        TacticalEventHooks.OnDamageReceived += OnDamageReceived;
        TacticalEventHooks.OnActorKilled += OnActorKilled;
        TacticalEventHooks.OnRoundStart += OnRoundStart;
        TacticalEventHooks.OnRoundEnd += OnRoundEnd;
        TacticalEventHooks.OnTurnEnd += OnTurnEnd;
        TacticalEventHooks.OnMovementStarted += OnMovementStarted;
        TacticalEventHooks.OnMovementFinished += OnMovementFinished;

        _hooksRegistered = true;
        SdkLogger.Msg("[GraphInterpreter] Initialized and registered with event hooks");
    }

    /// <summary>
    /// Load a mod graph from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the .modgraph.json file</param>
    /// <returns>True if loaded successfully</returns>
    public bool LoadModGraph(string filePath)
    {
        if (!File.Exists(filePath))
        {
            SdkLogger.Warning($"[GraphInterpreter] File not found: {filePath}");
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return LoadModGraphFromJson(json, filePath);
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[GraphInterpreter] Failed to read file {filePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load a mod graph from a JSON string.
    /// </summary>
    /// <param name="json">JSON content</param>
    /// <param name="source">Source identifier for logging</param>
    /// <returns>True if loaded successfully</returns>
    public bool LoadModGraphFromJson(string json, string source = "<json>")
    {
        try
        {
            var modFile = JsonSerializer.Deserialize<ModGraphFile>(json, GraphJsonOptions.Default);
            if (modFile == null)
            {
                SdkLogger.Warning($"[GraphInterpreter] Failed to deserialize: {source}");
                return false;
            }

            return LoadModGraphFile(modFile, source);
        }
        catch (JsonException ex)
        {
            SdkLogger.Error($"[GraphInterpreter] JSON parse error in {source}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[GraphInterpreter] Failed to load {source}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load a mod graph file object directly.
    /// </summary>
    /// <param name="modFile">The parsed mod file</param>
    /// <param name="source">Source identifier for logging</param>
    /// <returns>True if loaded successfully</returns>
    public bool LoadModGraphFile(ModGraphFile modFile, string source = "<memory>")
    {
        if (modFile == null)
            return false;

        // Validate format version
        if (modFile.FormatVersion > 1)
        {
            SdkLogger.Warning($"[GraphInterpreter] Unsupported format version {modFile.FormatVersion} in {source}");
            return false;
        }

        // Check for duplicate mod ID
        var existingMod = _loadedMods.FirstOrDefault(m => m.ModFile.Metadata?.Id == modFile.Metadata?.Id);
        if (existingMod != null)
        {
            SdkLogger.Warning($"[GraphInterpreter] Mod '{modFile.Metadata?.Id}' already loaded, replacing");
            _loadedMods.Remove(existingMod);
        }

        // Create compiled graphs
        var loadedMod = new LoadedMod
        {
            ModFile = modFile,
            Source = source,
            CompiledGraphs = new List<CompiledGraph>()
        };

        foreach (var graph in modFile.Graphs)
        {
            if (!graph.Enabled)
            {
                SdkLogger.Msg($"[GraphInterpreter] Skipping disabled graph: {graph.Name}");
                continue;
            }

            var compiled = CompileGraph(graph, modFile.Variables);
            if (compiled != null)
            {
                loadedMod.CompiledGraphs.Add(compiled);
                SdkLogger.Msg($"[GraphInterpreter] Compiled graph '{graph.Name}' -> {graph.HookPoint}");
            }
        }

        _loadedMods.Add(loadedMod);
        SdkLogger.Msg($"[GraphInterpreter] Loaded mod '{modFile.Metadata?.Name}' ({loadedMod.CompiledGraphs.Count} graphs)");
        return true;
    }

    /// <summary>
    /// Unload a mod by ID.
    /// </summary>
    /// <param name="modId">The mod's unique identifier</param>
    /// <returns>True if mod was found and unloaded</returns>
    public bool UnloadMod(string modId)
    {
        var mod = _loadedMods.FirstOrDefault(m => m.ModFile.Metadata?.Id == modId);
        if (mod == null)
            return false;

        _loadedMods.Remove(mod);
        SdkLogger.Msg($"[GraphInterpreter] Unloaded mod: {modId}");
        return true;
    }

    /// <summary>
    /// Unload all mods.
    /// </summary>
    public void UnloadAll()
    {
        _loadedMods.Clear();
        SdkLogger.Msg("[GraphInterpreter] All mods unloaded");
    }

    /// <summary>
    /// Load all .modgraph.json files from a directory.
    /// </summary>
    /// <param name="directory">Directory to scan</param>
    /// <param name="recursive">Whether to search subdirectories</param>
    /// <returns>Number of mods loaded</returns>
    public int LoadModsFromDirectory(string directory, bool recursive = true)
    {
        if (!Directory.Exists(directory))
        {
            SdkLogger.Warning($"[GraphInterpreter] Directory not found: {directory}");
            return 0;
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directory, "*.modgraph.json", searchOption);

        int loaded = 0;
        foreach (var file in files)
        {
            if (LoadModGraph(file))
                loaded++;
        }

        SdkLogger.Msg($"[GraphInterpreter] Loaded {loaded}/{files.Length} mods from {directory}");
        return loaded;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Graph Compilation
    // ═══════════════════════════════════════════════════════════════════

    private CompiledGraph CompileGraph(ModGraph graph, Dictionary<string, JsonElement> variables)
    {
        // Build node lookup
        var nodeMap = graph.Nodes.ToDictionary(n => n.Id, n => n);

        // Find the event node (entry point)
        var eventNode = graph.Nodes.FirstOrDefault(n => n.Type == NodeTypes.Event);
        if (eventNode == null)
        {
            SdkLogger.Warning($"[GraphInterpreter] Graph '{graph.Name}' has no event node");
            return null;
        }

        // Build connection lookup: target -> sources
        var incomingConnections = new Dictionary<string, List<NodeConnection>>();
        // Build connection lookup: source -> targets
        var outgoingConnections = new Dictionary<string, List<NodeConnection>>();

        foreach (var conn in graph.Connections)
        {
            // Incoming connections (by target node + port)
            var inKey = $"{conn.TargetNodeId}.{conn.TargetPort}";
            if (!incomingConnections.TryGetValue(inKey, out var inList))
            {
                inList = new List<NodeConnection>();
                incomingConnections[inKey] = inList;
            }
            inList.Add(conn);

            // Outgoing connections (by source node + port)
            var outKey = $"{conn.SourceNodeId}.{conn.SourcePort}";
            if (!outgoingConnections.TryGetValue(outKey, out var outList))
            {
                outList = new List<NodeConnection>();
                outgoingConnections[outKey] = outList;
            }
            outList.Add(conn);
        }

        return new CompiledGraph
        {
            Graph = graph,
            Variables = variables ?? new Dictionary<string, JsonElement>(),
            NodeMap = nodeMap,
            EventNode = eventNode,
            IncomingConnections = incomingConnections,
            OutgoingConnections = outgoingConnections
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Hook Handlers
    // ═══════════════════════════════════════════════════════════════════

    private void OnSkillUsed(IntPtr actorPtr, IntPtr skillPtr, IntPtr targetPtr)
    {
        var context = new ExecutionContext
        {
            HookPoint = HookPoints.SkillUsed,
            Actor = actorPtr != IntPtr.Zero ? Actor.Get(actorPtr) : null,
            Skill = skillPtr != IntPtr.Zero ? new Skill(skillPtr) : null,
            Target = targetPtr != IntPtr.Zero ? Actor.Get(targetPtr) : null
        };

        ExecuteGraphsForHook(HookPoints.SkillUsed, context);
    }

    private void OnDamageReceived(IntPtr targetPtr, IntPtr attackerPtr, IntPtr skillPtr)
    {
        var context = new ExecutionContext
        {
            HookPoint = HookPoints.DamageReceived,
            Actor = targetPtr != IntPtr.Zero ? Actor.Get(targetPtr) : null,
            Skill = skillPtr != IntPtr.Zero ? new Skill(skillPtr) : null,
            Target = attackerPtr != IntPtr.Zero ? Actor.Get(attackerPtr) : null  // Note: attacker stored in Target for damage events
        };

        ExecuteGraphsForHook(HookPoints.DamageReceived, context);
    }

    private void OnActorKilled(IntPtr actorPtr, IntPtr killerPtr, int factionId)
    {
        var context = new ExecutionContext
        {
            HookPoint = HookPoints.ActorKilled,
            Actor = actorPtr != IntPtr.Zero ? Actor.Get(actorPtr) : null,
            Target = killerPtr != IntPtr.Zero ? Actor.Get(killerPtr) : null,  // Killer stored in Target
            IntValue = factionId
        };

        ExecuteGraphsForHook(HookPoints.ActorKilled, context);
    }

    private void OnRoundStart(int roundNumber)
    {
        var context = new ExecutionContext
        {
            HookPoint = HookPoints.RoundStart,
            IntValue = roundNumber
        };

        ExecuteGraphsForHook(HookPoints.RoundStart, context);
    }

    private void OnRoundEnd(int roundNumber)
    {
        var context = new ExecutionContext
        {
            HookPoint = HookPoints.RoundEnd,
            IntValue = roundNumber
        };

        ExecuteGraphsForHook(HookPoints.RoundEnd, context);
    }

    private void OnTurnEnd(IntPtr actorPtr)
    {
        var context = new ExecutionContext
        {
            HookPoint = HookPoints.TurnEnd,
            Actor = actorPtr != IntPtr.Zero ? Actor.Get(actorPtr) : null
        };

        ExecuteGraphsForHook(HookPoints.TurnEnd, context);
    }

    private void OnMovementStarted(IntPtr actorPtr, IntPtr fromTilePtr, IntPtr toTilePtr)
    {
        var context = new ExecutionContext
        {
            HookPoint = HookPoints.MoveStart,
            Actor = actorPtr != IntPtr.Zero ? Actor.Get(actorPtr) : null
        };

        ExecuteGraphsForHook(HookPoints.MoveStart, context);
    }

    private void OnMovementFinished(IntPtr actorPtr, IntPtr tilePtr)
    {
        var context = new ExecutionContext
        {
            HookPoint = HookPoints.MoveComplete,
            Actor = actorPtr != IntPtr.Zero ? Actor.Get(actorPtr) : null
        };

        ExecuteGraphsForHook(HookPoints.MoveComplete, context);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Graph Execution
    // ═══════════════════════════════════════════════════════════════════

    private void ExecuteGraphsForHook(string hookPoint, ExecutionContext context)
    {
        // Collect all graphs for this hook point, sorted by priority
        var graphs = _loadedMods
            .SelectMany(m => m.CompiledGraphs)
            .Where(g => g.Graph.HookPoint == hookPoint && g.Graph.Enabled)
            .OrderBy(g => g.Graph.Priority)
            .ToList();

        foreach (var graph in graphs)
        {
            try
            {
                ExecuteGraph(graph, context);
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"[GraphInterpreter] Error executing graph '{graph.Graph.Name}': {ex.Message}");
            }
        }
    }

    private void ExecuteGraph(CompiledGraph graph, ExecutionContext context)
    {
        // Set up execution state
        var state = new ExecutionState
        {
            Context = context,
            Graph = graph,
            PortValues = new Dictionary<string, object>()
        };

        // Initialize event node output ports based on hook data
        InitializeEventNodeOutputs(graph.EventNode, state);

        // Find action nodes that need to be executed
        // We traverse from the event node through conditions to find reachable actions
        var actionNodes = graph.Graph.Nodes
            .Where(n => n.Type == NodeTypes.Action)
            .ToList();

        foreach (var actionNode in actionNodes)
        {
            // Check if this action node should execute
            if (ShouldExecuteAction(actionNode, state))
            {
                ExecuteActionNode(actionNode, state);
            }
        }
    }

    private void InitializeEventNodeOutputs(GraphNode eventNode, ExecutionState state)
    {
        var context = state.Context;

        // Set outputs based on event type
        switch (eventNode.Subtype)
        {
            case NodeSubtypes.SkillUsed:
                SetPortValue(state, eventNode.Id, "actor", context.Actor);
                SetPortValue(state, eventNode.Id, "skill", context.Skill);
                SetPortValue(state, eventNode.Id, "target", context.Target);
                break;

            case NodeSubtypes.DamageReceived:
                SetPortValue(state, eventNode.Id, "target", context.Actor);
                SetPortValue(state, eventNode.Id, "attacker", context.Target);
                SetPortValue(state, eventNode.Id, "skill", context.Skill);
                break;

            case NodeSubtypes.ActorKilled:
                SetPortValue(state, eventNode.Id, "actor", context.Actor);
                SetPortValue(state, eventNode.Id, "killer", context.Target);
                SetPortValue(state, eventNode.Id, "faction", context.IntValue);
                break;

            case NodeSubtypes.RoundStart:
            case NodeSubtypes.RoundEnd:
                SetPortValue(state, eventNode.Id, "round_number", context.IntValue);
                break;

            case NodeSubtypes.TurnEnd:
                SetPortValue(state, eventNode.Id, "actor", context.Actor);
                break;
        }
    }

    private bool ShouldExecuteAction(GraphNode actionNode, ExecutionState state)
    {
        // Check flow_in connection - must have a passing condition chain
        var flowIn = GetIncomingConnection(state.Graph, actionNode.Id, "flow_in");
        if (flowIn != null)
        {
            // Follow the flow back to check conditions
            if (!EvaluateFlowSource(flowIn.SourceNodeId, flowIn.SourcePort, state))
                return false;
        }

        return true;
    }

    private bool EvaluateFlowSource(string sourceNodeId, string sourcePort, ExecutionState state)
    {
        if (!state.Graph.NodeMap.TryGetValue(sourceNodeId, out var sourceNode))
            return false;

        switch (sourceNode.Type)
        {
            case NodeTypes.Event:
                // Event nodes always "pass" - they're the start
                return true;

            case NodeTypes.Condition:
                // Evaluate the condition
                var conditionResult = EvaluateCondition(sourceNode, state);
                // Check if the output port matches the result
                return (sourcePort == "pass" && conditionResult) ||
                       (sourcePort == "fail" && !conditionResult);

            case NodeTypes.Logic:
                return EvaluateLogicNode(sourceNode, state);

            default:
                return true;
        }
    }

    private bool EvaluateCondition(GraphNode conditionNode, ExecutionState state)
    {
        // Get the input value
        var inputValue = GetInputValue(conditionNode, "input", state);
        if (inputValue == null)
            return false;

        // Get condition config
        var property = conditionNode.GetConfigString("property");
        var op = conditionNode.GetConfigString("operator", "==");
        var expectedValue = GetConfigValue(conditionNode, "value");

        // Get the property value from the input
        var actualValue = GetPropertyValue(inputValue, property);

        // Compare
        return CompareValues(actualValue, expectedValue, op);
    }

    private bool EvaluateLogicNode(GraphNode logicNode, ExecutionState state)
    {
        switch (logicNode.Subtype)
        {
            case NodeSubtypes.And:
                var aValue = GetInputBool(logicNode, "a", state);
                var bValue = GetInputBool(logicNode, "b", state);
                return aValue && bValue;

            case NodeSubtypes.Or:
                aValue = GetInputBool(logicNode, "a", state);
                bValue = GetInputBool(logicNode, "b", state);
                return aValue || bValue;

            case NodeSubtypes.Not:
                var inputValue = GetInputBool(logicNode, "input", state);
                return !inputValue;

            default:
                return true;
        }
    }

    private void ExecuteActionNode(GraphNode actionNode, ExecutionState state)
    {
        switch (actionNode.Subtype)
        {
            case NodeSubtypes.AddEffect:
                ExecuteAddEffect(actionNode, state);
                break;

            case NodeSubtypes.Damage:
                ExecuteDamage(actionNode, state);
                break;

            case NodeSubtypes.Heal:
                ExecuteHeal(actionNode, state);
                break;

            case NodeSubtypes.Log:
                ExecuteLog(actionNode, state);
                break;

            case NodeSubtypes.SetFlag:
                ExecuteSetFlag(actionNode, state);
                break;

            default:
                SdkLogger.Warning($"[GraphInterpreter] Unknown action subtype: {actionNode.Subtype}");
                break;
        }
    }

    private void ExecuteAddEffect(GraphNode actionNode, ExecutionState state)
    {
        // Get the target actor from input
        var actor = GetInputValue(actionNode, "actor", state) as Actor;
        if (actor == null || !actor.IsValid)
        {
            SdkLogger.Warning($"[GraphInterpreter] add_effect: Invalid actor");
            return;
        }

        var property = actionNode.GetConfigString("property", "Concealment");
        var modifier = actionNode.GetConfigInt("modifier", 0);
        var duration = actionNode.GetConfigInt("duration", 1);

        // Duration 0 means instant/permanent (use 1 round minimum for effect system)
        if (duration <= 0)
            duration = 1;

        actor.AddEffect(property.ToLowerInvariant(), modifier, duration, state.Graph.Graph.Name);

        SdkLogger.Msg($"[GraphInterpreter] Added effect: {property} {modifier:+#;-#;0} for {duration} rounds to {actor.Name}");
    }

    private void ExecuteDamage(GraphNode actionNode, ExecutionState state)
    {
        var actor = GetInputValue(actionNode, "actor", state) as Actor;
        if (actor == null || !actor.IsValid)
        {
            SdkLogger.Warning($"[GraphInterpreter] damage: Invalid actor");
            return;
        }

        var amount = actionNode.GetConfigInt("amount", 0);
        if (amount <= 0)
            return;

        actor.ApplyDamage(amount);
        SdkLogger.Msg($"[GraphInterpreter] Applied {amount} damage to {actor.Name}");
    }

    private void ExecuteHeal(GraphNode actionNode, ExecutionState state)
    {
        var actor = GetInputValue(actionNode, "actor", state) as Actor;
        if (actor == null || !actor.IsValid)
        {
            SdkLogger.Warning($"[GraphInterpreter] heal: Invalid actor");
            return;
        }

        var amount = actionNode.GetConfigInt("amount", 0);
        if (amount <= 0)
            return;

        actor.Heal(amount);
        SdkLogger.Msg($"[GraphInterpreter] Healed {actor.Name} for {amount}");
    }

    private void ExecuteLog(GraphNode actionNode, ExecutionState state)
    {
        var message = actionNode.GetConfigString("message", "");
        if (string.IsNullOrEmpty(message))
            return;

        // Simple variable substitution
        message = message
            .Replace("{actor}", state.Context.Actor?.Name ?? "<null>")
            .Replace("{skill}", state.Context.Skill?.Name ?? "<null>")
            .Replace("{target}", state.Context.Target?.Name ?? "<null>");

        SdkLogger.Msg($"[Graph:{state.Graph.Graph.Name}] {message}");
    }

    private void ExecuteSetFlag(GraphNode actionNode, ExecutionState state)
    {
        var actor = GetInputValue(actionNode, "actor", state) as Actor;
        if (actor == null || !actor.IsValid)
        {
            SdkLogger.Warning($"[GraphInterpreter] set_flag: Invalid actor");
            return;
        }

        var flag = actionNode.GetConfigString("flag", "");
        var value = actionNode.GetConfigBool("value", true);

        // Flags would need EntityState support - log for now
        SdkLogger.Msg($"[GraphInterpreter] Would set flag '{flag}'={value} on {actor.Name}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Value Resolution
    // ═══════════════════════════════════════════════════════════════════

    private void SetPortValue(ExecutionState state, string nodeId, string portName, object value)
    {
        var key = $"{nodeId}.{portName}";
        state.PortValues[key] = value;
    }

    private object GetPortValue(ExecutionState state, string nodeId, string portName)
    {
        var key = $"{nodeId}.{portName}";
        return state.PortValues.TryGetValue(key, out var value) ? value : null;
    }

    private object GetInputValue(GraphNode node, string inputPort, ExecutionState state)
    {
        // Find incoming connection to this port
        var conn = GetIncomingConnection(state.Graph, node.Id, inputPort);
        if (conn == null)
            return null;

        // Get or compute the source value
        return ResolvePortValue(conn.SourceNodeId, conn.SourcePort, state);
    }

    private bool GetInputBool(GraphNode node, string inputPort, ExecutionState state)
    {
        var value = GetInputValue(node, inputPort, state);
        if (value == null)
            return false;

        if (value is bool b)
            return b;

        // Try to convert
        if (value is int i)
            return i != 0;

        return false;
    }

    private object ResolvePortValue(string nodeId, string portName, ExecutionState state)
    {
        // Check cache
        var cached = GetPortValue(state, nodeId, portName);
        if (cached != null)
            return cached;

        // Get the source node
        if (!state.Graph.NodeMap.TryGetValue(nodeId, out var node))
            return null;

        object value = null;

        switch (node.Type)
        {
            case NodeTypes.Event:
                // Event outputs are set during initialization
                value = GetPortValue(state, nodeId, portName);
                break;

            case NodeTypes.Condition:
                // Condition "pass" port - evaluate and return the input if true
                var condResult = EvaluateCondition(node, state);
                if (portName == "pass" && condResult)
                    value = GetInputValue(node, "input", state);
                else if (portName == "fail" && !condResult)
                    value = GetInputValue(node, "input", state);
                break;

            case NodeTypes.Value:
                value = ResolveValueNode(node, state);
                break;

            case NodeTypes.Logic:
                value = EvaluateLogicNode(node, state);
                break;
        }

        // Cache the result
        if (value != null)
            SetPortValue(state, nodeId, portName, value);

        return value;
    }

    private object ResolveValueNode(GraphNode node, ExecutionState state)
    {
        switch (node.Subtype)
        {
            case NodeSubtypes.Constant:
                return GetConfigValue(node, "value");

            case NodeSubtypes.Variable:
                var varId = node.GetConfigString("variableId");
                if (!string.IsNullOrEmpty(varId) && state.Graph.Variables.TryGetValue(varId, out var varElement))
                {
                    return JsonElementToObject(varElement);
                }
                return null;

            case NodeSubtypes.Random:
                var min = node.GetConfigInt("min", 0);
                var max = node.GetConfigInt("max", 100);
                return new Random().Next(min, max + 1);

            default:
                return null;
        }
    }

    private NodeConnection GetIncomingConnection(CompiledGraph graph, string nodeId, string portName)
    {
        var key = $"{nodeId}.{portName}";
        if (graph.IncomingConnections.TryGetValue(key, out var connections) && connections.Count > 0)
            return connections[0];
        return null;
    }

    private object GetConfigValue(GraphNode node, string key)
    {
        if (node.Config == null || !node.Config.TryGetValue(key, out var element))
            return null;

        return JsonElementToObject(element);
    }

    private static object JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            _ => null
        };
    }

    private object GetPropertyValue(object obj, string propertyName)
    {
        if (obj == null || string.IsNullOrEmpty(propertyName))
            return null;

        // Handle Skill properties
        if (obj is Skill skill)
        {
            return propertyName switch
            {
                "IsAttack" => skill.IsAttack,
                "IsSilent" => skill.IsSilent,
                "Name" => skill.Name,
                "TemplateName" => skill.TemplateName,
                _ => skill.GetTemplateProperty(propertyName)
            };
        }

        // Handle Actor properties
        if (obj is Actor actor)
        {
            return propertyName switch
            {
                "Name" => actor.Name,
                "FactionId" => actor.FactionId,
                "Faction" => actor.Faction,
                "FactionName" => actor.FactionName,
                "IsValid" => actor.IsValid,
                "ActionPoints" => actor.ActionPoints,
                "Morale" => actor.Morale,
                "Suppression" => actor.Suppression,
                _ => null
            };
        }

        // Fallback - try reflection (slow but flexible)
        try
        {
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop != null)
                return prop.GetValue(obj);
        }
        catch
        {
            // Ignore reflection errors
        }

        return null;
    }

    private static bool CompareValues(object actual, object expected, string op)
    {
        if (actual == null || expected == null)
            return op == "!=" ? actual != expected : false;

        // Handle boolean comparisons
        if (actual is bool actualBool)
        {
            bool expectedBool;
            if (expected is bool eb)
                expectedBool = eb;
            else if (expected is string es)
                expectedBool = es.Equals("true", StringComparison.OrdinalIgnoreCase);
            else
                expectedBool = Convert.ToBoolean(expected);

            return op switch
            {
                "==" => actualBool == expectedBool,
                "!=" => actualBool != expectedBool,
                _ => false
            };
        }

        // Handle numeric comparisons
        if (actual is int or double or float or long)
        {
            var actualDouble = Convert.ToDouble(actual);
            var expectedDouble = Convert.ToDouble(expected);

            return op switch
            {
                "==" => Math.Abs(actualDouble - expectedDouble) < 0.0001,
                "!=" => Math.Abs(actualDouble - expectedDouble) >= 0.0001,
                "<" => actualDouble < expectedDouble,
                ">" => actualDouble > expectedDouble,
                "<=" => actualDouble <= expectedDouble,
                ">=" => actualDouble >= expectedDouble,
                _ => false
            };
        }

        // Handle string comparisons
        var actualStr = actual.ToString();
        var expectedStr = expected.ToString();

        return op switch
        {
            "==" => actualStr.Equals(expectedStr, StringComparison.OrdinalIgnoreCase),
            "!=" => !actualStr.Equals(expectedStr, StringComparison.OrdinalIgnoreCase),
            "contains" => actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}

// ═══════════════════════════════════════════════════════════════════
//  Supporting Types
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// A loaded mod with its compiled graphs.
/// </summary>
public class LoadedMod
{
    public ModGraphFile ModFile { get; set; }
    public string Source { get; set; }
    public List<CompiledGraph> CompiledGraphs { get; set; } = new();
}

/// <summary>
/// A compiled graph ready for execution.
/// </summary>
public class CompiledGraph
{
    public ModGraph Graph { get; set; }
    public Dictionary<string, JsonElement> Variables { get; set; }
    public Dictionary<string, GraphNode> NodeMap { get; set; }
    public GraphNode EventNode { get; set; }
    public Dictionary<string, List<NodeConnection>> IncomingConnections { get; set; }
    public Dictionary<string, List<NodeConnection>> OutgoingConnections { get; set; }
}

/// <summary>
/// Context passed to graph execution from a hook.
/// </summary>
public class ExecutionContext
{
    public string HookPoint { get; set; }
    public Actor Actor { get; set; }
    public Skill Skill { get; set; }
    public Actor Target { get; set; }
    public int IntValue { get; set; }
    public float FloatValue { get; set; }
}

/// <summary>
/// Mutable state during graph execution.
/// </summary>
public class ExecutionState
{
    public ExecutionContext Context { get; set; }
    public CompiledGraph Graph { get; set; }
    public Dictionary<string, object> PortValues { get; set; }
}
