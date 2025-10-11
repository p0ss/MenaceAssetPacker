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
mkdir -p dist/gui-linux-x64/third_party/bundled
cp -r third_party/bundled/* dist/gui-linux-x64/third_party/bundled/

echo "  → Windows x64..."
$DOTNET publish src/Menace.Modkit.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -o dist/gui-win-x64

# Manually copy bundled files (PublishSingleFile excludes them)
echo "  → Copying bundled dependencies..."
mkdir -p dist/gui-win-x64/third_party/bundled
cp -r third_party/bundled/* dist/gui-win-x64/third_party/bundled/

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
