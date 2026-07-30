@echo off
setlocal
set UNITY_EXE="C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Unity.exe"
set PROJECT_PATH=%~dp0
if "%PROJECT_PATH:~-1%"=="\" set PROJECT_PATH=%PROJECT_PATH:~0,-1%
set RESULTS=%~dp0playtest-results.xml

rem Step 1: regenerate Assets/Scenes/Main.unity via the project's own scene
rem generator, so it picks up any Editor-time-only setup (e.g. CameraFit)
rem before Play Mode loads it. This step never enters Play Mode, so it is
rem safe to pass -quit.
%UNITY_EXE% -batchmode -quit -projectPath "%PROJECT_PATH%" -executeMethod YiSunSin.EditorTools.ProjectBootstrap.BuildProject -logFile "%~dp0build.log"

rem Step 2: run the automated Play Mode playtest (Assets/Tests/PlayMode/FullPlaytestTests.cs)
rem via Unity Test Framework's PlayMode runner. This is the reliable route in
rem this environment - an earlier -executeMethod + EditorApplication.isPlaying
rem polling approach reproducibly hung during generic batch-mode Editor
rem startup for reasons unrelated to this project's code (see Task 16's
rem report); -runTests -testPlatform PlayMode drives its own Play Mode
rem entry/update pump instead and does not hit that hang.
%UNITY_EXE% -batchmode -runTests -projectPath "%PROJECT_PATH%" -testResults "%RESULTS%" -testPlatform PlayMode -logFile "%~dp0playtest.log"

echo.
echo === Playtest results (%RESULTS%) ===
findstr /C:"result=" "%RESULTS%"
echo.
echo Screenshots: .superpowers\sdd\2026-07-28-yi-sunsin-unity-port\playtest-screenshots\
echo NOTE: Unity's batch-mode test runner exits 0 even when tests fail.
echo Check the "result=" lines above for "Failed" to confirm pass/fail -
echo do not rely on the process exit code alone.
endlocal
