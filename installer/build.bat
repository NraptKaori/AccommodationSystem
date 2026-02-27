@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if %ERRORLEVEL% == 0 (
    echo.
    echo [OK] Check installer\Output\ folder.
) else (
    echo.
    echo [ERROR] Build failed. See messages above.
)
pause
