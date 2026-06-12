#!/usr/bin/env bash
# =============================================================================
#  Chess Realtime Assistant — Linux Setup Script
#  Hỗ trợ: Ubuntu / Debian / Linux Mint / Arch Linux / Fedora / openSUSE
# =============================================================================
set -e

# ─── Colors ──────────────────────────────────────────────────────────────────
RED='\e[1;31m'; GRN='\e[1;32m'; YLW='\e[1;33m'
CYN='\e[1;36m'; MAG='\e[1;35m'; RST='\e[0m'

log_info()  { echo -e "${GRN}[INFO]${RST} $*"; }
log_warn()  { echo -e "${YLW}[WARN]${RST} $*"; }
log_error() { echo -e "${RED}[ERROR]${RST} $*"; }
log_step()  { echo -e "\n${MAG}══════════════════════════════════════════${RST}"; \
              echo -e "${CYN} $*${RST}"; \
              echo -e "${MAG}══════════════════════════════════════════${RST}"; }

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
cd "$SCRIPT_DIR"

clear
echo -e "${MAG}"
echo "  ██████╗██╗  ██╗███████╗███████╗███████╗"
echo "  ██╔════╝██║  ██║██╔════╝██╔════╝██╔════╝"
echo "  ██║     ███████║█████╗  ███████╗███████╗"
echo "  ██║     ██╔══██║██╔══╝  ╚════██║╚════██║"
echo "  ╚██████╗██║  ██║███████╗███████║███████║"
echo -e "${RST}"
echo -e "${CYN}  Chess Realtime Assistant — Linux Setup${RST}"
echo -e "${GRN}  Hỗ trợ: Ubuntu · Debian · Mint · Arch · Fedora · openSUSE${RST}"
echo ""

# ─── Step 0: Detect Distro ───────────────────────────────────────────────────
log_step "0/5 — Nhận diện hệ điều hành Linux"

detect_distro() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        DISTRO_ID="${ID,,}"           # lowercase
        DISTRO_LIKE="${ID_LIKE,,}"
    elif command -v lsb_release &>/dev/null; then
        DISTRO_ID="$(lsb_release -si | tr '[:upper:]' '[:lower:]')"
        DISTRO_LIKE=""
    else
        DISTRO_ID="unknown"
        DISTRO_LIKE=""
    fi
}

detect_distro

log_info "Phát hiện distro: ${DISTRO_ID} (like: ${DISTRO_LIKE:-none})"

is_debian_based() {
    [[ "$DISTRO_ID" =~ ^(ubuntu|debian|linuxmint|pop|elementary|kali|zorin|raspbian)$ ]] \
    || [[ "$DISTRO_LIKE" == *"debian"* ]] || [[ "$DISTRO_LIKE" == *"ubuntu"* ]]
}

is_arch_based() {
    [[ "$DISTRO_ID" =~ ^(arch|manjaro|endeavouros|garuda|artix)$ ]] \
    || [[ "$DISTRO_LIKE" == *"arch"* ]]
}

is_fedora_based() {
    [[ "$DISTRO_ID" =~ ^(fedora|rhel|centos|almalinux|rocky)$ ]] \
    || [[ "$DISTRO_LIKE" == *"fedora"* ]] || [[ "$DISTRO_LIKE" == *"rhel"* ]]
}

is_opensuse() {
    [[ "$DISTRO_ID" =~ ^(opensuse|sles)$ ]] || [[ "$DISTRO_LIKE" == *"suse"* ]]
}

# ─── Step 1: Install System Dependencies ─────────────────────────────────────
log_step "1/5 — Cài đặt dependencies hệ thống"

install_pkg() {
    # $1 = package name to check (command), $2 = package name to install
    if command -v "$1" &>/dev/null; then
        log_info "$1 đã được cài đặt, bỏ qua."
        return 0
    fi
    log_info "Đang cài đặt: $2 ..."
    if is_debian_based; then
        sudo apt-get install -y "$2"
    elif is_arch_based; then
        if command -v yay &>/dev/null;  then yay -S --noconfirm "$2"
        elif command -v paru &>/dev/null; then paru -S --noconfirm "$2"
        else sudo pacman -S --noconfirm "$2"; fi
    elif is_fedora_based; then
        sudo dnf install -y "$2"
    elif is_opensuse; then
        sudo zypper install -y "$2"
    else
        log_warn "Distro không nhận diện được. Hãy cài thủ công: $2"
    fi
}

# Update package list first (Debian/Ubuntu only)
if is_debian_based; then
    log_info "Cập nhật danh sách package (apt update)..."
    sudo apt-get update -qq
fi

# Core tools
install_pkg "curl"   "curl"
install_pkg "wget"   "wget"
install_pkg "git"    "git"
install_pkg "unzip"  "unzip"

# ─── Step 2: Install .NET 9 SDK ──────────────────────────────────────────────
log_step "2/5 — Cài đặt .NET 9 SDK"

if command -v dotnet &>/dev/null && dotnet --version 2>/dev/null | grep -q "^9\."; then
    log_info ".NET 9 đã cài đặt: $(dotnet --version)"
else
    log_info ".NET 9 chưa có hoặc version cũ, đang cài đặt..."

    if is_debian_based; then
        # Microsoft script (hỗ trợ Ubuntu 22.04/24.04, Debian 12, Mint 21/22)
        log_info "Cài .NET 9 qua Microsoft APT feed..."
        curl -fsSL https://packages.microsoft.com/config/ubuntu/$(. /etc/os-release && echo "$VERSION_ID" 2>/dev/null || echo "22.04")/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb 2>/dev/null \
            || curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb
        sudo dpkg -i /tmp/packages-microsoft-prod.deb
        sudo apt-get update -qq
        sudo apt-get install -y dotnet-sdk-9.0 || {
            log_warn "Thử cài qua snap..."
            sudo snap install dotnet-sdk --classic --channel=9.0/stable
            sudo snap alias dotnet-sdk.dotnet dotnet
        }

    elif is_arch_based; then
        install_pkg "dotnet" "dotnet-sdk"

    elif is_fedora_based; then
        sudo dnf install -y dotnet-sdk-9.0

    elif is_opensuse; then
        sudo zypper install -y dotnet-sdk-9.0

    else
        log_warn "Không tự động cài được .NET 9. Tải thủ công:"
        log_warn "  https://dotnet.microsoft.com/download/dotnet/9.0"
        log_warn "Sau đó chạy lại script này."
        exit 1
    fi

    # Verify
    if ! command -v dotnet &>/dev/null; then
        log_error ".NET SDK cài thất bại. Kiểm tra internet hoặc cài thủ công."
        exit 1
    fi
    log_info ".NET cài thành công: $(dotnet --version)"
fi

# ─── Step 3: Install Stockfish ───────────────────────────────────────────────
log_step "3/5 — Cài đặt Stockfish Engine"

install_stockfish() {
    if command -v stockfish &>/dev/null; then
        SF_PATH=$(command -v stockfish)
        log_info "Stockfish đã có tại: $SF_PATH"
        export Stockfish__EnginePath="$SF_PATH"
        return 0
    fi

    if [ -f "$SCRIPT_DIR/brain-backend/Engine/stockfish" ]; then
        log_info "Tìm thấy stockfish local tại: brain-backend/Engine/stockfish"
        export Stockfish__EnginePath="$SCRIPT_DIR/brain-backend/Engine/stockfish"
        return 0
    fi

    log_warn "Stockfish chưa được cài đặt."
    echo ""
    echo -e "  Chọn phương thức cài đặt:"
    echo -e "  ${CYN}1)${RST} Cài qua package manager (khuyến nghị)"
    echo -e "  ${CYN}2)${RST} Tải binary từ GitHub (mọi distro)"
    echo -e "  ${CYN}3)${RST} Bỏ qua (cài thủ công sau)"
    echo ""
    read -rp "  Lựa chọn [1/2/3]: " SF_CHOICE

    case "$SF_CHOICE" in
        1)
            if is_debian_based; then
                sudo apt-get install -y stockfish
            elif is_arch_based; then
                if command -v yay &>/dev/null;  then yay -S --noconfirm stockfish
                elif command -v paru &>/dev/null; then paru -S --noconfirm stockfish
                else sudo pacman -S --noconfirm stockfish; fi
            elif is_fedora_based; then
                sudo dnf install -y stockfish
            elif is_opensuse; then
                sudo zypper install -y stockfish
            fi
            ;;
        2)
            log_info "Tải Stockfish binary từ GitHub Releases..."
            mkdir -p "$SCRIPT_DIR/brain-backend/Engine"
            ARCH=$(uname -m)
            if [[ "$ARCH" == "x86_64" ]]; then
                SF_URL="https://github.com/official-stockfish/Stockfish/releases/download/sf_17/stockfish-ubuntu-x86-64-avx2.tar"
            else
                SF_URL="https://github.com/official-stockfish/Stockfish/releases/download/sf_17/stockfish-ubuntu-x86-64.tar"
            fi
            wget -q --show-progress -O /tmp/stockfish.tar "$SF_URL"
            tar xf /tmp/stockfish.tar -C /tmp/
            SF_BIN=$(find /tmp -name "stockfish*" -type f -executable 2>/dev/null | head -1)
            if [ -n "$SF_BIN" ]; then
                cp "$SF_BIN" "$SCRIPT_DIR/brain-backend/Engine/stockfish"
                chmod +x "$SCRIPT_DIR/brain-backend/Engine/stockfish"
                export Stockfish__EnginePath="$SCRIPT_DIR/brain-backend/Engine/stockfish"
                rm -f /tmp/stockfish.tar
                log_info "Stockfish đã tải về tại: brain-backend/Engine/stockfish"
            else
                log_error "Không tìm thấy binary stockfish trong archive."
            fi
            ;;
        3)
            log_warn "Bỏ qua. Hãy cài Stockfish và đặt đường dẫn trong appsettings.json"
            export Stockfish__EnginePath="/usr/bin/stockfish"
            ;;
    esac

    if command -v stockfish &>/dev/null; then
        export Stockfish__EnginePath=$(command -v stockfish)
    fi
}

install_stockfish

# ─── Step 4: Restore .NET packages ───────────────────────────────────────────
log_step "4/5 — Restore .NET packages"

cd "$SCRIPT_DIR/brain-backend"
log_info "Chạy dotnet restore..."
dotnet restore
log_info "dotnet restore hoàn tất."
cd "$SCRIPT_DIR"

# ─── Step 5: Save config & create run script ─────────────────────────────────
log_step "5/5 — Hoàn tất"

# Ghi Stockfish path vào .env cho START_HERE.sh tái sử dụng
cat > "$SCRIPT_DIR/.env" <<EOF
# Auto-generated by setup_linux.sh
Stockfish__EnginePath=${Stockfish__EnginePath:-/usr/bin/stockfish}
EOF

log_info "Đã lưu cấu hình tại: .env"

echo ""
echo -e "${GRN}╔══════════════════════════════════════════════════════╗${RST}"
echo -e "${GRN}║           ✅  Setup hoàn tất thành công!             ║${RST}"
echo -e "${GRN}╚══════════════════════════════════════════════════════╝${RST}"
echo ""
echo -e "  Để khởi chạy assistant, chạy lệnh:"
echo -e "  ${CYN}  bash START_HERE.sh${RST}"
echo ""
echo -e "  Hoặc khởi chạy backend riêng:"
echo -e "  ${CYN}  cd brain-backend && dotnet run${RST}"
echo ""
echo -e "  Sau đó vào Chrome → chrome://extensions/ → Load unpacked"
echo -e "  → chọn thư mục: ${CYN}chrome-extension/${RST}"
echo ""
