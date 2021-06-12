using SonicArranger;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SonicConvert
{
    class Program
    {
        static void Usage()
        {
            Console.WriteLine();
            Console.WriteLine($"Usage: {nameof(SonicConvert)} <sapath> <wavpath> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine(" -n: Use NTSC frequency (default: PAL)");
            Console.WriteLine(" -r<n>: Set output sample rate in Hz to n (default: 44100)");
            Console.WriteLine(" -s: Stereo \"LRRL\" (default: mono)");
            Console.WriteLine(" -q: 4 Channel Output (default: mono)");
            Console.WriteLine(" -d: Disable low pass filter (default: active)");
            Console.WriteLine(" -t<n>: Track/song index (default: 0)");
            Console.WriteLine(" -l<n>: Num track loops (default: 0 -> play once)");
            Console.WriteLine();
        }

        static void Main(string[] args)
        {
            string saPath = null;
            string wavPath = null;
            int sampleRate = 44100;
            bool pal = true;
            SonicArranger.Stream.ChannelMode channelMode = SonicArranger.Stream.ChannelMode.Mono;
            bool enableLPF = true;
            int song = 0;
            int numLoops = 0;

            foreach (var arg in args)
            {
                if (arg == "-h" || arg == "--help" || arg == "/?")
                {
                    Console.WriteLine($"Invalid option '{arg}'.");
                    Usage();
                    Environment.Exit(-1);
                    return;
                }    

                if (arg.StartsWith("-"))
                {
                    if (arg.Length < 2)
                    {
                        Console.WriteLine($"Invalid option '{arg}'.");
                        Usage();
                        Environment.Exit(-1);
                        return;
                    }
                    var opt = arg[1];
                    if (opt != 'r' && opt != 't' && opt != 'l' && arg.Length != 2)
                    {
                        Console.WriteLine($"Invalid option '{arg}'.");
                        Usage();
                        Environment.Exit(-1);
                        return;
                    }
                    switch (opt)
                    {
                        case 'n':
                            pal = false;
                            break;
                        case 's':
                            channelMode = SonicArranger.Stream.ChannelMode.Stereo;
                            break;
                        case 'q':
                            channelMode = SonicArranger.Stream.ChannelMode.Quad;
                            break;
                        case 'd':
                            enableLPF = false;
                            break;
                        case 't':
                        {
                            var result = ParseParam();

                            if (result == null)
                            {
                                Console.WriteLine($"Invalid track index option '{arg}'.");
                                Usage();
                                Environment.Exit(-1);
                                return;
                            }

                            song = result.Value;
                            break;
                        }
                        case 'r':
                        {
                            var result = ParseParam();

                            if (result == null)
                            {
                                Console.WriteLine($"Invalid sample rate option '{arg}'.");
                                Usage();
                                Environment.Exit(-1);
                                return;
                            }

                            sampleRate = result.Value;
                            break;
                        }
                        case 'l':
                        {
                            var result = ParseParam();

                            if (result == null)
                            {
                                Console.WriteLine($"Invalid loop amount option '{arg}'.");
                                Usage();
                                Environment.Exit(-1);
                                return;
                            }

                            numLoops = result.Value;
                            break;
                        }
                        default:
                        {
                            Console.WriteLine($"Invalid option '{arg}'.");
                            Usage();
                            Environment.Exit(-1);
                            return;
                        }
                    }

                    int? ParseParam()
                    {
                        var match = Regex.Match(arg.Substring(2), "^[0-9]+$");

                        if (!match.Success)
                            return null;

                        return int.Parse(match.Value);
                    }
                }
                else
                {
                    if (saPath == null)
                        saPath = arg;
                    else if (wavPath == null)
                        wavPath = arg;
                    else
                    {
                        Console.WriteLine("Invalid number of arguments.");
                        Usage();
                        Environment.Exit(-1);
                        return;
                    }
                }
            }

            if (saPath == null || wavPath == null)
            {
                Console.WriteLine("Invalid number of arguments.");
                Usage();
                Environment.Exit(-1);
                return;
            }

            SonicArrangerFile saFile;

            try
            {
                saFile = new SonicArrangerFile(File.OpenRead(saPath));
            }
            catch
            {
                Console.WriteLine("Failed to load sonic arranger file.");
                Environment.Exit(1);
                return;
            }

            if (song < 0 || song >= saFile.Songs.Length)
            {
                Console.WriteLine($"Song index {song} is not valid or present in the SA file.");
                Environment.Exit(1);
                return;
            }

            SonicArranger.Stream saStream;

            try
            {
                saStream = new SonicArranger.Stream(saFile, song, (uint)sampleRate, channelMode, enableLPF, pal);
            }
            catch
            {
                Console.WriteLine("Error while processing SA file with given options.");
                Environment.Exit(2);
                return;
            }

            System.IO.Stream outFile;

            try
            {
                outFile = File.Create(wavPath);
            }
            catch
            {
                Console.WriteLine("Error creating output file.");
                Environment.Exit(3);
                return;
            }

            try
            {
                WriteWave(outFile, saStream.ToSignedArray(numLoops), (uint)sampleRate, channelMode);
            }
            catch
            {
                Console.WriteLine("Error writing data as WAV to output file.");
                Environment.Exit(4);
                return;
            }
            finally
            {
                outFile.Close();
            }

            Console.WriteLine("Successfully converted SA file to:");
            Console.WriteLine(" -> " + wavPath);
            Environment.Exit(0);
        }

        static void WriteWave(System.IO.Stream stream, byte[] data, uint sampleRate, SonicArranger.Stream.ChannelMode channelMode)
        {
            uint numsamples = (uint)data.Length;
            ushort numchannels = (ushort)channelMode;
            ushort samplelength = 1; // in bytes

            using var wr = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);

            wr.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            wr.Write(38 + numsamples * numchannels * samplelength);
            wr.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            wr.Write(16);
            wr.Write((ushort)1);
            wr.Write(numchannels);
            wr.Write(sampleRate);
            wr.Write(sampleRate * samplelength * numchannels);
            wr.Write((ushort)(samplelength * numchannels));
            wr.Write((ushort)(8 * samplelength));
            wr.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            wr.Write(numsamples * samplelength * numchannels);

            for (int i = 0; i < numsamples; i++)
            {
                wr.Write((byte)((data[i] + (samplelength == 1 ? 128 : 0)) & 0xff));
            }
        }
    }
}
