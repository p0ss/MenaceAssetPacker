#!/usr/bin/env python3
"""
Build a hierarchical menu structure from extracted templates.

This script:
1. Reads all template JSON files
2. Analyzes template inheritance from IL2CPP dump
3. Creates a menu.json with proper hierarchy based on:
   - Template inheritance (BaseItem → Weapon → specific weapons)
   - Name structure (mod_weapon.heavy.cannon_long → mod_weapon/heavy/cannon_long)
4. Ensures each template instance appears only once in the hierarchy

Output: menu.json with structure like:
{
  "BaseItemTemplate": {
    "WeaponTemplate": {
      "mod_weapon": {
        "heavy": {
          "cannon_long": { "template": "WeaponTemplate", "name": "mod_weapon.heavy.cannon_long" }
        }
      }
    },
    "ArmorTemplate": {
      ...
    }
  }
}
"""

import json
import re
from pathlib import Path
from collections import defaultdict

def parse_template_inheritance(dump_path):
    """Extract template class hierarchy from IL2CPP dump"""
    with open(dump_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Find all Template classes and their base classes
    hierarchy = {}

    # Pattern: public class TemplateType : BaseType
    pattern = r'public class (\w+Template)\s+:\s+(\w+)'

    for match in re.finditer(pattern, content):
        template_type = match.group(1)
        base_type = match.group(2)

        # Only track inheritance between Template types
        if base_type.endswith('Template') or base_type == 'ScriptableObject':
            hierarchy[template_type] = base_type if base_type != 'ScriptableObject' else None

    return hierarchy

def build_name_path(name):
    """Convert dot-separated name to path list"""
    # mod_weapon.heavy.cannon_long → ['mod_weapon', 'heavy', 'cannon_long']
    return name.split('.')

def get_inheritance_chain(template_type, hierarchy):
    """Get full inheritance chain for a template type"""
    chain = [template_type]
    current = template_type

    while current in hierarchy and hierarchy[current]:
        parent = hierarchy[current]
        if parent in chain:  # Avoid cycles
            break
        chain.append(parent)
        current = parent

    return list(reversed(chain))  # Root to leaf

def build_hierarchical_menu(extracted_data_path, dump_path, output_path):
    """Build hierarchical menu from extracted templates"""

    print("Loading IL2CPP dump to analyze inheritance...")
    hierarchy = parse_template_inheritance(dump_path)

    print(f"Found {len(hierarchy)} template types in hierarchy")
    for template_type, base_type in sorted(hierarchy.items()):
        if base_type:
            print(f"  {template_type} → {base_type}")

    print("\nLoading extracted template data...")

    # Load all template JSON files
    templates_by_type = {}
    data_dir = Path(extracted_data_path)

    for json_file in data_dir.glob("*.json"):
        if json_file.name == "AssetReferences.json" or json_file.name == "menu.json":
            continue

        template_type = json_file.stem  # e.g., "WeaponTemplate"

        try:
            with open(json_file, 'r') as f:
                templates = json.load(f)

            if isinstance(templates, list):
                templates_by_type[template_type] = templates
                print(f"  Loaded {len(templates)} instances of {template_type}")
        except Exception as e:
            print(f"  ⚠️  Failed to load {json_file.name}: {e}")

    print(f"\nBuilding hierarchical menu...")

    # Build menu structure
    menu = {}
    placement_map = {}  # Track where each instance is placed to avoid duplicates

    # Sort template types by inheritance depth (most specific first)
    # This ensures WeaponTemplate is processed before ItemTemplate
    def get_depth(template_type):
        if template_type not in hierarchy:
            return 0
        chain = get_inheritance_chain(template_type, hierarchy)
        return len(chain)

    sorted_types = sorted(templates_by_type.keys(), key=get_depth, reverse=True)

    # Process each template type (most specific first)
    for template_type in sorted_types:
        instances = templates_by_type[template_type]
        # Get inheritance chain (root to leaf)
        chain = get_inheritance_chain(template_type, hierarchy)

        print(f"\n{template_type}: {' → '.join(chain)}")

        for instance in instances:
            if not isinstance(instance, dict):
                continue

            name = instance.get('name', '')
            if not name:
                continue

            # Check if already placed
            if name in placement_map:
                print(f"  ⚠️  Duplicate: {name} (already in {placement_map[name]})")
                continue

            # Build path: inheritance chain + name parts
            path_parts = chain.copy()

            # Add name hierarchy
            name_parts = build_name_path(name)
            path_parts.extend(name_parts[:-1])  # All but last part are folders

            # Navigate/create menu structure
            current = menu
            full_path = []

            for part in path_parts:
                full_path.append(part)
                if part not in current:
                    current[part] = {}
                current = current[part]

            # Place the leaf node (actual template instance)
            leaf_name = name_parts[-1] if name_parts else name
            current[leaf_name] = {
                'template_type': template_type,
                'name': name,
                'data': instance
            }

            placement_map[name] = '/'.join(full_path + [leaf_name])
            print(f"  ✓ {name} → {placement_map[name]}")

    # Save menu
    with open(output_path, 'w') as f:
        json.dump(menu, f, indent=2)

    print(f"\n✅ Menu saved to: {output_path}")
    print(f"📊 Stats:")
    print(f"   - {len(templates_by_type)} template types")
    print(f"   - {len(placement_map)} unique instances")
    print(f"   - {sum(len(v) for v in templates_by_type.values())} total instances (including duplicates)")

def main():
    # Paths
    game_dir = Path.home() / ".steam/debian-installation/steamapps/common/Menace Demo"
    extracted_data_path = game_dir / "UserData/ExtractedData"
    dump_path = Path("il2cpp_dump/dump.cs")
    output_path = extracted_data_path / "menu.json"

    if not extracted_data_path.exists():
        print(f"❌ Extracted data not found: {extracted_data_path}")
        return 1

    if not dump_path.exists():
        print(f"❌ IL2CPP dump not found: {dump_path}")
        return 1

    build_hierarchical_menu(extracted_data_path, dump_path, output_path)
    return 0

if __name__ == '__main__':
    exit(main())
