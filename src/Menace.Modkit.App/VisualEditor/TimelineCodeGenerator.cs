#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Generates C# code from a timeline graph.
/// Uses resolved types from type propagation to generate type-safe code.
/// </summary>
public class TimelineCodeGenerator
{
    private readonly TimelineGraph _graph;
    private readonly StringBuilder _sb = new();
    private int _indent;

    // Track variable names assigned to each node's outputs
    private readonly Dictionary<string, string> _portVariables = new();

    // Track template references that need to be declared
    private readonly Dictionary<string, (string templateType, string templateId)> _templateRefs = new();

    public TimelineCodeGenerator(TimelineGraph graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Generate C# code for the entire graph.
    /// </summary>
    public string Generate()
    {
        _sb.Clear();
        _indent = 0;
        _portVariables.Clear();
        _templateRefs.Clear();

        // Find all trigger nodes (entry points)
        var triggers = _graph.Nodes
            .Where(n => IsTriggerNode(n.NodeType))
            .OrderBy(n => n.X)
            .ThenBy(n => n.Y)
            .ToList();

        if (triggers.Count == 0)
        {
            return "// No trigger nodes found. Add a trigger node to generate code.";
        }

        // Collect template references used in the graph
        CollectTemplateReferences();

        AppendLine($"// Generated from: {_graph.Name}");
        AppendLine($"// Cycle: {_graph.Cycle}");
        AppendLine();
        AppendLine("using Menace.SDK;");
        AppendLine("using Menace.SDK.Entities;");
        AppendLine();
        AppendLine($"public static class {SanitizeName(_graph.Name)}Effect");
        AppendLine("{");
        _indent++;

        AppendLine("public static void Register()");
        AppendLine("{");
        _indent++;

        foreach (var trigger in triggers)
        {
            GenerateTrigger(trigger);
            AppendLine();
        }

        _indent--;
        AppendLine("}");

        _indent--;
        AppendLine("}");

        return _sb.ToString();
    }

    /// <summary>
    /// Collect all template reference nodes and their template IDs.
    /// </summary>
    private void CollectTemplateReferences()
    {
        foreach (var node in _graph.Nodes)
        {
            if (node.NodeType == TimelineNodeType.TemplateRefActor ||
                node.NodeType == TimelineNodeType.TemplateRefSkill ||
                node.NodeType == TimelineNodeType.TemplateRefItem)
            {
                var templateType = node.Properties.TryGetValue("templateType", out var tt) ? tt?.ToString() ?? "" : "";
                var templateId = node.Properties.TryGetValue("templateId", out var tid) ? tid?.ToString() ?? "" : "";

                if (!string.IsNullOrEmpty(templateId))
                {
                    _templateRefs[node.Id] = (templateType, templateId);
                }
            }
        }
    }

    private void GenerateTrigger(TimelineNode trigger)
    {
        var hookName = GetHookName(trigger.NodeType);
        var parameters = GetTriggerParameters(trigger.NodeType);

        AppendLine($"TacticalEventHooks.{hookName} += ({parameters}) =>");
        AppendLine("{");
        _indent++;

        // Generate variable declarations from trigger outputs and register them
        GenerateTriggerOutputs(trigger);

        // Process nodes in topological order to handle data flow correctly
        var processedNodes = new HashSet<string> { trigger.Id };
        ProcessConnectedNodes(trigger, processedNodes);

        _indent--;
        AppendLine("};");
    }

    private void GenerateTriggerOutputs(TimelineNode trigger)
    {
        switch (trigger.NodeType)
        {
            case TimelineNodeType.TriggerSkillUsed:
                AppendLine("var actor = Actor.Get(userPtr);");
                AppendLine("var skill = new Skill(skillPtr);");
                AppendLine("var target = targetPtr != IntPtr.Zero ? Actor.Get(targetPtr) : null;");
                RegisterTriggerVariables(trigger, ("actor", "actor"), ("skill", "skill"), ("target", "target"));
                break;

            case TimelineNodeType.TriggerDamageReceived:
                // Note: SDK doesn't provide damage amount in this event, only the skill used
                AppendLine("var target = Actor.Get(targetPtr);");
                AppendLine("var attacker = attackerPtr != IntPtr.Zero ? Actor.Get(attackerPtr) : null;");
                AppendLine("var skill = skillPtr != IntPtr.Zero ? new Skill(skillPtr) : null;");
                RegisterTriggerVariables(trigger, ("target", "target"), ("attacker", "attacker"), ("skill", "skill"));
                break;

            case TimelineNodeType.TriggerActorKilled:
                AppendLine("var actor = Actor.Get(actorPtr);");
                AppendLine("var killer = killerPtr != IntPtr.Zero ? Actor.Get(killerPtr) : null;");
                AppendLine("var faction = factionId;");
                RegisterTriggerVariables(trigger, ("actor", "actor"), ("killer", "killer"));
                break;

            case TimelineNodeType.TriggerMovementStarted:
                AppendLine("var actor = Actor.Get(actorPtr);");
                AppendLine("var fromTile = new Tile(fromTilePtr);");
                AppendLine("var toTile = new Tile(toTilePtr);");
                RegisterTriggerVariables(trigger, ("actor", "actor"), ("from", "fromTile"), ("to", "toTile"));
                break;

            case TimelineNodeType.TriggerMovementFinished:
                AppendLine("var actor = Actor.Get(actorPtr);");
                AppendLine("var tile = new Tile(tilePtr);");
                RegisterTriggerVariables(trigger, ("actor", "actor"), ("tile", "tile"));
                break;

            case TimelineNodeType.TriggerTurnStart:
            case TimelineNodeType.TriggerTurnEnd:
                AppendLine("var actor = Actor.Get(actorPtr);");
                RegisterTriggerVariables(trigger, ("actor", "actor"));
                break;

            case TimelineNodeType.TriggerRoundStart:
            case TimelineNodeType.TriggerRoundEnd:
                AppendLine("var round = roundNumber;");
                RegisterTriggerVariables(trigger, ("round", "round"));
                break;

            case TimelineNodeType.TriggerHitpointsChanged:
                AppendLine("var actor = Actor.Get(actorPtr);");
                AppendLine("var oldHitpoints = oldHp;");
                AppendLine("var newHitpoints = newHp;");
                AppendLine("var delta = newHp - oldHp;");
                RegisterTriggerVariables(trigger, ("actor", "actor"), ("oldHp", "oldHitpoints"), ("newHp", "newHitpoints"), ("delta", "delta"));
                break;

            case TimelineNodeType.TriggerSuppressed:
                AppendLine("var actor = Actor.Get(actorPtr);");
                RegisterTriggerVariables(trigger, ("actor", "actor"));
                break;
        }

        AppendLine();
    }

    /// <summary>
    /// Register variable names for trigger output ports.
    /// </summary>
    private void RegisterTriggerVariables(TimelineNode trigger, params (string portName, string varName)[] mappings)
    {
        foreach (var (portName, varName) in mappings)
        {
            var port = trigger.Outputs.FirstOrDefault(p => p.Name == portName);
            if (port != null)
            {
                _portVariables[$"{trigger.Id}:{portName}"] = varName;
            }
        }
    }

    /// <summary>
    /// Process nodes connected to a source node in dependency order.
    /// </summary>
    private void ProcessConnectedNodes(TimelineNode sourceNode, HashSet<string> processedNodes)
    {
        // Get all nodes that are directly or indirectly connected from this node
        var toProcess = new List<TimelineNode>();

        foreach (var output in sourceNode.Outputs)
        {
            var connections = _graph.Connections.Where(c => c.SourcePort == output);
            foreach (var conn in connections)
            {
                if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
                {
                    if (!toProcess.Contains(conn.TargetNode))
                    {
                        toProcess.Add(conn.TargetNode);
                    }
                }
            }
        }

        // Sort by Y position to maintain visual order
        toProcess = toProcess.OrderBy(n => n.Y).ToList();

        foreach (var node in toProcess)
        {
            if (!processedNodes.Contains(node.Id))
            {
                GenerateNode(node, processedNodes);
            }
        }
    }

    private void GenerateNode(TimelineNode node, HashSet<string> processedNodes)
    {
        if (processedNodes.Contains(node.Id))
            return;

        processedNodes.Add(node.Id);

        switch (node.NodeType)
        {
            // Data nodes that extract properties
            case TimelineNodeType.Actor:
            case TimelineNodeType.Skill:
            case TimelineNodeType.Tile:
                GenerateDataNode(node);
                break;

            // Template reference nodes
            case TimelineNodeType.TemplateRefActor:
            case TimelineNodeType.TemplateRefSkill:
            case TimelineNodeType.TemplateRefItem:
                GenerateTemplateRef(node);
                break;

            case TimelineNodeType.Condition:
                GenerateCondition(node, processedNodes);
                return; // Condition handles its own child processing

            case TimelineNodeType.Compare:
                GenerateCompare(node);
                break;

            case TimelineNodeType.MathOp:
                GenerateMathOp(node);
                break;

            case TimelineNodeType.Not:
                GenerateNot(node);
                break;

            case TimelineNodeType.And:
            case TimelineNodeType.Or:
                GenerateLogicOp(node);
                break;

            case TimelineNodeType.Effect:
                GenerateEffect(node);
                break;

            case TimelineNodeType.ActionApplyDamage:
                GenerateDamage(node);
                break;

            case TimelineNodeType.ActionHeal:
                GenerateHeal(node);
                break;

            case TimelineNodeType.ActionKill:
                GenerateKill(node);
                break;

            case TimelineNodeType.ActionLog:
                GenerateLog(node);
                break;

            case TimelineNodeType.ForEachActor:
                GenerateForEachActor(node, processedNodes);
                return; // Loop handles its own child processing

            case TimelineNodeType.SetVariable:
                GenerateSetVariable(node);
                break;

            case TimelineNodeType.GetVariable:
                // Get variables are resolved inline when referenced
                break;

            // Timing/Coroutines
            case TimelineNodeType.Delay:
                GenerateDelay(node, processedNodes);
                return; // Handles its own child processing

            case TimelineNodeType.Repeat:
                GenerateRepeat(node, processedNodes);
                return;

            case TimelineNodeType.Once:
                GenerateOnce(node, processedNodes);
                return;

            // State Machine
            case TimelineNodeType.State:
                GenerateState(node, processedNodes);
                return;

            case TimelineNodeType.Transition:
                GenerateTransition(node);
                break;

            case TimelineNodeType.GetState:
                GenerateGetState(node);
                break;

            case TimelineNodeType.SetState:
                GenerateSetState(node);
                break;
        }

        // Process connected nodes
        ProcessConnectedNodes(node, processedNodes);
    }

    /// <summary>
    /// Generate code for data extraction nodes (Actor, Skill, Tile).
    /// These nodes pass through their input and expose properties.
    /// </summary>
    private void GenerateDataNode(TimelineNode node)
    {
        // Get the input expression
        var inputExpr = GetInputExpression(node, "in");
        if (inputExpr == null) return;

        // Register the pass-through output
        var outPort = node.Outputs.FirstOrDefault(p => p.Name == "out");
        if (outPort != null)
        {
            _portVariables[$"{node.Id}:out"] = inputExpr;
        }

        // Register property access expressions for each property output
        foreach (var output in node.Outputs.Where(p => p.Name != "out"))
        {
            var propertyName = GetPropertyAccessor(node.NodeType, output.Name);
            _portVariables[$"{node.Id}:{output.Name}"] = $"{inputExpr}.{propertyName}";
        }
    }

    /// <summary>
    /// Get the C# property accessor name for a data node output.
    /// </summary>
    private static string GetPropertyAccessor(TimelineNodeType nodeType, string outputName)
    {
        return nodeType switch
        {
            TimelineNodeType.Actor => outputName switch
            {
                "name" => "Name",
                "faction" => "FactionId",
                "hp" => "Hitpoints",
                "ap" => "ActionPoints",
                _ => ToPascalCase(outputName)
            },
            TimelineNodeType.Skill => outputName switch
            {
                "isAttack" => "IsAttack",
                "isSilent" => "IsSilent",
                "name" => "Name",
                _ => ToPascalCase(outputName)
            },
            TimelineNodeType.Tile => outputName switch
            {
                "x" => "X",
                "y" => "Y",
                "occupant" => "Occupant",
                _ => ToPascalCase(outputName)
            },
            _ => ToPascalCase(outputName)
        };
    }

    /// <summary>
    /// Generate code for template reference nodes.
    /// </summary>
    private void GenerateTemplateRef(TimelineNode node)
    {
        var templateType = node.Properties.TryGetValue("templateType", out var tt) ? tt?.ToString() ?? "" : "";
        var templateId = node.Properties.TryGetValue("templateId", out var tid) ? tid?.ToString() ?? "" : "";

        if (string.IsNullOrEmpty(templateId))
        {
            AppendLine($"// Warning: Template reference node has no templateId set");
            return;
        }

        var varName = $"template_{SanitizeName(templateId)}";
        var entityType = templateType switch
        {
            "ActorTemplate" => "ActorTemplate",
            "SkillTemplate" => "SkillTemplate",
            "ItemTemplate" => "ItemTemplate",
            _ => "Template"
        };

        AppendLine($"var {varName} = Templates.Get<{entityType}>(\"{templateType}\", \"{templateId}\");");

        // Register the output variable
        var outputPort = node.Outputs.FirstOrDefault();
        if (outputPort != null)
        {
            _portVariables[$"{node.Id}:{outputPort.Name}"] = varName;
        }
    }

    private void GenerateCondition(TimelineNode node, HashSet<string> processedNodes)
    {
        var inputExpr = GetConditionExpression(node);

        AppendLine($"if ({inputExpr})");
        AppendLine("{");
        _indent++;

        // Get nodes connected to "pass" output
        var passConnections = GetOutputConnections(node, "pass");
        foreach (var conn in passConnections)
        {
            if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
            {
                GenerateNode(conn.TargetNode, processedNodes);
            }
        }

        _indent--;
        AppendLine("}");

        // Check for fail branch
        var failConnections = GetOutputConnections(node, "fail");
        if (failConnections.Any())
        {
            AppendLine("else");
            AppendLine("{");
            _indent++;

            foreach (var conn in failConnections)
            {
                if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
                {
                    GenerateNode(conn.TargetNode, processedNodes);
                }
            }

            _indent--;
            AppendLine("}");
        }
    }

    /// <summary>
    /// Get the condition expression based on what's connected to the "value" input.
    /// Generates proper property access based on the source node type.
    /// </summary>
    private string GetConditionExpression(TimelineNode node)
    {
        var valuePort = node.Inputs.FirstOrDefault(p => p.Name == "value");
        if (valuePort == null) return "false";

        var conn = _graph.Connections.FirstOrDefault(c => c.TargetPort == valuePort);
        if (conn?.SourcePort?.Node == null) return "false";

        var sourceNode = conn.SourcePort.Node;
        var sourcePortName = conn.SourcePort.Name;

        // If we have a registered variable, use it directly
        var varKey = $"{sourceNode.Id}:{sourcePortName}";
        if (_portVariables.TryGetValue(varKey, out var varExpr))
        {
            return varExpr;
        }

        // Handle data nodes with property outputs
        if (sourceNode.NodeType == TimelineNodeType.Skill)
        {
            // Need to get the skill variable from what's connected to the Skill node's input
            var skillInputExpr = GetInputExpression(sourceNode, "in") ?? "skill";
            var propertyAccessor = GetPropertyAccessor(TimelineNodeType.Skill, sourcePortName);
            return $"{skillInputExpr}.{propertyAccessor}";
        }

        if (sourceNode.NodeType == TimelineNodeType.Actor)
        {
            var actorInputExpr = GetInputExpression(sourceNode, "in") ?? "actor";
            var propertyAccessor = GetPropertyAccessor(TimelineNodeType.Actor, sourcePortName);
            return $"{actorInputExpr}.{propertyAccessor}";
        }

        // Handle logic nodes (Compare, Not, And, Or)
        if (sourceNode.NodeType == TimelineNodeType.Compare ||
            sourceNode.NodeType == TimelineNodeType.Not ||
            sourceNode.NodeType == TimelineNodeType.And ||
            sourceNode.NodeType == TimelineNodeType.Or)
        {
            return GetNodeVarName(sourceNode);
        }

        // Handle trigger outputs directly
        if (IsTriggerNode(sourceNode.NodeType))
        {
            return sourcePortName;
        }

        return "false";
    }

    private void GenerateCompare(TimelineNode node)
    {
        var a = GetInputExpression(node, "a") ?? "null";
        var b = GetInputExpression(node, "b") ?? "null";
        var op = node.Properties.TryGetValue("operator", out var opVal) ? opVal?.ToString() : "==";

        var varName = GetNodeVarName(node);
        AppendLine($"var {varName} = {a} {op} {b};");

        // Register the output for "result" port
        _portVariables[$"{node.Id}:result"] = varName;
    }

    private void GenerateMathOp(TimelineNode node)
    {
        var a = GetInputExpression(node, "a") ?? "0";
        var b = GetInputExpression(node, "b") ?? "0";
        var op = node.Properties.TryGetValue("operator", out var opVal) ? opVal?.ToString() : "+";

        var varName = GetNodeVarName(node);
        AppendLine($"var {varName} = {a} {op} {b};");

        // Register the output for "result" port
        _portVariables[$"{node.Id}:result"] = varName;
    }

    private void GenerateNot(TimelineNode node)
    {
        var input = GetInputExpression(node, "in") ?? "false";
        var varName = GetNodeVarName(node);
        AppendLine($"var {varName} = !({input});");

        // Register the output
        _portVariables[$"{node.Id}:out"] = varName;
    }

    private void GenerateLogicOp(TimelineNode node)
    {
        var a = GetInputExpression(node, "a") ?? "false";
        var b = GetInputExpression(node, "b") ?? "false";
        var op = node.NodeType == TimelineNodeType.And ? "&&" : "||";

        var varName = GetNodeVarName(node);
        AppendLine($"var {varName} = ({a}) {op} ({b});");

        // Register the output
        _portVariables[$"{node.Id}:result"] = varName;
    }

    private void GenerateEffect(TimelineNode node)
    {
        var actorExpr = GetInputExpression(node, "actor") ?? "actor";
        var property = node.Properties.TryGetValue("property", out var propVal) ? propVal?.ToString() : "concealment";
        var modifier = node.Properties.TryGetValue("modifier", out var modVal) ? modVal : -3;
        var duration = node.Properties.TryGetValue("duration", out var durVal) ? durVal : 1;

        // Use the resolved type to determine the appropriate API call
        var actorInput = node.Inputs.FirstOrDefault(p => p.Name == "actor");
        var resolvedType = actorInput?.ResolvedType;

        // Generate the appropriate effect call based on the resolved type
        if (resolvedType?.StartsWith("ActorTemplate:") == true)
        {
            // If connected to a template reference, use template-based effect
            AppendLine($"EffectSystem.AddTemplateEffect({actorExpr}, \"{property}\", {modifier}, {duration}, \"{_graph.Name}\");");
        }
        else
        {
            // Standard actor effect
            AppendLine($"{actorExpr}.AddEffect(\"{property}\", {modifier}, {duration}, \"{_graph.Name}\");");
        }
    }

    private void GenerateDamage(TimelineNode node)
    {
        var actorExpr = GetInputExpression(node, "actor") ?? "actor";
        var amountExpr = GetInputExpression(node, "amount");

        // Use connected amount or fall back to property
        var amount = amountExpr ?? (node.Properties.TryGetValue("amount", out var amtVal) ? amtVal?.ToString() : "10");

        AppendLine($"{actorExpr}.ApplyDamage({amount});");
    }

    private void GenerateHeal(TimelineNode node)
    {
        var actorExpr = GetInputExpression(node, "actor") ?? "actor";
        var amountExpr = GetInputExpression(node, "amount");

        // Use connected amount or fall back to property
        var amount = amountExpr ?? (node.Properties.TryGetValue("amount", out var amtVal) ? amtVal?.ToString() : "10");

        AppendLine($"{actorExpr}.Heal({amount});");
    }

    private void GenerateKill(TimelineNode node)
    {
        var actorExpr = GetInputExpression(node, "actor") ?? "actor";
        AppendLine($"{actorExpr}.Kill();");
    }

    private void GenerateLog(TimelineNode node)
    {
        var message = node.Properties.TryGetValue("message", out var msgVal) ? msgVal?.ToString() : "Debug";
        var messageInput = GetInputExpression(node, "message");

        if (messageInput != null)
        {
            // If message is connected, use string interpolation
            AppendLine($"Logger.Log($\"{{message}}: {{{messageInput}}}\");");
        }
        else
        {
            AppendLine($"Logger.Log(\"{message}\");");
        }
    }

    private void GenerateForEachActor(TimelineNode node, HashSet<string> processedNodes)
    {
        var filter = node.Properties.TryGetValue("filter", out var filterVal) ? filterVal?.ToString() : "all";
        var loopVar = "loopActor";

        // Use GameQuery to find all actors, with filter applied via continue
        AppendLine($"foreach (var actorObj in GameQuery.FindAll(\"Menace.Tactical.Actor\"))");
        AppendLine("{");
        _indent++;
        AppendLine($"var {loopVar} = new Actor(actorObj);");

        // Add filter condition if not "all"
        if (filter == "player")
        {
            AppendLine($"if ({loopVar}.FactionId != 1) continue; // Filter to player faction");
        }
        else if (filter == "enemy")
        {
            AppendLine($"if ({loopVar}.FactionId == 1) continue; // Filter to non-player factions");
        }

        AppendLine();

        // Register the loop variable for the "actor" output
        _portVariables[$"{node.Id}:actor"] = loopVar;

        // Get nodes connected to "actor" output and process them
        var actorConnections = GetOutputConnections(node, "actor");
        foreach (var conn in actorConnections)
        {
            if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
            {
                GenerateNode(conn.TargetNode, processedNodes);
            }
        }

        _indent--;
        AppendLine("}");

        // Process nodes connected to "done" output after the loop
        var doneConnections = GetOutputConnections(node, "done");
        foreach (var conn in doneConnections)
        {
            if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
            {
                GenerateNode(conn.TargetNode, processedNodes);
            }
        }
    }

    private void GenerateSetVariable(TimelineNode node)
    {
        var varName = node.Properties.TryGetValue("name", out var nameVal) ? nameVal?.ToString() : "myVar";
        var value = GetInputExpression(node, "value") ?? "null";

        AppendLine($"var {SanitizeName(varName!)} = {value};");
    }

    /// <summary>
    /// Get the expression for an input port, tracing back through connections.
    /// </summary>
    private string? GetInputExpression(TimelineNode node, string portName)
    {
        var port = node.Inputs.FirstOrDefault(p => p.Name == portName);
        if (port == null) return null;

        var connection = _graph.Connections.FirstOrDefault(c => c.TargetPort == port);
        if (connection?.SourcePort?.Node == null) return null;

        var sourceNode = connection.SourcePort.Node;
        var sourcePortName = connection.SourcePort.Name;

        // Check if we have a registered variable for this port
        var varKey = $"{sourceNode.Id}:{sourcePortName}";
        if (_portVariables.TryGetValue(varKey, out var varExpr))
        {
            return varExpr;
        }

        // Handle trigger node outputs
        if (IsTriggerNode(sourceNode.NodeType))
        {
            return sourcePortName;
        }

        // Handle data nodes with property outputs
        if (sourceNode.NodeType == TimelineNodeType.Actor ||
            sourceNode.NodeType == TimelineNodeType.Skill ||
            sourceNode.NodeType == TimelineNodeType.Tile)
        {
            // Get the input to the data node first
            var dataNodeInput = GetInputExpression(sourceNode, "in");
            if (dataNodeInput != null)
            {
                if (sourcePortName == "out")
                {
                    return dataNodeInput;
                }
                else
                {
                    var accessor = GetPropertyAccessor(sourceNode.NodeType, sourcePortName);
                    return $"{dataNodeInput}.{accessor}";
                }
            }
        }

        // Handle GetVariable nodes
        if (sourceNode.NodeType == TimelineNodeType.GetVariable)
        {
            var varName = sourceNode.Properties.TryGetValue("name", out var n) ? n?.ToString() : "myVar";
            return SanitizeName(varName!);
        }

        // Return a reference to the source node's output
        return GetNodeVarName(sourceNode);
    }

    private List<NodeConnection> GetOutputConnections(TimelineNode node, string portName)
    {
        var port = node.Outputs.FirstOrDefault(p => p.Name == portName);
        if (port == null) return new List<NodeConnection>();

        return _graph.Connections.Where(c => c.SourcePort == port).ToList();
    }

    private static string GetNodeVarName(TimelineNode node)
    {
        return $"node_{node.Id.Replace("-", "").Substring(0, 8)}";
    }

    private static bool IsTriggerNode(TimelineNodeType type)
    {
        return type switch
        {
            TimelineNodeType.TriggerSkillUsed => true,
            TimelineNodeType.TriggerDamageReceived => true,
            TimelineNodeType.TriggerActorKilled => true,
            TimelineNodeType.TriggerMovementStarted => true,
            TimelineNodeType.TriggerMovementFinished => true,
            TimelineNodeType.TriggerTurnStart => true,
            TimelineNodeType.TriggerTurnEnd => true,
            TimelineNodeType.TriggerRoundStart => true,
            TimelineNodeType.TriggerRoundEnd => true,
            TimelineNodeType.TriggerHitpointsChanged => true,
            TimelineNodeType.TriggerSuppressed => true,
            _ => false
        };
    }

    private static string GetHookName(TimelineNodeType type)
    {
        return type switch
        {
            TimelineNodeType.TriggerSkillUsed => "OnSkillUsed",
            TimelineNodeType.TriggerDamageReceived => "OnDamageReceived",
            TimelineNodeType.TriggerActorKilled => "OnActorKilled",
            TimelineNodeType.TriggerMovementStarted => "OnMovementStarted",
            TimelineNodeType.TriggerMovementFinished => "OnMovementFinished",
            TimelineNodeType.TriggerTurnStart => "OnTurnStart",
            TimelineNodeType.TriggerTurnEnd => "OnTurnEnd",
            TimelineNodeType.TriggerRoundStart => "OnRoundStart",
            TimelineNodeType.TriggerRoundEnd => "OnRoundEnd",
            TimelineNodeType.TriggerHitpointsChanged => "OnHitpointsChanged",
            TimelineNodeType.TriggerSuppressed => "OnSuppressed",
            _ => "OnUnknown"
        };
    }

    private static string GetTriggerParameters(TimelineNodeType type)
    {
        // Parameters must match TacticalEventHooks delegate signatures exactly
        return type switch
        {
            TimelineNodeType.TriggerSkillUsed => "IntPtr userPtr, IntPtr skillPtr, IntPtr targetPtr",
            TimelineNodeType.TriggerDamageReceived => "IntPtr targetPtr, IntPtr attackerPtr, IntPtr skillPtr",
            TimelineNodeType.TriggerActorKilled => "IntPtr actorPtr, IntPtr killerPtr, int factionId",
            TimelineNodeType.TriggerMovementStarted => "IntPtr actorPtr, IntPtr fromTilePtr, IntPtr toTilePtr",
            TimelineNodeType.TriggerMovementFinished => "IntPtr actorPtr, IntPtr tilePtr",
            TimelineNodeType.TriggerTurnStart => "IntPtr actorPtr",  // Note: SDK doesn't have OnTurnStart, we'll add it
            TimelineNodeType.TriggerTurnEnd => "IntPtr actorPtr",
            TimelineNodeType.TriggerRoundStart => "int roundNumber",
            TimelineNodeType.TriggerRoundEnd => "int roundNumber",
            TimelineNodeType.TriggerHitpointsChanged => "IntPtr actorPtr, int oldHp, int newHp",
            TimelineNodeType.TriggerSuppressed => "IntPtr actorPtr",
            _ => ""
        };
    }

    private static string SanitizeName(string name)
    {
        var result = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
                result.Append(c);
            else if (result.Length > 0 && result[result.Length - 1] != '_')
                result.Append('_');
        }

        var str = result.ToString().Trim('_');
        if (str.Length == 0 || char.IsDigit(str[0]))
            str = "_" + str;

        return str;
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private void AppendLine(string line = "")
    {
        if (string.IsNullOrEmpty(line))
        {
            _sb.AppendLine();
        }
        else
        {
            _sb.Append(new string(' ', _indent * 4));
            _sb.AppendLine(line);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Timing/Coroutine Node Generation
    // ═══════════════════════════════════════════════════════════════════

    private void GenerateDelay(TimelineNode node, HashSet<string> processedNodes)
    {
        var rounds = node.Properties.TryGetValue("rounds", out var r) ? Convert.ToInt32(r) : 1;
        var actorExpr = GetInputExpression(node, "actor") ?? "IntPtr.Zero";
        var nodeKey = $"delay_{node.Id.Replace("-", "").Substring(0, 8)}";

        AppendLine($"Coroutine.Delay({actorExpr}.Pointer, \"{nodeKey}\", {rounds}, () =>");
        AppendLine("{");
        _indent++;

        // Generate connected execute nodes
        var executeConnections = GetOutputConnections(node, "execute");
        foreach (var conn in executeConnections)
        {
            if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
            {
                GenerateNode(conn.TargetNode, processedNodes);
            }
        }

        _indent--;
        AppendLine("});");
    }

    private void GenerateRepeat(TimelineNode node, HashSet<string> processedNodes)
    {
        var interval = node.Properties.TryGetValue("interval", out var i) ? Convert.ToInt32(i) : 1;
        var count = node.Properties.TryGetValue("count", out var c) ? Convert.ToInt32(c) : 3;
        var actorExpr = GetInputExpression(node, "actor") ?? "IntPtr.Zero";
        var nodeKey = $"repeat_{node.Id.Replace("-", "").Substring(0, 8)}";

        // Track iterations for "done" callback
        var iterVar = $"iter_{node.Id.Replace("-", "").Substring(0, 8)}";
        AppendLine($"var {iterVar} = 0;");

        AppendLine($"Coroutine.Repeat({actorExpr}.Pointer, \"{nodeKey}\", {interval}, {count}, () =>");
        AppendLine("{");
        _indent++;
        AppendLine($"{iterVar}++;");

        // Generate connected execute nodes
        var executeConnections = GetOutputConnections(node, "execute");
        foreach (var conn in executeConnections)
        {
            if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
            {
                GenerateNode(conn.TargetNode, processedNodes);
            }
        }

        // Check if done
        AppendLine($"if ({iterVar} >= {count})");
        AppendLine("{");
        _indent++;

        var doneConnections = GetOutputConnections(node, "done");
        foreach (var conn in doneConnections)
        {
            if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
            {
                GenerateNode(conn.TargetNode, processedNodes);
            }
        }

        _indent--;
        AppendLine("}");

        _indent--;
        AppendLine("});");
    }

    private void GenerateOnce(TimelineNode node, HashSet<string> processedNodes)
    {
        var scope = node.Properties.TryGetValue("scope", out var s) ? s?.ToString() : "entity";
        var actorExpr = GetInputExpression(node, "actor");
        var nodeKey = $"once_{node.Id.Replace("-", "").Substring(0, 8)}";

        if (scope == "combat" || actorExpr == null)
        {
            AppendLine($"if (OnceTracker.TryExecuteGlobal(\"{nodeKey}\"))");
        }
        else
        {
            AppendLine($"if (OnceTracker.TryExecute({actorExpr}.Pointer, \"{nodeKey}\"))");
        }

        AppendLine("{");
        _indent++;

        var executeConnections = GetOutputConnections(node, "execute");
        foreach (var conn in executeConnections)
        {
            if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
            {
                GenerateNode(conn.TargetNode, processedNodes);
            }
        }

        _indent--;
        AppendLine("}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  State Machine Node Generation
    // ═══════════════════════════════════════════════════════════════════

    private void GenerateState(TimelineNode node, HashSet<string> processedNodes)
    {
        var stateName = node.Properties.TryGetValue("name", out var n) ? n?.ToString() : "idle";
        var machine = node.Properties.TryGetValue("machine", out var m) ? m?.ToString() : "default";

        // State nodes are a bit special - they define enter/exit callbacks
        // The actual state change happens via SetState or Transition nodes

        // We'll look for an actor input to determine context
        // If no actor connected, this is a declaration-only node
        var actorExpr = GetInputExpression(node, "enter");

        // Generate a comment explaining this state
        AppendLine($"// State '{stateName}' in machine '{machine}'");

        // If there are onEnter connections, register the callback
        var enterConnections = GetOutputConnections(node, "onEnter");
        if (enterConnections.Count > 0 && actorExpr != null)
        {
            AppendLine($"StateMachine.OnEnter({actorExpr}.Pointer, \"{machine}\", \"{stateName}\", () =>");
            AppendLine("{");
            _indent++;

            foreach (var conn in enterConnections)
            {
                if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
                {
                    GenerateNode(conn.TargetNode, processedNodes);
                }
            }

            _indent--;
            AppendLine("});");
        }

        // If there are onExit connections, register the callback
        var exitConnections = GetOutputConnections(node, "onExit");
        if (exitConnections.Count > 0 && actorExpr != null)
        {
            AppendLine($"StateMachine.OnExit({actorExpr}.Pointer, \"{machine}\", \"{stateName}\", () =>");
            AppendLine("{");
            _indent++;

            foreach (var conn in exitConnections)
            {
                if (conn.TargetNode != null && !processedNodes.Contains(conn.TargetNode.Id))
                {
                    GenerateNode(conn.TargetNode, processedNodes);
                }
            }

            _indent--;
            AppendLine("});");
        }

        // Register the active output expression
        if (actorExpr != null)
        {
            _portVariables[$"{node.Id}:active"] = $"StateMachine.IsInState({actorExpr}.Pointer, \"{machine}\", \"{stateName}\")";
        }
    }

    private void GenerateTransition(TimelineNode node)
    {
        var fromState = node.Properties.TryGetValue("from", out var f) ? f?.ToString() : "idle";
        var toState = node.Properties.TryGetValue("to", out var t) ? t?.ToString() : "active";
        var machine = node.Properties.TryGetValue("machine", out var m) ? m?.ToString() : "default";

        var conditionExpr = GetInputExpression(node, "condition") ?? "true";
        var actorExpr = GetInputExpression(node, "actor");

        if (actorExpr == null)
        {
            AppendLine($"// Transition: {fromState} -> {toState} (no actor connected)");
            return;
        }

        AppendLine($"if ({conditionExpr})");
        AppendLine("{");
        _indent++;
        AppendLine($"StateMachine.Transition({actorExpr}.Pointer, \"{machine}\", \"{fromState}\", \"{toState}\");");
        _indent--;
        AppendLine("}");
    }

    private void GenerateGetState(TimelineNode node)
    {
        var machine = node.Properties.TryGetValue("machine", out var m) ? m?.ToString() : "default";
        var checkState = node.Properties.TryGetValue("checkState", out var c) ? c?.ToString() : "";

        var actorExpr = GetInputExpression(node, "actor");
        if (actorExpr == null) return;

        var stateVar = $"state_{node.Id.Replace("-", "").Substring(0, 8)}";
        AppendLine($"var {stateVar} = StateMachine.GetState({actorExpr}.Pointer, \"{machine}\");");

        _portVariables[$"{node.Id}:state"] = stateVar;

        if (!string.IsNullOrEmpty(checkState))
        {
            _portVariables[$"{node.Id}:isState"] = $"({stateVar} == \"{checkState}\")";
        }
    }

    private void GenerateSetState(TimelineNode node)
    {
        var machine = node.Properties.TryGetValue("machine", out var m) ? m?.ToString() : "default";
        var state = node.Properties.TryGetValue("state", out var s) ? s?.ToString() : "idle";

        var actorExpr = GetInputExpression(node, "actor");
        if (actorExpr == null)
        {
            AppendLine($"// SetState: {state} (no actor connected)");
            return;
        }

        AppendLine($"StateMachine.SetState({actorExpr}.Pointer, \"{machine}\", \"{state}\");");
    }
}
