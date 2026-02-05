#!/usr/bin/env python3
"""
Extract conversation and event dialogue data from Unity asset binary files.

Works without type trees by finding serialized strings directly in the binary.
Extracts two types of data:
  1. Localization entries (Events/, Conversations/) - structured dialogue with role attribution
  2. Pipe-delimited tactical bark nodes (VARIATION|{}, SAY|{}, etc.)

Usage:
    python extract_conversations.py <game_data_dir> [output_dir]

    game_data_dir: Path to Menace_Data directory (or parent containing Menace_Data)
    output_dir:    Where to write JSON output (default: game_data_dir/ExtractedConversations)
"""

import json
import os
import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path


# Node types used in pipe-delimited format
NODE_TYPES = {
    "SAY", "CHOICE", "GOTO", "LABEL", "SET", "SHOW", "IF", "IFELSE",
    "VARIATION", "EXEC", "ACTION", "EMPTY", "NOTE", "SOUND", "VIDEO",
}
NODE_TYPE_PATTERN = re.compile(
    r"^(" + "|".join(NODE_TYPES) + r")\|(.+)$"
)

# Localization entry pattern: Path/...,Type,Context,Text
# The text field may be quoted with commas inside
LOCA_PATH_PREFIXES = ("Events/", "Conversations/", "TacticalBarks/")


def find_resources_assets(game_dir: str) -> str:
    """Find resources.assets in the game directory."""
    game_dir = Path(game_dir)
    candidates = [
        game_dir / "resources.assets",
        game_dir / "Menace_Data" / "resources.assets",
    ]
    for c in candidates:
        if c.exists():
            return str(c)
    raise FileNotFoundError(
        f"Could not find resources.assets in {game_dir} or {game_dir}/Menace_Data/"
    )


def extract_strings(filepath: str, min_length: int = 10) -> list[str]:
    """Extract printable strings from a binary file using the strings command."""
    result = subprocess.run(
        ["strings", f"-n{min_length}", filepath],
        capture_output=True, text=True, timeout=120
    )
    return result.stdout.splitlines()


def parse_loca_line(line: str) -> dict | None:
    """
    Parse a localization CSV line into structured data.

    Format: Path/subpath/key,Type,Context,Text,Extra
    Handles quoted fields with embedded commas.
    """
    # Must start with a known prefix
    if not any(line.startswith(p) for p in LOCA_PATH_PREFIXES):
        return None

    # Split on first comma to get path, then parse remaining CSV fields
    parts = []
    current = []
    in_quotes = False
    for ch in line:
        if ch == '"':
            in_quotes = not in_quotes
            current.append(ch)
        elif ch == ',' and not in_quotes:
            parts.append(''.join(current))
            current = []
        else:
            current.append(ch)
    parts.append(''.join(current))

    if len(parts) < 4:
        return None

    full_path = parts[0]
    field_type = parts[1] if len(parts) > 1 else ""
    context = parts[2] if len(parts) > 2 else ""
    text = parts[3] if len(parts) > 3 else ""
    extra = parts[4] if len(parts) > 4 else ""

    # Strip surrounding quotes from text
    if text.startswith('"') and text.endswith('"'):
        text = text[1:-1]
    if extra.startswith('"') and extra.endswith('"'):
        extra = extra[1:-1]

    return {
        "full_path": full_path,
        "type": field_type,
        "context": context,
        "text": text,
        "extra": extra,
    }


def parse_conversation_path(full_path: str) -> dict:
    """
    Break a localization path into conversation ID, node GUID, and field name.

    Examples:
        Events/Story/event_story_foo/gd_comment -> conv=Events/Story/event_story_foo, field=gd_comment
        Events/Story/event_story_foo/12345,Text,... -> conv=Events/Story/event_story_foo, node=12345
        Events/Story/event_story_foo/12345/Choice0 -> conv=Events/Story/event_story_foo, node=12345, field=Choice0
    """
    parts = full_path.split("/")

    # Known metadata field names at the end of the path
    metadata_fields = {
        "gd_comment", "event_sender", "event_message", "event_title",
        "EffectDesc", "StageName", "Name", "Desc",
    }

    # Check if last part is a metadata field
    if parts[-1] in metadata_fields:
        return {
            "conversation": "/".join(parts[:-1]),
            "node_guid": None,
            "field": parts[-1],
        }

    # Check if last part is a choice/effect under a node GUID
    # Pattern: .../nodeGUID/Choice0 or .../nodeGUID/Choice0EffectsTooltip or .../nodeGUID/ApplyEmotionTooltip
    if len(parts) >= 3 and parts[-2].isdigit():
        return {
            "conversation": "/".join(parts[:-2]),
            "node_guid": parts[-2],
            "field": parts[-1],
        }

    # Check if last part is a node GUID (numeric)
    if parts[-1].isdigit():
        return {
            "conversation": "/".join(parts[:-1]),
            "node_guid": parts[-1],
            "field": "text",
        }

    # Fallback: the whole path is the conversation ID
    return {
        "conversation": full_path,
        "node_guid": None,
        "field": "unknown",
    }


def extract_localization_data(strings_list: list[str]) -> dict:
    """
    Extract and group localization entries by conversation.

    Returns a dict keyed by conversation path, each containing:
        - metadata (gd_comment, event_sender, etc.)
        - nodes: list of dialogue nodes with role attribution and text
    """
    conversations = defaultdict(lambda: {
        "metadata": {},
        "nodes": defaultdict(lambda: {
            "text": "", "role": "", "choices": {}, "effects": {},
        }),
    })
    # Track seen entries to deduplicate (binary contains multiple loca variants)
    seen = set()

    for line in strings_list:
        entry = parse_loca_line(line)
        if entry is None:
            continue

        # Deduplicate: same path+field+text = same entry
        dedup_key = (entry["full_path"], entry["text"])
        if dedup_key in seen:
            continue
        seen.add(dedup_key)

        parsed = parse_conversation_path(entry["full_path"])
        conv_id = parsed["conversation"]
        node_guid = parsed["node_guid"]
        field = parsed["field"]

        conv = conversations[conv_id]

        if node_guid is None:
            # Metadata field
            conv["metadata"][field] = entry["text"]
        else:
            node = conv["nodes"][node_guid]
            if field == "text":
                node["text"] = entry["text"]
                node["role"] = entry["context"]
                node["guid"] = node_guid
            elif field.startswith("Choice"):
                if "EffectsTooltip" in field:
                    node["effects"][field] = {
                        "field": field,
                        "text": entry["text"],
                        "extra": entry.get("extra", ""),
                    }
                else:
                    node["choices"][field] = {
                        "field": field,
                        "role": entry["context"],
                        "text": entry["text"],
                    }
            elif "Tooltip" in field or "Emotion" in field:
                node["effects"][field] = {
                    "field": field,
                    "text": entry["text"],
                    "extra": entry.get("extra", ""),
                }
            else:
                node.setdefault("extra_fields", {})[field] = entry["text"]

    # Convert defaultdicts to regular dicts and nodes to lists
    result = {}
    for conv_id, conv_data in sorted(conversations.items()):
        nodes_list = []
        for guid, node_data in sorted(conv_data["nodes"].items(), key=lambda x: x[0]):
            node_entry = {"guid": guid}
            if node_data["role"]:
                node_entry["role"] = node_data["role"]
            if node_data["text"]:
                node_entry["text"] = node_data["text"]
            if node_data["choices"]:
                # Sort choices by field name (Choice0, Choice1, ...) for correct order
                node_entry["choices"] = sorted(
                    node_data["choices"].values(), key=lambda c: c["field"]
                )
            if node_data["effects"]:
                node_entry["effects"] = list(node_data["effects"].values())
            if node_data.get("extra_fields"):
                node_entry["extra_fields"] = node_data["extra_fields"]
            nodes_list.append(node_entry)

        result[conv_id] = {
            "path": conv_id,
            "metadata": dict(conv_data["metadata"]),
            "nodes": nodes_list,
        }

    return result


def extract_tactical_barks(strings_list: list[str]) -> list[dict]:
    """
    Extract pipe-delimited conversation nodes (tactical barks, etc.).

    These are VARIATION|{json}, SAY|{json}, EMPTY|{json}, etc.
    Returns a list of parsed node objects.
    """
    nodes = []
    for line in strings_list:
        m = NODE_TYPE_PATTERN.match(line)
        if m:
            node_type = m.group(1)
            json_str = m.group(2)
            try:
                data = json.loads(json_str)
                node = {"type": node_type, **data}
                nodes.append(node)
            except json.JSONDecodeError:
                # Truncated strings from the binary - still capture what we can
                nodes.append({
                    "type": node_type,
                    "raw": json_str[:500],
                    "truncated": True,
                })
    return nodes


def group_tactical_barks(nodes: list[dict]) -> list[dict]:
    """
    Group sequential tactical bark nodes into conversation-like units.

    Tactical barks follow a pattern of VARIATION/SAY nodes separated by EMPTY nodes.
    Each VARIATION contains dialogue variants for a single bark trigger.
    Groups are separated by larger EMPTY sequences or type changes.
    """
    # Collect unique RoleGuids and their dialogue
    roles = defaultdict(list)
    all_texts = []

    for node in nodes:
        if node["type"] == "VARIATION" and "Variations" in node:
            for var in node["Variations"]:
                if "m_SerializedNodes" in var:
                    for ser_node in var["m_SerializedNodes"]:
                        m = NODE_TYPE_PATTERN.match(ser_node)
                        if m and m.group(1) == "SAY":
                            try:
                                say_data = json.loads(m.group(2))
                                role_guid = say_data.get("RoleGuid", 0)
                                text = say_data.get("Text", "")
                                if text:
                                    roles[role_guid].append(text)
                                    all_texts.append({
                                        "role_guid": role_guid,
                                        "text": text,
                                        "guid": say_data.get("Guid", 0),
                                        "sound": say_data.get("Sound", {}),
                                    })
                            except json.JSONDecodeError:
                                pass
        elif node["type"] == "SAY" and "Text" in node:
            role_guid = node.get("RoleGuid", 0)
            text = node.get("Text", "")
            if text:
                roles[role_guid].append(text)
                all_texts.append({
                    "role_guid": role_guid,
                    "text": text,
                    "guid": node.get("Guid", 0),
                    "sound": node.get("Sound", {}),
                })
        elif node["type"] == "ACTION" and "m_SerAction" in node:
            all_texts.append({
                "type": "action",
                "action": node["m_SerAction"],
                "guid": node.get("Guid", 0),
            })

    return {
        "roles": {str(k): {"line_count": len(v), "sample_lines": v[:5]} for k, v in sorted(roles.items())},
        "dialogue_lines": all_texts,
        "total_lines": len(all_texts),
    }


def extract_all_loca_strings(strings_list: list[str]) -> list[str]:
    """Extract ALL localization-format strings (not just conversations)."""
    loca_entries = []
    for line in strings_list:
        # Localization entries have the format: Path/key,Type,Context,Text
        # They contain at least 3 commas and start with a category path
        if ",Text," in line or ",Tooltip," in line:
            loca_entries.append(line)
    return loca_entries


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    game_dir = sys.argv[1]
    output_dir = sys.argv[2] if len(sys.argv) > 2 else None

    try:
        assets_path = find_resources_assets(game_dir)
    except FileNotFoundError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    if output_dir is None:
        output_dir = str(Path(assets_path).parent / "ExtractedConversations")

    os.makedirs(output_dir, exist_ok=True)
    print(f"Extracting from: {assets_path}")
    print(f"Output dir:      {output_dir}")

    # Extract all strings from the binary
    print("Extracting strings from binary...")
    all_strings = extract_strings(assets_path, min_length=10)
    print(f"  Found {len(all_strings)} strings (min length 10)")

    # 1. Extract localization/dialogue data
    print("Parsing localization entries...")
    conversations = extract_localization_data(all_strings)
    print(f"  Found {len(conversations)} conversations/events")

    # Separate by category
    story_events = {}
    system_events = {}
    tactical_events = {}
    conversation_meta = {}
    other = {}

    for path, data in conversations.items():
        if path.startswith("Events/Story/"):
            story_events[path] = data
        elif path.startswith("Events/SystemMap/") or path.startswith("Events/Tactical/"):
            system_events[path] = data
        elif path.startswith("TacticalBarks/"):
            tactical_events[path] = data
        elif path.startswith("Conversations/"):
            conversation_meta[path] = data
        else:
            other[path] = data

    # 2. Extract pipe-delimited tactical barks
    print("Parsing pipe-delimited nodes...")
    bark_nodes = extract_tactical_barks(all_strings)
    bark_summary = group_tactical_barks(bark_nodes)
    print(f"  Found {len(bark_nodes)} raw nodes, {bark_summary['total_lines']} dialogue lines")
    print(f"  Unique speaker RoleGuids: {len(bark_summary['roles'])}")

    # 3. Write output files
    def write_json(filename, data):
        filepath = os.path.join(output_dir, filename)
        with open(filepath, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        count = len(data) if isinstance(data, (list, dict)) else 0
        print(f"  Wrote {filepath} ({count} entries)")

    print("\nWriting output files...")

    if story_events:
        write_json("story_events.json", story_events)
    if system_events:
        write_json("system_map_events.json", system_events)
    if tactical_events:
        write_json("tactical_events_loca.json", tactical_events)
    if conversation_meta:
        write_json("conversation_metadata.json", conversation_meta)
    if other:
        write_json("other_conversations.json", other)

    write_json("tactical_barks_raw.json", bark_nodes)
    write_json("tactical_barks_grouped.json", bark_summary)

    # 4. Write a combined "all dialogue" flat file for easy searching
    all_dialogue = []
    for path, conv in sorted(conversations.items()):
        for node in conv["nodes"]:
            if "text" in node and node["text"]:
                entry = {
                    "conversation": path,
                    "guid": node["guid"],
                    "text": node["text"],
                }
                if "role" in node:
                    entry["role"] = node["role"]
                if node.get("choices"):
                    entry["choices"] = [c["text"] for c in node["choices"]]
                all_dialogue.append(entry)

    # Add tactical bark lines
    for line in bark_summary["dialogue_lines"]:
        if "text" in line:
            all_dialogue.append({
                "conversation": "TacticalBarks",
                "guid": str(line.get("guid", "")),
                "text": line["text"],
                "role_guid": str(line.get("role_guid", "")),
            })

    write_json("all_dialogue_flat.json", all_dialogue)

    # 5. Summary stats
    print(f"\n--- Summary ---")
    print(f"Story events:        {len(story_events)}")
    print(f"System/Tactical:     {len(system_events)}")
    print(f"Tactical barks:      {len(tactical_events)} (loca) + {bark_summary['total_lines']} (raw nodes)")
    print(f"Conversation meta:   {len(conversation_meta)}")
    print(f"Other:               {len(other)}")
    print(f"Total dialogue lines: {len(all_dialogue)}")
    print(f"\nOutput written to: {output_dir}")


if __name__ == "__main__":
    main()
