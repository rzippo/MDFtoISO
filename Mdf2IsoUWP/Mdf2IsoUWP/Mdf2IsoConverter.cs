using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Mdf2IsoUWP
{
    internal static class Mdf2IsoConverter
    {
        private static readonly byte[] SyncHeader = {
            0x00,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0xFF,
            0x00
        };

        private static readonly byte[] SyncHeaderMdfAudio = {
            0x80,
            0x80,
            0x80,
            0x80,
            0x80,
            0x80,
            0x80,
            0xC0,
            0x80,
            0x80,
            0x80,
            0x80
        };

        private static readonly byte[] SyncHeaderMdf = {
            0x80,
            0xC0,
            0x80,
            0x80,
            0x80,
            0x80,
            0x80,
            0xC0,
            0x80,
            0x80,
            0x80,
            0x80
        };

        private static readonly byte[] Iso9660 = {
            0x01,
            0x43,
            0x44,
            0x30,
            0x30,
            0x31,
            0x01,
            0x00
        };

        //Unnamed constants
        private const int Iso9660Pos = 32768;
        private const int SyncHeaderMdfPos = 2352;

        public static int ProgressMax { get; set; } = 100;

        public static async Task ConvertAsync(
            StorageFile mdfFile,
            StorageFile isoFile,
            IProgress<int> progress = null,
            StreamWriter log = null,
            CancellationToken token = default(CancellationToken)
        )
        {
            try
            {
                using (Stream sourceStream = await mdfFile.OpenStreamForReadAsync())
                {
                    sourceStream.Seek(Iso9660Pos, SeekOrigin.Current);
                    byte[] iso9660HeaderBuf = new byte[8];
                    sourceStream.Read(iso9660HeaderBuf, 0, 8);
                    if (iso9660HeaderBuf.SequenceEqual(Iso9660)) //280 nagated
                    {
                        log?.WriteLine("File is already iso9660");
                        return;
                    }

                    int seekEcc,
                        sectorSize,
                        sectorData,
                        seekHead;

                    sourceStream.Seek(0, SeekOrigin.Begin);
                    byte[] syncHeaderBuf = new byte[12];
                    sourceStream.Read(syncHeaderBuf, 0, 12);

                    sourceStream.Seek(SyncHeaderMdfPos, SeekOrigin.Begin);
                    byte[] syncHeaderMdfBuf = new byte[12];
                    sourceStream.Read(syncHeaderMdfBuf, 0, 12);

                    if (syncHeaderBuf.SequenceEqual(SyncHeader)) //284
                    {
                        if (syncHeaderMdfBuf.SequenceEqual(SyncHeaderMdf)) //289
                        {
                            //skip 291: no cue option
                            //303
                            /*BAD SECTOR */
                            seekEcc = 384;
                            sectorSize = 2448;
                            sectorData = 2048;
                            seekHead = 16;
                        }
                        else if (syncHeaderMdfBuf.SequenceEqual(SyncHeader)) //321
                        {
                            //skip 323: no cue option
                            //335
                            /*NORMAL IMAGE */
                            seekEcc = 288;
                            sectorSize = 2352;
                            sectorData = 2048;
                            seekHead = 16;
                        }
                        else //349
                        {
                            log?.WriteLine("Sorry I don't know this format :(");
                            return;
                        }
                    }
                    else //356
                    {
                        if (syncHeaderMdfBuf.SequenceEqual(SyncHeaderMdfAudio)) //361
                        {
                            //368
                            /*BAD SECTOR AUDIO */
                            seekHead = 0;
                            sectorSize = 2448;
                            seekEcc = 96;
                            sectorData = 2352;
                        }
                        else
                        {
                            log?.WriteLine("Sorry I don't know this format :(");
                            return;
                        }
                    }

                    log?.WriteLine("File type ok, starting conversion...");

                    //376
                    using (Stream destStream = await isoFile.OpenStreamForWriteAsync())
                    {
                        destStream.SetLength(0);
                        long sourceSectorLength = sourceStream.Length / sectorSize;
                        long isoSize = sourceSectorLength * sectorData;

                        sourceStream.Seek(0, SeekOrigin.Begin);
                        byte[] sectorBuf = new byte[sectorData];

                        int lastReported = 0;
                        for (int i = 0; i < sourceSectorLength; i++) //391
                        {
                            sourceStream.Seek(seekHead, SeekOrigin.Current);
                            await sourceStream.ReadAsync(sectorBuf, 0, sectorData, token);
                            await destStream.WriteAsync(sectorBuf, 0, sectorData, token);
                            
                            //409
                            sourceStream.Seek(seekEcc, SeekOrigin.Current);

                            int currentStep = (int)(i * ProgressMax / sourceSectorLength);
                            if (currentStep > lastReported)
                            {
                                progress?.Report(currentStep);
                                log?.WriteLine($"Conversion {currentStep}% done");
                                lastReported = currentStep;
                            }
                        }
                        //416

                    }
                }

                progress?.Report(ProgressMax);
                log?.WriteLine("Conversion completed");
            }
            catch (OperationCanceledException)
            {
                log?.WriteLine("Conversion cancelled by user.");
            }
        }
    }
}
