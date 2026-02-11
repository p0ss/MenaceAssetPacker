#!/bin/bash
set -e

# Build script for Menace Modkit redistributables
# This creates component archives for GitHub releases and slim app builds

# Use system .NET SDK
DOTNET="dotnet"

echo "Building Menace Modkit redistributables..."
echo "Using: $($DOTNET --version)"
echo ""

# =============================================================================
# Generate ModkitVersion.cs from versions.json (single source of truth)
# =============================================================================

echo "📦 Generating ModkitVersion.cs from versions.json..."

# Release version for post-build instructions (overridden from versions.json when jq is available)
RELEASE_VERSION="1.0.0"

# Extract version from versions.json (requires jq)
if command -v jq &> /dev/null; then
  LOADER_VERSION=$(jq -r '.components.ModpackLoader.version' third_party/versions.json)
  RELEASE_VERSION="$LOADER_VERSION"
  # Extract major version number for BuildNumber (e.g., "19.0.0" -> 19)
  BUILD_NUMBER=$(echo "$LOADER_VERSION" | cut -d. -f1)

  echo "  → ModpackLoader version: $LOADER_VERSION (build $BUILD_NUMBER)"

  cat > src/Shared/ModkitVersion.cs << EOF
// AUTO-GENERATED from third_party/versions.json - DO NOT EDIT MANUALLY
// Run build-redistributables.sh to regenerate

namespace Menace;

/// <summary>
/// Centralized version information for the Menace Modkit ecosystem.
/// This file is generated from versions.json and linked into both
/// the desktop app and the in-game loader.
/// </summary>
public static class ModkitVersion
{
    /// <summary>
    /// The current build number. Derived from ModpackLoader version in versions.json.
    /// </summary>
    public const int BuildNumber = $BUILD_NUMBER;

    /// <summary>
    /// Version string for MelonLoader attribute (must be compile-time constant).
    /// </summary>
    public const string MelonVersion = "$LOADER_VERSION";

    /// <summary>
    /// Short display version (e.g., "v19").
    /// </summary>
    public const string Short = "v$BUILD_NUMBER";

    /// <summary>
    /// Full version for the Modkit App.
    /// </summary>
    public const string AppFull = "Menace Modkit v$BUILD_NUMBER";

    /// <summary>
    /// Full version for the Modpack Loader.
    /// </summary>
    public const string LoaderFull = "Menace Modpack Loader v$BUILD_NUMBER";
}
EOF

  echo "  → Generated src/Shared/ModkitVersion.cs"
else
  echo "  ⚠ jq not found, skipping ModkitVersion.cs generation"
  echo "    Install jq to enable automatic version sync: apt install jq / brew install jq"
fi

echo ""

# Clean previous builds
rm -rf dist/
mkdir -p dist
mkdir -p dist/components

# =============================================================================
# Build DLLs
# =============================================================================

echo "📦 Building DataExtractor Mod..."
$DOTNET build src/Menace.DataExtractor -c Release -o dist/DataExtractor

# Update source tree bundled copy
cp dist/DataExtractor/Menace.DataExtractor.dll third_party/bundled/DataExtractor/

echo ""
echo "📦 Building ModpackLoader Mod..."
$DOTNET build src/Menace.ModpackLoader -c Release -o dist/ModpackLoader

# Update source tree bundled copy (main DLL + Roslyn dependencies for REPL + SharpGLTF for GLB loading)
mkdir -p third_party/bundled/ModpackLoader
cp dist/ModpackLoader/Menace.ModpackLoader.dll third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/Microsoft.CodeAnalysis.dll third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/Microsoft.CodeAnalysis.CSharp.dll third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/System.Collections.Immutable.dll third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/System.Reflection.Metadata.dll third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/SharpGLTF.Core.dll third_party/bundled/ModpackLoader/

# =============================================================================
# Build GUI App
# =============================================================================

echo ""
echo "📦 Building GUI App..."

echo "  → Linux x64..."
$DOTNET publish src/Menace.Modkit.App -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -o dist/gui-linux-x64

echo "  → Windows x64..."
$DOTNET publish src/Menace.Modkit.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -o dist/gui-win-x64

# =============================================================================
# Bundle core files with GUI (versions.json for component downloads)
# =============================================================================

echo ""
echo "📦 Bundling core files with GUI..."

# Copy versions.json
mkdir -p dist/gui-linux-x64/third_party
mkdir -p dist/gui-win-x64/third_party
cp third_party/versions.json dist/gui-linux-x64/third_party/
cp third_party/versions.json dist/gui-win-x64/third_party/

# Copy bundled components as fallback (used when GitHub releases aren't available yet)
# Primary flow is download from GitHub; this enables pre-release testing
echo "  → Copying bundled components (fallback)..."
cp -r third_party/bundled dist/gui-linux-x64/third_party/
cp -r third_party/bundled dist/gui-win-x64/third_party/

# =============================================================================
# Create Component Archives for GitHub Release
# =============================================================================

echo ""
echo "📦 Creating component archives..."

# DataExtractor (platform-independent)
echo "  → DataExtractor.zip..."
mkdir -p dist/component-DataExtractor
cp dist/DataExtractor/Menace.DataExtractor.dll dist/component-DataExtractor/
(cd dist/component-DataExtractor && zip -q -r ../components/DataExtractor.zip .)

# ModpackLoader + Roslyn + SharpGLTF (platform-independent)
echo "  → ModpackLoader.zip..."
mkdir -p dist/component-ModpackLoader
cp dist/ModpackLoader/Menace.ModpackLoader.dll dist/component-ModpackLoader/
cp dist/ModpackLoader/Microsoft.CodeAnalysis.dll dist/component-ModpackLoader/
cp dist/ModpackLoader/Microsoft.CodeAnalysis.CSharp.dll dist/component-ModpackLoader/
cp dist/ModpackLoader/System.Collections.Immutable.dll dist/component-ModpackLoader/
cp dist/ModpackLoader/System.Reflection.Metadata.dll dist/component-ModpackLoader/
cp dist/ModpackLoader/SharpGLTF.Core.dll dist/component-ModpackLoader/
(cd dist/component-ModpackLoader && zip -q -r ../components/ModpackLoader.zip .)

# DotNetRefs (platform-independent)
if [ -d "third_party/bundled/dotnet-refs" ]; then
  echo "  → DotNetRefs.zip..."
  (cd third_party/bundled/dotnet-refs && zip -q -r ../../../dist/components/DotNetRefs.zip .)
fi

# MelonLoader (platform-specific)
if [ -d "third_party/bundled/MelonLoader" ]; then
  echo "  → MelonLoader-linux-x64.tar.gz..."
  tar -czf dist/components/MelonLoader-linux-x64.tar.gz \
    -C third_party/bundled/MelonLoader .

  echo "  → MelonLoader-win-x64.zip..."
  (cd third_party/bundled/MelonLoader && \
    zip -q -r ../../../dist/components/MelonLoader-win-x64.zip .)
fi

# AssetRipper (platform-specific)
if [ -d "third_party/bundled/AssetRipper/linux" ]; then
  echo "  → AssetRipper-linux-x64.tar.gz..."
  # Remove debug symbols first
  find third_party/bundled/AssetRipper -name "*.pdb" -delete 2>/dev/null || true
  find third_party/bundled/AssetRipper -name "*.dbg" -delete 2>/dev/null || true
  tar -czf dist/components/AssetRipper-linux-x64.tar.gz \
    -C third_party/bundled/AssetRipper/linux .
fi

if [ -d "third_party/bundled/AssetRipper/windows" ]; then
  echo "  → AssetRipper-win-x64.zip..."
  (cd third_party/bundled/AssetRipper/windows && \
    zip -q -r ../../../../dist/components/AssetRipper-win-x64.zip .)
fi

# DevMode modpack (source, platform-independent)
if [ -d "third_party/bundled/modpacks/DevMode-modpack" ]; then
  echo "  → DevMode.zip..."
  (cd third_party/bundled/modpacks/DevMode-modpack && \
    zip -q -r ../../../../dist/components/DevMode.zip . -x "*.obj" -x "bin/*" -x "obj/*")
fi

# TwitchSquaddies modpack (source, platform-independent)
if [ -d "third_party/bundled/modpacks/TwitchSquaddies-modpack" ]; then
  echo "  → TwitchSquaddies.zip..."
  (cd third_party/bundled/modpacks/TwitchSquaddies-modpack && \
    zip -q -r ../../../../dist/components/TwitchSquaddies.zip . -x "*.obj" -x "bin/*" -x "obj/*")
fi

# =============================================================================
# Generate Checksums and Manifest
# =============================================================================

echo ""
echo "📦 Generating checksums..."

cd dist/components

# Generate SHA256 for each archive
for file in *.zip *.tar.gz; do
  if [ -f "$file" ]; then
    sha256sum "$file" > "$file.sha256"
  fi
done

# Create manifest.json with all checksums
echo "  → manifest.json..."
echo "{" > manifest.json
echo '  "generatedAt": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'",' >> manifest.json
echo '  "components": {' >> manifest.json

first=true
for file in *.sha256; do
  if [ -f "$file" ]; then
    base="${file%.sha256}"
    hash=$(cat "$file" | cut -d' ' -f1)
    size=$(stat -c%s "$base" 2>/dev/null || stat -f%z "$base")
    if [ "$first" = true ]; then
      first=false
    else
      echo ',' >> manifest.json
    fi
    echo -n "    \"$base\": {\"sha256\": \"$hash\", \"size\": $size}" >> manifest.json
  fi
done
echo '' >> manifest.json
echo '  }' >> manifest.json
echo '}' >> manifest.json

cd ../..

# =============================================================================
# Build CLI Tool
# =============================================================================

echo ""
echo "📦 Building CLI Tool..."

echo "  → Linux x64..."
$DOTNET publish src/Menace.Modkit.Cli -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -o dist/cli-linux-x64

echo "  → Windows x64..."
$DOTNET publish src/Menace.Modkit.Cli -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -o dist/cli-win-x64

# =============================================================================
# Create App Archives
# =============================================================================

echo ""
echo "📦 Creating app archives..."

cd dist
tar -czf menace-modkit-gui-linux-x64.tar.gz -C gui-linux-x64 .
zip -q -r menace-modkit-gui-win-x64.zip gui-win-x64/
tar -czf menace-modkit-cli-linux-x64.tar.gz -C cli-linux-x64 .
zip -q -r menace-modkit-cli-win-x64.zip cli-win-x64/
cd ..

# Note: docs are not bundled separately - accessible via the Docs section in the app UI

# =============================================================================
# Summary
# =============================================================================

echo ""
echo "✅ Build complete!"
echo ""
echo "App archives (dist/):"
ls -lh dist/*.tar.gz dist/*.zip 2>/dev/null | grep -v components || true

echo ""
echo "Component archives (dist/components/):"
ls -lh dist/components/*.zip dist/components/*.tar.gz 2>/dev/null || true

echo ""
echo "Component manifest:"
cat dist/components/manifest.json

echo ""
echo "📋 Release workflow:"
echo ""
echo "   1. Create components release (from local build):"
echo "      gh release create components-v$RELEASE_VERSION dist/components/* --title 'Components for v$RELEASE_VERSION' --notes 'Component archives for Menace Modkit v$RELEASE_VERSION. Downloaded automatically by the app.'"
echo ""
echo "   2. Push GUI release tag (CI builds the app):"
echo "      git tag v$RELEASE_VERSION && git push origin v$RELEASE_VERSION"
echo ""
echo "   Or upload components to existing release:"
echo "      gh release upload components-v$RELEASE_VERSION dist/components/* --clobber"
