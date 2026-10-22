#!/usr/bin/env bash
# Copies the Sonic Arranger songs of Ambermoon (the packed files of the folder Music of
# https://github.com/Pyrdacor/Ambermoon) into player/music, where the player finds them.
#
#   player/fetch-music.sh [folder Music of an Ambermoon checkout]
#
# Without a folder, the folder Music is cloned (sparse, without the MP3s).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
source="${1:-}"
if [ -z "$source" ]; then
    work="$(mktemp -d)"
    trap 'rm -rf "$work"' EXIT
    git clone --depth 1 --filter=blob:none --sparse https://github.com/Pyrdacor/Ambermoon "$work/ambermoon"
    git -C "$work/ambermoon" sparse-checkout set --no-cone '/Music/SONIC.*'
    source="$work/ambermoon/Music"
fi
mkdir -p "$here/music"
count=0
for f in "$source"/SONIC.*; do
    case "$f" in
        *.sa | *.mp3) ;; # the modules and the MP3s are not needed
        *) [ -f "$f" ] && cp "$f" "$here/music/" && count=$((count + 1)) ;;
    esac
done
echo "$count songs in $here/music"
