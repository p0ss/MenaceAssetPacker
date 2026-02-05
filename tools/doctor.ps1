# Menace Modkit Doctor - Checks system dependencies and configuration
# Run this to diagnose issues before/after installing the modkit
# Usage: Right-click -> Run with PowerShell, or: powershell -ExecutionPolicy Bypass -File doctor.ps1

$Host.UI.RawUI.WindowTitle = "Menace Modkit Doctor"

function Write-Pass { param($msg) Write-Host "[OK] " -ForegroundColor Green -NoNewline; Write-Host $msg }
function Write-Fail { param($msg) Write-Host "[FAIL] " -ForegroundColor Red -NoNewline; Write-Host $msg; $script:Issues++ }
function Write-Warn { param($msg) Write-Host "[WARN] " -ForegroundColor Yellow -NoNewline; Write-Host $msg; $script:Warnings++ }
function Write-Info { param($msg) Write-Host "[INFO] " -ForegroundColor Cyan -NoNewline; Write-Host $msg }

$script:Issues = 0
$script:Warnings = 0

Write-Host "========================================"
Write-Host "  Menace Modkit Doctor"
Write-Host "========================================"
Write-Host ""

Write-Host "== .NET Runtime ==" -ForegroundColor White

# Check for dotnet
$dotnetPath = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetPath) {
    $dotnetVersion = & dotnet --version 2>$null
    Write-Pass "dotnet CLI found: v$dotnetVersion"

    Write-Host ""
    Write-Host "Installed runtimes:"
    $runtimes = & dotnet --list-runtimes 2>$null
    $runtimes | Where-Object { $_ -match "NETCore|AspNetCore" } | Select-Object -First 10 | ForEach-Object {
        Write-Host "  $_"
    }

    # Check for compatible runtime
    if ($runtimes -match "Microsoft.NETCore.App (6\.|8\.|10\.)") {
        Write-Pass "Compatible .NET runtime found (6.x, 8.x, or 10.x)"
    } else {
        Write-Warn "No .NET 6/8/10 runtime found - modpack compilation may fail"
    }
} else {
    Write-Fail "dotnet CLI not found - install .NET 8 SDK from https://dotnet.microsoft.com/download"
}

Write-Host ""
Write-Host "== Steam / Game Detection ==" -ForegroundColor White

# Common Steam paths on Windows
$steamPaths = @(
    "${env:ProgramFiles(x86)}\Steam\steamapps\common",
    "${env:ProgramFiles}\Steam\steamapps\common",
    "D:\SteamLibrary\steamapps\common",
    "E:\SteamLibrary\steamapps\common",
    "F:\SteamLibrary\steamapps\common"
)

# Also check Steam's libraryfolders.vdf for additional paths
$steamConfigPath = "${env:ProgramFiles(x86)}\Steam\steamapps\libraryfolders.vdf"
if (Test-Path $steamConfigPath) {
    $vdfContent = Get-Content $steamConfigPath -Raw
    $matches = [regex]::Matches($vdfContent, '"path"\s+"([^"]+)"')
    foreach ($match in $matches) {
        $libPath = $match.Groups[1].Value -replace '\\\\', '\'
        $commonPath = Join-Path $libPath "steamapps\common"
        if ($commonPath -notin $steamPaths) {
            $steamPaths += $commonPath
        }
    }
}

$gameFound = $null
foreach ($steamPath in $steamPaths) {
    $menacePath = Join-Path $steamPath "Menace"
    $demoPath = Join-Path $steamPath "Menace Demo"

    if (Test-Path $menacePath) {
        $gameFound = $menacePath
        break
    } elseif (Test-Path $demoPath) {
        $gameFound = $demoPath
        break
    }
}

if ($gameFound) {
    Write-Pass "Game found: $gameFound"

    # Check MelonLoader
    $mlPath = Join-Path $gameFound "MelonLoader"
    if (Test-Path $mlPath) {
        Write-Pass "MelonLoader directory exists"

        # Check version.dll
        $versionDll = Join-Path $gameFound "version.dll"
        if (Test-Path $versionDll) {
            Write-Pass "version.dll (MelonLoader proxy) exists"
        } else {
            Write-Fail "version.dll missing - MelonLoader not fully installed"
        }

        # Check Il2CppAssemblies
        $il2cppPath = Join-Path $mlPath "Il2CppAssemblies"
        if (Test-Path $il2cppPath) {
            $il2cppCount = (Get-ChildItem $il2cppPath -Filter "*.dll" -ErrorAction SilentlyContinue).Count
            if ($il2cppCount -gt 50) {
                Write-Pass "Il2CppAssemblies generated ($il2cppCount assemblies)"
            } else {
                Write-Warn "Il2CppAssemblies may be incomplete ($il2cppCount assemblies)"
            }
        } else {
            Write-Warn "Il2CppAssemblies not found - run the game once with MelonLoader"
        }

        # Check Mods folder
        $modsPath = Join-Path $gameFound "Mods"
        if (Test-Path $modsPath) {
            $modCount = (Get-ChildItem $modsPath -Filter "*.dll" -ErrorAction SilentlyContinue).Count
            Write-Info "Mods folder exists ($modCount DLLs)"
        } else {
            Write-Info "Mods folder not created yet"
        }
    } else {
        Write-Warn "MelonLoader not installed - use modkit to deploy"
    }
} else {
    Write-Warn "Game not found in common Steam locations"
    Write-Info "Set the game path manually in the modkit Settings"
}

Write-Host ""
Write-Host "== Visual C++ Runtime ==" -ForegroundColor White

# Check for VC++ Redistributable (needed by some native components)
$vcInstalled = $false
$vcKeys = @(
    "HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
)
foreach ($key in $vcKeys) {
    if (Test-Path $key) {
        $vcInstalled = $true
        break
    }
}

if ($vcInstalled) {
    Write-Pass "Visual C++ Redistributable installed"
} else {
    Write-Warn "Visual C++ Redistributable may not be installed"
    Write-Info "Download from: https://aka.ms/vs/17/release/vc_redist.x64.exe"
}

Write-Host ""
Write-Host "== Modkit Bundle Check ==" -ForegroundColor White

# Check if we're in the modkit directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$modkitDir = Split-Path -Parent $scriptDir

$exeFound = $false
if (Test-Path (Join-Path $modkitDir "MenaceModkit.exe")) { $exeFound = $true }
if (Test-Path (Join-Path $modkitDir "Menace.Modkit.App.exe")) { $exeFound = $true }

if ($exeFound) {
    Write-Pass "Modkit executable found"
} else {
    Write-Info "Run this script from the modkit distribution folder"
}

# Check bundled dependencies
$bundled = Join-Path $modkitDir "third_party\bundled"
if (Test-Path $bundled) {
    if (Test-Path (Join-Path $bundled "MelonLoader")) { Write-Pass "Bundled: MelonLoader" } else { Write-Fail "Missing: bundled MelonLoader" }
    if (Test-Path (Join-Path $bundled "DataExtractor")) { Write-Pass "Bundled: DataExtractor" } else { Write-Fail "Missing: bundled DataExtractor" }
    if (Test-Path (Join-Path $bundled "ModpackLoader")) { Write-Pass "Bundled: ModpackLoader" } else { Write-Fail "Missing: bundled ModpackLoader" }
}

Write-Host ""
Write-Host "========================================"

if ($script:Issues -gt 0) {
    Write-Host "Found $($script:Issues) issue(s) and $($script:Warnings) warning(s)" -ForegroundColor Red
    Write-Host "Please fix the issues above before using the modkit."
} elseif ($script:Warnings -gt 0) {
    Write-Host "Found $($script:Warnings) warning(s), but should work" -ForegroundColor Yellow
} else {
    Write-Host "All checks passed!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
