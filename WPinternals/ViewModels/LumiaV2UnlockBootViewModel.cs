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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace WPinternals
{
    internal class LumiaV2UnlockBootViewModel : ContextViewModel
    {
        internal static async Task LumiaV2FindFlashingProfile(PhoneNotifierViewModel Notifier, string FFUPath, bool DoResetFirst = true, bool Experimental = false, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            if (SetWorkingStatus == null)
            {
                SetWorkingStatus = (m, s, v, a, st) => { };
            }

            if (UpdateWorkingStatus == null)
            {
                UpdateWorkingStatus = (m, s, v, st) => { };
            }

            if (ExitSuccess == null)
            {
                ExitSuccess = (m, s) => { };
            }

            if (ExitFailure == null)
            {
                ExitFailure = (m, s) => { };
            }

            LogFile.BeginAction("FindFlashingProfile");
            try
            {
                LogFile.Log("Find Flashing Profile", LogType.FileAndConsole);

                LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);

                LumiaFlashAppPhoneInfo Info;
                if (DoResetFirst)
                {
                    // The phone will be reset before flashing, so we have the opportunity to get some more info from the phone
                    Info = FlashModel.ReadPhoneInfo();
                    Info.Log(LogType.ConsoleOnly);

                    string FfuFirmware = null;
                    if (FFUPath != null)
                    {
                        FFU FFU = new(FFUPath);
                        FfuFirmware = FFU.GetFirmwareVersion();
                    }
                    FlashProfile Profile = App.Config.GetProfile(Info.PlatformID, Info.Firmware, FfuFirmware);

                    if (Profile != null)
                    {
                        LogFile.Log("Flashing Profile already present for this phone", LogType.FileAndConsole);
                        return;
                    }
                }
                else
                {
                    Info = FlashModel.ReadPhoneInfo(ExtendedInfo: false);
                }

                SetWorkingStatus("Scanning for flashing-profile", "Your phone may appear to be in a reboot-loop. This is expected behavior. Don't interfere this process.", null, Status: WPinternalsStatus.Scanning);

                await LumiaV2CustomFlash(Notifier, FFUPath, false, !Info.IsBootloaderSecure, null, DoResetFirst, Experimental: Experimental, SetWorkingStatus:
                    (m, s, v, a, st) =>
                    {
                        if (st == WPinternalsStatus.SwitchingMode)
                        {
                            SetWorkingStatus(m, s, v, a, st);
                        }
                    },
                    UpdateWorkingStatus:
                    (m, s, v, st) =>
                    {
                        if (st == WPinternalsStatus.SwitchingMode)
                        {
                            UpdateWorkingStatus(m, s, v, st);
                        }
                    },
                    ExitSuccess: ExitSuccess, ExitFailure: ExitFailure);

                LogFile.Log("Flashing profile found!", LogType.FileAndConsole);
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
            }
            finally
            {
                LogFile.EndAction("FindFlashingProfile");
            }
        }

        internal static async Task LumiaV2EnableTestSigning(System.Threading.SynchronizationContext UIContext, string FFUPath, bool DoResetFirst = true)
        {
            LogFile.BeginAction("EnableTestSigning");
            try
            {
                LogFile.Log("Command: Enable testsigning", LogType.FileAndConsole);
                PhoneNotifierViewModel Notifier = new();
                UIContext.Send(s => Notifier.Start(), null);
                LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);

                List<FlashPart> Parts = new();
                FlashPart Part;

                // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
                // We need the fist sector if we want to write back the GPT.
                byte[] GPTChunk = FlashModel.GetGptChunk(0x20000);
                GPT GPT = new(GPTChunk);
                bool GPTChanged = false;

                Partition BACKUP_BS_NV = GPT.GetPartition("BACKUP_BS_NV");
                Partition UEFI_BS_NV;
                if (BACKUP_BS_NV == null)
                {
                    BACKUP_BS_NV = GPT.GetPartition("UEFI_BS_NV");
                    Guid OriginalPartitionTypeGuid = BACKUP_BS_NV.PartitionTypeGuid;
                    Guid OriginalPartitionGuid = BACKUP_BS_NV.PartitionGuid;
                    BACKUP_BS_NV.Name = "BACKUP_BS_NV";
                    BACKUP_BS_NV.PartitionGuid = Guid.NewGuid();
                    BACKUP_BS_NV.PartitionTypeGuid = Guid.NewGuid();
                    UEFI_BS_NV = new Partition
                    {
                        Name = "UEFI_BS_NV",
                        Attributes = BACKUP_BS_NV.Attributes,
                        PartitionGuid = OriginalPartitionGuid,
                        PartitionTypeGuid = OriginalPartitionTypeGuid,
                        FirstSector = BACKUP_BS_NV.LastSector + 1
                    };
                    UEFI_BS_NV.LastSector = UEFI_BS_NV.FirstSector + BACKUP_BS_NV.LastSector - BACKUP_BS_NV.FirstSector;
                    GPT.Partitions.Add(UEFI_BS_NV);
                    GPTChanged = true;
                }
                if (GPTChanged)
                {
                    GPT.Rebuild();
                    Part = new FlashPart
                    {
                        StartSector = 0,
                        Stream = new MemoryStream(GPTChunk)
                    };
                    Parts.Add(Part);
                }

                // This code was used to compress the partition to an embedded resource:
                //
                // byte[] sbpart = System.IO.File.ReadAllBytes(@"C:\Windows Phone 8\Sources\WPInternals\SB.Original.bin");
                // System.IO.FileStream s = new System.IO.FileStream(@"C:\Windows Phone 8\Sources\WPInternals\SB", System.IO.FileMode.Create, System.IO.FileAccess.Write);
                // CompressedStream Out = new CompressedStream(s, (ulong)sbpart.Length);
                // Out.Write(sbpart, 0, sbpart.Length);
                // Out.Close();
                // s.Close();

                Part = new FlashPart();
                Partition TargetPartition = GPT.GetPartition("UEFI_BS_NV");
                Part.StartSector = (UInt32)TargetPartition.FirstSector; // GPT is prepared for 64-bit sector-offset, but flash app isn't.
                Part.Stream = new SeekableStream(() =>
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                    // Magic!
                    // The SB resource is a compressed version of a raw NV-variable-partition.
                    // In this partition the SecureBoot variable is disabled.
                    // It overwrites the variable in a different NV-partition than where this variable is stored usually.
                    // This normally leads to endless-loops when the NV-variables are enumerated.
                    // But the partition contains an extra hack to break out the endless loops.
                    var stream = assembly.GetManifestResourceStream("WPinternals.SB");

                    return new DecompressedStream(stream);
                });
                Parts.Add(Part);

                await LumiaV2CustomFlash(Notifier, FFUPath, false, false, Parts, DoResetFirst, ClearFlashingStatusAtEnd: false);

                Notifier.Stop();
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
            }
            finally
            {
                LogFile.EndAction("EnableTestSigning");
            }
        }

        internal static async Task<string> LumiaV2SwitchToMassStorageMode(PhoneNotifierViewModel Notifier, string FFUPath, bool DoResetFirst = true)
        {
            // If there is no phone connected yet, we wait here for the phone to connect.
            // Because it could be connecting in mass storage mode.
            // So we dont want to let SwitchTo() wait for it, because it might already be trying to switch to flash mode, which is not possible and not necessary at that point.
            if (Notifier.CurrentInterface == null)
            {
                LogFile.Log("Waiting for phone to connect...", LogType.FileAndConsole);
                await Notifier.WaitForArrival();
            }

            if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_MassStorage)
            {
                LogFile.Log("Phone is already in Mass Storage Mode", LogType.FileAndConsole);
                return ((MassStorage)Notifier.CurrentModel).Drive;
            }

            LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
            if (DoResetFirst)
            {
                // The phone will be reset before flashing, so we have the opportunity to get some more info from the phone
                LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo();
                Info.Log(LogType.ConsoleOnly);
            }

            await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_MassStorage);

            MassStorage Storage = null;
            if (Notifier.CurrentModel is MassStorage)
            {
                Storage = (MassStorage)Notifier.CurrentModel;
            }

            if (Storage == null)
            {
                throw new WPinternalsException("Failed to switch to Mass Storage Mode");
            }

            return Storage.Drive;
        }

        internal static async Task LumiaV2ClearNV(System.Threading.SynchronizationContext UIContext, string FFUPath, bool DoResetFirst = true)
        {
            LogFile.BeginAction("ClearNV");
            try
            {
                LogFile.Log("Command: Clear NV", LogType.FileAndConsole);
                PhoneNotifierViewModel Notifier = new();
                UIContext.Send(s => Notifier.Start(), null);
                LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                List<FlashPart> Parts = new();

                // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
                // We need the fist sector if we want to write back the GPT.
                byte[] GPTChunk = FlashModel.GetGptChunk(0x20000);
                GPT GPT = new(GPTChunk);
                bool GPTChanged = false;
                Partition BACKUP_BS_NV = GPT.GetPartition("BACKUP_BS_NV");
                Partition UEFI_BS_NV;
                if (BACKUP_BS_NV == null)
                {
                    BACKUP_BS_NV = GPT.GetPartition("UEFI_BS_NV");
                    Guid OriginalPartitionTypeGuid = BACKUP_BS_NV.PartitionTypeGuid;
                    Guid OriginalPartitionGuid = BACKUP_BS_NV.PartitionGuid;
                    BACKUP_BS_NV.Name = "BACKUP_BS_NV";
                    BACKUP_BS_NV.PartitionGuid = Guid.NewGuid();
                    BACKUP_BS_NV.PartitionTypeGuid = Guid.NewGuid();
                    UEFI_BS_NV = new Partition
                    {
                        Name = "UEFI_BS_NV",
                        Attributes = BACKUP_BS_NV.Attributes,
                        PartitionGuid = OriginalPartitionGuid,
                        PartitionTypeGuid = OriginalPartitionTypeGuid,
                        FirstSector = BACKUP_BS_NV.LastSector + 1
                    };
                    UEFI_BS_NV.LastSector = UEFI_BS_NV.FirstSector + BACKUP_BS_NV.LastSector - BACKUP_BS_NV.FirstSector;
                    GPT.Partitions.Add(UEFI_BS_NV);
                    GPTChanged = true;
                }
                if (GPTChanged)
                {
                    GPT.Rebuild();
                    FlashPart Part = new();
                    Part.StartSector = 0;
                    Part.Stream = new MemoryStream(GPTChunk);
                    Parts.Add(Part);
                }

                using (MemoryStream Space = new(new byte[0x40000]))
                {
                    Partition Target = GPT.GetPartition("UEFI_BS_NV");
                    Parts.Add(new FlashPart() { StartSector = (uint)Target.FirstSector, Stream = Space });

                    await LumiaV2CustomFlash(Notifier, FFUPath, false, false, Parts, DoResetFirst, ClearFlashingStatusAtEnd: false);
                }

                LogFile.Log("NV successfully cleared!", LogType.FileAndConsole);
                Notifier.Stop();
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
            }
            finally
            {
                LogFile.EndAction("ClearNV");
            }
        }

        internal static async Task LumiaV2FlashPartition(System.Threading.SynchronizationContext UIContext, string FFUPath, string PartitionName, string PartitionPath, bool DoResetFirst = true)
        {
            LogFile.BeginAction("FlashPartition");
            try
            {
                LogFile.Log("Command: Flash Partition", LogType.FileAndConsole);
                LogFile.Log("Partition name: " + PartitionName, LogType.FileAndConsole);
                LogFile.Log("Partition file: " + PartitionPath, LogType.FileAndConsole);
                if (FFUPath != null)
                {
                    LogFile.Log("Profile FFU file: " + FFUPath, LogType.FileAndConsole);
                }

                PhoneNotifierViewModel Notifier = new();
                UIContext.Send(s => Notifier.Start(), null);

                LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);

                LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo();

                // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
                // We need the fist sector if we want to write back the GPT.
                byte[] GPTChunk = FlashModel.GetGptChunk(0x20000);
                GPT GPT = new(GPTChunk);

                Partition TargetPartition = GPT.GetPartition(PartitionName);
                if (TargetPartition == null)
                {
                    throw new WPinternalsException("Target partition not found!", "Couldn't find \"" + PartitionName + "\" from the device GPT.");
                }

                LogFile.Log("Target-partition found at sector: 0x" + TargetPartition.FirstSector.ToString("X8") + " - 0x" + TargetPartition.LastSector.ToString("X8"), LogType.FileAndConsole);

                bool IsUnlocked = false;
                bool GPTChanged = false;
                List<FlashPart> Parts = new();
                FlashPart Part;
                if (string.Equals(PartitionName, "EFIESP", StringComparison.CurrentCultureIgnoreCase))
                {
                    byte[] EfiespBinary = File.ReadAllBytes(PartitionPath);
                    IsUnlocked = (ByteOperations.ReadUInt32(EfiespBinary, 0x20) == (EfiespBinary.Length / 0x200 / 2)) && ByteOperations.ReadAsciiString(EfiespBinary, (UInt32)(EfiespBinary.Length / 2) + 3, 8) == "MSDOS5.0";

                    if (IsUnlocked)
                    {
                        Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                        if (IsUnlockedFlag == null)
                        {
                            IsUnlockedFlag = new Partition
                            {
                                Name = "IS_UNLOCKED",
                                Attributes = 0,
                                PartitionGuid = Guid.NewGuid(),
                                PartitionTypeGuid = Guid.NewGuid(),
                                FirstSector = 0x40,
                                LastSector = 0x40
                            };
                            GPT.Partitions.Add(IsUnlockedFlag);
                            GPTChanged = true;
                        }
                    }
                    else
                    {
                        Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                        if (IsUnlockedFlag != null)
                        {
                            GPT.Partitions.Remove(IsUnlockedFlag);
                            GPTChanged = true;
                        }
                    }

                    if (GPTChanged)
                    {
                        GPT.Rebuild();
                        Part = new FlashPart
                        {
                            StartSector = 0,
                            Stream = new MemoryStream(GPTChunk)
                        };
                        Parts.Add(Part);
                    }
                }

                using (FileStream Stream = new(PartitionPath, FileMode.Open))
                {
                    if ((UInt64)Stream.Length != (TargetPartition.SizeInSectors * 0x200))
                    {
                        throw new WPinternalsException("Raw partition has wrong size. Size = 0x" + Stream.Length.ToString("X8") + ". Expected size = 0x" + (TargetPartition.SizeInSectors * 0x200).ToString("X8"));
                    }

                    Part = new FlashPart
                    {
                        StartSector = (UInt32)TargetPartition.FirstSector,
                        Stream = Stream
                    };
                    Parts.Add(Part);
                    await LumiaV2CustomFlash(Notifier, FFUPath, false, false, Parts, DoResetFirst, !string.Equals(PartitionName, "UEFI_BS_NV", StringComparison.CurrentCultureIgnoreCase));
                }
                Notifier.Stop();
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
            }
            finally
            {
                LogFile.EndAction("FlashPartition");
            }
        }

        internal static async Task LumiaV2FlashRaw(System.Threading.SynchronizationContext UIContext, UInt64 StartSector, string DataPath, string FFUPath, bool DoResetFirst = true)
        {
            LogFile.BeginAction("FlashRaw");
            try
            {
                LogFile.Log("Command: Flash Raw", LogType.FileAndConsole);
                LogFile.Log("Start sector: 0x" + StartSector.ToString("X16"), LogType.FileAndConsole);
                LogFile.Log("Data file: " + DataPath, LogType.FileAndConsole);
                if (FFUPath != null)
                {
                    LogFile.Log("FFU file: " + FFUPath, LogType.FileAndConsole);
                }

                PhoneNotifierViewModel Notifier = new();
                UIContext.Send(s => Notifier.Start(), null);

                LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);

                LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo();

                byte[] Data = File.ReadAllBytes(DataPath);

                await LumiaV2CustomFlash(Notifier, FFUPath, false, false, (UInt32)StartSector, Data, DoResetFirst);
                Notifier.Stop();
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
            }
            finally
            {
                LogFile.EndAction("FlashRaw");
            }
        }

        internal async static Task LumiaV2CustomFlash(PhoneNotifierViewModel Notifier, string FFUPath, bool PerformFullFlashFirst, bool SkipWrite, UInt32 StartSector, byte[] Data, bool DoResetFirst = true, bool ClearFlashingStatusAtEnd = true, bool CheckSectorAlignment = true, bool ShowProgress = true, bool Experimental = false) //, string LoaderPath = null)
        {
            using MemoryStream Stream = new(Data);
            FlashPart Part = new() { StartSector = StartSector, Stream = Stream };
            List<FlashPart> Parts = new();
            Parts.Add(Part);
            await LumiaV2CustomFlash(Notifier, FFUPath, PerformFullFlashFirst, SkipWrite, Parts, DoResetFirst, ClearFlashingStatusAtEnd, CheckSectorAlignment, ShowProgress, Experimental);
        }

        internal async static Task LumiaV2CustomFlash(PhoneNotifierViewModel Notifier, string FFUPath, bool PerformFullFlashFirst, bool SkipWrite, UInt32 StartSector, Stream Data, bool DoResetFirst = true, bool ClearFlashingStatusAtEnd = true, bool CheckSectorAlignment = true, bool ShowProgress = true, bool Experimental = false) //, string LoaderPath = null)
        {
            FlashPart Part = new() { StartSector = StartSector, Stream = Data };
            List<FlashPart> Parts = new();
            Parts.Add(Part);
            await LumiaV2CustomFlash(Notifier, FFUPath, PerformFullFlashFirst, SkipWrite, Parts, DoResetFirst, ClearFlashingStatusAtEnd, CheckSectorAlignment, ShowProgress, Experimental);
        }

        internal async static Task LumiaV2CustomFlash(PhoneNotifierViewModel Notifier, string FFUPath, bool PerformFullFlashFirst, bool SkipWrite, List<FlashPart> FlashParts, bool DoResetFirst = true, bool ClearFlashingStatusAtEnd = true, bool CheckSectorAlignment = true, bool ShowProgress = true, bool Experimental = false, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null, string ProgrammerPath = null)
        {
            LumiaFlashAppModel Model = (LumiaFlashAppModel)Notifier.CurrentModel;
            LumiaFlashAppPhoneInfo Info = Model.ReadPhoneInfo();

            byte[] GPTChunk = Model.GetGptChunk(131072u);

            GPT GPT = new(GPTChunk);

            Partition UefiBSNV = GPT.GetPartition("UEFI_BS_NV");

            bool UseOlderExploit = Info.UefiSecureBootEnabled;

            if (!UseOlderExploit && ClearFlashingStatusAtEnd)
            {
                if (FlashParts == null)
                {
                    UseOlderExploit = true;
                }
                else
                {
                    foreach (var part in FlashParts)
                    {
                        if (part.StartSector >= UefiBSNV.FirstSector && part.StartSector <= UefiBSNV.LastSector)
                        {
                            UseOlderExploit = true;
                            break;
                        }
                    }
                }
            }

            if (UseOlderExploit || !ClearFlashingStatusAtEnd)
            {
                await LumiaV2CustomFlashInternal(Notifier, FFUPath, PerformFullFlashFirst, SkipWrite, FlashParts, DoResetFirst, ClearFlashingStatusAtEnd, CheckSectorAlignment, ShowProgress, Experimental, SetWorkingStatus, UpdateWorkingStatus, ExitSuccess, ExitFailure, ProgrammerPath);
            }
            else
            {
                await LumiaV3FlashRomViewModel.LumiaV3CustomFlash(Notifier, FlashParts, CheckSectorAlignment, SetWorkingStatus, UpdateWorkingStatus, ExitSuccess, ExitFailure);
            }
        }

        // Magic!
        internal async static Task LumiaV2CustomFlashInternal(PhoneNotifierViewModel Notifier, string FFUPath, bool PerformFullFlashFirst, bool SkipWrite, List<FlashPart> FlashParts, bool DoResetFirst = true, bool ClearFlashingStatusAtEnd = true, bool CheckSectorAlignment = true, bool ShowProgress = true, bool Experimental = false, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null, string ProgrammerPath = null) //, string LoaderPath = null)
        {
            // Both SecurityHeader and StoreHeader need to be modified.
            // Those should both not fall in a memory-gap to allow modification.
            // The partial FFU header must be allocated in front of those headers, so the size of the partial header must be at least the size of the the SecurityHeader.
            // Hashes take more space than descriptors, so the SecurityHeader will always be the biggest.

            bool AutoEmergencyReset = true;
            bool Timeout;

            if (SetWorkingStatus == null)
            {
                SetWorkingStatus = (m, s, v, a, st) => { };
            }

            if (UpdateWorkingStatus == null)
            {
                UpdateWorkingStatus = (m, s, v, st) => { };
            }

            if (ExitSuccess == null)
            {
                ExitSuccess = (m, s) => { };
            }

            if (ExitFailure == null)
            {
                ExitFailure = (m, s) => { };
            }

            LumiaFlashAppPhoneInfo FlashInfo = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadPhoneInfo();

            bool ModernFlashApp = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadPhoneInfo().FlashAppProtocolVersionMajor >= 2;
            if (ModernFlashApp)
            {
                ((LumiaFlashAppModel)Notifier.CurrentModel).SwitchToPhoneInfoAppContext();
            }
            else
            {
                ((LumiaFlashAppModel)Notifier.CurrentModel).SwitchToPhoneInfoAppContextLegacy();
            }

            if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
            {
                await Notifier.WaitForArrival();
            }

            if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
            {
                throw new WPinternalsException("Unexpected Mode");
            }

            LumiaPhoneInfoAppModel LumiaPhoneInfoModel = (LumiaPhoneInfoAppModel)Notifier.CurrentModel;
            LumiaPhoneInfoAppPhoneInfo PhoneInfo = LumiaPhoneInfoModel.ReadPhoneInfo();

            ModernFlashApp = PhoneInfo.PhoneInfoAppVersionMajor >= 2;
            if (ModernFlashApp)
            {
                LumiaPhoneInfoModel.SwitchToFlashAppContext();
            }
            else
            {
                LumiaPhoneInfoModel.ContinueBoot();
            }

            if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
            {
                await Notifier.WaitForArrival();
            }

            if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
            {
                throw new WPinternalsException("Unexpected Mode");
            }

            string Type = PhoneInfo.Type;
            if (ProgrammerPath == null)
            {
                ProgrammerPath = GetProgrammerPath(FlashInfo.RKH, Type);
                if (ProgrammerPath == null)
                {
                    LogFile.Log("WARNING: No emergency programmer file found. Finding flash profile and rebooting phone may take a long time!", LogType.FileAndConsole);
                }
            }
            List<FFUEntry> FFUs = null;
            FlashProfile Profile;
            if (FFUPath == null)
            {
                // Try to find an FFU from the repository for which there is also a known flashing profile
                FFUs = App.Config.FFURepository.Where(e => FlashInfo.PlatformID.StartsWith(e.PlatformID, StringComparison.OrdinalIgnoreCase) && e.Exists()).ToList();
                foreach (FFUEntry CurrentEntry in FFUs)
                {
                    Profile = App.Config.GetProfile(FlashInfo.PlatformID, FlashInfo.Firmware, CurrentEntry.FirmwareVersion);
                    if (Profile != null)
                    {
                        FFUPath = CurrentEntry.Path;
                        break;
                    }
                }
            }

            if (FFUPath == null)
            {
                // Try to find any FFU with matching PlatformID in the repository
                if (FFUs.Count > 0)
                {
                    FFUPath = FFUs[0].Path;
                }
            }

            if (FFUPath == null)
            {
                throw new WPinternalsException("No valid profile FFU found in repository", "You can download necessary files in the &quot;Download&quot; section");
            }

            FFU FFU = new(FFUPath);
            UInt32 UpdateType = ByteOperations.ReadUInt32(FFU.StoreHeader, 0);
            if (UpdateType != 0)
            {
                throw new WPinternalsException("Only Full Flash images supported", "The provided FFU file reports that it doesn't support Full Flash updates, but may support something else such as Partial Flash updates. This is not supported.");
            }

            if (FlashParts != null)
            {
                foreach (FlashPart Part in FlashParts)
                {
                    if (Part.Stream == null)
                    {
                        throw new ArgumentException("Stream is null");
                    }

                    if (!Part.Stream.CanSeek)
                    {
                        throw new ArgumentException("Streams must be seekable");
                    }

                    if ((Part.StartSector * 0x200 % FFU.ChunkSize) != 0)
                    {
                        throw new ArgumentException("Invalid StartSector alignment");
                    }

                    if (CheckSectorAlignment && (Part.Stream.Length % FFU.ChunkSize) != 0)
                    {
                        throw new ArgumentException("Invalid Data length");
                    }
                }
            }

            if ((FlashInfo.SecureFfuSupportedProtocolMask & ((ushort)FfuProtocol.ProtocolSyncV2)) == 0) // Exploit needs protocol v2 -> This check is not conclusive, because old phones also report support for this protocol, although it is really not supported.
            {
                throw new WPinternalsException("Flash failed!", "Protocols not supported. The phone reports that it does not support the Protocol Sync V2.");
            }

            if (FlashInfo.FlashAppProtocolVersionMajor < 2) // Old phones do not support the hack. These phones have Flash protocol 1.x.
            {
                throw new WPinternalsException("Flash failed!", "Protocols not supported. The phone reports that Flash App communication protocol is lower than 2. Reported version by the phone: " + FlashInfo.FlashAppProtocolVersionMajor + ".");
            }

            UEFI UEFI = new(FFU.GetPartition("UEFI"));
            string BootMgrName = UEFI.EFIs.First(efi => efi.Name?.Contains("BootMgrApp") == true).Name;
            UInt32 EstimatedSizeOfMemGap = (UInt32)UEFI.GetFile(BootMgrName).Length;
            byte Options = 0;
            if (SkipWrite)
            {
                Options = (byte)FlashOptions.SkipWrite;
            }

            if (!FlashInfo.IsBootloaderSecure)
            {
                Options = (byte)((FlashOptions)Options | FlashOptions.SkipSignatureCheck);
            }

            // Gap fill calculation:
            // About 0x18000 of the gap is used for other purposes.
            // Then round down to fill up the rest of the space, to make sure the memory for the headers is not allocated in a gap.
            UInt32 EstimatedGapFill = FFU.RoundDownToChunks(EstimatedSizeOfMemGap - 0x18000);

            UInt32 MaximumGapFill;
            int MaximumAttempts;

            if (!Experimental)
            {
                MaximumGapFill = FFU.RoundUpToChunks(2 * EstimatedSizeOfMemGap);
                MaximumAttempts = (int)(((MaximumGapFill / FFU.ChunkSize) + 1) * 4);
            }
            else
            {
                MaximumGapFill = FFU.RoundUpToChunks(4 * EstimatedSizeOfMemGap);
                MaximumAttempts = (int)(((MaximumGapFill / FFU.ChunkSize) + 1) * 8);
            }

            byte[] GPTChunk = ((LumiaFlashAppModel)Notifier.CurrentModel).GetGptChunk((UInt32)FFU.ChunkSize);

            // Start with a reset
            if (DoResetFirst)
            {
                SetWorkingStatus("Initializing flash...", "Rebooting phone", null, Status: WPinternalsStatus.Initializing);

                // When in flash mode, it is not possible to reboot straight to flash.
                // Reboot and catch the phone in bootloader mode and then switch to flash context
                ((LumiaFlashAppModel)Notifier.CurrentModel).ResetPhone();

                #region Properly recover from reset - many phones respond differently

                Timeout = false;
                try
                {
                    await Notifier.WaitForArrival().TimeoutAfter(TimeSpan.FromSeconds(40));
                }
                catch (TimeoutException)
                {
                    Timeout = true;
                }
                if ((Notifier.CurrentInterface == null) && (!AutoEmergencyReset || Timeout))
                {
                    AutoEmergencyReset = false;

                    if (Timeout)
                    {
                        LogFile.Log("The phone is not responding", LogType.ConsoleOnly);
                        LogFile.Log("It might be in emergency mode, while you have no matching driver installed", LogType.ConsoleOnly);
                    }
                    LogFile.Log("To continue the unlock-sequence, the phone needs to be rebooted", LogType.ConsoleOnly);
                    LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                    LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                    LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                    LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                    SetWorkingStatus("You need to manually reset your phone now!", (Timeout ? "The phone is not responding. It might be in emergency mode, while you have no matching driver installed. " : "") + "To continue the unlock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. The unlock-sequence will resume automatically.", null, false, WPinternalsStatus.WaitingForManualReset);

                    await Notifier.WaitForRemoval();

                    UpdateWorkingStatus("Initializing flash...", null, null);

                    await Notifier.WaitForArrival();
                }
                if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                {
                    bool FailedToStartProgrammer = false;
                    if (ProgrammerPath != null)
                    {
                        QualcommSahara Sahara = new((QualcommSerial)Notifier.CurrentModel);
                        QualcommFirehose Firehose = new((QualcommSerial)Notifier.CurrentModel);
                        try
                        {
                            await Sahara.LoadProgrammer(ProgrammerPath);
                            await Firehose.Reset();
                            await Notifier.WaitForArrival();
                        }
                        catch (BadConnectionException)
                        {
                            FailedToStartProgrammer = true;
                        }
                        catch (Exception ex)
                        {
                            LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                            LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                            FailedToStartProgrammer = true;
                        }
                    }

                    if (ProgrammerPath == null || FailedToStartProgrammer)
                    {
                        ((QualcommSerial)Notifier.CurrentModel).Close(); // Prevent "Resource in use";

                        Timeout = false;
                        if (AutoEmergencyReset)
                        {
                            try
                            {
                                await Notifier.WaitForArrival().TimeoutAfter(TimeSpan.FromSeconds(40));
                            }
                            catch (TimeoutException)
                            {
                                Timeout = true;
                            }
                            catch (Exception ex)
                            {
                                LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                                LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                                FailedToStartProgrammer = true;
                            }
                        }
                        if (!AutoEmergencyReset || Timeout)
                        {
                            AutoEmergencyReset = false;

                            if (!FailedToStartProgrammer)
                            {
                                LogFile.Log("The phone is in emergency mode and you didn't provide an emergency programmer", LogType.ConsoleOnly);
                                LogFile.Log("This phone also doesn't seem to reboot after a timeout, so you got to help a bit", LogType.ConsoleOnly);
                                LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                                LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                                LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                                LogFile.Log("To prevent this, provide an emergency programmer next time you will unlock a bootloader", LogType.ConsoleOnly);
                                LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                                SetWorkingStatus("You need to manually reset your phone now!",
                                    "The phone is in emergency mode and you didn't provide an emergency programmer." +
                                    " This phone also doesn't seem to reboot after a timeout, so you got to help a bit." +
                                    " Keep the phone connected to the PC." +
                                    " The unlock-sequence will resume automatically. To prevent this, provide an emergency programmer next time you will unlock a bootloader.",
                                    null, false, WPinternalsStatus.WaitingForManualReset);
                            }
                            else
                            {
                                LogFile.Log("The phone is in emergency mode and we couldn't start the emergency programmer", LogType.ConsoleOnly);
                                LogFile.Log("This phone also doesn't seem to reboot after a timeout, so you got to help a bit", LogType.ConsoleOnly);
                                LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                                LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                                LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                                LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                                SetWorkingStatus("You need to manually reset your phone now!",
                                    "The phone is in emergency mode and we couldn't start the emergency programmer." +
                                    " This phone also doesn't seem to reboot after a timeout, so you got to help a bit." +
                                    " Keep the phone connected to the PC." +
                                    " The unlock-sequence will resume automatically.",
                                    null, false, WPinternalsStatus.WaitingForManualReset);
                            }
                            await Notifier.WaitForRemoval();

                            UpdateWorkingStatus("Initializing flash...", null, null);

                            await Notifier.WaitForArrival();
                        }
                    }
                }

                #endregion

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader))
                {
                    throw new WPinternalsException("Phone is in wrong mode", "The phone should have been detected in bootloader or flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                }

                UpdateWorkingStatus("Initializing flash...", null, null);
            }

            if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
            {
                ((LumiaBootManagerAppModel)Notifier.CurrentModel).ResetPhoneToFlashMode();
            }

            if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
            {
                await Notifier.WaitForArrival();
            }

            if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
            {
                throw new WPinternalsException("Unexpected Mode");
            }

            // The payloads must be ordered by the number of locations
            //
            // FlashApp processes payloads like this:
            // - First payloads which are with one location, those can be sent in bulk
            // - Then payloads with more than one location, those should not be sent in bulk
            //
            // If you do not order payloads like this, you will get an error, most likely hash mismatch
            //
            FlashingPayload[] payloads = [];
            if (FlashParts != null)
            {
                payloads =
                [
                    .. GetNonOptimizedPayloads(FlashParts, FFU.ChunkSize, (uint)(FlashInfo.WriteBufferSize / FFU.ChunkSize), SetWorkingStatus, UpdateWorkingStatus).OrderBy(x => x.TargetLocations.Length),
                ];
            }

            bool AssumeImageHeaderFallsInGap = true;
            bool AllocateAsyncBuffersOnPhone = true;
            bool AllocateBackupBuffersOnPhone = false;
            UInt32 CurrentGapFill = EstimatedGapFill;
            UInt32 OldGapFill;
            bool Success = false;
            bool Abort = false;
            bool PhoneNeedsReset = false;
            bool WaitForReset = false;
            int AttemptCount = 0;
            UInt32 ExploitHeaderAllocationSize = 0;
            UInt32 LastHeaderV2Size = 0;
            byte[] PartialHeader;
            byte[] FfuHeader;
            UInt64 CombinedFFUHeaderSize;
            Allocation SecurityHeaderAllocation = null;
            Allocation ImageHeaderAllocation = null;
            Allocation StoreHeaderAllocation = null;
            Allocation PartialHeaderAllocation = null;
            UInt32 HeaderOffset = 0;
            bool Scanning = false;
            bool ResetScanning = false;

            Profile = App.Config.GetProfile(FlashInfo.PlatformID, FlashInfo.Firmware, FFU.GetFirmwareVersion());
            if (Profile == null)
            {
                LogFile.Log("No flashing profile found", LogType.FileAndConsole);
            }
            else
            {
                if (ShowProgress)
                {
                    LogFile.Log("Flashing profile loaded", LogType.FileAndConsole);
                }

                CurrentGapFill = Profile.FillSize;
                ExploitHeaderAllocationSize = Profile.HeaderSize;
                AllocateAsyncBuffersOnPhone = Profile.AllocateAsyncBuffersOnPhone;
                AssumeImageHeaderFallsInGap = Profile.AssumeImageHeaderFallsInGap;
            }

            do
            {
                AttemptCount++;
                if ((Profile == null) || (AttemptCount > 1))
                {
                    LogFile.Log("Custom flash attempt: " + AttemptCount + " of " + MaximumAttempts, LogType.FileAndConsole);

                    if (!Scanning)
                    {
                        SetWorkingStatus("Scanning for flashing-profile - attempt " + AttemptCount.ToString() + " of " + MaximumAttempts.ToString(), "Your phone may appear to be in a reboot-loop. This is expected behavior. Don't interfere this process.", (uint)MaximumAttempts, Status: WPinternalsStatus.Scanning);
                    }

                    Scanning = true;
                    UpdateWorkingStatus("Scanning for flashing-profile - attempt " + AttemptCount.ToString() + " of " + MaximumAttempts.ToString(), "Your phone may appear to be in a reboot-loop. This is expected behavior. Don't interfere this process.", (uint)AttemptCount, Status: WPinternalsStatus.Scanning);

                    ExploitHeaderAllocationSize = CurrentGapFill + (UInt32)FFU.ChunkSize;
                }

                // Initialize flash attempts
                // Make sure async buffers are allocated on the phone before overflow attempts,
                // or else failed attempts may cause more memory-gaps and allocation becomes more unpredictable.
                // The phone is rebooted after each attempt (to avoid memory-corruption).
                // And it seems that that normally all allocations are in a big memory-gap, which was created before BootMgr was loaded.
                // And there is still memory allocated in a lower range.
                // And by allocating USB buffers, it could cause more memory-scattering.
                // On Lumia 950 2 USB buffers are allocated and on Lumia 930 there is only one USB buffer allocated.
                // StartAsyncFlash() is needed on 950 and 640. But not needed on 930!
                // In any case, the allocation of the async-buffers should not be simulated in the Uefi Memory Simulator,
                // because that would create a gap in the Simulator, instead of avoiding a gap on the phone.
                //
                if (AllocateAsyncBuffersOnPhone)
                {
                    ((LumiaFlashAppModel)Notifier.CurrentModel).StartAsyncFlash();
                    ((LumiaFlashAppModel)Notifier.CurrentModel).EndAsyncFlash(); // Ending Async flashing is not necessary for Lumia 950, but it is necessary for Lumia 640!
                }

                if (AllocateBackupBuffersOnPhone)
                {
                    ((LumiaFlashAppModel)Notifier.CurrentModel).BackupPartitionToRam("MODEM_FSG");
                    ((LumiaFlashAppModel)Notifier.CurrentModel).BackupPartitionToRam("MODEM_FS1");
                    ((LumiaFlashAppModel)Notifier.CurrentModel).BackupPartitionToRam("MODEM_FS2");
                    ((LumiaFlashAppModel)Notifier.CurrentModel).BackupPartitionToRam("SSD");
                    ((LumiaFlashAppModel)Notifier.CurrentModel).BackupPartitionToRam("DPP");
                }

                HeaderOffset = 0;
                SecurityHeaderAllocation = null;
                ImageHeaderAllocation = null;
                StoreHeaderAllocation = null;
                PartialHeaderAllocation = null;
                UInt32 DestinationChunkIndex = 0;

                // Create memory map
                UefiMemorySim.Reset();
                SecurityHeaderAllocation = UefiMemorySim.AllocatePool((uint)FFU.SecurityHeader.Length);
                SecurityHeaderAllocation.CopyToThisAllocation(FFU.SecurityHeader, 0, (uint)FFU.SecurityHeader.Length, 0);
                if (!AssumeImageHeaderFallsInGap)
                {
                    ImageHeaderAllocation = UefiMemorySim.AllocatePool((uint)FFU.ImageHeader.Length);
                    ImageHeaderAllocation.CopyToThisAllocation(FFU.ImageHeader, 0, (uint)FFU.ImageHeader.Length, 0);
                }
                StoreHeaderAllocation = UefiMemorySim.AllocatePool((uint)FFU.StoreHeader.Length);
                StoreHeaderAllocation.CopyToThisAllocation(FFU.StoreHeader, 0, (uint)FFU.StoreHeader.Length, 0);

                // Simulate sending partial header
                PartialHeaderAllocation = UefiMemorySim.AllocatePool(ExploitHeaderAllocationSize);

                CombinedFFUHeaderSize = FFU.HeaderSize;
                FfuHeader = new byte[CombinedFFUHeaderSize];
                UInt32 TotalPayloadCount = (uint)payloads.Length;
                bool HeadersFull;
                int FlashingPhase = 0;
                UInt32 FlashingPhaseStartPayloadIndex = 0;
                UInt32 FlashingPhasePayloadCount = 0;
                bool FlashInProgress = false;
                byte[] Buffer = new byte[FFU.ChunkSize];
                LastHeaderV2Size = 0;
                do
                {
                    HeadersFull = false;

                    // On every flashing phase we must fill the memory gap first, before sending the header.
                    if (CurrentGapFill > UefiMemorySim.PageSize)
                    {
                        if (FlashingPhase > 0)
                        {
                            // Avoid Error 0x0010 "Invalid sub block length".
                            // Headersize must increase compared to last time a header was sent, to avoid processing the header.
                            // Offset must be 0, to reset buffers.
                            // Previous data + new data must fit in new headersize.
                            // We can send an extra byte, because last memory buffer was sent including the tail.
                            // And there is always extra space in the memoryspace after the tail.
                            ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV2(LastHeaderV2Size + 1, 0, new byte[1], Options);
                        }

                        // CurrentGapFill is the amount of data we want to be allocated on the phone
                        // But we send less data, so the header won't be processed yet.
                        PartialHeader = new byte[UefiMemorySim.PageSize];
                        ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV2(CurrentGapFill, 0, PartialHeader, Options); // Fill memory gap -> This will fail on phones with Flash Protocol v1.x !! On Lumia 640 this will hang on receiving the response when EndAsyncFlash was not called.
                    }

                    using (FileStream FfuFile = new(FFU.Path, FileMode.Open, FileAccess.Read))
                    {
                        // On every flashing phase we need to send the full header again to reset all the counters.
                        FfuFile.Read(FfuHeader, 0, (int)CombinedFFUHeaderSize);
                        ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV1(FfuHeader, Options);

                        if (PerformFullFlashFirst && (FlashingPhase == 0))
                        {
                            // If we flash the stock ROM at this point, the header is in memory is not overwritten yet.
                            // This means that after the last chunk was written, the flashing-status is written to NV (and also flushed).
                            // But this doesn't matter, because even when we want to overwrite NV, this happens later.
                            // When the header is successfully overwritten in memory, the next chunk of custom data will be allowed to be written.

                            LogFile.Log("Starting custom flash attempt by doing a full flash.", LogType.FileAndConsole);

                            UInt64 Position = CombinedFFUHeaderSize;
                            byte[] FlashPayload;
                            int ChunkIndex = 0;
                            UInt32 TotalChunkCount = (UInt32)FFU.TotalChunkCount;

                            // Protocol v2
                            FlashPayload = new byte[FlashInfo.WriteBufferSize];

                            while (Position < (UInt64)FfuFile.Length)
                            {
                                UInt32 CommonFlashPayloadSize = FlashInfo.WriteBufferSize;
                                if (((UInt64)FfuFile.Length - Position) < CommonFlashPayloadSize)
                                {
                                    CommonFlashPayloadSize = (UInt32)((UInt64)FfuFile.Length - Position);
                                    FlashPayload = new byte[CommonFlashPayloadSize];
                                }

                                FfuFile.Read(FlashPayload, 0, (int)CommonFlashPayloadSize);
                                ChunkIndex += (int)(CommonFlashPayloadSize / FFU.ChunkSize);
                                ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuPayloadV2(FlashPayload, ShowProgress ? (int)((double)(ChunkIndex + 1) * 100 / TotalChunkCount) : 0, 0);
                                Position += CommonFlashPayloadSize;
                            }
                        }
                    }

                    UInt32 NewWriteDescriptorOffset = 0xF8;
                    UInt32 CatalogSize = ByteOperations.ReadUInt32(UefiMemorySim.Buffer, SecurityHeaderAllocation.ContentStart + 0x18);
                    UInt32 NewHashOffset = 0x20 + CatalogSize;

                    UInt32 WriteDescriptorLength = 0;
                    UInt32 WriteDescriptorCount = 0;
                    UInt32 HashTableSize = 0;

                    if (PerformFullFlashFirst && (FlashingPhase == 0))
                    {
                        // Set offset for new descriptor to end of descriptor-table
                        WriteDescriptorLength = ByteOperations.ReadUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xD4);
                        NewWriteDescriptorOffset += WriteDescriptorLength;

                        // Get descriptor-count of the FFU
                        WriteDescriptorCount = ByteOperations.ReadUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xD0);

                        // Set offset for new hash to end of hash-table
                        HashTableSize = ByteOperations.ReadUInt32(UefiMemorySim.Buffer, SecurityHeaderAllocation.ContentStart + 0x1C);
                        NewHashOffset += HashTableSize; // Skip to the end of the original hash-table.
                    }
                    else
                    {
                        // From start of hash-table skip the first hashes for Image- and StoreHeaders.
                        HashTableSize = (UInt32)((FFU.ImageHeader.Length + FFU.StoreHeader.Length) / FFU.ChunkSize * 0x20);
                        NewHashOffset += HashTableSize;
                    }

                    // Determine available space and number of payloads to send for this phase
                    UInt32 HashSpace = (UInt32)(FFU.SecurityHeader.Length - NewHashOffset);
                    UInt32 DescriptorSpace = (UInt32)(FFU.StoreHeader.Length - NewWriteDescriptorOffset);

                    FlashingPhasePayloadCount = 0;

                    // Always flash one extra chunk on the GPT (for purpose of testing and for making sure that first chunk does not contain all zero's).
                    UInt32 SecurityHeaderSize = FlashInProgress ? 0 : 0x20u;
                    UInt32 StoreHeaderSize = FlashInProgress ? 0 : 0x10u;
                    for (UInt32 i = FlashingPhaseStartPayloadIndex; i < payloads.Length; i++)
                    {
                        UInt32 NewSecurityHeaderSize = SecurityHeaderSize + payloads[i].GetSecurityHeaderSize();
                        UInt32 NewStoreHeaderSize = StoreHeaderSize + payloads[i].GetStoreHeaderSize();

                        if (NewSecurityHeaderSize > HashSpace || NewStoreHeaderSize > DescriptorSpace)
                        {
                            HeadersFull = true;
                            break;
                        }

                        FlashingPhasePayloadCount++;
                        SecurityHeaderSize = NewSecurityHeaderSize;
                        StoreHeaderSize = NewStoreHeaderSize;
                    }

                    HashTableSize += SecurityHeaderSize;
                    WriteDescriptorCount += FlashingPhasePayloadCount + (FlashInProgress ? 0 : 1u);
                    WriteDescriptorLength += StoreHeaderSize;

                    if (!ClearFlashingStatusAtEnd || HeadersFull)
                    {
                        WriteDescriptorCount++;
                    }

                    // Write back new header values.
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xD4, WriteDescriptorLength);
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xD0, WriteDescriptorCount);
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, SecurityHeaderAllocation.ContentStart + 0x1C, HashTableSize);
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xEC, 0); // FlashOnlyTableLength - Make flash progress bar white immediately.
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xE8, 1); // FlashOnlyTableCount

                    // Write new descriptors
                    // First write descriptor and hash for the first GPT chunk
                    if (!FlashInProgress) // We only send the first GPT chunk when flash is not in progress yet.
                    {
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x00, 0x00000001); // Location count
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x04, 0x00000001); // Chunk count
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x08, 0x00000000); // Disk access method (0 = Begin, 2 = End)
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x0C, 0x00000000); // Chunk index = GPT
                        NewWriteDescriptorOffset += 0x10;
                        byte[] GPTHashValue = System.Security.Cryptography.SHA256.Create().ComputeHash(GPTChunk, 0, FFU.ChunkSize); // Hash is 0x20 bytes
                        System.Buffer.BlockCopy(GPTHashValue, 0, UefiMemorySim.Buffer, (int)(SecurityHeaderAllocation.ContentStart + NewHashOffset), 0x20);
                        NewHashOffset += 0x20;
                    }

                    for (UInt32 i = 0; i < FlashingPhasePayloadCount; i++)
                    {
                        FlashingPayload payload = payloads[FlashingPhaseStartPayloadIndex + i];

                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x00, (UInt32)payload.TargetLocations.Length); // Location count
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x04, payload.ChunkCount);                      // Chunk count
                        NewWriteDescriptorOffset += 0x08;

                        foreach (UInt32 location in payload.TargetLocations)
                        {
                            ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x00, 0x00000000);                          // Disk access method (0 = Begin, 2 = End)
                            ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x04, location);                            // Chunk index
                            NewWriteDescriptorOffset += 0x08;
                        }

                        foreach (byte[] hashValue in payload.ChunkHashes)
                        {
                            // Write new hash
                            System.Buffer.BlockCopy(hashValue, 0, UefiMemorySim.Buffer, (Int32)(SecurityHeaderAllocation.ContentStart + NewHashOffset), 0x20);
                            NewHashOffset += 0x20;
                        }
                    }

                    Stream CurrentStream = null;
                    int StreamIndex = 0;
                    int Step = 0;
                    try
                    {
                        // Send a small portion of header v2 at offset 0
                        // The payload is smaller than the total headersize, so that it won't start processing the header now.
                        // This will allocate new memory at the bottom of the memory-pool, but it will not reset the previously imported ffu header.
                        Step = 1;
                        PartialHeader = new byte[UefiMemorySim.PageSize];
                        ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV2(ExploitHeaderAllocationSize, 0, PartialHeader, Options); // SkipWrite = 1 (only works on engineering phones)

                        // Now we will send the rest of the exploit header, but we will increase the total size even higher, so that it still won't start processing the headers.
                        // We've send only a small first part of the header. The allocated header was bigger: ExploitHeaderAllocationSize.
                        // But we HAVE to send the whole header. We can't skip a part.
                        Step = 2;
                        UInt32 ExploitHeaderRemaining = SecurityHeaderAllocation.TailEnd + 1 - PartialHeaderAllocation.ContentStart - (UInt32)PartialHeader.Length;
                        HeaderOffset = (UInt32)PartialHeader.Length;
                        while (ExploitHeaderRemaining > 0)
                        {
                            UInt32 CurrentFill = ExploitHeaderRemaining;
                            if (CurrentFill > FlashInfo.WriteBufferSize)
                            {
                                CurrentFill = FlashInfo.WriteBufferSize;
                            }

                            PartialHeader = new byte[CurrentFill];
                            PartialHeaderAllocation.CopyFromThisAllocation(HeaderOffset, CurrentFill, PartialHeader, 0);
                            ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV2(HeaderOffset + CurrentFill + 1, HeaderOffset, PartialHeader, Options); // Phone may crash here. USB write is done. USB read might fail due to crash. Happens on my own Lumia 650.
                            LastHeaderV2Size = HeaderOffset + CurrentFill + 1;
                            ExploitHeaderRemaining -= CurrentFill;
                            HeaderOffset += CurrentFill;
                        }

                        // Send custom payload
                        Step = 3;
                        Int32 payloadCount = 0;
                        byte[] payloadBuffer = new byte[FlashInfo.WriteBufferSize];
                        bool sendPayload = false;
                        for (Int32 i = FlashInProgress ? 0 : -1; i < FlashingPhasePayloadCount; i++)
                        {
                            string NewProgressText = "Flashing resources...";
                            if (!FlashInProgress)
                            {
                                // First send the GPT chunk
                                Step = 4;
                                System.Buffer.BlockCopy(GPTChunk, 0, Buffer, 0, FFU.ChunkSize);

                                Step = 8;
                                // This may fail. Normally with WPinternalsException for Invalid Hash or Data not aligned.
                                // Or it may fail with a BadConnectionException when the phone crashes and drops the connection.
                                ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuPayloadV1(Buffer, 0);
                                if (!FlashInProgress)
                                {
                                    Step = 9;
                                    if (ShowProgress)
                                    {
                                        LogFile.Log("Flashing in progress!", LogType.FileAndConsole);
                                    }

                                    FlashInProgress = true;
                                    Scanning = false;
                                    SetWorkingStatus(null, null, (UInt64?)payloads.Length, Status: WPinternalsStatus.Flashing);
                                }
                            }
                            else
                            {
                                Step = 5;

                                FlashingPayload payload = payloads[FlashingPhaseStartPayloadIndex + i];

                                if (payloadCount == ((FlashInfo.WriteBufferSize / FFU.ChunkSize) - 1))
                                {
                                    sendPayload = true;
                                }

                                if (FlashingPhaseStartPayloadIndex + i + 1 >= FlashingPhasePayloadCount)
                                {
                                    sendPayload = true;
                                    byte[] tmpBuffer = new byte[(payloadCount + 1) * FFU.ChunkSize];
                                    System.Buffer.BlockCopy(payloadBuffer, 0, tmpBuffer, 0, payloadCount * FFU.ChunkSize);
                                    payloadBuffer = tmpBuffer;
                                }

                                // Check if the next payload contains more than one chunk
                                if (!sendPayload && FlashingPhaseStartPayloadIndex + i + 1 < FlashingPhasePayloadCount && (payloads[FlashingPhaseStartPayloadIndex + i + 1].ChunkCount != 1 || payloads[FlashingPhaseStartPayloadIndex + i + 1].TargetLocations.Length != 1))
                                {
                                    sendPayload = true;
                                    byte[] tmpBuffer = new byte[(payloadCount + 1) * FFU.ChunkSize];
                                    System.Buffer.BlockCopy(payloadBuffer, 0, tmpBuffer, 0, payloadCount * FFU.ChunkSize);
                                    payloadBuffer = tmpBuffer;
                                }

                                // We prepare the buffer setup above with all consecutive chunks we have to send in
                                // We can't send a single chunk otherwise we would get 0x1007: Payload data does not contain all data
                                if (payload.ChunkCount != 1)
                                {
                                    NewProgressText = "Flashing common resources...";
                                    payloadBuffer = new byte[payload.ChunkCount * FFU.ChunkSize];
                                    for (uint j = 0; j < payload.ChunkCount; j++)
                                    {
                                        StreamIndex = (Int32)payload.StreamIndexes[j];
                                        FlashPart flashPart = FlashParts[StreamIndex];
                                        CurrentStream = flashPart.Stream;
                                        CurrentStream.Seek(payload.StreamLocations[j], SeekOrigin.Begin);

                                        Step = 6;
                                        Array.Clear(payloadBuffer, (Int32)(FFU.ChunkSize * j), FFU.ChunkSize); // Not really needed anymore?

                                        Step = 7;
                                        CurrentStream.Read(payloadBuffer, (Int32)(FFU.ChunkSize * j), FFU.ChunkSize);
                                    }
                                }

                                if (payload.TargetLocations.Length != 1)
                                {
                                    NewProgressText = "Flashing common resources...";
                                    payloadBuffer = new byte[FFU.ChunkSize];
                                }

                                if (payload.ChunkCount == 1)
                                {
                                    StreamIndex = (Int32)payload.StreamIndexes[0];
                                    FlashPart flashPart = FlashParts[StreamIndex];
                                    CurrentStream = flashPart.Stream;
                                    CurrentStream.Seek(payload.StreamLocations[0], SeekOrigin.Begin);

                                    if (payload.TargetLocations.Length == 1 && !string.IsNullOrEmpty(flashPart.ProgressText))
                                    {
                                        NewProgressText = flashPart.ProgressText;
                                    }

                                    CurrentStream.Read(payloadBuffer, FFU.ChunkSize * payloadCount, FFU.ChunkSize);
                                }

                                Step = 8;
                                // This may fail. Normally with WPinternalsException for Invalid Hash or Data not aligned.
                                // Or it may fail with a BadConnectionException when the phone crashes and drops the connection.

                                payloadCount++;
                            }

                            UpdateWorkingStatus(NewProgressText, null, (UInt64?)(FlashingPhaseStartPayloadIndex + i + 1), WPinternalsStatus.Flashing);

                            if (i != -1 && sendPayload)
                            {
                                // This fails when sending multiple chunks per payload with 0x1003: Hash mismatch
                                ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuPayloadV2(payloadBuffer, ShowProgress ? (Int32)((FlashingPhaseStartPayloadIndex + i + 1) * 100 / payloads.Length) : 0);
                                sendPayload = false;
                                payloadCount = 0;
                                payloadBuffer = new byte[FlashInfo.WriteBufferSize];
                            }

                            DestinationChunkIndex++;
                        }

                        Step = 10;
                        FlashingPhaseStartPayloadIndex += FlashingPhasePayloadCount;

                        Step = 11;
                        if (!HeadersFull)
                        {
                            Step = 12;
                            App.Config.SetProfile(PhoneInfo.Type, FlashInfo.PlatformID, PhoneInfo.ProductCode, FlashInfo.Firmware, FFU.GetFirmwareVersion(), CurrentGapFill, ExploitHeaderAllocationSize, AssumeImageHeaderFallsInGap, AllocateAsyncBuffersOnPhone);
                            if (ShowProgress)
                            {
                                LogFile.Log("Custom flash succeeded!", LogType.FileAndConsole);
                            }

                            Success = true;
                        }
                        else
                        {
                            // At this point we're missing a few payloads, so we need to start again.
                            LogFile.Log("Reinitilizing a new flashing attempt because headers were full and we're not quite done yet!");
                        }
                    }
                    catch (BadConnectionException)
                    {
                        LogFile.Log("Connection to phone is lost - " +
                            Step.ToString() + " " +
                            StreamIndex.ToString() + " " +
                            (CurrentStream == null ? "0" : CurrentStream.Position.ToString()) + " " +
                            FlashingPhase.ToString() + " " +
                            FlashingPhaseStartPayloadIndex.ToString() + " " +
                            DestinationChunkIndex.ToString());
                        LogFile.Log("Expect phone to reboot", LogType.FileAndConsole);
                        WaitForReset = true;
                    }
                    catch (Exception Ex)
                    {
                        if (FlashInProgress)
                        {
                            // Normally, when we end up here, we were not in process of flashing yet.
                            // It would be a flash attempt which failed.
                            // But if we were already flashing, then something else is wrong.
                            // We need more info and stop flashing.
                            LogFile.Log("Custom flash failed", LogType.FileAndConsole);
                            LogFile.LogException(Ex, LogType.FileOnly,
                                Step.ToString() + " " +
                                StreamIndex.ToString() + " " +
                                (CurrentStream == null ? "0" : CurrentStream.Position.ToString()) + " " +
                                FlashingPhase.ToString() + " " +
                                FlashingPhaseStartPayloadIndex.ToString() + " " +
                                DestinationChunkIndex.ToString());
                            Abort = true;
                        }
                        else
                        {
                            LogFile.Log("Custom flash attempt failed", LogType.FileAndConsole);
                            LogFile.LogException(Ex, LogType.FileOnly,
                                Step.ToString() + " " +
                                StreamIndex.ToString() + " " +
                                (CurrentStream == null ? "0" : CurrentStream.Position.ToString()) + " " +
                                FlashingPhase.ToString() + " " +
                                FlashingPhaseStartPayloadIndex.ToString() + " " +
                                DestinationChunkIndex.ToString());
                        }

                        PhoneNeedsReset = true;
                    }

                    if (FlashInProgress)
                    {
                        FlashingPhase++;
                    }
                }
                while (HeadersFull && FlashInProgress && !Abort);

                if (!Success)
                {
                    if ((Profile != null) && !Abort)
                    {
                        LogFile.Log("Flashing profile was loaded, but it is not working", LogType.FileAndConsole);
                        LogFile.Log("Attempting to find a working profile", LogType.FileAndConsole);
                        ResetScanning = true;
                    }

                    if (PhoneNeedsReset)
                    {
                        ((LumiaFlashAppModel)Notifier.CurrentModel).ResetPhone();
                        WaitForReset = true;
                    }

                    if (WaitForReset)
                    {
                        #region Properly recover from reset between flash attempts - many phones respond differently

                        Timeout = false;
                        try
                        {
                            await Notifier.WaitForArrival().TimeoutAfter(TimeSpan.FromSeconds(40));
                        }
                        catch (TimeoutException)
                        {
                            Timeout = true;
                        }
                        if ((Notifier.CurrentInterface == null) && (!AutoEmergencyReset || Timeout))
                        {
                            AutoEmergencyReset = false;

                            if (Timeout)
                            {
                                LogFile.Log("The phone is not responding", LogType.ConsoleOnly);
                                LogFile.Log("It might be in emergency mode, while you have no matching driver installed", LogType.ConsoleOnly);
                            }
                            LogFile.Log("To continue the unlock-sequence, the phone needs to be rebooted", LogType.ConsoleOnly);
                            LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                            LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                            LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                            LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                            UpdateWorkingStatus("You need to manually reset your phone now!", (Timeout ? "The phone is not responding. It might be in emergency mode, while you have no matching driver installed. " : "") + "To continue the unlock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. The unlock-sequence will resume automatically.", null, WPinternalsStatus.WaitingForManualReset);

                            await Notifier.WaitForRemoval();

                            UpdateWorkingStatus("Scanning for flashing-profile - attempt " + AttemptCount.ToString() + " of " + MaximumAttempts.ToString(), "Your phone may appear to be in a reboot-loop. This is expected behavior. Don't interfere this process.", (uint)AttemptCount, Status: WPinternalsStatus.Scanning);

                            await Notifier.WaitForArrival();
                        }
                        if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                        {
                            bool FailedToStartProgrammer = false;
                            if (ProgrammerPath != null)
                            {
                                QualcommSahara Sahara = new((QualcommSerial)Notifier.CurrentModel);
                                QualcommFirehose Firehose = new((QualcommSerial)Notifier.CurrentModel);
                                try
                                {
                                    await Sahara.LoadProgrammer(ProgrammerPath);
                                    await Firehose.Reset();
                                    await Notifier.WaitForArrival();
                                }
                                catch (BadConnectionException)
                                {
                                    FailedToStartProgrammer = true;
                                }
                            }

                            if (ProgrammerPath == null || FailedToStartProgrammer)
                            {
                                ((QualcommSerial)Notifier.CurrentModel).Close(); // Prevent "Resource in use";

                                Timeout = false;
                                if (AutoEmergencyReset)
                                {
                                    try
                                    {
                                        await Notifier.WaitForArrival().TimeoutAfter(TimeSpan.FromSeconds(40));
                                    }
                                    catch (TimeoutException)
                                    {
                                        Timeout = true;
                                    }
                                    catch (Exception ex)
                                    {
                                        LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                                        LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                                        FailedToStartProgrammer = true;
                                    }
                                }
                                if (!AutoEmergencyReset || Timeout)
                                {
                                    AutoEmergencyReset = false;
                                    if (!FailedToStartProgrammer)
                                    {
                                        LogFile.Log("The phone is in emergency mode and you didn't provide an emergency programmer", LogType.ConsoleOnly);
                                        LogFile.Log("This phone also doesn't seem to reboot after a timeout, so you got to help a bit", LogType.ConsoleOnly);
                                        LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                                        LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                                        LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                                        LogFile.Log("To prevent this, provide an emergency programmer next time you will unlock a bootloader", LogType.ConsoleOnly);
                                        LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                                        SetWorkingStatus("You need to manually reset your phone now!",
                                            "The phone is in emergency mode and you didn't provide an emergency programmer." +
                                            " This phone also doesn't seem to reboot after a timeout, so you got to help a bit." +
                                            " Keep the phone connected to the PC." +
                                            " The unlock-sequence will resume automatically. To prevent this, provide an emergency programmer next time you will unlock a bootloader.",
                                            null, false, WPinternalsStatus.WaitingForManualReset);
                                    }
                                    else
                                    {
                                        LogFile.Log("The phone is in emergency mode and we couldn't start the emergency programmer", LogType.ConsoleOnly);
                                        LogFile.Log("This phone also doesn't seem to reboot after a timeout, so you got to help a bit", LogType.ConsoleOnly);
                                        LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                                        LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                                        LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                                        LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                                        SetWorkingStatus("You need to manually reset your phone now!",
                                            "The phone is in emergency mode and we couldn't start the emergency programmer." +
                                            " This phone also doesn't seem to reboot after a timeout, so you got to help a bit." +
                                            " Keep the phone connected to the PC." +
                                            " The unlock-sequence will resume automatically.",
                                            null, false, WPinternalsStatus.WaitingForManualReset);
                                    }

                                    await Notifier.WaitForRemoval();

                                    UpdateWorkingStatus("Scanning for flashing-profile - attempt " + AttemptCount.ToString() + " of " + MaximumAttempts.ToString(), "Your phone may appear to be in a reboot-loop. This is expected behavior. Don't interfere this process.", (uint)AttemptCount, Status: WPinternalsStatus.Scanning);

                                    await Notifier.WaitForArrival();
                                }
                            }
                        }

                        #endregion

                        // Sanity check: must be in flash mode
                        if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader))
                        {
                            break;
                        }

                        // In case we are on an Engineering phone which isn't stuck in flashmode and booted to BootMgrApp
                        ((LumiaBootManagerAppModel)Notifier.CurrentModel).SwitchToFlashAppContext();
                        ((LumiaFlashAppModel)Notifier.CurrentModel).DisableRebootTimeOut();
                    }

                    PhoneNeedsReset = false;
                    WaitForReset = false;

                    // Calculate variables for next attempt
                    if (ResetScanning)
                    {
                        AssumeImageHeaderFallsInGap = true;
                        AllocateAsyncBuffersOnPhone = true;
                        AllocateBackupBuffersOnPhone = false;
                        CurrentGapFill = EstimatedGapFill;
                        Profile = null;
                        ResetScanning = false;
                    }
                    else if (Experimental && !AllocateBackupBuffersOnPhone)
                    {
                        AllocateBackupBuffersOnPhone = true;
                    }
                    else
                    {
                        AllocateBackupBuffersOnPhone = false;
                        if (AllocateAsyncBuffersOnPhone)
                        {
                            AllocateAsyncBuffersOnPhone = false;
                        }
                        else
                        {
                            AllocateAsyncBuffersOnPhone = true;
                            OldGapFill = CurrentGapFill;
                            if (OldGapFill <= EstimatedGapFill)
                            {
                                CurrentGapFill = EstimatedGapFill + (EstimatedGapFill - OldGapFill) + (UInt32)FFU.ChunkSize;
                                if (CurrentGapFill > MaximumGapFill)
                                {
                                    if (OldGapFill > 0)
                                    {
                                        CurrentGapFill = OldGapFill - (UInt32)FFU.ChunkSize;
                                    }
                                    else if (AssumeImageHeaderFallsInGap)
                                    {
                                        AssumeImageHeaderFallsInGap = false;
                                        CurrentGapFill = EstimatedGapFill;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                if (OldGapFill <= (EstimatedGapFill * 2))
                                {
                                    CurrentGapFill = EstimatedGapFill - (OldGapFill - EstimatedGapFill);
                                }
                                else
                                {
                                    CurrentGapFill = OldGapFill + (UInt32)FFU.ChunkSize;
                                    if (CurrentGapFill > MaximumGapFill)
                                    {
                                        if (AssumeImageHeaderFallsInGap)
                                        {
                                            AssumeImageHeaderFallsInGap = false;
                                            CurrentGapFill = EstimatedGapFill;
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            while (!Success && !Abort);

            // Now we will first try to create a memory corruption in the phone, before we reset the phone.
            // The memory corruption will cause the phone to abort the shutdown-sequence and switch to emergency mode immediately.
            // We already avoided that the FlashingStatus was written to NV.
            // But we also need to avoid that the BootFlag is written to NV when the phone is properly shut down, because that will overwrite the NV vars we wrote earlier.
            if (Success && !ClearFlashingStatusAtEnd)
            {
                // Make the phone crash here!
                // This will actually make the phone crash when it frees memory during shutdown or reboot of the phone

                ByteOperations.WriteUInt32(UefiMemorySim.Buffer, SecurityHeaderAllocation.HeadStart + 4, 0); // Set allocation size to 0 in allocationhead
                ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.HeadStart + 4, 0); // Set allocation size to 0 in allocationhead
                if (CurrentGapFill > UefiMemorySim.PageSize)
                {
                    ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV2(LastHeaderV2Size + 1, 0, new byte[1], Options);
                    PartialHeader = new byte[UefiMemorySim.PageSize];
                    ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV2(CurrentGapFill, 0, PartialHeader, Options); // Fill memory gap
                }
                using (FileStream FfuFile = new(FFU.Path, FileMode.Open, FileAccess.Read))
                {
                    // On every flashing phase we need to send the full header again, because this triggers ffu_import_invalidate(), which is necessary to reset all the counters.
                    FfuFile.Read(FfuHeader, 0, (int)CombinedFFUHeaderSize);
                    ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV1(FfuHeader, Options);
                }
                PartialHeader = new byte[UefiMemorySim.PageSize];
                ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV2(ExploitHeaderAllocationSize, 0, PartialHeader, Options); // SkipWrite = 1 (only works on engineering phones)
                UInt32 ExploitHeaderRemaining = SecurityHeaderAllocation.TailEnd + 1 - PartialHeaderAllocation.ContentStart - (UInt32)PartialHeader.Length;
                HeaderOffset = (UInt32)PartialHeader.Length;
                while (ExploitHeaderRemaining > 0)
                {
                    UInt32 CurrentFill = ExploitHeaderRemaining;
                    if (CurrentFill > FlashInfo.WriteBufferSize)
                    {
                        CurrentFill = FlashInfo.WriteBufferSize;
                    }

                    PartialHeader = new byte[CurrentFill];
                    PartialHeaderAllocation.CopyFromThisAllocation(HeaderOffset, CurrentFill, PartialHeader, 0);
                    ((LumiaFlashAppModel)Notifier.CurrentModel).SendFfuHeaderV2(HeaderOffset + CurrentFill + 1, HeaderOffset, PartialHeader, Options);
                    LastHeaderV2Size = HeaderOffset + CurrentFill + 1;
                    ExploitHeaderRemaining -= CurrentFill;
                    HeaderOffset += CurrentFill;
                }

                // Do the actual reset, which will result in a crash while cleaning up memory
                ((LumiaFlashAppModel)Notifier.CurrentModel).ResetPhone();

                LogFile.Log("Phone performs hard exit", LogType.FileAndConsole);

                #region Properly recover from reset at the end of custom flash - many phones respond differently

                // Wait for the phone to boot to emergency mode
                // Emergency mode is always triggered on purpose to avoid writing NV vars
                // We also wait for emergency mode when no valid programmer is present, or else caller-code will not know at which stage the phone is rebooting

                // Possibilities here:
                // - Lumia 950 or 950 XL which does not crash to emergency mode, switching to mass storage mode, but not assigning a drive-letter -> Bootmgr, Nothing
                // - Lumia 950 or 950 XL which does not crash to emergency mode, switching to mass storage mode, and assigning a drive-letter -> Bootmgr, MSM
                // - Other Lumia's, switching to mass storage mode, but not assigning a drive-letter -> Bootmgr, Nothing

                Timeout = false;
                try
                {
                    await Notifier.WaitForArrival().TimeoutAfter(TimeSpan.FromSeconds(40));
                }
                catch (TimeoutException)
                {
                    Timeout = true;
                }
                if ((Notifier.CurrentInterface == null) && (!AutoEmergencyReset || Timeout))
                {
                    AutoEmergencyReset = false;

                    if (Timeout)
                    {
                        LogFile.Log("The phone is not responding", LogType.ConsoleOnly);
                        LogFile.Log("It might be in emergency mode, while you have no matching driver installed", LogType.ConsoleOnly);
                    }
                    LogFile.Log("To continue the unlock-sequence, the phone needs to be rebooted", LogType.ConsoleOnly);
                    LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                    LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                    LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                    LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                    SetWorkingStatus("You need to manually reset your phone now!", (Timeout ? "The phone is not responding. It might be in emergency mode, while you have no matching driver installed. " : "") + "To continue the unlock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. The unlock-sequence will resume automatically.", null, false, WPinternalsStatus.WaitingForManualReset);

                    await Notifier.WaitForRemoval();

                    SetWorkingStatus("Rebooting phone...");
                }
                if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                {
                    bool FailedToStartProgrammer = false;
                    if (ProgrammerPath != null)
                    {
                        QualcommSahara Sahara = new((QualcommSerial)Notifier.CurrentModel);
                        QualcommFirehose Firehose = new((QualcommSerial)Notifier.CurrentModel);
                        try
                        {
                            await Sahara.LoadProgrammer(ProgrammerPath);
                            await Firehose.Reset();
                            await Notifier.WaitForArrival();
                        }
                        catch (BadConnectionException)
                        {
                            FailedToStartProgrammer = true;
                        }
                    }

                    if (ProgrammerPath == null || FailedToStartProgrammer)
                    {
                        ((QualcommSerial)Notifier.CurrentModel).Close(); // Prevent "Resource in use";

                        Timeout = false;
                        if (AutoEmergencyReset)
                        {
                            try
                            {
                                await Notifier.WaitForArrival().TimeoutAfter(TimeSpan.FromSeconds(40));
                            }
                            catch (TimeoutException)
                            {
                                Timeout = true;
                            }
                            catch (Exception ex)
                            {
                                LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                                LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                                FailedToStartProgrammer = true;
                            }
                        }
                        if (!AutoEmergencyReset || Timeout)
                        {
                            AutoEmergencyReset = false;

                            if (!FailedToStartProgrammer)
                            {
                                LogFile.Log("The phone is in emergency mode and you didn't provide an emergency programmer", LogType.ConsoleOnly);
                                LogFile.Log("This phone also doesn't seem to reboot after a timeout, so you got to help a bit", LogType.ConsoleOnly);
                                LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                                LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                                LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                                LogFile.Log("To prevent this, provide an emergency programmer next time you will unlock a bootloader", LogType.ConsoleOnly);
                                LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                                SetWorkingStatus("You need to manually reset your phone now!",
                                    "The phone is in emergency mode and you didn't provide an emergency programmer." +
                                    " This phone also doesn't seem to reboot after a timeout, so you got to help a bit." +
                                    " Keep the phone connected to the PC." +
                                    " The unlock-sequence will resume automatically. To prevent this, provide an emergency programmer next time you will unlock a bootloader.",
                                    null, false, WPinternalsStatus.WaitingForManualReset);
                            }
                            else
                            {
                                LogFile.Log("The phone is in emergency mode and we couldn't start the emergency programmer", LogType.ConsoleOnly);
                                LogFile.Log("This phone also doesn't seem to reboot after a timeout, so you got to help a bit", LogType.ConsoleOnly);
                                LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                                LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                                LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                                LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                                SetWorkingStatus("You need to manually reset your phone now!",
                                    "The phone is in emergency mode and we couldn't start the emergency programmer." +
                                    " This phone also doesn't seem to reboot after a timeout, so you got to help a bit." +
                                    " Keep the phone connected to the PC." +
                                    " The unlock-sequence will resume automatically.",
                                    null, false, WPinternalsStatus.WaitingForManualReset);
                            }

                            await Notifier.WaitForRemoval();

                            SetWorkingStatus("Rebooting phone...");

                            // await Notifier.WaitForArrival(); // Function will exit while phone is rebooting
                        }
                    }
                }

                #endregion
            }
            else
            {
                // If we didn't do a hard exit, we need to do a normal reboot
                ((LumiaFlashAppModel)Notifier.CurrentModel).ResetPhone();
            }

            if (Success)
            {
                ExitSuccess("Flash succeeded!", null);
            }
            else
            {
                throw new WPinternalsException("Custom flash failed");
            }
        }

        internal class FlashingPayload
        {
            public UInt32 ChunkCount;
            public byte[][] ChunkHashes;
            public UInt32[] TargetLocations;
            public UInt32[] StreamIndexes;
            public Int64[] StreamLocations;

            public FlashingPayload(UInt32 ChunkCount, byte[][] ChunkHashes, UInt32[] TargetLocations, UInt32[] StreamIndexes, Int64[] StreamLocations)
            {
                this.ChunkCount = ChunkCount;
                this.ChunkHashes = ChunkHashes;
                this.TargetLocations = TargetLocations;
                this.StreamIndexes = StreamIndexes;
                this.StreamLocations = StreamLocations;
            }

            public UInt32 GetSecurityHeaderSize()
            {
                return 0x20 * (UInt32)ChunkHashes.Length;
            }

            public UInt32 GetStoreHeaderSize()
            {
                return 0x08 * ((UInt32)TargetLocations.Length + 1);
            }
        }

        //
        // Function to fall back into the legacy implementation of custom flash, to test the modifications done in the custom flash function
        // in LumiaV2UnlockBootViewModel
        //
        internal static FlashingPayload[] GetNonOptimizedPayloads(List<FlashPart> flashParts, Int32 chunkSize, UInt32 MaximumChunkCount, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null)
        {
            long TotalProcess1 = 0;
            for (Int32 j = 0; j < flashParts.Count; j++)
            {
                FlashPart flashPart = flashParts[j];
                TotalProcess1 += flashPart.Stream.Length / chunkSize;
            }

            ulong CurrentProcess1 = 0;
            SetWorkingStatus("Hashing resources...", "Initializing flash...", (UInt64)TotalProcess1, Status: WPinternalsStatus.Initializing);

            var crypto = System.Security.Cryptography.SHA256.Create();
            List<FlashingPayload> flashingPayloads = new();
            if (flashParts == null)
            {
                return [.. flashingPayloads];
            }

            for (UInt32 j = 0; j < flashParts.Count; j++)
            {
                FlashPart flashPart = flashParts[(Int32)j];
                flashPart.Stream.Seek(0, SeekOrigin.Begin);
                var totalChunkCount = flashPart.Stream.Length / chunkSize;
                for (UInt32 i = 0; i < totalChunkCount; i++)
                {
                    UpdateWorkingStatus("Hashing resources...", "Initializing flash...", CurrentProcess1, WPinternalsStatus.Initializing);
                    byte[] buffer = new byte[chunkSize];
                    Int64 position = flashPart.Stream.Position;
                    flashPart.Stream.Read(buffer, 0, chunkSize);
                    flashingPayloads.Add(new FlashingPayload(1, [crypto.ComputeHash(buffer)], [(flashPart.StartSector * 0x200 / (UInt32)chunkSize) + i], [j], [position]));
                    CurrentProcess1++;
                }
            }

            return [.. flashingPayloads];
        }

        //
        // This function finds in an optimized way the number of duplicate chunks in a given stream, and returns
        // a list of elements, defining a chunk occurence in said stream and the chunk precomputed SHA256 hash.
        //
        internal static FlashingPayload[] GetOptimizedPayloads(List<FlashPart> flashParts, Int32 chunkSize, UInt32 MaximumChunkCount, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null)
        {
            List<FlashingPayload> flashingPayloads = new();
            if (flashParts == null)
            {
                return [.. flashingPayloads];
            }

            long TotalProcess1 = 0;
            for (Int32 j = 0; j < flashParts.Count; j++)
            {
                FlashPart flashPart = flashParts[j];
                TotalProcess1 += flashPart.Stream.Length / chunkSize;
            }

            ulong CurrentProcess1 = 0;
            SetWorkingStatus("Hashing resources...", "Initializing flash...", (UInt64)TotalProcess1, Status: WPinternalsStatus.Initializing);

            using (System.Security.Cryptography.SHA256 crypto = System.Security.Cryptography.SHA256.Create())
            {
                for (UInt32 j = 0; j < flashParts.Count; j++)
                {
                    FlashPart flashPart = flashParts[(Int32)j];
                    flashPart.Stream.Seek(0, SeekOrigin.Begin);
                    var totalChunkCount = flashPart.Stream.Length / chunkSize;
                    for (UInt32 i = 0; i < totalChunkCount; i++)
                    {
                        UpdateWorkingStatus("Hashing resources...", "Initializing flash...", CurrentProcess1, WPinternalsStatus.Initializing);
                        byte[] buffer = new byte[chunkSize];
                        Int64 position = flashPart.Stream.Position;
                        flashPart.Stream.Read(buffer, 0, chunkSize);
                        var hash = crypto.ComputeHash(buffer);

                        if (flashingPayloads.Any(x => ByteOperations.Compare(x.ChunkHashes[0], hash)))
                        {
                            var payloadIndex = flashingPayloads.FindIndex(x => ByteOperations.Compare(x.ChunkHashes[0], hash));
                            var locationList = flashingPayloads[payloadIndex].TargetLocations.ToList();
                            locationList.Add((flashPart.StartSector * 0x200 / (UInt32)chunkSize) + i);
                            flashingPayloads[payloadIndex].TargetLocations = [.. locationList];
                        }
                        else
                        {
                            flashingPayloads.Add(new FlashingPayload(1, [hash], [(flashPart.StartSector * 0x200 / (UInt32)chunkSize) + i], [j], [position]));
                        }

                        CurrentProcess1++;
                    }
                }
            }

            return [.. flashingPayloads];
        }

        internal static string GetProgrammerPath(byte[] RKH, string Type)
        {
            IEnumerable<EmergencyFileEntry> RKHEntries = App.Config.EmergencyRepository.Where(e => StructuralComparisons.StructuralEqualityComparer.Equals(e.RKH, RKH) && e.ProgrammerExists());
            if (RKHEntries.Any())
            {
                if (RKHEntries.Count() == 1)
                {
                    return RKHEntries.First().ProgrammerPath;
                }
                else
                {
                    EmergencyFileEntry RKHEntry = RKHEntries.FirstOrDefault(e => string.Equals(e.Type, Type, StringComparison.CurrentCulture));
                    if (RKHEntry != null)
                    {
                        return RKHEntry.ProgrammerPath;
                    }
                    else
                    {
                        return RKHEntries.First().ProgrammerPath; // Cannot be sure this is the right one!!
                    }
                }
            }
            else
            {
                EmergencyFileEntry TypeEntry = App.Config.EmergencyRepository.Find(e => string.Equals(e.Type, Type, StringComparison.CurrentCulture) && e.ProgrammerExists());
                if (TypeEntry != null)
                {
                    return TypeEntry.ProgrammerPath;
                }
                else
                {
                    return null;
                }
            }
        }

        // Assumes phone with Flash protocol v2
        // Assumes phone is in flash mode
        internal async static Task LumiaV2FlashArchive(PhoneNotifierViewModel Notifier, string ArchivePath, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            LogFile.BeginAction("FlashCustomROM");

            LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)Notifier.CurrentModel;

            // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
            // We need the fist sector if we want to write back the GPT.
            byte[] GPTChunk = FlashModel.GetGptChunk(0x20000);
            GPT GPT = new(GPTChunk);

            Partition Target;
            FlashPart Part;
            List<FlashPart> Parts = new();
            ulong MainOSOldSectorCount = 0;
            ulong MainOSNewSectorCount = 0;
            ulong DataOldSectorCount = 0;
            ulong DataNewSectorCount = 0;
            ulong FirstMainOSSector = 0;
            int PartitionCount = 0;
            ulong TotalSizeSectors = 0;
            bool IsUnlocked = false;
            bool GPTChanged = false;

            bool ClearFlashingStatus = true;

            if (SetWorkingStatus == null)
            {
                SetWorkingStatus = (m, s, v, a, st) => { };
            }

            if (UpdateWorkingStatus == null)
            {
                UpdateWorkingStatus = (m, s, v, st) => { };
            }

            if (ExitSuccess == null)
            {
                ExitSuccess = (m, s) => { };
            }

            if (ExitSuccess == null)
            {
                ExitSuccess = (m, s) => { };
            }

            SetWorkingStatus("Initializing flash...", null, null, Status: WPinternalsStatus.Initializing);

            try
            {
                using FileStream FileStream = new(ArchivePath, FileMode.Open);
                using ZipArchive Archive = new(FileStream, ZipArchiveMode.Read);
                // Determine if there is a partition layout present
                ZipArchiveEntry PartitionEntry = Archive.GetEntry("Partitions.xml");
                if (PartitionEntry == null)
                {
                    GPT.MergePartitions(null, true, Archive);
                    GPTChanged |= GPT.HasChanged;
                }
                else
                {
                    using Stream ZipStream = PartitionEntry.Open();
                    using StreamReader ZipReader = new(ZipStream);
                    string PartitionXml = ZipReader.ReadToEnd();
                    GPT.MergePartitions(PartitionXml, true, Archive);
                    GPTChanged |= GPT.HasChanged;
                }

                // First determine if we need a new GPT!
                foreach (ZipArchiveEntry Entry in Archive.Entries)
                {
                    if (!Entry.FullName.Contains("/")) // No subfolders
                    {
                        string PartitionName = Entry.Name;
                        int Pos = PartitionName.IndexOf('.');
                        if (Pos >= 0)
                        {
                            PartitionName = PartitionName.Substring(0, Pos);
                        }

                        Partition Partition = GPT.Partitions.Find(p => string.Equals(p.Name, PartitionName, StringComparison.CurrentCultureIgnoreCase));
                        if (Partition != null)
                        {
                            using DecompressedStream DecompressedStream = new(Entry.Open());
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
                                LogFile.Log("Flash failed! Size of partition '" + PartitionName + "' is too big.", LogType.FileAndConsole);
                                ExitFailure("Flash failed!", "Size of partition '" + PartitionName + "' is too big.");
                                return;
                            }
                            else if (string.Equals(PartitionName, "EFIESP", StringComparison.CurrentCultureIgnoreCase))
                            {
                                ulong EfiespLength = StreamLengthInSectors * 0x200;
                                byte[] EfiespBinary = new byte[EfiespLength];
                                DecompressedStream.Read(EfiespBinary, 0, (int)EfiespLength);
                                IsUnlocked = (ByteOperations.ReadUInt32(EfiespBinary, 0x20) == (EfiespBinary.Length / 0x200 / 2)) || ByteOperations.ReadAsciiString(EfiespBinary, (UInt32)(EfiespBinary.Length / 2) + 3, 8) == "MSDOS5.0";
                                if (IsUnlocked)
                                {
                                    Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                                    if (IsUnlockedFlag == null)
                                    {
                                        IsUnlockedFlag = new Partition
                                        {
                                            Name = "IS_UNLOCKED",
                                            Attributes = 0,
                                            PartitionGuid = Guid.NewGuid(),
                                            PartitionTypeGuid = Guid.NewGuid(),
                                            FirstSector = 0x40,
                                            LastSector = 0x40
                                        };
                                        GPT.Partitions.Add(IsUnlockedFlag);
                                        GPTChanged = true;
                                        ClearFlashingStatus = false;
                                    }
                                }
                                else
                                {
                                    Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                                    if (IsUnlockedFlag != null)
                                    {
                                        GPT.Partitions.Remove(IsUnlockedFlag);
                                        GPTChanged = true;
                                        ClearFlashingStatus = false;
                                    }
                                }
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
                            if ((DataPartition.FirstSector % 0x100) > 0)
                            {
                                DataPartition.FirstSector = (DataPartition.FirstSector + 0x100) / 0x100 * 0x100;
                            }

                            DataPartition.LastSector = DataPartition.FirstSector + DataNewSectorCount - 1;

                            GPTChanged = true;
                        }
                        else
                        {
                            LogFile.Log("Flash failed! Sizes of partitions 'MainOS' and 'Data' together are too big.", LogType.FileAndConsole);
                            ExitFailure("Flash failed!", "Sizes of partitions 'MainOS' and 'Data' together are too big.");
                            return;
                        }
                    }
                }
                else if ((MainOSNewSectorCount > 0) && (MainOSNewSectorCount > MainOSOldSectorCount))
                {
                    LogFile.Log("Flash failed! Size of partition 'MainOS' is too big.", LogType.FileAndConsole);
                    ExitFailure("Flash failed!", "Size of partition 'MainOS' is too big.");
                    return;
                }
                else if ((DataNewSectorCount > 0) && (DataNewSectorCount > DataOldSectorCount))
                {
                    LogFile.Log("Flash failed! Size of partition 'Data' is too big.", LogType.FileAndConsole);
                    ExitFailure("Flash failed!", "Size of partition 'Data' is too big.");
                    return;
                }

                if (!ClearFlashingStatus)
                {
                    if (!IsUnlocked)
                    {
                        // Undo secure boot exploit
                        Partition NvBackupPartition = GPT.GetPartition("BACKUP_BS_NV");
                        if (NvBackupPartition != null)
                        {
                            // This must be a left over of a half unlocked bootloader
                            Partition NvPartition = GPT.GetPartition("UEFI_BS_NV");
                            NvBackupPartition.Name = "UEFI_BS_NV";
                            NvBackupPartition.PartitionGuid = NvPartition.PartitionGuid;
                            NvBackupPartition.PartitionTypeGuid = NvPartition.PartitionTypeGuid;
                            GPT.Partitions.Remove(NvPartition);
                            GPTChanged = true;
                        }

                        LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo(false);

                        // We should only clear NV if there was no backup NV to be restored and the current NV contains the SB unlock.
                        if ((NvBackupPartition == null) && !Info.UefiSecureBootEnabled)
                        {
                            // ClearNV
                            Part = new FlashPart();
                            Partition Target2 = GPT.GetPartition("UEFI_BS_NV");
                            Part.StartSector = (UInt32)Target2.FirstSector;
                            Part.Stream = new MemoryStream(new byte[0x40000]);
                            Parts.Add(Part);
                        }
                    }
                    else
                    {
                        // Now add NV partition
                        Partition BACKUP_BS_NV = GPT.GetPartition("BACKUP_BS_NV");
                        Partition UEFI_BS_NV;
                        if (BACKUP_BS_NV == null)
                        {
                            BACKUP_BS_NV = GPT.GetPartition("UEFI_BS_NV");
                            Guid OriginalPartitionTypeGuid = BACKUP_BS_NV.PartitionTypeGuid;
                            Guid OriginalPartitionGuid = BACKUP_BS_NV.PartitionGuid;
                            BACKUP_BS_NV.Name = "BACKUP_BS_NV";
                            BACKUP_BS_NV.PartitionGuid = Guid.NewGuid();
                            BACKUP_BS_NV.PartitionTypeGuid = Guid.NewGuid();
                            UEFI_BS_NV = new Partition
                            {
                                Name = "UEFI_BS_NV",
                                Attributes = BACKUP_BS_NV.Attributes,
                                PartitionGuid = OriginalPartitionGuid,
                                PartitionTypeGuid = OriginalPartitionTypeGuid,
                                FirstSector = BACKUP_BS_NV.LastSector + 1
                            };
                            UEFI_BS_NV.LastSector = UEFI_BS_NV.FirstSector + BACKUP_BS_NV.LastSector - BACKUP_BS_NV.FirstSector;
                            GPT.Partitions.Add(UEFI_BS_NV);
                            GPTChanged = true;
                        }

                        Part = new FlashPart();
                        Target = GPT.GetPartition("UEFI_BS_NV");
                        Part.StartSector = (UInt32)Target.FirstSector; // GPT is prepared for 64-bit sector-offset, but flash app isn't.
                        Part.Stream = new SeekableStream(() =>
                        {
                            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                                    // Magic!
                                    // The SB resource is a compressed version of a raw NV-variable-partition.
                                    // In this partition the SecureBoot variable is disabled.
                                    // It overwrites the variable in a different NV-partition than where this variable is stored usually.
                                    // This normally leads to endless-loops when the NV-variables are enumerated.
                                    // But the partition contains an extra hack to break out the endless loops.
                                    var stream = assembly.GetManifestResourceStream("WPinternals.SB");

                            return new DecompressedStream(stream);
                        });
                        Parts.Add(Part);
                    }
                }

                if (GPTChanged)
                {
                    GPT.Rebuild();
                    Part = new FlashPart
                    {
                        StartSector = 0,
                        Stream = new MemoryStream(GPTChunk)
                    };
                    Parts.Add(Part);
                }

                // And then add the partitions from the archive
                if (PartitionCount > 0)
                {
                    foreach (ZipArchiveEntry Entry in Archive.Entries)
                    {
                        if (!Entry.FullName.Contains("/")) // No subfolders
                        {
                            // "MainOS.bin.gz" => "MainOS"
                            string PartitionName = Entry.Name;
                            int Pos = PartitionName.IndexOf('.');
                            if (Pos >= 0)
                            {
                                PartitionName = PartitionName.Substring(0, Pos);
                            }

                            Target = GPT.Partitions.Find(p => string.Equals(p.Name, PartitionName, StringComparison.CurrentCultureIgnoreCase));
                            if (Target != null)
                            {
                                Part = new FlashPart
                                {
                                    StartSector = (UInt32)Target.FirstSector,
                                    Stream = new SeekableStream(() => new DecompressedStream(Entry.Open()), Entry.Length),
                                    ProgressText = "Flashing partition " + Target.Name
                                };
                                Parts.Add(Part);
                                LogFile.Log("Partition name=" + PartitionName + ", startsector=0x" + Target.FirstSector.ToString("X8") + ", sectorcount = 0x" + (Entry.Length / 0x200).ToString("X8"), LogType.FileOnly);
                            }
                        }
                    }

                    Parts = [.. Parts.OrderBy(p => p.StartSector)];
                    int Count = 1;
                    Parts.Where(p => p.ProgressText?.StartsWith("Flashing partition ") == true).ToList().ForEach((p) =>
                    {
                        p.ProgressText += " (" + Count.ToString() + "/" + PartitionCount.ToString() + ")";
                        Count++;
                    });

                    // Do actual flashing!
                    await LumiaV2CustomFlash(Notifier, null, false, false, Parts, true, ClearFlashingStatus, false, true, false, SetWorkingStatus, UpdateWorkingStatus, ExitSuccess, ExitFailure);
                }
                else
                {
                    LogFile.Log("Flash failed! No valid partitions found in the archive.", LogType.FileAndConsole);
                    ExitFailure("Flash failed!", "No valid partitions found in the archive");
                    return;
                }
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                ExitFailure(Ex.Message, Ex is WPinternalsException ? ((WPinternalsException)Ex).SubMessage : null);
            }

            LogFile.EndAction("FlashCustomROM");
        }

        internal async static Task LumiaV2FixBoot(PhoneNotifierViewModel Notifier, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            LogFile.BeginAction("FixBoot");

            LogFile.Log("Command: Fix boot after unlocking bootloader", LogType.FileAndConsole);
            NokiaFlashModel FlashModel = (NokiaFlashModel)Notifier.CurrentModel;

            if (SetWorkingStatus == null)
            {
                SetWorkingStatus = (m, s, v, a, st) => { };
            }

            if (UpdateWorkingStatus == null)
            {
                UpdateWorkingStatus = (m, s, v, st) => { };
            }

            if (ExitSuccess == null)
            {
                ExitSuccess = (m, s) => { };
            }

            if (ExitFailure == null)
            {
                ExitFailure = (m, s) => { };
            }

            try
            {
                await SwitchModeViewModel.SwitchToWithProgress(Notifier, PhoneInterfaces.Lumia_MassStorage, (msg, sub) => SetWorkingStatus(msg, sub, null, Status: WPinternalsStatus.SwitchingMode));
                SetWorkingStatus("Applying patches...", null, null, Status: WPinternalsStatus.Patching);
                App.PatchEngine.TargetPath = ((MassStorage)Notifier.CurrentModel).Drive + "\\";
                bool PatchResult = App.PatchEngine.Patch("SecureBootHack-MainOS");
                if (!PatchResult)
                {
                    throw new WPinternalsException("Patch failed", "An error occured while patching Operating System files on the MainOS partition of your phone. Make sure your phone runs a supported Operating System version.");
                }

                LogFile.Log("Fixed bootloader", LogType.FileAndConsole);
                LogFile.Log("The phone is left in Mass Storage mode", LogType.FileAndConsole);
                LogFile.Log("Press and hold the power-button of the phone for at least 10 seconds to reset the phone", LogType.FileAndConsole);
                ExitSuccess("Fixed bootloader!", "The phone is left in Mass Storage mode. Press and hold the power-button of the phone for at least 10 seconds to reset the phone.");
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                ExitFailure(Ex.Message, Ex is WPinternalsException ? ((WPinternalsException)Ex).SubMessage : null);
            }

            LogFile.EndAction("FixBoot");
        }

        // Assumes phone with Flash protocol v2
        // Assumes phone is in flash mode
        internal async static Task LumiaV2FlashPartitions(PhoneNotifierViewModel Notifier, string EFIESPPath, string MainOSPath, string DataPath, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)Notifier.CurrentModel;

            // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
            // We need the fist sector if we want to write back the GPT.
            byte[] GPTChunk = FlashModel.GetGptChunk(0x20000);
            GPT GPT = new(GPTChunk);

            Partition Target;
            FlashPart Part;
            List<FlashPart> Parts = new();
            ulong MainOSOldSectorCount = 0;
            ulong MainOSNewSectorCount = 0;
            ulong DataOldSectorCount = 0;
            ulong DataNewSectorCount = 0;
            ulong FirstMainOSSector = 0;
            int PartitionCount = 0;
            ulong LengthInSectors;
            FileInfo FileInfo;
            Partition Partition;
            bool IsUnlocked = false;
            bool GPTChanged = false;
            bool ClearFlashingStatus = true;

            if (SetWorkingStatus == null)
            {
                SetWorkingStatus = (m, s, v, a, st) => { };
            }

            if (UpdateWorkingStatus == null)
            {
                UpdateWorkingStatus = (m, s, v, st) => { };
            }

            if (ExitSuccess == null)
            {
                ExitSuccess = (m, s) => { };
            }

            if (ExitFailure == null)
            {
                ExitFailure = (m, s) => { };
            }

            SetWorkingStatus("Initializing flash...", null, null, Status: WPinternalsStatus.Initializing);

            try
            {
                if (EFIESPPath != null)
                {
                    FileInfo = new FileInfo(EFIESPPath);
                    LengthInSectors = (ulong)FileInfo.Length / 0x200;
                    Partition = GPT.GetPartition("EFIESP");
                    if (Partition.SizeInSectors < LengthInSectors)
                    {
                        LogFile.Log("Flash failed! Size of partition 'EFIESP' is too big.", LogType.FileAndConsole);
                        ExitFailure("Flash failed!", "Size of partition 'EFIESP' is too big.");
                        return;
                    }
                    PartitionCount++;

                    byte[] EfiespBinary = File.ReadAllBytes(EFIESPPath);
                    IsUnlocked = (ByteOperations.ReadUInt32(EfiespBinary, 0x20) == (EfiespBinary.Length / 0x200 / 2)) && ByteOperations.ReadAsciiString(EfiespBinary, (UInt32)(EfiespBinary.Length / 2) + 3, 8) == "MSDOS5.0";
                    if (IsUnlocked)
                    {
                        Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                        if (IsUnlockedFlag == null)
                        {
                            IsUnlockedFlag = new Partition
                            {
                                Name = "IS_UNLOCKED",
                                Attributes = 0,
                                PartitionGuid = Guid.NewGuid(),
                                PartitionTypeGuid = Guid.NewGuid(),
                                FirstSector = 0x40,
                                LastSector = 0x40
                            };
                            GPT.Partitions.Add(IsUnlockedFlag);
                            GPTChanged = true;
                            ClearFlashingStatus = false;
                        }
                    }
                    else
                    {
                        Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                        if (IsUnlockedFlag != null)
                        {
                            GPT.Partitions.Remove(IsUnlockedFlag);
                            GPTChanged = true;
                            ClearFlashingStatus = false;
                        }
                    }
                }

                if (MainOSPath != null)
                {
                    FileInfo = new FileInfo(MainOSPath);
                    LengthInSectors = (ulong)FileInfo.Length / 0x200;
                    Partition = GPT.GetPartition("MainOS");
                    MainOSOldSectorCount = Partition.SizeInSectors;
                    MainOSNewSectorCount = LengthInSectors;
                    FirstMainOSSector = Partition.FirstSector;
                    PartitionCount++;
                }

                if (DataPath != null)
                {
                    FileInfo = new FileInfo(DataPath);
                    LengthInSectors = (ulong)FileInfo.Length / 0x200;
                    Partition = GPT.GetPartition("Data");
                    DataOldSectorCount = Partition.SizeInSectors;
                    DataNewSectorCount = LengthInSectors;
                    PartitionCount++;
                }

                if (PartitionCount > 0)
                {
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
                                if ((DataPartition.FirstSector % 0x100) > 0)
                                {
                                    DataPartition.FirstSector = (DataPartition.FirstSector + 0x100) / 0x100 * 0x100;
                                }

                                DataPartition.LastSector = DataPartition.FirstSector + DataNewSectorCount - 1;

                                GPTChanged = true;
                            }
                            else
                            {
                                LogFile.Log("Flash failed! Sizes of partitions 'MainOS' and 'Data' together are too big.", LogType.FileAndConsole);
                                ExitFailure("Flash failed!", "Sizes of partitions 'MainOS' and 'Data' together are too big.");
                                return;
                            }
                        }
                    }
                    else if ((MainOSNewSectorCount > 0) && (MainOSNewSectorCount > MainOSOldSectorCount))
                    {
                        LogFile.Log("Flash failed! Size of partition 'MainOS' is too big.", LogType.FileAndConsole);
                        ExitFailure("Flash failed!", "Size of partition 'MainOS' is too big.");
                        return;
                    }
                    else if ((DataNewSectorCount > 0) && (DataNewSectorCount > DataOldSectorCount))
                    {
                        LogFile.Log("Flash failed! Size of partition 'Data' is too big.", LogType.FileAndConsole);
                        ExitFailure("Flash failed!", "Size of partition 'Data' is too big.");
                        return;
                    }

                    if (!ClearFlashingStatus)
                    {
                        if (!IsUnlocked)
                        {
                            // Undo secure boot exploit
                            Partition NvBackupPartition = GPT.GetPartition("BACKUP_BS_NV");
                            if (NvBackupPartition != null)
                            {
                                // This must be a left over of a half unlocked bootloader
                                Partition NvPartition = GPT.GetPartition("UEFI_BS_NV");
                                NvBackupPartition.Name = "UEFI_BS_NV";
                                NvBackupPartition.PartitionGuid = NvPartition.PartitionGuid;
                                NvBackupPartition.PartitionTypeGuid = NvPartition.PartitionTypeGuid;
                                GPT.Partitions.Remove(NvPartition);
                                GPTChanged = true;
                            }

                            LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo(false);

                            // We should only clear NV if there was no backup NV to be restored and the current NV contains the SB unlock.
                            if ((NvBackupPartition == null) && !Info.UefiSecureBootEnabled)
                            {
                                // ClearNV
                                Part = new FlashPart();
                                Partition Target2 = GPT.GetPartition("UEFI_BS_NV");
                                Part.StartSector = (UInt32)Target2.FirstSector;
                                Part.Stream = new MemoryStream(new byte[0x40000]);
                                Parts.Add(Part);
                            }
                        }
                        else
                        {
                            // Now add NV partition
                            Partition BACKUP_BS_NV = GPT.GetPartition("BACKUP_BS_NV");
                            Partition UEFI_BS_NV;
                            if (BACKUP_BS_NV == null)
                            {
                                BACKUP_BS_NV = GPT.GetPartition("UEFI_BS_NV");
                                Guid OriginalPartitionTypeGuid = BACKUP_BS_NV.PartitionTypeGuid;
                                Guid OriginalPartitionGuid = BACKUP_BS_NV.PartitionGuid;
                                BACKUP_BS_NV.Name = "BACKUP_BS_NV";
                                BACKUP_BS_NV.PartitionGuid = Guid.NewGuid();
                                BACKUP_BS_NV.PartitionTypeGuid = Guid.NewGuid();
                                UEFI_BS_NV = new Partition
                                {
                                    Name = "UEFI_BS_NV",
                                    Attributes = BACKUP_BS_NV.Attributes,
                                    PartitionGuid = OriginalPartitionGuid,
                                    PartitionTypeGuid = OriginalPartitionTypeGuid,
                                    FirstSector = BACKUP_BS_NV.LastSector + 1
                                };
                                UEFI_BS_NV.LastSector = UEFI_BS_NV.FirstSector + BACKUP_BS_NV.LastSector - BACKUP_BS_NV.FirstSector;
                                GPT.Partitions.Add(UEFI_BS_NV);
                                GPTChanged = true;
                            }

                            Part = new FlashPart();
                            Target = GPT.GetPartition("UEFI_BS_NV");
                            Part.StartSector = (UInt32)Target.FirstSector; // GPT is prepared for 64-bit sector-offset, but flash app isn't.
                            Part.Stream = new SeekableStream(() =>
                            {
                                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                                // Magic!
                                // The SB resource is a compressed version of a raw NV-variable-partition.
                                // In this partition the SecureBoot variable is disabled.
                                // It overwrites the variable in a different NV-partition than where this variable is stored usually.
                                // This normally leads to endless-loops when the NV-variables are enumerated.
                                // But the partition contains an extra hack to break out the endless loops.
                                var stream = assembly.GetManifestResourceStream("WPinternals.SB");

                                return new DecompressedStream(stream);
                            });
                            Parts.Add(Part);
                        }
                    }

                    if (GPTChanged)
                    {
                        GPT.Rebuild();
                        Part = new FlashPart
                        {
                            StartSector = 0,
                            Stream = new MemoryStream(GPTChunk)
                        };
                        Parts.Add(Part);
                    }

                    int Count = 0;

                    Target = GPT.Partitions.Find(p => string.Equals(p.Name, "EFIESP", StringComparison.CurrentCultureIgnoreCase));
                    if ((EFIESPPath != null) && (Target != null))
                    {
                        Count++;
                        Part = new FlashPart
                        {
                            StartSector = (UInt32)Target.FirstSector,
                            Stream = new FileStream(EFIESPPath, FileMode.Open),
                            ProgressText = "Flashing partition EFIESP (" + Count.ToString() + " / " + PartitionCount.ToString() + ")"
                        };
                        Parts.Add(Part);
                        LogFile.Log("Partition name=EFIESP, startsector=0x" + Target.FirstSector.ToString("X8") + ", sectorcount = 0x" + (Part.Stream.Length / 0x200).ToString("X8"), LogType.FileOnly);
                    }

                    Target = GPT.Partitions.Find(p => string.Equals(p.Name, "MainOS", StringComparison.CurrentCultureIgnoreCase));
                    if ((MainOSPath != null) && (Target != null))
                    {
                        Count++;
                        Part = new FlashPart
                        {
                            StartSector = (UInt32)Target.FirstSector,
                            Stream = new FileStream(MainOSPath, FileMode.Open),
                            ProgressText = "Flashing partition MainOS (" + Count.ToString() + " / " + PartitionCount.ToString() + ")"
                        };
                        Parts.Add(Part);
                        LogFile.Log("Partition name=MainOS, startsector=0x" + Target.FirstSector.ToString("X8") + ", sectorcount = 0x" + (Part.Stream.Length / 0x200).ToString("X8"), LogType.FileOnly);
                    }

                    Target = GPT.Partitions.Find(p => string.Equals(p.Name, "Data", StringComparison.CurrentCultureIgnoreCase));
                    if ((DataPath != null) && (Target != null))
                    {
                        Count++;
                        Part = new FlashPart
                        {
                            StartSector = (UInt32)Target.FirstSector,
                            Stream = new FileStream(DataPath, FileMode.Open),
                            ProgressText = "Flashing partition Data (" + Count.ToString() + " / " + PartitionCount.ToString() + ")"
                        };
                        Parts.Add(Part);
                        LogFile.Log("Partition name=Data, startsector=0x" + Target.FirstSector.ToString("X8") + ", sectorcount = 0x" + (Part.Stream.Length / 0x200).ToString("X8"), LogType.FileOnly);
                    }

                    // Do actual flashing!
                    await LumiaV2CustomFlash(Notifier, null, false, false, Parts, true, ClearFlashingStatus, false, true, false, SetWorkingStatus, UpdateWorkingStatus, ExitSuccess, ExitFailure);
                }
                else
                {
                    LogFile.Log("Flash failed! No valid partitions found in the archive.", LogType.FileAndConsole);
                    ExitFailure("Flash failed!", "No valid partitions found in the archive");
                    return;
                }
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                ExitFailure(Ex.Message, Ex is WPinternalsException ? ((WPinternalsException)Ex).SubMessage : null);
                return;
            }
        }
    }

    internal static class UefiMemorySim
    {
        internal const UInt32 PageSize = 0x1000;

        private static UInt32 CurrentBufferSize = 0;
        public static byte[] Buffer;
        private static readonly List<Allocation> Allocations = new();
        private static readonly List<FreeMemRange> FreeMemRanges = new();

        public static UInt32 RoundUpToPages(UInt32 Size)
        {
            UInt32 Result = Size + 0x18;
            if ((Size % PageSize) != 0)
            {
                Size = ((Size / PageSize) + 1) * PageSize;
            }

            return Result;
        }

        public static void Reset()
        {
            CurrentBufferSize = 0;
            Buffer = null;
            Allocations.Clear();
            FreeMemRanges.Clear();
        }

        private static void ExtendBuffer(UInt32 Size)
        {
            byte[] NewBuffer = new byte[CurrentBufferSize + Size];
            if (CurrentBufferSize > 0)
            {
                System.Buffer.BlockCopy(Buffer, 0, NewBuffer, (int)Size, (int)CurrentBufferSize);
            }

            foreach (Allocation CurrentAllocation in Allocations)
            {
                CurrentAllocation.TotalStart += Size;
                CurrentAllocation.TotalEnd += Size;
                CurrentAllocation.HeadStart += Size;
                CurrentAllocation.HeadEnd += Size;
                CurrentAllocation.ContentStart += Size;
                CurrentAllocation.ContentEnd += Size;
                CurrentAllocation.TailStart += Size;
                CurrentAllocation.TailEnd += Size;
            }
            foreach (FreeMemRange CurrentRange in FreeMemRanges)
            {
                CurrentRange.Start += Size;
                CurrentRange.End += Size;
            }
            CurrentBufferSize += Size;
            Buffer = NewBuffer;
        }

        internal static Allocation AllocatePages(UInt32 Size)
        {
            Allocation NewAllocation = null;

            UInt32 TotalSize = Size;

            if ((TotalSize % PageSize) != 0)
            {
                throw new NotSupportedException("Wrong allocation size");
            }
            else
            {
                for (int i = FreeMemRanges.Count - 1; i >= 0; i--)
                {
                    if (FreeMemRanges[i].Size >= TotalSize)
                    {
                        NewAllocation = new Allocation
                        {
                            TotalStart = FreeMemRanges[i].End - TotalSize + 1
                        };

                        if (FreeMemRanges[i].Size == TotalSize)
                        {
                            FreeMemRanges.RemoveAt(i);
                        }
                        else
                        {
                            FreeMemRanges[i].End -= TotalSize;
                        }

                        break;
                    }
                }

                if (NewAllocation == null)
                {
                    uint FreeBuffer = Allocations.Count > 0 ? Allocations[0].TotalStart : CurrentBufferSize;
                    if (FreeBuffer < TotalSize)
                    {
                        ExtendBuffer(TotalSize - FreeBuffer);
                    }

                    NewAllocation = new Allocation();

                    if (Allocations.Count > 0)
                    {
                        NewAllocation.TotalStart = Allocations[0].TotalStart - TotalSize;
                    }
                    else
                    {
                        FreeBuffer = CurrentBufferSize - TotalSize;
                    }
                }

                bool Added = false;
                for (int i = 0; i < Allocations.Count; i++)
                {
                    if (NewAllocation.TotalStart < Allocations[i].TotalStart)
                    {
                        Allocations.Insert(i, NewAllocation);
                        Added = true;
                        break;
                    }
                }
                if (!Added)
                {
                    Allocations.Add(NewAllocation);
                }
            }

            return NewAllocation;
        }

        internal static Allocation AllocatePool(UInt32 Size)
        {
            Allocation NewAllocation = null;

            UInt32 TotalSize = Size + 24;

            if (TotalSize < PageSize)
            {
                throw new NotSupportedException("Allocation of small memory area's is not supported by this UEFI memory management simulator");
            }
            else
            {
                if ((TotalSize % PageSize) != 0)
                {
                    TotalSize = ((TotalSize / PageSize) + 1) * PageSize;
                }

                for (int i = FreeMemRanges.Count - 1; i >= 0; i--)
                {
                    if (FreeMemRanges[i].Size >= TotalSize)
                    {
                        NewAllocation = new Allocation
                        {
                            TotalStart = FreeMemRanges[i].End - TotalSize + 1
                        };

                        if (FreeMemRanges[i].Size == TotalSize)
                        {
                            FreeMemRanges.RemoveAt(i);
                        }
                        else
                        {
                            FreeMemRanges[i].End -= TotalSize;
                        }

                        break;
                    }
                }

                if (NewAllocation == null)
                {
                    uint FreeBuffer = Allocations.Count > 0 ? Allocations[0].TotalStart : CurrentBufferSize;
                    if (FreeBuffer < TotalSize)
                    {
                        ExtendBuffer(TotalSize - FreeBuffer);
                    }

                    NewAllocation = new Allocation();

                    if (Allocations.Count > 0)
                    {
                        NewAllocation.TotalStart = Allocations[0].TotalStart - TotalSize;
                    }
                    else
                    {
                        FreeBuffer = CurrentBufferSize - TotalSize;
                    }
                }

                NewAllocation.TotalEnd = NewAllocation.TotalStart + TotalSize - 1;
                NewAllocation.HeadStart = NewAllocation.TotalStart;
                NewAllocation.HeadEnd = NewAllocation.HeadStart + 16 - 1;
                NewAllocation.ContentStart = NewAllocation.HeadEnd + 1;
                NewAllocation.ContentEnd = NewAllocation.ContentStart + Size - 1;
                NewAllocation.TailStart = NewAllocation.ContentEnd + 1;
                NewAllocation.TailEnd = NewAllocation.TailStart + 8 - 1;

                ByteOperations.WriteAsciiString(Buffer, NewAllocation.HeadStart + 0x00, "phd0");

                // Correct value here is: Size + 24
                // Wrong value is: TotalSize
                // Having correct value avoids memory errors and phone can reboot normally, but NV vars might be written (and that will overwrite the NV vars we wrote ourselves).
                // Wrong value will make phone reboot to emergency boot and it makes the phone crash when you want to flash in multiple phases, but it will avoid writing NV vars.
                ByteOperations.WriteUInt32(Buffer, NewAllocation.HeadStart + 0x04, Size + 24);

                ByteOperations.WriteUInt32(Buffer, NewAllocation.HeadStart + 0x08, 0x04); // EfiBootServicesData = 0x04
                ByteOperations.WriteUInt32(Buffer, NewAllocation.HeadStart + 0x0C, 0x00); // Reserved

                ByteOperations.WriteAsciiString(Buffer, NewAllocation.TailStart + 0x00, "ptal");

                // Correct value here is: Size + 24
                // Wrong value is: TotalSize
                // Having correct value avoids memory errors and phone can reboot normally, but NV vars might be written (and that will overwrite the NV vars we wrote ourselves).
                // Wrong value will make phone reboot to emergency boot and it makes the phone crash when you want to flash in multiple phases, but it will avoid writing NV vars.
                ByteOperations.WriteUInt32(Buffer, NewAllocation.TailStart + 0x04, Size + 24);

                bool Added = false;
                for (int i = 0; i < Allocations.Count; i++)
                {
                    if (NewAllocation.TotalStart < Allocations[i].TotalStart)
                    {
                        Allocations.Insert(i, NewAllocation);
                        Added = true;
                        break;
                    }
                }
                if (!Added)
                {
                    Allocations.Add(NewAllocation);
                }
            }

            return NewAllocation;
        }

        internal static void FreePool(Allocation Allocation)
        {
            if (Allocations.Contains(Allocation))
            {
                Allocations.Remove(Allocation);

                if (Allocations.Count == 0)
                {
                    FreeMemRanges.Clear();
                }
                else
                {
                    FreeMemRange NewFreeRange = new();
                    NewFreeRange.Start = Allocation.TotalStart;
                    NewFreeRange.End = Allocation.TotalEnd;

                    bool Added = false;
                    int i;
                    for (i = 0; i < FreeMemRanges.Count; i++)
                    {
                        if (NewFreeRange.Start < FreeMemRanges[i].Start)
                        {
                            FreeMemRanges.Insert(i, NewFreeRange);
                            Added = true;
                            break;
                        }
                    }
                    if (!Added)
                    {
                        FreeMemRanges.Add(NewFreeRange);
                        i = FreeMemRanges.Count;
                    }

                    if ((i > 0) && (FreeMemRanges[i].Start == (FreeMemRanges[i - 1].End + 1)))
                    {
                        FreeMemRanges[i - 1].End = FreeMemRanges[i].End;
                        FreeMemRanges.RemoveAt(i);
                        i--;
                    }

                    if ((i < (FreeMemRanges.Count - 1)) && (FreeMemRanges[i].End == (FreeMemRanges[i - 1].Start - 1)))
                    {
                        FreeMemRanges[i].End = FreeMemRanges[i + 1].End;
                        FreeMemRanges.RemoveAt(i + 1);
                    }

                    if ((Allocations.Count > 0) && (FreeMemRanges[i].Start < Allocations[0].TotalStart))
                    {
                        FreeMemRanges.RemoveAt(i);
                    }
                }
            }
        }
    }

    internal class Allocation
    {
        public UInt32 TotalStart;
        public UInt32 TotalEnd;
        public UInt32 ContentStart;
        public UInt32 ContentEnd;
        public UInt32 HeadStart;
        public UInt32 HeadEnd;
        public UInt32 TailStart;
        public UInt32 TailEnd;

        public UInt32 TotalSize
        {
            get
            {
                return TotalEnd - TotalStart + 1;
            }
        }

        public UInt32 ContentSize
        {
            get
            {
                return ContentEnd - ContentStart + 1;
            }
        }

        public void CopyFromThisAllocation(UInt32 ContentOffset, UInt32 Size, byte[] Destination, UInt32 DestinationOffset)
        {
            Buffer.BlockCopy(UefiMemorySim.Buffer, (int)(ContentStart + ContentOffset), Destination, (int)DestinationOffset, (int)Size);
        }

        public void CopyToThisAllocation(byte[] Source, UInt32 SourceOffset, UInt32 Size, UInt32 ContentOffset)
        {
            Buffer.BlockCopy(Source, (int)SourceOffset, UefiMemorySim.Buffer, (int)(ContentStart + ContentOffset), (int)Size);
        }
    }

    internal class FreeMemRange
    {
        public UInt32 Start;
        public UInt32 End;

        public UInt32 Size
        {
            get
            {
                return End - Start + 1;
            }
        }
    }

    internal class FlashPart
    {
        public string ProgressText;
        public UInt32 StartSector;
        public Stream Stream;
    }
}
