#!/bin/bash
# ImageViewer 发布打包脚本
# 用法:
#   ./publish.sh                    # 发布当前平台 (macOS 自动打包 .dmg)
#   ./publish.sh osx-arm64          # 指定 macOS ARM64
#   ./publish.sh osx-x64            # 指定 macOS x64
#   ./publish.sh win-x64            # 发布 Windows x64
#
# 体积优化（可选，追加在 RID 后面）:
#   ./publish.sh osx-arm64 --small  # 关闭 Invariant Globalization + trim，体积明显变小
#
# 注意: --small 会做裁剪(trim)，Avalonia 大量使用反射加载 XAML，
#       裁剪后请务必把每个界面都点一遍测试，确认没有控件/绑定丢失。

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

DOTNET="$SCRIPT_DIR/.dotnet/dotnet/dotnet"
PROJECT_DIR="$SCRIPT_DIR/src/Presentation"
ICON="$PROJECT_DIR/Assets/icon.ico"
ICNS_SOURCE="$PROJECT_DIR/Assets/icon.icns"

APP_NAME="ImageViewer"
APP_TITLE="Image"
APP_VERSION="1.0.0"
BUNDLE_ID="com.nightwish.image"
OUTPUT_DIR="$SCRIPT_DIR/publish"

# ---------- 参数解析 ----------
RID_ARG=""
SMALL_MODE=false
for arg in "$@"; do
    case "$arg" in
        --small) SMALL_MODE=true ;;
        *) RID_ARG="$arg" ;;
    esac
done

# ---------- 检测当前平台 ----------
detect_rid() {
    case "$(uname -s)" in
        Darwin) os="osx"   ;;
        Linux)  os="linux" ;;
        MINGW*|MSYS*|CYGWIN*) os="win" ;;
        *) echo "错误: 不支持的系统" && exit 1 ;;
    esac
    case "$(uname -m)" in
        arm64|aarch64) arch="arm64" ;;
        x86_64)        arch="x64" ;;
        *) echo "错误: 不支持的架构" && exit 1 ;;
    esac
    echo "$os-$arch"
}

RID="${RID_ARG:-$(detect_rid)}"
PLATFORM="${RID%-*}"
ARCH="${RID#*-}"

if [ ! -x "$DOTNET" ]; then
    echo "错误: 找不到 dotnet: $DOTNET"
    exit 1
fi

echo "========================================"
echo "  Image $APP_VERSION  发布打包"
echo "  目标: $RID"
if [ "$SMALL_MODE" = true ]; then
    echo "  模式: 体积优化 (--small)"
fi
echo "========================================"
echo ""

# ---------- Step 1: dotnet publish ----------
echo "[1/4] dotnet publish ..."
cd "$PROJECT_DIR"

rm -rf "$OUTPUT_DIR/$RID"

PUBLISH_ARGS=(
    -c Release -r "$RID"
    --self-contained true
    -p:DebugType=none
    -p:DebugSymbols=false
)

if [ "$SMALL_MODE" = true ]; then
    # 关闭国际化数据 (通常省 20MB+)，并做部分裁剪
    PUBLISH_ARGS+=(
        -p:InvariantGlobalization=true
        -p:PublishTrimmed=true
        -p:TrimMode=partial
    )
fi

"$DOTNET" publish "${PUBLISH_ARGS[@]}" -o "$OUTPUT_DIR/$RID"

echo ""

# ---------- macOS: .app + .dmg ----------
if [ "$PLATFORM" = "osx" ]; then
    PUBLISH_DIR="$OUTPUT_DIR/$RID"
    BUNDLE_DIR="$PUBLISH_DIR/$APP_TITLE.app"
    CONTENTS="$BUNDLE_DIR/Contents"
    MACOS_DIR="$CONTENTS/MacOS"
    RES_DIR="$CONTENTS/Resources"

    echo "[2/4] 创建 .app bundle ..."
    rm -rf "$BUNDLE_DIR"
    mkdir -p "$MACOS_DIR"
    mkdir -p "$RES_DIR"

    cp -a "$PUBLISH_DIR"/* "$MACOS_DIR/" 2>/dev/null || true
    rm -rf "$MACOS_DIR/$APP_TITLE.app"
    rm -f "$MACOS_DIR/createdump" 2>/dev/null || true

    chmod +x "$MACOS_DIR/$APP_NAME" 2>/dev/null || true

    # ---------- 生成 macOS 专用图标 (.icns) ----------
    ICONSET_DIR="$OUTPUT_DIR/AppIcon.iconset"
    ICNS_OUT="$RES_DIR/AppIcon.icns"
    ICON_OK=false

    if [ -f "$ICNS_SOURCE" ]; then
        # 已有现成 .icns，直接使用
        cp "$ICNS_SOURCE" "$ICNS_OUT"
        ICON_OK=true
        echo "  图标: 使用已有 $ICNS_SOURCE"
    else
        # 回退：从 PNG/ICO 生成 .icns
        PNG_SOURCE="$PROJECT_DIR/Assets/AppIcon.png"
        rm -rf "$ICONSET_DIR"
        mkdir -p "$ICONSET_DIR"

        if [ -f "$PNG_SOURCE" ]; then
            SRC_PNG="$PNG_SOURCE"
        else
            echo "  未找到 $PNG_SOURCE，尝试从 $ICON 提取最大尺寸帧 ..."
            SRC_PNG="$OUTPUT_DIR/icon_from_ico.png"
            python3 - "$ICON" "$SRC_PNG" << 'PYEOF' 2>/dev/null || true
import sys
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
img = Image.open(src)
frames = []
i = 0
try:
    while True:
        img.seek(i)
        frames.append(img.copy())
        i += 1
except EOFError:
    pass
best = max(frames, key=lambda f: f.size[0]) if frames else img
best.convert("RGBA").save(out)
PYEOF
        fi

        if [ -f "$SRC_PNG" ]; then
            for size in 16 32 128 256 512; do
                double=$((size * 2))
                sips -z $size $size "$SRC_PNG" --out "$ICONSET_DIR/icon_${size}x${size}.png" > /dev/null 2>&1
                sips -z $double $double "$SRC_PNG" --out "$ICONSET_DIR/icon_${size}x${size}@2x.png" > /dev/null 2>&1
            done
            sips -z 1024 1024 "$SRC_PNG" --out "$ICONSET_DIR/icon_512x512@2x.png" > /dev/null 2>&1

            if iconutil -c icns "$ICONSET_DIR" -o "$ICNS_OUT" > /dev/null 2>&1; then
                ICON_OK=true
            fi
        fi
        rm -rf "$ICONSET_DIR"
    fi

    if [ "$ICON_OK" = true ]; then
        ICON_PLIST_NAME="AppIcon"
        echo "  图标生成成功: $ICNS_OUT"
    else
        # 兜底：至少把原 .ico 也复制进去，虽然不会显示，但不影响其他功能
        cp "$ICON" "$RES_DIR/" 2>/dev/null || true
        ICON_PLIST_NAME="icon.ico"
        echo "  警告: 未能生成 .icns（可能缺 python3/Pillow，或没有可用的 png/ico 源），图标会显示为系统默认。"
        echo "        可执行: pip3 install --break-system-packages Pillow  然后重新打包。"
    fi

    echo "[3/4] 写入 Info.plist ..."
    cat > "$CONTENTS/Info.plist" << PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_TITLE</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_TITLE</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>
    <string>$APP_VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$APP_VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>CFBundleIconFile</key>
    <string>$ICON_PLIST_NAME</string>
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key>
            <string>Image</string>
            <key>CFBundleTypeRole</key>
            <string>Viewer</string>
            <key>LSHandlerRank</key>
            <string>Alternate</string>
            <key>LSItemContentTypes</key>
            <array>
                <string>public.image</string>
                <string>public.png</string>
                <string>public.jpeg</string>
                <string>public.tiff</string>
                <string>com.compuserve.gif</string>
                <string>com.microsoft.bmp</string>
                <string>public.heic</string>
                <string>public.webp</string>
            </array>
        </dict>
    </array>
</dict>
</plist>
PLIST

    # ---------- Step 4: 创建带图标布局的 .dmg ----------
    echo "[4/4] 创建 .dmg ..."
    DMG_OUT="$OUTPUT_DIR/$APP_TITLE-$APP_VERSION-$RID.dmg"
    rm -f "$DMG_OUT"

    DMG_SRC="$OUTPUT_DIR/dmg_src"
    rm -rf "$DMG_SRC"
    mkdir -p "$DMG_SRC"
    cp -R "$BUNDLE_DIR" "$DMG_SRC/"
    ln -s /Applications "$DMG_SRC/Applications"

    # 1) 先建一个可读写、留足空间的临时 dmg
    TMP_DMG="$OUTPUT_DIR/tmp.dmg"
    rm -f "$TMP_DMG"
    SRC_SIZE_MB=$(du -sm "$DMG_SRC" | cut -f1)
    DMG_SIZE_MB=$((SRC_SIZE_MB + 50))
    hdiutil create -volname "$APP_TITLE" \
        -srcfolder "$DMG_SRC" \
        -ov -format UDRW \
        -size "${DMG_SIZE_MB}m" \
        "$TMP_DMG" > /dev/null

    # 2) 挂载，用 AppleScript 设置图标视图/大小/位置
    MOUNT_DIR="/Volumes/$APP_TITLE"
    # 清理上一次可能残留的挂载点
    if [ -d "$MOUNT_DIR" ]; then
        hdiutil detach "$MOUNT_DIR" -force > /dev/null 2>&1 || true
        sleep 1
    fi

    hdiutil attach "$TMP_DMG" -mountpoint "$MOUNT_DIR" -nobrowse

    # 关掉 Spotlight 对这个临时卷的索引，避免 mdworker 占用句柄导致 detach 时 "resource busy"
    mdutil -i off "$MOUNT_DIR" > /dev/null 2>&1 || true

    OSASCRIPT_LOG="$OUTPUT_DIR/dmg_layout.log"
    if osascript > "$OSASCRIPT_LOG" 2>&1 <<EOF
tell application "Finder"
    tell disk "$APP_TITLE"
        open
        set current view of container window to icon view
        set toolbar visible of container window to false
        set statusbar visible of container window to false
        -- 打包过程中这个窗口只是用来让 Finder 记住图标布局，
        -- 挪到屏幕外可以避免每次打包都弹出来晃眼睛；
        -- 最终发布出去的 dmg 双击打开时不受影响，会显示在屏幕正中。
        set the bounds of container window to {-2000, 100, -1460, 420}
        set theViewOptions to the icon view options of container window
        set arrangement of theViewOptions to not arranged
        set icon size of theViewOptions to 96
        try
            set position of item "$APP_TITLE.app" of container window to {150, 180}
        end try
        try
            set position of item "Applications" of container window to {430, 180}
        end try
        close
        open
        update without registering applications
        delay 2
        -- 主动关窗口，减少 Finder 还占着这个卷的窗口引用
        close
    end tell
end tell
EOF
    then
        : # 设置成功
    else
        echo ""
        echo "  警告: 设置 DMG 图标布局失败（osascript 控制 Finder 没有成功）。"
        echo "        最常见原因是 macOS 没有授权当前终端应用控制 Finder。"
        echo "        请检查: 系统设置 → 隐私与安全性 → 自动化 → 勾选 [你的终端 App] 对 Finder 的权限"
        echo "        授权后重新运行本脚本即可。详细报错见: $OSASCRIPT_LOG"
        echo ""
    fi

    sync
    # detach 重试几次，实在不行再强制退出，避免 Spotlight/Finder 短暂占用导致整个脚本因 set -e 中断
    DETACHED=false
    for i in 1 2 3 4 5; do
        if hdiutil detach "$MOUNT_DIR" > /dev/null 2>&1; then
            DETACHED=true
            break
        fi
        sleep 2
    done
    if [ "$DETACHED" = false ]; then
        echo "  (普通 detach 未成功，改用 -force 强制弹出)"
        hdiutil detach "$MOUNT_DIR" -force > /dev/null 2>&1 || true
        sleep 1
    fi

    # 3) 转换成最终压缩只读 dmg
    hdiutil convert "$TMP_DMG" -format UDZO -o "$DMG_OUT" > /dev/null
    rm -f "$TMP_DMG"
    rm -rf "$DMG_SRC"

    DMG_SIZE=$(du -sh "$DMG_OUT" | cut -f1)
    echo ""
    echo "========================================"
    echo "  打包完成 ✓"
    echo "  .app : $BUNDLE_DIR"
    echo "  .dmg : $DMG_OUT  ($DMG_SIZE)"
    echo "========================================"

# ---------- Windows ----------
elif [ "$PLATFORM" = "win" ]; then
    echo ""
    echo "========================================"
    echo "  发布完成 ✓"
    echo "  目录: $OUTPUT_DIR/$RID/"
    echo "  入口: $OUTPUT_DIR/$RID/$APP_NAME.exe"
    echo "========================================"

# ---------- Linux ----------
else
    echo ""
    echo "========================================"
    echo "  发布完成 ✓"
    echo "  目录: $OUTPUT_DIR/$RID/"
    echo "  入口: $OUTPUT_DIR/$RID/$APP_NAME"
    echo "========================================"
fi
