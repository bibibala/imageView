#!/bin/bash
# ImageViewer 编译脚本
# 用法: ./build.sh [release|--clean]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

DOTNET="$ROOT_DIR/.dotnet/dotnet/dotnet"
PROJECT_DIR="$ROOT_DIR/src"
CONFIG="Debug"
CLEAN=false

for arg in "$@"; do
    case "$arg" in
        release|--release|-r) CONFIG="Release" ;;
        --clean|-c) CLEAN=true ;;
    esac
done

if [ ! -x "$DOTNET" ]; then
    echo "错误: 找不到 dotnet: $DOTNET"
    exit 1
fi

if [ ! -d "$PROJECT_DIR" ]; then
    echo "错误: 找不到项目目录: $PROJECT_DIR"
    exit 1
fi

cd "$PROJECT_DIR"

if $CLEAN; then
    echo "清理编译输出..."
    "$DOTNET" clean -c "$CONFIG" > /dev/null
fi

echo "编译 ImageViewer ($CONFIG)..."
"$DOTNET" build -c "$CONFIG"

echo ""
echo "编译完成 ✓"
echo "输出: $PROJECT_DIR/bin/$CONFIG/net10.0/"
