## Sonic Arranger Packed Files

Those files are size-optimized versions with only the information left that is needed to play the songs. The files can't be loaded by Sonic Arranger anymore and thus can't be edited.

Often these files also contain a replayer in Amiga m68k code. We will get to that later.

Note that all the data is stored as big-endian. Big-endian means the more significant bits come first. So the value 0x12345678 is stored as 0x12 0x34 0x56 0x78. This is not the case on systems like Windows or Linux where little-endian is used. So take care of this when reading values.

**Type descriptions:**
- **byte** is a 8-bit unsigned integer with the range 0..255
- **char** is a 8-bit signed integer with the range -128..127
- **ushort** is a 16-bit unsigned integer with the range 0..65535
- **short** is a 16-bit signed integer with the range -32768..32767
- **uint** is a 32-bit unsigned integer with the range 0..4294967295
- Note that signed values are represented as two-complement.

The file starts with a bunch of offsets. These offsets are relative to the start of the file data.

Offset | Type | Description
----|----|----
0x0000 | uint | Song data offset (always 0x00000028)
0x0004 | uint | Voice data offset (also called overtable)
0x0008 | uint | Note data offset (also called notetable)
0x000C | uint | Instrument data offset
0x0010 | uint | Wavetable data offset
0x0014 | uint | ADSR wavetable data offset
0x0018 | uint | AMF wavetable data offset
0x001C | uint | Sample data offset
0x0020 | ushort | **Unknown** (often but not always 0x2144 or 0x2154)
0x0022 | ushort | **Unknown** (always 0xffff?)
0x0024 | uint | **Unknown** (always 0?)

After this header the mentioned sections will follow in the given order at the given offsets.

Note that all sections (except for samples) have a fixed size per entry. So to calculate the number of entries you have to divide the whole section data size by the per-entry size.

**Section entry sizes in bytes:**
- Song: 12
- Voice: 4
- Note: 4
- Instrument: 152
- Waves (also ADSR and AMF waves): 128

So for example to calculate the number of songs you do:

```cs
int songSectionSize = voiceDataOffset - songDataOffset;
int songCount = songSectionSize / 12;
```

You can do this for all sections but samples. We get to samples later.

To read any section before the samples you do:

```cs
int sectionSize = nextSectionOffset - sectionOffset;
int entryCount = sectionSize / sizePerEntry;

for (int i = 0; i < entryCount; ++i)
    ReadSectionEntry(); // song, voice, note, instrument, wave
```

The first section is the song table. Offsets will now be relative to the section entry. The section's start offset is given in the header as mentioned. Here only a single entry is shown.

#### Song

Offset | Type | Description
----|----|----
0x0000 | ushort | Song speed (valid range is 1..16, see below)
0x0002 | ushort | Pattern length (number of divisions/notes in a pattern)
0x0004 | ushort | Start position (first pattern of the song, mostly 0)
0x0006 | ushort | Stop position (inclusive last pattern of the song)
0x0008 | ushort | Repeat position (if the song finishes it will continue from this pattern)
0x0008 | ushort | Number of interrupts per seconds (50 by default, see below)

There are two values that need a bit more explanation. We start with the number of interrupts per seconds (Ips).
This value is very important and influences the playback and effect speed. Every such interrupt will process
sound effects of all kinds like ADSR or AMF index changes, noise generator ticks, etc. It will also influence
how often a note is hit. Here comes the song speed into play.

The song speed gives the amount of ticks for each division. A division is one part of a pattern. So a division
can play a new note, call some command, etc. The default song speed is 6 so there are 6 ticks (or interrupts)
per division. This means that every 6th interrupt will play a new note or call some related command
(one division in the pattern is processed). Basically you can think of it like this:

```cs
void Tick()
{
  if (CurrentTick % SongSpeed == 0)
  {
    ProcessNextDivision(); // Play next notes, process note commands, etc
  }
  ProcessEffects(); // ADSR, AMF, instrument effects, etc
  ++CurrentTick;
}

Call Tick() every (1.0 / Ips) seconds.
```

Note that the song speed can change during playback via note commands while the Ips value will be constant for each file.
The default Ips of 50 means that every 20ms the interrupt is called. In SonicArranger terms an interrupt is a tick.

The song positions can not exceed the overtable size given in SonicArranger. This is basically the number of patterns.
This number is limited to 1000 so the song positions can be 999 at max and can only be overtable size minus 1. Don't
confuse the overtable size with the number of voice table entries. There are actually 4 * PatternCount voice entries
where the PatternCount is the overtable size given in SonicArranger. The 4 comes from the number of distinct tracks
in SonicArranger (and Amiga audio in general).

## Voice

Offset | Type | Description
----|----|----
0x0000 | ushort | Note address
0x0002 | char | Sound transpose
0x0003 | char | Note transpose

A voice basically represents a pattern. Therefore it contains several notes but stores only the address to the first note.
The other notes follow this note immediately inside the data. To process a pattern you can do the following:

```cs
for (int t = 0; t < 4; ++t) // 4 audio tracks
{
    Voice currentVoice = voiceTable[currentPatternIndex * 4 + t];

    for (int i = 0; i < song.PatternLength; ++i)
    {
        Note note = noteTable[currentVoice.NoteAddress + i];
        // Process note data ...
    }
}
```

In this example `currentPatternIndex` is the current song position or pattern to process.

So to sum things up, if the PatternLength is 16 there will be blocks of 16 note entries in the note table (see below).
And voices (or patterns) can reference such blocks via the note address. Voices therefore can and will re-use
notes (or blocks of notes).

The sound transpose will change the instrument index of all notes in the pattern by the given value. But each
note has a flag to disable this. The transpose is signed and can therefore be negative.

The note transpose will change the note index of all the notes in the pattern by the given value. But there
is a flag as well on each note to disable this. The transpose is signed and can therefore be negative.

The usage of the transpose values is described in the next section.

### Note

Offset | Type | Description
----|----|----
0x0000 | byte | Note index (0 = none, 1 = lowest C, max is 108)
0x0001 | byte | Instrument index (0 = none)
0x0002 | ushort | Note options

There is a fixed table of note period values that contains 110 values. Two of them have special meanings
so 108 are real note periods. This covers 9 octaves of 12 semi-tones. The order for each octave is
`C C# D D# E F F# G G# A A# B`.

#### Note period table

```cs
ushort[] NotePeriodTable = new ushort[110]
{
    // Index 0
    0x0000,
    // Octave 0
    0x3580, 0x3280, 0x2fa0, 0x2d00, 0x2a60, 0x2800, 0x25c0, 0x23a0, 0x21a0, 0x1fc0, 0x1e00, 0x1c50,
    // Octave 1
    0x1ac0, 0x1940, 0x17d0, 0x1680, 0x1530, 0x1400, 0x12e0, 0x11d0, 0x10d0, 0x0fe0, 0x0f00, 0x0e28,
    // Octave 2
    0x0d60, 0x0ca0, 0x0be8, 0x0b40, 0x0a98, 0x0a00, 0x0970, 0x08e8, 0x0868, 0x07f0, 0x0780, 0x0714,
    // Octave 3
    0x06b0, 0x0650, 0x05f4, 0x05a0, 0x054c, 0x0500, 0x04b8, 0x0474, 0x0434, 0x03f8, 0x03c0, 0x038a,
    // Octave 4
    0x0358, 0x0328, 0x02fa, 0x02d0, 0x02a6, 0x0280, 0x025c, 0x023a, 0x021a, 0x01fc, 0x01e0, 0x01c5,
    // Octave 5
    0x01ac, 0x0194, 0x017d, 0x0168, 0x0153, 0x0140, 0x012e, 0x011d, 0x010d, 0x00fe, 0x00f0, 0x00e2,
    // Octave 6
    0x00d6, 0x00ca, 0x00be, 0x00b4, 0x00aa, 0x00a0, 0x0097, 0x008f, 0x0087, 0x007f, 0x0078, 0x0071,
    // Octave 7
    0x006b, 0x0065, 0x005f, 0x005a, 0x0055, 0x0050, 0x004b, 0x0047, 0x0043, 0x003f, 0x003c, 0x0038,
    // Octave 8
    0x0035, 0x0032, 0x002f, 0x002d, 0x002a, 0x0028, 0x0025, 0x0023, 0x0021, 0x001f, 0x001e, 0x001c,
    // End marker
    0xffff,
};
```

The instument index is the index into the instrument table but 1-based so you have to decrease it by 1 to
access it. If the index is 0, no instrument is given. This will influence playback (we get to this later).

#### Note options

The note options provide several information. The highest two bits control if the voice's sound and note
transpose is disabled for this note.

You can handle the transpose values like this:

```cs
bool disableSoundTranspose = (note.Options & 0x8000) != 0;
bool disableNoteTranspose = (note.Options & 0x4000) != 0;
int noteIndex = note.Index;
int instrumentIndex = note.InstrumentIndex;

if (!disableNoteTranspose)
  noteIndex += voice.NoteTranspose;
if (instrumentIndex != 0 && !disableSoundTranspose)
  instrumentIndex += voice.SoundTranspose;
```

Then the next two bits together provide the arpeggio table index. Each instrument provides 3 arpeggio
tables. This is the 1-based index into it so reduce it by 1 to get the index. If this is 0 no arpeggio
table is used. In this case the note command 0 (arpeggio play) might also provide some arpeggio logic.

Speaking of note commands. Each note command has 3 parts. The command itself and 2 parameters. Each of
those are represented by a 4-bit nibble.

To sum things up, when note options are `0xFCAB` this means:

- 0xF: Disable flags and arpeggio index (expressed as 4 bits it is *SNAA*)
  - S: Disable sound transpose
  - N: Disable note transpose
  - AA: 1-based arpeggio table index (0 to 3)
- 0xC: The command (0x0 to 0xf)
- 0xA: Command parameter 1
- 0xB: Command parameter 2

Note that some commands will use one parameter that is created from the two parameters. For example often
`0xAB` is interpreted as one 8-bit parameter.

##### Note commands

For a list of note commands see [NoteCommands](NoteCommands.md).