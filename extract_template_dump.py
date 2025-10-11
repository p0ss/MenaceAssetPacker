#!/usr/bin/env python3
"""
Extract minimal IL2CPP dump containing only Template classes.

This significantly reduces dump size from ~35MB to ~500KB for faster processing.
Used by the orchestration layer to regenerate extraction code on game updates.
"""

import re
import sys
from pathlib import Path

def extract_template_classes(full_dump_path, output_path):
    """Extract only Template class definitions from full dump"""

    print(f"Reading full IL2CPP dump from: {full_dump_path}")
    with open(full_dump_path, 'r', encoding='utf-8') as f:
        content = f.read()

    print("Extracting Template classes...")

    # Find all template class definitions with their complete bodies
    template_pattern = r'(public class \w+Template\s.*?\n\{(?:\n.*?)*?\n\})'
    matches = re.findall(template_pattern, content, re.MULTILINE)

    # Filter out nested/internal templates
    filtered_classes = []
    template_names = []

    for match in matches:
        # Extract class name
        name_match = re.search(r'public class (\w+Template)', match)
        if name_match:
            name = name_match.group(1)
            # Skip utility templates
            if 'Uxml' not in name and name != 'DataTemplateLoader':
                filtered_classes.append(match)
                template_names.append(name)

    print(f"Found {len(filtered_classes)} Template classes")

    # Build minimal dump
    minimal_dump = []
    minimal_dump.append("// Minimal IL2CPP Dump - Template Classes Only")
    minimal_dump.append("// Auto-generated for fast template extraction code generation")
    minimal_dump.append(f"// Contains {len(filtered_classes)} template classes")
    minimal_dump.append("")
    minimal_dump.append("// Template Classes:")
    for name in sorted(template_names):
        minimal_dump.append(f"//   - {name}")
    minimal_dump.append("")
    minimal_dump.append("")

    # Add all template classes
    for class_def in filtered_classes:
        minimal_dump.append(class_def)
        minimal_dump.append("")

    # Write minimal dump
    output_content = '\n'.join(minimal_dump)
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(output_content)

    original_size = Path(full_dump_path).stat().st_size
    minimal_size = Path(output_path).stat().st_size
    reduction = (1 - minimal_size / original_size) * 100

    print(f"\n✅ Minimal dump created: {output_path}")
    print(f"   Original size: {original_size / 1024 / 1024:.1f} MB")
    print(f"   Minimal size:  {minimal_size / 1024:.1f} KB")
    print(f"   Reduction:     {reduction:.1f}%")

    return template_names

def main():
    if len(sys.argv) < 3:
        print("Usage: python extract_template_dump.py <full_dump.cs> <output_minimal.dump>")
        print("\nExample:")
        print("  python extract_template_dump.py il2cpp_dump/dump.cs il2cpp_templates.dump")
        return 1

    full_dump_path = sys.argv[1]
    output_path = sys.argv[2]

    if not Path(full_dump_path).exists():
        print(f"Error: Full dump not found: {full_dump_path}")
        return 1

    extract_template_classes(full_dump_path, output_path)
    return 0

if __name__ == '__main__':
    exit(main())
