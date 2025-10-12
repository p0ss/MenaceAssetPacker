#!/usr/bin/env python3
"""
Match asset references from DataExtractor to AssetRipper exported files.

This script reads AssetReferences.json and scans the AssetRipper export directory
to find matching assets by name, then updates the AssetPath field.
"""

import json
import os
from pathlib import Path
from collections import defaultdict

def find_assetripper_exports(base_path):
    """Scan AssetRipper export directory and build asset name index"""
    print(f"Scanning AssetRipper exports in: {base_path}")

    asset_index = defaultdict(list)
    extensions = {'.png', '.jpg', '.prefab', '.mat', '.asset', '.txt', '.wav', '.ogg', '.mp3'}

    for root, dirs, files in os.walk(base_path):
        for file in files:
            ext = Path(file).suffix.lower()
            if ext in extensions:
                full_path = os.path.join(root, file)
                relative_path = os.path.relpath(full_path, base_path)

                # Index by filename without extension
                name_without_ext = Path(file).stem
                asset_index[name_without_ext].append(relative_path)

    print(f"Found {len(asset_index)} unique asset names")
    return asset_index

def match_references(references_path, assetripper_path, output_path):
    """Match asset references to AssetRipper exports"""

    # Load asset references
    with open(references_path, 'r') as f:
        references = json.load(f)

    print(f"Loaded {len(references)} asset references")

    # Build asset index
    asset_index = find_assetripper_exports(assetripper_path)

    # Match references
    matched = 0
    unmatched = []

    for ref in references:
        name = ref.get('Name', '')
        asset_type = ref.get('Type', '')

        if not name:
            unmatched.append(ref)
            continue

        # Try exact match first
        if name in asset_index:
            # Prefer matches based on type
            candidates = asset_index[name]
            best_match = None

            if asset_type == 'Sprite' or asset_type == 'Texture2D':
                best_match = next((c for c in candidates if c.endswith(('.png', '.jpg'))), None)
            elif asset_type == 'AudioClip':
                best_match = next((c for c in candidates if c.endswith(('.wav', '.ogg', '.mp3'))), None)
            elif asset_type == 'Material':
                best_match = next((c for c in candidates if c.endswith('.mat')), None)

            # Fallback to first candidate
            if not best_match and candidates:
                best_match = candidates[0]

            if best_match:
                ref['AssetPath'] = best_match
                matched += 1
                continue

        # Try fuzzy match (remove special characters)
        clean_name = name.lower().replace('_', '').replace('.', '').replace('-', '')
        for indexed_name, paths in asset_index.items():
            clean_indexed = indexed_name.lower().replace('_', '').replace('.', '').replace('-', '')
            if clean_name == clean_indexed:
                ref['AssetPath'] = paths[0]
                matched += 1
                break
        else:
            unmatched.append(ref)

    # Save updated references
    with open(output_path, 'w') as f:
        json.dump(references, f, indent=2)

    print(f"\n✅ Matched {matched}/{len(references)} asset references")
    print(f"📝 Saved to: {output_path}")

    if unmatched:
        print(f"\n⚠️  {len(unmatched)} unmatched references:")
        for ref in unmatched[:10]:
            print(f"   - {ref.get('Name', 'unknown')} ({ref.get('Type', 'unknown')})")
        if len(unmatched) > 10:
            print(f"   ... and {len(unmatched) - 10} more")

def main():
    # Paths
    game_dir = Path.home() / ".steam/debian-installation/steamapps/common/Menace Demo"
    references_path = game_dir / "UserData/ExtractedData/AssetReferences.json"

    # Try to find AssetRipper exports
    possible_assetripper_paths = [
        game_dir / "ExportedProject",
        Path.cwd() / "AssetRipper_Export",
        Path.home() / "Documents/AssetRipper/Menace"
    ]

    assetripper_path = None
    for path in possible_assetripper_paths:
        if path.exists():
            assetripper_path = path
            break

    if not assetripper_path:
        print("❌ Could not find AssetRipper export directory")
        print("Tried:")
        for path in possible_assetripper_paths:
            print(f"  - {path}")
        print("\nPlease specify the path:")
        user_path = input("> ").strip()
        if user_path:
            assetripper_path = Path(user_path)

    if not assetripper_path or not assetripper_path.exists():
        print("❌ AssetRipper export directory not found")
        return 1

    if not references_path.exists():
        print(f"❌ AssetReferences.json not found at: {references_path}")
        print("Run the game with DataExtractor mod first to generate it")
        return 1

    output_path = references_path  # Update in place
    match_references(references_path, assetripper_path, output_path)

    return 0

if __name__ == '__main__':
    exit(main())
