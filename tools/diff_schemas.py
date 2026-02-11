#!/usr/bin/env python3
"""
Compare two schema.json files to identify changes between game versions.

Reports new/removed template types, field changes, offset changes, and
enum value changes. Critical for EA migration.

Usage:
  python diff_schemas.py schema_old.json schema_new.json
"""

import argparse
import json
import sys
from pathlib import Path


def diff_enums(old_enums, new_enums):
    """Compare enum definitions."""
    results = []
    old_names = set(old_enums.keys())
    new_names = set(new_enums.keys())

    added = new_names - old_names
    removed = old_names - new_names
    common = old_names & new_names

    if added:
        results.append(("ADD", f"{len(added)} new enums"))
        for name in sorted(added):
            results.append(("INFO", f"  + {name} ({len(new_enums[name]['values'])} values)"))

    if removed:
        results.append(("DEL", f"{len(removed)} removed enums"))
        for name in sorted(removed):
            results.append(("INFO", f"  - {name}"))

    for name in sorted(common):
        old_vals = old_enums[name]["values"]
        new_vals = new_enums[name]["values"]

        added_v = set(new_vals.keys()) - set(old_vals.keys())
        removed_v = set(old_vals.keys()) - set(new_vals.keys())
        changed_v = {k for k in set(old_vals.keys()) & set(new_vals.keys())
                     if old_vals[k] != new_vals[k]}

        if added_v or removed_v or changed_v:
            results.append(("CHG", f"{name}: "
                           f"+{len(added_v)} -{len(removed_v)} ~{len(changed_v)} values"))
            for v in sorted(added_v):
                results.append(("INFO", f"  + {v} = {new_vals[v]}"))
            for v in sorted(removed_v):
                results.append(("INFO", f"  - {v} = {old_vals[v]}"))
            for v in sorted(changed_v):
                results.append(("CRIT", f"  ~ {v}: {old_vals[v]} -> {new_vals[v]}"))

    return results


def diff_templates(old_templates, new_templates):
    """Compare template definitions."""
    results = []
    old_names = set(old_templates.keys())
    new_names = set(new_templates.keys())

    added = new_names - old_names
    removed = old_names - new_names
    common = old_names & new_names

    if added:
        results.append(("ADD", f"{len(added)} new templates"))
        for name in sorted(added):
            n_fields = len(new_templates[name].get("fields", []))
            results.append(("INFO", f"  + {name} ({n_fields} fields)"))

    if removed:
        results.append(("DEL", f"{len(removed)} removed templates"))
        for name in sorted(removed):
            results.append(("INFO", f"  - {name}"))

    offset_changes = 0

    for name in sorted(common):
        old_t = old_templates[name]
        new_t = new_templates[name]

        old_fields = {f["name"]: f for f in old_t.get("fields", [])}
        new_fields = {f["name"]: f for f in new_t.get("fields", [])}

        added_f = set(new_fields.keys()) - set(old_fields.keys())
        removed_f = set(old_fields.keys()) - set(new_fields.keys())

        # Check for offset changes (critical!)
        offset_changed = []
        type_changed = []
        for fname in sorted(set(old_fields.keys()) & set(new_fields.keys())):
            old_f = old_fields[fname]
            new_f = new_fields[fname]

            if old_f["offset"] != new_f["offset"]:
                offset_changed.append((fname, old_f["offset"], new_f["offset"]))
                offset_changes += 1

            if old_f["type"] != new_f["type"]:
                type_changed.append((fname, old_f["type"], new_f["type"]))

        if added_f or removed_f or offset_changed or type_changed:
            results.append(("CHG", f"{name}:"))

            for f in sorted(added_f):
                results.append(("INFO", f"  + {f}: {new_fields[f]['type']} @ {new_fields[f]['offset']}"))
            for f in sorted(removed_f):
                results.append(("INFO", f"  - {f}: {old_fields[f]['type']} @ {old_fields[f]['offset']}"))
            for fname, old_off, new_off in offset_changed:
                results.append(("CRIT", f"  OFFSET {fname}: {old_off} -> {new_off}"))
            for fname, old_type, new_type in type_changed:
                results.append(("WARN", f"  TYPE {fname}: {old_type} -> {new_type}"))

    if offset_changes:
        results.insert(0, ("CRIT",
                          f"*** {offset_changes} OFFSET CHANGES - extraction code must be regenerated ***"))

    return results


def diff_structs(old_structs, new_structs):
    """Compare struct definitions."""
    results = []
    old_names = set(old_structs.keys())
    new_names = set(new_structs.keys())

    added = new_names - old_names
    removed = old_names - new_names

    if added:
        results.append(("ADD", f"{len(added)} new structs"))
        for name in sorted(added):
            results.append(("INFO", f"  + {name}"))

    if removed:
        results.append(("DEL", f"{len(removed)} removed structs"))
        for name in sorted(removed):
            results.append(("INFO", f"  - {name}"))

    for name in sorted(old_names & new_names):
        old_s = old_structs[name]
        new_s = new_structs[name]
        if old_s.get("size_bytes") != new_s.get("size_bytes"):
            results.append(("CRIT",
                           f"{name}: size changed {old_s.get('size_bytes')} -> {new_s.get('size_bytes')}"))

    return results


def print_section(title, results):
    """Print a diff section."""
    if not results:
        print(f"\n--- {title} ---")
        print("     No changes")
        return

    print(f"\n--- {title} ---")
    prefix_map = {
        "ADD": " ADD ",
        "DEL": " DEL ",
        "CHG": " CHG ",
        "CRIT": "CRIT ",
        "WARN": "WARN ",
        "INFO": "     ",
    }
    for level, msg in results:
        print(f"{prefix_map.get(level, '     ')} {msg}")


def main():
    parser = argparse.ArgumentParser(description="Compare two schema files")
    parser.add_argument("old_schema", help="Path to old schema.json")
    parser.add_argument("new_schema", help="Path to new schema.json")
    args = parser.parse_args()

    old_path = Path(args.old_schema)
    new_path = Path(args.new_schema)

    for p in (old_path, new_path):
        if not p.exists():
            print(f"Error: {p} not found", file=sys.stderr)
            return 1

    with open(old_path) as f:
        old_schema = json.load(f)
    with open(new_path) as f:
        new_schema = json.load(f)

    print("=== Schema Diff Report ===")
    print(f"Old: {old_path} (hash: {old_schema.get('dump_hash', '?')[:16]}...)")
    print(f"New: {new_path} (hash: {new_schema.get('dump_hash', '?')[:16]}...)")

    has_critical = False

    # Enums
    enum_results = diff_enums(
        old_schema.get("enums", {}), new_schema.get("enums", {}))
    print_section("Enums", enum_results)
    has_critical |= any(l == "CRIT" for l, _ in enum_results)

    # Structs
    struct_results = diff_structs(
        old_schema.get("structs", {}), new_schema.get("structs", {}))
    print_section("Structs", struct_results)
    has_critical |= any(l == "CRIT" for l, _ in struct_results)

    # Templates
    template_results = diff_templates(
        old_schema.get("templates", {}), new_schema.get("templates", {}))
    print_section("Templates", template_results)
    has_critical |= any(l == "CRIT" for l, _ in template_results)

    # Summary
    total_changes = len([r for r in enum_results + struct_results + template_results
                        if r[0] != "INFO"])
    critical = len([r for r in enum_results + struct_results + template_results
                   if r[0] == "CRIT"])

    print(f"\n=== SUMMARY: {total_changes} changes, {critical} critical ===")

    if has_critical:
        print("\n*** CRITICAL CHANGES DETECTED ***")
        print("Extraction code MUST be regenerated before running against new build.")
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
