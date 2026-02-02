#!/bin/bash
set -e

# Use system .NET SDK
DOTNET="dotnet"

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

# Update source tree bundled copy
cp dist/DataExtractor/Menace.DataExtractor.dll third_party/bundled/DataExtractor/

# Bundle DataExtractor with GUI builds
echo "  → Bundling DataExtractor with GUI builds..."
mkdir -p dist/gui-linux-x64/third_party/bundled/DataExtractor
mkdir -p dist/gui-win-x64/third_party/bundled/DataExtractor
cp dist/DataExtractor/Menace.DataExtractor.dll dist/gui-linux-x64/third_party/bundled/DataExtractor/
cp dist/DataExtractor/Menace.DataExtractor.dll dist/gui-win-x64/third_party/bundled/DataExtractor/

# Build ModpackLoader mod
echo ""
echo "📦 Building ModpackLoader Mod..."
$DOTNET build src/Menace.ModpackLoader -c Release -o dist/ModpackLoader

# Update source tree bundled copy
mkdir -p third_party/bundled/ModpackLoader
cp dist/ModpackLoader/Menace.ModpackLoader.dll third_party/bundled/ModpackLoader/

# Bundle ModpackLoader with GUI builds
echo "  → Bundling ModpackLoader with GUI builds..."
mkdir -p dist/gui-linux-x64/third_party/bundled/ModpackLoader
mkdir -p dist/gui-win-x64/third_party/bundled/ModpackLoader
cp dist/ModpackLoader/Menace.ModpackLoader.dll dist/gui-linux-x64/third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/Menace.ModpackLoader.dll dist/gui-win-x64/third_party/bundled/ModpackLoader/

# Build CombinedArms mod
echo ""
echo "📦 Building CombinedArms Mod..."
$DOTNET build src/Menace.CombinedArms -c Release -o dist/CombinedArms

# Update source tree bundled copy
mkdir -p third_party/bundled/CombinedArms
cp dist/CombinedArms/Menace.CombinedArms.dll third_party/bundled/CombinedArms/

# Bundle CombinedArms with GUI builds
echo "  → Bundling CombinedArms with GUI builds..."
mkdir -p dist/gui-linux-x64/third_party/bundled/CombinedArms
mkdir -p dist/gui-win-x64/third_party/bundled/CombinedArms
cp dist/CombinedArms/Menace.CombinedArms.dll dist/gui-linux-x64/third_party/bundled/CombinedArms/
cp dist/CombinedArms/Menace.CombinedArms.dll dist/gui-win-x64/third_party/bundled/CombinedArms/

# Re-copy modpack sources to dist (picks up any source fixes made after initial build)
echo "  → Refreshing bundled modpack sources in dist..."
if [ -d "third_party/bundled/modpacks" ]; then
  cp -r third_party/bundled/modpacks/* dist/gui-linux-x64/third_party/bundled/modpacks/
  cp -r third_party/bundled/modpacks/* dist/gui-win-x64/third_party/bundled/modpacks/
fi

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
