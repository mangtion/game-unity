@echo off
setlocal
set UNITY_EXE="C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe"
set PROJECT_PATH=%~dp0
if "%PROJECT_PATH:~-1%"=="\" set PROJECT_PATH=%PROJECT_PATH:~0,-1%
set RESULTS=%~dp0results.xml

%UNITY_EXE% -batchmode -runTests -projectPath "%PROJECT_PATH%" -testResults "%RESULTS%" -testPlatform EditMode -logFile "%~dp0test.log"

echo.
echo === Test results (%RESULTS%) ===
findstr /C:"result=" "%RESULTS%"
echo.
echo NOTE: Unity's batch-mode test runner exits 0 even when tests fail.
echo Check the "result=" lines above for "Failed" to confirm pass/fail -
echo do not rely on the process exit code alone.
endlocal
