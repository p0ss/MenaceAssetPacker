# EntityCombat

`Menace.SDK.EntityCombat` -- Static class for entity combat actions including attacks, abilities, and status effects.

## Constants

### Suppression Thresholds

```csharp
public const float SUPPRESSION_THRESHOLD = 0.33f;  // 33% = Suppressed
public const float PINNED_THRESHOLD = 0.66f;       // 66% = Pinned
public const float MAX_SUPPRESSION = 100.0f;
```

## Methods

### Attack

```csharp
public static CombatResult Attack(GameObj attacker, GameObj target)
```

Have an actor attack a target using their primary weapon/skill.

**Returns:** `CombatResult` with success status.

### UseAbility

```csharp
public static CombatResult UseAbility(GameObj actor, string skillName, GameObj target = default)
```

Use a specific ability/skill on a target.

**Parameters:**
- `actor` - The actor using the ability
- `skillName` - Name of the skill to use
- `target` - Optional target (some skills don't require a target)

### GetSkills

```csharp
public static List<SkillInfo> GetSkills(GameObj actor)
```

Get all abilities/skills for an actor.

### CanUseAbility

```csharp
public static bool CanUseAbility(GameObj actor, string skillName)
```

Check if an actor can use a specific skill.

### GetAttackRange

```csharp
public static int GetAttackRange(GameObj actor)
```

Get the attack range for an actor's primary weapon.

### ApplySuppression / SetSuppression / GetSuppression

```csharp
public static bool ApplySuppression(GameObj actor, float amount)
public static bool SetSuppression(GameObj actor, float value)
public static float GetSuppression(GameObj actor)
```

Manage suppression for an actor. Values are clamped to [0, MAX_SUPPRESSION].

### SetMorale / GetMorale

```csharp
public static bool SetMorale(GameObj actor, float value)
public static float GetMorale(GameObj actor)
```

Manage morale for an actor.

### ApplyDamage / Heal

```csharp
public static bool ApplyDamage(GameObj entity, int damage)
public static bool Heal(GameObj entity, int amount)
```

Apply damage or healing to an entity. HP is clamped to valid range.

### SetTurnDone

```csharp
public static bool SetTurnDone(GameObj actor, bool done)
```

Set whether an actor's turn is done.

### SetStunned

```csharp
public static bool SetStunned(GameObj actor, bool stunned)
```

Stun or unstun an actor.

### GetCombatInfo

```csharp
public static CombatInfo GetCombatInfo(GameObj actor)
```

Get comprehensive combat status for an actor.

## Types

### CombatResult

```csharp
public class CombatResult
{
    public bool Success { get; set; }
    public string Error { get; set; }
    public int Damage { get; set; }
    public string SkillUsed { get; set; }
}
```

### SkillInfo

```csharp
public class SkillInfo
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public bool CanUse { get; set; }
    public int APCost { get; set; }
    public int Range { get; set; }
    public int Cooldown { get; set; }
    public int CurrentCooldown { get; set; }
    public bool IsAttack { get; set; }
    public bool IsPassive { get; set; }
}
```

### CombatInfo

```csharp
public class CombatInfo
{
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public float HPPercent { get; }
    public bool IsAlive { get; set; }
    public float Suppression { get; set; }
    public string SuppressionState { get; set; }  // "None", "Suppressed", "Pinned"
    public float Morale { get; set; }
    public bool IsTurnDone { get; set; }
    public bool IsStunned { get; set; }
    public bool HasActed { get; set; }
    public int TimesAttackedThisTurn { get; set; }
    public int CurrentAP { get; set; }
}
```

## Examples

### Attacking a target

```csharp
var attacker = TacticalController.GetActiveActor();
var enemies = EntitySpawner.ListEntities(factionFilter: 1);
if (enemies.Length > 0)
{
    var result = EntityCombat.Attack(attacker, enemies[0]);
    if (result.Success)
        DevConsole.Log($"Attack successful with {result.SkillUsed}");
    else
        DevConsole.Log($"Attack failed: {result.Error}");
}
```

### Using a specific ability

```csharp
var actor = TacticalController.GetActiveActor();
var result = EntityCombat.UseAbility(actor, "Grenade", target);
```

### Listing skills

```csharp
var actor = TacticalController.GetActiveActor();
var skills = EntityCombat.GetSkills(actor);
foreach (var skill in skills)
{
    var status = skill.CanUse ? "ready" : "unavailable";
    DevConsole.Log($"  {skill.Name} (AP: {skill.APCost}, Range: {skill.Range}) - {status}");
}
```

### Managing suppression

```csharp
var actor = TacticalController.GetActiveActor();

// Apply suppression
EntityCombat.ApplySuppression(actor, 25f);

// Clear all suppression
EntityCombat.SetSuppression(actor, 0f);

// Check suppression state
var info = EntityCombat.GetCombatInfo(actor);
DevConsole.Log($"Suppression: {info.Suppression}% ({info.SuppressionState})");
```

### Healing and damage

```csharp
var actor = TacticalController.GetActiveActor();

// Apply damage
EntityCombat.ApplyDamage(actor, 50);

// Heal
EntityCombat.Heal(actor, 25);

// Check HP
var info = EntityCombat.GetCombatInfo(actor);
DevConsole.Log($"HP: {info.CurrentHP}/{info.MaxHP} ({info.HPPercent:P0})");
```

## Console Commands

The following console commands are available:

- `attack` - Attack with selected actor (targets nearest enemy)
- `skills` - List all skills for the selected actor
- `damage <amount>` - Apply damage to the selected actor
- `heal <amount>` - Heal the selected actor
- `suppression [value]` - Get or set suppression for the selected actor
- `morale [value]` - Get or set morale for the selected actor
- `stun` - Toggle stun on the selected actor
- `combat` - Show combat info for the selected actor
