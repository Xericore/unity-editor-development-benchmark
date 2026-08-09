@echo off
setlocal enabledelayedexpansion

set "VERSION_FILE=%~dp0ProjectSettings\ProjectVersion.txt"

if not exist "%VERSION_FILE%" (
    echo ERROR: Could not find "%VERSION_FILE%"
    exit /b 1
)

set "UNITY_VERSION="
for /f "tokens=2" %%A in ('findstr /b "m_EditorVersion:" "%VERSION_FILE%"') do (
    if not defined UNITY_VERSION set "UNITY_VERSION=%%A"
)

if not defined UNITY_VERSION (
    echo ERROR: Could not parse m_EditorVersion from "%VERSION_FILE%"
    exit /b 1
)

rem Editor install roots to check, in order. Set UNITY_HUB_EDITOR_DIR to check a
rem custom location first (e.g. a non-default Hub install drive/path).
set "EDITOR_ROOTS="
if defined UNITY_HUB_EDITOR_DIR set "EDITOR_ROOTS=%UNITY_HUB_EDITOR_DIR%;"
set "EDITOR_ROOTS=%EDITOR_ROOTS%C:\Program Files\Unity\Hub\Editor;%USERPROFILE%\Unity\Hub\Editor"

set "UNITY_EXE="
for %%R in ("%EDITOR_ROOTS:;=" "%") do (
    if not defined UNITY_EXE (
        set "CANDIDATE=%%~R\%UNITY_VERSION%\Editor\Unity.exe"
        if exist "!CANDIDATE!" set "UNITY_EXE=!CANDIDATE!"
    )
)

if not defined UNITY_EXE (
    echo ERROR: Unity %UNITY_VERSION% is not installed. Checked:
    for %%R in ("%EDITOR_ROOTS:;=" "%") do (
        echo   %%~R\%UNITY_VERSION%\Editor\Unity.exe
    )
    exit /b 1
)

"%UNITY_EXE%" -projectPath "%~dp0." -logFile - -executeMethod UnityEditorDevelopmentBenchmark.Editor.Benchmarking.BenchmarkRunner.StartBenchmarkHeadless
