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
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace WPinternals
{
    internal class BackupViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel PhoneNotifier;
        private readonly Action Callback;
        private readonly Action SwitchToUnlockBoot;

        private readonly static string[] KnownOSPartitions =
        [
            "BDP",
            "BOOT_EFI",
            "BSP",
            "CrashDump",
            "Data",
            "EFIESP_MUNGED",
            "EFIESP", // Can bring issues when partition is meant for test devices, breaking end user fash app, need code to ensure this won't happen or user can recover if possible
            "HACK_EFIESP_MUNGED",
            "HACK_EFIESP",
            "IU_RESERVE",
            "MainOS",
            "MMOS",
            "OSData",
            "OSPool",
            "PLAT", // Can bring issues when PLATID differs, need code to ensure it matches or user can recover if different
            "PreInstalled",
            "SERVICING_METADATA",
            "TEST-UI-MARKER",
            "VIRT_EFIESP",
            "WSP"
        ];

        private readonly static string[] ProvisioningPartitions =
        [
            "APDP",
            "BOOTMODE",
            "DBI",
            "DDR",
            "DPO",
            "DPP",
            "LIMITS",
            "MODEM_FS1",
            "MODEM_FS2",
            "MODEM_FSC",
            "MODEM_FSG",
            "MSADP",
            "SEC",
            "SSD",
            "UEFI_BS_NV",
            "UEFI_NV",
            "UEFI_RT_NV_RPMB",
            "UEFI_RT_NV"
        ];

        internal BackupViewModel(PhoneNotifierViewModel PhoneNotifier, Action SwitchToUnlockBoot, Action Callback)
            : base()
        {
            IsFlashModeOperation = true;

            this.PhoneNotifier = PhoneNotifier;
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;
            this.Callback = Callback;
        }

        internal override void EvaluateViewState()
        {
            if (!IsActive)
            {
                return;
            }

            if (SubContextViewModel == null)
            {
                ActivateSubContext(new BackupTargetSelectionViewModel(PhoneNotifier, SwitchToUnlockBoot, DoBackupArchive, DoBackup, DoBackupArchiveProvisioning));
                IsSwitchingInterface = false;
            }

            if (SubContextViewModel is BackupTargetSelectionViewModel)
            {
                ((BackupTargetSelectionViewModel)SubContextViewModel).EvaluateViewState();
            }
        }

        internal async void DoBackup(string EFIESPPath, string MainOSPath, string DataPath)
        {
            try
            {
                IsSwitchingInterface = true;
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_MassStorage,
                    (msg, sub) => ActivateSubContext(new BusyViewModel(msg, sub)));
                BackupTask(EFIESPPath, MainOSPath, DataPath);
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
            }
        }

        internal async void DoBackupArchive(string ArchivePath)
        {
            try
            {
                IsSwitchingInterface = true;
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_MassStorage,
                    (msg, sub) => ActivateSubContext(new BusyViewModel(msg, sub)));
                BackupArchiveTask(ArchivePath);
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
            }
        }

        internal async void DoBackupArchiveProvisioning(string ArchiveProvisioningPath)
        {
            try
            {
                IsSwitchingInterface = true;
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_MassStorage,
                    (msg, sub) => ActivateSubContext(new BusyViewModel(msg, sub)));
                BackupArchiveProvisioningTask(ArchiveProvisioningPath);
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
            }
        }

        internal void BackupTask(string EFIESPPath, string MainOSPath, string DataPath)
        {
            IsSwitchingInterface = false;
            new Thread(() =>
                {
                    bool Result = true;

                    ActivateSubContext(new BusyViewModel("Initializing backup..."));

                    ulong TotalSizeSectors = 0;
                    int PartitionCount = 0;

                    MassStorage Phone = (MassStorage)PhoneNotifier.CurrentModel;

                    Phone.OpenVolume(false);
                    byte[] GPTBuffer = Phone.ReadSectors(1, 33);
                    GPT GPT = new(GPTBuffer);
                    Partition Partition;
                    try
                    {
                        if (EFIESPPath != null)
                        {
                            Partition = GPT.Partitions.First(p => p.Name == "EFIESP");
                            TotalSizeSectors += Partition.SizeInSectors;
                            PartitionCount++;
                        }

                        if (MainOSPath != null)
                        {
                            Partition = GPT.Partitions.First(p => p.Name == "MainOS");
                            TotalSizeSectors += Partition.SizeInSectors;
                            PartitionCount++;
                        }

                        if (DataPath != null)
                        {
                            Partition = GPT.Partitions.First(p => p.Name == "Data");
                            TotalSizeSectors += Partition.SizeInSectors;
                            PartitionCount++;
                        }
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        Result = false;
                    }

                    BusyViewModel Busy = new("Create backup...", MaxProgressValue: TotalSizeSectors, UIContext: UIContext);
                    ProgressUpdater Updater = Busy.ProgressUpdater;
                    ActivateSubContext(Busy);

                    int i = 0;
                    if (Result)
                    {
                        try
                        {
                            if (EFIESPPath != null)
                            {
                                i++;
                                Busy.Message = "Create backup of partition EFIESP (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.BackupPartition("EFIESP", EFIESPPath, Updater);
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }
                    }

                    if (Result)
                    {
                        try
                        {
                            if (MainOSPath != null)
                            {
                                i++;
                                Busy.Message = "Create backup of partition MainOS (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.BackupPartition("MainOS", MainOSPath, Updater);
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }
                    }

                    if (Result)
                    {
                        try
                        {
                            if (DataPath != null)
                            {
                                i++;
                                Busy.Message = "Create backup of partition Data (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.BackupPartition("Data", DataPath, Updater);
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }
                    }

                    Phone.CloseVolume();

                    if (!Result)
                    {
                        ActivateSubContext(new MessageViewModel("Failed to create backup!", Exit));
                        return;
                    }

                    ActivateSubContext(new MessageViewModel("Successfully created a backup!", Exit));
                }).Start();
        }

        internal void BackupArchiveTask(string ArchivePath)
        {
            IsSwitchingInterface = false;
            new Thread(() =>
            {
                bool Result = true;

                ActivateSubContext(new BusyViewModel("Initializing backup..."));

                ulong TotalSizeSectors = 0;

                MassStorage Phone = (MassStorage)PhoneNotifier.CurrentModel;

                try
                {
                    Phone.OpenVolume(false);
                    byte[] GPTBuffer = Phone.ReadSectors(1, 33);
                    GPT GPT = new(GPTBuffer);

                    Partition[] Partitions = GPT.Partitions.Where(p => KnownOSPartitions.Any(x => x == p.Name)).ToArray();
                    int PartitionCount = Partitions.Length;

                    try
                    {
                        foreach (Partition Partition in Partitions)
                        {
                            TotalSizeSectors += Partition.SizeInSectors;
                        }
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        Result = false;
                    }

                    BusyViewModel Busy = new("Create backup...", MaxProgressValue: TotalSizeSectors, UIContext: UIContext);
                    ProgressUpdater Updater = Busy.ProgressUpdater;
                    ActivateSubContext(Busy);
                    ZipArchiveEntry Entry;
                    Stream EntryStream = null;

                    using FileStream FileStream = new(ArchivePath, FileMode.Create);
                    using ZipArchive Archive = new(FileStream, ZipArchiveMode.Create);

                    if (Result)
                    {
                        try
                        {
                            Entry = Archive.CreateEntry("Partitions.xml", CompressionLevel.Optimal);
                            EntryStream = Entry.Open();
                            Busy.Message = "Create backup of partition tablle";
                            GPT.WritePartitions(EntryStream);
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }
                        finally
                        {
                            EntryStream?.Close();
                            EntryStream = null;
                        }
                    }

                    for (int i = 0; i < PartitionCount; i++)
                    {
                        string Partition = Partitions[i].Name;
                        if (Result)
                        {
                            try
                            {
                                Entry = Archive.CreateEntry(Partition + ".bin", CompressionLevel.Optimal);
                                EntryStream = Entry.Open();
                                Busy.Message = "Create backup of partition " + Partition + " (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.BackupPartition(Partition, EntryStream, Updater);
                            }
                            catch (Exception Ex)
                            {
                                LogFile.LogException(Ex);
                                Result = false;
                            }
                            finally
                            {
                                EntryStream?.Close();
                                EntryStream = null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
                finally
                {
                    Phone.CloseVolume();
                }

                if (!Result)
                {
                    ActivateSubContext(new MessageViewModel("Failed to create backup!", Exit));
                    return;
                }

                ActivateSubContext(new MessageViewModel("Successfully created a backup!", Exit));
            }).Start();
        }

        internal void BackupArchiveProvisioningTask(string ArchiveProvisioningPath)
        {
            IsSwitchingInterface = false;
            new Thread(() =>
            {
                bool Result = true;

                ActivateSubContext(new BusyViewModel("Initializing backup..."));

                ulong TotalSizeSectors = 0;
                int PartitionCount = 0;

                MassStorage Phone = (MassStorage)PhoneNotifier.CurrentModel;

                try
                {
                    Phone.OpenVolume(false);
                    byte[] GPTBuffer = Phone.ReadSectors(1, 33);
                    GPT GPT = new(GPTBuffer);

                    Partition Partition;

                    try
                    {
                        foreach (string PartitionName in ProvisioningPartitions)
                        {
                            if (GPT.Partitions.Any(p => p.Name == PartitionName))
                            {
                                Partition = GPT.Partitions.First(p => p.Name == PartitionName);
                                if (PartitionName == "UEFI_BS_NV" && GPT.Partitions.Any(p => p.Name == "BACKUP_BS_NV"))
                                {
                                    Partition = GPT.Partitions.First(p => p.Name == "BACKUP_BS_NV");
                                }

                                TotalSizeSectors += Partition.SizeInSectors;
                                PartitionCount++;
                            }
                        }
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        Result = false;
                    }

                    BusyViewModel Busy = new("Create backup...", MaxProgressValue: TotalSizeSectors, UIContext: UIContext);
                    ProgressUpdater Updater = Busy.ProgressUpdater;
                    ActivateSubContext(Busy);
                    ZipArchiveEntry Entry;
                    Stream EntryStream = null;

                    using FileStream FileStream = new(ArchiveProvisioningPath, FileMode.Create);
                    using ZipArchive Archive = new(FileStream, ZipArchiveMode.Create);
                    int i = 0;

                    foreach (string PartitionName in ProvisioningPartitions)
                    {
                        if (GPT.Partitions.Any(p => p.Name == PartitionName) && Result)
                        {
                            try
                            {
                                Entry = Archive.CreateEntry(PartitionName + ".bin", CompressionLevel.Optimal);
                                EntryStream = Entry.Open();
                                i++;
                                Busy.Message = "Create backup of partition " + PartitionName + " (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                if (PartitionName == "UEFI_BS_NV" && GPT.Partitions.Any(p => p.Name == "BACKUP_BS_NV"))
                                {
                                    Phone.BackupPartition("BACKUP_BS_NV", EntryStream, Updater);
                                }
                                else
                                {
                                    Phone.BackupPartition(PartitionName, EntryStream, Updater);
                                }
                            }
                            catch (Exception Ex)
                            {
                                LogFile.LogException(Ex);
                                Result = false;
                            }
                            finally
                            {
                                EntryStream?.Close();
                                EntryStream = null;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
                finally
                {
                    Phone.CloseVolume();
                }

                if (!Result)
                {
                    ActivateSubContext(new MessageViewModel("Failed to create backup!", Exit));
                    return;
                }

                ActivateSubContext(new MessageViewModel("Successfully created a backup!", Exit));
            }).Start();
        }

        private void Exit()
        {
            IsSwitchingInterface = false;
            ActivateSubContext(null);
            Callback();
        }
    }
}
