@echo off
chcp 65001 >nul
echo ========================================
echo   Nacos Sync Tool - Windows Native
echo   Quick Build Script
echo ========================================
echo.

echo Checking PowerShell...
where powershell >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] PowerShell not found!
    pause
    exit /b 1
)

echo Starting build...
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Configuration Release -Portable

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Build completed successfully!
echo ========================================
pause
