@echo off
setlocal EnableExtensions

cd /d "%~dp0"
if errorlevel 1 exit /b 1
echo BeRDP clean installer build is starting...
echo.

call "installer\Build-Installers.cmd" -Configuration Release -Runtime win-x64 -RepoRoot "%CD%" %* > "installer-build.log" 2>&1
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
type "installer-build.log"

echo.
if not "%BUILD_EXIT_CODE%"=="0" (
    echo [FAILED] Build or delivery failed. See installer-build.log for the full error.
) else (
    echo [SUCCESS] Fresh installers are available in:
    echo %CD%\dist\installers
)
echo.
pause
exit /b %BUILD_EXIT_CODE%
