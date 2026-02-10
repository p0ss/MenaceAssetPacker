# Menace Reference Code

This directory contains **reconstructed C# reference implementations** of Menace game systems, derived from reverse engineering analysis.

## Purpose

These files help modders:
- **Understand game mechanics** - See exactly how calculations work
- **Plan Harmony patches** - Know which methods to target and their signatures
- **Copy/adapt logic** - Use as starting points for custom implementations
- **Learn the architecture** - Understand how systems interconnect

## Important Disclaimers

1. **Not Original Code** - This is reconstructed from Ghidra decompilation, not leaked source
2. **May Have Errors** - While carefully verified, some details may be inaccurate
3. **May Become Outdated** - Game updates may change implementations
4. **Educational Only** - For modding reference, not redistribution

## Structure

```
reference-code/
├── Combat/
│   ├── EntityProperties.cs      # Central stat container
│   ├── FloatExtensions.cs       # Multiplier math utilities
│   ├── HitChanceCalculation.cs  # Hit chance formula
│   ├── DamageCalculation.cs     # Damage & armor resolution
│   └── SuppressionMorale.cs     # Psychological warfare system
├── AI/
│   ├── AgentSystem.cs           # AI evaluation loop
│   ├── RoleData.cs              # Per-unit AI configuration
│   └── Behaviors.cs             # Behaviors & Criterions
├── Skills/
│   ├── SkillEffects.cs          # Effect types & application
│   └── SkillExecution.cs        # Skill usage flow & hit resolution
├── Templates/
│   ├── DataTemplateLoader.cs    # Template loading & caching
│   └── CommonTemplates.cs       # Entity, Skill, Weapon, Armor templates
└── SaveLoad/
    ├── SaveSystem.cs            # Save/load manager & state structures
    └── SaveProcessors.cs        # Per-system save/load handlers
```

## How To Use

### Understanding a System

Each file contains:
- XML documentation with explanations
- Memory addresses for Ghidra cross-reference
- Offset annotations for struct fields
- Comments explaining non-obvious behavior

### For Harmony Patching

The method signatures match the game's IL2CPP exports:

```csharp
// To patch GetHitchance, use:
[HarmonyPatch(typeof(Menace.Tactical.Skills.Skill), "GetHitchance")]
class HitChancePatch
{
    static void Postfix(ref HitChanceResult __result)
    {
        // Modify hit chance after calculation
        __result.FinalHitChance *= 1.5f; // +50% hit chance
    }
}
```

### For Understanding Multipliers

The game uses **additive multiplier stacking**:

```csharp
// WRONG assumption (multiplicative):
// 1.0 * 1.2 * 1.3 = 1.56 (56% bonus)

// CORRECT (additive, how Menace works):
// 1.0 + (1.2 - 1.0) + (1.3 - 1.0) = 1.5 (50% bonus)

float result = 1.0f.AddMult(1.2f).AddMult(1.3f);  // = 1.5f
```

### For Modifying Skills

Skills flow through several stages you can intercept:

```csharp
// 1. Modify skill costs
[HarmonyPatch(typeof(Menace.Tactical.Skills.Skill), "CanUse")]
class ReduceAPCostPatch
{
    static void Prefix(Skill __instance)
    {
        // Make all skills cost 1 less AP
        __instance.Template.Costs.ActionPoints =
            Math.Max(0, __instance.Template.Costs.ActionPoints - 1);
    }
}

// 2. Modify effect application
[HarmonyPatch(typeof(Menace.Tactical.Skills.DamageEffect), "Apply")]
class DoubleDamagePatch
{
    static void Prefix(DamageEffect __instance)
    {
        __instance.BaseDamage *= 2f;  // Double all damage
    }
}

// 3. Add custom effect types
// Create new SkillEffect subclass with custom Apply() logic
```

### For AI Modding

AI behavior is controlled by RoleData on EntityTemplate:

```csharp
// Make an entity more aggressive
[HarmonyPatch(typeof(EntityTemplate), "get_AIRole")]
class AggressiveAIPatch
{
    static void Postfix(ref RoleData __result)
    {
        __result.UtilityScale = 40f;  // Prioritize offense
        __result.SafetyScale = 5f;    // Ignore danger
        __result.PeekInAndOutOfCover = true;
    }
}
```

### For Save File Modding

Intercept save/load operations to add custom data:

```csharp
// Add custom data to saves
[HarmonyPatch(typeof(SaveManager), "SaveGame")]
class CustomSaveDataPatch
{
    static void Prefix(SaveState __state)
    {
        // Add mod data to save
        MyMod.SaveModData(__state);
    }
}

// Migrate old templates in saves
[HarmonyPatch(typeof(TemplateRedirector), "GetRedirect")]
class TemplateMigrationPatch
{
    static void Postfix(string originalName, ref string __result)
    {
        // Redirect removed template to new one
        if (originalName == "OldWeapon_v1")
            __result = "MyMod_NewWeapon";
    }
}
```

## Coverage Status

| System | Status | Files |
|--------|--------|-------|
| Hit Chance | ✅ Complete | EntityProperties, HitChance, FloatExtensions |
| Damage | ✅ Complete | DamageCalculation, EntityProperties |
| Cover | ✅ Complete | HitChanceCalculation |
| Armor | ✅ Complete | DamageCalculation (armor resolution) |
| Suppression | ✅ Complete | SuppressionMorale |
| Morale | ✅ Complete | SuppressionMorale |
| AI Agent System | ✅ Complete | AgentSystem, RoleData, Behaviors |
| AI Criterions | ✅ Complete | Behaviors (criterion classes) |
| AI Behaviors | ✅ Complete | Behaviors (behavior classes) |
| Skill Effects | ✅ Complete | SkillEffects (effect types, application) |
| Skill Execution | ✅ Complete | SkillExecution (targeting, costs, flow) |
| Templates | ✅ Complete | DataTemplateLoader, CommonTemplates |
| Save/Load | ✅ Complete | SaveSystem, SaveProcessors |

## Verification

Each reconstructed method has been verified by:
1. Comparing Ghidra decompilation to reconstructed C#
2. Cross-referencing with dump.cs signatures
3. Testing documented formulas against game behavior

## Contributing

If you find errors or want to add systems:
1. Decompile the relevant function in Ghidra
2. Reconstruct as clean C# with documentation
3. Include memory addresses for verification
4. Test against actual game behavior if possible

## Related Documentation

- [Reverse Engineering Notes](../reverse-engineering/README.md) - Detailed analysis docs
- [IL2CPP Runtime Reference](../il2cpp-runtime.md) - Runtime function names
- [Offset Reference](../reverse-engineering/offsets.md) - Consolidated struct offsets
