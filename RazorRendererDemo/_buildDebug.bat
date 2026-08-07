@echo off
setlocal enabledelayedexpansion

:: --- CONFIGURATION FLAGS ---
set "configuration=Debug"
set "disableZip=true"
:: ---------------------------

set "outputPath=bin\Publish%configuration%"
set "baseManifest=%outputPath%\wwwroot\manifest.json"

:: 1. Build default
echo Creating %configuration% publish build...
if exist "%outputPath%" rmdir /Q /S "%outputPath%"
call dotnet publish --nologo --configuration %configuration% --output "%outputPath%"

:: 2. Check if the base manifest.json exists. If not, exit.
if not exist "%baseManifest%" (
    echo Error: Base manifest not found at "%baseManifest%". Exiting.
    exit /b 1
)

:: Keep track of if we found any browser-specific manifests
set "found_cnt=0"

:: 3. Process each browser-specific manifest found in the wwwroot folder
for %%F in ("%outputPath%\wwwroot\manifest.*.json") do (
    set /a found_cnt+=1
    
    :: Extract the browser name from manifest.[browser].json
    set "filename=%%~nF"
    set "browser=!filename:manifest.=!"
    
    echo.
    echo Processing browser target: !browser!
    
    :: Define target folders
    set "targetDir=%outputPath%\!browser!"
    set "targetWwwroot=!targetDir!\wwwroot"
    
    :: (Re)create the target folder structure
    if exist "!targetDir!" rmdir /Q /S "!targetDir!"
    mkdir "!targetWwwroot!"
    
    :: Copy wwwroot to the target folder
    xcopy /I /E /Y /Q "%outputPath%\wwwroot" "!targetWwwroot!" >nul
    
    :: Move the base manifest.json to the target folder (one up from wwwroot)
    move "!targetWwwroot!\manifest.json" "!targetDir!\manifest.json" >nul
    
    :: Merge browser-specific manifest with the base manifest
    echo Merging manifest.!browser!.json keys into the base manifest...
    powershell -Command ^
        "$base = Get-Content '!targetDir!\manifest.json' | ConvertFrom-Json;" ^
        "$specific = Get-Content '!targetWwwroot!\manifest.!browser!.json' | ConvertFrom-Json;" ^
        "foreach ($prop in $specific.psobject.Properties) { $base | Add-Member -MemberType NoteProperty -Name $prop.Name -Value $prop.Value -Force };" ^
        "$base | ConvertTo-Json -Depth 100 | Set-Content '!targetDir!\manifest.json'"
        
    :: Delete all manifest.*.json from target/wwwroot (no longer needed there)
    del /Q "!targetWwwroot!\manifest.*.json" >nul
    
    :: Zip target folder for submission (conditional check)
    if /i "!disableZip!"=="true" (
        echo Skipping compression ^(zipping disabled^).
    ) else (
        echo Zipping target folder for !browser! submission...
        if exist "!targetDir!.zip" del /Q "!targetDir!.zip"
        powershell -Command "Compress-Archive -Path '!targetDir!\*' -DestinationPath '!targetDir!.zip' -Force"
    )
)

:: 4. Fallback if found_cnt == 0 (use generic 'browser' name)
if !found_cnt! equ 0 (
    echo.
    echo No browser-specific manifests found. Using generic target name 'browser'...
    set "targetDir=%outputPath%\browser"
    set "targetWwwroot=!targetDir!\wwwroot"
    
    if exist "!targetDir!" rmdir /Q /S "!targetDir!"
    mkdir "!targetWwwroot!"
    xcopy /I /E /Y /Q "%outputPath%\wwwroot" "!targetWwwroot!" >nul
    move "!targetWwwroot!\manifest.json" "!targetDir!\manifest.json" >nul
    
    :: Zip target folder for fallback (conditional check)
    if /i "%disableZip%"=="true" (
        echo Skipping compression ^(zipping disabled^).
    ) else (
        echo Zipping target folder...
        if exist "!targetDir!.zip" del /Q "!targetDir!.zip"
        powershell -Command "Compress-Archive -Path '!targetDir!\*' -DestinationPath '!targetDir!.zip' -Force"
    )
)

echo.
echo Build complete.
pause
