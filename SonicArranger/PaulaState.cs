﻿using System;

namespace SonicArranger
{
    /// <summary>
    /// This mimicks the audio channel data of the Amiga
    /// which can be found at 0xdff0a0, 0xdff0b0, 0xdff0c0
    /// and 0xdff0d0 for the 4 audio channels.
    /// 
    /// It is documented at http://amiga-dev.wikidot.com/information:hardware.
    /// </summary>
    internal class PaulaState
    {
        public const int NumTracks = 4;

        public class TrackState
        {
            byte[] data = null;
            int dataIndex = 0;

            public bool DataChanged { get; set; } = false;

            /// <summary>
            /// Source data for playback.
            /// 
            /// This mimicks the audio channel location bits at
            /// AUDxLCH and AUDxLCL.
            /// </summary>
            public byte[] Data
            {
                get => data;
                set
                {
                    if (data != value)
                    {
                        data = value;
                        DataChanged = true;
                    }
                }
            }
            /// <summary>
            /// Length of the data to use.
            /// 
            /// This mimicks the audio channel length at AUDxLEN.
            /// </summary>
            public int Length { get; set; }
            /// <summary>
            /// Current period value (note frequency).
            /// 
            /// This mimicks the audio channel period at AUDxPER
            /// </summary>
            public int Period { get; set; }
            /// <summary>
            /// Current output volume.
            /// 
            /// This mimicks the audio channel volume at AUDxVOL
            /// </summary>
            public int Volume { get; set; }
            /// <summary>
            /// The current data index into the source data.
            /// 
            /// This together with <see cref="Data"/> is used
            /// for playback by replacing the DMA audio controller
            /// which fills AUDxDAT automatically.
            /// </summary>
            public int DataIndex
            {
                get => dataIndex;
                set
                {
                    if (dataIndex != value)
                    {
                        dataIndex = value;
                        DataChanged = true;
                    }
                }
            }
        }

        class CurrentTrackState
        {
            public byte[] Data { get; set; }
            public double StartPlayTime { get; set; }
        }

        public interface ICurrentSample
        {
            sbyte Sample { get; set; }
            int Index { get; }
            int Length { get; }
            sbyte this[int index] { get; set; }
            byte[] CopyTarget { get; }
        }

        class CurrentSample : ICurrentSample
        {
            readonly CurrentTrackState currentTrackState;

            public CurrentSample(CurrentTrackState currentTrackState)
            {
                this.currentTrackState = currentTrackState;
            }

            public int Index { get; set; } = 0;
            public int NextIndex => Index == Length - 1 ? 0 : Index + 1;
            public double Gamma { get; set; } = 0.0;
            public int Length => currentTrackState.Data?.Length ?? 0;
            public byte[] CopyTarget => currentTrackState.Data;

            public sbyte Sample
            {
                get => this[Index];
                set => this[Index] = value;
            }

            public sbyte this[int index]
            {
                get => currentTrackState.Data == null || index >= currentTrackState.Data.Length
                    ? (sbyte)0 : unchecked((sbyte)currentTrackState.Data[index]);
                set
                {
                    if (currentTrackState.Data != null && index < currentTrackState.Data.Length)
                        currentTrackState.Data[index] = unchecked((byte)value);
                }
            }
        }

        public delegate void TrackFinishedHandler(int trackIndex, double currentPlayTime);
        event TrackFinishedHandler track1Finished;
        event TrackFinishedHandler track2Finished;
        event TrackFinishedHandler track3Finished;
        event TrackFinishedHandler track4Finished;
        public readonly TrackState[] Tracks = new TrackState[NumTracks];
        readonly CurrentTrackState[] currentTrackStates = new CurrentTrackState[NumTracks];
        readonly CurrentSample[] currentSamples = new CurrentSample[4];
        public ICurrentSample[] CurrentSamples => currentSamples;
        const double palClockFrequency = 7093789.2;
        const double ntscClockFrequency = 7159090.5;
        double clockFrequency = palClockFrequency;
        int masterVolume = 64;
        public int MasterVolume
        {
            get => masterVolume;
            set => masterVolume = Math.Max(0, Math.Min(value, 64));
        }
        public bool UseLowPassFilter
        {
            get;
            set;
        } = true;
        bool allowLowPassFilter = true;
        readonly LowPassFilter[] lowPassFilters = new LowPassFilter[4];

        public PaulaState()
        {
            for (int i = 0; i < NumTracks; ++i)
            {
                Tracks[i] = new TrackState();
                currentTrackStates[i] = new CurrentTrackState();
                currentSamples[i] = new CurrentSample(currentTrackStates[i]);
            }
            for (int i = 0; i < lowPassFilters.Length; ++i)
            {
                lowPassFilters[i] = new LowPassFilter();
            }
        }

        public void Reset(bool allowLowPassFilter, bool pal)
        {
            this.allowLowPassFilter = allowLowPassFilter;
            clockFrequency = pal ? palClockFrequency : ntscClockFrequency;

            for (int i = 0; i < lowPassFilters.Length; ++i)
            {
                lowPassFilters[i].Reset();
            }

            for (int i = 0; i < NumTracks; ++i)
            {
                var track = Tracks[i];
                track.Data = null;
                track.Length = 0;
                track.Period = 0;
                track.Volume = 0;
                track.DataIndex = 0;

                var trackState = currentTrackStates[i];
                trackState.Data = null;
                trackState.StartPlayTime = 0.0;

                currentSamples[i].Index = 0;
                currentSamples[i].Gamma = 0.0;
            }
        }

        public void StopTrack(int trackIndex)
        {
            if (trackIndex < 0 || trackIndex > NumTracks)
                throw new IndexOutOfRangeException("Invalid track index.");

            var track = Tracks[trackIndex];
            var trackState = currentTrackStates[trackIndex];

            track.Data = null;
            trackState.Data = null;
            currentSamples[trackIndex].Index = 0;
            currentSamples[trackIndex].Gamma = 0;
        }

        public void StartTrackData(int trackIndex, double currentPlayTime)
        {
            if (trackIndex < 0 || trackIndex > NumTracks)
                throw new IndexOutOfRangeException("Invalid track index.");

            var track = Tracks[trackIndex];
            var trackState = currentTrackStates[trackIndex];

            if (track.DataChanged)
            {
                int size = track.Data.Length - track.DataIndex;

                if (size <= 0)
                    throw new ArgumentOutOfRangeException("Track data index must be less than the data size.");

                trackState.Data = new byte[size];
                Buffer.BlockCopy(track.Data, track.DataIndex, trackState.Data, 0, size);
                track.DataChanged = false;
            }

            trackState.StartPlayTime = currentPlayTime;
            currentSamples[trackIndex].Index = 0;
            currentSamples[trackIndex].Gamma = 0;
        }

        class LowPassFilter
        {
            // According to Amiga hardware manual the hardward audio low-pass filter
            // starts decreasing frequencies starting at 4kHz and won't let any
            // frequencies above 7kHz through. But more precisely it's a -12dB/oct
            // (second order) Butterworth low-pass filter with a cutoff frequency of
            // around 3.3kHz.
            //
            // Amiga 1200       | 6 dB/oct    | 28867 Hz , As is
            // Amiga 1200 (LED) | 2x12 dB/oct | 3275 Hz , Butterworth
            //
            // | 2nd-order Butterworth s-domain coefficients are: |
            // |                                                  |
            // | b0 = 1.0  b1 = 0        b2 = 0                   |
            // | a0 = 1    a1 = sqrt(2)  a2 = 1                   |
            // |                                                  |
            const double CutOffFrequency = 3275.0; // 3.275kHz


            double[] lastInputs = new double[2];
            double[] lastOutputs = new double[2];
            const double b0 = 1.0;
            const double a0 = 1.0;
            const double a1 = 1.41421;
            const double a2 = 1.0;
            const double omega0 = 2.0 * Math.PI * CutOffFrequency / 44100.0;
            readonly double q, r, s, t, u;

            public LowPassFilter()
            {
                double k = omega0 / Math.Tan(0.5 * omega0 * 44100.0);
                double k2 = k * k;
                double d = a0 * k2 + a1 * k + a2;

                q = (b0 * k2) / d;
                r = (-2.0 * b0 * k2) / d;
                s = q;
                t = (2.0 * a2 - 2.0 * a0 * k2) / d;
                u = (a0 * k2 - a1 * k + a2) / d;
            }

            public void Reset()
            {
                lastInputs[0] = 0;
                lastInputs[1] = 0;
                lastOutputs[0] = 0;
                lastOutputs[1] = 0;
            }

            public double Filter(double value)
            {
                double output = q * value + r * lastInputs[1] + s * lastInputs[0] - t * lastOutputs[1] - u * lastOutputs[0];

                lastInputs[0] = lastInputs[1];
                lastInputs[1] = value;
                lastOutputs[0] = lastOutputs[1];
                lastOutputs[1] = output;

                return output;
            }
        }

        public double ProcessTrack(int trackIndex, double currentPlaybackTime)
        {
            if (trackIndex < 0 || trackIndex > NumTracks)
                throw new IndexOutOfRangeException("Invalid track index.");

            var data = currentTrackStates[trackIndex].Data;

            if (data == null || Tracks[trackIndex].Period < 1)
                return 0.0;

            var currentSample = currentSamples[trackIndex];
            double leftValue = unchecked((sbyte)data[currentSample.Index]) / 128.0;
            double rightValue = unchecked((sbyte)data[currentSample.NextIndex]) / 128.0;

            return Tracks[trackIndex].Volume * (leftValue + currentSample.Gamma * (rightValue - leftValue)) / 64.0;
        }

        public void UpdateCurrentSample(int trackIndex, double currentPlaybackTime)
        {
            if (trackIndex < 0 || trackIndex > NumTracks)
                throw new IndexOutOfRangeException("Invalid track index.");

            var trackState = currentTrackStates[trackIndex];
            var currentSample = currentSamples[trackIndex];

            if (trackState.Data == null || trackState.StartPlayTime > currentPlaybackTime)
            {
                currentSample.Index = 0;
                currentSample.Gamma = 0.0;
                return;
            }

            var period = Tracks[trackIndex].Period;

            if (period < 0.01)
            {
                currentSample.Index = 0;
                currentSample.Gamma = 0.0;
                return;
            }

            double samplesPerSecond = clockFrequency / (2.0 * period);
            double trackTime = currentPlaybackTime - trackState.StartPlayTime;
            double index = samplesPerSecond * trackTime;

            var data = trackState.Data;
            int leftIndex = (int)index;

            if (leftIndex >= data.Length)
            {
                InvokeTrackFinishHandler(trackIndex, currentPlaybackTime);

                if (trackState.Data == null)
                {
                    currentSample.Index = 0;
                    currentSample.Gamma = 0.0;
                    return;
                }

                index -= data.Length;
                leftIndex = 0;
                trackState.StartPlayTime = currentPlaybackTime;
            }

            currentSample.Index = leftIndex;
            currentSample.Gamma = index - leftIndex;
        }

        void InvokeTrackFinishHandler(int trackIndex, double currentPlaybackTime)
        {
            switch (trackIndex)
            {
                case 0:
                    track1Finished?.Invoke(trackIndex, currentPlaybackTime);
                    break;
                case 1:
                    track2Finished?.Invoke(trackIndex, currentPlaybackTime);
                    break;
                case 2:
                    track3Finished?.Invoke(trackIndex, currentPlaybackTime);
                    break;
                case 3:
                    track4Finished?.Invoke(trackIndex, currentPlaybackTime);
                    break;
            }
        }

        public void AttachTrackFinishHandler(int trackIndex, TrackFinishedHandler handler)
        {
            switch (trackIndex)
            {
                case 0:
                    track1Finished = null;
                    track1Finished += handler;
                    break;
                case 1:
                    track2Finished = null;
                    track2Finished += handler;
                    break;
                case 2:
                    track3Finished = null;
                    track3Finished += handler;
                    break;
                case 3:
                    track4Finished = null;
                    track4Finished += handler;
                    break;
            }
        }

        public double Process(double currentPlaybackTime)
        {
            double output = 0.0;

            for (int i = 0; i < NumTracks; ++i)
                output += 0.25 * ProcessTrack(i, currentPlaybackTime);

            if (allowLowPassFilter && UseLowPassFilter)
                output = lowPassFilters[0].Filter(output);

            return Math.Max(-1.0, Math.Min(1.0, output));
        }

        public double ProcessLeftOutput(double currentPlaybackTime)
        {
            double output = 0.0;

            // LRRL
            output += 0.5 * ProcessTrack(0, currentPlaybackTime);
            output += 0.5 * ProcessTrack(3, currentPlaybackTime);

            if (allowLowPassFilter && UseLowPassFilter)
                output = lowPassFilters[0].Filter(output);

            return Math.Max(-1.0, Math.Min(1.0, output));
        }

        public double ProcessRightOutput(double currentPlaybackTime)
        {
            double output = 0.0;

            // LRRL
            output += 0.5 * ProcessTrack(1, currentPlaybackTime);
            output += 0.5 * ProcessTrack(2, currentPlaybackTime);

            if (allowLowPassFilter && UseLowPassFilter)
                output = lowPassFilters[1].Filter(output);

            return Math.Max(-1.0, Math.Min(1.0, output));
        }

        public double ProcessTrackOutput(int trackIndex, double currentPlaybackTime)
        {
            double output = ProcessTrack(trackIndex, currentPlaybackTime);

            if (allowLowPassFilter && UseLowPassFilter)
                output = lowPassFilters[trackIndex].Filter(output);

            return Math.Max(-1.0, Math.Min(1.0, output));
        }
    }
}
