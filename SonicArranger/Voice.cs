namespace SonicArranger
{
	public struct Voice
	{
		public short NoteAddress { get; private set; }
		public sbyte SoundTranspose { get; private set; }
		public sbyte NoteTranspose { get; private set; }

		internal Voice(ICustomReader reader) : this()
		{
			NoteAddress = reader.ReadBEInt16();
			SoundTranspose = unchecked((sbyte)reader.ReadByte());
			NoteTranspose = unchecked((sbyte)reader.ReadByte());
		}

		internal void Write(System.IO.BinaryWriter writer)
		{
			writer.WriteBEInt16(NoteAddress);
			writer.Write(unchecked((byte)SoundTranspose));
			writer.Write(unchecked((byte)NoteTranspose));
		}
	}
}
