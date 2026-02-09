#!/bin/bash
set -e

# Parse arguments
BUNDLE_MODE=false
for arg in "$@"; do
  case $arg in
    --bundle)
      BUNDLE_MODE=true
      shift
      ;;
  esac
done

# Use system .NET SDK
DOTNET="dotnet"

echo "Building Menace Modkit redistributables..."
echo "Using: $($DOTNET --version)"
if [ "$BUNDLE_MODE" = true ]; then
  echo "Mode: BUNDLED (includes all tools)"
else
  echo "Mode: SLIM (tools downloaded on demand)"
fi
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

echo "  → Windows x64..."
$DOTNET publish src/Menace.Modkit.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -o dist/gui-win-x64

# Build DataExtractor mod
echo ""
echo "📦 Building DataExtractor Mod..."
$DOTNET build src/Menace.DataExtractor -c Release -o dist/DataExtractor

# Update source tree bundled copy
cp dist/DataExtractor/Menace.DataExtractor.dll third_party/bundled/DataExtractor/

# Bundle DataExtractor with GUI builds (small, always include)
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

# Bundle ModpackLoader with GUI builds (small, always include)
echo "  → Bundling ModpackLoader with GUI builds..."
mkdir -p dist/gui-linux-x64/third_party/bundled/ModpackLoader
mkdir -p dist/gui-win-x64/third_party/bundled/ModpackLoader
cp dist/ModpackLoader/Menace.ModpackLoader.dll dist/gui-linux-x64/third_party/bundled/ModpackLoader/
cp dist/ModpackLoader/Menace.ModpackLoader.dll dist/gui-win-x64/third_party/bundled/ModpackLoader/

# Copy versions.json to GUI builds
echo "  → Copying versions.json to GUI builds..."
mkdir -p dist/gui-linux-x64/third_party
mkdir -p dist/gui-win-x64/third_party
cp third_party/versions.json dist/gui-linux-x64/third_party/
cp third_party/versions.json dist/gui-win-x64/third_party/

# Bundle modpacks (small, always include)
echo "  → Copying bundled modpacks..."
if [ -d "third_party/bundled/modpacks" ]; then
  mkdir -p dist/gui-linux-x64/third_party/bundled/modpacks
  mkdir -p dist/gui-win-x64/third_party/bundled/modpacks
  cp -r third_party/bundled/modpacks/* dist/gui-linux-x64/third_party/bundled/modpacks/
  cp -r third_party/bundled/modpacks/* dist/gui-win-x64/third_party/bundled/modpacks/
fi

# Bundle dotnet-refs (small, needed for mod compilation)
echo "  → Copying dotnet-refs..."
if [ -d "third_party/bundled/dotnet-refs" ]; then
  mkdir -p dist/gui-linux-x64/third_party/bundled/dotnet-refs
  mkdir -p dist/gui-win-x64/third_party/bundled/dotnet-refs
  cp -r third_party/bundled/dotnet-refs/* dist/gui-linux-x64/third_party/bundled/dotnet-refs/
  cp -r third_party/bundled/dotnet-refs/* dist/gui-win-x64/third_party/bundled/dotnet-refs/
fi

# Copy tools (doctor scripts, etc.)
echo "  → Copying tools..."
mkdir -p dist/gui-linux-x64/tools
mkdir -p dist/gui-win-x64/tools
cp tools/doctor.sh dist/gui-linux-x64/tools/
cp tools/doctor.ps1 dist/gui-win-x64/tools/
cp tools/doctor.bat dist/gui-win-x64/tools/
cp tools/doctor.ps1 dist/gui-linux-x64/tools/
cp tools/doctor.bat dist/gui-linux-x64/tools/

# Bundle documentation
echo ""
echo "📦 Bundling documentation..."
(cd docs && zip -q -r ../dist/docs.zip .)
cp dist/docs.zip dist/gui-linux-x64/
cp dist/docs.zip dist/gui-win-x64/

# Handle heavy tools based on mode
if [ "$BUNDLE_MODE" = true ]; then
  echo ""
  echo "📦 Bundling heavy tools (--bundle mode)..."

  # AssetRipper
  echo "  → Copying AssetRipper..."
  mkdir -p dist/gui-linux-x64/third_party/bundled/AssetRipper/linux
  mkdir -p dist/gui-win-x64/third_party/bundled/AssetRipper/windows
  cp -r third_party/bundled/AssetRipper/linux/* dist/gui-linux-x64/third_party/bundled/AssetRipper/linux/
  cp -r third_party/bundled/AssetRipper/windows/* dist/gui-win-x64/third_party/bundled/AssetRipper/windows/

  # Strip debug symbols from AssetRipper bundles to reduce size
  echo "  → Stripping debug symbols from AssetRipper..."
  rm -f dist/gui-linux-x64/third_party/bundled/AssetRipper/linux/*.dbg 2>/dev/null || true
  rm -f dist/gui-linux-x64/third_party/bundled/AssetRipper/linux/*.pdb 2>/dev/null || true
  rm -f dist/gui-win-x64/third_party/bundled/AssetRipper/windows/*.dbg 2>/dev/null || true
  rm -f dist/gui-win-x64/third_party/bundled/AssetRipper/windows/*.pdb 2>/dev/null || true

  # MelonLoader
  echo "  → Copying MelonLoader..."
  mkdir -p dist/gui-linux-x64/third_party/bundled/MelonLoader
  mkdir -p dist/gui-win-x64/third_party/bundled/MelonLoader
  cp -r third_party/bundled/MelonLoader/* dist/gui-linux-x64/third_party/bundled/MelonLoader/
  cp -r third_party/bundled/MelonLoader/* dist/gui-win-x64/third_party/bundled/MelonLoader/

  # TwitchServer (if exists)
  if [ -d "third_party/bundled/tools/TwitchServer" ]; then
    echo "  → Copying TwitchServer..."
    mkdir -p dist/gui-linux-x64/third_party/bundled/tools/TwitchServer
    mkdir -p dist/gui-win-x64/third_party/bundled/tools/TwitchServer
    cp -r third_party/bundled/tools/TwitchServer/* dist/gui-linux-x64/third_party/bundled/tools/TwitchServer/
    cp -r third_party/bundled/tools/TwitchServer/* dist/gui-win-x64/third_party/bundled/tools/TwitchServer/
  fi
else
  echo ""
  echo "📦 Creating separate tools archives (slim mode)..."

  # Read ToolsVersion from ModkitVersion.cs
  TOOLS_VERSION=$(grep -o 'ToolsVersion = [0-9]*' src/Shared/ModkitVersion.cs | grep -o '[0-9]*')
  if [ -z "$TOOLS_VERSION" ]; then
    TOOLS_VERSION=1
  fi
  echo "  → Tools version: $TOOLS_VERSION"

  # Create tools archive for Linux
  echo "  → Creating tools-linux-x64.tar.gz..."
  mkdir -p dist/tools-linux-x64/AssetRipper
  mkdir -p dist/tools-linux-x64/MelonLoader
  cp -r third_party/bundled/AssetRipper/linux/* dist/tools-linux-x64/AssetRipper/
  cp -r third_party/bundled/MelonLoader/* dist/tools-linux-x64/MelonLoader/
  # Strip debug symbols from AssetRipper
  rm -f dist/tools-linux-x64/AssetRipper/*.dbg 2>/dev/null || true
  rm -f dist/tools-linux-x64/AssetRipper/*.pdb 2>/dev/null || true
  if [ -d "third_party/bundled/tools/TwitchServer" ]; then
    mkdir -p dist/tools-linux-x64/TwitchServer
    cp -r third_party/bundled/tools/TwitchServer/* dist/tools-linux-x64/TwitchServer/
  fi
  # Include tools version info
  cat > dist/tools-linux-x64/tools-version.json << EOF
{
  "toolsVersion": $TOOLS_VERSION,
  "platform": "linux-x64"
}
EOF
  tar -czf dist/tools-linux-x64.tar.gz -C dist/tools-linux-x64 .

  # Create tools archive for Windows
  echo "  → Creating tools-win-x64.zip..."
  mkdir -p dist/tools-win-x64/AssetRipper
  mkdir -p dist/tools-win-x64/MelonLoader
  cp -r third_party/bundled/AssetRipper/windows/* dist/tools-win-x64/AssetRipper/
  cp -r third_party/bundled/MelonLoader/* dist/tools-win-x64/MelonLoader/
  # Strip debug symbols from AssetRipper
  rm -f dist/tools-win-x64/AssetRipper/*.dbg 2>/dev/null || true
  rm -f dist/tools-win-x64/AssetRipper/*.pdb 2>/dev/null || true
  if [ -d "third_party/bundled/tools/TwitchServer" ]; then
    mkdir -p dist/tools-win-x64/TwitchServer
    cp -r third_party/bundled/tools/TwitchServer/* dist/tools-win-x64/TwitchServer/
  fi
  # Include tools version info
  cat > dist/tools-win-x64/tools-version.json << EOF
{
  "toolsVersion": $TOOLS_VERSION,
  "platform": "win-x64"
}
EOF
  (cd dist/tools-win-x64 && zip -q -r ../tools-win-x64.zip .)
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

if [ "$BUNDLE_MODE" = true ]; then
  # Rename to indicate bundled
  mv menace-modkit-gui-linux-x64.tar.gz menace-modkit-gui-linux-x64-bundled.tar.gz
  mv menace-modkit-gui-win-x64.zip menace-modkit-gui-win-x64-bundled.zip
fi

tar -czf menace-modkit-cli-linux-x64.tar.gz -C cli-linux-x64 .
zip -q -r menace-modkit-cli-win-x64.zip cli-win-x64/

cd ..

echo ""
echo "✅ Build complete!"
echo ""
echo "Redistributables created in dist/:"
ls -lh dist/*.tar.gz dist/*.zip 2>/dev/null || true

echo ""
echo "Sizes:"
du -sh dist/gui-* dist/cli-* 2>/dev/null || true

if [ "$BUNDLE_MODE" = false ]; then
  echo ""
  echo "📋 Release checklist (slim mode):"
  echo "   1. Upload menace-modkit-gui-linux-x64.tar.gz"
  echo "   2. Upload menace-modkit-gui-win-x64.zip"
  echo "   3. Upload tools-linux-x64.tar.gz"
  echo "   4. Upload tools-win-x64.zip"
  echo "   5. Upload CLI archives if needed"
fi
