#!/usr/bin/env python3
"""
Regenerate all generated code from the schema.

Run this after updating schema.json (e.g., after a game update):
  python tools/update_generated.py

This regenerates:
  - generated/generated_injection_code.cs (template patching)
  - generated/sdk/* (SDK type definitions)
"""

import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).parent.parent

def main():
    schema_path = REPO_ROOT / "generated" / "schema.json"

    if not schema_path.exists():
        print(f"Error: Schema not found at {schema_path}")
        print("Run data extraction first to generate schema.json")
        return 1

    print(f"Using schema: {schema_path}")
    print(f"Schema modified: {schema_path.stat().st_mtime}")
    print()

    # Regenerate injection code
    print("=== Regenerating injection code ===")
    result = subprocess.run([
        sys.executable,
        str(REPO_ROOT / "tools" / "il2cpp-dump-legacy" / "generate_injection_code.py"),
        "--from-schema", str(schema_path)
    ], cwd=REPO_ROOT)
    if result.returncode != 0:
        print("Failed to generate injection code")
        return 1

    # Regenerate SDK
    print()
    print("=== Regenerating SDK ===")
    result = subprocess.run([
        sys.executable,
        str(REPO_ROOT / "tools" / "generate_sdk.py")
    ], cwd=REPO_ROOT)
    if result.returncode != 0:
        print("Failed to generate SDK")
        return 1

    print()
    print("=== Done ===")
    print("Generated files updated. Don't forget to:")
    print("  1. Rebuild the ModpackLoader")
    print("  2. Update bundled components")
    print("  3. Commit the changes")

    return 0

if __name__ == "__main__":
    sys.exit(main())
