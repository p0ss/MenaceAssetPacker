#!/usr/bin/env python3
"""
Validate extracted template data against the schema.

Checks template coverage, instance naming, field coverage, type correctness,
and cross-references. Outputs a PASS/WARN/FAIL report.

Usage:
  python validate_extraction.py --schema schema.json --data /path/to/ExtractedData
  python validate_extraction.py --schema schema.json  # uses default game data path
"""

import argparse
import json
import math
import sys
from pathlib import Path


# ---------------------------------------------------------------------------
# Validation checks
# ---------------------------------------------------------------------------

def check_template_coverage(schema, data_dir):
    """Check that every non-abstract template type has a JSON file."""
    results = []
    expected = set()
    found = set()
    abstract_count = 0

    for tname, tinfo in schema["templates"].items():
        if tinfo["is_abstract"]:
            abstract_count += 1
            continue
        expected.add(tname)
        json_path = data_dir / f"{tname}.json"
        if json_path.exists():
            found.add(tname)

    missing = expected - found
    pct = len(found) / len(expected) * 100 if expected else 100

    level = "PASS" if not missing else ("WARN" if pct >= 70 else "FAIL")
    msg = (f"{len(found)}/{len(expected)} types have output "
           f"({abstract_count} abstract, expected no output)")

    results.append((level, msg))
    if missing:
        for m in sorted(missing):
            results.append(("INFO", f"  Missing: {m}"))

    return results, found


def check_instance_names(data_dir, found_types):
    """Check for unknown_N names vs properly named instances."""
    results = []
    has_fail = False

    for tname in sorted(found_types):
        json_path = data_dir / f"{tname}.json"
        try:
            with open(json_path) as f:
                instances = json.load(f)
        except (json.JSONDecodeError, OSError):
            results.append(("FAIL", f"{tname}: Could not read JSON"))
            has_fail = True
            continue

        if not isinstance(instances, list):
            results.append(("WARN", f"{tname}: Not a list"))
            continue

        total = len(instances)
        if total == 0:
            results.append(("WARN", f"{tname}: Empty file"))
            continue

        named = sum(1 for inst in instances
                    if isinstance(inst, dict) and
                    inst.get("name") and
                    not inst["name"].startswith("unknown_"))

        pct = named / total * 100

        if pct == 100:
            level = "PASS"
        elif pct >= 50:
            level = "WARN"
        else:
            level = "FAIL"
            has_fail = True

        results.append((level, f"{tname}: {named}/{total} named ({pct:.0f}%)"))

    return results


def check_field_coverage(schema, data_dir, found_types):
    """Compare schema fields vs JSON keys per type."""
    results = []

    for tname in sorted(found_types):
        tinfo = schema["templates"].get(tname)
        if not tinfo:
            continue

        # Get expected field names (skip collections and complex types)
        expected_fields = set()
        skipped = 0
        for f in tinfo["fields"]:
            if f["category"] in ("collection", "unity_asset", "localization", "unknown"):
                skipped += 1
                continue
            expected_fields.add(f["name"])

        json_path = data_dir / f"{tname}.json"
        try:
            with open(json_path) as f:
                instances = json.load(f)
        except (json.JSONDecodeError, OSError):
            continue

        if not isinstance(instances, list) or not instances:
            continue

        # Check first valid instance for field presence
        sample = None
        for inst in instances:
            if isinstance(inst, dict) and inst.get("name"):
                sample = inst
                break
        if sample is None and instances:
            sample = instances[0] if isinstance(instances[0], dict) else None
        if sample is None:
            continue

        json_keys = set(sample.keys()) - {"name", "_template_type"}
        present = expected_fields & json_keys
        missing = expected_fields - json_keys

        total = len(expected_fields)
        found_count = len(present)

        if total == 0:
            continue

        pct = found_count / total * 100
        suffix = f" ({skipped} complex skipped)" if skipped else ""
        level = "PASS" if pct >= 90 else ("WARN" if pct >= 60 else "FAIL")
        results.append((level, f"{tname}: {found_count}/{total} fields{suffix}"))

        if missing and level != "PASS":
            for m in sorted(missing)[:5]:
                results.append(("INFO", f"  Missing: {m}"))

    return results


def check_type_validation(schema, data_dir, found_types):
    """Validate that field values match expected types."""
    results = []

    type_checks = {
        "int": lambda v: isinstance(v, int),
        "Int32": lambda v: isinstance(v, int),
        "short": lambda v: isinstance(v, int),
        "Int16": lambda v: isinstance(v, int),
        "long": lambda v: isinstance(v, int),
        "Int64": lambda v: isinstance(v, int),
        "byte": lambda v: isinstance(v, int) and 0 <= v <= 255,
        "Byte": lambda v: isinstance(v, int) and 0 <= v <= 255,
        "float": lambda v: isinstance(v, (int, float)),
        "Single": lambda v: isinstance(v, (int, float)),
        "double": lambda v: isinstance(v, (int, float)),
        "Double": lambda v: isinstance(v, (int, float)),
        "bool": lambda v: isinstance(v, bool),
        "Boolean": lambda v: isinstance(v, bool),
    }

    garbage_threshold = 1e10  # floats above this are likely wrong offsets

    for tname in sorted(found_types):
        tinfo = schema["templates"].get(tname)
        if not tinfo:
            continue

        json_path = data_dir / f"{tname}.json"
        try:
            with open(json_path) as f:
                instances = json.load(f)
        except (json.JSONDecodeError, OSError):
            continue

        if not isinstance(instances, list):
            continue

        field_map = {f["name"]: f for f in tinfo["fields"]}
        errors = []

        for inst in instances[:50]:  # check first 50 instances max
            if not isinstance(inst, dict):
                continue
            inst_name = inst.get("name", "?")

            for fname, finfo in field_map.items():
                if fname not in inst:
                    continue
                val = inst[fname]
                ftype = finfo["type"]

                checker = type_checks.get(ftype)
                if checker and not checker(val):
                    errors.append(
                        f"{tname}.{fname}[{inst_name}]: expected {ftype}, got {type(val).__name__}={val}")

                # Garbage float check
                if ftype in ("float", "Single") and isinstance(val, (int, float)):
                    if abs(val) > garbage_threshold and val != 0:
                        errors.append(
                            f"{tname}.{fname}[{inst_name}]: "
                            f"expected float, got {val:.3e} (garbage - wrong offset?)")

        if errors:
            results.append(("FAIL", f"{tname}: {len(errors)} type errors"))
            for e in errors[:5]:
                results.append(("INFO", f"  {e}"))
            if len(errors) > 5:
                results.append(("INFO", f"  ... and {len(errors) - 5} more"))
        else:
            # Only report PASS for types with actual data
            has_data = any(isinstance(i, dict) and len(i) > 1 for i in instances[:5])
            if has_data:
                results.append(("PASS", f"{tname}: all values match expected types"))

    return results


# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------

def print_section(title, results):
    """Print a report section."""
    print(f"\n--- {title} ---")
    for level, msg in results:
        prefix = {"PASS": "PASS ", "WARN": "WARN ", "FAIL": "FAIL ", "INFO": "     "}
        print(f"{prefix.get(level, '     ')} {msg}")


def main():
    parser = argparse.ArgumentParser(description="Validate extraction output")
    parser.add_argument("--schema", required=True, help="Path to schema.json")
    parser.add_argument("--data", default=None,
                        help="Path to extracted data directory")
    args = parser.parse_args()

    schema_path = Path(args.schema)
    if not schema_path.exists():
        print(f"Error: {schema_path} not found", file=sys.stderr)
        return 1

    with open(schema_path) as f:
        schema = json.load(f)

    if args.data:
        data_dir = Path(args.data)
    else:
        data_dir = (Path.home() /
                    ".steam/debian-installation/steamapps/common/Menace Demo"
                    "/UserData/ExtractedData")

    if not data_dir.exists():
        print(f"Error: Data directory not found: {data_dir}", file=sys.stderr)
        return 1

    print("=== Extraction Validation Report ===")
    print(f"Schema: {schema_path}")
    print(f"Data:   {data_dir}")

    fail_count = 0
    warn_count = 0

    # 1. Template coverage
    coverage_results, found_types = check_template_coverage(schema, data_dir)
    print_section("Template Coverage", coverage_results)
    fail_count += sum(1 for l, _ in coverage_results if l == "FAIL")
    warn_count += sum(1 for l, _ in coverage_results if l == "WARN")

    if not found_types:
        print("\nNo extracted data found. Cannot run further checks.")
        return 2

    # 2. Instance names
    name_results = check_instance_names(data_dir, found_types)
    print_section("Instance Names", name_results)
    fail_count += sum(1 for l, _ in name_results if l == "FAIL")
    warn_count += sum(1 for l, _ in name_results if l == "WARN")

    # 3. Field coverage
    field_results = check_field_coverage(schema, data_dir, found_types)
    print_section("Field Coverage", field_results)
    fail_count += sum(1 for l, _ in field_results if l == "FAIL")
    warn_count += sum(1 for l, _ in field_results if l == "WARN")

    # 4. Type validation
    type_results = check_type_validation(schema, data_dir, found_types)
    print_section("Type Validation", type_results)
    fail_count += sum(1 for l, _ in type_results if l == "FAIL")
    warn_count += sum(1 for l, _ in type_results if l == "WARN")

    # Overall
    print(f"\n=== OVERALL: {fail_count} FAIL, {warn_count} WARN ===")

    if fail_count > 0:
        return 2
    if warn_count > 0:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
