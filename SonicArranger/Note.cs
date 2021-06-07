namespace SonicArranger
{
	public struct Note
	{
		public enum NoteCommand
        {
			None = 0x0,
			SlideUp = 0x1,
			SetADSRIndex = 0x2,
			Unused = 0x3,
			ResetVibrato = 0x4,
			SetVibrato = 0x5,
			SetMasterVolume = 0x6,
			SetPortamento = 0x7,
			ClearPortamento = 0x8,
			Unknown9 = 0x9,
			VolumeSlide = 0xa,
			PositionJump = 0xb,
			SetVolume = 0xc,
			PatternBreak = 0xd,
			DisableHardwareLPF = 0xe,
			SetSpeed = 0xf
		}

		public byte Value { get; private set; }
		public byte Instrument { get; private set; }
		public NoteCommand Command { get; private set; }
		public int ArpeggioIndex { get; private set; }
		public byte CommandInfo { get; private set; }
		/// <summary>
		/// If set the note's instrument can't be changed
		/// by a voice's sound transpose.
		/// </summary>
		public bool DisableSoundTranspose { get; private set; }
		/// <summary>
		/// If set the note's value can't be changed
		/// by a voice's note transpose.
		/// </summary>
		public bool DisableNoteTranspose { get; private set; }

		internal Note(ICustomReader reader) : this()
		{
			Value = reader.ReadByte();
			Instrument = reader.ReadByte();
			var flagsAndCommand = reader.ReadByte();
			DisableSoundTranspose = (flagsAndCommand & 0x80) != 0;
			DisableNoteTranspose = (flagsAndCommand & 0x40) != 0;
			ArpeggioIndex = (flagsAndCommand >> 4) & 0x3;
			Command = (NoteCommand)(flagsAndCommand & 0xf);
			CommandInfo = reader.ReadByte();
		}
	}
}
