#!/usr/bin/env python3
"""
Generate a structured schema.json from an IL2CPP dump.cs file.

This replaces scattered regex parsing across multiple Python scripts with a
single source of truth that describes all template types, fields, enums,
structs, and relationships.

Usage:
  python generate_schema.py [dump_path] [output_path]
  python generate_schema.py                                                # defaults: il2cpp_dump/dump.cs -> generated/schema.json
  python generate_schema.py il2cpp_dump/dump.cs generated/schema.json
"""

import argparse
import hashlib
import json
import re
import sys
from collections import defaultdict
from pathlib import Path


# ---------------------------------------------------------------------------
# Parsing helpers
# ---------------------------------------------------------------------------

def compute_file_hash(path):
    """SHA-256 of the dump file for version tracking."""
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def parse_class_from_dump(content, class_name, allow_abstract=False):
    """Extract class definition and fields from dump.cs."""
    patterns = [
        rf"public class {re.escape(class_name)}\s.*?\n\{{\n(.*?)\n\}}",
        rf"public abstract class {re.escape(class_name)}\s.*?\n\{{\n(.*?)\n\}}",
    ]

    match = None
    for pattern in patterns:
        match = re.search(pattern, content, re.DOTALL)
        if match:
            break

    if not match:
        return None

    class_body = match.group(1)

    # Check if abstract
    is_abstract = bool(re.search(
        rf"public abstract class {re.escape(class_name)}\s", content))

    # Extract base class
    base_patterns = [
        rf"public class {re.escape(class_name)}.*?:\s+(\w+)",
        rf"public abstract class {re.escape(class_name)}.*?:\s+(\w+)",
    ]
    base_class = None
    for pattern in base_patterns:
        base_match = re.search(pattern, content)
        if base_match:
            base_class = base_match.group(1)
            break

    # Parse fields (both public and private - private with [SerializeField] are Unity-serialized)
    fields = []
    field_pattern = r"(?:public|private)\s+([\w<>\[\]\.]+)\s+(\w+);\s+//\s+0x([0-9A-Fa-f]+)"

    for m in re.finditer(field_pattern, class_body):
        field_type = m.group(1)
        field_name = m.group(2)
        offset = m.group(3)

        # Skip internal IL2CPP fields
        if (field_name.startswith("NativeFieldInfoPtr") or
                field_name.startswith("Il2Cpp") or
                "k__BackingField" in field_name or
                offset == "0"):  # static fields
            continue

        fields.append({
            "type": field_type,
            "name": field_name,
            "offset": f"0x{offset}",
        })

    return {
        "name": class_name,
        "base": base_class,
        "is_abstract": is_abstract,
        "fields": fields,
    }


def collect_all_fields(content, class_name, visited=None):
    """Recursively collect all fields from a class and its base classes."""
    if visited is None:
        visited = set()
    if class_name in visited:
        return []
    visited.add(class_name)

    class_info = parse_class_from_dump(content, class_name, allow_abstract=True)
    if not class_info:
        return []

    all_fields = []

    stop_bases = {"ScriptableObject", "MonoBehaviour", "Object",
                  "SerializedScriptableObject"}
    if class_info["base"] and class_info["base"] not in stop_bases:
        all_fields.extend(collect_all_fields(content, class_info["base"], visited))

    all_fields.extend(class_info["fields"])
    return all_fields


def get_inheritance_chain(class_name, hierarchy):
    """Get full inheritance chain root -> ... -> leaf."""
    chain = [class_name]
    current = class_name
    while current in hierarchy and hierarchy[current]:
        parent = hierarchy[current]
        if parent in chain:
            break
        chain.append(parent)
        current = parent
    return list(reversed(chain))


# ---------------------------------------------------------------------------
# Type classification
# ---------------------------------------------------------------------------

PRIMITIVE_TYPES = {
    "int", "Int32", "float", "Single", "bool", "Boolean",
    "byte", "Byte", "short", "Int16", "long", "Int64",
    "double", "Double",
}

UNITY_ASSET_TYPES = {
    "Sprite", "Texture2D", "Material", "Mesh", "AudioClip",
    "AnimationClip", "GameObject", "RuntimeAnimatorController",
}

LOCALIZATION_TYPES = {"LocalizedLine", "LocalizedMultiLine"}


def classify_field(field_type, known_enums, known_structs, known_templates):
    """Classify a field type into a category."""
    base = field_type.rstrip("[]")

    if base in PRIMITIVE_TYPES:
        return "primitive", None

    if base in ("string", "String"):
        return "string", None

    if base in known_enums:
        return "enum", None

    if base in known_structs:
        return "struct", None

    if base in LOCALIZATION_TYPES:
        return "localization", None

    if base in UNITY_ASSET_TYPES:
        return "unity_asset", None

    # Collections
    list_match = re.match(r"List<(\w+)>", field_type)
    if list_match:
        return "collection", list_match.group(1)
    if "[]" in field_type:
        return "collection", base

    # Template references
    if base.endswith("Template") and base in known_templates:
        return "reference", None

    # If it looks like a known class name, treat as reference
    if base[0:1].isupper() and not any(c in base for c in "<>[]."):
        return "reference", None

    return "unknown", None


# ---------------------------------------------------------------------------
# Enum/struct/template parsing
# ---------------------------------------------------------------------------

def parse_all_enums(content):
    """Parse all enum definitions from dump.cs."""
    enums = {}
    enum_pattern = r"public enum (\w+).*?\n\{\n(.*?)\n\}"

    for m in re.finditer(enum_pattern, content, re.DOTALL):
        enum_name = m.group(1)
        enum_body = m.group(2)

        # Get underlying type
        underlying = "int"
        underlying_match = re.search(r"public (\w+) value__;", enum_body)
        if underlying_match:
            underlying = underlying_match.group(1)

        values = {}
        value_pattern = r"public const \w+ (\w+) = (-?\d+);"
        for vm in re.finditer(value_pattern, enum_body):
            values[vm.group(1)] = int(vm.group(2))

        if values:
            enums[enum_name] = {
                "underlying_type": underlying,
                "values": values,
            }

    return enums


def parse_all_structs(content):
    """Parse all struct definitions from dump.cs."""
    structs = {}
    struct_pattern = r"public struct (\w+).*?\n\{\n(.*?)\n\}"

    for m in re.finditer(struct_pattern, content, re.DOTALL):
        struct_name = m.group(1)
        struct_body = m.group(2)

        fields = []
        field_pattern = r"(?:public|private)\s+([\w<>\[\]\.]+)\s+(\w+);\s+//\s+0x([0-9A-Fa-f]+)"

        for fm in re.finditer(field_pattern, struct_body):
            field_type = fm.group(1)
            field_name = fm.group(2)
            offset = fm.group(3)

            if (field_name.startswith("NativeFieldInfoPtr") or
                    field_name.startswith("Il2Cpp") or
                    "k__BackingField" in field_name):
                continue

            # Skip statics (offset 0x0 can be legitimate for struct field 0,
            # but also static. Check if there's a 'static' keyword.)
            line_match = re.search(
                rf"(?:public|private) static\s+.*\s+{re.escape(field_name)};", struct_body)
            if line_match:
                continue

            fields.append({
                "name": field_name,
                "type": field_type,
                "offset": f"0x{offset}",
            })

        if fields:
            # Estimate size from last field offset + 4 (rough)
            last_offset = int(fields[-1]["offset"], 16)
            estimated_size = last_offset + 4  # conservative

            structs[struct_name] = {
                "size_bytes": estimated_size,
                "fields": fields,
            }

    return structs


def parse_all_templates(content):
    """Find all template classes and their metadata."""
    # Find concrete + abstract template classes
    template_names = set()
    for pattern in [r"public class (\w+Template)\s",
                    r"public abstract class (\w+Template)\s"]:
        template_names.update(re.findall(pattern, content))

    # Filter noise
    template_names = {t for t in template_names
                      if "." not in t and "Uxml" not in t and t != "DataTemplateLoader"}

    return sorted(template_names)


def parse_embedded_class(content, class_name):
    """Parse a regular class (not template) that's used as an element type."""
    patterns = [
        rf"public class {re.escape(class_name)}\s.*?\n\{{\n(.*?)\n\}}",
    ]

    match = None
    for pattern in patterns:
        match = re.search(pattern, content, re.DOTALL)
        if match:
            break

    if not match:
        return None

    class_body = match.group(1)

    # Extract base class
    base_match = re.search(rf"public class {re.escape(class_name)}.*?:\s+(\w+)", content)
    base_class = base_match.group(1) if base_match else None

    # Parse fields (both public and private - private with [SerializeField] are Unity-serialized)
    fields = []
    field_pattern = r"(?:public|private)\s+([\w<>\[\]\.]+)\s+(\w+);\s+//\s+0x([0-9A-Fa-f]+)"

    for m in re.finditer(field_pattern, class_body):
        field_type = m.group(1)
        field_name = m.group(2)
        offset = m.group(3)

        # Skip internal IL2CPP fields and back-references to parent
        if (field_name.startswith("NativeFieldInfoPtr") or
                field_name.startswith("Il2Cpp") or
                "k__BackingField" in field_name or
                field_name == "Parent" or  # Skip parent back-references
                offset == "0"):  # static fields
            continue

        fields.append({
            "type": field_type,
            "name": field_name,
            "offset": f"0x{offset}",
        })

    if not fields:
        return None

    return {
        "name": class_name,
        "base": base_class,
        "fields": fields,
    }


def discover_embedded_classes(content, templates, known_enums, known_structs, known_templates):
    """
    Discover classes used as element types in template collections.
    These are regular classes (not templates) that are embedded in template fields.
    Recursively discovers nested element types.
    """
    embedded = {}
    to_process = set()

    # Skip these - they're Unity/system types, not game data classes
    skip_types = {
        "String", "Object", "Int32", "Single", "Boolean", "Byte",
        "GameObject", "Transform", "Component", "MonoBehaviour",
        "Sprite", "Texture2D", "Material", "AudioClip", "AnimationClip",
        "ScriptableObject", "Color", "Vector2", "Vector3", "Vector4",
        "Quaternion", "Rect", "Bounds",
    }

    # Collect element types from templates
    for tname, tdata in templates.items():
        for field in tdata.get("fields", []):
            elem_type = field.get("element_type")
            if elem_type and elem_type not in known_templates and elem_type not in known_structs:
                if elem_type not in known_enums and elem_type not in skip_types:
                    to_process.add(elem_type)

    # Process embedded classes, discovering nested element types
    processed = set()
    while to_process:
        class_name = to_process.pop()
        if class_name in processed or class_name in skip_types:
            continue
        processed.add(class_name)

        class_info = parse_embedded_class(content, class_name)
        if not class_info:
            continue

        # Classify fields and find nested element types
        classified_fields = []
        for f in class_info["fields"]:
            category, element_type = classify_field(
                f["type"], known_enums, known_structs, known_templates)

            field_entry = {
                "name": f["name"],
                "type": f["type"],
                "offset": f["offset"],
                "category": category,
            }
            if element_type:
                field_entry["element_type"] = element_type
                # Queue nested element type for processing
                if (element_type not in known_templates and
                    element_type not in known_structs and
                    element_type not in known_enums and
                    element_type not in skip_types and
                    element_type not in processed):
                    to_process.add(element_type)

            classified_fields.append(field_entry)

        embedded[class_name] = {
            "base_class": class_info["base"],
            "fields": classified_fields,
        }

    return embedded


def build_template_hierarchy(content, template_names):
    """Build inheritance map for template classes."""
    hierarchy = {}
    for pattern in [r"public class (\w+Template)\s+:\s+(\w+)",
                    r"public abstract class (\w+Template)\s+:\s+(\w+)"]:
        for m in re.finditer(pattern, content):
            child = m.group(1)
            parent = m.group(2)
            if child in template_names:
                hierarchy[child] = parent if parent.endswith("Template") or parent in (
                    "ScriptableObject", "SerializedScriptableObject") else parent

    return hierarchy


# ---------------------------------------------------------------------------
# Schema assembly
# ---------------------------------------------------------------------------

def build_schema(dump_path):
    """Build the complete schema from a dump.cs file."""
    print(f"Reading {dump_path}...")
    content = dump_path.read_text(encoding="utf-8")
    dump_hash = compute_file_hash(dump_path)

    print("Parsing enums...")
    enums = parse_all_enums(content)
    print(f"  Found {len(enums)} enums")

    print("Parsing structs...")
    structs = parse_all_structs(content)
    print(f"  Found {len(structs)} structs")

    print("Finding template classes...")
    template_names = parse_all_templates(content)
    print(f"  Found {len(template_names)} template classes")

    print("Building inheritance hierarchy...")
    hierarchy = build_template_hierarchy(content, set(template_names))

    known_enums = set(enums.keys())
    known_structs = set(structs.keys())
    known_templates = set(template_names)

    print("Parsing template fields...")
    templates = {}
    for tname in template_names:
        class_info = parse_class_from_dump(content, tname, allow_abstract=True)
        if not class_info:
            continue

        all_fields = collect_all_fields(content, tname)
        classified_fields = []

        for f in all_fields:
            category, element_type = classify_field(
                f["type"], known_enums, known_structs, known_templates)

            field_entry = {
                "name": f["name"],
                "type": f["type"],
                "offset": f["offset"],
                "category": category,
            }
            if element_type:
                field_entry["element_type"] = element_type

            classified_fields.append(field_entry)

        templates[tname] = {
            "base_class": class_info["base"],
            "is_abstract": class_info["is_abstract"],
            "fields": classified_fields,
        }

    # Build inheritance chains
    inheritance = {}
    for tname in template_names:
        if tname in hierarchy:
            inheritance[tname] = get_inheritance_chain(tname, hierarchy)

    # Discover embedded classes (used as element types in collections)
    print("Discovering embedded classes...")
    embedded_classes = discover_embedded_classes(
        content, templates, known_enums, known_structs, known_templates)
    print(f"  Found {len(embedded_classes)} embedded classes")

    schema = {
        "version": "1.0.0",
        "dump_hash": dump_hash,
        "enums": enums,
        "structs": structs,
        "embedded_classes": embedded_classes,
        "templates": templates,
        "inheritance": inheritance,
    }

    return schema


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Generate schema.json from IL2CPP dump.cs")
    parser.add_argument("dump_path", nargs="?", default="il2cpp_dump/dump.cs",
                        help="Path to dump.cs (default: il2cpp_dump/dump.cs)")
    parser.add_argument("output_path", nargs="?", default="generated/schema.json",
                        help="Output path (default: generated/schema.json)")
    args = parser.parse_args()

    dump_path = Path(args.dump_path)
    output_path = Path(args.output_path)

    if not dump_path.exists():
        print(f"Error: {dump_path} not found", file=sys.stderr)
        return 1

    schema = build_schema(dump_path)

    # Summary
    n_templates = len(schema["templates"])
    n_abstract = sum(1 for t in schema["templates"].values() if t["is_abstract"])
    n_concrete = n_templates - n_abstract
    n_fields = sum(len(t["fields"]) for t in schema["templates"].values())

    print(f"\nWriting {output_path}...")
    with open(output_path, "w") as f:
        json.dump(schema, f, indent=2)

    print(f"\nSchema summary:")
    print(f"  Enums:              {len(schema['enums'])}")
    print(f"  Structs:            {len(schema['structs'])}")
    print(f"  Embedded classes:   {len(schema['embedded_classes'])}")
    print(f"  Templates:          {n_templates} ({n_concrete} concrete, {n_abstract} abstract)")
    print(f"  Total fields:       {n_fields}")
    print(f"  Inheritance chains: {len(schema['inheritance'])}")
    print(f"  Dump hash:          {schema['dump_hash'][:16]}...")
    print(f"\nOutput: {output_path}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
