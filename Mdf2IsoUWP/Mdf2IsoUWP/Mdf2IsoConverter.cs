using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Mdf2IsoUWP
{
    internal enum ConversionResult
    {
        Success,
        AlreadyIso,
        FormatNotSupported,
        ConversionCanceled,
        IoException
    }

    internal enum CopyResult
    {
        Success,
        IoException,
        CopyCanceled
    }

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

        //log strings
        private static int conversionId = 1;

        private const string AlreadyIsoLog = "Input file is already in iso9660 format.";
        private const string NotSupportedLog = "Sorry, this format is not supported";
        private const string StartingConversionLog = "File format ok, starting conversion...";
        private const string ConversionCompletedLog = "Conversion completed";
        private const string ConversionCanceledLog = "Conversion cancelled by user.";
        private const string IoExceptionLog = "Exception while accessing files.";
        private static string ConversionProgressLog(int currentStep) => $"Conversion {currentStep}% done";
        
        private static string StartingNewConversionLog() => $"Starting conversion #{conversionId++}";

        private const string StartingCopyLog = "Starting copy of contents...";
        private const string CopyCompletedLog = "Copy completed";
        private const string CopyCanceledLog = "Copy cancelled by user.";

        private static string CopyProgressLog(int currentStep) => $"Copy {currentStep}% done";

        public static async Task<ConversionResult> ConvertAsync(
            StorageFile mdfFile,
            StorageFile isoFile,
            IProgress<int> progress = null,
            StreamWriter log = null,
            CancellationToken token = default(CancellationToken)
        )
        {
            if(conversionId != 1)
                log?.WriteLine();
            log?.WriteLine(StartingNewConversionLog());

            try
            {
                using (Stream sourceStream = await mdfFile.OpenStreamForReadAsync())
                {
                    sourceStream.Seek(Iso9660Pos, SeekOrigin.Current);
                    byte[] iso9660HeaderBuf = new byte[8];
                    sourceStream.Read(iso9660HeaderBuf, 0, 8);
                    if (iso9660HeaderBuf.SequenceEqual(Iso9660)) //280 nagated
                    {
                        log?.WriteLine(AlreadyIsoLog);
                        return ConversionResult.AlreadyIso;
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
                            log?.WriteLine(NotSupportedLog);
                            return ConversionResult.FormatNotSupported;
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
                            log?.WriteLine(NotSupportedLog);
                            return ConversionResult.FormatNotSupported;
                        }
                    }

                    log?.WriteLine(StartingConversionLog);

                    //376
                    using (Stream destStream = await isoFile.OpenStreamForWriteAsync())
                    {
                        destStream.SetLength(0);
                        long sourceSectorsCount = sourceStream.Length / sectorSize;
                        //long isoSize = sourceSectorsCount * sectorData;

                        sourceStream.Seek(0, SeekOrigin.Begin);
                        var sectorBuf = new byte[sectorData];

                        int lastReportedProgress = 0;
                        for (int i = 0; i < sourceSectorsCount; i++) //391
                        {
                            sourceStream.Seek(seekHead, SeekOrigin.Current);
                            await sourceStream.ReadAsync(sectorBuf, 0, sectorData, token);
                            await destStream.WriteAsync(sectorBuf, 0, sectorData, token);
                            
                            //409
                            sourceStream.Seek(seekEcc, SeekOrigin.Current);

                            int currentProgress = (int)(i * ProgressMax / sourceSectorsCount);
                            if (currentProgress > lastReportedProgress)
                            {
                                progress?.Report(currentProgress);
                                log?.WriteLine(ConversionProgressLog(currentProgress));
                                lastReportedProgress = currentProgress;
                            }
                        }
                        //416

                    }
                }

                progress?.Report(ProgressMax);
                log?.WriteLine(ConversionCompletedLog);
                return ConversionResult.Success;
            }
            catch(IOException)
            {
                log?.WriteLine(IoExceptionLog);
                return ConversionResult.IoException;
            }
            catch (OperationCanceledException)
            {
                log?.WriteLine(ConversionCanceledLog);
                return ConversionResult.ConversionCanceled;
            }
        }

        public static async Task<CopyResult> CopyAsync(
            StorageFile mdfFile,
            StorageFile isoFile,
            IProgress<int> progress = null,
            StreamWriter log = null,
            CancellationToken token = default(CancellationToken)
        )
        {
            log?.WriteLine(StartingCopyLog);
            try
            {
                using (Stream sourceStream = await mdfFile.OpenStreamForReadAsync())
                {
                    using (Stream destStream = await isoFile.OpenStreamForWriteAsync())
                    {
                        int blockSize = 16 * 1024;
                        long sourceBlocksCount = sourceStream.Length / blockSize;

                        byte[] blockBuffer = new byte[blockSize];
                        sourceStream.Seek(0, SeekOrigin.Begin);

                        int lastReportedProgress = 0;
                        for (int i = 0; i < sourceBlocksCount; i++)
                        {
                            await sourceStream.ReadAsync(blockBuffer, 0, blockSize, token);
                            await destStream.WriteAsync(blockBuffer, 0, blockSize, token);

                            int currentProgress = (int) (i * ProgressMax / sourceBlocksCount);
                            if (currentProgress > lastReportedProgress)
                            {
                                progress?.Report(currentProgress);
                                log?.WriteLine(CopyProgressLog(currentProgress));
                                lastReportedProgress = currentProgress;
                            }
                        }
                    }
                }

                progress?.Report(ProgressMax);
                log?.WriteLine(CopyCompletedLog);
                return CopyResult.Success;
            }
            catch (IOException)
            {
                log?.WriteLine(IoExceptionLog);
                return CopyResult.IoException;
            }
            catch (OperationCanceledException)
            {
                log?.WriteLine(CopyCanceledLog);
                return CopyResult.CopyCanceled;
            }
        }
    }


}
