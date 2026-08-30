@echo off
rem Passerelle vers build.ps1, l unique implementation des builds locaux.
rem Voir README.md pour les prerequis et les niveaux disponibles.
setlocal

where pwsh >nul 2>&1
if %ERRORLEVEL% equ 0 (
    pwsh -NoProfile -File "%~dp0build.ps1" %*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
)

exit /b %ERRORLEVEL%
