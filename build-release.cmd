@echo off
setlocal
cd /d "%~dp0"
where pwsh >nul 2>&1 && (set "PS=pwsh") || (set "PS=powershell.exe")
"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-all.ps1" %*
exit /b %ERRORLEVEL%
