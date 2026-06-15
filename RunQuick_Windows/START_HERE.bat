@echo off
echo ===================================================
echo  CHESS REALTIME ASSISTANT v2.0
echo ===================================================

cd /d "%~dp0"

echo [1/2] Stopping old processes...
taskkill /F /IM dotnet.exe /T 2>nul
taskkill /F /IM stockfish.exe /T 2>nul
taskkill /F /IM node.exe /T 2>nul
timeout /t 2 /nobreak >nul

echo [2/2] Starting Brain Backend (Stockfish on port 5000)...
start "Chess Brain Backend" cmd /k "cd brain-backend && dotnet run --no-build"
timeout /t 5 /nobreak >nul

echo.
echo ===================================================
echo  BACKEND STARTED!
echo.
echo  Backend: http://localhost:5000
echo  Status:  http://localhost:5000/api/analyze/status
echo.
echo  TO USE:
echo  1. Open Chrome and go to chess.com
echo  2. Start a game
echo  3. Click the chess piece icon in Chrome toolbar
echo  4. You should see "Stockfish Online" in the popup
echo  5. Make a move — analysis appears automatically!
echo.
echo  NOTE: If popup shows "Content script not loaded",
echo        press F5 to refresh the chess.com tab.
echo ===================================================
pause
