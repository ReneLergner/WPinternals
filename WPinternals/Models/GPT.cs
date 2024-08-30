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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Serialization;

namespace WPinternals
{
    [XmlType("Partitions")]
    public class GPT
    {
        private byte[] GPTBuffer;
        private readonly UInt32 HeaderOffset;
        private readonly UInt32 HeaderSize;
        private UInt32 TableOffset;
        private UInt32 TableSize;
        private readonly UInt32 PartitionEntrySize;
        private readonly UInt32 MaxPartitions;
        internal UInt64 FirstUsableSector;
        internal UInt64 LastUsableSector;
        internal bool HasChanged = false;

        [XmlElement("Partition")]
        public List<Partition> Partitions = new();

        public GPT() // Only for serialization
        {
        }

        internal GPT(byte[] GPTBuffer)
        {
            this.GPTBuffer = GPTBuffer;
            UInt32? TempHeaderOffset = ByteOperations.FindAscii(GPTBuffer, "EFI PART");
            if (TempHeaderOffset == null)
            {
                throw new WPinternalsException("Bad GPT", "The GPT read isn't valid. Couldn't find the text \"EFI PART\".");
            }

            HeaderOffset = (UInt32)TempHeaderOffset;
            HeaderSize = ByteOperations.ReadUInt32(GPTBuffer, HeaderOffset + 0x0C);
            TableOffset = HeaderOffset + 0x200;
            FirstUsableSector = ByteOperations.ReadUInt64(GPTBuffer, HeaderOffset + 0x28);
            LastUsableSector = ByteOperations.ReadUInt64(GPTBuffer, HeaderOffset + 0x30);
            MaxPartitions = ByteOperations.ReadUInt32(GPTBuffer, HeaderOffset + 0x50);
            PartitionEntrySize = ByteOperations.ReadUInt32(GPTBuffer, HeaderOffset + 0x54);
            TableSize = MaxPartitions * PartitionEntrySize;
            if ((TableOffset + TableSize) > GPTBuffer.Length)
            {
                throw new WPinternalsException("Bad GPT", "The GPT read isn't valid. The sizes defined in the GPT header exceed the provided GPT size.");
            }

            UInt32 PartitionOffset = TableOffset;

            while (PartitionOffset < (TableOffset + TableSize))
            {
                string Name = ByteOperations.ReadUnicodeString(GPTBuffer, PartitionOffset + 0x38, 0x48).TrimEnd([(char)0, ' ']);
                if (Name.Length == 0)
                {
                    break;
                }

                Partition CurrentPartition = new();
                CurrentPartition.Name = Name;
                CurrentPartition.FirstSector = ByteOperations.ReadUInt64(GPTBuffer, PartitionOffset + 0x20);
                CurrentPartition.LastSector = ByteOperations.ReadUInt64(GPTBuffer, PartitionOffset + 0x28);
                CurrentPartition.PartitionTypeGuid = ByteOperations.ReadGuid(GPTBuffer, PartitionOffset + 0x00);
                CurrentPartition.PartitionGuid = ByteOperations.ReadGuid(GPTBuffer, PartitionOffset + 0x10);
                CurrentPartition.Attributes = ByteOperations.ReadUInt64(GPTBuffer, PartitionOffset + 0x30);
                Partitions.Add(CurrentPartition);
                PartitionOffset += PartitionEntrySize;
            }

            HasChanged = false;
        }

        internal Partition GetPartition(string Name)
        {
            return Partitions.Find(p => string.Equals(p.Name, Name, StringComparison.CurrentCultureIgnoreCase));
        }

        // Magic!
        // SecureBoot hack for Bootloader Spec A starts here
        internal byte[] InsertHack()
        {
            Partition HackPartition = Partitions.Find(p => p.Name == "HACK");
            Partition SBL1 = Partitions.Find(p => p.Name == "SBL1");
            Partition SBL2 = Partitions.Find(p => p.Name == "SBL2");

            if ((SBL1 == null) || (SBL2 == null))
            {
                throw new WPinternalsException("Bad GPT", "Can't patch GPT for the Secure Boot hack for Spec A devices. The provided GPT does not include a SBL1 and/or SBL2 partition.");
            }

            if (HackPartition == null)
            {
                HackPartition = new Partition
                {
                    Name = "HACK",
                    Attributes = SBL2.Attributes,
                    FirstSector = SBL1.LastSector,
                    LastSector = SBL1.LastSector,

                    PartitionTypeGuid = SBL2.PartitionTypeGuid,
                    PartitionGuid = SBL2.PartitionGuid
                };

                Partitions.Add(HackPartition);

                SBL1.LastSector--;

                SBL2.PartitionTypeGuid = new Guid(new byte[] { 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74 });
                SBL2.PartitionGuid = new Guid(new byte[] { 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74, 0x74 });
            }

            HasChanged = true;

            return Rebuild();
        }

        internal byte[] RemoveHack()
        {
            Partition HackPartition = Partitions.Find(p => p.Name == "HACK");
            Partition SBL1 = Partitions.Find(p => p.Name == "SBL1");
            Partition SBL2 = Partitions.Find(p => p.Name == "SBL2");

            if ((SBL1 == null) || (SBL2 == null))
            {
                throw new WPinternalsException("Bad GPT", "Can't un-patch GPT for the Secure Boot hack for Spec A devices. The provided GPT does not include a SBL1 and/or SBL2 partition.");
            }

            if (HackPartition != null)
            {
                SBL2.PartitionTypeGuid = HackPartition.PartitionTypeGuid;
                SBL2.PartitionGuid = HackPartition.PartitionGuid;

                Partitions.Remove(HackPartition);

                SBL1.LastSector++;
            }

            HasChanged = true;

            return Rebuild();
        }

        internal byte[] Rebuild()
        {
            if (GPTBuffer == null)
            {
                TableSize = 0x4200;
                TableOffset = 0;
                GPTBuffer = new byte[TableSize];
            }
            else
            {
                Array.Clear(GPTBuffer, (int)TableOffset, (int)TableSize);
            }

            UInt32 PartitionOffset = TableOffset;
            foreach (Partition CurrentPartition in Partitions)
            {
                ByteOperations.WriteGuid(GPTBuffer, PartitionOffset + 0x00, CurrentPartition.PartitionTypeGuid);
                ByteOperations.WriteGuid(GPTBuffer, PartitionOffset + 0x10, CurrentPartition.PartitionGuid);
                ByteOperations.WriteUInt64(GPTBuffer, PartitionOffset + 0x20, CurrentPartition.FirstSector);
                ByteOperations.WriteUInt64(GPTBuffer, PartitionOffset + 0x28, CurrentPartition.LastSector);
                ByteOperations.WriteUInt64(GPTBuffer, PartitionOffset + 0x30, CurrentPartition.Attributes);
                ByteOperations.WriteUnicodeString(GPTBuffer, PartitionOffset + 0x38, CurrentPartition.Name, 0x48);

                PartitionOffset += PartitionEntrySize;
            }

            ByteOperations.WriteUInt32(GPTBuffer, HeaderOffset + 0x58, ByteOperations.CRC32(GPTBuffer, TableOffset, TableSize));
            ByteOperations.WriteUInt32(GPTBuffer, HeaderOffset + 0x10, 0);
            ByteOperations.WriteUInt32(GPTBuffer, HeaderOffset + 0x10, ByteOperations.CRC32(GPTBuffer, HeaderOffset, HeaderSize));

            return GPTBuffer;
        }

        internal void MergePartitionsFromFile(string Path, bool RoundToChunks)
        {
            MergePartitions(File.ReadAllText(Path), RoundToChunks);
        }

        internal void MergePartitionsFromStream(Stream Partitions, bool RoundToChunks)
        {
            using TextReader tr = new StreamReader(Partitions);
            MergePartitions(tr.ReadToEnd(), RoundToChunks);
        }

        internal void MergePartitions(string Xml, bool RoundToChunks, ZipArchive Archive = null)
        {
            GPT GptToMerge;
            if (Xml == null)
            {
                GptToMerge = new GPT();
            }
            else
            {
                XmlSerializer x = new(typeof(GPT), "");
                MemoryStream s = new(System.Text.Encoding.ASCII.GetBytes(Xml));
                GptToMerge = (GPT)x.Deserialize(s);
                s.Dispose();
            }

            if (Archive != null)
            {
                foreach (Partition NewPartition in GptToMerge.Partitions)
                {
                    ZipArchiveEntry Entry = Archive.Entries.FirstOrDefault(e => string.Equals(e.Name, NewPartition.Name, StringComparison.CurrentCultureIgnoreCase) || e.Name.StartsWith(NewPartition.Name + ".", true, System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                    if (Entry == null)
                    {
                        // There is a partition entry in the xml, but this partition is not present in the archive.

                        Partition OldPartition = GetPartition(NewPartition.Name);
                        if (OldPartition == null)
                        {
                            // The partition entry in the xml is also not present in the current partition table.
                            // It must have a know position and length.

                            if (NewPartition.LastSector == 0)
                            {
                                throw new WPinternalsException("Unknown length for partition \"" + NewPartition.Name + "\". The last sector property is set to 0 and the partition doesn't exist on the device currently.");
                            }
                        }
                        else
                        {
                            // The partition entry in the xml is also present in the current partition table.
                            // But since the partition is not present in the archive, the partition cannot be relocated.
                            // If the location of the new partition is specified, it must be the same as the current partition.

                            if ((NewPartition.FirstSector != 0) && (NewPartition.FirstSector != OldPartition.FirstSector))
                            {
                                throw new WPinternalsException("Incorrect location for partition \"" + NewPartition.Name + "\". A partition defined in the xml file got its boundaries updated, but as the partition isn't provided in the archive, it is not possible to relocate it.");
                            }

                            if ((NewPartition.LastSector != 0) && (NewPartition.LastSector != OldPartition.LastSector))
                            {
                                throw new WPinternalsException("Incorrect length for partition \"" + NewPartition.Name + "\". A partition defined in the xml file got its boundaries updated, but as the partition isn't provided in the archive, it is not possible to relocate it.");
                            }

                            NewPartition.FirstSector = OldPartition.FirstSector;
                            NewPartition.LastSector = OldPartition.LastSector;
                        }
                    }
                    else
                    {
                        // The partition in the xml is also present in the archive.
                        // If the length is specified in the xml, it must match the file in the archive.

                        ulong StreamLengthInSectors = (ulong)Entry.Length / 0x200;
                        using (DecompressedStream DecompressedStream = new(Entry.Open()))
                        {
                            try
                            {
                                StreamLengthInSectors = (ulong)DecompressedStream.Length / 0x200;
                            }
                            catch (Exception ex)
                            {
                                LogFile.LogException(ex, LogType.FileOnly);
                            }
                        }

                        if (NewPartition.LastSector == 0)
                        {
                            NewPartition.SizeInSectors = StreamLengthInSectors;
                        }
                        else
                        {
                            if (NewPartition.SizeInSectors != StreamLengthInSectors)
                            {
                                throw new WPinternalsException("Inconsistent length specified for partition \"" + NewPartition.Name + "\". The provided partition in the archive does not match the length specified in the xml file.");
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (Partition NewPartition in GptToMerge.Partitions)
                {
                    // This is a partition entry in the xml, and there is no archive.

                    Partition OldPartition = GetPartition(NewPartition.Name);
                    if (OldPartition == null)
                    {
                        // The partition entry in the xml is also not present in the current partition table.
                        // It must have a known position and length.

                        if (NewPartition.LastSector == 0)
                        {
                            throw new WPinternalsException("Unknown length for partition \"" + NewPartition.Name + "\". The last sector property is set to 0 and the partition doesn't exist on the device currently.");
                        }
                    }
                    else
                    {
                        // The partition entry in the xml is also present in the current partition table.
                        // But since the partition is not present in the archive, the partition cannot be relocated.
                        // If the location of the new partition is specified, it must be the same as the current partition.

                        if ((NewPartition.FirstSector != 0) && (NewPartition.FirstSector != OldPartition.FirstSector))
                        {
                            throw new WPinternalsException("Incorrect location for partition \"" + NewPartition.Name + "\". A partition defined in the xml file got its boundaries updated, but as the partition isn't provided in the archive, it is not possible to relocate it.");
                        }

                        if ((NewPartition.LastSector != 0) && (NewPartition.LastSector != OldPartition.LastSector))
                        {
                            throw new WPinternalsException("Incorrect length for partition \"" + NewPartition.Name + "\". A partition defined in the xml file got its boundaries updated, but as the partition isn't provided in the archive, it is not possible to relocate it.");
                        }

                        NewPartition.FirstSector = OldPartition.FirstSector;
                        NewPartition.LastSector = OldPartition.LastSector;
                    }
                }
            }

            List<Partition> DynamicPartitions = new();
            if (Archive != null)
            {
                // Partitions which are present in the archive, and which have no start-sector in the new GPT data (dynamic relocation),
                // and which can be clustered to the end of emmc, are first removed from the existing GPT.
                IEnumerable<Partition> SortedPartitions = Partitions.OrderBy(p => p.FirstSector);
                for (int i = SortedPartitions.Count() - 1; i >= 0; i--)
                {
                    Partition OldPartition = SortedPartitions.ElementAt(i);

                    // Present in archive?
                    ZipArchiveEntry Entry = Archive.Entries.FirstOrDefault(e => string.Equals(e.Name, OldPartition.Name, StringComparison.CurrentCultureIgnoreCase) || e.Name.StartsWith(OldPartition.Name + ".", true, System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                    if (Entry != null)
                    {
                        // Not present in new GPT or present in GPT without FirstSector?
                        Partition NewPartition = GptToMerge.GetPartition(OldPartition.Name);
                        if ((NewPartition == null) || (NewPartition.FirstSector == 0))
                        {
                            DynamicPartitions.Insert(0, OldPartition);
                            this.Partitions.Remove(OldPartition);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // All partitions in the new GPT data should have a start-sector and end-sector by now.
            // The partitions in the new GPT data will be applied to the current partition-table.
            // Existing partitions, which are overwritten by the new partitions will be removed from the existing GPT.
            // Existing partition with the same name in the existing GPT is reused (guids and attribs remain, if not specified).
            UInt64 LowestSector = 0;
            Partition DPP = this.GetPartition("DPP");
            if (DPP != null)
            {
                LowestSector = DPP.LastSector + 1;
            }

            foreach (Partition NewPartition in GptToMerge.Partitions)
            {
                // If the new partition is a dynamic partition, then skip it here. It will be added later.
                if (DynamicPartitions.Select(p => p.Name).Any(n => string.Equals(n, NewPartition.Name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    continue;
                }

                // Sanity check
                if (NewPartition.FirstSector < LowestSector)
                {
                    throw new WPinternalsException("Bad sector alignment for partition: " + NewPartition.Name + ". The partition is located before DPP.");
                }

                Partition CurrentPartition = this.GetPartition(NewPartition.Name);
                if (CurrentPartition == null)
                {
                    CurrentPartition = new Partition
                    {
                        Name = NewPartition.Name
                    };
                    this.Partitions.Add(CurrentPartition);
                    HasChanged = true;
                }

                if ((NewPartition.FirstSector != 0) && (NewPartition.FirstSector != CurrentPartition.FirstSector))
                {
                    CurrentPartition.FirstSector = NewPartition.FirstSector;
                    HasChanged = true;
                }
                if ((NewPartition.LastSector != 0) && (NewPartition.LastSector != CurrentPartition.LastSector))
                {
                    CurrentPartition.LastSector = NewPartition.LastSector;
                    HasChanged = true;
                }
                if ((NewPartition.Attributes != 0) && (CurrentPartition.Attributes != NewPartition.Attributes))
                {
                    CurrentPartition.Attributes = NewPartition.Attributes;
                    HasChanged = true;
                }

                if ((NewPartition.PartitionGuid != Guid.Empty) || (NewPartition.PartitionGuid != CurrentPartition.PartitionGuid))
                {
                    HasChanged = true;
                }

                if (NewPartition.PartitionGuid != Guid.Empty)
                {
                    CurrentPartition.PartitionGuid = NewPartition.PartitionGuid;
                }

                if (CurrentPartition.PartitionGuid != Guid.Empty)
                {
                    CurrentPartition.PartitionGuid = Guid.NewGuid();
                }

                if ((NewPartition.PartitionTypeGuid != Guid.Empty) || (NewPartition.PartitionTypeGuid != CurrentPartition.PartitionTypeGuid))
                {
                    HasChanged = true;
                }

                if (NewPartition.PartitionTypeGuid != Guid.Empty)
                {
                    CurrentPartition.PartitionTypeGuid = NewPartition.PartitionTypeGuid;
                }

                if (CurrentPartition.PartitionTypeGuid != Guid.Empty)
                {
                    CurrentPartition.PartitionTypeGuid = Guid.NewGuid();
                }

                for (int i = this.Partitions.Count - 1; i >= 0; i--)
                {
                    if (this.Partitions[i] != CurrentPartition && (CurrentPartition.FirstSector <= this.Partitions[i].LastSector) && (CurrentPartition.LastSector >= this.Partitions[i].FirstSector))
                    {
                        this.Partitions.RemoveAt(i);
                        HasChanged = true;
                    }
                }
            }

            if (Archive != null)
            {
                // All partitions listed in the archive, which are present in the existing GPT, should overwrite the existing partition.
                // Check if the sizes of the partitions in the archive do not exceed the size of the partition, as listed in the GPT.
                foreach (Partition OldPartition in this.Partitions)
                {
                    ZipArchiveEntry Entry = Archive.Entries.FirstOrDefault(e => string.Equals(e.Name, OldPartition.Name, StringComparison.CurrentCultureIgnoreCase) || e.Name.StartsWith(OldPartition.Name + ".", true, System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                    if (Entry != null)
                    {
                        DecompressedStream DecompressedStream = new(Entry.Open());
                        ulong StreamLengthInSectors = (ulong)Entry.Length / 0x200;
                        try
                        {
                            StreamLengthInSectors = (ulong)DecompressedStream.Length / 0x200;
                        }
                        catch (Exception ex)
                        {
                            LogFile.LogException(ex, LogType.FileOnly);
                        }
                        DecompressedStream.Close();

                        UInt64 MaxPartitionSizeInSectors = OldPartition.SizeInSectors;
                        Partition NextPartition = this.Partitions.Where(p => p.FirstSector > OldPartition.FirstSector).OrderBy(p => p.FirstSector).FirstOrDefault();
                        if (NextPartition != null)
                        {
                            MaxPartitionSizeInSectors = NextPartition.FirstSector - OldPartition.FirstSector;
                        }

                        if (StreamLengthInSectors > MaxPartitionSizeInSectors)
                        {
                            throw new WPinternalsException("Incorrect length for partition \"" + OldPartition.Name + "\". The provided partition in the archive does not match the length specified in the xml file.");
                        }

                        if (OldPartition.SizeInSectors != StreamLengthInSectors)
                        {
                            OldPartition.SizeInSectors = StreamLengthInSectors;
                            HasChanged = true;
                        }
                    }
                }

                // All remaining partitions in the archive, which were listed in the original GPT, 
                // should be added at the end of the partition-table.
                UInt64 FirstFreeSector = 0x5000;
                if (this.Partitions.Count > 0)
                {
                    FirstFreeSector = this.Partitions.Max(p => p.LastSector) + 1;

                    // Always start a new partition on a new chunk (0x100 sector boundary), to be more flexible during custom flash
                    if (RoundToChunks && (((double)FirstFreeSector % 0x100) != 0))
                    {
                        FirstFreeSector = (UInt64)(Math.Ceiling((double)FirstFreeSector / 0x100) * 0x100);
                    }
                }
                foreach (Partition NewPartition in DynamicPartitions)
                {
                    if (NewPartition.FirstSector != FirstFreeSector)
                    {
                        NewPartition.FirstSector = FirstFreeSector;
                        HasChanged = true;
                    }
                    ZipArchiveEntry Entry = Archive.Entries.FirstOrDefault(e => string.Equals(e.Name, NewPartition.Name, StringComparison.CurrentCultureIgnoreCase) || e.Name.StartsWith(NewPartition.Name + ".", true, System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                    DecompressedStream DecompressedStream = new(Entry.Open());
                    ulong StreamLengthInSectors = (ulong)Entry.Length / 0x200;
                    try
                    {
                        StreamLengthInSectors = (ulong)DecompressedStream.Length / 0x200;
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }
                    DecompressedStream.Close();
                    if (NewPartition.SizeInSectors != StreamLengthInSectors)
                    {
                        NewPartition.SizeInSectors = StreamLengthInSectors;
                        HasChanged = true;
                    }
                    this.Partitions.Add(NewPartition);
                    FirstFreeSector += StreamLengthInSectors;

                    // Always start a new partition on a new chunk (0x100 sector boundary), to be more flexible during custom flash
                    if (RoundToChunks && (((double)FirstFreeSector % 0x100) != 0))
                    {
                        FirstFreeSector = (UInt64)(Math.Ceiling((double)FirstFreeSector / 0x100) * 0x100);
                    }
                }
            }

            Rebuild();
        }

        internal void WritePartitions(string Path)
        {
            string DirPath = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(DirPath) && !Directory.Exists(DirPath))
            {
                Directory.CreateDirectory(DirPath);
            }

            XmlSerializer x = new(typeof(GPT), "");

            XmlSerializerNamespaces ns = new();
            ns.Add("", "");
            StreamWriter FileWriter = new(Path);
            x.Serialize(FileWriter, this, ns);
            FileWriter.Close();
        }

        internal void WritePartitions(Stream Stream)
        {
            XmlSerializer x = new(typeof(GPT), "");

            XmlSerializerNamespaces ns = new();
            ns.Add("", "");
            x.Serialize(Stream, this, ns);
        }

        internal static GPT ReadPartitions(string Path)
        {
            XmlSerializer x = new(typeof(GPT), "");
            using FileStream s = new(Path, FileMode.Open);
            return (GPT)x.Deserialize(s);
        }

        internal void RestoreBackupPartitions()
        {
            // This is necessary, because the partitions and backup-partitions can exchange.
            // This may cause the startsector to be higher than the maximum allowed sector for flashing with a Lumia V1 programmer (hardcoded in programmer)
            foreach (string RevisePartitionName in (List<string>)(new(new string[] { "SBL1", "SBL2", "SBL3", "UEFI", "TZ", "RPM", "WINSECAPP" })))
            {
                Partition RevisePartition = GetPartition(RevisePartitionName);
                Partition ReviseBackupPartition = GetPartition("BACKUP_" + RevisePartitionName);
                if ((RevisePartition != null) && (ReviseBackupPartition != null) && (RevisePartition.FirstSector > ReviseBackupPartition.FirstSector))
                {
                    ulong OriginalFirstSector = RevisePartition.FirstSector;
                    ulong OriginalLastSector = RevisePartition.LastSector;
                    RevisePartition.FirstSector = ReviseBackupPartition.FirstSector;
                    RevisePartition.LastSector = ReviseBackupPartition.LastSector;
                    ReviseBackupPartition.FirstSector = OriginalFirstSector;
                    ReviseBackupPartition.LastSector = OriginalLastSector;

                    HasChanged = true;
                }

                if (RevisePartition.LastSector >= 0xF400)
                {
                    throw new WPinternalsException("Unsupported partition layout!", "The last sector of one of the BACKUP partitions defined in GPT exceeds the maximum threshold expected in order to restore BACKUP partitions to the device.");
                }
            }
        }
    }

    public class Partition
    {
        private UInt64 _SizeInSectors;
        private UInt64 _FirstSector;
        private UInt64 _LastSector;

        public string Name;            // 0x48
        public Guid PartitionTypeGuid; // 0x10
        public Guid PartitionGuid;     // 0x10
        [XmlIgnore]
        internal UInt64 Attributes;      // 0x08

        [XmlIgnore]
        internal UInt64 SizeInSectors
        {
            get
            {
                if (_SizeInSectors != 0)
                {
                    return _SizeInSectors;
                }
                else
                {
                    return LastSector - FirstSector + 1;
                }
            }
            set
            {
                _SizeInSectors = value;
                if (FirstSector != 0)
                {
                    LastSector = FirstSector + _SizeInSectors - 1;
                }
            }
        }

        [XmlIgnore]
        internal UInt64 FirstSector // 0x08
        {
            get
            {
                return _FirstSector;
            }
            set
            {
                _FirstSector = value;
                if (_SizeInSectors != 0)
                {
                    _LastSector = FirstSector + _SizeInSectors - 1;
                }
            }
        }

        [XmlIgnore]
        internal UInt64 LastSector // 0x08
        {
            get
            {
                return _LastSector;
            }
            set
            {
                _LastSector = value;
                _SizeInSectors = 0;
            }
        }

        [XmlIgnore]
        public string Volume
        {
            get
            {
                return @"\\?\Volume" + PartitionGuid.ToString("b") + @"\";
            }
        }

        [XmlElement(ElementName = "FirstSector")]
        public string FirstSectorAsString
        {
            get
            {
                return "0x" + FirstSector.ToString("X16");
            }
            set
            {
                FirstSector = Convert.ToUInt64(value, 16);
            }
        }

        [XmlElement(ElementName = "LastSector")]
        public string LastSectorAsString
        {
            get
            {
                return "0x" + LastSector.ToString("X16");
            }
            set
            {
                LastSector = Convert.ToUInt64(value, 16);
            }
        }

        [XmlElement(ElementName = "Attributes")]
        public string AttributesAsString
        {
            get
            {
                return "0x" + Attributes.ToString("X16");
            }
            set
            {
                Attributes = Convert.ToUInt64(value, 16);
            }
        }
    }
}
