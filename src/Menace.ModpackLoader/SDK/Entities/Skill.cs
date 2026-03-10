using System;
using System.Runtime.InteropServices;

namespace Menace.SDK.Entities;

/// <summary>
/// Object-oriented wrapper for a Skill instance.
///
/// Provides clean property access to skill data including template properties
/// like IsAttack and IsSilent.
///
/// Usage:
///   var skill = new Skill(skillPtr);
///   if (skill.IsAttack && !skill.IsSilent)
///       actor.AddEffect("concealment", -3, 1);
///
/// Properties are loaded from the schema when available, with fallback
/// hardcoded offsets for known fields.
/// </summary>
public class Skill
{
    // Known offsets (fallback if schema not loaded)
    private const int OFFSET_TEMPLATE = 0x10;
    private const int OFFSET_TEMPLATE_IS_ATTACK = 0xF2;
    private const int OFFSET_TEMPLATE_IS_SILENT = 0x110;

    private readonly IntPtr _pointer;
    private readonly IntPtr _templatePtr;

    // Cached schema offsets
    private static int? _schemaOffsetTemplate;
    private static int? _schemaOffsetIsAttack;
    private static int? _schemaOffsetIsSilent;
    private static bool _schemaChecked;

    /// <summary>
    /// Create a Skill wrapper from a pointer.
    /// </summary>
    public Skill(IntPtr pointer)
    {
        _pointer = pointer;

        if (_pointer != IntPtr.Zero)
        {
            // Read template pointer from skill
            var templateOffset = GetOffsetTemplate();
            _templatePtr = Marshal.ReadIntPtr(_pointer + templateOffset);
        }
    }

    /// <summary>
    /// Create a Skill wrapper from a pointer value (for Lua interop).
    /// </summary>
    public Skill(long pointerValue) : this(new IntPtr(pointerValue))
    {
    }

    /// <summary>
    /// The raw pointer to the Skill instance.
    /// </summary>
    public IntPtr Pointer => _pointer;

    /// <summary>
    /// The raw pointer to the SkillTemplate.
    /// </summary>
    public IntPtr TemplatePointer => _templatePtr;

    /// <summary>
    /// Check if this skill wrapper points to a valid skill.
    /// </summary>
    public bool IsValid => _pointer != IntPtr.Zero;

    /// <summary>
    /// Check if this skill wrapper has a valid template.
    /// </summary>
    public bool HasTemplate => _templatePtr != IntPtr.Zero;

    /// <summary>
    /// Get the skill's name via GameObj.
    /// </summary>
    public string Name
    {
        get
        {
            if (!IsValid) return "<invalid>";
            try
            {
                return new GameObj(_pointer).GetName() ?? "<unnamed>";
            }
            catch
            {
                return "<error>";
            }
        }
    }

    /// <summary>
    /// Get the template's name via GameObj.
    /// </summary>
    public string TemplateName
    {
        get
        {
            if (!HasTemplate) return "<no template>";
            try
            {
                return new GameObj(_templatePtr).GetName() ?? "<unnamed>";
            }
            catch
            {
                return "<error>";
            }
        }
    }

    /// <summary>
    /// Whether this skill is an attack skill.
    /// Read from SkillTemplate.IsAttack.
    /// </summary>
    public bool IsAttack
    {
        get
        {
            if (!HasTemplate) return false;
            try
            {
                var offset = GetOffsetIsAttack();
                return Marshal.ReadByte(_templatePtr + offset) != 0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Whether this skill is silent (doesn't reveal position when used).
    /// Read from SkillTemplate.IsSilent.
    /// </summary>
    public bool IsSilent
    {
        get
        {
            if (!HasTemplate) return false;
            try
            {
                var offset = GetOffsetIsSilent();
                return Marshal.ReadByte(_templatePtr + offset) != 0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Read a template property by name using Templates.ReadField.
    /// Use this for properties not exposed as direct accessors.
    /// </summary>
    public object GetTemplateProperty(string propertyName)
    {
        if (!HasTemplate) return null;
        try
        {
            var templateObj = new GameObj(_templatePtr);
            return Templates.ReadField(templateObj, propertyName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Read a template property with type conversion.
    /// </summary>
    public T GetTemplateProperty<T>(string propertyName)
    {
        var value = GetTemplateProperty(propertyName);
        if (value == null) return default;
        try
        {
            if (value is T typed) return typed;
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Offset Resolution (schema-first with fallback)
    // ═══════════════════════════════════════════════════════════════════

    private static void EnsureSchemaChecked()
    {
        if (_schemaChecked) return;
        _schemaChecked = true;

        if (!TemplateSchema.IsInitialized) return;

        // Try to get offsets from schema
        if (TemplateSchema.TryGetOffset("Skill", "Template", out var tOff))
            _schemaOffsetTemplate = tOff;

        if (TemplateSchema.TryGetOffset("SkillTemplate", "IsAttack", out var aOff))
            _schemaOffsetIsAttack = aOff;

        if (TemplateSchema.TryGetOffset("SkillTemplate", "IsSilent", out var sOff))
            _schemaOffsetIsSilent = sOff;
    }

    private static int GetOffsetTemplate()
    {
        EnsureSchemaChecked();
        return _schemaOffsetTemplate ?? OFFSET_TEMPLATE;
    }

    private static int GetOffsetIsAttack()
    {
        EnsureSchemaChecked();
        return _schemaOffsetIsAttack ?? OFFSET_TEMPLATE_IS_ATTACK;
    }

    private static int GetOffsetIsSilent()
    {
        EnsureSchemaChecked();
        return _schemaOffsetIsSilent ?? OFFSET_TEMPLATE_IS_SILENT;
    }

    public override string ToString()
    {
        return $"Skill({Name}, IsAttack={IsAttack}, IsSilent={IsSilent})";
    }
}
