@echo off
echo ===================================================
echo CHESS REALTIME ASSISTANT v2.0 - MOCK STARTUP SCRIPT
echo ===================================================

cd /d "%~dp0"

echo [1/3] Starting Brain Backend (C# ASP.NET Core)...
start "Chess Brain Backend" cmd /k "cd brain-backend && dotnet run"
timeout /t 5 /nobreak >nul

echo [2/3] Starting Overlay UI (React + Vite)...
start "Chess Overlay UI" cmd /k "cd overlay-ui && npm run dev"
timeout /t 3 /nobreak >nul

echo [3/3] Starting MOCK Vision Module (Python)...
start "Chess MOCK Vision Module" cmd /k "cd vision-module && .\venv\Scripts\activate && python mock_vision.py"

echo ===================================================
echo MOCK services started in separate windows!
echo - UI is available at: http://localhost:5173
echo - Backend is running on: http://localhost:5000
echo - Mock Vision is simulating a game automatically!
echo ===================================================
echo Close this window at any time.
pause
