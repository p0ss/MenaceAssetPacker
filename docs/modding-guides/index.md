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

- **Baby's First Mod** - Change one number, see it in-game
- **Stat Adjustments** - Balance tweaks, unit modifications
- **Template Cloning** - Create variants of existing units/weapons

### Tier 2: Asset Replacement

- **Textures & Icons** - Replace 2D images
- **3D Models** - Replace meshes (requires external tools)
- **Audio** - Replace sound effects and music

### Tier 3: SDK Coding

- **SDK Basics** - Query game state, react to events
- **Template Modding** - Programmatically modify game data
- **UI Modifications** - Add custom interface elements

### Tier 4: Advanced Code

- **Custom DLLs** - Full C# programming with Harmony patches
- **Security Considerations** - Why source-verified mods matter

## Getting Started

1. **Install the Modkit** - Download and run the Menace Modkit application
2. **Set Game Path** - Point it to your Menace installation
3. **Install Mod Loader** - Click "Install" in the Mod Loader section
4. **Create Your First Mod** - Follow the Baby's First Mod guide

Ready? Let's make your first mod!

---

**Next:** [Baby's First Mod](01-first-mod.md)
