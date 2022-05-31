using System.Text;

namespace SonicArranger
{
    /// <summary>
    /// Editor data.
    /// 
    /// There seems to be a bug with the voice states.
    /// There are two 16-bit words. The first stores
    /// the enable state of Voice 1, the second that
    /// of Voice 2. But the states for Voice 3 and 4
    /// are missing. I guess it was planned to use
    /// the 4 bytes for the 4 voices but then used
    /// words. Neither saving nor loading will consider
    /// voices 3 or 4.
    /// </summary>
    public class EditData
    {
        public string Version { get; set; }
        /// <summary>
        /// Voice 1 state (on/off).
        /// </summary>
        public bool EnableVoice1 { get; set; }
        /// <summary>
        /// Voice 2 state (on/off).
        /// </summary>
        public bool EnableVoice2 { get; set; }
        /// <summary>
        /// Voice 3 state (on/off).
        /// </summary>
        public bool EnableVoice3 { get; set; }
        /// <summary>
        /// Voice 4 state (on/off).
        /// </summary>
        public bool EnableVoice4 { get; set; }
        /// <summary>
        /// Currently selected play position (pattern editor).
        /// </summary>
        public short PlayPosition { get; set; }
        /// <summary>
        /// Currently selected song position (row index).
        /// This is also the edit position in the pattern editor.
        /// </summary>
        public short SelectedPosition { get; set; }
        /// <summary>
        /// Currently selected song. In contrast to
        /// the UI this is 0-based so the first song
        /// is 0, the second is 1, etc.
        /// </summary>
        public short SelectedSong { get; set; }
        public short Unknown { get; set; }
        /// <summary>
        /// Current column (voice) in pattern editor.
        /// </summary>
        public short PatternEditorVoice { get; set; }
        /// <summary>
        /// Current row in pattern editor.
        /// </summary>
        public short PatternEditorRow { get; set; }

        internal EditData(string version = null)
        {
            Version = version?.PadRight(4, '\0') ?? "V1.1";
            EnableVoice1 = true;
            EnableVoice2 = true;
            EnableVoice3 = true;
            EnableVoice4 = true;
            PlayPosition = 0;
            SelectedPosition = 0;
            SelectedSong = 0;
            Unknown = 0;
            PatternEditorVoice = 0;
            PatternEditorRow = 0;
        }

        internal EditData(ICustomReader reader)
        {
            Version = new string(reader.ReadChars(4));
            EnableVoice1 = reader.ReadByte() != 0;
            EnableVoice2 = reader.ReadByte() != 0;
            EnableVoice3 = reader.ReadByte() != 0;
            EnableVoice4 = reader.ReadByte() != 0;
            PlayPosition = reader.ReadBEInt16();
            SelectedPosition = reader.ReadBEInt16();
            SelectedSong = reader.ReadBEInt16();
            Unknown = reader.ReadBEInt16();
            PatternEditorVoice = reader.ReadBEInt16();
            PatternEditorRow = reader.ReadBEInt16();
        }

        internal void Write(System.IO.BinaryWriter writer)
        {
            writer.Write(Encoding.ASCII.GetBytes((Version ?? "").PadRight(4, '\0')[0..4]));
            writer.Write((byte)(EnableVoice1 ? 1 : 0));
            writer.Write((byte)(EnableVoice2 ? 1 : 0));
            writer.Write((byte)(EnableVoice3 ? 1 : 0));
            writer.Write((byte)(EnableVoice4 ? 1 : 0));
            writer.WriteBEInt16(PlayPosition);
            writer.WriteBEInt16(SelectedPosition);
            writer.WriteBEInt16(SelectedSong);
            writer.WriteBEInt16(Unknown);
            writer.WriteBEInt16(PatternEditorVoice);
            writer.WriteBEInt16(PatternEditorRow);
        }
    }
}
