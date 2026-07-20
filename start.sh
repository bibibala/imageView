#!/bin/bash
# ImageViewer 启动脚本（编译 + 运行）
# 用法: ./start.sh [release]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

DOTNET="$SCRIPT_DIR/.dotnet/dotnet/dotnet"
PROJECT_DIR="$SCRIPT_DIR/src/Presentation"
CONFIG="Debug"

if [ "${1:-}" = "release" ] || [ "${1:-}" = "--release" ] || [ "${1:-}" = "-r" ]; then
    CONFIG="Release"
fi

if [ ! -x "$DOTNET" ]; then
    echo "错误: 找不到 dotnet: $DOTNET"
    exit 1
fi

if [ ! -d "$PROJECT_DIR" ]; then
    echo "错误: 找不到项目目录: $PROJECT_DIR"
    exit 1
fi

cd "$PROJECT_DIR"

echo "编译 ImageViewer ($CONFIG)..."
"$DOTNET" build -c "$CONFIG"

echo "启动..."
exec "$DOTNET" run -c "$CONFIG" --no-build
