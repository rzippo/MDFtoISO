using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Mdf2IsoUWP
{
    static class Mdf2IsoConverter
    {
        static readonly byte[] SYNC_HEADER = {
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

        static readonly byte[] SYNC_HEADER_MDF_AUDIO = {
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

        static readonly byte[] SYNC_HEADER_MDF = {
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

        static readonly byte[] ISO_9660 = {
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
        static int iso9660Pos = 32768;
        static int syncHeaderMdfPos = 2352;

        public static async Task ConvertAsync(StorageFile mdfFile, StorageFile isoFile)
        {
            using (Stream sourceStream = await mdfFile.OpenStreamForReadAsync())
            {
                sourceStream.Seek(iso9660Pos, SeekOrigin.Current);
                byte[] iso9660HeaderBuf = new byte[8];
                sourceStream.Read(iso9660HeaderBuf, 0, 8);
                if (iso9660HeaderBuf.SequenceEqual(ISO_9660)) //280 negato
                {
                    //File is already iso9660
                    return;
                }

                int seek_ecc,
                    sector_size,
                    sector_data,
                    seek_head;

                sourceStream.Seek(0, SeekOrigin.Begin);
                byte[] syncHeaderBuf = new byte[12];
                sourceStream.Read(syncHeaderBuf, 0, 12);

                sourceStream.Seek(syncHeaderMdfPos, SeekOrigin.Begin);
                byte[] syncHeaderMdfBuf = new byte[12];
                sourceStream.Read(syncHeaderMdfBuf, 0, 12);
                
                if (syncHeaderBuf.SequenceEqual(SYNC_HEADER)) //284
                {
                    if(syncHeaderMdfBuf.SequenceEqual(SYNC_HEADER_MDF)) //289
                    {
                        //skip 291: no cue option
                        //303
                        /*BAD SECTOR */
                        seek_ecc = 384;
                        sector_size = 2448;
                        sector_data = 2048;
                        seek_head = 16;
                    }
                    else if(syncHeaderMdfBuf.SequenceEqual(SYNC_HEADER)) //321
                    {
                            //skip 323: no cue option
                            //335
                            /*NORMAL IMAGE */
                            seek_ecc = 288;
                            sector_size = 2352;
                            sector_data = 2048;
                            seek_head = 16;
                    }
                    else //349
                    {
                        //Sorry I don't know this format :(
                        return;
                    }
                }
                else //356
                {
                    if(syncHeaderMdfBuf.SequenceEqual(SYNC_HEADER_MDF_AUDIO)) //361
                    {
                        //368
                        /*BAD SECTOR AUDIO */
                        seek_head = 0;
                        sector_size = 2448;
                        seek_ecc = 96;
                        sector_data = 2352;
                    }
                    else
                    {
                        //Sorry I don't know this format :(
                        return;
                    }
                }

                //376
                using (Stream destStream = await isoFile.OpenStreamForWriteAsync())
                {
                    long sourceSectorLength = sourceStream.Length / sector_size;
                    long isoSize = sourceSectorLength * sector_data;
                    //todo: use progressBar
                    double progressBar = ((double) 100) / sourceSectorLength;

                    sourceStream.Seek(0, SeekOrigin.Begin);
                    byte[] sectorBuf = new byte[sector_data];

                    for (int i = 0; i < sourceSectorLength; i++) //391
                    {
                        sourceStream.Seek(seek_head, SeekOrigin.Current);
                        sourceStream.Read(sectorBuf, 0, sector_data);
                        destStream.Write(sectorBuf, 0, sector_data);
                        //409
                        sourceStream.Seek(seek_ecc, SeekOrigin.Current);

                        /* todo: use percent
                         * write_iso = (int) (sector_data * i);
					     * if (i != 0)
						 *  percent = (int) (write_iso * 100 / size_iso);
					     *main_percent (percent);
                         */
                    }
                    //416
                    //Should be finished here
                }
            }
        }

    }
}
