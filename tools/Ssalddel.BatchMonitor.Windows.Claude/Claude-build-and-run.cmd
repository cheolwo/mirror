@echo off
rem Claude copy: rule checks -> build -> run. Output goes to claude-build-log.txt
setlocal
cd /d "%~dp0..\.."
set LOG=%~dp0claude-build-log.txt
echo ==== checks ==== > "%LOG%"
echo [1/3] Running rule checks...
dotnet run --project tools\Ssalddel.BatchMonitor.Windows.Claude.Checks >> "%LOG%" 2>&1
set CHECKS=%ERRORLEVEL%
echo ==== build ==== >> "%LOG%"
echo [2/3] Building WPF app...
dotnet build tools\Ssalddel.BatchMonitor.Windows.Claude\Ssalddel.BatchMonitor.Windows.Claude.csproj -c Debug >> "%LOG%" 2>&1
set BUILD=%ERRORLEVEL%
echo checks=%CHECKS% build=%BUILD% >> "%LOG%"
if not "%BUILD%"=="0" (
  echo Build failed. Log: %LOG%
  pause
  exit /b 1
)
echo [3/3] Starting app...
start "" "%~dp0bin\Debug\net10.0-windows\Ssalddel.BatchMonitor.Windows.exe"
echo Done. checks=%CHECKS% build=%BUILD%
timeout /t 5 >nul
