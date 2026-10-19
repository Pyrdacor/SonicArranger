# SonicArranger2026

A tracker for Sonic Arranger modules (the Amiga tracker) for Windows, Linux and the browser. It is written in
[CShift](https://github.com/Robert-Schneckenhaus/CShift) and uses its immediate mode GUI `System.Ui`. The layout follows
the original (the over table with the positions, the instruments, the pattern editor with 4 voices), with a modern look.

This is the start; it grows step by step.

| | |
| --- | --- |
| Open | the module format (`SOAR`, `.sa`) and the packed format of the export, also with a replayer before it (as in Ambermoon) |
| Show | the songs and their timing, the over table (pattern address, sound and note transpose of each voice per position), the instruments, the patterns |
| Save | in the module format; Sonic Arranger and SonicConvert load it |
| Next | editing, the replayer (sound), the instrument editor, waves and samples, saving in the packed format |

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
| Left, Right | the voice |
| Page Up, Page Down | the position |
| Ctrl+O, Ctrl+S, Ctrl+Q | open, save as, quit |

`sonicarranger2026 --convert in out` saves a file in the module format without a window (a packed file becomes an
editable module, like `SonicConvert dec`).

## Layout

| Folder | |
| --- | --- |
| `lib` | the library `SonicArranger` (no GUI): the data of a module (`module.cshift`), reading both formats (`reader.cshift`), writing the module format (`writer.cshift`), names of notes, commands and effects |
| `src` | the tracker: the window, the over table, the instruments and the pattern view |
| `tests` | the tests of the library and `run.sh` |

Every value of a module is kept as it is in the file, also the bytes whose meaning is unknown, so a module file that
is opened and saved stays the same, byte for byte.

## Tests

```
SonicArranger2026/tests/run.sh [folder with Sonic Arranger files]
```

The script builds and runs the tests: a module made in code is saved and read back, and every file of the folder is
opened, saved in the module format and opened again (a module file must stay the same byte for byte). With `dotnet`
it then builds [SonicConvert](../SonicConvert) and converts each packed file and the module saved by
SonicArranger2026 from it to WAV: both must give the same WAV. Songs with a noise generator sound different on each run
of SonicConvert (it seeds the noise with the time), so for them only the length must agree.

With the 34 songs of Ambermoon (`git clone https://github.com/Pyrdacor/Ambermoon` and the folder `Music`, 34 packed
files and 34 modules) all checks pass, and SonicConvert gives the same WAV for 32 of the packed songs and the same
length for the 2 with noise. The workflow `.github/workflows/sonicarranger2026.yml` runs this on every push.

One difference to `SonicConvert dec` is on purpose: it writes an instrument's vibrato delay 0xFFFF ("no vibrato") as
0x00FF; SonicArranger2026 keeps 0xFFFF as it is in the file. Both sound the same.
