﻿using System.Text;

namespace SonicArranger
{
	public struct Instrument
	{
		public enum Effect
        {
			NoEffect,
			/// <summary>
			/// Effect1: -
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			WaveNegator,
			/// <summary>
			/// Effect1: EffWave
			/// Effect2: WaveLen
			/// Effect3: WaveRept
			/// </summary>
			FreeNegator,
			/// <summary>
			/// Effect1: DeltaVal
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			RotateVertical,
			/// <summary>
			/// Effect1: -
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			RotateHorizontal,
			/// <summary>
			/// Effect1: EffWave
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			AlienVoice,
			/// <summary>
			/// Effect1: -
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			PolyNegator,
			/// <summary>
			/// Effect1: EffWave
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			ShackWave1,
			/// <summary>
			/// Effect1: EffWave
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			ShackWave2,
			/// <summary>
			/// Effect1: DestWave
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			Metawdrpk,
			/// <summary>
			/// Effect1: -
			/// Effect2: Detune
			/// Effect3: Repeats
			/// </summary>
			LaserAmf,
			/// <summary>
			/// Effect1: DeltaVal
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			WaveAlias,
			/// <summary>
			/// Effect1: -
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			NoiseGenerator,
			/// <summary>
			/// Effect1: DeltaVal
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			LowPassFilter1,
			/// <summary>
			/// Effect1: EffWave
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			LowPassFilter2,
			/// <summary>
			/// Effect1: DestWave
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			Oscillator1,
			/// <summary>
			/// Effect1: -
			/// Effect2: StartPnt
			/// Effect3: StopPnt
			/// </summary>
			NoiseGenerator2,
			/// <summary>
			/// Effect1: Level
			/// Effect2: Factor
			/// Effect3: Repeats
			/// </summary>
			FMDrum
		}

		/// <summary>
		/// Synthetic mode (off/on).
		/// </summary>
		public bool SynthMode { get; private set; }
		/// <summary>
		/// 0-based sample or wave index (dependent on <see cref="SynthMode"/>).
		/// </summary>
		public short SampleWaveNo { get; private set; }
		/// <summary>
		/// Length in words (max 64 for synthetic instruments).
		/// </summary>
		public short Length { get; private set; }
		/// <summary>
		/// Repeat size in words.
		/// 
		/// Always 0 for synthetic instruments.
		/// 
		/// Note: The total size in bytes for sampled instruments is
		/// <see cref="Length"/> * 2 + <see cref="Repeat"/> * 2.
		/// 
		/// The special case Repeat=1 seems to be used to disable
		/// repeating and looping. So if it is set, the playback
		/// will stop after the first cycle and then will only
		/// output zeros (no sound).
		/// 
		/// If repeat is 0, the whole sound is looped from offset
		/// 0 to Length*2. This is the reason why synth waves always
		/// have this set to 0.
		/// </summary>
		public short Repeat { get; private set; }
		/// <summary>
		/// Unknown 8 bytes
		/// </summary>
		public byte[] Unknown8Bytes { get; private set; }
		/// <summary>
		/// Volume level (0 to 64).
		/// </summary>
		public short Volume { get; private set; }
		/// <summary>
		/// 0 to 255.
		/// </summary>
		public short FineTuning { get; private set; }
		public short Portamento { get; private set; }
		/// <summary>
		/// 255 (or -1) means no vibrato (default).
		/// 
		/// Range can thus be 0 to 254.
		/// </summary>
		public short VibDelay { get; private set; }
		/// <summary>
		/// Default value is 18 (0x12). Range is 0 to 255.
		/// </summary>
		public short VibSpeed { get; private set; }
		/// <summary>
		/// Default value is 160 (0xA0). Range is 0 to 255.
		/// </summary>
		public short VibLevel { get; private set; }
		/// <summary>
		/// 0-based AMF wave index.
		/// </summary>
		public short AmfWave { get; private set; }
		/// <summary>
		/// Default value is 1. Range is 1 to 255.
		/// </summary>
		public short AmfDelay { get; private set; }
		/// <summary>
		/// Size of AMF wave data in bytes.
		/// 
		/// Note: The total size is <see cref="AmfLength"/> + <see cref="AmfRepeat"/>.
		/// </summary>
		public short AmfLength { get; private set; }
		/// <summary>
		/// Size of the repeat portion of the AMF wave data in bytes.
		/// 
		/// Note: The total size is <see cref="AmfLength"/> + <see cref="AmfRepeat"/>.
		/// </summary>
		public short AmfRepeat { get; private set; }
		/// <summary>
		/// 0-based ADSR wave index.
		/// </summary>
		public short AdsrWave { get; private set; }
		/// <summary>
		/// Default value is 1. Range is 1 to 255.
		/// </summary>
		public short AdsrDelay { get; private set; }
		/// <summary>
		/// Size of ADSR wave data in bytes.
		/// 
		/// Note: The total size is <see cref="AdsrLength"/> + <see cref="AdsrRepeat"/>.
		/// </summary>
		public short AdsrLength { get; private set; }
		/// <summary>
		/// Size of the repeat portion of the ADSR wave data in bytes.
		/// 
		/// Note: The total size is <see cref="AdsrLength"/> + <see cref="AdsrRepeat"/>.
		/// </summary>
		public short AdsrRepeat { get; private set; }
		/// <summary>
		/// The 0-based index of the ADSR wave data byte
		/// to use as the sustain.
		/// </summary>
		public short SustainPt { get; private set; }
		public short SustainVal { get; private set; }
		/// <summary>
		/// Unknown 16 bytes
		/// </summary>
		public byte[] Unknown16Bytes { get; private set; }
		public Effect EffectNumber { get; private set; }
		public short Effect1 { get; private set; }
		public short Effect2 { get; private set; }
		public short Effect3 { get; private set; }
		public short EffectDelay { get; private set; }

		public Arpeggiato[] ArpegData { get; private set; }

		public string Name { get; private set; }

		internal Instrument(ICustomReader reader) : this()
		{
			SynthMode = reader.ReadBEInt16() != 0;
			SampleWaveNo = reader.ReadBEInt16();
			Length = reader.ReadBEInt16();
			Repeat = reader.ReadBEInt16();
			Unknown8Bytes = reader.ReadBytes(8); // TODO
			Volume = reader.ReadBEInt16();
			FineTuning = reader.ReadBEInt16();
			Portamento = reader.ReadBEInt16();
			VibDelay = reader.ReadBEInt16();
			if (VibDelay >= 255)
				VibDelay = -1;
			VibSpeed = reader.ReadBEInt16();
			VibLevel = reader.ReadBEInt16();
			AmfWave = reader.ReadBEInt16();
			AmfDelay = reader.ReadBEInt16();
			AmfLength = reader.ReadBEInt16();
			AmfRepeat = reader.ReadBEInt16();
			AdsrWave = reader.ReadBEInt16();
			AdsrDelay = reader.ReadBEInt16();
			AdsrLength = reader.ReadBEInt16();
			AdsrRepeat = reader.ReadBEInt16();
			SustainPt = reader.ReadBEInt16();
			SustainVal = reader.ReadBEInt16();
			Unknown16Bytes = reader.ReadBytes(16); // TODO
			Effect1 = reader.ReadBEInt16();
			EffectNumber = (Effect)reader.ReadBEInt16();
			Effect2 = reader.ReadBEInt16();
			Effect3 = reader.ReadBEInt16();
			EffectDelay = reader.ReadBEInt16();

			ArpegData = new Arpeggiato[3];
			for (int i = 0; i < ArpegData.Length; i++)
			{
				ArpegData[i] = new Arpeggiato(reader);
			}
			Name = new string(reader.ReadChars(30)).Split(new[] { '\0' }, 2)[0];
		}

		internal void Write(System.IO.BinaryWriter writer)
		{
			writer.WriteBEUInt16((ushort)(SynthMode ? 1 : 0));
			writer.WriteBEInt16(SampleWaveNo);
			writer.WriteBEInt16(Length);
			writer.WriteBEInt16(Repeat);
			writer.Write(Unknown8Bytes);
			writer.WriteBEInt16(Volume);
			writer.WriteBEInt16(FineTuning);
			writer.WriteBEInt16(Portamento);
			writer.WriteBEInt16(VibDelay);
			writer.WriteBEInt16(VibSpeed);
			writer.WriteBEInt16(VibLevel);
			writer.WriteBEInt16(AmfWave);
			writer.WriteBEInt16(AmfDelay);
			writer.WriteBEInt16(AmfLength);
			writer.WriteBEInt16(AmfRepeat);
			writer.WriteBEInt16(AdsrWave);
			writer.WriteBEInt16(AdsrDelay);
			writer.WriteBEInt16(AdsrLength);
			writer.WriteBEInt16(AdsrRepeat);
			writer.WriteBEInt16(SustainPt);
			writer.WriteBEInt16(SustainVal);
			writer.Write(Unknown16Bytes);
			writer.WriteBEInt16(Effect1);
			writer.WriteBEInt16((short)EffectNumber);
			writer.WriteBEInt16(Effect2);
			writer.WriteBEInt16(Effect3);
			writer.WriteBEInt16(EffectDelay);

			foreach (var arp in ArpegData)
				arp.Write(writer);

			writer.Write(Encoding.ASCII.GetBytes((Name ?? "<unknown>").PadRight(30, '\0')[0..30]));
		}

		public override string ToString()
		{
			return Name;
		}
	}
}
