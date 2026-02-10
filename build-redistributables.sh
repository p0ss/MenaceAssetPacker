#!/bin/bash
set -e

# Build script for Menace Modkit redistributables
# This creates component archives for GitHub releases and slim app builds

# Use system .NET SDK
DOTNET="dotnet"

echo "Building Menace Modkit redistributables..."
echo "Using: $($DOTNET --version)"
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

# Update source tree bundled copy (main DLL + Roslyn dependencies for REPL)
mkdir -p third_party/bundled/ModpackLoader
cp dist/ModpackLoader/Menace.ModpackLoader.dll third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/Microsoft.CodeAnalysis.dll third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/Microsoft.CodeAnalysis.CSharp.dll third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/System.Collections.Immutable.dll third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/System.Reflection.Metadata.dll third_party/bundled/ModpackLoader/

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
# Bundle core files with GUI (versions.json, tools scripts)
# =============================================================================

echo ""
echo "📦 Bundling core files with GUI..."

# Copy versions.json
mkdir -p dist/gui-linux-x64/third_party
mkdir -p dist/gui-win-x64/third_party
cp third_party/versions.json dist/gui-linux-x64/third_party/
cp third_party/versions.json dist/gui-win-x64/third_party/

# Copy tools (doctor scripts)
mkdir -p dist/gui-linux-x64/tools
mkdir -p dist/gui-win-x64/tools
cp tools/doctor.sh dist/gui-linux-x64/tools/
cp tools/doctor.ps1 dist/gui-win-x64/tools/
cp tools/doctor.bat dist/gui-win-x64/tools/

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

# ModpackLoader + Roslyn (platform-independent)
echo "  → ModpackLoader.zip..."
mkdir -p dist/component-ModpackLoader
cp dist/ModpackLoader/Menace.ModpackLoader.dll dist/component-ModpackLoader/
cp dist/ModpackLoader/Microsoft.CodeAnalysis.dll dist/component-ModpackLoader/
cp dist/ModpackLoader/Microsoft.CodeAnalysis.CSharp.dll dist/component-ModpackLoader/
cp dist/ModpackLoader/System.Collections.Immutable.dll dist/component-ModpackLoader/
cp dist/ModpackLoader/System.Reflection.Metadata.dll dist/component-ModpackLoader/
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

# Create docs archive
if [ -d "docs" ]; then
  echo "  → docs.zip..."
  (cd docs && zip -q -r ../dist/docs.zip .)
fi

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
echo "   1. Push a tag (e.g., git tag v1.0.0 && git push origin v1.0.0)"
echo "   2. GitHub Actions will build and create the release automatically"
echo "   3. Or run manually: gh workflow run release.yml -f version=1.0.0"
