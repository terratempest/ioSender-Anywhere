@echo off
setlocal
cd /d "%~dp0"

echo.
echo  ioSender - release artifact build
echo  =================================
echo  Produces:
echo    - Windows portable .zip
echo    - Windows installer .exe
echo    - Linux installer .deb
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-all.ps1" %*
set EXITCODE=%ERRORLEVEL%

if %EXITCODE% neq 0 (
    echo.
    echo  BUILD FAILED ^(exit %EXITCODE%^)
    echo.
    echo %* | findstr /i /c:"-NoPause" >nul
    if errorlevel 1 pause
    exit /b %EXITCODE%
)

exit /b 0
