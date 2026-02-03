# REPL

The Menace SDK includes a live C# REPL (Read-Eval-Print Loop) built on Roslyn, accessible through the DevConsole's REPL tab.

## Overview

The REPL system compiles and executes C# expressions and statements at runtime against the live game process. It consists of three components:

- **RuntimeReferenceResolver** -- discovers all available metadata references (assemblies) for the Roslyn compiler.
- **RuntimeCompiler** -- wraps input in a generated class, compiles via Roslyn, and loads the resulting assembly.
- **ConsoleEvaluator** -- orchestrates compilation and execution, maintains evaluation history, and formats output.

## Access

Press `~` to open the DevConsole, then click the **REPL** tab.

Type a C# expression or statement in the input field at the bottom and press **Enter** or click **Run**. Use the **Up/Down arrow** keys to navigate input history.

The REPL panel is only available when Roslyn (`Microsoft.CodeAnalysis.CSharp`) is present in the runtime. If Roslyn is not available, the REPL tab will not appear.

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

`Evaluate` compiles and executes the input, catching `TargetInvocationException` to surface the inner exception cleanly. Results are stored in `History` for the REPL panel to display.

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

The following are expressions and statements you can type into the REPL input field.

### Simple expressions

```
> 2 + 2
  4

> GameType.Find("Agent").IsValid
  true

> GameType.Find("Agent").FullName
  "Agent"
```

### Querying game objects

```
> GameQuery.FindAll("Agent").Length
  8

> GameQuery.FindByName("Agent", "Player")
  {Agent 'Player' @ 0x7F1234ABCD00}
```

### Reading fields

```
> GameQuery.FindByName("Agent", "Player").ReadInt("health")
  100

> Templates.ReadField(Templates.Find("WeaponDef", "AssaultRifle"), "damage")
  35
```

### Multi-statement blocks

```
> var agents = GameQuery.FindAll("Agent"); return agents.Length;
  8

> var t = GameType.Find("WeaponDef"); return t.HasField("damage");
  true
```

### Modifying game state

```
> var p = GameQuery.FindByName("Agent", "Player"); p.WriteInt("health", 999); return p.ReadInt("health");
  999
```

### Using LINQ (auto-imported)

```
> GameQuery.FindAll("Agent").Where(a => a.IsAlive).Count()
  6
```

### Walking a type hierarchy

```
> var parts = new List<string>(); var t = GameType.Find("SpecialAgent"); while (t != null) { parts.Add(t.FullName); t = t.Parent; } return string.Join(" -> ", parts);
  "SpecialAgent -> Agent -> UnitDef -> ScriptableObject -> Object"
```
