#!/usr/bin/env bash

# ===================================================
#  CHESS REALTIME ASSISTANT v2.0 - SHUTDOWN SCRIPT
# ===================================================

clear

echo -e "\e[1;35m===================================================\e[0m"
echo -e "\e[1;31m  CHESS REALTIME ASSISTANT - SHUTDOWN SCRIPT\e[0m"
echo -e "\e[1;35m===================================================\e[0m"
echo

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
cd "$SCRIPT_DIR"

PID_FILE=".assistant.pids"

# 1. Stopping via PID file if it exists
if [ -f "$PID_FILE" ]; then
    echo -e "\e[1;34mStopping active services from recorded PIDs...\e[0m"
    while read -r pid; do
        if kill -0 "$pid" >/dev/null 2>&1; then
            echo -e "  Killing PID: $pid..."
            pkill -P "$pid" >/dev/null 2>&1
            kill "$pid" >/dev/null 2>&1
        fi
    done < "$PID_FILE"
    rm -f "$PID_FILE"
fi

# 2. Safely cleaning up any remaining stray processes
echo -e "\e[1;34mCleaning up any leftover processes...\e[0m"

# Stop Brain Backend
echo -e "  Stopping Brain Backend (dotnet)..."
pkill -f "dotnet run.*brain-backend" >/dev/null 2>&1
pkill -f "brain-backend.*dll" >/dev/null 2>&1

# Stop Stockfish
echo -e "  Stopping Stockfish engine..."
pkill -f "stockfish" >/dev/null 2>&1

# Stop Overlay UI & Electron
echo -e "  Stopping Overlay UI (node & electron)..."
pkill -f "node.*overlay-ui" >/dev/null 2>&1
pkill -f "vite.*overlay-ui" >/dev/null 2>&1
pkill -f "electron.*overlay-ui" >/dev/null 2>&1

# Stop Vision Module
echo -e "  Stopping Vision Module (python)..."
pkill -f "python.*main.py" >/dev/null 2>&1
pkill -f "python.*mock_vision.py" >/dev/null 2>&1

# Stop backend standalone if any
rm -f .backend.pid

echo -e "\e[1;35m===================================================\e[0m"
echo -e "\e[1;32m  All services stopped successfully!\e[0m"
echo -e "\e[1;35m===================================================\e[0m"
