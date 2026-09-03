@echo off
rem Gateway to build.ps1, the single implementation of local builds.
rem See README.md for the prerequisites and the available levels.
setlocal

where pwsh >nul 2>&1
if %ERRORLEVEL% equ 0 (
    pwsh -NoProfile -File "%~dp0build.ps1" %*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
)

exit /b %ERRORLEVEL%
