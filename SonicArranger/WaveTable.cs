namespace SonicArranger
{
    public struct WaveTable
    {
        public byte[] Data { get; private set; }

        internal WaveTable(ICustomReader reader) : this()
        {
            Data = reader.ReadBytes(128);
        }

        internal void Write(System.IO.BinaryWriter writer)
        {
            writer.Write(Data);
        }
    }
}
