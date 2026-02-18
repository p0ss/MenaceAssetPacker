# Localization System

This document describes how the game's localization system works, based on reverse engineering analysis.

## Overview

The game uses a custom localization system built on three main classes:
- `Menace.Tools.BaseLocalizedString` - Abstract base class
- `Menace.Tools.LocalizedLine` - Single-line localized text
- `Menace.Tools.LocalizedMultiLine` - Multi-line localized text (descriptions, etc.)

These are **wrapper objects**, not plain strings. This is the source of the "invalid cast from System.String to LocalizedMultiLine" error that mods encounter.

## Class Structure

### BaseLocalizedString Memory Layout

```
Offset  Type        Field                   Description
------  ----        -----                   -----------
+0x00   vtable      (inherited)             Virtual method table
+0x10   int         LocaCategory            Category enum (0-21)
+0x18   string      m_KeyPart1              First part of localization key
+0x20   string      m_KeyPart2              Optional second part of key (nullable)
+0x28   string      m_CategoryName          Category name string (e.g., "EntityTemplate")
+0x30   string      m_Identifier            Identifier/name for this entry
+0x38   string      m_DefaultTranslation    The actual text content (fallback)
+0x40   bool        hasPlaceholders         Whether text has {0}, {1} placeholders
+0x48   string[]    m_PlaceholderParams     Array for placeholder replacement values
```

### LocalizedLine and LocalizedMultiLine

Both `LocalizedLine` and `LocalizedMultiLine` are thin wrappers that inherit directly from `BaseLocalizedString`. Their constructors simply call the base constructor:

```csharp
// Pseudo-code from decompilation
LocalizedLine(int category, string identifier, bool hasPlaceholders)
    : BaseLocalizedString(category, identifier, hasPlaceholders) { }

LocalizedMultiLine(int category, string identifier, bool hasPlaceholders)
    : BaseLocalizedString(category, identifier, hasPlaceholders) { }
```

## LocaCategory Enum

The `LocaCategory` enum has 22 values (0-21). Values >= 22 trigger a debug error.

Categories are divided into:
- **Data Template Categories** (0-21): Used for game data templates
- **Dynamic Categories** (22+): Used for runtime-generated content
- **Conversation Editor Content**: Special handling for dialogue

```csharp
// IsDataTemplateCategory check from decompilation
bool IsDataTemplateCategory(int category) => category < 22;
```

## Localization Key Format

Keys are constructed from category + parts using the pattern:
```
{CategoryName}.{KeyPart1}.{Identifier}
// or if KeyPart2 is set:
{CategoryName}.{KeyPart1}.{Identifier}.{KeyPart2}
```

Example: `EntityTemplate.Soldier.DisplayName`

## Translation Loading

### CSV File Format

Translations are stored in CSV files under `Loca/{Language}/` (e.g., `Loca/English/`).

CSV columns:
1. **Key** - The full localization key
2. **EntryType** - Type of entry (parsed by `EnumParser<LocaEntryType>`)
3. **DefaultTranslation** - Fallback text (English)
4. **Translation** - Localized text for target language
5. **Additional** - Extra data (language-specific handling)

### Loading Process

1. `LocaData.LoadTranslation(language, clearExisting)` is called
2. CSV file is loaded from `Loca/{LanguageName}` resource or external file
3. Line endings are normalized (CRLF/CR → LF)
4. Each line is parsed by `LocaReader.ParseCsvLine()`
5. Category is extracted from key prefix
6. Entry is added to `LocaCategoryData` dictionary

### External Override Support

The system checks for external CSV files first:
```
{DataPath}/../Loca/{Language}.csv
```
If found, it loads from file instead of embedded resources. This allows modding translations.

## String Conversion

### Implicit String Conversion (op_Implicit)

`BaseLocalizedString` has an `op_Implicit` operator that converts to string:

```csharp
// Simplified from decompilation
public static implicit operator string(BaseLocalizedString loc)
{
    if (loc == null) throw NullReferenceException;
    return loc.GetTranslated();  // Virtual call via vtable
}
```

### GetTranslated() Method

```csharp
// Simplified logic from decompilation
string GetTranslated(string[] overrideParams = null)
{
    var locaManager = LocaManager.Get();

    // If no translation system, use default
    if (locaManager == null || !locaManager.IsLoaded)
    {
        return m_DefaultTranslation ?? "";
    }

    // Try to get translation
    var category = locaManager.Data.GetCategory(this.LocaCategory);
    var key = this.GetKey();

    if (category.TryGetTranslation(key, out string translation))
    {
        // Apply placeholder replacements if needed
        if (hasPlaceholders && m_PlaceholderParams != null)
        {
            foreach (var param in m_PlaceholderParams)
            {
                translation = translation.Replace("{n}", param);
            }
        }
        return translation;
    }

    // Fallback to default
    return m_DefaultTranslation ?? "";
}
```

## Why Mods Fail

### The Problem

When a mod tries to do:
```csharp
template.DisplayName = "My Custom Name";  // FAILS!
```

This fails because `DisplayName` is of type `LocalizedLine`, not `string`. The IL2CPP runtime cannot implicitly convert a `System.String` to a `LocalizedLine` object.

### Current Extractor Behavior

The data extractor correctly reads localized strings by:
1. Detecting `LocalizedLine`/`LocalizedMultiLine` types via `IsLocalizationClass()`
2. Reading `m_DefaultTranslation` at offset +0x38
3. Exporting as plain string in JSON

### What Mods Need

To properly set localized text, mods need to either:

1. **Create a new LocalizedLine/LocalizedMultiLine object** with the text
2. **Write directly to m_DefaultTranslation** (offset +0x38) if modifying existing object
3. **Use the game's localization system** by adding entries to LocaData

## Recommendations for ModpackLoader

### IMPORTANT: Shared Localization Objects

**Localization objects are SHARED across templates.** When multiple templates have the same localization key (e.g., both Tank A and Tank B use the same description key), they reference the **same** `LocalizedLine` instance. Modifying the shared instance corrupts ALL templates that reference it.

This caused a bug where modifying one tank's description would corrupt unrelated text (conversations, other descriptions) with random content that changed each launch.

### Option 1: Direct Field Write (DEPRECATED - DO NOT USE)

~~When loading mod data, write the string value directly to `m_DefaultTranslation` at offset +0x38.~~

**WARNING:** This approach modifies shared localization objects and causes random text corruption across unrelated game content. Do not use this pattern.

### Option 2: Create New Objects (IMPLEMENTED)

The ModpackLoader now creates **new** `LocalizedLine`/`LocalizedMultiLine` objects for each modification:

```csharp
// Pseudo-code for ModpackLoader
if (IsLocalizationField(fieldType))
{
    IntPtr existingLoc = ReadFieldPtr(obj, fieldOffset);
    if (existingLoc != IntPtr.Zero)
    {
        // Create a NEW localization object
        IntPtr newLoc = CreateLocalizedObject(existingLoc, stringValue);

        // Replace the template's field reference with the new object
        Marshal.WriteIntPtr(obj + fieldOffset, newLoc);
    }
}
```

This ensures each modified template has its own unique localization object, preventing corruption of shared instances.

### Option 3: Custom Category (Alternative)

Register a mod-specific localization category and use proper keys:
1. Add entries to `LocaCategoryData` for the mod's category
2. Create `LocalizedLine` objects that reference those keys
3. Translations will work with the game's normal system

## Related Classes

### LocaManager

Singleton that manages all localization data:
- `LocaManager.Get()` - Get instance
- `LocaData Data` at +0x18 - The translation database
- `bool IsLoaded` at +0x10 - Whether translations are loaded

### LocaData

Container for all translation categories:
- `GetCategory(int category)` - Get `LocaCategoryData` for a category
- `LoadTranslation(language, clear)` - Load translations from CSV

### LocaCategoryData

Dictionary of translations for one category:
- `Dictionary<string, LocaEntry> Entries` at +0x18
- `AddEntry(key, defaultTranslation, entryType)` - Add new entry
- `TryGetTranslation(key, out translation)` - Get translated text

### LocaEntry

Single translation entry:
- `string Key` - The localization key
- `string DefaultTranslation` at +0x28 - Fallback text
- `string Translation` at +0x30 - Localized text

## Field Offsets Summary

| Class | Field | Offset | Type |
|-------|-------|--------|------|
| BaseLocalizedString | LocaCategory | +0x10 | int |
| BaseLocalizedString | m_KeyPart1 | +0x18 | string |
| BaseLocalizedString | m_KeyPart2 | +0x20 | string |
| BaseLocalizedString | m_CategoryName | +0x28 | string |
| BaseLocalizedString | m_Identifier | +0x30 | string |
| BaseLocalizedString | m_DefaultTranslation | +0x38 | string |
| BaseLocalizedString | hasPlaceholders | +0x40 | bool |
| BaseLocalizedString | m_PlaceholderParams | +0x48 | string[] |
| LocaManager | IsLoaded | +0x10 | bool |
| LocaManager | Data | +0x18 | LocaData |
| LocaCategoryData | Entries | +0x18 | Dictionary |
| LocaEntry | DefaultTranslation | +0x28 | string |
| LocaEntry | Translation | +0x30 | string |
