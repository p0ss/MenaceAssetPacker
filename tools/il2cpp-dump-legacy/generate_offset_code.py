#!/usr/bin/env python3
"""
Generate C# offset extraction code from IL2CPP dump.cs

Usage:
  python generate_offset_code.py <template_name>

Example:
  python generate_offset_code.py AccessoryTemplate
"""

import re
import sys
from pathlib import Path

def parse_class_from_dump(dump_path, class_name):
    """Extract class definition and fields from dump.cs"""
    with open(dump_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Find the class definition
    class_pattern = rf'public class {class_name}\s.*?\n\{{\n(.*?)\n\}}'
    match = re.search(class_pattern, content, re.DOTALL)

    if not match:
        print(f"Class {class_name} not found in dump.cs")
        return None

    class_body = match.group(1)

    # Extract base class
    base_match = re.search(rf'public class {class_name}.*?:\s+(\w+)', content)
    base_class = base_match.group(1) if base_match else None

    # Parse fields
    fields = []
    field_pattern = r'public\s+([\w<>\[\]\.]+)\s+(\w+);\s+//\s+0x([0-9A-Fa-f]+)'

    for match in re.finditer(field_pattern, class_body):
        field_type = match.group(1)
        field_name = match.group(2)
        offset = match.group(3)

        # Skip internal IL2CPP fields
        if field_name.startswith('NativeFieldInfoPtr') or \
           field_name.startswith('Il2Cpp') or \
           'k__BackingField' in field_name:
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

def get_csharp_type_reader(field_type, offset_expr):
    """Generate appropriate Marshal read code for a field type"""

    # Handle arrays and collections
    if '[]' in field_type or field_type.startswith('List<'):
        return None, f"// TODO: Array/List reading for {field_type}"

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

    # Check if it's an enum
    if not any(c in field_type for c in ['<', '>', '[', '.']) and field_type[0].isupper():
        # Likely an enum, read as int
        return f'Marshal.ReadInt32({offset_expr})', f'// Enum: {field_type}'

    # Unknown type - might be embedded struct
    return None, f'// TODO: Handle {field_type} (possibly embedded struct)'

def generate_extraction_code(class_info):
    """Generate C# extraction code for a template class"""

    code = []
    code.append(f'else if (templateType.Name == "{class_info["name"]}")')
    code.append('{')

    # Track if we need embedded struct handling
    has_embedded_structs = False

    for field in class_info['fields']:
        offset_hex = f'0x{field["offset"]}'
        reader_code, comment = get_csharp_type_reader(field['type'], f'ptr + {offset_hex}')

        if reader_code:
            indent = '    '
            if comment:
                code.append(f'{indent}{comment}')
            code.append(f'{indent}data["{field["name"]}"] = {reader_code};')
        elif comment:
            code.append(f'    {comment}')
            if 'embedded struct' in comment.lower():
                has_embedded_structs = True

    code.append('}')

    return '\n'.join(code), has_embedded_structs

def main():
    if len(sys.argv) < 2:
        print("Usage: python generate_offset_code.py <TemplateName>")
        print("Example: python generate_offset_code.py AccessoryTemplate")
        sys.exit(1)

    template_name = sys.argv[1]
    dump_path = Path('il2cpp_dump/dump.cs')

    if not dump_path.exists():
        print(f"Error: {dump_path} not found")
        sys.exit(1)

    print(f"Parsing {template_name} from IL2CPP dump...")
    class_info = parse_class_from_dump(dump_path, template_name)

    if not class_info:
        sys.exit(1)

    print(f"\nFound {len(class_info['fields'])} fields")
    if class_info['base']:
        print(f"Base class: {class_info['base']}")

    print(f"\n// Generated extraction code for {template_name}:")
    print("// Copy this into DataExtractorMod.cs ExtractTemplateDataDirect() method\n")

    code, has_embedded = generate_extraction_code(class_info)
    print(code)

    if has_embedded:
        print("\n// WARNING: This template has embedded structs that need manual handling")
        print("// For embedded structs, subtract 0x10 from offsets (IL2CPP object header)")

    # Also print the fields for reference
    print(f"\n// Field reference for {template_name}:")
    for field in class_info['fields']:
        print(f"// {field['name']}: 0x{field['offset']} ({field['type']})")

if __name__ == '__main__':
    main()
