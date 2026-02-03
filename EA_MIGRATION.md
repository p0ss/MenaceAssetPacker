# EA Migration Workflow

Step-by-step process for migrating the extraction pipeline to a new game build (e.g. Early Access).

## Prerequisites

- IL2Cpp Dumper (Il2CppDumper or cpp2il)
- MelonLoader installed on the new build
- Python 3.8+
- .NET 6.0 SDK

## Steps

### 1. Generate IL2CPP dump from new build

Run Il2CppDumper against the EA build's `GameAssembly.dll` and `global-metadata.dat`:

```bash
# Output goes to il2cpp_dump_ea/
Il2CppDumper GameAssembly.dll global-metadata.dat il2cpp_dump_ea
```

### 2. Generate schema from new dump

```bash
python generate_schema.py il2cpp_dump_ea/dump.cs schema_ea.json
```

### 3. Diff schemas to identify changes

```bash
python diff_schemas.py generated/schema.json schema_ea.json
```

Review the output. Pay attention to:
- **CRIT (offset changes)**: Extraction code MUST be regenerated. Any template with changed offsets will read garbage at old offsets.
- **ADD/DEL (new/removed types)**: New templates may need extraction support. Removed templates should be cleaned from output.
- **CHG (enum value changes)**: Enum-based lookups (like KeyBindPlayerSetting) may need updating.

### 4. Regenerate extraction code

```bash
python generate_all_templates.py --from-schema schema_ea.json
```

This regenerates `generated/generated_extraction_code.cs` with the new offsets.

Optionally regenerate injection code:

```bash
python generate_injection_code.py --from-schema schema_ea.json
```

### 5. Build and deploy DataExtractor

```bash
dotnet build src/Menace.DataExtractor -c Release
```

Copy the output DLL to the game's `Mods/` directory.

### 6. Run extraction

Launch the game with MelonLoader. The DataExtractor mod will:
1. Wait for templates to load
2. Extract all template data to `UserData/ExtractedData/`
3. Log progress to MelonLoader console

### 7. Validate extraction

```bash
python validate_extraction.py --schema schema_ea.json --data /path/to/ExtractedData
```

Review the validation report:
- **FAIL on type validation**: Likely wrong offsets. Cross-reference with the schema diff.
- **WARN on instance names**: `unknown_N` names indicate the `.name` property read failed. May need the three-phase approach.
- **FAIL on coverage**: Some templates may not have instances in the current game state. Try extracting from different scenes.

### 8. Fix issues

Common fixes:
- **Garbage float values**: Offset shifted. Regenerate from updated schema.
- **Missing templates**: Template may only exist in specific scenes (tactical, campaign map, etc.)
- **Name resolution failures**: The `obj.name` read may crash. Verify the m_CachedPtr is valid before reading.

### 9. Update DevMode mod (if needed)

If the schema diff shows changes to `KeyBindPlayerSettingTemplate` or `TacticalState`:

1. Check if `ToggleCheatMenu` enum value changed
2. Check if `KeyBinding` struct offsets changed
3. Update constants in `DevModeMod.cs`
4. Rebuild: `dotnet build src/Menace.DevMode -c Release`

### 10. Commit the new schema

```bash
cp schema_ea.json generated/schema.json
git add generated/schema.json generated/generated_extraction_code.cs generated/generated_injection_code.cs
git commit -m "update schema and extraction code for EA build"
```

## Quick Reference

| Script | Purpose |
|--------|---------|
| `generate_schema.py` | Parse dump.cs → generated/schema.json |
| `diff_schemas.py` | Compare two schemas |
| `generate_all_templates.py` | Schema/dump → extraction C# code |
| `generate_injection_code.py` | Extraction code → injection C# code |
| `build_template_hierarchy.py` | Extracted JSON → menu.json hierarchy |
| `validate_extraction.py` | Validate extracted data against schema |
