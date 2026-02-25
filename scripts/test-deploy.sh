#!/bin/bash
# Automated deployment test script
# Tests the full deploy -> verify cycle using MCP endpoints

set -e

GAME_MCP="http://127.0.0.1:7655"
MODKIT_MCP="http://127.0.0.1:7654"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Check if game MCP is running
check_game_mcp() {
    log_info "Checking game MCP server..."
    if curl -s --connect-timeout 2 "$GAME_MCP/status" > /dev/null 2>&1; then
        log_info "Game MCP is running"
        return 0
    else
        log_error "Game MCP not responding at $GAME_MCP"
        log_error "Make sure game is running with MCP Server enabled"
        return 1
    fi
}

# Get game status
get_status() {
    log_info "Game status:"
    curl -s "$GAME_MCP/status" | jq .
}

# Check CompiledAssetLoader status
check_assets() {
    log_info "Checking CompiledAssetLoader status..."
    local code='return new { HasManifest = Menace.ModpackLoader.CompiledAssetLoader.HasManifest, ManifestCount = Menace.ModpackLoader.CompiledAssetLoader.ManifestAssetCount, LoadedCount = Menace.ModpackLoader.CompiledAssetLoader.LoadedAssetCount };'
    curl -s "$GAME_MCP/repl?code=$(echo -n "$code" | jq -sRr @uri)" | jq .
}

# Check for a specific template
check_template() {
    local template_id="$1"
    log_info "Checking template: $template_id"
    curl -s "$GAME_MCP/template?id=$template_id" | jq .
}

# List templates of a type
list_templates() {
    local type="${1:-EntityTemplate}"
    local limit="${2:-10}"
    log_info "Listing $type templates (limit $limit)..."
    curl -s "$GAME_MCP/templates?type=$type&limit=$limit" | jq .
}

# Check for errors
check_errors() {
    log_info "Checking for mod errors..."
    local result=$(curl -s "$GAME_MCP/errors")
    local error_count=$(echo "$result" | jq '.errors | length' 2>/dev/null || echo "0")

    if [ "$error_count" = "0" ]; then
        log_info "No errors found"
    else
        log_error "Found $error_count errors:"
        echo "$result" | jq '.errors'
    fi
}

# Execute REPL code
repl() {
    local code="$1"
    log_info "Executing REPL: $code"
    curl -s "$GAME_MCP/repl?code=$(echo -n "$code" | jq -sRr @uri)" | jq .
}

# Main test sequence
run_tests() {
    log_info "=== Starting Asset Deployment Tests ==="

    check_game_mcp || exit 1

    echo ""
    get_status

    echo ""
    check_assets

    echo ""
    check_errors

    # Check specific clones if provided as arguments
    if [ $# -gt 0 ]; then
        for template in "$@"; do
            echo ""
            check_template "$template"
        done
    fi

    log_info "=== Tests Complete ==="
}

# Show help
show_help() {
    echo "Usage: $0 [command] [args...]"
    echo ""
    echo "Commands:"
    echo "  test [templates...]   Run full test sequence, optionally check specific templates"
    echo "  status               Get game status"
    echo "  assets               Check CompiledAssetLoader status"
    echo "  errors               Check for mod errors"
    echo "  template <id>        Check specific template"
    echo "  templates [type]     List templates of type (default: EntityTemplate)"
    echo "  repl <code>          Execute REPL code"
    echo ""
    echo "Examples:"
    echo "  $0 test weapon.laser_smg entity.my_clone"
    echo "  $0 template weapon.laser_rifle"
    echo "  $0 repl 'return DataTemplateLoader.GetAll<EntityTemplate>().Count'"
}

# Parse command
case "${1:-test}" in
    test)
        shift
        run_tests "$@"
        ;;
    status)
        check_game_mcp && get_status
        ;;
    assets)
        check_game_mcp && check_assets
        ;;
    errors)
        check_game_mcp && check_errors
        ;;
    template)
        check_game_mcp && check_template "$2"
        ;;
    templates)
        check_game_mcp && list_templates "$2" "$3"
        ;;
    repl)
        check_game_mcp && repl "$2"
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        log_error "Unknown command: $1"
        show_help
        exit 1
        ;;
esac
