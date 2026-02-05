@echo off
:: Menace Modkit Doctor - Windows launcher
:: This runs the PowerShell diagnostic script

powershell -ExecutionPolicy Bypass -File "%~dp0doctor.ps1"
