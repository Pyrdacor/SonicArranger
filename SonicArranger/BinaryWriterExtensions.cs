using System.ComponentModel;
using System.IO;

namespace SonicArranger
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class BinaryWriterExtensions
    {
        public static void WriteBEUInt32(this BinaryWriter writer, uint value)
        {
            writer.Write((byte)((value >> 24) & 0xff));
            writer.Write((byte)((value >> 16) & 0xff));
            writer.Write((byte)((value >> 8) & 0xff));
            writer.Write((byte)(value & 0xff));
        }

        public static void WriteBEInt32(this BinaryWriter writer, int value)
        {
            WriteBEUInt32(writer, unchecked((uint)value));
        }

        public static void WriteBEUInt16(this BinaryWriter writer, ushort value)
        {
            writer.Write((byte)((value >> 8) & 0xff));
            writer.Write((byte)(value & 0xff));
        }

        public static void WriteBEInt16(this BinaryWriter writer, short value)
        {
            WriteBEUInt16(writer, unchecked((ushort)value));
        }
    }
}
