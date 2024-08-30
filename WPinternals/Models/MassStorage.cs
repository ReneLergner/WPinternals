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
using System.Management;
using System.Runtime.InteropServices;

namespace WPinternals
{
    internal class MassStorage : NokiaPhoneModel
    {
        internal string Drive = null;
        internal string PhysicalDrive = null;
        internal string VolumeLabel = null;
        internal IntPtr hVolume = (IntPtr)(-1);
        internal IntPtr hDrive = (IntPtr)(-1);
        private bool OpenWithWriteAccess;

        private string Serial;

        internal MassStorage(string DevicePath) : base(DevicePath)
        {
            try
            {
                foreach (ManagementObject logical in new ManagementObjectSearcher("select * from Win32_LogicalDisk").Get())
                {
                    System.Diagnostics.Debug.Print(logical["Name"].ToString());

                    string Label = "";
                    foreach (ManagementObject partition in logical.GetRelated("Win32_DiskPartition"))
                    {
                        foreach (ManagementObject drive in partition.GetRelated("Win32_DiskDrive"))
                        {
                            if (drive["PNPDeviceID"].ToString().Contains("VEN_QUALCOMM&PROD_MMC_STORAGE", StringComparison.CurrentCulture) ||
                                drive["PNPDeviceID"].ToString().Contains("VEN_MSFT&PROD_PHONE_MMC_STOR", StringComparison.CurrentCulture))
                            {
                                Label = logical["VolumeName"] == null ? "" : logical["VolumeName"].ToString();
                                if ((Drive == null) || string.Equals(Label, "MainOS", StringComparison.CurrentCultureIgnoreCase)) // Always prefer the MainOS drive-mapping
                                {
                                    Drive = logical["Name"].ToString();
                                    PhysicalDrive = drive["DeviceID"].ToString();
                                    VolumeLabel = Label;
                                }
                                if (string.Equals(Label, "MainOS", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    break;
                                }
                            }
                        }
                        if (string.Equals(Label, "MainOS", StringComparison.CurrentCultureIgnoreCase))
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
            }
        }

        internal void AttachQualcommSerial(string DevicePath)
        {
            try
            {
                QualcommSerial SerialDevice = new(DevicePath);
                SerialDevice.Close();
                SerialDevice.Dispose();

                Serial = DevicePath;
            }
            catch (Exception ex)
            {
                LogFile.Log(ex.Message);
            }
        }

        internal bool DoesDeviceSupportReboot()
        {
            return Serial != null;
        }

        internal void Reboot()
        {
            if (Serial == null)
            {
                return;
            }

            try
            {
                QualcommSerial SerialDevice = new(Serial);

                SerialDevice.EncodeCommands = false;

                // This will succeed on new models
                SerialDevice.SendData([0x7, 0x0, 0x0, 0x0, 0x8, 0x0, 0x0, 0x0]);

                // This will succeed on old models
                SerialDevice.SendData([0x7E, 0xA, 0x0, 0x0, 0xB6, 0xB5, 0x7E]);

                SerialDevice.Close();
                SerialDevice.Dispose();
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return;
            }

            if (disposing)
            {
                CloseVolume();

                base.Dispose(disposing);
            }
        }

        internal void OpenVolume(bool WriteAccess)
        {
            OpenWithWriteAccess = false;

            if (IsVolumeOpen())
            {
                throw new Exception("Volume already opened");
            }

            if (WriteAccess)
            {
                // Unmounting the volume does not have the desired effect.
                // It does not unmount the mountpoints on the phone.
                // So the sectors of the filesystems of EFIESP, Data, etc cannot be written.
                // Unmounting the mounting points would alter the NTFS structure, which is also an undesired effect.
                // Restoring partitions with file-systems can better be done using Flash mode!

                // Open volume
                hVolume = NativeMethods.CreateFile(
                    PhysicalDrive,
                    NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OPEN_EXISTING,
                    NativeMethods.FILE_FLAG_WRITE_THROUGH | NativeMethods.FILE_FLAG_NO_BUFFERING, // !!!
                    IntPtr.Zero);
            }
            else
            {
                hVolume = NativeMethods.CreateFile(
                    // @"\\.\" + Drive,
                    PhysicalDrive,
                    NativeMethods.GENERIC_READ,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    NativeMethods.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);
            }

            if ((int)hVolume == -1)
            {
                throw new Exception(Marshal.GetLastWin32Error().ToString());
            }

            OpenWithWriteAccess = WriteAccess;
        }

        internal bool IsVolumeOpen()
        {
            return (int)hVolume != -1;
        }

        internal void CloseVolume()
        {
            if ((int)hDrive != -1)
            {
                NativeMethods.CloseHandle(hDrive); // This reloads the logical drive!!
                hDrive = new IntPtr(-1);
            }

            if ((int)hVolume != -1)
            {
                NativeMethods.CloseHandle(hVolume);
                hVolume = new IntPtr(-1);
            }
        }

        internal void SetSectorPosition(UInt64 Sector)
        {
            if (!IsVolumeOpen())
            {
                throw new Exception("Volume is not opened");
            }

            int High = (int)(Sector >> (32 - 9));
            NativeMethods.SetFilePointer(hVolume, (int)(Sector << 9), ref High, EMoveMethod.Begin);
        }

        internal void WriteSector(byte[] buffer)
        {
            if (!IsVolumeOpen())
            {
                throw new Exception("Volume is not opened");
            }

            if (!OpenWithWriteAccess)
            {
                throw new Exception("Volume is not opened with Write Acces");
            }

            bool result = NativeMethods.WriteFile(hVolume, buffer, 512, out uint count, IntPtr.Zero);
            if (!result)
            {
                throw new Exception("Exception: 0x" + Marshal.GetLastWin32Error().ToString("X8"));
            }
        }

        internal void ReadSector(byte[] buffer)
        {
            if (!IsVolumeOpen())
            {
                throw new Exception("Volume is not opened");
            }

            bool result = NativeMethods.ReadFile(hVolume, buffer, 512, out uint count, IntPtr.Zero);
            if (!result)
            {
                throw new Exception("Exception: 0x" + Marshal.GetLastWin32Error().ToString("X8"));
            }
        }

        internal void ReadSectors(byte[] buffer, out uint ActualSectorsRead, uint SizeInSectors = uint.MaxValue)
        {
            if (!IsVolumeOpen())
            {
                throw new Exception("Volume is not opened");
            }

            bool Result = NativeMethods.ReadFile(hVolume, buffer, (SizeInSectors * 0x200) < buffer.Length ? (SizeInSectors * 0x200) : (uint)buffer.Length, out uint count, IntPtr.Zero);
            ActualSectorsRead = count / 0x200;
            if (!Result)
            {
                throw new Exception("Failed to read sectors. Exception: 0x" + Marshal.GetLastWin32Error().ToString("X8"));
            }
        }

        internal byte[] ReadSectors(UInt64 StartSector, UInt64 SectorCount)
        {
            byte[] Result = new byte[SectorCount * 0x200];

            bool VolumeWasOpen = IsVolumeOpen();
            if (!VolumeWasOpen)
            {
                OpenVolume(false);
            }

            SetSectorPosition(StartSector);
            ReadSectors(Result, out uint SectorsRead);

            if (!VolumeWasOpen)
            {
                CloseVolume();
            }

            if (Result == null)
            {
                throw new Exception("Failed to read from phone");
            }

            return Result;
        }

        internal void DumpSectors(UInt64 StartSector, UInt64 SectorCount, string Path)
        {
            DumpSectors(StartSector, SectorCount, Path, null, null, null);
        }

        internal void DumpSectors(UInt64 StartSector, UInt64 SectorCount, string Path, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            DumpSectors(StartSector, SectorCount, Path, null, ProgressUpdateCallback, null);
        }

        internal void DumpSectors(UInt64 StartSector, UInt64 SectorCount, string Path, ProgressUpdater UpdaterPerSector)
        {
            DumpSectors(StartSector, SectorCount, Path, null, null, UpdaterPerSector);
        }

        internal void DumpSectors(UInt64 StartSector, UInt64 SectorCount, Stream OutputStream)
        {
            DumpSectors(StartSector, SectorCount, null, OutputStream, null, null);
        }

        internal void DumpSectors(UInt64 StartSector, UInt64 SectorCount, Stream OutputStream, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            DumpSectors(StartSector, SectorCount, null, OutputStream, ProgressUpdateCallback, null);
        }

        internal void DumpSectors(UInt64 StartSector, UInt64 SectorCount, Stream OutputStream, ProgressUpdater UpdaterPerSector)
        {
            DumpSectors(StartSector, SectorCount, null, OutputStream, null, UpdaterPerSector);
        }

        private void DumpSectors(UInt64 StartSector, UInt64 SectorCount, string Path, Stream OutputStream, Action<int, TimeSpan?> ProgressUpdateCallback, ProgressUpdater UpdaterPerSector)
        {
            bool VolumeWasOpen = IsVolumeOpen();
            if (!VolumeWasOpen)
            {
                OpenVolume(false);
            }

            SetSectorPosition(StartSector);
            ProgressUpdater Progress = UpdaterPerSector;
            if ((Progress == null) && (ProgressUpdateCallback != null))
            {
                Progress = new ProgressUpdater(SectorCount, ProgressUpdateCallback);
            }

            byte[] Buffer = SectorCount >= 0x80 ? (new byte[0x10000]) : (new byte[SectorCount * 0x200]);

            Stream Stream = Path == null ? OutputStream : File.Open(Path, FileMode.Create);
            using (BinaryWriter Writer = new(Stream))
            {
                for (UInt64 i = 0; i < SectorCount; i += 0x80)
                {
                    // TODO: Reading sectors and writing to compressed stream should be on different threads.
                    // Backup of 3 partitions without compression takes about 40 minutes.
                    // Backup of same partitions with compression takes about 70 minutes.
                    // Separation reading and compression could potentially speed up a lot.
                    // BinaryWriter doesnt support async.
                    // Calling async directly on the EntryStream of a Ziparchive blocks.

                    ReadSectors(Buffer, out uint ActualSectorsRead, (SectorCount - i) >= 0x80 ? 0x80 : (uint)(SectorCount - i));
                    Writer.Write(Buffer, 0, (int)ActualSectorsRead * 0x200);
                    Progress?.IncreaseProgress(ActualSectorsRead);
                }
                Stream.Flush();
            }

            if (!VolumeWasOpen)
            {
                CloseVolume();
            }
        }

        internal void BackupPartition(string PartitionName, string Path)
        {
            BackupPartition(PartitionName, Path, null, null, null);
        }

        internal void BackupPartition(string PartitionName, string Path, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            BackupPartition(PartitionName, Path, null, ProgressUpdateCallback, null);
        }

        internal void BackupPartition(string PartitionName, string Path, ProgressUpdater UpdaterPerSector)
        {
            BackupPartition(PartitionName, Path, null, null, UpdaterPerSector);
        }

        internal void BackupPartition(string PartitionName, Stream OutputStream)
        {
            BackupPartition(PartitionName, null, OutputStream, null, null);
        }

        internal void BackupPartition(string PartitionName, Stream OutputStream, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            BackupPartition(PartitionName, null, OutputStream, ProgressUpdateCallback, null);
        }

        internal void BackupPartition(string PartitionName, Stream OutputStream, ProgressUpdater UpdaterPerSector)
        {
            BackupPartition(PartitionName, null, OutputStream, null, UpdaterPerSector);
        }

        private void BackupPartition(string PartitionName, string Path, Stream OutputStream, Action<int, TimeSpan?> ProgressUpdateCallback, ProgressUpdater UpdaterPerSector)
        {
            bool VolumeWasOpen = IsVolumeOpen();
            if (!VolumeWasOpen)
            {
                OpenVolume(false);
            }

            SetSectorPosition(1);
            byte[] GPTBuffer = ReadSectors(1, 33);
            GPT GPT = new(GPTBuffer);
            Partition Partition = GPT.Partitions.First((p) => p.Name == PartitionName);

            DumpSectors(Partition.FirstSector, Partition.LastSector - Partition.FirstSector + 1, Path, OutputStream, ProgressUpdateCallback, UpdaterPerSector);

            if (!VolumeWasOpen)
            {
                CloseVolume();
            }
        }

        internal void WriteSectors(UInt64 StartSector, byte[] Buffer)
        {
            bool Result = true;

            bool VolumeWasOpen = IsVolumeOpen();
            if (!VolumeWasOpen)
            {
                OpenVolume(true);
            }

            SetSectorPosition(StartSector);

            Result = NativeMethods.WriteFile(hVolume, Buffer, (uint)Buffer.Length, out uint count, IntPtr.Zero);

            if (!VolumeWasOpen)
            {
                CloseVolume();
            }

            if (!Result)
            {
                throw new Exception("Failed to write sectors.");
            }
        }

        internal void WriteSectors(byte[] Buffer, uint Length = uint.MaxValue)
        {
            if (!IsVolumeOpen())
            {
                throw new Exception("Volume is not opened");
            }

            bool Result = NativeMethods.WriteFile(hVolume, Buffer, Length < Buffer.Length ? Length : (uint)Buffer.Length, out uint count, IntPtr.Zero);
            if (!Result)
            {
                throw new Exception("Failed to write sectors. Exception: 0x" + Marshal.GetLastWin32Error().ToString("X8"));
            }
        }

        internal void WriteSectors(UInt64 StartSector, string Path)
        {
            WriteSectors(StartSector, Path, null, null);
        }

        internal void WriteSectors(UInt64 StartSector, string Path, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            WriteSectors(StartSector, Path, ProgressUpdateCallback, null);
        }

        internal void WriteSectors(UInt64 StartSector, string Path, ProgressUpdater UpdaterPerSector)
        {
            WriteSectors(StartSector, Path, null, UpdaterPerSector);
        }

        private void WriteSectors(UInt64 StartSector, string Path, Action<int, TimeSpan?> ProgressUpdateCallback, ProgressUpdater UpdaterPerSector)
        {
            bool VolumeWasOpen = IsVolumeOpen();
            if (!VolumeWasOpen)
            {
                OpenVolume(true);
            }

            SetSectorPosition(StartSector);

            byte[] Buffer;

            using (BinaryReader Reader = new(File.Open(Path, FileMode.Open)))
            {
                ProgressUpdater Progress = UpdaterPerSector;
                if ((Progress == null) && (ProgressUpdateCallback != null))
                {
                    Progress = new ProgressUpdater((UInt64)(Reader.BaseStream.Length / 0x200), ProgressUpdateCallback);
                }

                Buffer = Reader.BaseStream.Length >= 0x10000 ? (new byte[0x10000]) : (new byte[Reader.BaseStream.Length]);

                int Count;
                for (UInt64 i = 0; i < (UInt64)(Reader.BaseStream.Length / 0x200); i += 0x80)
                {
                    Count = Reader.Read(Buffer, 0, Buffer.Length);

                    WriteSectors(Buffer, (uint)Count);

                    Progress?.IncreaseProgress((ulong)Count / 0x200);
                }
            }

            if (!VolumeWasOpen)
            {
                CloseVolume();
            }
        }

        internal void RestorePartition(string Path, string PartitionName)
        {
            RestorePartition(Path, PartitionName, null, null);
        }

        internal void RestorePartition(string Path, string PartitionName, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            RestorePartition(Path, PartitionName, ProgressUpdateCallback, null);
        }

        internal void RestorePartition(string Path, string PartitionName, ProgressUpdater UpdaterPerSector)
        {
            RestorePartition(Path, PartitionName, null, UpdaterPerSector);
        }

        private void RestorePartition(string Path, string PartitionName, Action<int, TimeSpan?> ProgressUpdateCallback, ProgressUpdater UpdaterPerSector)
        {
            bool VolumeWasOpen = IsVolumeOpen();
            if (!VolumeWasOpen)
            {
                OpenVolume(true);
            }

            SetSectorPosition(1);
            byte[] GPTBuffer = ReadSectors(1, 33);
            GPT GPT = new(GPTBuffer);
            Partition Partition = GPT.Partitions.First((p) => p.Name == PartitionName);
            ulong PartitionSize = (Partition.LastSector - Partition.FirstSector + 1) * 0x200;
            ulong FileSize = (ulong)new FileInfo(Path).Length;
            if (FileSize > PartitionSize)
            {
                throw new InvalidOperationException("Partition can not be restored, because its size is too big!");
            }

            WriteSectors(Partition.FirstSector, Path, ProgressUpdateCallback);

            if (!VolumeWasOpen)
            {
                CloseVolume();
            }
        }
    }
}
