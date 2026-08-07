@echo off
:: Thin wrapper - real build logic lives in build.cs (cross-platform, pure C#).
cd /d "%~dp0"
dotnet run --file "build.cs" -- Debug --no-zip
