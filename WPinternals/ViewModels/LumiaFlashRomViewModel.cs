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
using System.Threading;
using System.Threading.Tasks;

namespace WPinternals
{
    internal class LumiaFlashRomViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel PhoneNotifier;
        internal Action SwitchToUnlockBoot;
        internal Action SwitchToUnlockRoot;
        internal Action SwitchToDumpFFU;
        internal Action SwitchToBackup;
        private readonly Action Callback;

        internal LumiaFlashRomViewModel(PhoneNotifierViewModel PhoneNotifier, Action SwitchToUnlockBoot, Action SwitchToUnlockRoot, Action SwitchToDumpFFU, Action SwitchToBackup, Action Callback)
            : base()
        {
            IsSwitchingInterface = false;
            IsFlashModeOperation = true;

            this.PhoneNotifier = PhoneNotifier;
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;
            this.SwitchToUnlockRoot = SwitchToUnlockRoot;
            this.SwitchToDumpFFU = SwitchToDumpFFU;
            this.SwitchToBackup = SwitchToBackup;
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
                ActivateSubContext(new LumiaFlashRomSourceSelectionViewModel(PhoneNotifier, SwitchToUnlockBoot, SwitchToUnlockRoot, SwitchToDumpFFU, SwitchToBackup, FlashPartitions, FlashArchive, FlashFFU, FlashMMOS));
            }
        }

        // Called from an event-handler. So, "async void" is valid here.
        internal async void FlashPartitions(string EFIESPPath, string MainOSPath, string DataPath)
        {
            IsSwitchingInterface = true; // Prevents that a device is forced to Flash mode on this screen which is meant for flashing
            try
            {
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_Flash,
                    (msg, sub) =>
                        ActivateSubContext(new BusyViewModel(msg, sub)));
                if (((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo(ExtendedInfo: false).FlashAppProtocolVersionMajor < 2)
                {
                    FlashPartitionsTask(EFIESPPath, MainOSPath, DataPath);
                }
                else
                {
                    await Task.Run(async () => await LumiaV2UnlockBootViewModel.LumiaV2FlashPartitions(PhoneNotifier, EFIESPPath, MainOSPath, DataPath, SetWorkingStatus, UpdateWorkingStatus, ExitSuccess, ExitFailure));
                }
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
            }
        }

        internal void FlashPartitionsTask(string EFIESPPath, string MainOSPath, string DataPath)
        {
            new Thread(() =>
                {
                    bool Result = true;

                    ActivateSubContext(new BusyViewModel("Initializing flash..."));

                    LumiaFlashAppModel Phone = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;

                    GPT GPT = Phone.ReadGPT();

                    ulong TotalSizeSectors = 0;
                    int PartitionCount = 0;
                    ulong MainOSOldSectorCount = 0;
                    ulong MainOSNewSectorCount = 0;
                    ulong DataOldSectorCount = 0;
                    ulong DataNewSectorCount = 0;
                    ulong FirstMainOSSector = 0;

                    try
                    {
                        if (EFIESPPath != null)
                        {
                            using Stream Stream = new DecompressedStream(File.Open(EFIESPPath, FileMode.Open));
                            ulong StreamLengthInSectors = (ulong)Stream.Length / 0x200;
                            TotalSizeSectors += StreamLengthInSectors;
                            PartitionCount++;
                            Partition Partition = GPT.Partitions.Find(p => string.Equals(p.Name, "EFIESP", StringComparison.CurrentCultureIgnoreCase));
                            if (StreamLengthInSectors > Partition.SizeInSectors)
                            {
                                LogFile.Log("Flash failed! Size of partition 'EFIESP' is too big.");
                                ExitFailure("Flash failed!", "Size of partition 'EFIESP' is too big.");
                                return;
                            }
                        }

                        if (MainOSPath != null)
                        {
                            using Stream Stream = new DecompressedStream(File.Open(MainOSPath, FileMode.Open));
                            ulong StreamLengthInSectors = (ulong)Stream.Length / 0x200;
                            TotalSizeSectors += StreamLengthInSectors;
                            PartitionCount++;
                            Partition Partition = GPT.Partitions.Find(p => string.Equals(p.Name, "MainOS", StringComparison.CurrentCultureIgnoreCase));
                            MainOSOldSectorCount = Partition.SizeInSectors;
                            MainOSNewSectorCount = StreamLengthInSectors;
                            FirstMainOSSector = Partition.FirstSector;
                        }

                        if (DataPath != null)
                        {
                            using Stream Stream = new DecompressedStream(File.Open(DataPath, FileMode.Open));
                            ulong StreamLengthInSectors = (ulong)Stream.Length / 0x200;
                            TotalSizeSectors += StreamLengthInSectors;
                            PartitionCount++;
                            Partition Partition = GPT.Partitions.Find(p => string.Equals(p.Name, "Data", StringComparison.CurrentCultureIgnoreCase));
                            DataOldSectorCount = Partition.SizeInSectors;
                            DataNewSectorCount = StreamLengthInSectors;
                        }
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        Result = false;
                    }

                    if ((MainOSNewSectorCount > 0) && (DataNewSectorCount > 0))
                    {
                        if ((MainOSNewSectorCount > MainOSOldSectorCount) || (DataNewSectorCount > DataOldSectorCount))
                        {
                            UInt64 OSSpace = GPT.LastUsableSector - FirstMainOSSector + 1;
                            if ((MainOSNewSectorCount + DataNewSectorCount) <= OSSpace)
                            {
                                // MainOS and Data partitions need to be re-aligned!
                                Partition MainOSPartition = GPT.Partitions.Single(p => string.Equals(p.Name, "MainOS", StringComparison.CurrentCultureIgnoreCase));
                                Partition DataPartition = GPT.Partitions.Single(p => string.Equals(p.Name, "Data", StringComparison.CurrentCultureIgnoreCase));
                                MainOSPartition.LastSector = MainOSPartition.FirstSector + MainOSNewSectorCount - 1;
                                DataPartition.FirstSector = MainOSPartition.LastSector + 1;
                                DataPartition.LastSector = DataPartition.FirstSector + DataNewSectorCount - 1;
                                Phone.WriteGPT(GPT);
                            }
                            else
                            {
                                LogFile.Log("Flash failed! Size of partitions 'MainOS' and 'Data' together are too big.");
                                ExitFailure("Flash failed!", "Sizes of partitions 'MainOS' and 'Data' together are too big.");
                                return;
                            }
                        }
                    }
                    else if ((MainOSNewSectorCount > 0) && (MainOSNewSectorCount > MainOSOldSectorCount))
                    {
                        LogFile.Log("Flash failed! Size of partition 'MainOS' is too big.");
                        ExitFailure("Flash failed!", "Size of partition 'MainOS' is too big.");
                        return;
                    }
                    else if ((DataNewSectorCount > 0) && (DataNewSectorCount > DataOldSectorCount))
                    {
                        LogFile.Log("Flash failed! Size of partition 'Data' is too big.");
                        ExitFailure("Flash failed!", "Size of partition 'Data' together is too big.");
                        return;
                    }

                    BusyViewModel Busy = new("Flashing...", MaxProgressValue: TotalSizeSectors, UIContext: UIContext);
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
                                Busy.Message = "Flashing partition EFIESP (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.FlashRawPartition(EFIESPPath, "EFIESP", Updater);
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
                                Busy.Message = "Flashing partition MainOS (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.FlashRawPartition(MainOSPath, "MainOS", Updater);
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
                                Busy.Message = "Flashing partition Data (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                Phone.FlashRawPartition(DataPath, "Data", Updater);
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            Result = false;
                        }
                    }

                    if (!Result)
                    {
                        ExitFailure("Flash failed!", null);
                        return;
                    }

                    ExitSuccess("Flash successful! Make sure you disable Windows Update on the phone!", null);
                }).Start();
        }

        // Called from an event-handler. So, "async void" is valid here.
        internal async void FlashArchive(string ArchivePath)
        {
            IsSwitchingInterface = true; // Prevents that a device is forced to Flash mode on this screen which is meant for flashing
            try
            {
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_Flash,
                    (msg, sub) =>
                        ActivateSubContext(new BusyViewModel(msg, sub)));
                if (((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo(ExtendedInfo: false).FlashAppProtocolVersionMajor < 2)
                {
                    FlashArchiveTask(ArchivePath);
                }
                else
                {
                    await Task.Run(async () => await LumiaV2UnlockBootViewModel.LumiaV2FlashArchive(PhoneNotifier, ArchivePath, SetWorkingStatus, UpdateWorkingStatus, ExitSuccess, ExitFailure));
                }
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
            }
        }

        internal void FlashArchiveTask(string ArchivePath)
        {
            new Thread(() =>
            {
                ActivateSubContext(new BusyViewModel("Initializing flash..."));

                LumiaFlashAppModel Phone = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;

                ulong TotalSizeSectors = 0;
                int PartitionCount = 0;
                ulong MainOSOldSectorCount = 0;
                ulong MainOSNewSectorCount = 0;
                ulong DataOldSectorCount = 0;
                ulong DataNewSectorCount = 0;
                ulong FirstMainOSSector = 0;
                bool GPTChanged = false;

                try
                {
                    GPT GPT = Phone.ReadGPT();

                    using FileStream FileStream = new(ArchivePath, FileMode.Open);
                    using ZipArchive Archive = new(FileStream, ZipArchiveMode.Read);
                    foreach (ZipArchiveEntry Entry in Archive.Entries)
                    {
                        // Determine if there is a partition layout present
                        ZipArchiveEntry PartitionEntry = Archive.GetEntry("Partitions.xml");
                        if (PartitionEntry == null)
                        {
                            GPT.MergePartitions(null, false, Archive);
                            GPTChanged |= GPT.HasChanged;
                        }
                        else
                        {
                            using Stream ZipStream = PartitionEntry.Open();
                            using StreamReader ZipReader = new(ZipStream);
                            string PartitionXml = ZipReader.ReadToEnd();
                            GPT.MergePartitions(PartitionXml, false, Archive);
                            GPTChanged |= GPT.HasChanged;
                        }

                        // First determine if we need a new GPT!
                        if (!Entry.FullName.Contains("/")) // No subfolders
                        {
                            string PartitionName = Path.GetFileNameWithoutExtension(Entry.Name);
                            int P = PartitionName.IndexOf('.');
                            if (P >= 0)
                            {
                                PartitionName = PartitionName.Substring(0, P); // Example: Data.bin.gz -> Data
                            }

                            Partition Partition = GPT.Partitions.Find(p => string.Equals(p.Name, PartitionName, StringComparison.CurrentCultureIgnoreCase));
                            if (Partition != null)
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

                                TotalSizeSectors += StreamLengthInSectors;
                                PartitionCount++;

                                if (string.Equals(PartitionName, "MainOS", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    MainOSOldSectorCount = Partition.SizeInSectors;
                                    MainOSNewSectorCount = StreamLengthInSectors;
                                    FirstMainOSSector = Partition.FirstSector;
                                }
                                else if (string.Equals(PartitionName, "Data", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    DataOldSectorCount = Partition.SizeInSectors;
                                    DataNewSectorCount = StreamLengthInSectors;
                                }
                                else if (StreamLengthInSectors > Partition.SizeInSectors)
                                {
                                    LogFile.Log("Flash failed! Size of partition '" + PartitionName + "' is too big.");
                                    ExitFailure("Flash failed!", "Size of partition '" + PartitionName + "' is too big.");
                                    return;
                                }
                            }
                        }
                    }

                    if ((MainOSNewSectorCount > 0) && (DataNewSectorCount > 0))
                    {
                        if ((MainOSNewSectorCount > MainOSOldSectorCount) || (DataNewSectorCount > DataOldSectorCount))
                        {
                            UInt64 OSSpace = GPT.LastUsableSector - FirstMainOSSector + 1;
                            if ((MainOSNewSectorCount + DataNewSectorCount) <= OSSpace)
                            {
                                // MainOS and Data partitions need to be re-aligned!
                                Partition MainOSPartition = GPT.Partitions.Single(p => string.Equals(p.Name, "MainOS", StringComparison.CurrentCultureIgnoreCase));
                                Partition DataPartition = GPT.Partitions.Single(p => string.Equals(p.Name, "Data", StringComparison.CurrentCultureIgnoreCase));
                                MainOSPartition.LastSector = MainOSPartition.FirstSector + MainOSNewSectorCount - 1;
                                DataPartition.FirstSector = MainOSPartition.LastSector + 1;
                                DataPartition.LastSector = DataPartition.FirstSector + DataNewSectorCount - 1;

                                GPTChanged = true;
                            }
                            else
                            {
                                LogFile.Log("Flash failed! Size of partitions 'MainOS' and 'Data' together are too big.");
                                ExitFailure("Flash failed!", "Sizes of partitions 'MainOS' and 'Data' together are too big.");
                                return;
                            }
                        }
                    }
                    else if ((MainOSNewSectorCount > 0) && (MainOSNewSectorCount > MainOSOldSectorCount))
                    {
                        LogFile.Log("Flash failed! Size of partition 'MainOS' is too big.");
                        ExitFailure("Flash failed!", "Size of partition 'MainOS' is too big.");
                        return;
                    }
                    else if ((DataNewSectorCount > 0) && (DataNewSectorCount > DataOldSectorCount))
                    {
                        LogFile.Log("Flash failed! Size of partition 'Data' is too big.");
                        ExitFailure("Flash failed!", "Size of partition 'Data' is too big.");
                        return;
                    }

                    if (GPTChanged)
                    {
                        Phone.WriteGPT(GPT);
                    }

                    if (PartitionCount > 0)
                    {
                        BusyViewModel Busy = new("Flashing...", MaxProgressValue: TotalSizeSectors, UIContext: UIContext);
                        ProgressUpdater Updater = Busy.ProgressUpdater;
                        ActivateSubContext(Busy);

                        int i = 0;

                        foreach (ZipArchiveEntry Entry in Archive.Entries)
                        {
                            // "MainOS.bin.gz" => "MainOS"
                            string PartitionName = Entry.Name;
                            int Pos = PartitionName.IndexOf('.');
                            if (Pos >= 0)
                            {
                                PartitionName = PartitionName.Substring(0, Pos);
                            }

                            Partition Partition = GPT.Partitions.Find(p => string.Equals(p.Name, PartitionName, StringComparison.CurrentCultureIgnoreCase));
                            if (Partition != null)
                            {
                                Stream DecompressedStream = new DecompressedStream(Entry.Open());
                                ulong StreamLengthInSectors = (ulong)Entry.Length / 0x200;
                                try
                                {
                                    StreamLengthInSectors = (ulong)DecompressedStream.Length / 0x200;
                                }
                                catch (Exception ex)
                                {
                                    LogFile.LogException(ex, LogType.FileOnly);
                                }

                                if (StreamLengthInSectors <= Partition.SizeInSectors)
                                {
                                    i++;
                                    Busy.Message = "Flashing partition " + Partition.Name + " (" + i.ToString() + "/" + PartitionCount.ToString() + ")";
                                    Phone.FlashRawPartition(DecompressedStream, Partition.Name, Updater);
                                }
                                DecompressedStream.Close();
                            }
                        }
                    }
                    else
                    {
                        LogFile.Log("Flash failed! No valid partitions found in the archive.");
                        ExitFailure("Flash failed!", "No valid partitions found in the archive");
                        return;
                    }
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    if (Ex is WPinternalsException)
                    {
                        ExitFailure("Flash failed!", ((WPinternalsException)Ex).SubMessage);
                    }
                    else
                    {
                        ExitFailure("Flash failed!", null);
                    }

                    return;
                }

                ExitSuccess("Flash successful! Make sure you disable Windows Update on the phone!", null);
            }).Start();
        }

        // Called from an event-handler. So, "async void" is valid here.
        internal async void FlashFFU(string FFUPath)
        {
            IsSwitchingInterface = true; // Prevents that a device is forced to Flash mode on this screen which is meant for flashing
            try
            {
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_Flash,
                    (msg, sub) =>
                        ActivateSubContext(new BusyViewModel(msg, sub)));
                FlashFFUTask(FFUPath);
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
            }
        }

        internal void FlashFFUTask(string FFUPath)
        {
            new Thread(async () =>
            {
                bool Result = true;

                LumiaFlashAppModel Phone = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;
                LumiaFlashAppPhoneInfo Info = Phone.ReadPhoneInfo(false);

                #region Remove bootloader changes

                // If necessary remove bootloader changes
                // In case the NV vars were redirected, and a stock FFU is flashed, then the IsFlashing flag will be cleared in the redirected NV vars
                // And after a reboot the original NV vars are active again, but the IsFlashing flag is still set from when the bootloader was unlocked
                // So we will first restore the GPT, so the original vars are active again.
                // Then IsFlashing is true and the phone boots forcibly to FlashApp again.
                // Then we start normal FFU flasing and at the end the IsFlashing flag is cleared in the original vars.

                if (Info.FlashAppProtocolVersionMajor >= 2)
                {
                    Phone.SwitchToBootManagerContext();

                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    {
                        await PhoneNotifier.WaitForArrival();
                    }

                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    {
                        throw new WPinternalsException("Unexpected Mode");
                    }

                    byte[] GPTChunk = ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).GetGptChunk(0x20000); // TODO: Get proper profile FFU and get ChunkSizeInBytes
                    GPT GPT = new(GPTChunk);

                    ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).SwitchToFlashAppContext();

                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        await PhoneNotifier.WaitForArrival();
                    }

                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        throw new WPinternalsException("Unexpected Mode");
                    }

                    Phone = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;

                    FlashPart Part;
                    List<FlashPart> FlashParts = new();

                    Partition NvBackupPartition = GPT.GetPartition("BACKUP_BS_NV");
                    if (NvBackupPartition != null)
                    {
                        // This must be a left over of a half unlocked bootloader
                        Partition NvPartition = GPT.GetPartition("UEFI_BS_NV");
                        NvBackupPartition.Name = "UEFI_BS_NV";
                        NvBackupPartition.PartitionGuid = NvPartition.PartitionGuid;
                        NvBackupPartition.PartitionTypeGuid = NvPartition.PartitionTypeGuid;
                        GPT.Partitions.Remove(NvPartition);

                        GPT.Rebuild();
                        Part = new FlashPart
                        {
                            StartSector = 0,
                            Stream = new MemoryStream(GPTChunk)
                        };
                        FlashParts.Add(Part);
                    }

                    bool ClearFlashingStatus = true;

                    // We should only clear NV if there was no backup NV to be restored and the current NV contains the SB unlock.
                    if ((NvBackupPartition == null) && !Info.UefiSecureBootEnabled)
                    {
                        // ClearNV
                        Part = new FlashPart();
                        Partition Target = GPT.GetPartition("UEFI_BS_NV");
                        Part.StartSector = (UInt32)Target.FirstSector;
                        Part.Stream = new MemoryStream(new byte[0x40000]);
                        FlashParts.Add(Part);

                        ClearFlashingStatus = false;
                    }

                    if (FlashParts.Count > 0)
                    {
                        ActivateSubContext(new BusyViewModel("Restoring bootloader..."));
                        WPinternalsStatus LastStatus = WPinternalsStatus.Undefined;
                        LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(PhoneNotifier, FFUPath, false, false, FlashParts, true, ClearFlashingStatusAtEnd: ClearFlashingStatus,
                            SetWorkingStatus: (m, s, v, a, st) =>
                            {
                                if ((st == WPinternalsStatus.Scanning) || (st == WPinternalsStatus.WaitingForManualReset))
                                {
                                    SetWorkingStatus(m, s, v, a, st);
                                }
                                else if ((LastStatus == WPinternalsStatus.Scanning) || (LastStatus == WPinternalsStatus.WaitingForManualReset))
                                {
                                    SetWorkingStatus("Restoring bootloader...", null, null, Status: WPinternalsStatus.Flashing);
                                }

                                LastStatus = st;
                            },
                            UpdateWorkingStatus: (m, s, v, st) =>
                            {
                                if ((st == WPinternalsStatus.Scanning) || (st == WPinternalsStatus.WaitingForManualReset))
                                {
                                    UpdateWorkingStatus(m, s, v, st);
                                }
                                else if ((LastStatus == WPinternalsStatus.Scanning) || (LastStatus == WPinternalsStatus.WaitingForManualReset))
                                {
                                    SetWorkingStatus("Restoring bootloader...", null, null, Status: WPinternalsStatus.Flashing);
                                }

                                LastStatus = st;
                            }
                        ).Wait();

                        if ((PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                        {
                            PhoneNotifier.WaitForArrival().Wait();
                        }

                        if (PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                        {
                            ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).SwitchToFlashAppContext();
                        }
                    }
                }

                #endregion

                Phone = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;

                ActivateSubContext(new BusyViewModel("Initializing flash..."));

                string ErrorSubMessage = null;

                try
                {
                    FFU FFU = new(FFUPath);
                    BusyViewModel Busy = new("Flashing original FFU...", MaxProgressValue: FFU.TotalChunkCount, UIContext: UIContext);
                    ActivateSubContext(Busy);
                    byte Options = 0;
                    if (!Info.IsBootloaderSecure)
                    {
                        Options = (byte)((FlashOptions)Options | FlashOptions.SkipSignatureCheck);
                    }

                    Phone.FlashFFU(FFU, Busy.ProgressUpdater, true, Options);
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    if (Ex is WPinternalsException)
                    {
                        ErrorSubMessage = ((WPinternalsException)Ex).SubMessage;
                    }

                    Result = false;
                }

                if (!Result)
                {
                    ExitFailure("Flash failed!", ErrorSubMessage);
                    return;
                }

                ExitSuccess("Flash successful!", null);
            }).Start();
        }

        // Called from an event-handler. So, "async void" is valid here.
        internal async void FlashMMOS(string MMOSPath)
        {
            IsSwitchingInterface = true; // Prevents that a device is forced to Flash mode on this screen which is meant for flashing
            try
            {
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_Flash,
                    (msg, sub) =>
                        ActivateSubContext(new BusyViewModel(msg, sub)));
                FlashMMOSTask(MMOSPath);
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
            }
        }

        internal void FlashMMOSTask(string MMOSPath)
        {
            if (PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
            {
                ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).SwitchToFlashAppContext();
            }

            LumiaFlashAppModel Phone = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;

            new Thread(() =>
            {
                bool Result = true;

                ActivateSubContext(new BusyViewModel("Initializing flash..."));

                string ErrorSubMessage = null;

                try
                {
                    FileInfo info = new(MMOSPath);
                    uint length = uint.Parse(info.Length.ToString());
                    const int maximumbuffersize = 0x00240000;
                    uint totalcounts = (uint)Math.Truncate((decimal)length / maximumbuffersize);
                    BusyViewModel Busy = new("Flashing Test Mode package...", MaxProgressValue: totalcounts, UIContext: UIContext);
                    ActivateSubContext(Busy);

                    Phone.FlashMMOS(MMOSPath, Busy.ProgressUpdater);

                    ActivateSubContext(new BusyViewModel("And now booting phone to MMOS...", "If the phone stays on the lightning cog screen for a while, you may need to unplug and replug the phone to continue the boot process."));

                    PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    if (Ex is WPinternalsException)
                    {
                        ErrorSubMessage = ((WPinternalsException)Ex).SubMessage;
                    }

                    Result = false;
                }

                if (!Result)
                {
                    ExitFailure("Flash failed!", ErrorSubMessage);
                    return;
                }
            }).Start();
        }

        private void NewDeviceArrived(ArrivalEventArgs Args)
        {
            PhoneNotifier.NewDeviceArrived -= NewDeviceArrived;

            if (Args.NewInterface != PhoneInterfaces.Lumia_Label)
            {
                ExitFailure("Flash failed!", "Phone unexpectedly switched mode while booting MMOS image.");
                return;
            }
            else
            {
                ExitSuccess("Flash successful!", null);
                return;
            }
        }

        // Called from an event-handler. So, "async void" is valid here.
        internal async void Exit()
        {
            try
            {
                await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_Normal,
                    (msg, sub) =>
                        ActivateSubContext(new BusyViewModel(msg, sub)));
                IsSwitchingInterface = false;
                Callback();
                ActivateSubContext(null);
            }
            catch (Exception Ex)
            {
                ActivateSubContext(new MessageViewModel(Ex.Message, () =>
                {
                    IsSwitchingInterface = false;
                    Callback();
                    ActivateSubContext(null);
                }));
            }
        }

        internal void ExitSuccess(string Message, string SubMessage)
        {
            MessageViewModel SuccessMessageViewModel = new(Message, () =>
            {
                // No need to call Exit() to go to normal mode, because it already switches to normal mode automatically.
                IsSwitchingInterface = false; // From here on a device will be forced to Flash mode again on this screen which is meant for flashing
                Callback();
                ActivateSubContext(null);
            });
            SuccessMessageViewModel.SubMessage = SubMessage;
            ActivateSubContext(SuccessMessageViewModel);
        }

        internal void ExitFailure(string Message, string SubMessage)
        {
            MessageViewModel ErrorMessageViewModel = new(Message, () =>
            {
                IsSwitchingInterface = false;
                Callback();
                ActivateSubContext(null);
            });
            ErrorMessageViewModel.SubMessage = SubMessage;
            ActivateSubContext(ErrorMessageViewModel);
        }
    }
}
