#!/usr/bin/env python3
"""
Generate a complete SDK from IL2CPP dump.cs.

This parses ALL classes, methods, properties, and fields from the dump and generates:
1. A comprehensive manifest (JSON) of the entire API surface
2. C# wrapper classes with safe accessors
3. Hook registration helpers for all public methods

Usage:
  python tools/generate_sdk.py                           # analyze only
  python tools/generate_sdk.py --generate               # generate C# SDK
  python tools/generate_sdk.py --namespace Menace.Tactical.AI  # filter to namespace
"""

import argparse
import json
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field, asdict
from pathlib import Path
from typing import Optional


# ---------------------------------------------------------------------------
# Data structures
# ---------------------------------------------------------------------------

@dataclass
class FieldInfo:
    name: str
    type: str
    offset: str
    is_static: bool = False


@dataclass
class PropertyInfo:
    name: str
    type: str
    has_getter: bool = False
    has_setter: bool = False


@dataclass
class ParameterInfo:
    name: str
    type: str


@dataclass
class MethodInfo:
    name: str
    return_type: str
    parameters: list  # List[ParameterInfo]
    is_static: bool = False
    is_virtual: bool = False
    rva: str = ""


@dataclass
class ClassInfo:
    name: str
    namespace: str
    full_name: str
    base_class: Optional[str] = None
    is_abstract: bool = False
    is_sealed: bool = False
    is_struct: bool = False
    is_enum: bool = False
    is_interface: bool = False
    fields: list = field(default_factory=list)  # List[FieldInfo]
    properties: list = field(default_factory=list)  # List[PropertyInfo]
    methods: list = field(default_factory=list)  # List[MethodInfo]
    enum_values: dict = field(default_factory=dict)  # name -> value


# ---------------------------------------------------------------------------
# Parsing
# ---------------------------------------------------------------------------

def parse_dump(content: str, namespace_filter: Optional[str] = None) -> dict:
    """Parse all types from dump.cs into ClassInfo objects."""

    classes = {}
    current_namespace = ""

    # Split into chunks by type definition
    lines = content.split('\n')
    i = 0

    while i < len(lines):
        line = lines[i]

        # Track namespace
        if line.startswith("// Namespace:"):
            current_namespace = line.replace("// Namespace:", "").strip()
            i += 1
            continue

        # Skip if namespace filter doesn't match
        if namespace_filter and not current_namespace.startswith(namespace_filter):
            i += 1
            continue

        # Look for type definitions
        type_match = re.match(
            r'^(public |private |internal |protected )?(abstract |sealed |static )*'
            r'(class|struct|enum|interface)\s+(\w+)(?:<[^>]+>)?'
            r'(?:\s*:\s*([^\n{]+))?',
            line
        )

        if type_match:
            modifiers = (type_match.group(1) or "") + (type_match.group(2) or "")
            kind = type_match.group(3)
            name = type_match.group(4)
            base_part = type_match.group(5)

            # Parse base class (first item before comma)
            base_class = None
            if base_part:
                bases = [b.strip() for b in base_part.split(',')]
                if bases and not bases[0].startswith('I') or kind == 'class':
                    base_class = bases[0].split('<')[0].strip()

            full_name = f"{current_namespace}.{name}" if current_namespace else name

            cls = ClassInfo(
                name=name,
                namespace=current_namespace,
                full_name=full_name,
                base_class=base_class,
                is_abstract='abstract' in modifiers,
                is_sealed='sealed' in modifiers,
                is_struct=(kind == 'struct'),
                is_enum=(kind == 'enum'),
                is_interface=(kind == 'interface'),
            )

            # Find the opening brace and parse body
            while i < len(lines) and '{' not in lines[i]:
                i += 1

            if i < len(lines):
                # Parse the body
                brace_count = 1
                body_start = i + 1
                i += 1

                while i < len(lines) and brace_count > 0:
                    brace_count += lines[i].count('{') - lines[i].count('}')
                    i += 1

                body_lines = lines[body_start:i-1]
                body = '\n'.join(body_lines)

                if cls.is_enum:
                    parse_enum_body(cls, body)
                else:
                    parse_class_body(cls, body)

                classes[full_name] = cls
        else:
            i += 1

    return classes


def parse_enum_body(cls: ClassInfo, body: str):
    """Parse enum values from body."""
    pattern = r'public const \w+ (\w+) = (-?\d+);'
    for m in re.finditer(pattern, body):
        cls.enum_values[m.group(1)] = int(m.group(2))


def parse_class_body(cls: ClassInfo, body: str):
    """Parse fields, properties, and methods from class body."""

    # Fields: public Type name; // 0xNN
    field_pattern = r'(public|private|protected|internal)\s+(static\s+)?(readonly\s+)?([\w<>\[\],\s\.]+?)\s+(\w+);\s*//\s*0x([0-9A-Fa-f]+)'
    for m in re.finditer(field_pattern, body):
        visibility = m.group(1)
        is_static = m.group(2) is not None
        field_type = m.group(4).strip()
        field_name = m.group(5)
        offset = m.group(6)

        # Skip compiler-generated
        if 'k__BackingField' in field_name or field_name.startswith('NativeFieldInfo'):
            continue

        if visibility == 'public':
            cls.fields.append(FieldInfo(
                name=field_name,
                type=field_type,
                offset=f"0x{offset}",
                is_static=is_static
            ))

    # Properties: public Type Name { get; set; }
    prop_pattern = r'public\s+([\w<>\[\],\s\.]+?)\s+(\w+)\s*\{'
    for m in re.finditer(prop_pattern, body):
        prop_type = m.group(1).strip()
        prop_name = m.group(2)

        # Find the property body to check for get/set
        prop_start = m.end()
        brace_count = 1
        prop_end = prop_start
        while prop_end < len(body) and brace_count > 0:
            if body[prop_end] == '{':
                brace_count += 1
            elif body[prop_end] == '}':
                brace_count -= 1
            prop_end += 1

        prop_body = body[prop_start:prop_end]

        cls.properties.append(PropertyInfo(
            name=prop_name,
            type=prop_type,
            has_getter='get' in prop_body or 'get_' in prop_body,
            has_setter='set' in prop_body or 'set_' in prop_body
        ))

    # Methods: // RVA: 0x... \n public ReturnType MethodName(params) { }
    method_pattern = r'// RVA: (0x[0-9A-Fa-f]+).*?\n\s*(?:\[.*?\]\n\s*)*(public|private|protected)\s+(static\s+)?(virtual\s+)?([\w<>\[\],\s\.]+?)\s+(\w+)\s*\(([^)]*)\)'
    for m in re.finditer(method_pattern, body):
        rva = m.group(1)
        visibility = m.group(2)
        is_static = m.group(3) is not None
        is_virtual = m.group(4) is not None
        return_type = m.group(5).strip()
        method_name = m.group(6)
        params_str = m.group(7)

        # Skip compiler-generated
        if method_name.startswith('<') or method_name in ('.ctor', '.cctor'):
            continue

        # Parse parameters
        parameters = []
        if params_str.strip():
            for param in params_str.split(','):
                param = param.strip()
                if param:
                    parts = param.rsplit(' ', 1)
                    if len(parts) == 2:
                        parameters.append(ParameterInfo(
                            name=parts[1],
                            type=parts[0].strip()
                        ))

        if visibility == 'public':
            cls.methods.append(MethodInfo(
                name=method_name,
                return_type=return_type,
                parameters=parameters,
                is_static=is_static,
                is_virtual=is_virtual,
                rva=rva
            ))


# ---------------------------------------------------------------------------
# Analysis
# ---------------------------------------------------------------------------

def analyze_api(classes: dict) -> dict:
    """Analyze the parsed classes and produce statistics."""

    stats = {
        'total_types': len(classes),
        'classes': 0,
        'structs': 0,
        'enums': 0,
        'interfaces': 0,
        'total_fields': 0,
        'total_properties': 0,
        'total_methods': 0,
        'hookable_methods': 0,
        'namespaces': defaultdict(int),
        'base_classes': defaultdict(int),
    }

    for cls in classes.values():
        if cls.is_enum:
            stats['enums'] += 1
        elif cls.is_struct:
            stats['structs'] += 1
        elif cls.is_interface:
            stats['interfaces'] += 1
        else:
            stats['classes'] += 1

        stats['total_fields'] += len(cls.fields)
        stats['total_properties'] += len(cls.properties)
        stats['total_methods'] += len(cls.methods)
        stats['hookable_methods'] += sum(1 for m in cls.methods if not m.is_static)

        ns = cls.namespace or "(global)"
        stats['namespaces'][ns] += 1

        if cls.base_class:
            stats['base_classes'][cls.base_class] += 1

    # Convert defaultdicts to regular dicts for JSON
    stats['namespaces'] = dict(stats['namespaces'])
    stats['base_classes'] = dict(sorted(
        stats['base_classes'].items(),
        key=lambda x: -x[1]
    )[:20])  # Top 20

    return stats


def generate_manifest(classes: dict) -> dict:
    """Generate a JSON manifest of the entire API."""

    def serialize_class(cls: ClassInfo) -> dict:
        return {
            'name': cls.name,
            'namespace': cls.namespace,
            'base': cls.base_class,
            'kind': 'enum' if cls.is_enum else 'struct' if cls.is_struct else 'interface' if cls.is_interface else 'class',
            'abstract': cls.is_abstract,
            'fields': [asdict(f) for f in cls.fields],
            'properties': [asdict(p) for p in cls.properties],
            'methods': [
                {
                    'name': m.name,
                    'returns': m.return_type,
                    'params': [asdict(p) for p in m.parameters],
                    'static': m.is_static,
                    'virtual': m.is_virtual,
                }
                for m in cls.methods
            ],
            'enum_values': cls.enum_values if cls.is_enum else None,
        }

    return {
        'version': '1.0.0',
        'types': {name: serialize_class(cls) for name, cls in classes.items()}
    }


# ---------------------------------------------------------------------------
# C# Generation
# ---------------------------------------------------------------------------

def csharp_safe_type(il2cpp_type: str) -> str:
    """Convert IL2CPP type to safe C# type."""
    # Handle common IL2CPP types
    mappings = {
        'Int32': 'int',
        'Int64': 'long',
        'Int16': 'short',
        'Single': 'float',
        'Double': 'double',
        'Boolean': 'bool',
        'Byte': 'byte',
        'String': 'string',
        'Void': 'void',
    }

    for old, new in mappings.items():
        il2cpp_type = il2cpp_type.replace(old, new)

    return il2cpp_type


def generate_wrapper_class(cls: ClassInfo) -> str:
    """Generate a C# wrapper class for safe access."""

    if cls.is_enum or cls.is_interface:
        return ""

    lines = []
    safe_name = f"{cls.name}Safe"

    lines.append(f"/// <summary>Safe wrapper for {cls.full_name}</summary>")
    lines.append(f"public class {safe_name} : SafeGameObject")
    lines.append("{")
    lines.append(f"    private readonly {cls.name} _obj;")
    lines.append("")
    lines.append(f"    public {safe_name}({cls.name} obj) : base(obj) {{ _obj = obj; }}")
    lines.append(f"    public {safe_name}(IntPtr ptr) : base(ptr) {{ _obj = new {cls.name}(ptr); }}")
    lines.append("")

    # Generate property accessors
    for prop in cls.properties:
        safe_type = csharp_safe_type(prop.type)
        if prop.has_getter:
            lines.append(f"    public {safe_type} {prop.name} => SafeRead(() => _obj.{prop.name});")

    lines.append("")

    # Generate method wrappers
    for method in cls.methods:
        if method.is_static:
            continue

        safe_return = csharp_safe_type(method.return_type)
        params_def = ", ".join(
            f"{csharp_safe_type(p.type)} {p.name}"
            for p in method.parameters
        )
        params_call = ", ".join(p.name for p in method.parameters)

        if safe_return == "void":
            lines.append(f"    public void {method.name}({params_def}) => SafeCall(() => _obj.{method.name}({params_call}));")
        else:
            lines.append(f"    public {safe_return} {method.name}({params_def}) => SafeCall(() => _obj.{method.name}({params_call}));")

    lines.append("}")
    lines.append("")

    return "\n".join(lines)


def generate_hooks_class(cls: ClassInfo) -> str:
    """Generate hook registration for a class."""

    if cls.is_enum or cls.is_interface or cls.is_struct:
        return ""

    hookable = [m for m in cls.methods if not m.is_static]
    if not hookable:
        return ""

    lines = []
    lines.append(f"/// <summary>Hooks for {cls.full_name}</summary>")
    lines.append(f"public static class {cls.name}Hooks")
    lines.append("{")

    for method in hookable:
        safe_return = csharp_safe_type(method.return_type)
        params_types = ", ".join(
            csharp_safe_type(p.type) for p in method.parameters
        )

        # Event delegates
        if safe_return == "void":
            lines.append(f"    public static event Action<{cls.name}Safe{', ' + params_types if params_types else ''}> Before{method.name};")
            lines.append(f"    public static event Action<{cls.name}Safe{', ' + params_types if params_types else ''}> After{method.name};")
        else:
            lines.append(f"    public static event Action<{cls.name}Safe{', ' + params_types if params_types else ''}> Before{method.name};")
            lines.append(f"    public static event Func<{cls.name}Safe, {params_types + ', ' if params_types else ''}{safe_return}, {safe_return}> After{method.name};")

    lines.append("}")
    lines.append("")

    return "\n".join(lines)


def generate_sdk_file(classes: dict, namespace_filter: str) -> str:
    """Generate a complete SDK C# file."""

    lines = []
    lines.append("// Auto-generated SDK - DO NOT EDIT")
    lines.append("// Generated from IL2CPP dump.cs")
    lines.append("")
    lines.append("using System;")
    lines.append("using UnityEngine;")
    lines.append("using Il2CppInterop.Runtime;")
    lines.append("")
    lines.append("namespace Menace.SDK.Generated")
    lines.append("{")
    lines.append("    // Base class for safe wrappers")
    lines.append("    public abstract class SafeGameObject")
    lines.append("    {")
    lines.append("        protected readonly IntPtr Pointer;")
    lines.append("        protected SafeGameObject(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase obj) { Pointer = obj?.Pointer ?? IntPtr.Zero; }")
    lines.append("        protected SafeGameObject(IntPtr ptr) { Pointer = ptr; }")
    lines.append("        public bool IsValid => Pointer != IntPtr.Zero && IsNativeAlive(Pointer);")
    lines.append("        ")
    lines.append("        protected T SafeRead<T>(Func<T> getter, T fallback = default)")
    lines.append("        {")
    lines.append("            if (!IsValid) return fallback;")
    lines.append("            try { return getter(); }")
    lines.append("            catch { return fallback; }")
    lines.append("        }")
    lines.append("        ")
    lines.append("        protected void SafeCall(Action action)")
    lines.append("        {")
    lines.append("            if (!IsValid) return;")
    lines.append("            try { action(); }")
    lines.append("            catch { }")
    lines.append("        }")
    lines.append("        ")
    lines.append("        protected T SafeCall<T>(Func<T> func, T fallback = default)")
    lines.append("        {")
    lines.append("            if (!IsValid) return fallback;")
    lines.append("            try { return func(); }")
    lines.append("            catch { return fallback; }")
    lines.append("        }")
    lines.append("        ")
    lines.append("        private static bool IsNativeAlive(IntPtr ptr)")
    lines.append("        {")
    lines.append("            // TODO: Check m_CachedPtr offset")
    lines.append("            return ptr != IntPtr.Zero;")
    lines.append("        }")
    lines.append("    }")
    lines.append("")

    # Generate wrappers for each class
    for cls in sorted(classes.values(), key=lambda c: c.full_name):
        wrapper = generate_wrapper_class(cls)
        if wrapper:
            for line in wrapper.split('\n'):
                lines.append("    " + line)

    # Generate hooks
    lines.append("    // ═══════════════════════════════════════════════════════")
    lines.append("    // Hook Registration")
    lines.append("    // ═══════════════════════════════════════════════════════")
    lines.append("")

    for cls in sorted(classes.values(), key=lambda c: c.full_name):
        hooks = generate_hooks_class(cls)
        if hooks:
            for line in hooks.split('\n'):
                lines.append("    " + line)

    lines.append("}")

    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Generate SDK from IL2CPP dump")
    parser.add_argument("--dump", default="il2cpp_dump/dump.cs", help="Path to dump.cs")
    parser.add_argument("--namespace", help="Filter to specific namespace (e.g., Menace.Tactical.AI)")
    parser.add_argument("--generate", action="store_true", help="Generate C# SDK files")
    parser.add_argument("--output", default="generated/sdk", help="Output directory")
    parser.add_argument("--manifest", action="store_true", help="Output JSON manifest")
    args = parser.parse_args()

    dump_path = Path(args.dump)
    if not dump_path.exists():
        print(f"Error: {dump_path} not found", file=sys.stderr)
        return 1

    print(f"Parsing {dump_path}...")
    content = dump_path.read_text(encoding='utf-8')

    classes = parse_dump(content, args.namespace)
    print(f"Parsed {len(classes)} types")

    # Analyze
    stats = analyze_api(classes)

    print(f"\n{'='*60}")
    print("API Surface Analysis")
    print(f"{'='*60}")
    print(f"Total types:       {stats['total_types']}")
    print(f"  Classes:         {stats['classes']}")
    print(f"  Structs:         {stats['structs']}")
    print(f"  Enums:           {stats['enums']}")
    print(f"  Interfaces:      {stats['interfaces']}")
    print(f"Total fields:      {stats['total_fields']}")
    print(f"Total properties:  {stats['total_properties']}")
    print(f"Total methods:     {stats['total_methods']}")
    print(f"Hookable methods:  {stats['hookable_methods']}")
    print(f"\nTop namespaces:")
    for ns, count in sorted(stats['namespaces'].items(), key=lambda x: -x[1])[:15]:
        print(f"  {ns}: {count}")
    print(f"\nTop base classes:")
    for base, count in list(stats['base_classes'].items())[:10]:
        print(f"  {base}: {count}")

    # Output manifest
    if args.manifest:
        output_dir = Path(args.output)
        output_dir.mkdir(parents=True, exist_ok=True)

        manifest = generate_manifest(classes)
        manifest_path = output_dir / "api_manifest.json"

        print(f"\nWriting manifest to {manifest_path}...")
        with open(manifest_path, 'w') as f:
            json.dump(manifest, f, indent=2)

    # Generate C#
    if args.generate:
        output_dir = Path(args.output)
        output_dir.mkdir(parents=True, exist_ok=True)

        sdk_code = generate_sdk_file(classes, args.namespace or "")
        sdk_path = output_dir / "GeneratedSDK.cs"

        print(f"\nWriting SDK to {sdk_path}...")
        with open(sdk_path, 'w') as f:
            f.write(sdk_code)

        print(f"Generated {len([c for c in classes.values() if not c.is_enum and not c.is_interface])} wrapper classes")

    return 0


if __name__ == "__main__":
    sys.exit(main())
