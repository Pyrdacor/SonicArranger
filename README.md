# SonicArranger

Cross-platform SonicArranger reader/converter

### Documentation

All the docs can be found [here](Docs).
- [General file format](Docs/NormalFileFormat.md)
- [Packed file format](Docs/PackedFileFormat.md)
- [Note commands](Docs/NoteCommands.md)
- *to be continued ...*


### SonicConvert

Converts SA files to WAV files.

Version | Normal | Standalone
--- | --- | --- 
**1.5** (Windows 64bit) | [Download](https://github.com/Pyrdacor/SonicArranger/releases/download/v1.5/SonicConvert-Windows.zip) | [Download](https://github.com/Pyrdacor/SonicArranger/releases/download/v1.5/SonicConvert-Windows-Standalone.zip)
**1.5** (Linux 64bit) | [Download](https://github.com/Pyrdacor/SonicArranger/releases/download/v1.5/SonicConvert-Linux.tar.gz) | [Download](https://github.com/Pyrdacor/SonicArranger/releases/download/v1.5/SonicConvert-Linux-Standalone.tar.gz)
**1.5** (Windows 32bit) | [Download](https://github.com/Pyrdacor/SonicArranger/releases/download/v1.5/SonicConvert-Windows32Bit.zip) | [Download](https://github.com/Pyrdacor/SonicArranger/releases/download/v1.5/SonicConvert-Windows32Bit-Standalone.zip)

The standalone versions should work without .NET installed but are larger in size.

[![Build status](https://ci.appveyor.com/api/projects/status/iieprvdbq1hdp1uc?svg=true)](https://ci.appveyor.com/project/Pyrdacor/sonicarranger)

#### Changlog

- 1.5: Fixed loading of module format, added EDAT reading (editor data)
- 1.4: Fixed an issue with the effect index which lead to crashes in some cases, new hardware LPF emu
- 1.3: Library adjustments, no new features or bugfixes though
- 1.2: Fixed wrong instrument bug (and also an associated crash)
- 1.1: Fixed several effects (wave negator, vibrato with delay 0, etc)
- 1.0: First release