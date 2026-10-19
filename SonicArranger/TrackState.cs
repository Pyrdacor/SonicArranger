using System;

namespace SonicArranger
{
    /// <summary>
    /// The state of the song shared by the 4 voices: what the note commands
    /// change (speed, pattern length, the next position) and where the
    /// replayer is.
    /// </summary>
    internal class SongState
    {
        public int Speed;
        public int PatternLength;
        /// <summary>
        /// The position in the over table (sa_overLinePos). A position jump
        /// sets it to the target - 1; the next row then moves on by one.
        /// </summary>
        public int OverLinePos;
        /// <summary>
        /// The row in the pattern (sa_overPatternPos). Set to the pattern
        /// length, the next row starts the next position.
        /// </summary>
        public int OverPatternPos;
        /// <summary>
        /// The tick in the row (sa_songSpeedPos).
        /// </summary>
        public int SongSpeedPos;
        /// <summary>
        /// Note command 5 sets this value for the program that plays the
        /// music (it does not change the sound).
        /// </summary>
        public int SyncValue;
    }

    /// <summary>
    /// One voice of the replayer. This follows the Amiga replay routine of
    /// Sonic Arranger (SONIC_ARRANGER.S, reconstructed from Ambermoon): the
    /// fields are those of its track structure and the steps are done in
    /// the same order with the same integer arithmetic.
    /// </summary>
    internal class TrackState
    {
        static readonly byte[] silence = new byte[2];

        readonly PaulaState paulaState;
        readonly int trackIndex;
        readonly SonicArrangerFile file;
        readonly SongState song;
        readonly Random random;

        // the wave of a synth instrument, changed by the instrument effects
        // and played from here (the first 128 bytes of the track structure)
        readonly byte[] synthWave = new byte[128];

        Instrument? instrument;
        int currNote;
        int prevNote;
        int fineTuning;
        int pitchBend;
        int volume;
        int volumeSlide;
        int vibratoPos;
        int vibratoDelay;
        int portamentoValue;
        int portamento;
        int adsrPos;
        int sustainPos;
        int adsrDelay;
        int amfPos;
        int amfDelay;
        int synthEffectPos;
        int synthPos;
        int synthDelay;
        int arpeggioPos;
        int simpleArpeggioPos;
        /// <summary>
        /// The volume level for VU meters (4 times the volume).
        /// </summary>
        public int VolumeLevel { get; private set; }
        // bit 0: the instrument has ended, bit 1: a morph reached its wave,
        // bit 2: the free negator has ended, bit 3: the direction of the oscillator
        int flags;
        bool syntheticMode;
        bool sampleRepeat;
        Note rowNote;

        public TrackState(int index, PaulaState paulaState, SonicArrangerFile file, SongState song, Random random)
        {
            if (index < 0 || index > 3)
                throw new ArgumentOutOfRangeException(nameof(index));

            this.paulaState = paulaState ?? throw new ArgumentNullException(nameof(paulaState));
            trackIndex = index;
            this.file = file;
            this.song = song;
            this.random = random;
            flags = 1; // silent until the first note
        }

        PaulaState.TrackState Paula => paulaState.Tracks[trackIndex];

        // ---- reading the tables: like the original, an index past the end
        // of a wave reads the next one (the waves are one block of memory) ----

        static int WaveByte(WaveTable[] waves, int wave, int index)
        {
            long at = (long)wave * 128 + index;

            if (waves == null || at < 0 || at >= (long)waves.Length * 128)
                return 0;

            var data = waves[at / 128].Data;
            return data == null || at % 128 >= data.Length ? 0 : data[at % 128];
        }

        static int Signed(int value) => unchecked((sbyte)(byte)value);

        static int Period(int note)
        {
            return note < 0 || note >= Tables.NotePeriodTable.Length ? 0 : Tables.NotePeriodTable[note];
        }

        // the bytes of an instrument after the effect values: the 3 arpeggio
        // tables (16 bytes each) and the name; an arpeggio past its table
        // reads on into the next one
        static int ArpeggioByte(Instrument instrument, int offset)
        {
            if (offset < 0)
                return 0;
            int table = offset / 16;
            if (table < 3)
            {
                var arp = instrument.ArpegData[table];
                int i = offset % 16;
                return i == 0 ? arp.Length : i == 1 ? arp.Repeat : (arp.Data != null && i - 2 < arp.Data.Length ? arp.Data[i - 2] : 0);
            }
            int n = offset - 48;
            var name = instrument.Name ?? "";
            return n < name.Length ? name[n] & 0xff : 0;
        }

        int Byte(int index) => index >= 0 && index < synthWave.Length ? Signed(synthWave[index]) : 0;

        void SetByte(int index, int value)
        {
            if (index >= 0 && index < synthWave.Length)
                synthWave[index] = unchecked((byte)value);
        }

        // ---- a new row (SA_AdvanceTrack) ----

        /// <summary>
        /// Plays a row of the pattern: a note, an instrument or nothing, and
        /// the note command. Returns true when the DMA of the channel has to
        /// be turned on at the end of the interrupt.
        /// </summary>
        public bool Advance(Note note, int noteTranspose, int soundTranspose)
        {
            rowNote = note;
            bool startDma = false;
            int noteValue = note.Value;
            int instrumentNumber = note.Instrument;

            if (noteValue == 0)
            {
                if (instrumentNumber != 0)
                    LoadTrackInst(instrumentNumber);
            }
            else if (noteValue == 0x80)
            {
                // hold (sustain)
            }
            else if (noteValue == 0x7f)
            {
                ClrTrackInst();
            }
            else
            {
                if (!note.DisableNoteTranspose)
                    noteValue += noteTranspose;
                if (instrumentNumber != 0 && !note.DisableSoundTranspose)
                    instrumentNumber += soundTranspose;
                instrumentNumber &= 0xff;

                prevNote = currNote;
                currNote = noteValue;
                if (prevNote == 0)
                    prevNote = noteValue;

                // a sample is started again from the beginning: the DMA goes off
                if (!syntheticMode)
                {
                    paulaState.StopDma(trackIndex);
                    paulaState.SetLocation(trackIndex, Paula.Data, Paula.Offset, 2);
                    Paula.Period = 114;
                }

                if (instrumentNumber == 0)
                {
                    if (instrument == null)
                    {
                        // no instrument to play: the note command is not done
                        ClrTrackInst();
                        return false;
                    }
                    SetTrackInst();
                }
                else if (!LoadTrackInst(instrumentNumber))
                {
                    return false;
                }

                syntheticMode = instrument.Value.SynthMode;
                startDma = syntheticMode ? LoadTrackSynth() : LoadTrackSample();
            }

            ApplyNoteEffect(note);
            return startDma;
        }

        bool LoadTrackSample()
        {
            var instr = instrument.Value;
            int sample = instr.SampleWaveNo;

            if (sample < 0 || sample >= file.Samples.Length || file.Samples[sample].Data == null ||
                file.Samples[sample].Data.Length == 0)
            {
                ClrTrackInst();
                flags |= 1;
                return false;
            }

            var data = file.Samples[sample].Data;
            int words = (instr.Length & 0xffff) + (instr.Repeat == 1 ? 0 : instr.Repeat & 0xffff);
            paulaState.SetLocation(trackIndex, data, 0, Math.Min(words * 2, data.Length));
            // the repeat part is set in the next interrupt
            sampleRepeat = true;
            return true;
        }

        bool LoadTrackSynth()
        {
            var instr = instrument.Value;
            int words = instr.Length;
            paulaState.SetLocation(trackIndex, synthWave, 0, Math.Max(0, Math.Min(words * 2, synthWave.Length)));
            // the wave is copied in blocks of 16 bytes
            int bytes = Math.Min(128, (((words - 1) >> 3) + 1) * 16);
            for (int i = 0; i < bytes; ++i)
                synthWave[i] = (byte)WaveByte(file.Waves, instr.SampleWaveNo, i);
            sampleRepeat = false;
            return true;
        }

        /// <summary>
        /// The interrupt after a sample was started: it plays its repeat part
        /// from now on, or silence if its repeat is 1.
        /// </summary>
        public void RepeatSample()
        {
            if (!sampleRepeat)
                return;

            sampleRepeat = false;

            if (instrument == null || instrument.Value.Repeat == 0)
                return;

            var instr = instrument.Value;
            var data = instr.SampleWaveNo >= 0 && instr.SampleWaveNo < file.Samples.Length ? file.Samples[instr.SampleWaveNo].Data : null;

            if (instr.Repeat == 1 || data == null)
            {
                paulaState.SetLocation(trackIndex, silence, 0, 2);
                return;
            }

            int start = (instr.Length & 0xffff) * 2;
            paulaState.SetLocation(trackIndex, data, start, Math.Max(0, Math.Min((instr.Repeat & 0xffff) * 2, data.Length - start)));
        }

        bool LoadTrackInst(int number)
        {
            if (number <= 0 || number > file.Instruments.Length)
            {
                ClrTrackInst();
                return false;
            }

            var instr = file.Instruments[number - 1];
            instrument = instr;
            volume = instr.Volume;
            portamentoValue = instr.Portamento;
            portamento = 0;
            vibratoPos = 0;
            vibratoDelay = instr.RawVibDelay;
            SetTrackInst();
            return true;
        }

        void SetTrackInst()
        {
            var instr = instrument.Value;
            fineTuning = Signed(instr.FineTuning);
            adsrDelay = instr.AdsrDelay;
            adsrPos = 0;
            sustainPos = 0;
            amfDelay = instr.AmfDelay;
            amfPos = 0;
            synthEffectPos = instr.Effect2;
            synthPos = 0;
            synthDelay = instr.EffectDelay;
            arpeggioPos = 0;
            simpleArpeggioPos = 0;
            VolumeLevel = ((volume * paulaState.MasterVolume) >> 6) * 4;
            flags = 0;
        }

        void ClrTrackInst()
        {
            Paula.Volume = 0;
            paulaState.SetLocation(trackIndex, silence, 0, 2);
            volume = 0;
            VolumeLevel = 0;
            instrument = null;
            sampleRepeat = false;
        }

        // ---- note commands (SA_ApplyNoteEffect) ----

        void ApplyNoteEffect(Note note)
        {
            pitchBend = 0;
            volumeSlide = 0;
            int param = note.CommandInfo;
            int signedParam = Signed(param);

            switch (note.Command)
            {
                case Note.NoteCommand.SlideUp:
                    pitchBend = signedParam;
                    break;
                case Note.NoteCommand.SetADSRIndex:
                    adsrPos = param;
                    break;
                case Note.NoteCommand.ResetVibrato:
                    // the vibrato starts now (speed and level stay those of the instrument)
                    vibratoDelay = 0;
                    break;
                case Note.NoteCommand.SetVibrato:
                    song.SyncValue = param;
                    break;
                case Note.NoteCommand.SetMasterVolume:
                    paulaState.MasterVolume = param & 0x3f;
                    break;
                case Note.NoteCommand.SetPortamento:
                    portamentoValue = param;
                    break;
                case Note.NoteCommand.ClearPortamento:
                    portamentoValue = 0;
                    break;
                case Note.NoteCommand.SetPatternLength:
                    if (param > 0 && param <= 64)
                        song.PatternLength = param;
                    break;
                case Note.NoteCommand.VolumeSlide:
                    volumeSlide = signedParam;
                    break;
                case Note.NoteCommand.PositionJump:
                    song.OverLinePos = param - 1;
                    song.OverPatternPos = song.PatternLength;
                    break;
                case Note.NoteCommand.SetVolume:
                    volume = Math.Min(64, param);
                    VolumeLevel = ((paulaState.MasterVolume * volume) >> 6) * 4;
                    break;
                case Note.NoteCommand.PatternBreak:
                    song.OverPatternPos = song.PatternLength;
                    break;
                case Note.NoteCommand.DisableHardwareLPF:
                    // the power LED: 0 turns the filter on, other values turn it off
                    paulaState.UseLowPassFilter = param == 0;
                    break;
                case Note.NoteCommand.SetSpeed:
                    if (signedParam > 0 && signedParam <= 16)
                        song.Speed = param;
                    break;
                default:
                    break;
            }
        }

        // ---- every tick (SA_ProcessTrack) ----

        public void Process()
        {
            if ((flags & 1) != 0 || instrument == null)
            {
                Paula.Volume = 0;
                paulaState.SetLocation(trackIndex, silence, 0, 2);
                if (VolumeLevel != 0)
                    VolumeLevel -= 4;
                return;
            }

            var instr = instrument.Value;
            int note = currNote;
            int lastNote = prevNote;
            int arpeggio = rowNote.ArpeggioIndex;

            if (arpeggio != 0)
            {
                // the arpeggio table of the instrument: the notes are bytes
                int table = (arpeggio - 1) * 16;
                int offset = ArpeggioByte(instr, table + 2 + arpeggioPos);
                note = (note & ~0xff) | ((note + offset) & 0xff);
                lastNote = (lastNote & ~0xff) | ((lastNote + offset) & 0xff);
                ++arpeggioPos;
                int length = ArpeggioByte(instr, table);
                if (((length + ArpeggioByte(instr, table + 1)) & 0xff) <= arpeggioPos)
                    arpeggioPos = length;
            }
            else if (rowNote.Command == Note.NoteCommand.Arpeggio && rowNote.CommandInfo != 0)
            {
                // command 0: the note, the note + x semitones, the note + y semitones
                int add = 0;
                if (simpleArpeggioPos == 0)
                    ++simpleArpeggioPos;
                else if (simpleArpeggioPos == 1)
                {
                    ++simpleArpeggioPos;
                    add = (rowNote.CommandInfo >> 4) & 0xf;
                }
                else
                {
                    simpleArpeggioPos = 0;
                    add = rowNote.CommandInfo & 0xf;
                }
                note += add;
                lastNote += add;
            }

            int period = Period(note);
            int lastPeriod = Period(lastNote);

            // portamento: from the last note to this one
            if (portamentoValue != 0)
            {
                if (portamento == 0)
                    portamento = lastPeriod;
                lastPeriod = portamento;
                if (Math.Abs(period - lastPeriod) - portamentoValue < 0)
                    portamentoValue = 0;
                else
                {
                    lastPeriod += lastPeriod < period ? portamentoValue : -portamentoValue;
                    portamento = lastPeriod;
                    period = lastPeriod;
                }
            }

            // vibrato: after its delay in every tick; a delay with a low byte
            // of 255 means no vibrato
            if ((vibratoDelay & 0xff) != 0xff)
            {
                if (vibratoDelay != 0)
                    --vibratoDelay;
                else
                {
                    int level = unchecked((short)instr.VibLevel);
                    if (level != 0)
                        period += Signed(Tables.VibratoTable[vibratoPos & 0xff]) * 4 / level;
                    vibratoPos = (vibratoPos + instr.VibSpeed) & 0xff;
                }
            }

            // AMF: the period changes along a wave
            int amfTotal = (instr.AmfLength & 0xffff) + (instr.AmfRepeat & 0xffff);
            if ((amfTotal & 0xffff) != 0)
            {
                period -= Signed(WaveByte(file.AmfWaves, instr.AmfWave, amfPos));
                amfDelay = (amfDelay - 1) & 0xffff;
                if (amfDelay == 0)
                {
                    amfDelay = instr.AmfDelay;
                    ++amfPos;
                    if (amfTotal <= amfPos)
                        amfPos = instr.AmfLength - (instr.AmfRepeat == 0 ? 1 : 0);
                }
            }

            period -= fineTuning;
            if (period < 113)
                period = 113;
            if (song.SongSpeedPos != 0)
                fineTuning += pitchBend;

            Paula.Period = period;

            if (instr.SynthMode)
                ApplySynthEffect(instr);

            // the volume, with the ADSR envelope
            int adsrTotal = (instr.AdsrLength & 0xffff) + (instr.AdsrRepeat & 0xffff);
            if ((adsrTotal & 0xffff) == 0)
            {
                Paula.Volume = (volume * paulaState.MasterVolume) >> 6;
                if (VolumeLevel != 0)
                    VolumeLevel -= 4;
            }
            else
            {
                int adsr = (WaveByte(file.AdsrWaves, instr.AdsrWave, adsrPos) * paulaState.MasterVolume) >> 6;
                int vol = (volume * adsr) >> 6;
                Paula.Volume = vol;
                VolumeLevel = vol * 4;
                bool step = true;

                // a held note (0x80) stays at the sustain point, or moves on
                // every SustainVal ticks
                if (rowNote.Value == 0x80 && adsrPos >= instr.SustainPt)
                {
                    if (instr.SustainVal == 0)
                        step = false;
                    else if (sustainPos != 0)
                    {
                        --sustainPos;
                        step = false;
                    }
                    else
                        sustainPos = instr.SustainVal;
                }

                if (step)
                {
                    adsrDelay = (adsrDelay - 1) & 0xffff;
                    if (adsrDelay == 0)
                    {
                        adsrDelay = instr.AdsrDelay;
                        ++adsrPos;
                        if (adsrTotal <= adsrPos)
                        {
                            adsrPos = instr.AdsrLength - (instr.AdsrRepeat == 0 ? 1 : 0);
                            if (instr.AdsrRepeat == 0 && (adsr & 0xff) == 0)
                                flags |= 1;
                        }
                    }
                }
            }

            volume = Math.Max(0, Math.Min(64, volume + volumeSlide));
        }

        // ---- the effects of synth instruments (SA_ApplySynthEffect) ----

        void ApplySynthEffect(Instrument instr)
        {
            synthDelay = (synthDelay - 1) & 0xffff;
            if (synthDelay != 0)
                return;
            synthDelay = instr.EffectDelay;

            int effect = (int)instr.EffectNumber;
            if (effect <= 0 || effect > 17)
                return;

            int e1 = instr.Effect1;
            int e2 = instr.Effect2;
            int e3 = instr.Effect3;
            bool advance = true;

            switch (instr.EffectNumber)
            {
                case Instrument.Effect.WaveNegator:
                    SetByte(synthEffectPos, -Byte(synthEffectPos));
                    break;
                case Instrument.Effect.FreeNegator:
                {
                    advance = false;
                    if ((flags & 4) != 0)
                        break;
                    int offset = WaveByte(file.Waves, e1, synthPos) & 0x7f;
                    int length = (instr.Length * 2) & 0xffff;
                    for (int i = offset; i < length; ++i)
                        SetByte(i, WaveByte(file.Waves, instr.SampleWaveNo, i));
                    for (int i = 0; i < offset; ++i)
                        SetByte(i, -Signed(WaveByte(file.Waves, instr.SampleWaveNo, i)));
                    ++synthPos;
                    if ((synthPos & 0xffff) > ((e2 + e3) & 0xffff))
                    {
                        synthPos = e2;
                        if (e3 == 0 && offset == 0)
                            flags |= 4;
                    }
                    break;
                }
                case Instrument.Effect.RotateVertical:
                    for (int i = e2; i <= e3; ++i)
                        SetByte(i, Byte(i) + e1);
                    break;
                case Instrument.Effect.RotateHorizontal:
                {
                    // the bytes from Effect2 to Effect3 + 1 rotate by one
                    int first = Byte(e2);
                    int i = e2;
                    int count = e3 - e2;
                    do
                    {
                        SetByte(i, Byte(i + 1));
                        ++i;
                    } while (--count >= 0);
                    SetByte(i, first);
                    break;
                }
                case Instrument.Effect.AlienVoice:
                    advance = false;
                    for (int i = e2; i <= e3; ++i)
                        SetByte(i, Byte(i) + WaveByte(file.Waves, e1, i));
                    break;
                case Instrument.Effect.PolyNegator:
                {
                    int pos = synthEffectPos;
                    SetByte(pos, WaveByte(file.Waves, instr.SampleWaveNo, pos));
                    if (pos >= e3)
                        pos = e2 - 1;
                    SetByte(pos + 1, -Byte(pos + 1));
                    break;
                }
                case Instrument.Effect.ShackWave1:
                    ShackWave(e1, e2, e3);
                    break;
                case Instrument.Effect.ShackWave2:
                    ShackWave(e1, e2, e3);
                    SetByte(e2 + synthPos, -Byte(e2 + synthPos));
                    ++synthPos;
                    if (e3 - e2 < synthPos)
                        synthPos = 0;
                    break;
                case Instrument.Effect.Metawdrpk:
                    // metamorph: the wave changes step by step into the effect wave
                    advance = false;
                    if ((flags & 2) == 0)
                        Morph(e1, e2, e3);
                    break;
                case Instrument.Effect.Oscillator1:
                    // the wave changes into the effect wave and back, again and again
                    advance = false;
                    if ((flags & 2) != 0)
                    {
                        flags ^= 8;
                        flags &= ~2;
                    }
                    Morph((flags & 8) != 0 ? instr.SampleWaveNo : e1, e2, e3);
                    break;
                case Instrument.Effect.LaserAmf:
                    if ((e3 & 0xffff) > (synthPos & 0xffff))
                    {
                        fineTuning += Signed(e2);
                        ++synthPos;
                    }
                    break;
                case Instrument.Effect.WaveAlias:
                    for (int i = e2; i <= e3; ++i)
                        SetByte(i, Byte(i) > Byte(i + 1) ? Byte(i) - e1 : Byte(i) + e1);
                    break;
                case Instrument.Effect.NoiseGenerator:
                    // the original uses the horizontal position of the beam
                    SetByte(synthEffectPos, Byte(synthEffectPos) ^ random.Next(256));
                    break;
                case Instrument.Effect.NoiseGenerator2:
                    advance = false;
                    for (int i = e3; i >= e2; --i)
                    {
                        int value = (Byte(i) ^ 5) & 0xff;
                        value = ((value << 2) | (value >> 6)) & 0xff;
                        SetByte(i, value + random.Next(256));
                    }
                    break;
                case Instrument.Effect.LowPassFilter1:
                    LowPass(e2, e3, i => e1);
                    break;
                case Instrument.Effect.LowPassFilter2:
                    // the limits come from the effect wave, read from Effect3 on
                    LowPass(e2, e3, i => WaveByte(file.Waves, e1, e3 + i - e2) & 0x7f);
                    break;
                case Instrument.Effect.FMDrum:
                    if ((e3 & 0xffff) <= (synthPos & 0xffff))
                    {
                        fineTuning = instr.FineTuning;
                        synthPos = 0;
                    }
                    fineTuning = unchecked((short)(fineTuning - (((e2 & 0xffff) * (e1 & 0xffff)) & 0xffff)));
                    ++synthPos;
                    break;
            }

            if (advance)
            {
                ++synthEffectPos;
                if ((synthEffectPos & 0xffff) > (e3 & 0xffff))
                    synthEffectPos = e2;
            }
        }

        // the same value of the effect wave is added to all bytes
        void ShackWave(int e1, int e2, int e3)
        {
            int value = WaveByte(file.Waves, e1, e2 + synthEffectPos);
            for (int i = e2; i <= e3; ++i)
                SetByte(i, Byte(i) + value);
        }

        // each byte moves one step towards the wave; when none moves, the morph has ended
        void Morph(int wave, int e2, int e3)
        {
            bool changed = false;
            for (int i = e2; i <= e3; ++i)
            {
                int value = Byte(i);
                int target = Signed(WaveByte(file.Waves, wave, i));
                if (value == target)
                    continue;
                changed = true;
                SetByte(i, value < target ? value + 1 : value - 1);
            }
            if (!changed)
                flags |= 2;
        }

        // the bytes that differ from the next one by more than the limit move 2 towards it
        void LowPass(int e2, int e3, Func<int, int> limit)
        {
            for (int i = e2; i <= e3; ++i)
            {
                int value = Byte(i);
                int next = i == e3 ? Byte(e2) : Byte(i + 1);
                bool up = value <= next;
                int diff = Signed(value - next);
                if (diff < 0)
                    diff = (-diff) & 0xff;
                if (diff > unchecked((short)limit(i)))
                    SetByte(i, up ? value + 2 : value - 2);
            }
        }
    }
}
