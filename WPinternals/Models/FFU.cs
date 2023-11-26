// Copyright (c) 2018, Rene Lergner - @Heathcliff74xda
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Linq;

namespace WPinternals
{
    internal class FFU
    {
        internal int ChunkSize;
        internal string Path;
        internal byte[] SecurityHeader;
        internal byte[] ImageHeader;
        internal byte[] StoreHeader;
        private readonly int?[] ChunkIndexes;
        private FileStream FFUFile = null;
        private int FileOpenCount = 0;

        internal string PlatformID;
        internal GPT GPT;

        internal UInt64 TotalSize;
        internal UInt64 HeaderSize;
        internal UInt64 PayloadSize;
        internal UInt64 TotalChunkCount;

        internal FFU(string Path)
        {
            this.Path = Path;

            try
            {
                OpenFile();

                // Read Security Header
                byte[] ShortSecurityHeader = new byte[0x20];
                FFUFile.Read(ShortSecurityHeader, 0, 0x20);
                if (ByteOperations.ReadAsciiString(ShortSecurityHeader, 0x04, 0x0C) != "SignedImage ")
                {
                    throw new BadImageFormatException();
                }

                ChunkSize = ByteOperations.ReadInt32(ShortSecurityHeader, 0x10) * 1024;
                UInt32 SecurityHeaderSize = ByteOperations.ReadUInt32(ShortSecurityHeader, 0x00);
                UInt32 CatalogSize = ByteOperations.ReadUInt32(ShortSecurityHeader, 0x18);
                UInt32 HashTableSize = ByteOperations.ReadUInt32(ShortSecurityHeader, 0x1C);
                SecurityHeader = new byte[RoundUpToChunks(SecurityHeaderSize + CatalogSize + HashTableSize)];
                FFUFile.Seek(0, SeekOrigin.Begin);
                FFUFile.Read(SecurityHeader, 0, SecurityHeader.Length);

                // Read Image Header
                byte[] ShortImageHeader = new byte[0x1C];
                FFUFile.Read(ShortImageHeader, 0, 0x1C);
                if (ByteOperations.ReadAsciiString(ShortImageHeader, 0x04, 0x0C) != "ImageFlash  ")
                {
                    throw new BadImageFormatException();
                }

                UInt32 ImageHeaderSize = ByteOperations.ReadUInt32(ShortImageHeader, 0x00);
                UInt32 ManifestSize = ByteOperations.ReadUInt32(ShortImageHeader, 0x10);
                ImageHeader = new byte[RoundUpToChunks(ImageHeaderSize + ManifestSize)];
                FFUFile.Seek(SecurityHeader.Length, SeekOrigin.Begin);
                FFUFile.Read(ImageHeader, 0, ImageHeader.Length);

                // Read Store Header
                byte[] ShortStoreHeader = new byte[248];
                FFUFile.Read(ShortStoreHeader, 0, 248);
                PlatformID = ByteOperations.ReadAsciiString(ShortStoreHeader, 0x0C, 192).TrimEnd([(char)0, ' ']);
                int WriteDescriptorCount = ByteOperations.ReadInt32(ShortStoreHeader, 208);
                UInt32 WriteDescriptorLength = ByteOperations.ReadUInt32(ShortStoreHeader, 212);
                UInt32 ValidateDescriptorLength = ByteOperations.ReadUInt32(ShortStoreHeader, 220);
                StoreHeader = new byte[RoundUpToChunks(248 + WriteDescriptorLength + ValidateDescriptorLength)];
                FFUFile.Seek(SecurityHeader.Length + ImageHeader.Length, SeekOrigin.Begin);
                FFUFile.Read(StoreHeader, 0, StoreHeader.Length);

                // Parse Chunk Indexes
                int HighestChunkIndex = 0;
                UInt32 LocationCount;
                int ChunkIndex;
                int ChunkCount;
                int DiskAccessMethod;
                UInt32 WriteDescriptorEntryOffset = 248 + ValidateDescriptorLength;
                int FFUChunkIndex = 0;
                for (int i = 0; i < WriteDescriptorCount; i++)
                {
                    LocationCount = ByteOperations.ReadUInt32(StoreHeader, WriteDescriptorEntryOffset + 0x00);
                    ChunkCount = ByteOperations.ReadInt32(StoreHeader, WriteDescriptorEntryOffset + 0x04);

                    for (int j = 0; j < LocationCount; j++)
                    {
                        DiskAccessMethod = ByteOperations.ReadInt32(StoreHeader, (UInt32)(WriteDescriptorEntryOffset + 0x08 + (j * 0x08)));
                        ChunkIndex = ByteOperations.ReadInt32(StoreHeader, (UInt32)(WriteDescriptorEntryOffset + 0x0C + (j * 0x08)));

                        if (DiskAccessMethod == 0 && (ChunkIndex + ChunkCount - 1) > HighestChunkIndex) // 0 = From begin, 2 = From end. We ignore chunks at end of disk. These contain secondairy GPT.
                        {
                            HighestChunkIndex = ChunkIndex + ChunkCount - 1;
                        }
                    }
                    WriteDescriptorEntryOffset += 8 + (LocationCount * 0x08);
                    FFUChunkIndex += ChunkCount;
                }
                ChunkIndexes = new int?[HighestChunkIndex + 1];
                WriteDescriptorEntryOffset = 248 + ValidateDescriptorLength;
                FFUChunkIndex = 0;
                for (int i = 0; i < WriteDescriptorCount; i++)
                {
                    LocationCount = ByteOperations.ReadUInt32(StoreHeader, WriteDescriptorEntryOffset + 0x00);
                    ChunkCount = ByteOperations.ReadInt32(StoreHeader, WriteDescriptorEntryOffset + 0x04);

                    for (int j = 0; j < LocationCount; j++)
                    {
                        DiskAccessMethod = ByteOperations.ReadInt32(StoreHeader, (UInt32)(WriteDescriptorEntryOffset + 0x08 + (j * 0x08)));
                        ChunkIndex = ByteOperations.ReadInt32(StoreHeader, (UInt32)(WriteDescriptorEntryOffset + 0x0C + (j * 0x08)));

                        if (DiskAccessMethod == 0) // 0 = From begin, 2 = From end. We ignore chunks at end of disk. These contain secondairy GPT.
                        {
                            for (int k = 0; k < ChunkCount; k++)
                            {
                                ChunkIndexes[ChunkIndex + k] = FFUChunkIndex + k;
                            }
                        }
                    }
                    WriteDescriptorEntryOffset += 8 + (LocationCount * 0x08);
                    FFUChunkIndex += ChunkCount;
                }

                byte[] GPTBuffer = GetSectors(0x01, 0x21);
                GPT = new GPT(GPTBuffer);

                HeaderSize = (UInt64)(SecurityHeader.Length + ImageHeader.Length + StoreHeader.Length);

                TotalChunkCount = (UInt64)FFUChunkIndex;
                PayloadSize = TotalChunkCount * (UInt64)ChunkSize;
                TotalSize = HeaderSize + PayloadSize;

                if (TotalSize != (UInt64)FFUFile.Length)
                {
                    throw new WPinternalsException("Bad FFU file", "Bad FFU file: " + Path + "." + Environment.NewLine + "Expected size: " + TotalSize.ToString() + ". Actual size: " + FFUFile.Length + ".");
                }
            }
            catch (WPinternalsException)
            {
                throw;
            }
            catch (Exception Ex)
            {
                throw new WPinternalsException("Bad FFU file", "Bad FFU file: " + Path + "." + Environment.NewLine + Ex.Message, Ex);
            }
            finally
            {
                CloseFile();
            }
        }

        internal static bool IsFFU(string FileName)
        {
            bool Result = false;

            FileStream FFUFile = new(FileName, FileMode.Open, FileAccess.Read);

            byte[] Signature = new byte[0x10];
            FFUFile.Read(Signature, 0, 0x10);

            Result = ByteOperations.ReadAsciiString(Signature, 0x04, 0x0C) == "SignedImage ";

            FFUFile.Close();

            return Result;
        }

        private void OpenFile()
        {
            if (FFUFile == null)
            {
                FFUFile = new FileStream(Path, FileMode.Open, FileAccess.Read);
                FileOpenCount = 0;
            }
            FileOpenCount++;
        }

        private void CloseFile()
        {
            FileOpenCount--;
            if (FileOpenCount == 0)
            {
                FFUFile.Close();
                FFUFile = null;
            }
        }

        private void FileSeek(long Position)
        {
            // https://social.msdn.microsoft.com/Forums/vstudio/en-US/2e67ca57-3556-4275-accd-58b7df30d424/unnecessary-filestreamseek-and-setting-filestreamposition-has-huge-effect-on-performance?forum=csharpgeneral

            if (FFUFile != null && FFUFile.Position != Position)
            {
                FFUFile.Seek(Position, SeekOrigin.Begin);
            }
        }

        internal UInt32 RoundUpToChunks(UInt32 Size)
        {
            if ((Size % ChunkSize) > 0)
            {
                return (UInt32)(((Size / ChunkSize) + 1) * ChunkSize);
            }
            else
            {
                return Size;
            }
        }

        internal UInt32 RoundDownToChunks(UInt32 Size)
        {
            if ((Size % ChunkSize) > 0)
            {
                return (UInt32)(Size / ChunkSize * ChunkSize);
            }
            else
            {
                return Size;
            }
        }

        internal byte[] GetSectors(int StartSector, int SectorCount)
        {
            int FirstChunk = GetChunkIndexFromSectorIndex(StartSector);
            int LastChunk = GetChunkIndexFromSectorIndex(StartSector + SectorCount - 1);

            byte[] Buffer = new byte[ChunkSize];

            OpenFile();

            byte[] Result = new byte[SectorCount * 0x200];

            int ResultOffset = 0;

            for (int j = FirstChunk; j <= LastChunk; j++)
            {
                GetChunk(Buffer, j);

                int FirstSector = 0;
                int LastSector = (ChunkSize / 0x200) - 1;

                if (j == FirstChunk)
                {
                    FirstSector = GetSectorNumberInChunkFromSectorIndex(StartSector);
                }

                if (j == LastChunk)
                {
                    LastSector = GetSectorNumberInChunkFromSectorIndex(StartSector + SectorCount - 1);
                }

                int Offset = FirstSector * 0x200;
                int Size = (LastSector - FirstSector + 1) * 0x200;

                System.Buffer.BlockCopy(Buffer, Offset, Result, ResultOffset, Size);

                ResultOffset += Size;
            }

            CloseFile();

            return Result;
        }

        internal byte[] GetPartition(string Name)
        {
            Partition Target = GPT.Partitions.Find(p => string.Equals(p.Name, Name, StringComparison.CurrentCultureIgnoreCase));
            if (Target == null)
            {
                throw new ArgumentOutOfRangeException();
            }

            return GetSectors((int)Target.FirstSector, (int)(Target.LastSector - Target.FirstSector + 1));
        }

        internal void WritePartition(string Name, string FilePath, bool Compress = false)
        {
            WritePartition(Name, FilePath, null, null, Compress);
        }

        internal void WritePartition(string Name, string FilePath, Action<int, TimeSpan?> ProgressUpdateCallback, bool Compress = false)
        {
            WritePartition(Name, FilePath, ProgressUpdateCallback, null, Compress);
        }

        internal void WritePartition(string Name, string FilePath, ProgressUpdater UpdaterPerSector, bool Compress = false)
        {
            WritePartition(Name, FilePath, null, UpdaterPerSector, Compress);
        }

        private void WritePartition(string Name, string FilePath, Action<int, TimeSpan?> ProgressUpdateCallback, ProgressUpdater UpdaterPerSector, bool Compress = false)
        {
            Partition Target = GPT.Partitions.Find(p => string.Equals(p.Name, Name, StringComparison.CurrentCultureIgnoreCase));
            if (Target == null)
            {
                throw new ArgumentOutOfRangeException();
            }

            int FirstChunk = GetChunkIndexFromSectorIndex((int)Target.FirstSector);
            int LastChunk = GetChunkIndexFromSectorIndex((int)Target.LastSector);

            ProgressUpdater Updater = UpdaterPerSector;
            if ((Updater == null) && (ProgressUpdateCallback != null))
            {
                Updater = new ProgressUpdater(Target.LastSector - Target.FirstSector + 1, ProgressUpdateCallback);
            }

            byte[] Buffer = new byte[ChunkSize];

            OpenFile();

            FileStream OutputFile = new(FilePath, FileMode.Create, FileAccess.Write);
            Stream OutStream = OutputFile;

            // We use gzip compression
            //
            // LZMA is about 60 times slower (compression is twice as good, but compressed size is already really small, so it doesnt matter much)
            // OutStream = new LZMACompressionStream(OutputFile, System.IO.Compression.CompressionMode.Compress, false);
            //
            // DeflateStream is a raw compression stream without recognizable header
            // Deflate has almost no performance penalty
            // OutStream = new DeflateStream(OutputFile, CompressionLevel.Optimal, false);
            //
            // GZip can be recognized. It always starts with 1F 8B 08 (1F 8B is the magic value, 08 is the Deflate compression method)
            // With GZip compression, dump time goes from 1m to 1m37s. So that doesnt matter much.
            if (Compress)
            {
                OutStream = new CompressedStream(OutputFile, (Target.LastSector - Target.FirstSector + 1) * 0x200);
            }

            for (int j = FirstChunk; j <= LastChunk; j++)
            {
                GetChunk(Buffer, j);

                int FirstSector = 0;
                int LastSector = (ChunkSize / 0x200) - 1;

                if (j == FirstChunk)
                {
                    FirstSector = GetSectorNumberInChunkFromSectorIndex((int)Target.FirstSector);
                }

                if (j == LastChunk)
                {
                    LastSector = GetSectorNumberInChunkFromSectorIndex((int)Target.LastSector);
                }

                int Offset = FirstSector * 0x200;
                int Size = (LastSector - FirstSector + 1) * 0x200;

                OutStream.Write(Buffer, Offset, Size);

                Updater?.IncreaseProgress((UInt64)(ChunkSize / 0x200));
            }

            OutStream.Close();

            CloseFile();
        }

        private byte[] GetChunk(int ChunkIndex)
        {
            long BaseOffset = (long)SecurityHeader.Length + ImageHeader.Length + StoreHeader.Length;
            if (ChunkIndexes[ChunkIndex] == null)
            {
                return new byte[ChunkSize];
            }
            else
            {
                OpenFile();
                FileSeek(BaseOffset + ((long)ChunkIndexes[ChunkIndex] * ChunkSize));
                byte[] Chunk = new byte[ChunkSize];
                FFUFile.Read(Chunk, 0, ChunkSize);
                CloseFile();
                return Chunk;
            }
        }

        private void GetChunk(byte[] Chunk, int ChunkIndex)
        {
            long BaseOffset = SecurityHeader.Length + ImageHeader.Length + StoreHeader.Length;
            if (ChunkIndexes[ChunkIndex] == null)
            {
                Array.Clear(Chunk, 0, ChunkSize);
            }
            else
            {
                OpenFile();
                FileSeek(BaseOffset + ((long)ChunkIndexes[ChunkIndex] * ChunkSize));
                FFUFile.Read(Chunk, 0, ChunkSize);
                CloseFile();
            }
        }

        private int GetChunkIndexFromSectorIndex(int SectorIndex)
        {
            int SectorsPerChunk = ChunkSize / 0x200;
            return SectorIndex / SectorsPerChunk;
        }

        private int GetSectorNumberInChunkFromSectorIndex(int SectorIndex)
        {
            int SectorsPerChunk = ChunkSize / 0x200;
            return SectorIndex % SectorsPerChunk;
        }

        internal bool IsPartitionPresentInFFU(string PartitionName)
        {
            Partition Target = GPT.GetPartition(PartitionName);
            if (Target == null)
            {
                throw new InvalidOperationException("Partitionname is not found!");
            }

            int ChunkIndex = GetChunkIndexFromSectorIndex((int)Target.FirstSector);
            return ChunkIndexes[ChunkIndex] != null;
        }

        private int GetChunkIndexFromSectorIndex(ulong p)
        {
            throw new NotImplementedException();
        }

        internal string GetFirmwareVersion()
        {
            string Result = null;

            Partition Plat = GPT.GetPartition("PLAT");
            if (Plat != null)
            {
                byte[] Data = GetPartition("PLAT");
                uint? Offset = ByteOperations.FindAscii(Data, "SWVERSION=");
                if (Offset != null)
                {
                    uint Start = (uint)Offset + 10;
                    uint Length = (uint)ByteOperations.FindPattern(Data, Start, 0x100, [0x00], null, null) - Start;
                    uint? Offset0D = ByteOperations.FindPattern(Data, Start, 0x100, [0x0D], null, null);
                    if ((Offset0D != null) && (Offset0D < (Start + Length)))
                    {
                        Length = (uint)Offset0D - Start;
                    }

                    Result = ByteOperations.ReadAsciiString(Data, Start, Length);
                }
            }

            return Result;
        }

        internal string GetOSVersion()
        {
            byte[] efiesp = GetPartition("EFIESP");
            MemoryStream s = new(efiesp);
            DiscUtils.Fat.FatFileSystem fs = new(s);
            Stream mss = fs.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Open, FileAccess.Read);
            MemoryStream msms = new();
            mss.CopyTo(msms);
            byte[] mobilestartup = msms.ToArray();
            Version OSVersion = PE.GetProductVersion(mobilestartup);
            s.Close();

            return OSVersion.ToString();
        }
    }
}
