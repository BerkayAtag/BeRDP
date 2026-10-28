@echo off
setlocal
powershell.exe -NoProfile -File "%~dp0Build-Installers.ps1" %*
exit /b %ERRORLEVEL%
