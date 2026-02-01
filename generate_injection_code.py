#!/usr/bin/env python3
"""
Auto-generate injection code to apply template modifications back into the game.

This generates Marshal.WriteXXX code that mirrors the extraction code,
allowing modified template data to be written back into IL2CPP memory.

Usage:
  python generate_injection_code.py
  python generate_injection_code.py --from-schema schema.json
"""

import argparse
import json
import re
from pathlib import Path

def parse_extraction_code(content):
    """Parse generated_extraction_code.cs to extract field information"""
    templates = {}
    current_template = None

    # Find all template blocks
    template_pattern = r'else if \(templateType\.Name == "(\w+)"\)'
    field_pattern = r'data\["(\w+)"\] = (Marshal\.\w+|BitConverter\.\w+)\(ptr \+ (0x[0-9A-Fa-f]+)'

    for line in content.split('\n'):
        template_match = re.search(template_pattern, line)
        if template_match:
            current_template = template_match.group(1)
            templates[current_template] = []
            continue

        if current_template:
            field_match = re.search(field_pattern, line)
            if field_match:
                field_name = field_match.group(1)
                read_method = field_match.group(2)
                offset = field_match.group(3)

                # Determine type from read method
                field_type = get_type_from_read_method(read_method)

                templates[current_template].append({
                    'name': field_name,
                    'type': field_type,
                    'offset': offset
                })

    return templates

def get_type_from_read_method(read_method):
    """Determine C# type from Marshal read method"""
    type_map = {
        'Marshal.ReadInt32': 'int',
        'Marshal.ReadInt16': 'short',
        'Marshal.ReadInt64': 'long',
        'Marshal.ReadByte': 'byte',
        'BitConverter.ToSingle': 'float',
        'BitConverter.Int64BitsToDouble': 'double',
    }

    for method, csharp_type in type_map.items():
        if method in read_method:
            return csharp_type

    return 'int'  # Default

def get_write_method(field_type, ptr_expr, value_expr):
    """Generate Marshal write code for a field type"""
    write_map = {
        'int': f'Marshal.WriteInt32({ptr_expr}, {value_expr})',
        'short': f'Marshal.WriteInt16({ptr_expr}, {value_expr})',
        'long': f'Marshal.WriteInt64({ptr_expr}, {value_expr})',
        'byte': f'Marshal.WriteByte({ptr_expr}, {value_expr})',
        'float': f'Marshal.WriteInt32({ptr_expr}, BitConverter.ToInt32(BitConverter.GetBytes({value_expr}), 0))',
        'double': f'Marshal.WriteInt64({ptr_expr}, BitConverter.DoubleToInt64Bits({value_expr}))',
    }

    return write_map.get(field_type, f'Marshal.WriteInt32({ptr_expr}, {value_expr})')

def generate_injection_method(templates):
    """Generate the ApplyTemplateModifications method"""
    lines = []
    lines.append("// Auto-generated template injection code")
    lines.append("// Writes modified template data back into IL2CPP memory")
    lines.append("")
    lines.append("private void ApplyTemplateModifications(UnityEngine.Object obj, Type templateType, Dictionary<string, object> modifications)")
    lines.append("{")
    lines.append("    // Get IL2CPP pointer from the object")
    lines.append("    IntPtr ptr = IntPtr.Zero;")
    lines.append("    if (obj is Il2CppObjectBase il2cppObj)")
    lines.append("    {")
    lines.append("        ptr = il2cppObj.Pointer;")
    lines.append("    }")
    lines.append("    else")
    lines.append("    {")
    lines.append("        LoggerInstance.Error($\"Cannot apply modifications: Object {obj.name} is not Il2CppObjectBase\");")
    lines.append("        return;")
    lines.append("    }")
    lines.append("")
    lines.append("    // Template-specific injection")

    first = True
    for template_name, fields in sorted(templates.items()):
        if not fields:
            continue

        if not first:
            lines.append("")

        lines.append(f"            {'else ' if not first else ''}if (templateType.Name == \"{template_name}\")")
        lines.append("            {")

        for field in fields:
            lines.append(f"                if (modifications.ContainsKey(\"{field['name']}\"))")
            lines.append("                {")

            # Convert value to appropriate type
            type_converter_map = {
                'int': 'ToInt32',
                'short': 'ToInt16',
                'long': 'ToInt64',
                'byte': 'ToByte',
                'float': 'ToSingle',
                'double': 'ToDouble'
            }
            converter = type_converter_map.get(field['type'], 'ToInt32')
            convert_expr = f"Convert.{converter}(modifications[\"{field['name']}\"])"

            write_code = get_write_method(field['type'], f"ptr + {field['offset']}", convert_expr)
            lines.append(f"                    {write_code};")
            lines.append("                }")

        lines.append("            }")
        first = False

    lines.append("            else")
    lines.append("            {")
    lines.append("                LoggerInstance.Warning($\"Unknown template type for injection: {templateType.Name}\");")
    lines.append("            }")
    lines.append("")
    lines.append("    LoggerInstance.Msg($\"Applied modifications to {obj.name} ({templateType.Name})\");")
    lines.append("}")

    return '\n'.join(lines)

def load_templates_from_schema(schema_path):
    """Load template field data from schema.json for injection code generation."""
    with open(schema_path, 'r') as f:
        schema = json.load(f)

    # Map schema categories to Marshal types
    schema_type_map = {
        'int': 'int', 'Int32': 'int',
        'float': 'float', 'Single': 'float',
        'bool': 'byte', 'Boolean': 'byte',
        'byte': 'byte', 'Byte': 'byte',
        'short': 'short', 'Int16': 'short',
        'long': 'long', 'Int64': 'long',
        'double': 'double', 'Double': 'double',
    }

    templates = {}
    for tname, tinfo in schema.get('templates', {}).items():
        if tinfo.get('is_abstract', False):
            continue

        fields = []
        for f in tinfo.get('fields', []):
            if f.get('category') in ('collection', 'unity_asset', 'localization',
                                     'unknown', 'string'):
                continue

            marshal_type = schema_type_map.get(f['type'])
            if not marshal_type and f.get('category') in ('enum', 'reference'):
                marshal_type = 'int'
            if not marshal_type:
                continue

            fields.append({
                'name': f['name'],
                'type': marshal_type,
                'offset': f['offset'],
            })

        if fields:
            templates[tname] = fields

    return templates


def main():
    parser = argparse.ArgumentParser(description="Generate template injection code")
    parser.add_argument('--from-schema', dest='schema_path', default=None,
                        help='Read types/fields/offsets from schema.json instead of extraction code')
    args = parser.parse_args()

    if args.schema_path:
        schema_path = Path(args.schema_path)
        if not schema_path.exists():
            print(f"Error: {schema_path} not found")
            return 1

        print(f"Loading templates from schema: {schema_path}")
        templates = load_templates_from_schema(schema_path)
    else:
        extraction_file = Path('generated_extraction_code.cs')

        if not extraction_file.exists():
            print(f"Error: {extraction_file} not found")
            print("Please run generate_all_templates.py first")
            return 1

        print("Reading extraction code...")
        with open(extraction_file, 'r') as f:
            content = f.read()

        print("Parsing field information...")
        templates = parse_extraction_code(content)

    print(f"Found {len(templates)} templates with {sum(len(f) for f in templates.values())} total fields")

    print("Generating injection code...")
    injection_code = generate_injection_method(templates)

    output_file = Path('generated_injection_code.cs')
    with open(output_file, 'w') as f:
        f.write(injection_code)

    print(f"\nGenerated injection code written to: {output_file}")

    return 0

if __name__ == '__main__':
    exit(main())
