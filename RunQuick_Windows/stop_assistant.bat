@echo off
echo ===================================================
echo CHESS REALTIME ASSISTANT v2.0 - SHUTDOWN SCRIPT
echo ===================================================

echo Stopping Brain Backend (dotnet.exe)...
taskkill /F /IM dotnet.exe /T 2>nul
taskkill /F /IM stockfish.exe /T 2>nul

echo Stopping Overlay UI (node.exe)...
taskkill /F /IM node.exe /T 2>nul

echo Stopping Vision Module (python.exe)...
taskkill /F /IM python.exe /T 2>nul

echo ===================================================
echo All services stopped successfully!
echo ===================================================
pause
