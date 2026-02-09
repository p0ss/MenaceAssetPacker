# Reverse Engineering Coverage Plan

## Overview

This document tracks systematic documentation of Menace's game systems. Each system will be analyzed via Ghidra decompilation and documented with:
- Class/struct layouts with field offsets
- Key method signatures and logic
- Call chains and system interactions
- Modding hooks and Harmony patch points

## Current Status

### Documented Systems (36)
- [x] Combat - Hit Chance
- [x] Combat - Damage
- [x] Combat - Cover
- [x] Combat - Armor
- [x] Suppression & Morale
- [x] Skills & Effects
- [x] Entity Properties
- [x] Weapon Stats
- [x] AI Decision Making
- [x] AI Roles & Weights
- [x] Template Loading
- [x] Asset References
- [x] Save/Load System
- [x] Stat System & Multipliers
- [x] UI System
- [x] Offset Stability
- [x] Terrain Generation
- [x] Tile & Map System
- [x] Pathfinding System
- [x] Actor System
- [x] Turn & Action System
- [x] Mission System
- [x] Operation System
- [x] Army Generation
- [x] Roster & Unit Management
- [x] Item System
- [x] Vehicle System
- [x] Conversation System
- [x] Event System
- [x] Emotional State System
- [x] BlackMarket

---

## Phase 1: Core Tactical Systems

### 1.1 Tile & Map System
**Status:** ✅ Complete
**Output:** `tile-map-system.md`

**Key Classes:**
- `Menace.Tactical.Tile` - Runtime tile instance
- `Menace.Tactical.BaseTile` - Base tile functionality
- `Menace.Tactical.Map` - Map container
- `Menace.Tactical.BaseMap<T>` - Generic map base

**Investigation Points:**
- [ ] Tile field layout (position, elevation, blocked, surface type)
- [ ] Cover system (half-cover directions, cover values)
- [ ] Tile flags and states
- [ ] Map dimensions and coordinate system
- [ ] Tile adjacency and neighbor access
- [ ] Surface types enum
- [ ] Blocked tile logic

**Key Methods to Decompile:**
- `BaseTile.CalculateSurroundingCover`
- `BaseTile.SetCover` / `SetHalfCover`
- `BaseTile.HasCover` / `HasHalfCover`
- `Map.GetTile`
- `Map.IsValidTile`

---

### 1.2 Pathfinding System
**Status:** ✅ Complete
**Output:** `pathfinding-system.md`

**Key Classes:**
- `Menace.Tactical.PathfindingManager` - Manager/pool
- `Menace.Tactical.PathfindingProcess` - Actual pathfinding
- `Menace.Tactical.PathfindingNode` - A* node
- `Menace.Tactical.Pathfinding.Modifiers.*` - Path modifiers

**Investigation Points:**
- [ ] PathfindingProcess field layout
- [ ] Movement cost calculation
- [ ] Blocked/traversable tile logic
- [ ] Path modifiers (funnel, spline, simplify)
- [ ] Allied unit collision
- [ ] Structure traversal

**Key Methods to Decompile:**
- `PathfindingProcess.FindPath`
- `PathfindingProcess.ProcessNode`
- `PathfindingProcess.IsTraversable`
- `PathfindingNode` comparison operators

---

### 1.3 Actor System (Full)
**Status:** ✅ Complete
**Output:** `actor-system.md`

**Key Classes:**
- `Menace.Tactical.Actor` - Main actor class
- `Menace.Tactical.Element` - Visual/model component
- `Menace.Tactical.Entity` - Base entity

**Investigation Points:**
- [ ] Actor field layout (beyond EntityProperties)
- [ ] Movement state and animation
- [ ] Aiming system
- [ ] Death and destruction handling
- [ ] Vehicle entry/exit
- [ ] Element management (multi-model units)

**Key Methods to Decompile:**
- `Actor.AimAt`
- `Actor.ApplyMorale` / `ApplySuppression`
- `Actor.OnDamageReceived`
- `Actor.CalculateTilesInMovementRange`
- `Element.UpdateDamageShader`

---

### 1.4 Turn & Action System
**Status:** ✅ Complete
**Output:** `turn-action-system.md`

**Key Classes:**
- `Menace.States.TacticalState` - Main tactical state machine
- `Menace.States.*Action` - Action classes (SkillAction, NoneAction, etc.)
- `Menace.Tactical.AI.BaseFaction` - Faction turn management

**Investigation Points:**
- [ ] TacticalState field layout
- [ ] Turn flow (start → actions → end)
- [ ] Action state machine
- [ ] Skill selection and execution
- [ ] Active actor management
- [ ] Round vs Turn distinction

**Key Methods to Decompile:**
- `TacticalState.EndTurn`
- `TacticalState.OnTurnStart`
- `TacticalState.TrySelectSkill`
- `SkillAction.HandleLeftClickOnTile`
- `BaseFaction.GetAmountOfActorsLeftToAct`

---

## Phase 2: Campaign/Strategy Layer

### 2.1 Mission System
**Status:** ✅ Complete
**Output:** `mission-system.md`

**Key Classes:**
- `Menace.Strategy.Mission` - Runtime mission
- `Menace.Strategy.MissionTemplate` - Mission definition
- `Menace.Strategy.Missions.MissionActorConfig` - Spawn config

**Investigation Points:**
- [x] Mission field layout
- [x] Mission generation from template
- [x] Army assignment
- [x] Objective system
- [x] Mission status flow
- [x] Rewards and consequences

**Key Methods to Decompile:**
- `Mission.Init`
- `Mission.Start`
- `Mission.ProcessSaveState`
- `Mission.GetArmy`

---

### 2.2 Operation System
**Status:** ✅ Complete
**Output:** `operation-system.md`

**Key Classes:**
- `Menace.Strategy.Operation` - Runtime operation
- `Menace.Strategy.OperationTemplate` - Operation definition
- `Menace.Strategy.OperationsManager` - Manager

**Investigation Points:**
- [x] Operation field layout
- [x] Operation flow (start → missions → end)
- [x] Time advancement
- [x] Operation properties calculation
- [x] Faction trust changes
- [x] Auto-save triggers

**Key Methods to Decompile:**
- `Operation.StartOperation`
- `Operation.EndOperation`
- `Operation.AdvanceTime`
- `Operation.CalculateOperationProperties`

---

### 2.3 Army Generation
**Status:** ✅ Complete
**Output:** `army-generation.md`

**Key Classes:**
- `Menace.Strategy.Army` - Generated army
- `Menace.Strategy.ArmyTemplate` - Army definition
- `Menace.Strategy.ArmyEntry` - Unit in army
- `Menace.Strategy.ArmyTemplateEntry` - Possible unit

**Investigation Points:**
- [x] Army field layout
- [x] Budget-based generation algorithm
- [x] Unit weighting and selection
- [x] Campaign progress scaling
- [x] Spawn area assignment

**Key Methods to Decompile:**
- `Army.CreateArmy`
- `Army.GetRandomArmy`
- `ArmyTemplateEntry` weight calculation

---

### 2.4 Roster & Unit Management
**Status:** ✅ Complete
**Output:** `roster-system.md`

**Key Classes:**
- `Menace.Strategy.Roster` - Player roster
- `Menace.Strategy.BaseUnitLeader` - Unit leader base
- `Menace.Strategy.Squaddie` - Individual squaddie

**Investigation Points:**
- [x] Roster field layout
- [x] Unit leader management
- [x] Squaddie assignment
- [x] Perk system
- [x] Health/injury status
- [x] Deployment costs

**Key Methods to Decompile:**
- `BaseUnitLeader.ProcessSaveState`
- `BaseUnitLeader.OnMissionFinished`
- `BaseUnitLeader.GetDeployCosts`
- `BaseUnitLeader.AddPerk`

---

## Phase 3: Items & Equipment

### 3.1 Item System
**Status:** ✅ Complete
**Output:** `item-system.md`

**Key Classes:**
- `Menace.Items.Item` - Runtime item
- `Menace.Items.BaseItemTemplate` - Item definition base
- `Menace.Items.ItemTemplate` - Standard item
- `Menace.Items.ItemContainer` - Inventory container

**Investigation Points:**
- [ ] Item field layout
- [ ] Item creation from template
- [ ] Container management
- [ ] Skill addition/removal
- [ ] Tag system
- [ ] Trade values

**Key Methods to Decompile:**
- `Item.AddSkills` / `RemoveSkills`
- `ItemContainer.Add` / `Remove`
- `BaseItemTemplate.CreateItem`

---

### 3.2 Vehicle System
**Status:** ✅ Complete
**Output:** `vehicle-system.md`

**Key Classes:**
- `Menace.Strategy.Vehicle` - Vehicle instance
- `Menace.Strategy.ItemsModularVehicle` - Modular slots
- `Menace.Items.VehicleItemTemplate` - Vehicle equipment

**Investigation Points:**
- [ ] Vehicle field layout
- [ ] Modular slot system
- [ ] Equipment restrictions
- [ ] Twin-fire detection
- [ ] Hitpoint management

**Key Methods to Decompile:**
- `ItemsModularVehicle.Add`
- `ItemsModularVehicle.GetFreeSlot`
- `VehicleItemTemplate.OnEquip` / `OnUnequip`

---

## Phase 4: Dialogue & Events

### 4.1 Conversation System
**Status:** ✅ Complete
**Output:** `conversation-system.md`

**Key Classes:**
- `Menace.Conversations.BaseConversationManager` - Manager
- `Menace.Conversations.BaseConversationNode` - Node base
- `Menace.Conversations.ActionConversationNode` - Action node
- `Menace.Conversations.*Requirement` - Role requirements

**Investigation Points:**
- [ ] Conversation node structure
- [ ] Speaker assignment
- [ ] Role requirements system
- [ ] Conversation actions
- [ ] Trigger types
- [ ] Repetition tracking

**Key Methods to Decompile:**
- `BaseConversationManager.TryFindSpeakersForConversation`
- `BaseConversationNode.Execute`
- `ActionConversationNode.Execute`

---

### 4.2 Event System
**Status:** ✅ Complete
**Output:** `event-system.md`

**Key Classes:**
- `Menace.Strategy.EventManager` - Event manager
- Various event/trigger classes

**Investigation Points:**
- [ ] Event types and triggers
- [ ] Mission select events
- [ ] Operation events
- [ ] Mandatory vs optional events

**Key Methods to Decompile:**
- `EventManager.GenerateMissionSelectEvents`
- `EventManager.OnOperationFinished`

---

### 4.3 Emotional State System
**Status:** ✅ Complete
**Output:** `emotional-system.md`

**Key Classes:**
- `Menace.Strategy.EmotionalState` - Single emotion
- `Menace.Strategy.EmotionalStates` - Collection
- `Menace.Strategy.EmotionalTriggerExtensions` - Triggers

**Investigation Points:**
- [ ] Emotion types
- [ ] Trigger conditions
- [ ] Skill modifications from emotions
- [ ] Mission participation effects

**Key Methods to Decompile:**
- `EmotionalState.AddSkill` / `RemoveSkill`
- `EmotionalStates.OnMissionFinished`

---

## Phase 5: Supporting Systems

### 5.1 Tile Effects
**Status:** ✅ Complete
**Output:** `tile-effects.md`

**Key Classes:**
- `Menace.Tactical.TileEffects.TileEffectHandler` - Base handler
- `Menace.Tactical.TileEffects.TileEffectTemplate` - Base template
- ApplySkill, ApplyStatusEffect, BleedOut, RecoverableObject, RefillAmmo handlers

**Investigation Points:**
- [x] TileEffectHandler base structure (+0x10 Tile, +0x18 RoundsElapsed, +0x1C Delay, +0x20 Visual)
- [x] TileEffectTemplate structure (+0x78 Title, +0x80 Desc, +0x8C HasDuration, +0x90 Duration, +0x98 Prefab, +0xAC BlocksLOS)
- [x] Handler lifecycle (AssignToTile, RemoveFromTile, OnEnter, OnLeave, OnRoundStart)
- [x] Specific handlers: BleedOut mechanics, RecoverableObject pickup, RefillAmmo

---

### 5.2 Line of Sight
**Status:** ✅ Complete
**Output:** `line-of-sight.md`

**Key Classes:**
- `Tactical.LineOfSight` - Core ray-tracing algorithm
- `Menace.Tactical.Map` - Visibility management, fog of war
- `Menace.Tactical.Actor` - Detection/concealment checks

**Investigation Points:**
- [x] LOS ray-tracing algorithm (HasLineOfSight with tile stepping)
- [x] Tile visibility mask at +0x58 (bit per faction)
- [x] Blocking at +0x60, blocker flag at +0x1C bit 11
- [x] Vision/Detection stats (+0xC4/+0xCC in EntityProperties)
- [x] Fog of war toggle at Map +0x38
- [x] Cover concealment values

---

### 5.3 Offmap Abilities
**Status:** ✅ Complete
**Output:** `offmap-abilities.md`

**Key Classes:**
- `Menace.OffmapAbilities.DelayedOffmapAbility` - Scheduled ability instance
- `Menace.OffmapAbilities.DelayedOffmapAbilities` - Manager
- `Menace.OffmapAbilities.OffmapAbilityTemplate` - Ability definition

**Investigation Points:**
- [x] Delay system (rounds + turn progress timing)
- [x] DelayedOffmapAbility layout (+0x10 Template, +0x18 Skill, +0x20 Target, +0x38 RoundScheduled)
- [x] OffmapAbilityTemplate layout (+0x78 Skill, +0x80 DelayRounds, +0x84 UseSound)
- [x] Schedule/OnTurnEnd execution flow

---

### 5.4 BlackMarket
**Status:** ✅ Complete
**Output:** `blackmarket.md`

**Key Classes:**
- `Menace.Strategy.BlackMarket`
- `Menace.Strategy.BlackMarket.BlackMarketItemStack`

**Investigation Points:**
- [x] BlackMarket field layout (+0x10 List<BlackMarketItemStack>)
- [x] BlackMarketItemStack layout (+0x10 Template, +0x18 OperationsRemaining, +0x20 Items, +0x28 Type)
- [x] FillUp generation algorithm (progress-based filtering, duplicate prevention)
- [x] OnOperationFinished timeout decrement and refill
- [x] ProcessSaveState serialization order
- [x] StrategyConfig integration (+0x198 items, +0x1A0 min, +0x1A4 max, +0x1A8 timeout)

---

## Execution Log

| Date | System | Status | Notes |
|------|--------|--------|-------|
| 2026-02-10 | Tile & Map System | ✅ Complete | BaseTile, Tile, Map classes documented |
| 2026-02-10 | Pathfinding System | ✅ Complete | A* algorithm, PathfindingProcess, PathfindingNode documented |
| 2026-02-10 | Actor System | ✅ Complete | Entity, Actor, UnitActor, Structure, Element classes documented |
| 2026-02-10 | Turn & Action System | ✅ Complete | TacticalManager, TacticalState, Action classes documented |
| 2026-02-10 | Mission System | ✅ Complete | Mission field layout, Init/Start flow, save state, army management |
| 2026-02-10 | Operation System | ✅ Complete | Operation lifecycle, faction trust, strategic assets, time management |
| 2026-02-10 | Army Generation | ✅ Complete | Budget-based selection, weighted random, progress filtering |
| 2026-02-10 | Roster & Unit Management | ✅ Complete | Roster, BaseUnitLeader, Squaddie, perks, deployment costs |
| 2026-02-10 | Item System | ✅ Complete | Item, ItemContainer, BaseItemTemplate, 11 slot types documented |
| 2026-02-10 | Vehicle System | ✅ Complete | Vehicle, ItemsModularVehicle, twin-fire detection documented |
| 2026-02-10 | Conversation System | ✅ Complete | BaseConversationManager, Role, requirements system, node types |
| 2026-02-10 | Event System | ✅ Complete | EventManager, ConversationInstance, EventData, event lifecycle |
| 2026-02-10 | Emotional State System | ✅ Complete | EmotionalStates, EmotionalState, triggers, skill modifiers |
| 2026-02-10 | Tile Effects | ✅ Complete | TileEffectHandler, 6 handler types, lifecycle callbacks, template layouts |
| 2026-02-10 | Line of Sight | ✅ Complete | Ray-tracing algorithm, visibility masks, fog of war, detection/concealment |
| 2026-02-10 | Offmap Abilities | ✅ Complete | DelayedOffmapAbility/Abilities, round+turn timing, OffmapAbilityTemplate |
| 2026-02-10 | BlackMarket | ✅ Complete | BlackMarket, BlackMarketItemStack, operation-based timeouts, FillUp generation |

---

## Notes

- Priority systems for modding: Tile/Map, Pathfinding, Turn/Action, Army Generation
- Some systems may merge if they're tightly coupled
- Field offsets should be verified against dump.cs where possible
