# Modding Menace

Welcome to the Menace Modkit! This guide will take you from complete beginner to capable modder, covering everything from simple stat tweaks to advanced code modifications.

## What Can You Mod?

Menace is highly moddable. Here's what you can change:

- **Game Balance** - Adjust unit stats, weapon damage, costs, movement speeds
- **Visuals** - Replace textures, icons, UI elements
- **3D Models** - Swap character models, weapons, props
- **Game Logic** - Add new features, change AI behavior, create new mechanics

## How Modding Works

The Menace Modkit uses a **modpack** system. A modpack is a folder containing:

```
MyMod-modpack/
  modpack.json      <- Manifest describing your mod
  patches/          <- Data changes (stats, balance)
  assets/           <- Replacement textures, models
  src/              <- Source code (optional)
  dlls/             <- Compiled code (optional)
```

The **Modpack Loader** runs inside the game and applies your changes at runtime. No game files are permanently modified - disable the mod and the game returns to vanilla.

## Modding Tiers

This guide is organized by complexity:

### Tier 1: Data Patches (No Code Required)

- [Baby's First Mod](01-first-mod.md) - Change one number, see it in-game
- [Stat Adjustments](02-stat-changes.md) - Balance tweaks, unit modifications
- [Template Cloning](03-template-cloning.md) - Create variants of existing units/weapons

### Tier 2: Asset Replacement

- [Textures & Icons](04-textures-icons.md) - Replace 2D images
- [3D Models](05-3d-models.md) - Replace meshes (requires external tools)
- [Audio](06-audio.md) - Replace sound effects and music

### Tier 3: SDK Coding

- [SDK Getting Started](../coding-sdk/getting-started.md) - Canonical entry point for current plugin lifecycle and setup
- [What Is the SDK?](../coding-sdk/what-is-sdk.md) - Runtime model, scope, and when to use SDK vs patches
- [SDK API Reference](../coding-sdk/index.md) - Full API surface across all tiers

### Tier 4: Advanced Code

- [Advanced Code & Security](10-advanced-code.md) - DLLs, Harmony, and security considerations

## Legacy SDK Guides

These pages are kept for historical context and examples, but some snippets use older API names:

- [SDK Basics](07-sdk-basics.md)
- [Template Modding](08-template-modding.md)
- [UI Modifications](09-ui-modifications.md)
- [Advanced Code & Security](10-advanced-code.md)

## Getting Started

1. **Install the Modkit** - Download and run the Menace Modkit application
2. **Set Game Path** - Point it to your Menace installation
3. **Install Mod Loader** - Click "Install" in the Mod Loader section
4. **Create Your First Mod** - Follow the Baby's First Mod guide

Ready? Let's make your first mod!

---

**Next:** [Baby's First Mod](01-first-mod.md)
