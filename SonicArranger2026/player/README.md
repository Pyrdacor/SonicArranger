# The Ambermoon player

Plays the Sonic Arranger songs of [Ambermoon](https://github.com/Pyrdacor/Ambermoon) (the folder `Music`, the music
of Matthias Steinwachs) with the replayer of SonicArranger2026 (`../lib`), so they sound as in SonicConvert 1.11. It is
written in [CShift](https://github.com/Robert-Schneckenhaus/CShift) with `System.Ui` and `System.Audio`, and runs on
Windows, Linux and in the browser: on GitHub Pages at <https://pyrdacor.github.io/SonicArranger/player/>.

| | |
| --- | --- |
| The list | the 32 songs of the game with the numbers and names of the README of Ambermoon, then the intro, the main menu (the second song of the intro's file) and the extro |
| Each song | Play (Pause while it plays), Stop, the number and name, a timeline with a marker at the time being heard, the time and the length |
| Timeline | a click plays the song from the position at that time (a song jumps to the start of its positions, as F10 in the tracker) |
| Another song | Play on another song stops the one playing |
| After a song | Stop, Loop the song, Loop all songs (the next one of the list, after the last the first) or Shuffle (any other song); the next song follows without a gap |
| Keys | Space pauses and resumes |

## Build and run

```
SonicArranger2026/player/fetch-music.sh            # the songs into player/music (a sparse clone of Ambermoon)
cshiftc build SonicArranger2026/player             # -> SonicArranger2026/player/bin/ambermoon-player(.exe)
cshiftc publish SonicArranger2026/player           # -> SonicArranger2026/player/bin/ambermoon-player.html
```

`fetch-music.sh <folder>` copies the songs from the folder `Music` of an Ambermoon checkout instead. The player reads
the packed files (the export format, as the game has them) from the folder `music` next to it; in the browser page
they are inside of the page (`assets` of `cshift.json`). The workflow `.github/workflows/sonicarranger2026-pages.yml`
fetches the songs and publishes the page under `/player/` with the tracker.

When the page opens, it measures one song per frame (the replayer at 2000 Hz: the rows are the same at every rate),
so the lengths fill in within a few seconds; a song played before it was measured is measured at once. The browser
starts the sound only after a click on the page, which Play is.

| File | |
| --- | --- |
| `src/main.cshift` | the window: the list, the timelines, the modes |
| `src/play.cshift` | playing: the sound output, the songs one after the other, pause, seeking |
| `src/songs.cshift` | the songs of Ambermoon: number, name, file and the song in it |
