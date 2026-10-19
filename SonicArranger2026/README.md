# SonicArranger2026

A tracker for Sonic Arranger modules (the Amiga tracker) for Windows, Linux and the browser. It is written in
[CShift](https://github.com/Robert-Schneckenhaus/CShift) and uses its immediate mode GUI `System.Ui`. The layout follows
the original (the over table with the positions, the instruments, the pattern editor with 4 voices), with a modern look.

This is the start; it grows step by step.

| | |
| --- | --- |
| Open | the module format (`SOAR`, `.sa`) and the packed format of the export, also with a replayer before it (as in Ambermoon) |
| Show | the songs and their timing, the over table (pattern address, sound and note transpose of each voice per position), the instruments, the patterns |
| Edit | notes, instruments and note options in the pattern (the keyboard as a piano), the values of the song, the voices of a position, inserting and removing positions, new patterns; undo and redo |
| Save | in the module format; Sonic Arranger and SonicConvert load it |
| Next | the replayer (sound), the instrument editor, waves and samples, saving in the packed format |

## Build and run

With [cshiftc](https://github.com/Robert-Schneckenhaus/CShift/releases) (0.37 or newer):

```
cshiftc build SonicArranger2026          # -> SonicArranger2026/bin/sonicarranger2026(.exe)
SonicArranger2026/bin/sonicarranger2026 [file]
cshiftc publish SonicArranger2026        # -> SonicArranger2026/bin/sonicarranger2026.html, for the browser
```

Nothing else has to be installed: the window comes from the operating system (Win32, X11), the GUI draws itself.
In the browser, File > Open uses the browser's file chooser and File > Save as gives a download.

| Key | |
| --- | --- |
| Up, Down, Home, End | the row in the pattern |
| Left, Right | the column of the cell (note, instrument, note options) and the voice |
| Page Up, Page Down | the position |
| Space | edit mode on and off (the cursor is red in edit mode) |
| F1 to F8 | the octave (0 to 7) of the lower row of piano keys |
| Ctrl+Z, Ctrl+Y | undo, redo |
| Ctrl+I, Ctrl+Del | insert a position after this one (a copy), remove this position |
| Ctrl+N, Ctrl+D | the voice gets a new empty pattern, or a copy of its pattern, at the end of the note table |
| Ctrl+O, Ctrl+S, Ctrl+Q | open, save as, quit (asks first when there are changes) |

In edit mode, on the note column the keyboard is a piano as in ProTracker: `Z S X D C V G B H N J M` is the octave
chosen with F1 to F8, `Q 2 W 3 E R 5 T 6 Y 7 U I` the one above (letters of a US keyboard; on a German one Y and Z
are swapped). A note gets the instrument selected in the list. On the instrument columns the keys 0 to 9 set its two
digits, on the 4 columns of the note options the hex digits 0 to 9 and A to F (flags and arpeggio table, command,
parameter). Delete and Backspace clear the column. The cursor then moves down by the step.

Patterns are blocks of the note table and positions only point at them, so a pattern can be played at several
positions and by several voices: changing a note changes it everywhere the pattern is played (as in Sonic Arranger).
Ctrl+N or Ctrl+D give the voice a pattern of its own. A new module starts with one position and a pattern for each
voice.

`sonicarranger2026 --convert in out` saves a file in the module format without a window (a packed file becomes an
editable module, like `SonicConvert dec`).

## Layout

| Folder | |
| --- | --- |
| `lib` | the library `SonicArranger` (no GUI): the data of a module (`module.cshift`), reading both formats (`reader.cshift`), writing the module format (`writer.cshift`), names of notes, commands and effects |
| `lib/src/edit.cshift` | changing notes, positions and patterns; undo and redo |
| `src` | the tracker: the window (`main`), the panels (`views`), the pattern view (`pattern`), the keys (`keys`) |
| `tests` | the tests of the library and `run.sh` |

Every value of a module is kept as it is in the file, also the bytes whose meaning is unknown, so a module file that
is opened and saved stays the same, byte for byte.

## Tests

```
SonicArranger2026/tests/run.sh [folder with Sonic Arranger files]
```

The script builds and runs the tests: a module made in code is saved and read back; changing notes, positions and
patterns, and undo; and every file of the folder is opened, saved in the module format and opened again (a module
file must stay the same byte for byte). With `dotnet` it then builds [SonicConvert](../SonicConvert) and converts each
packed file and the module saved by SonicArranger2026 from it to WAV: both must give the same WAV. Songs with a noise
generator sound different on each run of SonicConvert (it seeds the noise with the time), so for them only the length
must agree. A copy of each module with a note changed must convert as well and sound different.

`tests/ui-check.mjs` drives the browser page with Playwright: it enters notes and a command in edit mode, undoes a
change, saves the module (a download) and checks the notes in the file.

With the 34 songs of Ambermoon (`git clone https://github.com/Pyrdacor/Ambermoon` and the folder `Music`, 34 packed
files and 34 modules) all checks pass, and SonicConvert gives the same WAV for 32 of the packed songs and the same
length for the 2 with noise; all 34 edited ones convert and sound different. The workflow
`.github/workflows/sonicarranger2026.yml` runs this and the browser check on every push.

One difference to `SonicConvert dec` is on purpose: it writes an instrument's vibrato delay 0xFFFF ("no vibrato") as
0x00FF; SonicArranger2026 keeps 0xFFFF as it is in the file. Both sound the same.
