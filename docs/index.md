# Menace Modkit Documentation

Welcome to the Menace Modkit documentation. Choose a section based on what you're trying to do.

## For Modders

### [Modding Guides](modding-guides/index.md)

Start here if you want to make mods. Step-by-step tutorials from beginner to advanced:

**Tier 1 - Data Patches:**
- [Baby's First Mod](modding-guides/01-first-mod.md)
- [Stat Adjustments](modding-guides/02-stat-changes.md)
- [Template Cloning](modding-guides/03-template-cloning.md)

**Tier 2 - Asset Replacement:**
- [Textures & Icons](modding-guides/04-textures-icons.md)
- [3D Models](modding-guides/05-3d-models.md)
- [Audio](modding-guides/06-audio.md)

**Tier 3 - SDK Coding:**
- [SDK Getting Started](coding-sdk/getting-started.md)
- [What Is the SDK?](coding-sdk/what-is-sdk.md)
- [SDK API Reference](coding-sdk/index.md)

**Tier 4 - Advanced:**
- [Advanced Code & Security](modding-guides/10-advanced-code.md)

### [Coding SDK](coding-sdk/index.md)

Comprehensive API reference for the Menace SDK. 28 documented APIs across 9 tiers:

Start here for code mods:
- [What Is the SDK?](coding-sdk/what-is-sdk.md)
- [Getting Started: Your First Plugin](coding-sdk/getting-started.md)

- **Core**: GameType, GameObj, GameQuery, Templates, GameState
- **Tactical**: EntitySpawner, EntityMovement, EntityCombat, TacticalController
- **Map**: TileMap, Pathfinding, LineOfSight, TileEffects
- **Strategy**: Mission, Operation, Roster, Inventory, ArmyGeneration, Vehicle, BlackMarket
- **Social**: Conversation, Emotions
- **Tools**: DevConsole, ModSettings, ModError, REPL

All systems have console commands accessible via `~` key.

**SDK Guides:**
- [Debugging Guide](coding-sdk/guides/debugging-guide.md) - Troubleshooting mods
- [Compilation Troubleshooting](coding-sdk/guides/compilation-troubleshooting.md) - Fix build and reference issues
- [Patching Guide](coding-sdk/guides/patching-guide.md) - Harmony patching patterns
- [Template Modding](coding-sdk/guides/template-modding.md) - Working with game templates
- [Migration from Raw IL2CPP](coding-sdk/guides/migration-from-raw-il2cpp.md) - Upgrading legacy mods

**Roadmap:**
- [Advanced Features](coding-sdk/roadmap/advanced.md) - Planned SDK enhancements
- [Hot Reload](coding-sdk/roadmap/hot-reload.md) - Future live reloading support

## For Contributors

### [System Guide](system-guide/architecture.md)

Technical documentation for Modkit development:

- [Architecture Overview](system-guide/architecture.md)
- [Release Workflow](system-guide/RELEASE_WORKFLOW.md)
- [Component Setup](system-guide/COMPONENT_SETUP.md)
- [Reference Resolution](system-guide/REFERENCE_RESOLUTION.md)
- [Asset Reference System](system-guide/ASSET_REFERENCE_SYSTEM.md)
- [Modpack Loader Implementation](system-guide/MODPACK_LOADER_IMPLEMENTATION.md)
- [Extraction Orchestration](system-guide/EXTRACTION_ORCHESTRATION.md)
- [Template Modding Workflow](system-guide/TEMPLATE_MODDING_WORKFLOW.md)
- [Testing Guide](system-guide/TESTING.md)
- [Third Party Notices](system-guide/THIRD_PARTY_NOTICES.md)

### [Reverse Engineering](reverse-engineering/README.md)

Comprehensive notes on Menace game internals (45+ documented systems):

- Combat: Hit chance, damage, armor, suppression, morale
- Entities: Actor system, properties, skills, AI decisions
- Map: Tiles, pathfinding, LOS, terrain generation, tile effects
- Campaign: Missions, operations, roster, army generation
- Economy: Items, vehicles, black market
- Social: Conversations, events, emotions
- Infrastructure: Save system, templates, UI, localization

---

## AI Assistant

### [AI Assistant Setup](system-guide/AI_ASSISTANT_SETUP.md)

Connect an AI assistant to help with modding. The AI can query game state, explore templates, create mods, and debug issues. Supports:

- **OpenCode** — Works with Ollama (free, local) or cloud APIs
- **Claude Code/Desktop** — Best quality, requires Anthropic subscription

---

## Quick Links

| I want to... | Go to... |
|--------------|----------|
| Set up AI assistant | [AI Assistant Setup](system-guide/AI_ASSISTANT_SETUP.md) |
| Make my first mod | [Baby's First Mod](modding-guides/01-first-mod.md) |
| Change unit stats | [Stat Adjustments](modding-guides/02-stat-changes.md) |
| Create unit variants | [Template Cloning](modding-guides/03-template-cloning.md) |
| Replace textures | [Textures & Icons](modding-guides/04-textures-icons.md) |
| Replace sounds | [Audio](modding-guides/06-audio.md) |
| Write code mods | [SDK Getting Started](coding-sdk/getting-started.md) |
| Add custom UI | [DevConsole API](coding-sdk/api/dev-console.md) |
| Use Harmony patches | [Patching Guide](coding-sdk/guides/patching-guide.md) |
| Look up an API | [Coding SDK](coding-sdk/index.md) |
| Debug my mod | [Debugging Guide](coding-sdk/guides/debugging-guide.md) |
| Understand security | [Advanced Code](modding-guides/10-advanced-code.md) |
| Understand game internals | [Reverse Engineering](reverse-engineering/README.md) |
