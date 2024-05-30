using NAudio.Lame;
using NAudio.Wave;
using SonicArranger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace SonicConvert
{
    class Program
    {
        static void Usage()
        {
            Console.WriteLine();
            Console.WriteLine($"Usage: {nameof(SonicConvert)} <sapath> <outpath> [options]");
            Console.WriteLine("        Converts a sonic arranger file to wav or mp3.");
			Console.WriteLine($"Usage: {nameof(SonicConvert)} mp3s <sadir> <outdir> [options]");
			Console.WriteLine("        Converts many sonic arranger files to mp3s.");
			Console.WriteLine($"Usage: {nameof(SonicConvert)} dec <sapath> <soarpath>");
            Console.WriteLine("        Converts a sonic arranger file to the editable version (soar).");
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
            if (args.Length == 3 && args[0].ToLower() == "dec")
            {
                SonicArrangerFile saFile;

                try
                {
                    saFile = new SonicArrangerFile(File.OpenRead(args[1]));
                }
                catch
                {
                    Console.WriteLine("Failed to load sonic arranger file.");
                    Environment.Exit(1);
                    return;
                }

                try
                {
                    saFile.Save(args[2], true);
                }
                catch
                {
                    Console.WriteLine("Failed to save sonic arranger file.");
                    Environment.Exit(1);
                    return;
                }
            }
            else if (args.Length == 3 && args[0].ToLower() == "mp3s")
			{
                args = args.Skip(1).ToArray();
                var dirs = args.Where(arg => !arg.StartsWith("-")).ToArray();

                if (dirs.Length != 2)
                {
					Console.WriteLine("Wrong arguments.");
					Environment.Exit(1);
					return;
				}

                var options = args.Where(arg => arg.StartsWith("-"));
				var sourceDir = dirs[0];
                var destDir = dirs[1];

                Directory.CreateDirectory(destDir);

                var processedFiles = new List<string>();
                var sourceFiles = Directory.GetFiles(sourceDir);

                foreach (var sourceFile in sourceFiles)
                {
                    var extension = Path.GetExtension(sourceFile).ToLower();

                    if (extension == ".wav" || extension == ".mp3" || extension == ".txt" || extension == ".md")
                        continue;

                    var filename = extension == ".sa" ? Path.GetFileNameWithoutExtension(sourceFile) : Path.GetFileName(sourceFile);

                    if (processedFiles.Contains(filename))
                        continue;

                    processedFiles.Add(filename);

                    var fileArgs = Enumerable.Concat(new List<string> { sourceFile, Path.Combine(destDir, filename + ".mp3") }, options).ToArray();
                    SaToWavOrMp3(fileArgs, false);
                }
			}
			else
            {
				SaToWavOrMp3(args);
            }
        }

        static void SaToWavOrMp3(string[] args, bool exitOnCompletion = true)
        {
            string saPath = null;
            string wavOrMp3Path = null;
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
                    else if (wavOrMp3Path == null)
                        wavOrMp3Path = arg;
                    else
                    {
                        Console.WriteLine("Invalid number of arguments.");
                        Usage();
                        Environment.Exit(-1);
                        return;
                    }
                }
            }

            if (saPath == null || wavOrMp3Path == null)
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
                Console.WriteLine($"Failed to load sonic arranger file \"{saPath}\".");
                if (exitOnCompletion)
                    Environment.Exit(1);
                return;
            }

            if (song < 0 || song >= saFile.Songs.Length)
            {
                Console.WriteLine($"Song index {song} is not valid or present in the SA file \"{saPath}\".");
				if (exitOnCompletion)
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
                Console.WriteLine($"Error while processing SA file \"{saPath}\" with given options.");
				if (exitOnCompletion)
					Environment.Exit(2);
                return;
            }

            System.IO.Stream outFile;

            try
            {
                outFile = File.Create(wavOrMp3Path);
            }
            catch
            {
                Console.WriteLine($"Error creating output file \"{wavOrMp3Path}\".");
				if (exitOnCompletion)
					Environment.Exit(3);
                return;
            }

            bool mp3 = wavOrMp3Path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
            var waveStream = mp3 ? new MemoryStream() : outFile;

            try
            {
                WriteWave(waveStream, saStream.ToSignedArray(numLoops), (uint)sampleRate, channelMode);

                if (mp3)
                {
                    waveStream.Position = 0;
                    WaveToMp3(waveStream, outFile, sampleRate, channelMode);
                }
            }
            catch
            {
                Console.WriteLine($"Error writing data to output file \"{wavOrMp3Path}\".");
				if (exitOnCompletion)
					Environment.Exit(4);
                return;
            }
            finally
            {
                outFile.Close();
            }

            Console.WriteLine("Successfully converted SA file to:");
            Console.WriteLine(" -> " + wavOrMp3Path);
			if (exitOnCompletion)
				Environment.Exit(0);
        }

        static void WaveToMp3(System.IO.Stream waveStream, System.IO.Stream mp3Stream, int sampleRate, SonicArranger.Stream.ChannelMode channelMode)
        {
            // First convert to 16 bit PCM
			WaveFormat target = new(sampleRate, 16, (int)channelMode);
            using WaveStream sourceStream = new WaveFileReader(waveStream);
            using WaveFormatConversionStream conversionStream = new(target, sourceStream);
			using MemoryStream convertedStream = new();
            WaveFileWriter.WriteWavFileToStream(convertedStream, conversionStream);
            convertedStream.Position = 0;
			using var waveReader = new WaveFileReader(convertedStream);
			using var mp3Writer = new LameMP3FileWriter(mp3Stream, waveReader.WaveFormat, 192);
			waveReader.CopyTo(mp3Writer);
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
