@echo off
setlocal EnableExtensions
rem ===================================================================
rem  LAUNCH:ER Day 8 - One-click Unity build (Windows / Win64) + zip
rem  Usage   : build_unity.bat 0.1.1
rem            or double-click and type the version when asked.
rem  Output  : %OUT_ROOT%\Kairos_v<version>_<yyyyMMdd>\Kairos.exe ...
rem            %OUT_ROOT%\Kairos_v<version>_<yyyyMMdd>.log
rem            %OUT_ROOT%\Kairos_v<version>_<yyyyMMdd>.zip
rem  CLOSE the Unity Editor for this project first, or the build fails.
rem  Scenes  : the scene list in Build Profiles (Build Settings).
rem  Exit    : 0 = success, 1 = failure.
rem  CI      : when CI is defined (GitHub Actions sets CI=true), no pause.
rem  Comments are in English on purpose: Korean text in .bat files
rem  can break depending on the file encoding / console code page.
rem ===================================================================

rem ---- 1) Paths: keep the value if already defined, else use default --
rem      Replace 6000.3.24f1 with your editor version folder in Unity Hub.
if not defined UNITY    set "UNITY=C:\Unity\6000.3.24f1\Editor\Unity.exe"
rem      PROJECT defaults to the parent folder of this .bat (repo root).
if not defined PROJECT  for %%I in ("%~dp0..") do set "PROJECT=%%~fI"
if not defined OUT_ROOT set "OUT_ROOT=D:\Launcher\Builds"
rem -------------------------------------------------------------------

set "GAME=Kairos"
set "RC=1"
if "%PROJECT:~-1%"=="\" set "PROJECT=%PROJECT:~0,-1%"
if "%OUT_ROOT:~-1%"=="\" set "OUT_ROOT=%OUT_ROOT:~0,-1%"

rem ---- 2) Version: first argument, or ask --------------------------
set "VERSION=%~1"
if not defined VERSION set /p "VERSION=Build version (e.g. 0.1.1): "
if not defined VERSION goto :err_version

rem ---- 3) Date (locale-independent, so %date% is not used) ---------
set "TODAY="
for /f "usebackq delims=" %%D in (`powershell -NoProfile -Command "Get-Date -Format yyyyMMdd"`) do set "TODAY=%%D"
if not defined TODAY goto :err_date

set "NAME=%GAME%_v%VERSION%_%TODAY%"
set "OUT=%OUT_ROOT%\%NAME%"
set "LOG=%OUT_ROOT%\%NAME%.log"
set "ZIP=%OUT_ROOT%\%NAME%.zip"

rem ---- 4) Checks and output folder ---------------------------------
if not exist "%UNITY%" goto :err_engine
if not exist "%PROJECT%\Assets" goto :err_project
if exist "%OUT%" goto :err_exists
if exist "%ZIP%" goto :err_exists
if not exist "%OUT_ROOT%" mkdir "%OUT_ROOT%"
mkdir "%OUT%"
if not exist "%OUT%" goto :err_out

rem ---- 5) Unity build -----------------------------------------------
echo [1/2] Building %GAME% v%VERSION% [Win64]
echo       Project: %PROJECT%
echo       Output : %OUT%
echo       Log    : %LOG%

"%UNITY%" -batchmode -quit -projectPath "%PROJECT%" ^
  -buildTarget win64 ^
  -buildWindows64Player "%OUT%\%GAME%.exe" ^
  -logFile "%LOG%"
set "BUILD_RC=%ERRORLEVEL%"
if not "%BUILD_RC%"=="0" goto :err_build
if not exist "%OUT%\%GAME%.exe" goto :err_build

rem ---- 6) Zip (Windows tar, not the Git tar on PATH) ---------------
echo [2/2] Zipping: %ZIP%
"%SystemRoot%\System32\tar.exe" -a -c -f "%ZIP%" -C "%OUT_ROOT%" "%NAME%"
if errorlevel 1 goto :err_zip
if not exist "%ZIP%" goto :err_zip

echo.
echo [DONE] %GAME% v%VERSION%
echo        Folder : %OUT%
echo        Zip    : %ZIP%
echo        Log    : %LOG%
set "RC=0"
goto :finish

:err_version
echo [ERROR] No version given. Usage: build_unity.bat 0.1.1
goto :finish

:err_date
echo [ERROR] Could not get today's date from PowerShell.
goto :finish

:err_engine
echo [ERROR] Unity.exe not found. Check UNITY: %UNITY%
goto :finish

:err_project
echo [ERROR] Not a Unity project folder. Check PROJECT: %PROJECT%
goto :finish

:err_exists
echo [ERROR] Output already exists. Nothing was deleted.
echo         Folder : %OUT%
echo         Zip    : %ZIP%
echo         Remove them yourself, or use another version number.
goto :finish

:err_out
echo [ERROR] Could not create the output folder: %OUT%
goto :finish

:err_build
echo [FAILED] Unity build failed (exit code %BUILD_RC%).
if not exist "%OUT%\%GAME%.exe" echo          %GAME%.exe was not created.
echo          Log: %LOG%
echo          Is the Unity Editor still open with this project?
if not exist "%LOG%" goto :finish
echo          First line with "error" in the log:
for /f "delims=" %%L in ('findstr /i /n /c:"error" "%LOG%"') do (
  echo          %%L
  goto :finish
)
echo          (no line with "error" found)
goto :finish

:err_zip
echo [ERROR] Could not create the zip: %ZIP%
goto :finish

:finish
if not defined CI pause
exit /b %RC%
