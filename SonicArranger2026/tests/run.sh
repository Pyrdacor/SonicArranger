#!/usr/bin/env bash
# The tests of SonicArranger2026.
#
#   tests/run.sh [folder with Sonic Arranger files]
#
# 1. Builds the tests with cshiftc (CSHIFTC, else cshiftc from PATH) and runs them: a module made in code, and every
#    file of the folder read, saved in the module format (SOAR) and read back (a module file byte for byte).
# 2. With dotnet: builds SonicConvert of this repository and converts each packed file of the folder and its SOAR
#    file saved by SonicArranger2026 to WAV. The two WAV files must be the same.
#    The module with a note changed (by the tests) must convert as well, and give another WAV than the file.
# 3. The replayer of SonicArranger2026 (sonicarranger2026 --wav) must give the same WAV as SonicConvert 1.10 for each
#    packed file (for the songs with noise: the same length). That SonicConvert is built from the revision
#    REFERENCE_REV of this repository (ee93b93, before its replayer was fixed to follow the Amiga routine).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
cshiftc="${CSHIFTC:-cshiftc}"
songs="${1:-}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

"$cshiftc" build "$here"
"$cshiftc" build "$here/.."
tracker="$here/../bin/sonicarranger2026"
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
# SonicConvert 1.10, the replayer that SonicArranger2026 follows for now
reference_rev="${REFERENCE_REV:-ee93b93}"
mkdir -p "$work/reference"
git -C "$repo" archive "$reference_rev" SonicArranger SonicConvert | tar -x -C "$work/reference"
dotnet build "$work/reference/SonicConvert/SonicConvert.csproj" -c Release -v quiet -nologo > "$work/build.log" || {
    cat "$work/build.log"
    exit 1
}
reference=$(find "$work/reference/SonicConvert/bin/Release" -name SonicConvert.dll | head -1)
# SonicConvert is built for .NET 6; a newer runtime runs it as well
export DOTNET_ROLL_FORWARD=Major

same=0
edited=0
played=0
playednoise=0
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
    # the noise of SonicConvert is the same on each run
    if cmp -s "$work/a.wav" "$work/b.wav"; then
        same=$((same + 1))
    else
        echo "FAILED: the WAV of the SOAR file of $name is not that of the file"
        failed=$((failed + 1))
    fi
    # the replayer of SonicArranger2026, compared with SonicConvert 1.10 (which seeds its noise with the time: a
    # second run tells whether the song has noise)
    dotnet "$reference" "$f" "$work/r.wav" > /dev/null
    dotnet "$reference" "$f" "$work/c.wav" > /dev/null
    noisy=0
    cmp -s "$work/r.wav" "$work/c.wav" || noisy=1
    if ! "$tracker" --wav "$f" "$work/p.wav"; then
        echo "FAILED: SonicArranger2026 cannot play $name"
        failed=$((failed + 1))
    elif cmp -s "$work/r.wav" "$work/p.wav"; then
        played=$((played + 1))
    elif [ $noisy = 1 ] && [ "$(stat -c %s "$work/r.wav")" = "$(stat -c %s "$work/p.wav")" ]; then
        playednoise=$((playednoise + 1))
    else
        echo "FAILED: SonicArranger2026 plays $name not as SonicConvert 1.10 does ($(cmp "$work/r.wav" "$work/p.wav" | head -1))"
        failed=$((failed + 1))
    fi
done
echo "SonicConvert: $same the same WAV, $edited edited ones converted and changed"
echo "SonicArranger2026 replayer: $played the same WAV as SonicConvert 1.10, $playednoise with noise the same length"
echo "$failed failed"
[ $failed -eq 0 ]
