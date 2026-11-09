@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build Android.ps1"
set "build_exit=%ERRORLEVEL%"
if not "%build_exit%"=="0" pause
exit /b %build_exit%
