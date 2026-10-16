@echo off
rem Builds scripts\QuickGPS.net.dll from src\QuickGPS.cs.
rem Usage: build.cmd ["C:\path\to\Grand Theft Auto IV\GTAIV"]
setlocal
set "GAME=%~1"
if "%GAME%"=="" set "GAME=C:\Steam\steamapps\common\Grand Theft Auto IV\GTAIV"
set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
rem The old compiler can't handle long paths, so build in a short temp folder.
set "B=%TEMP%\qgps-build"

if not exist "%GAME%\ScriptHookDotNet.asi" (
  echo ScriptHookDotNet.asi not found in "%GAME%"
  exit /b 1
)
if not exist "%B%" mkdir "%B%"
copy /y "%GAME%\ScriptHookDotNet.asi" "%B%\ScriptHookDotNet.dll" >nul
copy /y "%~dp0src\QuickGPS.cs" "%B%\QuickGPS.cs" >nul

pushd "%B%"
"%CSC%" -nologo -platform:x86 -optimize+ -t:library -out:QuickGPS.net.dll -r:ScriptHookDotNet.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll QuickGPS.cs
set "ERR=%ERRORLEVEL%"
popd
if not "%ERR%"=="0" exit /b %ERR%

copy /y "%B%\QuickGPS.net.dll" "%~dp0scripts\QuickGPS.net.dll" >nul
echo Built scripts\QuickGPS.net.dll
