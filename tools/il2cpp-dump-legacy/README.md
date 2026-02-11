# IL2CPP Dump Legacy Tools

These scripts parse IL2CPP dump files (from tools like Il2CppDumper) to generate extraction and injection code. They are **no longer used** in the active workflow.

## Current Approach

The modkit now uses:
- **Dynamic reflection** in the DataExtractor for runtime field discovery
- **Ghidra** with MCP integration for reverse engineering memory layouts

This approach is more robust and doesn't require regenerating code when the game updates.

## When to Use These

These scripts are preserved as a fallback for situations where:
- Dynamic reflection fails for specific types
- Ghidra is unavailable
- You need to understand the structure from a fresh IL2CPP dump

## Scripts

| Script | Purpose |
|--------|---------|
| `generate_all_templates.py` | Generates C# extraction code from IL2CPP dump |
| `generate_injection_code.py` | Generates C# injection code for template patching |
| `generate_offset_code.py` | Generates field offset constants |
| `extract_template_dump.py` | Extracts minimal template dump (35MB → 500KB) |
| `build_template_hierarchy.py` | Builds template inheritance hierarchy |

## Usage

```bash
# Extract minimal dump from full IL2CPP dump
python extract_template_dump.py /path/to/dump.cs il2cpp_templates.dump

# Generate extraction code
python generate_all_templates.py

# Generate injection code
python generate_injection_code.py
```

The generated files were previously placed in `generated/` but are no longer compiled into the modkit.
