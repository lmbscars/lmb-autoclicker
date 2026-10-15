@echo off
setlocal enabledelayedexpansion
title .gg/lmb Auto Clicker - Push Update

cd /d "%~dp0"
if not exist "AutoClicker.cs" (
    if exist "C:\Users\isabe\.gemini\antigravity\scratch\AutoClicker\AutoClicker.cs" (
        cd /d "C:\Users\isabe\.gemini\antigravity\scratch\AutoClicker"
    )
)

echo ===================================================
echo     .gg/lmb Auto Clicker - Push Update Utility
echo ===================================================
echo Current Directory: %CD%
echo.

set /p NEW_VER="Enter New Version (e.g. 1.0.1): "
if "%NEW_VER%"=="" (
    echo Error: Version cannot be empty.
    pause
    exit /b
)

set /p CHANGELOG="Enter Changelog / What's New: "
if "%CHANGELOG%"=="" set CHANGELOG=Bug fixes and performance improvements.

echo.
echo [1/4] Compiling updated executable...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /out:"AutoClicker.exe" /win32icon:"app.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll "AutoClicker.cs"
if errorlevel 1 (
    echo [ERROR] Compilation failed!
    pause
    exit /b
)

echo [2/4] Updating version.json...
powershell -Command "$json = @{ version = '%NEW_VER%'; download_url = 'https://raw.githubusercontent.com/lmbscars/lmb-autoclicker/main/.gg%E2%88%95lmb%20auto%20clicker.exe'; changelog = '%CHANGELOG%' } | ConvertTo-Json; [System.IO.File]::WriteAllText('version.json', $json)"

echo [3/4] Deploying to Downloads and Desktop...
powershell -Command "$slash = [char]0x2215; Copy-Item 'AutoClicker.exe' -Destination \"$env:USERPROFILE\Downloads\.gg$($slash)lmb auto clicker.exe\" -Force; Copy-Item 'AutoClicker.exe' -Destination \"$env:USERPROFILE\Downloads\.gg-lmb auto clicker.exe\" -Force; Copy-Item 'AutoClicker.exe' -Destination 'C:\Users\isabe\.gemini\antigravity\scratch\AutoClicker\.gg$($slash)lmb auto clicker.exe' -Force; if (Test-Path \"$env:USERPROFILE\OneDrive\Desktop\") { Copy-Item 'AutoClicker.exe' -Destination \"$env:USERPROFILE\OneDrive\Desktop\.gg$($slash)lmb auto clicker.exe\" -Force }; if (Test-Path \"$env:USERPROFILE\Desktop\") { Copy-Item 'AutoClicker.exe' -Destination \"$env:USERPROFILE\Desktop\.gg$($slash)lmb auto clicker.exe\" -Force }"

echo [4/4] Committing and pushing to GitHub (if remote is set)...
git add -A
git commit -m "Release v%NEW_VER%: %CHANGELOG%"
git push origin main 2>nul
if errorlevel 1 (
    echo [NOTE] Git remote not yet configured or pushed. Once you connect your GitHub repo, git push will automatically push updates online!
) else (
    echo [SUCCESS] Update v%NEW_VER% pushed online to everyone!
)

echo.
echo ===================================================
echo   Update v%NEW_VER% Prepared Successfully!
echo ===================================================
pause
