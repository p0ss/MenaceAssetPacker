#!/usr/bin/env python3
"""
Auto-generate complete DataExtractor template extraction code from IL2CPP dump.

This tool parses dump.cs and generates the ExtractTemplateDataDirect() method
with offset-based extraction for all template types.

Usage:
  python generate_all_templates.py
"""

import re
from pathlib import Path
from collections import defaultdict

def parse_class_from_dump(content, class_name, allow_abstract=False):
    """Extract class definition and fields from dump.cs"""
    # Try to find public class or public abstract class
    patterns = [
        rf'public class {class_name}\s.*?\n\{{\n(.*?)\n\}}',
        rf'public abstract class {class_name}\s.*?\n\{{\n(.*?)\n\}}'
    ]

    match = None
    for pattern in patterns:
        match = re.search(pattern, content, re.DOTALL)
        if match:
            break

    if not match:
        return None

    class_body = match.group(1)

    # Extract base class - check both concrete and abstract
    base_patterns = [
        rf'public class {class_name}.*?:\s+(\w+)',
        rf'public abstract class {class_name}.*?:\s+(\w+)'
    ]

    base_class = None
    for pattern in base_patterns:
        base_match = re.search(pattern, content)
        if base_match:
            base_class = base_match.group(1)
            break

    # Parse fields
    fields = []
    field_pattern = r'public\s+([\w<>\[\]\.]+)\s+(\w+);\s+//\s+0x([0-9A-Fa-f]+)'

    for match in re.finditer(field_pattern, class_body):
        field_type = match.group(1)
        field_name = match.group(2)
        offset = match.group(3)

        # Skip internal IL2CPP fields and static fields
        if field_name.startswith('NativeFieldInfoPtr') or \
           field_name.startswith('Il2Cpp') or \
           'k__BackingField' in field_name or \
           offset == '0':  # Static fields have 0x0 offset
            continue

        fields.append({
            'type': field_type,
            'name': field_name,
            'offset': offset
        })

    return {
        'name': class_name,
        'base': base_class,
        'fields': fields
    }

def collect_all_fields(content, class_name, visited=None):
    """Recursively collect all fields from a class and its base classes"""
    if visited is None:
        visited = set()

    # Avoid infinite loops
    if class_name in visited:
        return []
    visited.add(class_name)

    # Parse current class
    class_info = parse_class_from_dump(content, class_name, allow_abstract=True)
    if not class_info:
        return []

    all_fields = []

    # First collect base class fields (they come first in memory)
    if class_info['base'] and class_info['base'] not in ['ScriptableObject', 'MonoBehaviour', 'Object', 'DataTemplate']:
        base_fields = collect_all_fields(content, class_info['base'], visited)
        all_fields.extend(base_fields)

    # Then add current class fields
    all_fields.extend(class_info['fields'])

    return all_fields

def get_csharp_type_reader(field_type, offset_expr, field_name):
    """Generate appropriate Marshal read code for a field type"""

    # Handle arrays and collections
    if '[]' in field_type or field_type.startswith('List<'):
        return None, f"// TODO: Array/List - {field_name}: {field_type}"

    # Unity Object reference types that need dereferencing
    unity_object_types = [
        'LocalizedLine', 'LocalizedMultiLine', 'Sprite', 'GameObject',
        'Material', 'Texture2D', 'AudioClip', 'AnimationClip',
        'SkillTemplate', 'TagTemplate', 'EntityTemplate', 'ItemTemplate',
        'WeaponTemplate', 'ArmorTemplate', 'AccessoryTemplate'
    ]

    if field_type in unity_object_types or field_type.endswith('Template'):
        return f'ReadUnityObjectReference(Marshal.ReadIntPtr({offset_expr}))', f'// {field_type} reference'

    # Map IL2CPP types to Marshal read methods
    type_map = {
        'int': f'Marshal.ReadInt32({offset_expr})',
        'Int32': f'Marshal.ReadInt32({offset_expr})',
        'float': f'BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32({offset_expr})), 0)',
        'Single': f'BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32({offset_expr})), 0)',
        'bool': f'Marshal.ReadByte({offset_expr}) != 0',
        'Boolean': f'Marshal.ReadByte({offset_expr}) != 0',
        'byte': f'Marshal.ReadByte({offset_expr})',
        'Byte': f'Marshal.ReadByte({offset_expr})',
        'short': f'Marshal.ReadInt16({offset_expr})',
        'Int16': f'Marshal.ReadInt16({offset_expr})',
        'long': f'Marshal.ReadInt64({offset_expr})',
        'Int64': f'Marshal.ReadInt64({offset_expr})',
        'double': f'BitConverter.Int64BitsToDouble(Marshal.ReadInt64({offset_expr}))',
        'Double': f'BitConverter.Int64BitsToDouble(Marshal.ReadInt64({offset_expr}))',
        'string': f'// TODO: String reading',
        'String': f'// TODO: String reading',
    }

    # Check for exact match
    if field_type in type_map:
        return type_map[field_type], None

    # Check if it's likely an enum or struct
    if not any(c in field_type for c in ['<', '>', '[', '.']) and field_type[0].isupper():
        # Likely an enum or small struct, try reading as int
        return f'Marshal.ReadInt32({offset_expr})', f'// {field_type}'

    # Unknown type - might be embedded struct or reference
    return None, f'// TODO: Complex type - {field_name}: {field_type}'

def generate_template_extraction(class_info, is_first=False, indent='            '):
    """Generate C# extraction code for a template class"""
    if not class_info or not class_info['fields']:
        return None

    lines = []
    if_keyword = 'if' if is_first else 'else if'
    lines.append(f'{indent}{if_keyword} (templateType.Name == "{class_info["name"]}")')
    lines.append(f'{indent}{{')

    for field in class_info['fields']:
        offset_hex = f'0x{field["offset"]}'
        reader_code, comment = get_csharp_type_reader(
            field['type'],
            f'ptr + {offset_hex}',
            field['name']
        )

        if reader_code:
            if comment:
                lines.append(f'{indent}    {comment}')
            lines.append(f'{indent}    data["{field["name"]}"] = {reader_code};')
        elif comment:
            lines.append(f'{indent}    {comment}')

    lines.append(f'{indent}}}')
    return '\n'.join(lines)

def main():
    dump_path = Path('il2cpp_dump/dump.cs')

    if not dump_path.exists():
        print(f"Error: {dump_path} not found")
        print("Please ensure the IL2CPP dump is at il2cpp_dump/dump.cs")
        return 1

    print("Reading IL2CPP dump...")
    with open(dump_path, 'r', encoding='utf-8') as f:
        content = f.read()

    print("Finding all Template classes...")

    # Find all template classes
    template_pattern = r'public class (\w+Template)\s'
    template_classes = set(re.findall(template_pattern, content))

    # Filter out nested/internal templates
    template_classes = [t for t in sorted(template_classes)
                       if '.' not in t and 'Uxml' not in t and 'DataTemplateLoader' != t]

    print(f"Found {len(template_classes)} template classes")

    # Parse all templates and collect all fields (including inherited)
    templates_with_fields = []
    templates_without_fields = []

    for template_name in template_classes:
        all_fields = collect_all_fields(content, template_name)
        if all_fields:
            # Get base class info for the template
            class_info = parse_class_from_dump(content, template_name)
            templates_with_fields.append({
                'name': template_name,
                'base': class_info['base'] if class_info else None,
                'fields': all_fields  # Now includes inherited fields!
            })
        else:
            templates_without_fields.append(template_name)

    print(f"{len(templates_with_fields)} templates have fields (including inherited)")
    print(f"{len(templates_without_fields)} templates have no fields")

    # Generate the complete extraction method
    output = []
    output.append("// Auto-generated template extraction code")
    output.append("// Generated from IL2CPP dump.cs")
    output.append("")
    output.append("private object ExtractTemplateDataDirect(UnityEngine.Object obj, Type templateType)")
    output.append("{")
    output.append("    var data = new Dictionary<string, object>();")
    output.append("")
    output.append("    // Get IL2CPP pointer from the object")
    output.append("    IntPtr ptr = IntPtr.Zero;")
    output.append("    if (obj is Il2CppObjectBase il2cppObj)")
    output.append("    {")
    output.append("        ptr = il2cppObj.Pointer;")
    output.append("    }")
    output.append("    else")
    output.append("    {")
    output.append("        data[\"name\"] = $\"ERROR: Object is not Il2CppObjectBase, type is {obj.GetType().Name}\";")
    output.append("        return data;")
    output.append("    }")
    output.append("")
    output.append("    data[\"name\"] = obj.name;")
    output.append("")
    output.append("    // Template-specific extraction")

    first = True
    for class_info in templates_with_fields:
        code = generate_template_extraction(class_info, is_first=first)
        if code:
            if not first:
                output.append("")
            output.append(code)
            first = False

    output.append("            else")
    output.append("            {")
    output.append("                // Unknown template type - return basic info only")
    output.append("                data[\"_template_type\"] = templateType.Name;")
    output.append("            }")
    output.append("")
    output.append("    return data;")
    output.append("}")

    # Write the generated code
    output_file = Path('generated_extraction_code.cs')
    with open(output_file, 'w') as f:
        f.write('\n'.join(output))

    print(f"\n✅ Generated extraction code written to: {output_file}")
    print(f"\n📋 Summary:")
    print(f"   - {len(templates_with_fields)} templates with extraction code")
    print(f"   - {len(templates_without_fields)} templates without fields")

    if templates_without_fields:
        print(f"\n⚠️  Templates with no fields (check base classes):")
        for t in templates_without_fields[:10]:
            print(f"   - {t}")
        if len(templates_without_fields) > 10:
            print(f"   ... and {len(templates_without_fields) - 10} more")

    print(f"\n💡 Next steps:")
    print(f"   1. Review {output_file}")
    print(f"   2. Copy the method into DataExtractorMod.cs")
    print(f"   3. Handle TODO items for complex types")
    print(f"   4. Test with the game")

    return 0

if __name__ == '__main__':
    exit(main())
