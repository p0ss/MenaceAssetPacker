# REPL

The Menace SDK includes a live C# REPL (Read-Eval-Print Loop) built on Roslyn, integrated into the DevConsole `Console` panel.

## Overview

The REPL system compiles and executes C# expressions and statements at runtime against the live game process. It consists of three components:

- **RuntimeReferenceResolver** -- discovers all available metadata references (assemblies) for the Roslyn compiler.
- **RuntimeCompiler** -- wraps input in a generated class, compiles via Roslyn, and loads the resulting assembly.
- **ConsoleEvaluator** -- orchestrates compilation and execution, maintains evaluation history, and formats output.

## Access

Press `~` to open the DevConsole and switch to the **Console** panel.

Type a C# expression or statement in the input field at the bottom and press **Enter** or click **Run**. Use the **Up/Down arrow** keys to navigate input history.

Commands are resolved first. If no command matches and Roslyn (`Microsoft.CodeAnalysis.CSharp`) is available, the input is evaluated as C#.

## Auto-Imports

Every REPL input is compiled with the following `using` directives automatically included:

```csharp
using System;
using System.Linq;
using System.Collections.Generic;
using Menace.SDK;
using UnityEngine;
```

You do not need to add these manually in REPL expressions.

## Expression vs. Statement Detection

The compiler auto-detects whether input is an expression or a block of statements:

| Input pattern | Mode | Behavior |
|---------------|------|----------|
| No `;`, no `{` | Expression | Wrapped as `return <input>;` |
| Single trailing `;` without `return` | Expression | Trailing `;` stripped, wrapped as `return <input>;` |
| Contains `{`, or multiple `;` | Statements | Compiled as-is. If no `return` is present, `return null;` is appended. |
| Contains `return` | Statements | Compiled as-is. |

## RuntimeReferenceResolver

```csharp
public class RuntimeReferenceResolver
{
    public List<MetadataReference> ResolveAll()
    public void Invalidate()
}
```

Discovers MetadataReferences by scanning:

1. System/BCL assemblies from the game's `dotnet/` or `MelonLoader/net6/` directory.
2. MelonLoader and Il2CppInterop assemblies.
3. Game IL2CPP assemblies from `MelonLoader/Il2CppAssemblies/`.
4. Mod DLLs from the `Mods/` directory and its subdirectories.
5. The ModpackLoader assembly itself.
6. Fallback: all non-dynamic assemblies in the current AppDomain.

Results are cached after the first call. Call `Invalidate()` to force re-resolution.

## RuntimeCompiler

```csharp
public class RuntimeCompiler
{
    public RuntimeCompiler(IReadOnlyList<MetadataReference> references)
    public CompilationResult Compile(string input)
    public CompilationResult CompileExpression(string expression)
    public CompilationResult CompileStatements(string statements)
}
```

Each input is wrapped in a generated static class:

```csharp
using System;
using System.Linq;
using System.Collections.Generic;
using Menace.SDK;
using UnityEngine;

public static class ReplExpr_N
{
    public static object Execute()
    {
        // your code here
    }
}
```

The compilation uses `OptimizationLevel.Debug` and allows unsafe code.

### CompilationResult

```csharp
public class CompilationResult
{
    public bool Success;
    public Assembly LoadedAssembly;
    public IReadOnlyList<string> Errors;
    public IReadOnlyList<string> Warnings;
}
```

## ConsoleEvaluator

```csharp
public class ConsoleEvaluator
{
    public ConsoleEvaluator(RuntimeCompiler compiler)
    public EvalResult Evaluate(string input)
    public IReadOnlyList<(string Input, EvalResult Result)> History { get; }
    public void ClearHistory()
}
```

`Evaluate` compiles and executes the input, catching `TargetInvocationException` to surface the inner exception cleanly. Results are stored in `History` for display in the Console output.

### EvalResult

```csharp
public class EvalResult
{
    public bool Success;
    public object Value;
    public string DisplayText;
    public string Error;
}
```

`DisplayText` is formatted with type-aware rendering:
- `GameObj` -- shows `{TypeName 'Name' @ 0xPTR}` or `GameObj.Null`
- `GameType` -- shows `GameType(FullName)` or `GameType.Invalid`
- `string` -- shows quoted: `"value"`
- `bool` -- shows lowercase: `true` / `false`
- Arrays -- shows `ElementType[length]`
- Collections with `Count` -- shows `TypeName (Count = N)`
- Other values -- `ToString()`, truncated to 500 characters

## Examples

The following are expressions and statements you can type into the DevConsole Console input field.

### Simple expressions

```
> 2 + 2
  4

> GameType.Find("WeaponTemplate").IsValid
  true

> GameType.Find("WeaponTemplate").FullName
  "Menace.Strategy.WeaponTemplate"
```

### Querying game objects

```
> GameQuery.FindAll("WeaponTemplate").Length
  142

> GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762")
  {WeaponTemplate 'weapon.generic_assault_rifle_tier1_ARC_762' @ 0x7F1234ABCD00}
```

### Reading fields

```
> GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762").ReadFloat("Damage")
  10.0

> Templates.ReadField(Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762"), "MaxRange")
  7
```

### Multi-statement blocks

```
> var weapons = GameQuery.FindAll("WeaponTemplate"); return weapons.Length;
  142

> var t = GameType.Find("WeaponTemplate"); return t.HasField("Damage");
  true
```

### Modifying game state

```
> var w = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762"); w.WriteFloat("Damage", 25.0f); return w.ReadFloat("Damage");
  25.0
```

### Using LINQ (auto-imported)

```
> GameQuery.FindAll("WeaponTemplate").Where(w => !w.IsNull).Count()
  142
```

### Walking a type hierarchy

```
> var parts = new List<string>(); var t = GameType.Find("WeaponTemplate"); while (t != null) { parts.Add(t.FullName); t = t.Parent; } return string.Join(" -> ", parts);
  "Menace.Strategy.WeaponTemplate -> Menace.Data.DataTemplate -> UnityEngine.ScriptableObject -> UnityEngine.Object"
```
