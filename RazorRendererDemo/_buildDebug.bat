@echo off

set configuration=Debug
set outputPath=bin\Publish%configuration%

REM build default
echo "Creating %configuration% publish build"
rmdir /Q /S "%outputPath%"
dotnet publish --nologo --configuration %configuration% --output "%outputPath%"

rmdir /Q /S "%outputPath%\net10.0"
del /Q "%outputPath%\*"

move "%outputPath%\wwwroot\manifest.json" "%outputPath%\manifest.json"

echo "Build complete."

