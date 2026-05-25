#!/usr/bin/env bash

# ===================================================
#  CHESS REALTIME ASSISTANT v2.0 - MOCK STARTUP SCRIPT
# ===================================================

clear

echo -e "\e[1;35m===================================================\e[0m"
echo -e "\e[1;36m  CHESS REALTIME ASSISTANT - MOCK STARTUP SCRIPT\e[0m"
echo -e "\e[1;35m===================================================\e[0m"
echo

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
cd "$SCRIPT_DIR"

PID_FILE=".assistant.pids"
rm -f "$PID_FILE"

# 1. Setup Python Virtual Environment for Linux
echo -e "\e[1;34m[1/4] Checking Python Virtual Environment...\e[0m"
cd vision-module
if [ -d "venv" ] && [ ! -f "venv/bin/activate" ]; then
    echo -e "  \e[1;33mFound Windows-style virtual environment. Recreating for Linux...\e[0m"
    rm -rf venv
fi

if [ ! -d "venv" ]; then
    echo -e "  \e[1;32mCreating Linux Python virtual environment (venv)...\e[0m"
    python3 -m venv venv
    if [ $? -ne 0 ]; then
        echo -e "  \e[1;31mError: Failed to create virtual environment.\e[0m"
        echo -e "  Please ensure you have python3 and python-virtualenv installed on Arch Linux."
        exit 1
    fi
    echo -e "  \e[1;32mInstalling Python requirements...\e[0m"
    ./venv/bin/pip install -r requirements.txt
else
    echo -e "  \e[1;32m✓ Python virtual environment is ready.\e[0m"
fi
cd ..

# 2. Stockfish Detection
echo -e "\e[1;34m[2/4] Detecting Stockfish Engine...\e[0m"
STOCKFISH_PATH=""
if command -v stockfish >/dev/null 2>&1; then
    STOCKFISH_PATH=$(command -v stockfish)
    echo -e "  \e[1;32m✓ Found system Stockfish at: $STOCKFISH_PATH\e[0m"
elif [ -f "brain-backend/Engine/stockfish" ]; then
    STOCKFISH_PATH="Engine/stockfish"
    echo -e "  \e[1;32m✓ Found local Stockfish at: brain-backend/$STOCKFISH_PATH\e[0m"
else
    echo -e "  \e[1;33m⚠ Stockfish was not found on your system!\e[0m"
    echo -e "  Please install it on Arch Linux using: \e[1;36msudo pacman -S stockfish\e[0m"
    echo
    read -p "  Press Enter to continue anyway (using default path /usr/bin/stockfish)..."
    STOCKFISH_PATH="/usr/bin/stockfish"
fi
export Stockfish__EnginePath="$STOCKFISH_PATH"

# 3. Clean old processes
echo -e "\e[1;34m[3/4] Cleaning up any stray assistant processes...\e[0m"
pkill -f "dotnet run.*brain-backend" >/dev/null 2>&1
pkill -f "brain-backend.*dll" >/dev/null 2>&1
pkill -f "stockfish" >/dev/null 2>&1
pkill -f "node.*overlay-ui" >/dev/null 2>&1
pkill -f "vite.*overlay-ui" >/dev/null 2>&1
pkill -f "python.*mock_vision.py" >/dev/null 2>&1
sleep 1

echo -e "\e[1;34m[4/4] Starting MOCK services in background...\e[0m"
echo -e "  Outputs will be prefixed with service names."
echo -e "  Press \e[1;31m[Ctrl+C]\e[0m at any time to shut down all services cleanly."
echo -e "\e[1;35m---------------------------------------------------\e[0m"

# Function to run process and prefix output
run_with_prefix() {
    local prefix="$1"
    local color="$2"
    local reset="\e[0m"
    shift 2
    "$@" 2>&1 | while read -r line; do
        echo -e "${color}${prefix}${reset} ${line}"
    done
}

# A. Start Brain Backend
echo -e "\e[1;32m[Startup] Launching Brain Backend (dotnet run)...\e[0m"
cd brain-backend
run_with_prefix "[Backend]" "\e[1;32m" dotnet run &
BACKEND_PID=$!
echo $BACKEND_PID >> "../$PID_FILE"
cd ..

# Wait for backend
sleep 4

# B. Start Overlay UI
echo -e "\e[1;36m[Startup] Launching MOCK Overlay UI (npm run dev)...\e[0m"
cd overlay-ui
run_with_prefix "[Overlay UI]" "\e[1;36m" npm run dev &
UI_PID=$!
echo $UI_PID >> "../$PID_FILE"
cd ..

sleep 2

# C. Start Mock Vision Module
echo -e "\e[1;35m[Startup] Launching MOCK Vision Module (python mock_vision.py)...\e[0m"
cd vision-module
run_with_prefix "[Mock Vision]" "\e[1;35m" ./venv/bin/python mock_vision.py &
VISION_PID=$!
echo $VISION_PID >> "../$PID_FILE"
cd ..

echo -e "\e[1;32m✓ All MOCK services running successfully!\e[0m"
echo -e "  - \e[1;32mBrain Backend PID:  $BACKEND_PID\e[0m"
echo -e "  - \e[1;36mMock Overlay UI PID: $UI_PID\e[0m"
echo -e "  - \e[1;35mMock Vision PID:     $VISION_PID\e[0m"
echo -e "\e[1;35m===================================================\e[0m"

# Graceful cleanup trap
cleanup() {
    echo -e "\n\e[1;31mShutting down all MOCK services...\e[0m"
    
    # Kill process trees
    pkill -P $BACKEND_PID >/dev/null 2>&1
    kill $BACKEND_PID >/dev/null 2>&1
    
    pkill -P $UI_PID >/dev/null 2>&1
    kill $UI_PID >/dev/null 2>&1
    
    pkill -P $VISION_PID >/dev/null 2>&1
    kill $VISION_PID >/dev/null 2>&1
    
    # Extra cleanup to ensure no processes are orphaned
    pkill -f "dotnet run.*brain-backend" >/dev/null 2>&1
    pkill -f "brain-backend.*dll" >/dev/null 2>&1
    pkill -f "stockfish" >/dev/null 2>&1
    pkill -f "node.*overlay-ui" >/dev/null 2>&1
    pkill -f "vite.*overlay-ui" >/dev/null 2>&1
    pkill -f "python.*mock_vision.py" >/dev/null 2>&1
    
    rm -f "$PID_FILE"
    echo -e "\e[1;32m✓ Shutdown complete. All processes terminated.\e[0m"
    exit 0
}

trap cleanup INT TERM

# Wait for background jobs to finish (keeps script running)
wait
