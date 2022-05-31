namespace SonicArranger
{
	public class SampleTable
	{
		public int Count { get; private set; }
		public Sample[] Samples { get; private set; }

		internal SampleTable(ICustomReader reader)
		{
			Count = reader.ReadBEInt32();
			if (Count > 0)
			{
				var sampleSizes = new int[Count];
				for (int i = 0; i < Count; ++i)
					sampleSizes[i] = reader.ReadBEInt32();
				Samples = new Sample[Count];
				for (int i = 0; i < Count; i++)
					Samples[i] = new Sample(reader, sampleSizes[i]);
			}
		}

		internal SampleTable(Sample[] samples)
		{
			Count = samples.Length;
			Samples = samples;
		}

		internal void Write(System.IO.BinaryWriter writer)
		{
			writer.WriteBEInt32(Count);

			foreach (var sample in Samples)
				writer.WriteBEInt32(sample.Data?.Length ?? 0);
			foreach (var sample in Samples)
			{
				if (sample.Data != null && sample.Data.Length != 0)
					writer.Write(sample.Data);
			}
		}
	}
}
