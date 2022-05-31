using System.Text;

namespace SonicArranger
{
	public class SampleTable
	{
		public int Count { get; private set; }
		public Sample[] Samples { get; private set; }
		private readonly int[] sampleLengths;
		private readonly int[] sampleRepeats;
		private readonly string[] sampleNames;

		internal SampleTable(ICustomReader reader, bool editable)
		{
			Count = reader.ReadBEInt32();
			if (Count > 0)
			{
				Samples = new Sample[Count];

				if (editable)
				{
					sampleLengths = new int[Count];
					for (int i = 0; i < Count; ++i)
						sampleLengths[i] = reader.ReadBEInt32();
					sampleRepeats = new int[Count];
					for (int i = 0; i < Count; ++i)
						sampleRepeats[i] = reader.ReadBEInt32();
					sampleNames = new string[Count];
					for (int i = 0; i < Count; ++i)
						sampleNames[i] = new string(reader.ReadChars(30));
					var sampleSizes = new int[Count];
					for (int i = 0; i < Count; ++i)
						sampleSizes[i] = reader.ReadBEInt32();
					for (int i = 0; i < Count; i++)
						Samples[i] = new Sample(reader, sampleSizes[i]);
				}
				else
                {
					var sampleSizes = new int[Count];
					for (int i = 0; i < Count; ++i)
						sampleSizes[i] = reader.ReadBEInt32();
					for (int i = 0; i < Count; i++)
						Samples[i] = new Sample(reader, sampleSizes[i]);
				}
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

			if (Count == 0)
				return;

			if (sampleLengths != null)
			{
				foreach (var sampleLength in sampleLengths)
					writer.WriteBEInt32(sampleLength);
			}
			else
			{
				foreach (var sample in Samples)
					writer.WriteBEInt32((sample.Data?.Length ?? 0) / 2);
			}

			if (sampleRepeats != null)
			{
				foreach (var sampleRepeat in sampleRepeats)
					writer.WriteBEInt32(sampleRepeat);
			}
			else
			{
				for (int s = 0; s < Samples.Length; ++s)
					writer.WriteBEInt32(1);
			}

			if (sampleNames != null)
			{
				foreach (var sampleName in sampleNames)
					writer.Write(Encoding.ASCII.GetBytes(sampleName.PadRight(30, '\0')[0..30]));
			}
			else
			{
				var nameBytes = Encoding.ASCII.GetBytes("--blank--".PadRight(30, '\0'));

				for (int s = 0; s < Samples.Length; ++s)
					writer.Write(nameBytes);
			}

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
