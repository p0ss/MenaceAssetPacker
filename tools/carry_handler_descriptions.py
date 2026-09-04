#!/usr/bin/env python3
"""
Carry event-handler field descriptions forward onto a freshly generated schema.json.

After a game update the pipeline is:
  Il2CppDumper -> tools/generate_schema.py -> extract_eventhandlers.py -> THIS SCRIPT

generate_schema.py and extract_eventhandlers.py produce a structurally correct
schema for the new build, but every effect_handlers field comes out with an empty
"description". The descriptions (Ghidra-verified semantics, formulas, enum meanings)
live in eventhandler_knowledge.json and in the previous schema.json. This script
merges them back in by (handler name, field name), so a game update never throws
that work away. Fields that are new in this build stay blank and are reported.

Usage:
  python tools/carry_handler_descriptions.py NEW_SCHEMA [--kb eventhandler_knowledge.json]
                                             [--previous schema.json] [--report]
"""

import argparse
import json
from pathlib import Path


def load(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def kb_descriptions(kb):
    """{handler: {field: {description, confidence, source}}} from eventhandler_knowledge.json."""
    out = {}
    for handler, fields in (kb.get("handlers") or {}).items():
        if not isinstance(fields, dict):
            continue
        out[handler] = {}
        for field, info in fields.items():
            if isinstance(info, dict) and info.get("description"):
                out[handler][field] = info
    return out


def schema_descriptions(schema):
    """{handler: {field: {description}}} from a previous schema's effect_handlers."""
    out = {}
    for handler, data in (schema.get("effect_handlers") or {}).items():
        out[handler] = {}
        for field in data.get("fields", []):
            if field.get("description"):
                out[handler][field["name"]] = {"description": field["description"]}
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("new_schema", help="Freshly generated + enriched schema.json (updated in place)")
    parser.add_argument("--kb", default="eventhandler_knowledge.json", help="Knowledge base (primary source)")
    parser.add_argument("--previous", default="schema.json", help="Previous schema.json (fallback source)")
    parser.add_argument("--report", action="store_true", help="List handlers/fields left without a description")
    args = parser.parse_args()

    new_path = Path(args.new_schema)
    schema = load(new_path)
    handlers = schema.get("effect_handlers") or {}
    if not handlers:
        print("No effect_handlers in the new schema. Run extract_eventhandlers.py first.")
        return 1

    sources = []
    if Path(args.kb).exists():
        sources.append(("knowledge base", kb_descriptions(load(args.kb))))
    if Path(args.previous).exists():
        sources.append(("previous schema", schema_descriptions(load(args.previous))))
    if not sources:
        print("No description sources found; nothing to carry.")
        return 1

    carried = 0
    total = 0
    missing = []
    new_handlers = []
    for handler, data in handlers.items():
        seen_handler = any(handler in src for _, src in sources)
        if not seen_handler:
            new_handlers.append(handler)
        for field in data.get("fields", []):
            total += 1
            if field.get("description"):
                carried += 1
                continue
            for _, src in sources:
                info = src.get(handler, {}).get(field["name"])
                if info:
                    field["description"] = info["description"]
                    if "confidence" in info:
                        field["confidence"] = info["confidence"]
                    if "source" in info:
                        field["source"] = info["source"]
                    carried += 1
                    break
            else:
                missing.append(f"{handler}.{field['name']}")

    with open(new_path, "w", encoding="utf-8") as f:
        json.dump(schema, f, indent=2, ensure_ascii=False)

    print(f"Handlers: {len(handlers)}  fields: {total}  described: {carried}  undescribed: {len(missing)}")
    if new_handlers:
        print(f"Handlers with no prior knowledge ({len(new_handlers)}): {', '.join(sorted(new_handlers))}")
    if args.report and missing:
        print("Undescribed fields:")
        for m in missing:
            print(f"  {m}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
