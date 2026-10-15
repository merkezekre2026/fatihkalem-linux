#!/usr/bin/env bash
# Builds FatihKalem-x86_64.AppImage from a linux-x64 self-contained publish folder.
#
# usage: build-appimage.sh <publish-dir> <library-root> <output.AppImage>
#   publish-dir   output of: dotnet publish src/FatihKalem.App -c Release -r linux-x64 --self-contained
#   library-root  folder that holds "Görseller" and "Arka Plan Sayfaları"
#
# Runs on Linux (or WSL). Downloads appimagetool once into ~/.cache and refuses to use it unless its
# SHA-256 matches the digest published for that release.
set -euo pipefail

TOOL_VERSION="1.9.1"
# appimagetool-x86_64.AppImage, as published in the GitHub release's asset digest
TOOL_SHA256="ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0"
TOOL_URL="https://github.com/AppImage/appimagetool/releases/download/${TOOL_VERSION}/appimagetool-x86_64.AppImage"

publish="${1:?usage: build-appimage.sh <publish-dir> <library-root> <output.AppImage>}"
library="${2:?missing library root}"
output="${3:?missing output path}"

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
icon="$here/../src/FatihKalem.App/Assets/fatih-kalem.png"

[[ -x "$publish/FatihKalem" || -f "$publish/FatihKalem" ]] || { echo "no FatihKalem binary in $publish" >&2; exit 1; }
[[ -d "$library/Görseller" && -d "$library/Arka Plan Sayfaları" ]] || { echo "library folders not found in $library" >&2; exit 1; }
[[ -f "$icon" ]] || { echo "icon not found: $icon" >&2; exit 1; }

cache="${XDG_CACHE_HOME:-$HOME/.cache}/fatih-kalem-build"
mkdir -p "$cache"
tool="$cache/appimagetool-${TOOL_VERSION}-x86_64.AppImage"

if [[ ! -f "$tool" ]] || ! echo "$TOOL_SHA256  $tool" | sha256sum -c --status -; then
  echo "downloading appimagetool ${TOOL_VERSION}"
  curl -fL --retry 3 -o "$tool.part" "$TOOL_URL"
  if ! echo "$TOOL_SHA256  $tool.part" | sha256sum -c --status -; then
    rm -f "$tool.part"
    echo "appimagetool checksum mismatch: refusing to use it" >&2
    exit 1
  fi
  mv "$tool.part" "$tool"
fi
chmod +x "$tool"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
appdir="$work/FatihKalem.AppDir"

mkdir -p "$appdir/usr/lib/FatihKalem" "$appdir/usr/bin" "$appdir/usr/share/FatihKalem/library"
cp -a "$publish/." "$appdir/usr/lib/FatihKalem/"
chmod +x "$appdir/usr/lib/FatihKalem/FatihKalem"
ln -s ../lib/FatihKalem/FatihKalem "$appdir/usr/bin/FatihKalem"

cp -a "$library/Görseller" "$library/Arka Plan Sayfaları" "$appdir/usr/share/FatihKalem/library/"
find "$appdir/usr/share/FatihKalem/library" -name desktop.ini -delete

install -m 0755 "$here/AppRun" "$appdir/AppRun"
install -m 0644 "$here/fatih-kalem.desktop" "$appdir/fatih-kalem.desktop"
install -m 0644 "$icon" "$appdir/fatih-kalem.png"
ln -s fatih-kalem.png "$appdir/.DirIcon"

mkdir -p "$(dirname "$output")"
rm -f "$output"
# --appimage-extract-and-run: appimagetool is itself an AppImage and this host may have no FUSE
ARCH=x86_64 "$tool" --appimage-extract-and-run "$appdir" "$output"
chmod +x "$output"
ls -l "$output"
echo "$output"
