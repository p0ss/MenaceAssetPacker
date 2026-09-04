#!/usr/bin/env bash
#
# Bring the modkit up to date with the MENACE build currently installed.
#
# What it does, in order:
#   1. Reads the game version out of the install.
#   2. Runs Il2CppDumper against GameAssembly.dll + global-metadata.dat.
#   3. Regenerates schema.json (structure), enriches it with event handlers, and carries
#      the hand-written handler field descriptions forward from the previous schema.
#   4. Diffs old vs new schema and writes a report next to the dump.
#   5. Installs the new schema into generated/ (embedded in the DataExtractor) and the repo root.
#   6. Rebuilds the DataExtractor and ModpackLoader in Release; the loader auto-syncs to
#      third_party/bundled/, the extractor is copied there explicitly.
#   7. Deploys the extractor into the game's Mods/ folder, writes _force_extraction.flag and,
#      unless --no-launch, starts the game through Steam. The extractor sees the flag and runs
#      without showing its dialog; templates land in <game>/UserData/ExtractedData/.
#
# Prerequisites: a MENACE install that has been launched once with MelonLoader 0.7.3
# (so MelonLoader/Il2CppAssemblies exists), a built Il2CppDumper, and a .NET 10 SDK
# (the repo-local .dotnet/ is used when present).
#
# Usage:
#   tools/update-for-game-patch.sh [--no-launch] [--skip-dump] [--full-build]
#
# Environment overrides:
#   MENACE_GAME_PATH   game folder (default: Steam's Linux location)
#   IL2CPPDUMPER_DLL   path to Il2CppDumper.dll (default: ../IL2CppDumper/.../net8.0/Il2CppDumper.dll)
#   DUMP_DIR           where to write the dump (default: ../il2cpp_dump_<version>)
#   DOTNET             dotnet executable (default: ./.dotnet/dotnet if present, else dotnet on PATH)

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

NO_LAUNCH=0
SKIP_DUMP=0
FULL_BUILD=0
for arg in "$@"; do
  case "$arg" in
    --no-launch) NO_LAUNCH=1 ;;
    --skip-dump) SKIP_DUMP=1 ;;
    --full-build) FULL_BUILD=1 ;;
    -h|--help) sed -n '2,30p' "$0"; exit 0 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

GAME="${MENACE_GAME_PATH:-$HOME/.local/share/Steam/steamapps/common/Menace}"
DOTNET="${DOTNET:-}"
if [ -z "$DOTNET" ]; then
  if [ -x "$REPO_ROOT/.dotnet/dotnet" ]; then DOTNET="$REPO_ROOT/.dotnet/dotnet"; else DOTNET="dotnet"; fi
fi
DUMPER="${IL2CPPDUMPER_DLL:-$REPO_ROOT/../IL2CppDumper/Il2CppDumper/Il2CppDumper/bin/Release/net8.0/Il2CppDumper.dll}"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$REPO_ROOT/.dotnet_cli_home}" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

step() { printf '\n\033[1;36m== %s\033[0m\n' "$*"; }
die()  { printf '\033[1;31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

[ -f "$GAME/GameAssembly.dll" ] || die "GameAssembly.dll not found under $GAME (set MENACE_GAME_PATH)"
[ -f "$GAME/Menace_Data/il2cpp_data/Metadata/global-metadata.dat" ] || die "global-metadata.dat not found under $GAME"

# --- 1. game version ---------------------------------------------------------
step "Game version"
GAME_VERSION="$(strings -n 6 "$GAME/Menace_Data/globalgamemanagers" | grep -m1 -oE 'v?[0-9]+\.[0-9]+\.[0-9]+\+[0-9]+' || true)"
UNITY_VERSION="$(strings -n 6 "$GAME/Menace_Data/globalgamemanagers" | grep -m1 -oE '^[0-9]{4}\.[0-9]+\.[0-9]+[a-z][0-9]+' || true)"
GAME_VERSION="${GAME_VERSION:-unknown}"
echo "MENACE ${GAME_VERSION}  Unity ${UNITY_VERSION:-unknown}"
SAFE_VERSION="$(echo "$GAME_VERSION" | tr -c 'A-Za-z0-9.\n' '_' | sed 's/_*$//')"
DUMP_DIR="${DUMP_DIR:-$REPO_ROOT/../il2cpp_dump_${SAFE_VERSION}}"

# --- 2. IL2CPP dump ----------------------------------------------------------
step "IL2CPP dump -> $DUMP_DIR"
if [ "$SKIP_DUMP" -eq 1 ] && [ -f "$DUMP_DIR/dump.cs" ]; then
  echo "Reusing existing dump."
else
  [ -f "$DUMPER" ] || die "Il2CppDumper.dll not found at $DUMPER (set IL2CPPDUMPER_DLL)"
  mkdir -p "$DUMP_DIR"
  # Il2CppDumper ends with a "press any key" that throws when stdin is not a console;
  # the dump is complete by then, so the exit code is ignored and the output checked instead.
  DOTNET_ROLL_FORWARD=Major "$DOTNET" "$DUMPER" "$GAME/GameAssembly.dll" \
    "$GAME/Menace_Data/il2cpp_data/Metadata/global-metadata.dat" "$DUMP_DIR/" </dev/null 2>&1 \
    | grep -vE "ReadKey|ConsolePal|Press any key|^\s+at " || true
  [ -s "$DUMP_DIR/dump.cs" ] || die "Il2CppDumper produced no dump.cs"
fi
echo "dump.cs: $(wc -l < "$DUMP_DIR/dump.cs") lines"

# Mirror into the repo's conventional (gitignored) location so the other tools' defaults work.
mkdir -p il2cpp_dump
cp -f "$DUMP_DIR/dump.cs" "$DUMP_DIR/il2cpp.h" "$DUMP_DIR/script.json" "$DUMP_DIR/stringliteral.json" il2cpp_dump/ 2>/dev/null || true
rm -rf il2cpp_dump/DummyDll && cp -r "$DUMP_DIR/DummyDll" il2cpp_dump/DummyDll 2>/dev/null || true

# --- 3. schema ---------------------------------------------------------------
step "Schema: generate, enrich with event handlers, carry descriptions"
NEW_SCHEMA="$DUMP_DIR/schema.json"
python3 tools/generate_schema.py "$DUMP_DIR/dump.cs" "$NEW_SCHEMA"
python3 extract_eventhandlers.py "$DUMP_DIR/dump.cs" "$NEW_SCHEMA" | grep -E "Found|Total fields|Updated" || true
python3 tools/carry_handler_descriptions.py "$NEW_SCHEMA" --kb eventhandler_knowledge.json --previous generated/schema.json

# --- 4. diff -----------------------------------------------------------------
step "Schema drift vs previous"
REPORT="$DUMP_DIR/schema-diff.txt"
python3 tools/diff_schemas.py generated/schema.json "$NEW_SCHEMA" > "$REPORT" 2>&1 || true
grep -E "OFFSET CHANGES|new templates|removed templates|new enums|removed enums|new structs|removed structs" "$REPORT" || true
echo "Full report: $REPORT"

# --- 5. install schema -------------------------------------------------------
step "Install schema"
cp -f "$NEW_SCHEMA" generated/schema.json
cp -f "$NEW_SCHEMA" schema.json
echo "generated/schema.json and schema.json updated ($(stat -c %s generated/schema.json) bytes)"

# --- 6. build ----------------------------------------------------------------
step "Build"
if [ "$FULL_BUILD" -eq 1 ]; then
  "$DOTNET" build Menace.Modkit.sln -c Release -nologo -v q
else
  "$DOTNET" build src/Menace.DataExtractor/Menace.DataExtractor.csproj -c Release -nologo -v q
  "$DOTNET" build src/Menace.ModpackLoader/Menace.ModpackLoader.csproj -c Release -nologo -v q
fi
cp -f src/Menace.DataExtractor/bin/Release/net6.0/Menace.DataExtractor.dll third_party/bundled/DataExtractor/Menace.DataExtractor.dll
echo "Bundled DataExtractor and ModpackLoader refreshed."

# --- 7. deploy + extract -----------------------------------------------------
step "Deploy extractor to game"
if [ ! -d "$GAME/MelonLoader" ]; then
  echo "MelonLoader is not installed in $GAME; skipping deploy. Install it (the modkit app does this) and launch once first."
  exit 0
fi
if pgrep -f '[M]enace.exe' >/dev/null 2>&1; then
  die "MENACE is running; close it so the extractor DLL can be replaced."
fi
mkdir -p "$GAME/Mods" "$GAME/UserData/ExtractedData"
cp -f third_party/bundled/DataExtractor/Menace.DataExtractor.dll "$GAME/Mods/Menace.DataExtractor.dll"
touch "$GAME/UserData/ExtractedData/_force_extraction.flag"
echo "Extractor deployed, force flag set."

if [ "$NO_LAUNCH" -eq 1 ]; then
  echo "Launch the game yourself (Steam launch option on Linux: WINEDLLOVERRIDES=\"version=n,b\" %command%)."
  echo "Templates will be written to $GAME/UserData/ExtractedData/ without any prompt."
  exit 0
fi

step "Launching MENACE for extraction"
START_TS=$(date +%s)
if command -v steam >/dev/null 2>&1; then
  (setsid steam "steam://rungameid/2432860" >/dev/null 2>&1 &)
else
  die "steam is not on PATH; launch the game manually."
fi
echo "Waiting for extraction to finish (the game will freeze for a minute or two while it runs)..."
FP="$GAME/UserData/ExtractedData/_extraction_fingerprint.txt"
for i in $(seq 1 240); do
  sleep 5
  if [ -f "$FP" ] && [ "$(stat -c %Y "$FP")" -gt "$START_TS" ]; then
    N=$(ls "$GAME/UserData/ExtractedData"/*.json 2>/dev/null | wc -l)
    echo "Extraction complete: $N template files in $GAME/UserData/ExtractedData/"
    echo "You can close the game."
    exit 0
  fi
done
echo "Timed out waiting for the extraction fingerprint. Check $GAME/MelonLoader/Latest.log."
exit 1
