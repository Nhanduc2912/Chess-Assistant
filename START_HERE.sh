#!/usr/bin/env bash

# ===================================================
#  CHESS REALTIME ASSISTANT v2.0 - LINUX ARCH
# ===================================================

# Clear screen for beautiful startup
clear

echo -e "\e[1;35m===================================================\e[0m"
echo -e "\e[1;36m  CHESS REALTIME ASSISTANT v2.0 (Linux Arch Edition)\e[0m"
echo -e "\e[1;35m===================================================\e[0m"
echo

# Get the script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
cd "$SCRIPT_DIR"

# Clean old processes safely
echo -e "\e[1;34m[1/3] Stopping old processes...\e[0m"
pkill -f "dotnet run.*brain-backend" >/dev/null 2>&1
pkill -f "brain-backend.*dll" >/dev/null 2>&1
pkill -f "stockfish" >/dev/null 2>&1
sleep 1

# Detect Stockfish on Arch Linux
echo -e "\e[1;34m[2/3] Detecting Stockfish Engine...\e[0m"
STOCKFISH_PATH=""

if command -v stockfish >/dev/null 2>&1; then
    STOCKFISH_PATH=$(command -v stockfish)
    echo -e "\e[1;32m  ✓ Found system Stockfish at: $STOCKFISH_PATH\e[0m"
elif [ -f "brain-backend/Engine/stockfish" ]; then
    STOCKFISH_PATH="Engine/stockfish"
    echo -e "\e[1;32m  ✓ Found local Stockfish at: brain-backend/$STOCKFISH_PATH\e[0m"
else
    echo -e "\e[1;33m  ⚠ Stockfish was not found on your system!\e[0m"
    echo -e "  Since you are on Arch Linux, we can install it for you automatically."
    read -p "  Do you want to install Stockfish now? (y/n): " INSTALL_SF
    if [[ "$INSTALL_SF" =~ ^[Yy]$ ]]; then
        if command -v yay >/dev/null 2>&1; then
            echo -e "  Found AUR helper \e[1;32myay\e[0m. Installing stockfish from AUR..."
            yay -S stockfish
        elif command -v paru >/dev/null 2>&1; then
            echo -e "  Found AUR helper \e[1;32mparu\e[0m. Installing stockfish from AUR..."
            paru -S stockfish
        else
            echo -e "  Running: \e[1;36msudo pacman -S stockfish\e[0m..."
            sudo pacman -S --noconfirm stockfish
        fi
        
        if command -v stockfish >/dev/null 2>&1; then
            STOCKFISH_PATH=$(command -v stockfish)
            echo -e "  \e[1;32m✓ Successfully installed Stockfish at: $STOCKFISH_PATH\e[0m"
        else
            echo -e "  \e[1;31m✗ Failed to install Stockfish. Defaulting to fallback path...\e[0m"
            STOCKFISH_PATH="/usr/bin/stockfish"
        fi
    else
        echo -e "  Continuing without installing. We will look in default path..."
        STOCKFISH_PATH="/usr/bin/stockfish"
    fi
fi

# Export environment variable to override appsettings.json Stockfish path
export Stockfish__EnginePath="$STOCKFISH_PATH"

echo -e "\e[1;34m[3/3] Starting Brain Backend (Stockfish on port 5000)...\e[0m"
echo -e "  Running: \e[0;32mdotnet run\e[0m inside brain-backend/"
echo

# Start the dotnet backend in the background
cd brain-backend
dotnet run &
BACKEND_PID=$!
cd ..

# Save PID to stop easily later
echo $BACKEND_PID > .backend.pid

# Wait a few seconds for backend to start
sleep 4

echo -e "\e[1;35m===================================================\e[0m"
echo -e "\e[1;32m  BACKEND STARTED IN BACKGROUND! (PID: $BACKEND_PID)\e[0m"
echo
echo -e "  Backend URL: \e[1;36mhttp://localhost:5000\e[0m"
echo -e "  Status API:  \e[1;36mhttp://localhost:5000/api/analyze/status\e[0m"
echo
echo -e "\e[1;33m  TO USE:\e[0m"
echo -e "  1. Open Chrome/Chromium and go to chess.com"
echo -e "  2. Start a game"
echo -e "  3. Click the Chess piece icon in Chrome toolbar (Extension)"
echo -e "  4. You should see \e[1;32m\"Stockfish Online\"\e[0m in the popup"
echo -e "  5. Make a move — analysis appears automatically!"
echo
echo -e "  \e[1;33mNOTE:\e[0m If popup shows \"Content script not loaded\","
echo -e "        press F5 to refresh the chess.com tab."
echo -e "\e[1;35m===================================================\e[0m"
echo -e "Press \e[1;31m[Ctrl+C]\e[0m to stop the Backend and exit."

# Handle cleanup on exit
cleanup() {
    echo -e "\n\e[1;31mStopping Brain Backend (PID: $BACKEND_PID)...\e[0m"
    kill $BACKEND_PID >/dev/null 2>&1
    pkill -f "stockfish" >/dev/null 2>&1
    rm -f .backend.pid
    echo -e "\e[1;32m✓ Shutdown successful. Goodbye!\e[0m"
    exit 0
}

trap cleanup INT TERM

# Keep script running to show logs/output or just wait
wait $BACKEND_PID
