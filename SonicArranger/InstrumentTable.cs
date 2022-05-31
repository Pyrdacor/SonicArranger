namespace SonicArranger
{
	public class InstrumentTable
	{
		public int Count { get; private set; }
		public Instrument[] Instruments { get; private set; }

		internal InstrumentTable(ICustomReader reader)
		{
			Count = reader.ReadBEInt32();
			Instruments = new Instrument[Count];
			for (int i = 0; i < Count; i++)
			{
				Instruments[i] = new Instrument(reader);
			}
		}

		internal InstrumentTable(Instrument[] instruments)
		{
			Count = instruments.Length;
			Instruments = instruments;
		}

		internal void Write(System.IO.BinaryWriter writer)
		{
			writer.WriteBEInt32(Count);

			foreach (var instrument in Instruments)
			{
				instrument.Write(writer);
			}
		}
	}
}
