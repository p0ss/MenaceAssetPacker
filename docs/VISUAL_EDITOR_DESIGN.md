# Visual Mod Editor Design Document

## Overview

This document explores two approaches to a visual mod editor that replaces Lua scripting with a node-based interface. The key question: **Lego blocks on train tracks** vs **Playdough with strings**.

---

## The Game Cycles

Before designing nodes, we must understand what modders are inserting into.

### Tactical Cycle (per mission)

```
╔═══════════════════════════════════════════════════════════════════════════════╗
║  ROUND START                                                                   ║
║  ├── [Hook: round_start]                                                       ║
║  │                                                                             ║
║  ├── FACTION TURN (repeat for each faction) ◀────────────────────────────┐    ║
║  │   │                                                                    │    ║
║  │   ├── ACTOR TURN (repeat for each actor) ◀───────────────────────┐   │    ║
║  │   │   │                                                           │   │    ║
║  │   │   ├── Movement ─────────── [Hook: move_start, move_complete] │   │    ║
║  │   │   │                                                           │   │    ║
║  │   │   ├── Skill Use ────────── [Hook: skill_used] ◀── KEY HOOK   │   │    ║
║  │   │   │   │                                                       │   │    ║
║  │   │   │   ├── Hit ──────────── [Hook: damage_received]           │   │    ║
║  │   │   │   ├── Miss ─────────── [Hook: attack_missed]             │   │    ║
║  │   │   │   └── Kill ─────────── [Hook: actor_killed]              │   │    ║
║  │   │   │                                                           │   │    ║
║  │   │   └── Turn End ─────────── [Hook: turn_end] ─────────────────┘   │    ║
║  │   │                                                                    │    ║
║  │   └────────────────────────────────────────────────────────────────────┘    ║
║  │                                                                             ║
║  └── ROUND END ─────────────────── [Hook: round_end]                          ║
║                                                                                ║
╚═══════════════════════════════════════════════════════════════════════════════╝
```

### Strategic Cycle (per operation)

```
╔═══════════════════════════════════════════════════════════════════════════════╗
║  OPERATION START                                                               ║
║  │                                                                             ║
║  ├── Roster Management ────────── [Hook: leader_hired, leader_dismissed]      ║
║  │                                                                             ║
║  ├── Black Market ─────────────── [Hook: blackmarket_restocked]               ║
║  │                                                                             ║
║  ├── Faction Diplomacy ────────── [Hook: faction_trust_changed]               ║
║  │                                                                             ║
║  ├── MISSION (repeat) ◀─────────────────────────────────────────────────┐    ║
║  │   │                                                                   │    ║
║  │   ├── → Tactical Cycle →                                             │    ║
║  │   │                                                                   │    ║
║  │   └── Mission End ──────────── [Hook: mission_ended] ────────────────┘    ║
║  │                                                                             ║
║  └── OPERATION END ────────────── [Hook: operation_finished]                  ║
║                                                                                ║
╚═══════════════════════════════════════════════════════════════════════════════╝
```

---

## Approach A: Lego Blocks on Train Tracks

**Metaphor**: Factory game (Factorio, Shapez, Satisfactory)

### Core Concept

The game cycle IS the track. Events flow along it like items on a conveyor belt. You place filter blocks and action blocks at specific stations along the track.

### Visual Representation

```
    ╭─────────────────── ROUND ───────────────────╮
    │                                              │
    ▼                                              │
┌────────┐    ┌────────┐    ┌────────┐            │
│ ROUND  │───▶│ TURN   │───▶│ ACTION │───┐        │
│ START  │    │        │    │        │   │        │
└────────┘    └────────┘    └────────┘   │        │
                                          ▼        │
                               ┌──────────────────┐│
                               │   SKILL USED     ││
                               │   ┌──┐  ┌──┐     ││
                               │   │🎯│  │🔇│     ││  ◀── Place blocks here
                               │   └┬─┘  └┬─┘     ││
                               │    │     │       ││
                               │    ▼     ▼       ││
                               │   ┌───────────┐  ││
                               │   │ CONCEAL-3 │  ││
                               │   └───────────┘  ││
                               └──────────────────┘│
                                          │        │
                                          ▼        │
                               ┌────────┐          │
                               │ ROUND  │──────────┘
                               │  END   │
                               └────────┘
```

### Block Types (from schema)

**Event Stations** (where you attach blocks):
| Station | Data Available |
|---------|----------------|
| Skill Used | actor, skill |
| Damage Received | target, attacker, amount |
| Actor Killed | actor, killer |
| Round Start/End | round_number |
| Turn End | actor |

**Filter Blocks** (one per enum value):
```
From SkillTemplate:
  [🎯 Is Attack]     [🔇 Is Silent]     [🎭 Is Active]
  [📍 Is Targeted]   [🛡️ Ignores Cover] [⚡ Costs 0 AP]

From FactionType:
  [👤 Is Player]     [👥 Is Ally]       [💀 Is Enemy]
  [🏴 Is Pirate]     [🦎 Is Wildlife]

From EntityFlags:
  [😵 Is Stunned]    [🌳 Is Rooted]     [🛡️ Is Immune]

From MoraleState:
  [😰 Is Wavering]   [🏃 Is Fleeing]

From CoverType:
  [🧱 In Heavy]      [🪨 In Medium]     [🌿 In Light]
```

**Action Blocks** (one per property × common values):
```
Concealment Effects:
  [Conceal +3]  [Conceal +2]  [Conceal +1]
  [Conceal -1]  [Conceal -2]  [Conceal -3]

Accuracy Effects:
  [Accuracy +20]  [Accuracy +10]  [Accuracy -10]  [Accuracy -20]

Damage:
  [Damage 5]  [Damage 10]  [Damage 25]  [Damage 50]

Healing:
  [Heal 10]  [Heal 25]  [Heal Full]

Status:
  [Stun 1 Round]  [Root 1 Round]  [Suppress 50]
```

### Estimated Block Count

| Category | Count |
|----------|-------|
| Event Stations | ~12 |
| Skill Filters | ~15 |
| Faction Filters | ~10 |
| State Filters | ~15 |
| Concealment Actions | ~7 |
| Accuracy Actions | ~7 |
| Damage Actions | ~5 |
| Heal Actions | ~4 |
| Status Actions | ~8 |
| Misc Actions | ~10 |
| **Total** | **~93 blocks** |

### Pros
- Self-documenting: the track IS the game cycle
- No configuration: pick the exact block you need
- Approachable: feels like a game, not a programming tool
- Constrained: can't make invalid combinations easily

### Cons
- Many blocks to browse (needs good organization/search)
- Less flexible for edge cases
- Custom values require "custom" blocks or not supported
- More art assets needed for distinct block shapes

---

## Approach B: Playdough with Strings

**Metaphor**: Node graph (Blender, ComfyUI, Unreal Blueprints)

### Core Concept

Fewer, more flexible nodes that you configure via dropdowns and fields. Connect them with wires to define data flow.

### Visual Representation

```
┌─────────────────────┐
│ ⚡ Event: Skill Used │
│                     │
│  actor ●────────────┼─────────────────────────────────┐
│  skill ●────────────┼───────┐                         │
└─────────────────────┘       │                         │
                              ▼                         │
                 ┌─────────────────────┐                │
                 │ ❓ Condition         │                │
                 │                     │                │
                 │  ●─── input         │                │
                 │  Property: [IsAttack ▼]              │
                 │  Operator: [== ▼]   │                │
                 │  Value:    [true]   │                │
                 │                     │                │
                 │       pass ●────────┼───┐            │
                 └─────────────────────┘   │            │
                                           ▼            │
                              ┌─────────────────────┐   │
                              │ ❓ Condition         │   │
                              │                     │   │
                              │  ●─── input         │   │
                              │  Property: [IsSilent ▼] │
                              │  Operator: [== ▼]   │   │
                              │  Value:    [false]  │   │
                              │                     │   │
                              │       pass ●────────┼───┼───┐
                              └─────────────────────┘   │   │
                                                        │   │
                 ┌──────────────────────────────────────┘   │
                 │                                          │
                 ▼                                          ▼
    ┌─────────────────────────────────────────────────────────┐
    │ ✨ Action: Add Effect                                   │
    │                                                         │
    │  ●─── actor                                             │
    │  Property: [Concealment ▼]                              │
    │  Modifier: [-3         ]                                │
    │  Duration: [1 round    ]                                │
    └─────────────────────────────────────────────────────────┘
```

### Node Types (configurable)

**Event Nodes** (one per hook type):
| Node | Outputs | Config |
|------|---------|--------|
| Skill Used | actor, skill | - |
| Damage Received | target, attacker, skill, amount | - |
| Actor Killed | actor, killer, faction | - |
| Round Start/End | round_number | Start/End toggle |
| State Changed | actor, property, old, new | Property dropdown |

**Condition Node** (generic, configurable):
| Input | Config |
|-------|--------|
| any value | Property dropdown |
| | Operator dropdown (==, !=, <, >, contains) |
| | Value field or dropdown |

**Action Nodes** (one per action type):
| Node | Inputs | Config |
|------|--------|--------|
| Add Effect | actor | Property, Modifier, Duration |
| Damage | actor | Amount |
| Heal | actor | Amount |
| Set Flag | actor | Flag dropdown, true/false |
| Change Trust | faction | Delta |
| Add Item | - | Template dropdown |
| Log | - | Message field |

### Estimated Node Count

| Category | Count |
|----------|-------|
| Event Nodes | ~12 |
| Condition Node | 1 (configurable) |
| Logic Nodes (AND/OR/NOT) | ~3 |
| Action Nodes | ~15 |
| Value/Transform Nodes | ~5 |
| **Total** | **~36 nodes** |

### Communicating the Game Cycle

The cycle is NOT visible in the graph itself. Options:

1. **Sidebar Timeline**: Show the cycle as a vertical timeline, highlight which phase each event belongs to
2. **Event Node Colors**: Color-code by phase (blue = round, green = turn, red = combat)
3. **Documentation Panel**: Context-sensitive help showing where in the cycle you are
4. **Grouping**: Auto-group nodes by phase they belong to

### Pros
- Fewer nodes to learn
- Flexible: any value, any comparison
- Familiar to Blender/ComfyUI users
- Easier to extend with new properties

### Cons
- More abstract: cycle not self-evident
- Configuration overhead: dropdowns everywhere
- Easier to make mistakes (wrong property type)
- Steeper learning curve for non-programmers

---

## Hybrid Approach?

Could combine both:

1. **Track view** shows the game cycle with event stations
2. **Click a station** to open a node graph for that specific hook
3. **Pre-built "recipes"** are single blocks (Lego)
4. **Custom logic** opens the full node editor (Playdough)

```
┌─────────────────────────────────────────────────────────────┐
│  TACTICAL CYCLE                                             │
│  ═══════════════                                            │
│                                                             │
│  ┌─────────┐    ┌─────────┐    ┌─────────┐                 │
│  │ ROUND   │───▶│  TURN   │───▶│ SKILL   │                 │
│  │ START   │    │  START  │    │  USED   │ ◀── [+] Add     │
│  │         │    │         │    │ ┌─────┐ │                 │
│  │ (empty) │    │ (empty) │    │ │Mod 1│ │ ◀── Click to   │
│  │         │    │         │    │ │Mod 2│ │     edit        │
│  └─────────┘    └─────────┘    │ └─────┘ │                 │
│                                └─────────┘                 │
│                                     │                       │
│                                     ▼                       │
│                                ┌─────────┐                 │
│                                │ DAMAGE  │                 │
│                                │RECEIVED │                 │
│                                │ (empty) │                 │
│                                └─────────┘                 │
└─────────────────────────────────────────────────────────────┘

Double-click "Mod 1" opens:
┌─────────────────────────────────────────────────────────────┐
│  MOD: Beagle's Concealment                                  │
│  ════════════════════════                                   │
│                                                             │
│  [Skill Used] ──▶ [Is Attack?] ──▶ [Not Silent?] ──▶       │
│       │                                      │              │
│       └──────────────────────────────────────┼──▶ [Conceal] │
│                                              │      -3      │
│                                              └──────────────│
└─────────────────────────────────────────────────────────────┘
```

---

## Schema-Driven Generation

Both approaches can be generated from `schema.json`:

```python
# Lego approach: generate specific blocks
for prop in schema.enums.EntityPropertyType:
    for value in [-3, -2, -1, +1, +2, +3]:
        generate_block(f"{prop} {value:+d}", prop, value)

# Playdough approach: generate dropdown options
dropdown_options = {
    "property": list(schema.enums.EntityPropertyType),
    "faction": list(schema.enums.FactionType),
    "skill_type": list(schema.enums.SkillType),
}
```

---

## Recommendation

Start with **Approach B (Playdough)** using Nodify-Avalonia because:

1. Faster to prototype (fewer assets needed)
2. Nodify already exists and works
3. Can validate the concept before investing in art
4. Schema-driven dropdowns are straightforward

But design with **Approach A (Lego)** as the north star:

1. Use the hybrid track view even if nodes are configurable
2. Build "recipe blocks" as pre-configured node groups
3. Plan for eventual custom block shapes
4. Keep the game cycle visible at all times

This lets us ship something useful quickly while leaving room to evolve toward the more game-like interface.

---

## Next Steps

1. [ ] Prototype track view in Avalonia (just the visualization)
2. [ ] Integrate Nodify-Avalonia for the node editing
3. [ ] Generate dropdown options from schema.json
4. [ ] Build the Beagle's Concealment mod as first test case
5. [ ] User test with actual modders
6. [ ] Iterate based on feedback

---

## Open Questions

1. How do we handle "for each actor" loops? (e.g., apply effect to all enemies)
2. Should mods be able to define new blocks/nodes?
3. How do we version mods when the game updates?
4. Where do mods live? (files, database, embedded in save?)
5. Can mods conflict? How do we handle load order?
