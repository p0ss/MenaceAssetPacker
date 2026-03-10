# FloatExtensions.cs Verification Report

**Verified Against:** Ghidra Decompilation
**Date:** 2026-03-10
**Status:** VERIFIED WITH MINOR NOTATION DIFFERENCES

---

## Summary

All three functions in FloatExtensions.cs have been verified against the actual decompiled code. The reference code is **accurate** and correctly documents the implementation logic.

---

## Function-by-Function Analysis

### 1. AddMult (0x1805320a0)

**Reference Code:**
```csharp
public static float AddMult(this float accumulator, float mult)
{
    return accumulator + (mult - 1.0f);
}
```

**Decompiled Code:**
```c
void FloatExtensions_AddMult_StackPercentages(float *param_1, float param_2)
{
    *param_1 = (param_2 - DAT_182d8fb9c) + *param_1;
    return;
}
```

**Verification Result:** MATCH

**Notes:**
- `DAT_182d8fb9c` is the float constant `1.0f`
- The decompiled version shows this as an extension method (modifies param_1 in place via pointer)
- Formula is identical: `accumulator + (mult - 1.0f)`
- The reference code correctly documents the additive stacking behavior

**No corrections needed.**

---

### 2. Clamped (0x1805320c0)

**Reference Code:**
```csharp
public static float Clamped(float value)
{
    return Math.Max(0.0f, value);
}
```

**Decompiled Code:**
```c
float Menace_Tools_FloatExtensions__Clamped(float param_1)
{
    float fVar1;

    fVar1 = 0.0;
    if (0.0 <= param_1) {
        fVar1 = param_1;
    }
    return fVar1;
}
```

**Verification Result:** MATCH

**Notes:**
- The decompiled code is a manual implementation of `Math.Max(0.0f, value)`
- Logic: If `value >= 0`, return `value`; otherwise return `0.0f`
- This is semantically identical to `Math.Max(0.0f, value)`

**No corrections needed.**

---

### 3. Flipped (0x1805320d0)

**Reference Code:**
```csharp
public static float Flipped(float value)
{
    // Mathematically: 2.0 - value, or equivalently: 1.0 - (value - 1.0)
    return 2.0f - value;
}
```

**Decompiled Code:**
```c
float Menace_Tools_FloatExtensions__Flipped(float param_1)
{
    return DAT_182d8fb9c - (param_1 - DAT_182d8fb9c);
}
```

**Verification Result:** MATCH

**Notes:**
- `DAT_182d8fb9c` is `1.0f`
- Decompiled formula: `1.0f - (value - 1.0f)`
- Simplifies to: `1.0f - value + 1.0f = 2.0f - value`
- Both formulas are mathematically equivalent
- The reference code comment correctly notes both representations

**No corrections needed.**

---

## Constants Identified

| Address | Value | Usage |
|---------|-------|-------|
| 0x182d8fb9c | 1.0f | Used in AddMult and Flipped for multiplier math |

---

## Conclusion

The reference code in `FloatExtensions.cs` is **accurate and complete**. All three functions:

1. **AddMult** - Correctly implements additive multiplier stacking
2. **Clamped** - Correctly implements zero-floor clamping
3. **Flipped** - Correctly implements multiplier inversion around 1.0

The documentation comments accurately describe the purpose and behavior of each function. The examples provided (e.g., `1.0.AddMult(1.2).AddMult(1.3) = 1.5`) are mathematically correct based on the verified implementation.

**Recommendation:** This reference code can be trusted for SDK development and modding documentation.
