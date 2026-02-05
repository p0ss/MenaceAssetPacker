using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Battle Log panel for the DevConsole. Captures combat events from the game's
/// DevCombatLog static class via Harmony postfixes and displays them in a
/// color-coded, filterable, scrollable feed.
/// </summary>
public static class BattleLog
{
    [Flags]
    public enum EntryType
    {
        SkillUsed   = 1 << 0,
        Hit         = 1 << 1,
        Miss        = 1 << 2,
        Suppression = 1 << 3,
        Morale      = 1 << 4,
        ArmorPen    = 1 << 5,
        Death       = 1 << 6,
    }

    private struct LogEntry
    {
        public string Timestamp;
        public string Message;
        public EntryType Type;
    }

    // Ring buffer
    private const int BufferCapacity = 300;
    private static readonly LogEntry[] _buffer = new LogEntry[BufferCapacity];
    private static int _head;
    private static int _count;

    // Filter state — all types visible by default
    private static EntryType _activeFilters = (EntryType)0x7F;

    // Scroll / auto-scroll
    private static Vector2 _scroll;
    private static bool _scrollToBottom = true;

    // Cached MoraleState enum type for name resolution
    private static Type _moraleStateType;
    private static bool _moraleTypeLookedUp;

    // GUI styles (lazy-initialized) — one per entry type for color coding,
    // avoiding GUI.color which may not be unstripped in IL2CPP.
    private static bool _stylesInit;
    private static GUIStyle _labelStyle;
    private static GUIStyle _hitStyle;
    private static GUIStyle _missStyle;
    private static GUIStyle _suppressionStyle;
    private static GUIStyle _moraleStyle;
    private static GUIStyle _deathStyle;
    private static GUIStyle _skillStyle;
    private static GUIStyle _armorStyle;
    private static GUIStyle _toggleOnStyle;
    private static GUIStyle _toggleOffStyle;

    private const float LineHeight = 18f;

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Append a combat event to the rolling log.
    /// </summary>
    public static void AddEntry(string message, EntryType type)
    {
        _buffer[_head] = new LogEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Message   = message,
            Type      = type,
        };
        _head = (_head + 1) % BufferCapacity;
        if (_count < BufferCapacity) _count++;
        _scrollToBottom = true;
    }

    /// <summary>
    /// Clear all log entries.
    /// </summary>
    public static void Clear()
    {
        _head = 0;
        _count = 0;
        _scroll = Vector2.zero;
    }

    // ------------------------------------------------------------------ //
    //  Panel drawing (called by DevConsole)
    // ------------------------------------------------------------------ //

    internal static void DrawPanel(Rect area)
    {
        InitStyles();

        float y = area.y;

        // --- Toolbar row ---
        float bx = area.x;

        if (GUI.Button(new Rect(bx, y, 50, 20), "Clear"))
            Clear();
        bx += 56;

        GUI.Label(new Rect(bx, y, 44, 20), "Filter:", SafeStyle(_labelStyle));
        bx += 48;

        DrawFilterToggle(ref bx, y, "Hits",   EntryType.Hit);
        DrawFilterToggle(ref bx, y, "Miss",   EntryType.Miss);
        DrawFilterToggle(ref bx, y, "Supp",   EntryType.Suppression);
        DrawFilterToggle(ref bx, y, "Morale", EntryType.Morale);
        DrawFilterToggle(ref bx, y, "Armor",  EntryType.ArmorPen);
        DrawFilterToggle(ref bx, y, "Death",  EntryType.Death);
        DrawFilterToggle(ref bx, y, "Skill",  EntryType.SkillUsed);

        y += 24;

        // --- Collect visible entries in chronological order ---
        var visible = new List<LogEntry>();
        int start = _count < BufferCapacity ? 0 : _head;
        for (int i = 0; i < _count; i++)
        {
            var entry = _buffer[(start + i) % BufferCapacity];
            if ((_activeFilters & entry.Type) != 0)
                visible.Add(entry);
        }

        // --- Scrollable log (manual scroll — GUI.BeginScrollView not unstripped) ---
        float scrollHeight = area.yMax - y;
        float contentHeight = visible.Count * LineHeight;
        var viewRect = new Rect(area.x, y, area.width, scrollHeight);

        if (_scrollToBottom)
        {
            _scroll.y = Math.Max(0, contentHeight - scrollHeight);
            _scrollToBottom = false;
        }

        _scroll.y = DevConsole.HandleScrollWheel(viewRect, contentHeight, _scroll.y);

        GUI.BeginGroup(viewRect);
        float sy = -_scroll.y;
        foreach (var entry in visible)
        {
            if (sy + LineHeight > 0 && sy < scrollHeight)
            {
                GUI.Label(new Rect(0, sy, viewRect.width, LineHeight),
                    $"{entry.Timestamp}  {entry.Message}", StyleForType(entry.Type));
            }
            sy += LineHeight;
        }
        GUI.EndGroup();
    }

    private static void DrawFilterToggle(ref float x, float y, string label, EntryType type)
    {
        bool isOn = (_activeFilters & type) != 0;
        var style = SafeStyle(isOn ? _toggleOnStyle : _toggleOffStyle);
        string text = isOn ? $"[x]{label}" : $"[ ]{label}";
        float w = text.Length * 7 + 12;
        if (GUI.Button(new Rect(x, y, w, 20), text, style))
            _activeFilters ^= type;
        x += w + 2;
    }

    // ------------------------------------------------------------------ //
    //  Harmony patch installation
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Install Harmony postfixes on DevCombatLog static methods.
    /// Safe to call even if DevCombatLog doesn't exist — failures are logged, not thrown.
    /// </summary>
    internal static void ApplyPatches(HarmonyLib.Harmony harmony)
    {
        var self = typeof(BattleLog);
        var flags = BindingFlags.NonPublic | BindingFlags.Static;

        Patch(harmony, "ReportSkillUsed",       self.GetMethod(nameof(Post_ReportSkillUsed), flags));
        Patch(harmony, "ReportHit",             self.GetMethod(nameof(Post_ReportHit), flags));
        Patch(harmony, "ReportMiss",            self.GetMethod(nameof(Post_ReportMiss), flags));
        Patch(harmony, "ReportSuppression",     self.GetMethod(nameof(Post_ReportSuppression), flags));
        Patch(harmony, "ReportMoraleChanged",   self.GetMethod(nameof(Post_ReportMoraleChanged), flags));
        Patch(harmony, "ReportArmorPenetration",self.GetMethod(nameof(Post_ReportArmorPenetration), flags));
        Patch(harmony, "ReportDeath",           self.GetMethod(nameof(Post_ReportDeath), flags));
    }

    private static void Patch(HarmonyLib.Harmony harmony, string methodName, MethodInfo patch)
    {
        if (!GamePatch.Postfix(harmony, "DevCombatLog", methodName, patch))
            MelonLoader.MelonLogger.Warning($"[BattleLog] Could not patch DevCombatLog.{methodName}");
    }

    // ------------------------------------------------------------------ //
    //  Harmony postfix handlers
    //
    //  Parameters use Harmony's __0, __1, … positional convention.
    //  IL2CPP proxy types (Actor, Skill, Entity, etc.) must be received
    //  as `object` — Harmony's DMD trampoline passes Il2CppInterop proxy
    //  instances which can't be implicitly converted to IntPtr.
    //  We extract the native pointer via Il2CppObjectBase.Pointer.
    //  Primitives (int, float, bool) are received as their managed type.
    //  Each handler is wrapped in try/catch so a bad read never crashes.
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Safely extract the IL2CPP native pointer from a Harmony-provided
    /// parameter.  Returns IntPtr.Zero if the object is null or not an
    /// Il2CppObjectBase.
    /// </summary>
    private static IntPtr Ptr(object obj)
    {
        if (obj is Il2CppObjectBase il2cpp)
            return il2cpp.Pointer;
        return IntPtr.Zero;
    }

    // DevCombatLog.ReportSkillUsed(Actor, Skill, Tile)
    private static void Post_ReportSkillUsed(object __0, object __1, object __2)
    {
        try
        {
            var actor = new GameObj(Ptr(__0));
            AddEntry($"{ReadEntityName(actor)} uses {ReadSkillName(__1)}",
                EntryType.SkillUsed);
        }
        catch (Exception ex)
        {
            AddEntry($"[skill used - read error: {ex.Message}]", EntryType.SkillUsed);
        }
    }

    // DevCombatLog.ReportHit(Actor, Skill, HitResult, int, Entity, DamageInfo)
    private static void Post_ReportHit(object __0, object __1, object __2,
        int __3, object __4, object __5)
    {
        try
        {
            var actorName  = ReadEntityName(new GameObj(Ptr(__0)));
            var targetName = ReadEntityName(new GameObj(Ptr(__4)));
            int elementsKilled = __3;

            // HitResult (__2) — boxed value-type proxy
            float finalValue = 0f;
            int roll = 0;
            ReadHitResult(__2, out finalValue, out roll);

            // DamageInfo (__5) — boxed value-type proxy
            int damage = 0, armorDamage = 0;
            ReadDamageInfo(__5, out damage, out armorDamage);

            var msg = $"-> {targetName}: HIT ({finalValue:F0}% chance, rolled {roll})";
            if (damage > 0 || armorDamage > 0)
                msg += $" -- {damage} dmg, {armorDamage} armor dmg";
            if (elementsKilled > 0)
                msg += $", {elementsKilled} killed";

            AddEntry(msg, EntryType.Hit);
        }
        catch (Exception ex)
        {
            AddEntry($"[hit - read error: {ex.Message}]", EntryType.Hit);
        }
    }

    // DevCombatLog.ReportMiss(Actor, Skill, HitResult)
    private static void Post_ReportMiss(object __0, object __1, object __2)
    {
        try
        {
            var actorName = ReadEntityName(new GameObj(Ptr(__0)));

            float finalValue = 0f;
            int roll = 0;
            ReadHitResult(__2, out finalValue, out roll);

            AddEntry($"{actorName}: MISS ({finalValue:F0}% chance, rolled {roll})",
                EntryType.Miss);
        }
        catch (Exception ex)
        {
            AddEntry($"[miss - read error: {ex.Message}]", EntryType.Miss);
        }
    }

    // DevCombatLog.ReportSuppression(Skill, Entity, float, bool)
    private static void Post_ReportSuppression(object __0, object __1, float __2, bool __3)
    {
        try
        {
            var targetName = ReadEntityName(new GameObj(Ptr(__1)));
            var label = __3 ? "suppressed" : "suppressed (indirect)";
            AddEntry($"{targetName} {label} +{__2:F1}", EntryType.Suppression);
        }
        catch (Exception ex)
        {
            AddEntry($"[suppression - read error: {ex.Message}]",
                EntryType.Suppression);
        }
    }

    // DevCombatLog.ReportMoraleChanged(Entity, MoraleState)
    //   MoraleState is an IL2CPP enum — arrives as a boxed proxy; extract int value.
    private static void Post_ReportMoraleChanged(object __0, object __1)
    {
        try
        {
            var entityName = ReadEntityName(new GameObj(Ptr(__0)));
            int moraleInt = ExtractEnumInt(__1);
            var stateName = ResolveMoraleStateName(moraleInt);
            AddEntry($"{entityName} morale -> {stateName}", EntryType.Morale);
        }
        catch (Exception ex)
        {
            AddEntry($"[morale - read error: {ex.Message}]", EntryType.Morale);
        }
    }

    // DevCombatLog.ReportArmorPenetration(Entity, Skill, float, int)
    private static void Post_ReportArmorPenetration(object __0, object __1, float __2, int __3)
    {
        try
        {
            var entityName = ReadEntityName(new GameObj(Ptr(__0)));
            float chance = __2 * 100f;
            AddEntry($"{entityName} armor pen: {chance:F0}% chance, rolled {__3}",
                EntryType.ArmorPen);
        }
        catch (Exception ex)
        {
            AddEntry($"[armor pen - read error: {ex.Message}]", EntryType.ArmorPen);
        }
    }

    // DevCombatLog.ReportDeath(Entity)
    private static void Post_ReportDeath(object __0)
    {
        try
        {
            var entityName = ReadEntityName(new GameObj(Ptr(__0)));
            AddEntry($"{entityName} KILLED", EntryType.Death);
        }
        catch (Exception ex)
        {
            AddEntry($"[death - read error: {ex.Message}]", EntryType.Death);
        }
    }

    // ------------------------------------------------------------------ //
    //  Field-reading helpers
    // ------------------------------------------------------------------ //

    private static string ReadEntityName(GameObj obj)
    {
        if (obj.IsNull) return "<null>";

        // Try DebugName first (common on Actor / Entity in Menace.Tactical)
        var name = obj.ReadString("DebugName");
        if (!string.IsNullOrEmpty(name)) return name;

        // Fall back to Unity object name
        name = obj.GetName();
        return !string.IsNullOrEmpty(name) ? name : $"<0x{obj.Pointer:X8}>";
    }

    /// <summary>
    /// Read a display name from the Skill proxy object using reflection.
    /// Il2CppInterop proxies expose the game type's properties as managed
    /// properties, so we search for name/template properties directly.
    /// </summary>
    private static string ReadSkillName(object skillProxy)
    {
        if (skillProxy == null) return "<null>";

        try
        {
            var type = skillProxy.GetType();
            var flags = BindingFlags.Public | BindingFlags.Instance;

            // Try to get a Template/SkillTemplate property — templates are
            // ScriptableObjects and have a .name property from UnityEngine.Object
            foreach (var propName in new[] { "Template", "SkillTemplate", "m_Template" })
            {
                var prop = type.GetProperty(propName, flags);
                if (prop == null) continue;
                var template = prop.GetValue(skillProxy);
                if (template == null) continue;

                var nameProp = template.GetType().GetProperty("name", flags);
                if (nameProp != null)
                {
                    var n = nameProp.GetValue(template)?.ToString();
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }

            // Try direct name properties on the Skill itself
            foreach (var propName in new[] { "name", "Name", "DebugName", "DisplayName", "Title" })
            {
                var prop = type.GetProperty(propName, flags);
                if (prop == null) continue;
                var val = prop.GetValue(skillProxy)?.ToString();
                if (!string.IsNullOrEmpty(val)) return val;
            }

            // Last resort: ToString may return something useful
            var str = skillProxy.ToString();
            if (!string.IsNullOrEmpty(str) && !str.Contains(type.FullName ?? ""))
                return str;
        }
        catch { /* fall through */ }

        // Final fallback: try GameObj approach
        var ptr = Ptr(skillProxy);
        if (ptr != IntPtr.Zero)
        {
            var obj = new GameObj(ptr);
            var name = obj.GetName();
            if (!string.IsNullOrEmpty(name)) return name;
        }

        return "<skill>";
    }

    /// <summary>
    /// Try to extract FinalValue (float, 0-1) and Roll (int) from a HitResult.
    /// HitResult is a value struct — Harmony passes it as a boxed Il2CppInterop
    /// proxy.  We try GameObj field reads first, then fall back to known offsets.
    /// </summary>
    private static void ReadHitResult(object hitResultObj, out float finalValue, out int roll)
    {
        finalValue = 0f;
        roll = 0;

        var ptr = Ptr(hitResultObj);
        if (ptr == IntPtr.Zero) return;

        try
        {
            // Approach 1: treat as boxed IL2CPP object and use GameObj field reads
            var obj = new GameObj(ptr);

            // HitResult may embed a HitChance sub-struct or expose FinalValue directly
            var hitChance = obj.ReadObj("HitChance");
            if (!hitChance.IsNull)
                finalValue = hitChance.ReadFloat("FinalValue");
            else
                finalValue = obj.ReadFloat("FinalValue");

            roll = obj.ReadInt("Roll");

            // FinalValue is 0-1 in the game, display as percentage
            finalValue *= 100f;

            // If we got plausible data, return
            if (finalValue != 0f || roll != 0) return;
        }
        catch { /* fall through to raw reads */ }

        // Approach 2: raw struct memory reads at known offsets.
        // HitChance starts at offset 0x0 of HitResult data; FinalValue is
        // the first float in HitChance.  For a boxed object the data starts
        // at ptr + 0x10 (IL2CPP object header on 64-bit).
        try
        {
            // Try with IL2CPP boxing header
            finalValue = BitConverter.Int32BitsToSingle(
                Marshal.ReadInt32(ptr + 0x10)) * 100f;
            // Roll follows HitChance (5 floats x 4 bytes = 0x14 from data start)
            roll = Marshal.ReadInt32(ptr + 0x10 + 0x14);

            if (finalValue < 0 || finalValue > 100 || roll < 0 || roll > 100)
            {
                // Values look implausible — try without header (raw struct pointer)
                finalValue = BitConverter.Int32BitsToSingle(
                    Marshal.ReadInt32(ptr)) * 100f;
                roll = Marshal.ReadInt32(ptr + 0x14);
            }
        }
        catch { /* give up, zeros are fine */ }
    }

    /// <summary>
    /// Try to extract Damage and ArmorDamage from a DamageInfo struct/object.
    /// </summary>
    private static void ReadDamageInfo(object damageInfoObj, out int damage, out int armorDamage)
    {
        damage = 0;
        armorDamage = 0;

        var ptr = Ptr(damageInfoObj);
        if (ptr == IntPtr.Zero) return;

        try
        {
            var obj = new GameObj(ptr);
            damage = obj.ReadInt("Damage");
            armorDamage = obj.ReadInt("ArmorDamage");
            if (damage != 0 || armorDamage != 0) return;
        }
        catch { /* fall through */ }

        // Raw reads at known offsets (with IL2CPP boxing header)
        try
        {
            damage = Marshal.ReadInt32(ptr + 0x10 + 0x2C);
            armorDamage = Marshal.ReadInt32(ptr + 0x10 + 0x38);
        }
        catch { /* zeros are fine */ }
    }

    /// <summary>
    /// Extract an integer value from an IL2CPP enum proxy object.
    /// IL2CPP enums are boxed value types whose data starts after the
    /// object header (0x10 on 64-bit).
    /// </summary>
    private static int ExtractEnumInt(object enumObj)
    {
        if (enumObj is int i) return i;

        var ptr = Ptr(enumObj);
        if (ptr == IntPtr.Zero) return 0;

        try
        {
            // Boxed enum: int value sits right after the IL2CPP object header
            return Marshal.ReadInt32(ptr + 0x10);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Try to resolve the MoraleState enum name via reflection on the game assembly.
    /// Falls back to the integer value.
    /// </summary>
    private static string ResolveMoraleStateName(int value)
    {
        if (!_moraleTypeLookedUp)
        {
            _moraleTypeLookedUp = true;
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                if (asm != null)
                    _moraleStateType = asm.GetTypes()
                        .FirstOrDefault(t => t.Name == "MoraleState" && t.IsEnum);
            }
            catch { /* leave null */ }
        }

        if (_moraleStateType != null)
        {
            try
            {
                var name = Enum.GetName(_moraleStateType, value);
                if (name != null) return name;
            }
            catch { /* fall through */ }
        }

        return value.ToString();
    }

    // ------------------------------------------------------------------ //
    //  Style initialization & lookup
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Returns the color style for an entry type, falling back to GUI.skin.label
    /// if styles haven't been created yet (e.g. InitStyles failed on first call).
    /// </summary>
    private static GUIStyle StyleForType(EntryType type)
    {
        var style = type switch
        {
            EntryType.Hit         => _hitStyle,
            EntryType.Miss        => _missStyle,
            EntryType.Suppression => _suppressionStyle,
            EntryType.Morale      => _moraleStyle,
            EntryType.Death       => _deathStyle,
            EntryType.SkillUsed   => _skillStyle,
            EntryType.ArmorPen    => _armorStyle,
            _                     => _labelStyle,
        };
        return style ?? GUI.skin.label;
    }

    /// <summary>
    /// Safe style accessor — never returns null.
    /// </summary>
    private static GUIStyle SafeStyle(GUIStyle style) => style ?? GUI.skin.label;

    private static void InitStyles()
    {
        if (_stylesInit) return;

        // Create all styles inside try/catch.  If any GUIStyle constructor or
        // property setter is unstripped, we still get a usable (un-colored)
        // panel on the next frame because _stylesInit stays false until the
        // very end.
        try
        {
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 13;
            _labelStyle.wordWrap = true;
            _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            // Per-entry-type color styles (mirrors DevConsole's _errorStyle pattern)
            _hitStyle         = new GUIStyle(_labelStyle);
            _hitStyle.normal.textColor = new Color(0.4f, 0.9f, 0.4f);

            _missStyle        = new GUIStyle(_labelStyle);
            _missStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);

            _suppressionStyle = new GUIStyle(_labelStyle);
            _suppressionStyle.normal.textColor = new Color(1f, 1f, 0.3f);

            _moraleStyle      = new GUIStyle(_labelStyle);
            _moraleStyle.normal.textColor = new Color(1f, 0.65f, 0.2f);

            _deathStyle       = new GUIStyle(_labelStyle);
            _deathStyle.normal.textColor = Color.white;
            _deathStyle.fontStyle = FontStyle.Bold;

            _skillStyle       = new GUIStyle(_labelStyle);
            _skillStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);

            _armorStyle       = new GUIStyle(_labelStyle);
            _armorStyle.normal.textColor = new Color(0.6f, 0.7f, 0.85f);

            _toggleOnStyle = new GUIStyle(GUI.skin.button);
            _toggleOnStyle.fontSize = 11;
            _toggleOnStyle.fontStyle = FontStyle.Bold;
            _toggleOnStyle.normal.textColor = Color.white;

            _toggleOffStyle = new GUIStyle(GUI.skin.button);
            _toggleOffStyle.fontSize = 11;
            _toggleOffStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

            // Only set the flag after all styles are successfully created.
            // If anything above threw, we'll retry next frame.
            _stylesInit = true;
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"[BattleLog] Style init failed (will retry): {ex.Message}");
        }
    }
}
