# Visual Mod Graph JSON Format Specification

## Overview

This document specifies the JSON format for visual mod graphs. These graphs represent game modifications using a node-based system where **event nodes** trigger **condition nodes** that filter data before passing to **action nodes** that modify game state.

The format is designed for:
- Easy serialization with `System.Text.Json`
- Clean mapping to runtime C# code generation
- Forward compatibility through versioning
- Human readability for debugging

**Corresponding C# models:** `Menace.SDK.VisualEditor.Models.GraphModel.cs`

---

## File Structure

A mod graph file (`.modgraph.json`) contains:

```
ModGraphFile
├── formatVersion: int          # Schema version for compatibility
├── metadata: ModMetadata       # Mod identification and versioning
├── graphs: ModGraph[]          # One or more graphs (per hook point)
└── variables: Dictionary       # Optional shared variables
```

---

## Schema

### Root Object: `ModGraphFile`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `formatVersion` | `int` | Yes | Schema version. Current: `1` |
| `metadata` | `ModMetadata` | Yes | Mod identification |
| `graphs` | `ModGraph[]` | Yes | List of graphs |
| `variables` | `object` | No | User-defined variables |

### `ModMetadata`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | `string` | Yes | Unique mod identifier (e.g., `"beagles-concealment"`) |
| `name` | `string` | Yes | Human-readable name |
| `author` | `string` | Yes | Creator name |
| `version` | `string` | Yes | Semantic version (e.g., `"1.0.0"`) |
| `description` | `string` | No | What the mod does |
| `gameVersion` | `string` | No | Minimum compatible game version |
| `dependencies` | `string[]` | No | Required mod IDs |
| `tags` | `string[]` | No | Categorization tags |

### `ModGraph`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | `string` | Yes | Unique graph identifier within file |
| `name` | `string` | Yes | Display name |
| `hookPoint` | `string` | Yes | Game event to attach to |
| `enabled` | `bool` | No | Whether graph is active (default: `true`) |
| `priority` | `int` | No | Execution order (default: `0`, lower = earlier) |
| `nodes` | `GraphNode[]` | Yes | All nodes in graph |
| `connections` | `NodeConnection[]` | Yes | All connections |
| `description` | `string` | No | Graph documentation |

### `GraphNode`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | `string` | Yes | Unique node identifier within graph |
| `type` | `string` | Yes | Node category: `event`, `condition`, `action`, `logic`, `value` |
| `subtype` | `string` | Yes | Specific node type (e.g., `skill_used`, `add_effect`) |
| `x` | `number` | Yes | X position in editor |
| `y` | `number` | Yes | Y position in editor |
| `config` | `object` | No | Property values (dropdowns, fields, etc.) |
| `label` | `string` | No | Custom display label |
| `comment` | `string` | No | User note (not compiled) |
| `collapsed` | `bool` | No | Editor display state |

### `NodeConnection`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | `string` | Yes | Unique connection identifier |
| `sourceNodeId` | `string` | Yes | Output node ID |
| `sourcePort` | `string` | Yes | Output port name |
| `targetNodeId` | `string` | Yes | Input node ID |
| `targetPort` | `string` | Yes | Input port name |

---

## Hook Points

Hook points define when a graph executes. The graph's `hookPoint` field must match one of these values:

### Tactical (Combat) Hooks

| Hook Point | Available Data | Description |
|------------|----------------|-------------|
| `skill_used` | `actor`, `skill`, `target` | Any skill/ability activation |
| `damage_received` | `target`, `attacker`, `skill`, `amount` | Entity takes damage |
| `actor_killed` | `actor`, `killer`, `skill` | Entity dies |
| `attack_missed` | `attacker`, `target`, `skill` | Attack fails to hit |
| `round_start` | `round_number` | New combat round begins |
| `round_end` | `round_number` | Combat round ends |
| `turn_start` | `actor` | Entity's turn begins |
| `turn_end` | `actor` | Entity's turn ends |
| `move_start` | `actor`, `from_tile`, `to_tile` | Movement begins |
| `move_complete` | `actor`, `from_tile`, `to_tile` | Movement completes |

### Strategy Hooks

| Hook Point | Available Data | Description |
|------------|----------------|-------------|
| `leader_hired` | `leader`, `template` | Leader joins roster |
| `leader_dismissed` | `leader` | Leader leaves roster |
| `leader_levelup` | `leader`, `perk` | Leader gains perk |
| `faction_trust_changed` | `faction`, `delta` | Faction relationship changes |
| `mission_ended` | `mission`, `status` | Mission completes |
| `blackmarket_restocked` | - | Shop inventory refreshes |

---

## Node Types

### Event Nodes (`type: "event"`)

Event nodes are the entry points for graphs. Each graph must have exactly one event node matching its `hookPoint`.

**Outputs:** Data ports matching the hook point's available data.

| Subtype | Outputs | Description |
|---------|---------|-------------|
| `skill_used` | `actor`, `skill`, `target` | Skill activation |
| `damage_received` | `target`, `attacker`, `skill`, `amount` | Damage event |
| `actor_killed` | `actor`, `killer` | Death event |
| `round_start` | `round_number` | Round begins |
| `turn_end` | `actor` | Turn ends |

### Condition Nodes (`type: "condition"`)

Condition nodes filter data based on property checks.

**Inputs:** One data input (`input`)
**Outputs:** `pass` (condition true), `fail` (condition false)

**Config Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `property` | `string` | Property to check (e.g., `"IsAttack"`, `"IsSilent"`) |
| `operator` | `string` | Comparison: `==`, `!=`, `<`, `>`, `<=`, `>=`, `contains` |
| `value` | `any` | Value to compare against |

### Logic Nodes (`type: "logic"`)

Logic nodes combine boolean signals.

| Subtype | Inputs | Outputs | Description |
|---------|--------|---------|-------------|
| `and` | `a`, `b` (multiple) | `result` | All inputs true |
| `or` | `a`, `b` (multiple) | `result` | Any input true |
| `not` | `input` | `result` | Invert signal |

### Action Nodes (`type: "action"`)

Action nodes modify game state.

**Inputs:** `actor` (or `target`), plus action-specific data

| Subtype | Inputs | Config | Description |
|---------|--------|--------|-------------|
| `add_effect` | `actor` | `property`, `modifier`, `duration` | Modify entity stat |
| `damage` | `actor` | `amount` | Deal damage |
| `heal` | `actor` | `amount` | Restore health |
| `set_flag` | `actor` | `flag`, `value` | Set entity flag |
| `log` | - | `message` | Debug output |

### Value Nodes (`type: "value"`)

Value nodes provide constant or computed values.

| Subtype | Outputs | Config | Description |
|---------|---------|--------|-------------|
| `constant` | `value` | `value` | Fixed value |
| `variable` | `value` | `variableId` | Reference to mod variable |
| `random` | `value` | `min`, `max` | Random number |

---

## Port Data Types

Ports have data types for connection validation:

| Type | Description | Example |
|------|-------------|---------|
| `flow` | Execution flow (no data) | Control flow connections |
| `actor` | Entity reference | Soldiers, enemies |
| `skill` | Skill/ability reference | Attack skills |
| `number` | Numeric value | Damage amounts |
| `bool` | Boolean value | Condition results |
| `string` | Text value | Log messages |
| `any` | Any compatible type | Generic connections |

---

## Example: Beagle's Concealment Mod

This example implements a mod where using non-silent attack skills reduces the actor's concealment by 3.

```json
{
  "formatVersion": 1,
  "metadata": {
    "id": "beagles-concealment",
    "name": "Beagle's Concealment",
    "author": "Beagle",
    "version": "1.0.0",
    "description": "Non-silent attacks reduce concealment by 3, making stealth tactics more meaningful.",
    "tags": ["balance", "stealth"]
  },
  "graphs": [
    {
      "id": "main",
      "name": "Concealment Penalty",
      "hookPoint": "skill_used",
      "enabled": true,
      "priority": 0,
      "description": "When a non-silent attack is used, reduce actor concealment",
      "nodes": [
        {
          "id": "event1",
          "type": "event",
          "subtype": "skill_used",
          "x": 100,
          "y": 200,
          "config": {},
          "label": "Skill Used"
        },
        {
          "id": "cond1",
          "type": "condition",
          "subtype": "property_check",
          "x": 350,
          "y": 150,
          "config": {
            "property": "IsAttack",
            "operator": "==",
            "value": true
          },
          "label": "Is Attack?"
        },
        {
          "id": "cond2",
          "type": "condition",
          "subtype": "property_check",
          "x": 600,
          "y": 150,
          "config": {
            "property": "IsSilent",
            "operator": "==",
            "value": false
          },
          "label": "Not Silent?"
        },
        {
          "id": "action1",
          "type": "action",
          "subtype": "add_effect",
          "x": 850,
          "y": 200,
          "config": {
            "property": "Concealment",
            "modifier": -3,
            "duration": 0
          },
          "label": "Concealment -3"
        }
      ],
      "connections": [
        {
          "id": "conn1",
          "sourceNodeId": "event1",
          "sourcePort": "skill",
          "targetNodeId": "cond1",
          "targetPort": "input"
        },
        {
          "id": "conn2",
          "sourceNodeId": "cond1",
          "sourcePort": "pass",
          "targetNodeId": "cond2",
          "targetPort": "input"
        },
        {
          "id": "conn3",
          "sourceNodeId": "event1",
          "sourcePort": "actor",
          "targetNodeId": "action1",
          "targetPort": "actor"
        },
        {
          "id": "conn4",
          "sourceNodeId": "cond2",
          "sourcePort": "pass",
          "targetNodeId": "action1",
          "targetPort": "flow_in"
        }
      ]
    }
  ],
  "variables": {}
}
```

### Visual Representation

```
    +-----------------+
    |  Skill Used     |
    |  (event)        |
    |                 |
    |  actor o--------+------------------------+
    |  skill o---+    |                        |
    +-----------------+                        |
                 |                             |
                 v                             |
    +-----------------+                        |
    |  Is Attack?     |                        |
    |  (condition)    |                        |
    |                 |                        |
    |  o input        |                        |
    |  IsAttack == T  |                        |
    |                 |                        |
    |  pass o---------+                        |
    +-----------------+                        |
                      |                        |
                      v                        |
         +-----------------+                   |
         |  Not Silent?    |                   |
         |  (condition)    |                   |
         |                 |                   |
         |  o input        |                   |
         |  IsSilent == F  |                   |
         |                 |                   |
         |  pass o---------+                   |
         +-----------------+                   |
                           |                   |
                           v                   v
              +-------------------------+
              |  Add Effect             |
              |  (action)               |
              |                         |
              |  o flow_in   o actor    |
              |  Property: Concealment  |
              |  Modifier: -3           |
              |  Duration: Instant      |
              +-------------------------+
```

---

## Generated C# Code

The above graph compiles to approximately:

```csharp
[HookPriority(0)]
public static void BeaglesConcealment_ConcealmentPenalty(SkillUsedEventArgs e)
{
    var actor = e.Actor;
    var skill = e.Skill;

    // cond1: Is Attack?
    if (skill.IsAttack != true)
        return;

    // cond2: Not Silent?
    if (skill.IsSilent != false)
        return;

    // action1: Add Effect
    EntityEffects.AddModifier(actor, EntityProperty.Concealment, -3, duration: 0);
}
```

---

## Multiple Graphs Example

A mod can contain multiple graphs for different hook points:

```json
{
  "formatVersion": 1,
  "metadata": {
    "id": "tactical-overhaul",
    "name": "Tactical Overhaul",
    "author": "ModTeam",
    "version": "2.0.0"
  },
  "graphs": [
    {
      "id": "concealment",
      "name": "Concealment on Attack",
      "hookPoint": "skill_used",
      "nodes": [ /* ... */ ],
      "connections": [ /* ... */ ]
    },
    {
      "id": "morale",
      "name": "Morale on Kill",
      "hookPoint": "actor_killed",
      "nodes": [ /* ... */ ],
      "connections": [ /* ... */ ]
    },
    {
      "id": "round_effects",
      "name": "Round Start Effects",
      "hookPoint": "round_start",
      "nodes": [ /* ... */ ],
      "connections": [ /* ... */ ]
    }
  ]
}
```

---

## Variables

Variables allow mod configuration without editing the graph:

```json
{
  "variables": {
    "concealmentPenalty": {
      "name": "Concealment Penalty",
      "type": "int",
      "defaultValue": -3,
      "min": -10,
      "max": 0,
      "description": "How much concealment to remove on non-silent attacks"
    },
    "affectedFactions": {
      "name": "Affected Factions",
      "type": "enum",
      "enumType": "FactionType",
      "defaultValue": "Player",
      "description": "Which factions this applies to"
    }
  }
}
```

Reference in node config:
```json
{
  "config": {
    "modifier": { "$var": "concealmentPenalty" }
  }
}
```

---

## Validation Rules

1. **Unique IDs**: All `id` fields must be unique within their scope
2. **Valid hookPoint**: Must match a known hook point
3. **Single event node**: Each graph must have exactly one event node
4. **Connected ports**: All required ports must be connected
5. **Type compatibility**: Connected ports must have compatible data types
6. **No cycles**: Condition/action flow must be acyclic

---

## Version History

| Version | Changes |
|---------|---------|
| 1 | Initial format |

---

## File Extension

Mod graph files should use the extension `.modgraph.json` or be embedded within modpack archives.

---

## C# Usage

```csharp
using System.Text.Json;
using Menace.SDK.VisualEditor.Models;

// Load
var json = File.ReadAllText("mymod.modgraph.json");
var modFile = JsonSerializer.Deserialize<ModGraphFile>(json, GraphJsonOptions.Default);

// Save
var outputJson = JsonSerializer.Serialize(modFile, GraphJsonOptions.Default);
File.WriteAllText("output.modgraph.json", outputJson);

// Access
foreach (var graph in modFile.Graphs)
{
    Console.WriteLine($"Graph: {graph.Name} @ {graph.HookPoint}");
    foreach (var node in graph.Nodes)
    {
        Console.WriteLine($"  Node: {node.Type}/{node.Subtype} at ({node.X}, {node.Y})");
    }
}
```
