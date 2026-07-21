#!/usr/bin/env bash
#
# Builds a self-contained PR Thingy.app bundle so the app can be launched from
# Finder/Dock/Spotlight instead of `dotnet run` in a terminal.
#
# Usage:
#   scripts/package-macos.sh [--arch arm64|x64] [--output-dir <dir>]
#
# Output:
#   <output-dir>/PR Thingy.app   (default output-dir: dist/macos)
#
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_PROJECT="$REPO_ROOT/src/PrThingy.App/PrThingy.App.csproj"
ICON_SOURCE="$REPO_ROOT/src/PrThingy.App/Assets/avalonia-logo.ico"

APP_DISPLAY_NAME="PR Thingy"
EXECUTABLE_NAME="PrThingy.App"
BUNDLE_ID="com.prthingy.app"
BUNDLE_VERSION="1.0.0"

ARCH="$(uname -m)"
OUTPUT_DIR="$REPO_ROOT/dist/macos"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --arch)
            ARCH="$2"
            shift 2
            ;;
        --output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        *)
            echo "Unknown argument: $1" >&2
            exit 1
            ;;
    esac
done

case "$ARCH" in
    arm64) RID="osx-arm64" ;;
    x64|x86_64) RID="osx-x64" ;;
    *)
        echo "Unsupported --arch '$ARCH' (expected arm64 or x64)" >&2
        exit 1
        ;;
esac

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

PUBLISH_DIR="$WORK_DIR/publish"
ICONSET_DIR="$WORK_DIR/AppIcon.iconset"
APP_BUNDLE="$OUTPUT_DIR/$APP_DISPLAY_NAME.app"
CONTENTS_DIR="$APP_BUNDLE/Contents"

echo "==> Publishing self-contained build for $RID"
dotnet publish "$APP_PROJECT" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -o "$PUBLISH_DIR"

echo "==> Assembling app bundle at $APP_BUNDLE"
rm -rf "$APP_BUNDLE"
mkdir -p "$CONTENTS_DIR/MacOS" "$CONTENTS_DIR/Resources"
cp -R "$PUBLISH_DIR/." "$CONTENTS_DIR/MacOS/"
chmod +x "$CONTENTS_DIR/MacOS/$EXECUTABLE_NAME"

echo "==> Generating AppIcon.icns from $(basename "$ICON_SOURCE")"
mkdir -p "$ICONSET_DIR"
BASE_PNG="$WORK_DIR/icon-source.png"
sips -s format png "$ICON_SOURCE" --out "$BASE_PNG" >/dev/null

# name:size pairs required by iconutil; the source ico only has a 256x256
# master image, so larger sizes are upscaled (fine for a placeholder icon —
# swap in a higher-resolution source image and re-run if you want a crisper one).
for entry in \
    "icon_16x16.png:16" \
    "icon_16x16@2x.png:32" \
    "icon_32x32.png:32" \
    "icon_32x32@2x.png:64" \
    "icon_128x128.png:128" \
    "icon_128x128@2x.png:256" \
    "icon_256x256.png:256" \
    "icon_256x256@2x.png:512" \
    "icon_512x512.png:512" \
    "icon_512x512@2x.png:1024"
do
    name="${entry%%:*}"
    size="${entry##*:}"
    sips -z "$size" "$size" "$BASE_PNG" --out "$ICONSET_DIR/$name" >/dev/null
done
iconutil -c icns "$ICONSET_DIR" -o "$CONTENTS_DIR/Resources/AppIcon.icns"

echo "==> Writing Info.plist"
cat > "$CONTENTS_DIR/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>$EXECUTABLE_NAME</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon.icns</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleName</key>
    <string>$APP_DISPLAY_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_DISPLAY_NAME</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$BUNDLE_VERSION</string>
    <key>CFBundleVersion</key>
    <string>$BUNDLE_VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

echo "==> Ad-hoc code-signing the bundle"
codesign --force --deep --sign - "$APP_BUNDLE"

echo "==> Done: $APP_BUNDLE"
echo "    Move it to /Applications, or run: open \"$APP_BUNDLE\""
echo "    First launch: right-click -> Open (unsigned app, Gatekeeper will warn once)."
