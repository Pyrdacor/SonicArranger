using System;

namespace SonicArranger
{
    /// <summary>
    /// This mimicks the 4 audio channels of the Amiga (Paula) which can be
    /// found at 0xdff0a0, 0xdff0b0, 0xdff0c0 and 0xdff0d0.
    ///
    /// It is documented at http://amiga-dev.wikidot.com/information:hardware.
    ///
    /// Like the hardware, each channel plays a block of data (AUDxLC and
    /// AUDxLEN) with the rate given by the period (AUDxPER): one byte every
    /// period clock cycles, so changing the period changes the speed but
    /// not the position. A location and length written while the channel
    /// plays only take effect when the current block ends (the registers
    /// are latched); the channel then plays the new block, again and again.
    /// Turning the DMA of a channel on starts the block written last from
    /// its beginning.
    ///
    /// The output of a channel is the byte being played (held for the whole
    /// period) times AUDxVOL / 64. Optionally the bytes are interpolated
    /// linearly instead, which sounds smoother but is not what the hardware
    /// does.
    /// </summary>
    internal class PaulaState
    {
        public const int NumTracks = 4;

        public class TrackState
        {
            // the latched location (AUDxLC) and length in bytes (AUDxLEN * 2)
            internal byte[] Data;
            internal int Offset;
            internal int Length;
            /// <summary>
            /// Current period value (AUDxPER).
            /// </summary>
            public int Period { get; set; }
            /// <summary>
            /// Current output volume (AUDxVOL). Values above 64 count as 64.
            /// </summary>
            public int Volume { get; set; }
            internal bool DmaEnabled;
            // the block being played
            internal byte[] CurrentData;
            internal int CurrentOffset;
            internal int CurrentLength;
            internal double Position;
        }

        public readonly TrackState[] Tracks = new TrackState[NumTracks];
        const double palClockFrequency = 3546895.0;
        const double ntscClockFrequency = 3579545.0;
        double clockFrequency = palClockFrequency;
        bool allowLowPassFilter = true;
        bool interpolate = false;
        uint sampleRate = 44100;
        readonly LowPassFilter[] lowPassFilters = new LowPassFilter[4];

        /// <summary>
        /// The master volume of the replayer (0 to 63). It starts with 63.
        /// </summary>
        public int MasterVolume { get; set; } = 63;

        /// <summary>
        /// The power LED, which switches the low pass filter of the Amiga.
        /// The replayer turns it off when a song starts; note command E
        /// switches it.
        /// </summary>
        public bool UseLowPassFilter { get; set; } = false;

        public PaulaState()
        {
            for (int i = 0; i < NumTracks; ++i)
                Tracks[i] = new TrackState();
            for (int i = 0; i < lowPassFilters.Length; ++i)
                lowPassFilters[i] = new LowPassFilter();
        }

        public void Reset(bool allowLowPassFilter, bool pal, bool interpolate, uint sampleRate)
        {
            this.allowLowPassFilter = allowLowPassFilter;
            this.interpolate = interpolate;
            this.sampleRate = sampleRate;
            clockFrequency = pal ? palClockFrequency : ntscClockFrequency;
            MasterVolume = 63;
            UseLowPassFilter = false;

            foreach (var filter in lowPassFilters)
                filter.Reset(sampleRate);

            for (int i = 0; i < NumTracks; ++i)
                Tracks[i] = new TrackState();
        }

        /// <summary>
        /// Writes AUDxLC and AUDxLEN (length in bytes). While the channel
        /// plays, they take effect when the current block ends.
        /// </summary>
        public void SetLocation(int trackIndex, byte[] data, int offset, int length)
        {
            var track = Tracks[trackIndex];
            track.Data = data;
            track.Offset = offset;
            track.Length = length;
        }

        public void StopDma(int trackIndex)
        {
            Tracks[trackIndex].DmaEnabled = false;
        }

        /// <summary>
        /// Turns the DMA of a channel on. If it was off, the channel starts
        /// the block written last.
        /// </summary>
        public void StartDma(int trackIndex)
        {
            var track = Tracks[trackIndex];

            if (track.DmaEnabled)
                return;

            track.DmaEnabled = true;
            LoadBlock(track);
            track.Position = 0.0;
        }

        static void LoadBlock(TrackState track)
        {
            track.CurrentData = track.Data;
            track.CurrentOffset = track.Offset;
            track.CurrentLength = track.Length;
        }

        static int ByteAt(byte[] data, int index)
        {
            return data == null || index < 0 || index >= data.Length ? 0 : unchecked((sbyte)data[index]);
        }

        bool Playing(TrackState track)
        {
            return track.DmaEnabled && track.CurrentLength > 0 && track.Period > 0;
        }

        /// <summary>
        /// The output of a channel (-1 to 1) at its current position.
        /// </summary>
        public double ProcessTrack(int trackIndex)
        {
            var track = Tracks[trackIndex];

            if (!Playing(track))
                return 0.0;

            int index = (int)track.Position;
            double value = ByteAt(track.CurrentData, track.CurrentOffset + index) / 128.0;

            if (interpolate)
            {
                // the next byte: in this block, or the first one of the next block
                double next = index + 1 < track.CurrentLength
                    ? ByteAt(track.CurrentData, track.CurrentOffset + index + 1) / 128.0
                    : ByteAt(track.Data, track.Offset) / 128.0;
                value += (track.Position - index) * (next - value);
            }

            return Math.Min(64, Math.Max(0, track.Volume)) * value / 64.0;
        }

        /// <summary>
        /// Moves the channels on by one output sample.
        /// </summary>
        public void Advance()
        {
            foreach (var track in Tracks)
            {
                if (!Playing(track))
                    continue;

                track.Position += clockFrequency / track.Period / sampleRate;

                // at the end of a block the latched location and length are used
                while (track.Position >= track.CurrentLength)
                {
                    track.Position -= track.CurrentLength;
                    LoadBlock(track);

                    if (track.CurrentLength <= 0)
                    {
                        track.Position = 0.0;
                        break;
                    }
                }
            }
        }

        class LowPassFilter
        {
            // The low pass filter that the power LED switches: a second order
            // (-12 dB/oct) Butterworth filter with a cutoff frequency of about
            // 3.3 kHz.
            const double CutOffFrequency = 3275.0;

            double b0, b1, b2, a1, a2;
            double x1, x2, y1, y2;

            public void Reset(uint sampleRate)
            {
                // bilinear transform with prewarping
                double k = Math.Tan(Math.PI * CutOffFrequency / sampleRate);
                double k2 = k * k;
                double norm = 1.0 / (1.0 + Math.Sqrt(2.0) * k + k2);
                b0 = k2 * norm;
                b1 = 2.0 * b0;
                b2 = b0;
                a1 = 2.0 * (k2 - 1.0) * norm;
                a2 = (1.0 - Math.Sqrt(2.0) * k + k2) * norm;
                x1 = x2 = y1 = y2 = 0.0;
            }

            public double Filter(double value)
            {
                double output = b0 * value + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = value;
                y2 = y1;
                y1 = output;
                return output;
            }
        }

        double Filtered(int filter, double value)
        {
            // the filter always runs, so switching it does not click
            double filtered = lowPassFilters[filter].Filter(value);

            if (allowLowPassFilter && UseLowPassFilter)
                value = filtered;

            return Math.Max(-1.0, Math.Min(1.0, value));
        }

        public double Process()
        {
            double output = 0.0;

            for (int i = 0; i < NumTracks; ++i)
                output += 0.25 * ProcessTrack(i);

            return Filtered(0, output);
        }

        public double ProcessLeftOutput()
        {
            // LRRL
            return Filtered(0, 0.5 * ProcessTrack(0) + 0.5 * ProcessTrack(3));
        }

        public double ProcessRightOutput()
        {
            // LRRL
            return Filtered(1, 0.5 * ProcessTrack(1) + 0.5 * ProcessTrack(2));
        }

        public double ProcessTrackOutput(int trackIndex)
        {
            return Filtered(trackIndex, ProcessTrack(trackIndex));
        }
    }
}
