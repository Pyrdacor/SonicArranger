﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SonicArranger
{
	/// <summary>
	/// Timing: There is the primary timing which is
	/// mostly used for effects and settings. It is
	/// controlled by the IrqsPerSecond setting which
	/// is stored as <see cref="Song.NBIrqps"/>. This
	/// is the number of interrupt calls per second.
	/// Each interrupt applies effects like ADSR, AMF
	/// or volume fading.
	/// 
	/// Table entries used in patterns normally won't
	/// use the primary ticks as it is much too short
	/// for a sample to play. Therefore the secondary
	/// timing can be used. It is controlled by the
	/// song speed which is 6 by default but can be
	/// changed during playback or globally.
	/// 
	/// The BPM for song speed 6 is calculated as
	/// 60 * NBIrqps / 24 which is 125 by default
	/// as NBIrqps is 50 by default. For other speeds
	/// this BPM value is just multiplied by speed/6.
	/// 
	/// For example if song speed is 4 and NBIrqps is 50
	/// the BPM will be 125 * 6 / 4 which is 187.5.
	/// 
	/// To get the number of notes per second the
	/// simple formula NBIrqps/speed can be used.
	/// For example with default settings (speed=6,
	/// NBIrqps=50) there are 50/6 notes per second
	/// which equals 8.333 notes per second.
	/// 
	/// So the note duration in seconds is:
	/// 1/(NBIrqps/speed) = speed/NBIrqps.
	/// Default a note lasts for 0.12 seconds.
	/// 
	/// The volume (amplitude) is always in the range
	/// 0x00 (0) to 0x40 (64) which means 0% to 100%.
	/// 
	/// Sonic Arranger supports samples and synthetic
	/// mode (waves). If synthetic mode is off (0) a
	/// sample is used, otherwise (1) a wave from the
	/// synth wave tables.
	/// 
	/// Each instrument sets this option in <see cref="Instrument.SynthMode"/>.
	/// Then the sample or synth wave index is given in <see cref="Instrument.SampleWaveNo"/>.
	/// 
	/// Synth waves are given as a wave table and are always 128 bytes long.
	/// But the length can be lower than that and parts can be repeated.
	/// You can access the wave table data by the property <see cref="Waves"/>.
	/// Synth waves are often used together with ADSR waves to create effects.
	/// Note that the total data length of the wave data is Length plus Repeat
	/// and the sum can only be 128 at max. Also note that both values are in words
	/// so you have multiply each of them by 2 to get the size in bytes. This is
	/// not true for ADSR and AMF waves though but the Length + Repeat thing is.
	/// 
	/// Samples are at the end of the SA file and are stored in <see cref="Samples"/>.
	/// 
	/// There are also two additional wave tables. The first one (right after the
	/// synth wave tables) are the ADSR waves (also up to 128 bytes each).
	/// An instrument can use them by specify the index in <see cref="Instrument.AdsrWave"/>.
	/// Then with <see cref="Instrument.AdsrLength"/> and <see cref="Instrument.AdsrRepeat"/>
	/// the used part can be specified. A length and repeat of 0 means that no ADSR is used.
	/// You can access the ADSR waves with <see cref="AdsrWaves"/>.
	/// ADSR wave tables contain volume amplitudes in the range 0 to 64 (100%).
	/// 
	/// The second of those wave tables is stored as "SYAF" and I guess it is related
	/// to the AMF values of the instrument. It is rarely used and I don't know the exact
	/// usage but data-wise it is handled in the same fashion as the ADSR waves.
	/// You can access the AMF waves with <see cref="AmfWaves"/>.
	/// 
	/// </summary>
	public class SonicArrangerFile
	{
		public string Owner { get; private set; }
		public string Version { get; private set; }
		public EditData EditData { get; private set; }

		public Song[] Songs { get; private set; }
		public Voice[] Voices { get; private set; }
		public Note[] Notes { get; private set; }
		public Instrument[] Instruments { get; private set; }
		public WaveTable[] Waves { get; private set; }
		public WaveTable[] AmfWaves { get; private set; }
		public WaveTable[] AdsrWaves { get; private set; }
		public Sample[] Samples { get; private set; }

		private readonly SongTable songTable = null;
		private readonly OverTable overTable = null;
		private readonly NoteTable noteTable = null;
		private readonly InstrumentTable instrumentTable = null;
		private readonly SampleTable sampleTable = null;

		public SonicArrangerFile(ICustomReader reader)
        {
			void ThrowInvalidData() => throw new InvalidDataException("No valid SonicArranger data stream");

			if (reader.Size < 4)
				ThrowInvalidData();

			string soar = new string(reader.ReadChars(4));

			if (soar != "SOAR")
			{
				reader.Position -= 4;
				int start = FindStart(reader);

				if (start == -1)
					ThrowInvalidData();

				// Songtable
				int songOffset = 0x28;
				// Overtable (Voices)
				int overTableOffset = reader.ReadBEInt32();
				// Notetable
				int noteTableOffset = reader.ReadBEInt32();
				// Instruments
				int instrumentsOffset = reader.ReadBEInt32();
				// Synth waveforms
				int sywtptr = reader.ReadBEInt32();
				// Synth ASDR waves
				int syarOffset = reader.ReadBEInt32();
				// Synth AMF waves
				int syafOffset = reader.ReadBEInt32();
				// Sample data
				int samplesOffset = reader.ReadBEInt32();

				ushort magic = reader.ReadBEUInt16(); // always 0x2144 or 0x2154
				// TODO: is this some sample rate?

				/*if (magic != 0x2144 && magic != 0x2154)
					ThrowInvalidData();*/

				if (reader.ReadBEUInt16() != 0xffff) // always 0xffff
					ThrowInvalidData();

				uint unknownDword = reader.ReadBEUInt32(); // always 0? end of header marker?

				// Read songs
				reader.Position = start + songOffset;
				int numSongs = (overTableOffset - songOffset) / 12;
				Songs = new Song[numSongs];
				for (int i = 0; i < numSongs; ++i)
				{
					Songs[i] = new Song(reader);
				}
				songTable = new SongTable(Songs);

				// Read voices
				reader.Position = start + overTableOffset;
				int numVoices = (noteTableOffset - overTableOffset) / 4;
				Voices = new Voice[numVoices];
				for (int i = 0; i < numVoices; ++i)
				{
					Voices[i] = new Voice(reader);
				}
				overTable = new OverTable(Voices);

				// Read notes
				reader.Position = start + noteTableOffset;
				int numNotes = (instrumentsOffset - noteTableOffset) / 4;
				Notes = new Note[numNotes];
				for (int i = 0; i < numNotes; ++i)
				{
					Notes[i] = new Note(reader);
				}
				noteTable = new NoteTable(Notes);

				// Read instruments
				reader.Position = start + instrumentsOffset;
				int numInstruments = (sywtptr - instrumentsOffset) / 152;
				Instruments = new Instrument[numInstruments];
				for (int i = 0; i < numInstruments; ++i)
				{
					Instruments[i] = new Instrument(reader);
				}
				instrumentTable = new InstrumentTable(Instruments);

				// Read synth wave forms
				reader.Position = start + sywtptr;
				int numWaveForms = (syarOffset - sywtptr) / 128;
				Waves = new WaveTable[numWaveForms];
				for (int i = 0; i < numWaveForms; ++i)
                {
					Waves[i] = new WaveTable(reader);
                }

				// Read ADSR wave forms
				reader.Position = start + syarOffset;
				int numSynthArrangements = (syafOffset - syarOffset) / 128;
				AdsrWaves = new WaveTable[numSynthArrangements];
				for (int i = 0; i < numSynthArrangements; ++i)
				{
					AdsrWaves[i] = new WaveTable(reader);
				}

				// Read AMF wave forms
				reader.Position = start + syafOffset;
				int numSynthAmfWaves = (samplesOffset - syafOffset) / 128;
				AmfWaves = new WaveTable[numSynthAmfWaves];
				for (int i = 0; i < numSynthAmfWaves; ++i)
				{
					AmfWaves[i] = new WaveTable(reader);
				}

				// Read samples
				reader.Position = start + samplesOffset;
				Samples = (sampleTable = new SampleTable(reader, false)).Samples;

				if (new string(reader.ReadChars(8)) != "deadbeef" ||
					reader.ReadBEUInt32() != 0)
					ThrowInvalidData();

				List<byte> authorBytes = new List<byte>(256);
				bool readAuthor = true;

				while (true)
                {
					byte b = reader.ReadByte();

					if (b == 0)
						break;

					if (!readAuthor)
						continue;

					// As characters are "NOT" encoded and ASCII is used
					// where the msb is 0, the msb for printable characters
					// should be 1 in the author data. So we stop if this
					// is no longer the case. But we will still wait for
					// the end-marker (0 byte).
					if ((b & 0x80) == 0)
					{
						readAuthor = false;
					}
					else
					{
						// Characters are "NOT" encoded
						b = unchecked((byte)~b);
						authorBytes.Add(b);
					}
				}

				Owner = Encoding.ASCII.GetString(authorBytes.ToArray());
				Version = "V1.0";
			}
			else
			{
				Version = new string(reader.ReadChars(4));
				string tag;
				while (true)
				{
					if (reader.Position == reader.Size)
						break;

					tag = new string(reader.ReadChars(4));
					switch (tag)
					{
						case "STBL":
							Songs = (songTable = new SongTable(reader)).Songs;
							break;
						case "OVTB":
							Voices = (overTable = new OverTable(reader)).Voices;
							break;
						case "NTBL":
							Notes = (noteTable = new NoteTable(reader)).Notes;
							break;
						case "INST":
							Instruments = (instrumentTable = new InstrumentTable(reader)).Instruments;
							break;
						case "SD8B":
							Samples = (sampleTable = new SampleTable(reader, true)).Samples;
							break;
						case "SYWT":
						{
							int numTables = reader.ReadBEInt32();
							if (numTables > 0)
							{
								Waves = new WaveTable[numTables];
								for (int i = 0; i < numTables; ++i)
									Waves[i] = new WaveTable(reader);
							}
							break;
						}
						case "SYAR":
						{
							int numTables = reader.ReadBEInt32();
							if (numTables > 0)
							{
								AdsrWaves = new WaveTable[numTables];
								for (int i = 0; i < numTables; ++i)
									AdsrWaves[i] = new WaveTable(reader);
							}
							break;
						}
						case "SYAF":
						{
							int numTables = reader.ReadBEInt32();
							if (numTables > 0)
							{
								AmfWaves = new WaveTable[numTables];
								for (int i = 0; i < numTables; ++i)
									AmfWaves[i] = new WaveTable(reader);
							}
							break;
						}
						case "EDAT":
                        {
							EditData = new EditData(reader);
							break;
                        }
						default:
							throw new FormatException("Invalid SonicArranger module format.");
					}
				}

				if (Samples == null)
				{
					Samples = new Sample[0];
					sampleTable = new SampleTable(Samples);
				}
				if (Waves == null)
					Waves = new WaveTable[0];
				if (AdsrWaves == null)
					AdsrWaves = new WaveTable[0];
				if (AmfWaves == null)
					AmfWaves = new WaveTable[0];
			}
		}

		public SonicArrangerFile(BinaryReader reader)
			: this(new BuiltinReader(reader))
        {

        }

		public SonicArrangerFile(System.IO.Stream stream, bool leaveOpen = false)
			: this(new BinaryReader(stream, Encoding.ASCII, leaveOpen))
		{
		
		}

		private static int FindStart(ICustomReader reader)
		{
			const uint start = 0x00000028;

			try
			{
				uint check = reader.ReadBEUInt32();

				if (check == start)
					return reader.Position - 4;

				while (reader.Position < reader.Size)
				{
					check <<= 8;
					check |= reader.ReadByte();

					if (check == start)
						return reader.Position - 4;
				}

				return -1;
			}
			catch (EndOfStreamException)
			{
				return -1;
			}
		}

		public static SonicArrangerFile Open(string file)
		{
			using (var stream = new FileStream(file, FileMode.Open))
			{
				return new SonicArrangerFile(stream);
			}
		}

		public void Save(BinaryWriter writer, bool editable)
		{
			if (editable)
            {
				void WriteHeader(string header)
                {
					writer.Write(Encoding.ASCII.GetBytes(header));
                }

				WriteHeader("SOAR");

				WriteHeader((Version ?? "").PadRight(4, '\0')[0..4]);

				// Songs
				WriteHeader("STBL");
				songTable.Write(writer);

				// Voices
				WriteHeader("OVTB");
				overTable.Write(writer);

				// Notes
				WriteHeader("NTBL");
				noteTable.Write(writer);

				// Instruments
				WriteHeader("INST");
				instrumentTable.Write(writer);

				// Samples
				WriteHeader("SD8B");
				sampleTable.Write(writer);

				// Synth waves
				WriteHeader("SYWT");
				writer.WriteBEInt32(Waves.Length);
				foreach (var waveTable in Waves)
					waveTable.Write(writer);

				// ADSR waves
				WriteHeader("SYAR");
				writer.WriteBEInt32(AdsrWaves.Length);
				foreach (var waveTable in AdsrWaves)
					waveTable.Write(writer);

				// AMF waves
				WriteHeader("SYAF");
				writer.WriteBEInt32(AmfWaves.Length);
				foreach (var waveTable in AmfWaves)
					waveTable.Write(writer);

				// Edit data
				WriteHeader("EDAT");
				(EditData ?? new EditData()).Write(writer);
			}
			else
            {
				// TODO
				throw new NotImplementedException();
            }
		}

		public void Save(System.IO.Stream stream, bool editable, bool leaveOpen = false)
        {
			using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen))
            {
				Save(writer, editable);
            }
        }

		public void Save(string file, bool editable)
		{
			using (var stream = new FileStream(file, FileMode.Create))
			{
				Save(stream, editable);
			}
		}
	}
}