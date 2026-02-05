#!/bin/bash
# Menace Modkit Doctor - Checks system dependencies and configuration
# Run this to diagnose issues before/after installing the modkit

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

PASS="${GREEN}[OK]${NC}"
FAIL="${RED}[FAIL]${NC}"
WARN="${YELLOW}[WARN]${NC}"
INFO="${BLUE}[INFO]${NC}"

echo "========================================"
echo "  Menace Modkit Doctor"
echo "========================================"
echo ""

ISSUES=0
WARNINGS=0

# Helper functions
check_pass() { echo -e "$PASS $1"; }
check_fail() { echo -e "$FAIL $1"; ISSUES=$((ISSUES + 1)); }
check_warn() { echo -e "$WARN $1"; WARNINGS=$((WARNINGS + 1)); }
check_info() { echo -e "$INFO $1"; }

echo "== .NET Runtime =="

# Check for dotnet
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "unknown")
    check_pass "dotnet CLI found: v$DOTNET_VERSION"

    # List installed runtimes
    echo ""
    echo "Installed runtimes:"
    dotnet --list-runtimes 2>/dev/null | grep -E "NETCore|AspNetCore" | head -10 | while read line; do
        echo "  $line"
    done

    # Check for .NET 6, 8, or 10
    if dotnet --list-runtimes 2>/dev/null | grep -qE "Microsoft.NETCore.App (6\.|8\.|10\.)"; then
        check_pass "Compatible .NET runtime found (6.x, 8.x, or 10.x)"
    else
        check_warn "No .NET 6/8/10 runtime found - modpack compilation may fail"
    fi
else
    check_fail "dotnet CLI not found - install .NET 8 SDK from https://dotnet.microsoft.com/download"
fi

echo ""
echo "== Steam / Game Detection =="

# Common Steam paths on Linux
STEAM_PATHS=(
    "$HOME/.steam/debian-installation/steamapps/common"
    "$HOME/.steam/steam/steamapps/common"
    "$HOME/.local/share/Steam/steamapps/common"
)

GAME_FOUND=""
for STEAM_PATH in "${STEAM_PATHS[@]}"; do
    if [ -d "$STEAM_PATH/Menace" ]; then
        GAME_FOUND="$STEAM_PATH/Menace"
        break
    elif [ -d "$STEAM_PATH/Menace Demo" ]; then
        GAME_FOUND="$STEAM_PATH/Menace Demo"
        break
    fi
done

if [ -n "$GAME_FOUND" ]; then
    check_pass "Game found: $GAME_FOUND"

    # Check MelonLoader
    if [ -d "$GAME_FOUND/MelonLoader" ]; then
        check_pass "MelonLoader directory exists"

        # Check version.dll
        if [ -f "$GAME_FOUND/version.dll" ]; then
            check_pass "version.dll (MelonLoader proxy) exists"
        else
            check_fail "version.dll missing - MelonLoader not fully installed"
        fi

        # Check Il2CppAssemblies
        if [ -d "$GAME_FOUND/MelonLoader/Il2CppAssemblies" ]; then
            IL2CPP_COUNT=$(ls -1 "$GAME_FOUND/MelonLoader/Il2CppAssemblies"/*.dll 2>/dev/null | wc -l)
            if [ "$IL2CPP_COUNT" -gt 50 ]; then
                check_pass "Il2CppAssemblies generated ($IL2CPP_COUNT assemblies)"
            else
                check_warn "Il2CppAssemblies may be incomplete ($IL2CPP_COUNT assemblies)"
            fi
        else
            check_warn "Il2CppAssemblies not found - run the game once with MelonLoader"
        fi

        # Check Mods folder
        if [ -d "$GAME_FOUND/Mods" ]; then
            MOD_COUNT=$(ls -1 "$GAME_FOUND/Mods"/*.dll 2>/dev/null | wc -l)
            check_info "Mods folder exists ($MOD_COUNT DLLs)"
        else
            check_info "Mods folder not created yet"
        fi
    else
        check_warn "MelonLoader not installed - use modkit to deploy"
    fi
else
    check_warn "Game not found in common Steam locations"
    check_info "Set the game path manually in the modkit Settings"
fi

echo ""
echo "== Linux/Proton Setup =="

# Check if running on Linux
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    check_info "Running on Linux - game requires Proton/Wine"

    echo ""
    echo "For MelonLoader to work with Proton, add this Steam launch option:"
    echo ""
    echo "  ${YELLOW}WINEDLLOVERRIDES=\"version=n,b\" %command%${NC}"
    echo ""
    echo "Right-click game in Steam -> Properties -> Launch Options"
fi

echo ""
echo "== Modkit Bundle Check =="

# Check if we're in the modkit directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODKIT_DIR="$(dirname "$SCRIPT_DIR")"

if [ -f "$MODKIT_DIR/MenaceModkit" ] || [ -f "$MODKIT_DIR/Menace.Modkit.App" ]; then
    check_pass "Modkit executable found"
elif [ -f "$SCRIPT_DIR/../MenaceModkit" ]; then
    check_pass "Modkit executable found"
else
    check_info "Run this script from the modkit distribution folder"
fi

# Check bundled dependencies
BUNDLED="$MODKIT_DIR/third_party/bundled"
if [ -d "$BUNDLED" ]; then
    [ -d "$BUNDLED/MelonLoader" ] && check_pass "Bundled: MelonLoader" || check_fail "Missing: bundled MelonLoader"
    [ -d "$BUNDLED/DataExtractor" ] && check_pass "Bundled: DataExtractor" || check_fail "Missing: bundled DataExtractor"
    [ -d "$BUNDLED/ModpackLoader" ] && check_pass "Bundled: ModpackLoader" || check_fail "Missing: bundled ModpackLoader"
fi

echo ""
echo "========================================"
if [ $ISSUES -gt 0 ]; then
    echo -e "${RED}Found $ISSUES issue(s) and $WARNINGS warning(s)${NC}"
    echo "Please fix the issues above before using the modkit."
    exit 1
elif [ $WARNINGS -gt 0 ]; then
    echo -e "${YELLOW}Found $WARNINGS warning(s), but should work${NC}"
    exit 0
else
    echo -e "${GREEN}All checks passed!${NC}"
    exit 0
fi
