#!/bin/bash
set -e

# Use .NET 9 SDK
DOTNET="$HOME/.dotnet9/dotnet"

echo "Building Menace Modkit redistributables..."
echo "Using: $($DOTNET --version)"
echo ""

# Clean previous builds
rm -rf dist/
mkdir -p dist

# Build GUI for each platform
echo "📦 Building GUI App..."

echo "  → Linux x64..."
$DOTNET publish src/Menace.Modkit.App -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -o dist/gui-linux-x64

# Manually copy bundled files for Linux (PublishSingleFile excludes them)
echo "  → Copying bundled dependencies..."
mkdir -p dist/gui-linux-x64/third_party/bundled/AssetRipper/linux
cp -r third_party/bundled/AssetRipper/linux/* dist/gui-linux-x64/third_party/bundled/AssetRipper/linux/
# Copy shared bundled files if any exist (excluding AssetRipper)
if [ -d "third_party/bundled" ]; then
  for item in third_party/bundled/*; do
    if [ "$(basename "$item")" != "AssetRipper" ]; then
      cp -r "$item" dist/gui-linux-x64/third_party/bundled/
    fi
  done
fi

echo "  → Windows x64..."
$DOTNET publish src/Menace.Modkit.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -o dist/gui-win-x64

# Manually copy bundled files for Windows (PublishSingleFile excludes them)
echo "  → Copying bundled dependencies..."
mkdir -p dist/gui-win-x64/third_party/bundled/AssetRipper/windows
cp -r third_party/bundled/AssetRipper/windows/* dist/gui-win-x64/third_party/bundled/AssetRipper/windows/
# Copy shared bundled files if any exist (excluding AssetRipper)
if [ -d "third_party/bundled" ]; then
  for item in third_party/bundled/*; do
    if [ "$(basename "$item")" != "AssetRipper" ]; then
      cp -r "$item" dist/gui-win-x64/third_party/bundled/
    fi
  done
fi

# Build DataExtractor mod
echo ""
echo "📦 Building DataExtractor Mod..."
$DOTNET build src/Menace.DataExtractor -c Release -o dist/DataExtractor

# Bundle DataExtractor with GUI builds
echo "  → Bundling DataExtractor with GUI builds..."
mkdir -p dist/gui-linux-x64/third_party/bundled/DataExtractor
mkdir -p dist/gui-win-x64/third_party/bundled/DataExtractor
cp dist/DataExtractor/Menace.DataExtractor.dll dist/gui-linux-x64/third_party/bundled/DataExtractor/
cp dist/DataExtractor/Menace.DataExtractor.dll dist/gui-win-x64/third_party/bundled/DataExtractor/

# Build CLI for each platform
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

# Create archives
echo ""
echo "📦 Creating archives..."

cd dist

tar -czf menace-modkit-gui-linux-x64.tar.gz -C gui-linux-x64 .
zip -q -r menace-modkit-gui-win-x64.zip gui-win-x64/

tar -czf menace-modkit-cli-linux-x64.tar.gz -C cli-linux-x64 .
zip -q -r menace-modkit-cli-win-x64.zip cli-win-x64/

cd ..

echo ""
echo "✅ Build complete!"
echo ""
echo "Redistributables created in dist/:"
ls -lh dist/*.tar.gz dist/*.zip

echo ""
echo "Sizes:"
du -sh dist/gui-* dist/cli-*
