#!/usr/bin/env bash
# The tests of SonicArranger2026.
#
#   tests/run.sh [folder with Sonic Arranger files]
#
# 1. Builds the tests with cshiftc (CSHIFTC, else cshiftc from PATH) and runs them: a module made in code, and every
#    file of the folder read, saved in the module format (SOAR) and read back (a module file byte for byte).
# 2. With dotnet: builds SonicConvert of this repository and converts each packed file of the folder and its SOAR
#    file saved by SonicArranger2026 to WAV. The two WAV files must be the same. Songs with a noise generator sound
#    different each time in SonicConvert (it seeds it with the time): for them, only the length must be the same.
#    The module with a note changed (by the tests) must convert as well, and give another WAV than the file.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cshiftc="${CSHIFTC:-cshiftc}"
songs="${1:-}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

"$cshiftc" build "$here"
files=()
if [ -n "$songs" ]; then
    for f in "$songs"/*; do
        case "$f" in
            *.md | *.txt | *.wav | *.mp3) ;; # no songs
            *) [ -f "$f" ] && files+=("$f") ;;
        esac
    done
fi
mkdir -p "$work/soar"
"$here/bin/sa2026-tests" --out "$work/soar" "${files[@]}"

if [ ${#files[@]} -eq 0 ]; then
    echo "no folder of Sonic Arranger files given: SonicConvert not checked"
    exit 0
fi
if ! command -v dotnet > /dev/null; then
    echo "no dotnet: SonicConvert not checked"
    exit 0
fi
dotnet build "$repo/SonicConvert/SonicConvert.csproj" -c Release -v quiet -nologo > "$work/build.log" || {
    cat "$work/build.log"
    exit 1
}
convert=$(find "$repo/SonicConvert/bin/Release" -name SonicConvert.dll | head -1)
# SonicConvert is built for .NET 6; a newer runtime runs it as well
export DOTNET_ROLL_FORWARD=Major

same=0
noise=0
edited=0
failed=0
for f in "${files[@]}"; do
    name="$(basename "$f")"
    soar="$work/soar/$name.soar"
    # module files are saved byte for byte (checked above): only the packed ones are converted
    if head -c 4 "$f" | grep -q SOAR; then
        continue
    fi
    dotnet "$convert" "$f" "$work/a.wav" > /dev/null
    if ! dotnet "$convert" "$soar" "$work/b.wav" > /dev/null; then
        echo "FAILED: SonicConvert cannot convert the SOAR file of $name"
        failed=$((failed + 1))
        continue
    fi
    if ! dotnet "$convert" "$work/soar/$name.edited" "$work/e.wav" > /dev/null; then
        echo "FAILED: SonicConvert cannot convert the edited module of $name"
        failed=$((failed + 1))
    elif cmp -s "$work/a.wav" "$work/e.wav"; then
        echo "FAILED: the edited module of $name sounds the same as the file"
        failed=$((failed + 1))
    else
        edited=$((edited + 1))
    fi
    if cmp -s "$work/a.wav" "$work/b.wav"; then
        same=$((same + 1))
        continue
    fi
    dotnet "$convert" "$f" "$work/c.wav" > /dev/null
    if ! cmp -s "$work/a.wav" "$work/c.wav" && [ "$(stat -c %s "$work/a.wav")" = "$(stat -c %s "$work/b.wav")" ]; then
        noise=$((noise + 1))
        continue
    fi
    echo "FAILED: the WAV of the SOAR file of $name is not that of the file"
    failed=$((failed + 1))
done
echo "SonicConvert: $same the same WAV, $noise with noise the same length, $edited edited ones converted and changed, $failed failed"
[ $failed -eq 0 ]
