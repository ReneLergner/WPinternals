// Copyright (c) 2018, Rene Lergner - wpinternals.net - @Heathcliff74xda
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
            if (SetWorkingStatus == null) SetWorkingStatus = (m, s, v, a, st) => { };
            if (UpdateWorkingStatus == null) UpdateWorkingStatus = (m, s, v, st) => { };
            if (ExitSuccess == null) ExitSuccess = (m, s) => { };
            if (ExitFailure == null) ExitFailure = (m, s) => { };

            LogFile.BeginAction("FindFlashingProfile");
            try
            {
                LogFile.Log("Find Flashing Profile", LogType.FileAndConsole);

                NokiaFlashModel FlashModel = (NokiaFlashModel)(await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash));

                PhoneInfo Info;
                if (DoResetFirst)
                {
                    // The phone will be reset before flashing, so we have the opportunity to get some more info from the phone
                    Info = FlashModel.ReadPhoneInfo();
                    Info.Log(LogType.ConsoleOnly);

                    string FfuFirmware = null;
                    if (FFUPath != null)
                    {
                        FFU FFU = new FFU(FFUPath);
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

                await LumiaV2CustomFlash(Notifier, FFUPath, false, !Info.SecureFfuEnabled || Info.RdcPresent || Info.Authenticated, null, DoResetFirst, Experimental: Experimental, SetWorkingStatus:
                    (m, s, v, a, st) =>
                    {
                        if (st == WPinternalsStatus.SwitchingMode)
                            SetWorkingStatus(m, s, v, a, st);
                    },
                    UpdateWorkingStatus:
                    (m, s, v, st) =>
                    {
                        if (st == WPinternalsStatus.SwitchingMode)
                            UpdateWorkingStatus(m, s, v, st);
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

        internal static byte[] GetGptChunk(NokiaFlashModel FlashModel, UInt32 Size)
        {
            // This function is also used to generate a dummy chunk to flash for testing.
            // The dummy chunk will contain the GPT, so it can be flashed to the first sectors for testing.
            byte[] GPTChunk = new byte[Size];

            PhoneInfo Info = FlashModel.ReadPhoneInfo(ExtendedInfo: false);
            FlashAppType OriginalAppType = Info.App;
            bool Switch = ((Info.App != FlashAppType.BootManager) && Info.SecureFfuEnabled && !Info.Authenticated && !Info.RdcPresent);
            if (Switch)
                FlashModel.SwitchToBootManagerContext();

            byte[] Request = new byte[0x04];
            const string Header = "NOKT";

            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);

            byte[] Buffer = FlashModel.ExecuteRawMethod(Request);
            if ((Buffer == null) || (Buffer.Length < 0x4408))
                throw new InvalidOperationException("Unable to read GPT!");

            UInt16 Error = (UInt16)((Buffer[6] << 8) + Buffer[7]);
            if (Error > 0)
                throw new NotSupportedException("ReadGPT: Error 0x" + Error.ToString("X4"));

            System.Buffer.BlockCopy(Buffer, 8, GPTChunk, 0, 0x4400);

            if (Switch)
            {
                if (OriginalAppType == FlashAppType.FlashApp)
                    FlashModel.SwitchToFlashAppContext();
                else
                    FlashModel.SwitchToPhoneInfoAppContext();
            }

            return GPTChunk;
        }

        internal static async Task LumiaV2EnableTestSigning(System.Threading.SynchronizationContext UIContext, string FFUPath, bool DoResetFirst = true)
        {
            LogFile.BeginAction("EnableTestSigning");
            try
            {
                LogFile.Log("Command: Enable testsigning", LogType.FileAndConsole);
                PhoneNotifierViewModel Notifier = new PhoneNotifierViewModel();
                UIContext.Send(s => Notifier.Start(), null);
                NokiaFlashModel FlashModel = (NokiaFlashModel)(await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash));

                List<FlashPart> Parts = new List<FlashPart>();
                FlashPart Part;

                // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
                // We need the fist sector if we want to write back the GPT.
                byte[] GPTChunk = GetGptChunk(FlashModel, 0x20000);
                GPT GPT = new GPT(GPTChunk);
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
                    UEFI_BS_NV = new Partition();
                    UEFI_BS_NV.Name = "UEFI_BS_NV";
                    UEFI_BS_NV.Attributes = BACKUP_BS_NV.Attributes;
                    UEFI_BS_NV.PartitionGuid = OriginalPartitionGuid;
                    UEFI_BS_NV.PartitionTypeGuid = OriginalPartitionTypeGuid;
                    UEFI_BS_NV.FirstSector = BACKUP_BS_NV.LastSector + 1;
                    UEFI_BS_NV.LastSector = UEFI_BS_NV.FirstSector + BACKUP_BS_NV.LastSector - BACKUP_BS_NV.FirstSector;
                    GPT.Partitions.Add(UEFI_BS_NV);
                    GPTChanged = true;
                }
                if (GPTChanged)
                {
                    GPT.Rebuild();
                    Part = new FlashPart();
                    Part.StartSector = 0;
                    Part.Stream = new MemoryStream(GPTChunk);
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

                await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(Notifier, FFUPath, false, false, Parts, DoResetFirst, ClearFlashingStatusAtEnd: false);

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

            NokiaFlashModel FlashModel = (NokiaFlashModel)(await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash));
            if (DoResetFirst)
            {
                // The phone will be reset before flashing, so we have the opportunity to get some more info from the phone
                PhoneInfo Info = FlashModel.ReadPhoneInfo();
                Info.Log(LogType.ConsoleOnly);
            }

            await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_MassStorage);

            MassStorage Storage = null;
            if (Notifier.CurrentModel is MassStorage)
                Storage = (MassStorage)Notifier.CurrentModel;

            if (Storage == null)
                throw new WPinternalsException("Failed to switch to Mass Storage Mode");
            string Drive = Storage.Drive;

            return Drive;
        }

        internal static async Task LumiaV2RelockPhone(PhoneNotifierViewModel Notifier, string FFUPath = null, bool DoResetFirst = true, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            if (SetWorkingStatus == null) SetWorkingStatus = (m, s, v, a, st) => { };
            if (UpdateWorkingStatus == null) UpdateWorkingStatus = (m, s, v, st) => { };
            if (ExitSuccess == null) ExitSuccess = (m, s) => { };
            if (ExitFailure == null) ExitFailure = (m, s) => { };

            LogFile.BeginAction("RelockPhone");
            try
            {
                GPT GPT = null;
                Partition Target = null;
                NokiaFlashModel FlashModel = null;

                LogFile.Log("Command: Relock phone", LogType.FileAndConsole);

                if (Notifier.CurrentInterface == null)
                    await Notifier.WaitForArrival();

                byte[] EFIESPBackup = null;

                try
                {
                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                    {
                        await SwitchModeViewModel.SwitchToWithStatus(Notifier, PhoneInterfaces.Lumia_MassStorage, SetWorkingStatus, UpdateWorkingStatus);
                    }

                    if (!(Notifier.CurrentModel is MassStorage))
                        throw new WPinternalsException("Failed to switch to Mass Storage mode");

                    SetWorkingStatus("Patching...", null, null, Status: WPinternalsStatus.Patching);

                    // Now relock the phone
                    MassStorage Storage = (MassStorage)Notifier.CurrentModel;

                    App.PatchEngine.TargetPath = Storage.Drive + "\\EFIESP\\";
                    App.PatchEngine.Restore("SecureBootHack-V2-EFIESP");

                    App.PatchEngine.TargetPath = Storage.Drive + "\\";
                    App.PatchEngine.Restore("SecureBootHack-MainOS");
                    App.PatchEngine.Restore("RootAccess-MainOS");

                    // Edit BCD
                    LogFile.Log("Edit BCD");
                    using (Stream BCDFileStream = new System.IO.FileStream(Storage.Drive + @"\EFIESP\efi\Microsoft\Boot\BCD", FileMode.Open, FileAccess.ReadWrite))
                    {
                        using (DiscUtils.Registry.RegistryHive BCDHive = new DiscUtils.Registry.RegistryHive(BCDFileStream))
                        {
                            DiscUtils.BootConfig.Store BCDStore = new DiscUtils.BootConfig.Store(BCDHive.Root);
                            DiscUtils.BootConfig.BcdObject MobileStartupObject = BCDStore.GetObject(new Guid("{01de5a27-8705-40db-bad6-96fa5187d4a6}"));
                            DiscUtils.BootConfig.Element NoCodeIntegrityElement = MobileStartupObject.GetElement(0x16000048);
                            if (NoCodeIntegrityElement != null)
                                MobileStartupObject.RemoveElement(0x16000048);

                            DiscUtils.BootConfig.BcdObject WinLoadObject = BCDStore.GetObject(new Guid("{7619dcc9-fafe-11d9-b411-000476eba25f}"));
                            NoCodeIntegrityElement = WinLoadObject.GetElement(0x16000048);
                            if (NoCodeIntegrityElement != null)
                                WinLoadObject.RemoveElement(0x16000048);
                        }
                    }

                    byte[] GPTBuffer = Storage.ReadSectors(0, 0x22);
                    GPT = new GPT(GPTBuffer);
                    Partition EFIESPPartition = GPT.GetPartition("EFIESP");
                    byte[] EFIESP = Storage.ReadSectors(EFIESPPartition.FirstSector, EFIESPPartition.SizeInSectors);
                    UInt32 EfiespSizeInSectors = (UInt32)EFIESPPartition.SizeInSectors;
                    
                    //
                    // (ByteOperations.ReadUInt32(EFIESP, 0x20) == (EfiespSizeInSectors / 2)) was originally present in this check, but it does not seem to be reliable with all cases
                    // It should be looked as why some phones have half the sector count in gpt, compared to the real partition.
                    // With that check added, the phone won't get back its original EFIESP partition, on phones like 650s.
                    // The second check should be more than enough in any case, if we find a header named MSDOS5.0 right in the middle of EFIESP,
                    // there's not many cases other than us splitting the partition in half to get this here.
                    //
                    if ((ByteOperations.ReadAsciiString(EFIESP, (UInt32)(EFIESP.Length / 2) + 3, 8)) == "MSDOS5.0")
                    {
                        EFIESPBackup = new byte[EfiespSizeInSectors * 0x200 / 2];
                        Buffer.BlockCopy(EFIESP, (Int32)EfiespSizeInSectors * 0x200 / 2, EFIESPBackup, 0, (Int32)EfiespSizeInSectors * 0x200 / 2);
                    }

                    LogFile.Log("The phone is currently in Mass Storage Mode", LogType.ConsoleOnly);
                    LogFile.Log("To continue the relock-sequence, the phone needs to be rebooted", LogType.ConsoleOnly);
                    LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                    LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                    LogFile.Log("The relock-sequence will resume automatically", LogType.ConsoleOnly);
                    LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                    SetWorkingStatus("You need to manually reset your phone now!", "The phone is currently in Mass Storage Mode. To continue the relock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates. The relock-sequence will resume automatically.", null, false, WPinternalsStatus.WaitingForManualReset);

                    await Notifier.WaitForRemoval();

                    SetWorkingStatus("Rebooting phone...");

                    await Notifier.WaitForArrival();
                }
                catch
                {
                    // If switching to mass storage mode failed, then we just skip that part. This might be a half unlocked phone.
                    LogFile.Log("Skipping Mass Storage mode", LogType.FileAndConsole);
                }

                // Phone can also be in normal mode if switching to Mass Storage Mode had failed.
                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Normal)
                    await SwitchModeViewModel.SwitchToWithStatus(Notifier, PhoneInterfaces.Lumia_Flash, SetWorkingStatus, UpdateWorkingStatus);

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                    await Notifier.WaitForArrival();

                SetWorkingStatus("Flashing...", "The phone may reboot a couple of times. Just wait for it.", null, Status: WPinternalsStatus.Flashing);

                ((NokiaFlashModel)Notifier.CurrentModel).SwitchToFlashAppContext();

                List<FlashPart> FlashParts = new List<FlashPart>();
                FlashPart Part;

                FlashModel = (NokiaFlashModel)Notifier.CurrentModel;

                // Remove IS_UNLOCKED flag in GPT
                byte[] GPTChunk = GetGptChunk(FlashModel, 0x20000); // TODO: Get proper profile FFU and get ChunkSizeInBytes
                GPT = new GPT(GPTChunk);
                bool GPTChanged = false;

                Partition IsUnlockedPartition = GPT.GetPartition("IS_UNLOCKED");
                if (IsUnlockedPartition != null)
                {
                    GPT.Partitions.Remove(IsUnlockedPartition);
                    GPTChanged = true;
                }

                Partition EfiEspBackupPartition = GPT.GetPartition("BACKUP_EFIESP");
                if (EfiEspBackupPartition != null)
                {
                    // This must be a left over of a half unlocked bootloader
                    Partition EfiEspPartition = GPT.GetPartition("EFIESP");
                    EfiEspBackupPartition.Name = "EFIESP";
                    EfiEspBackupPartition.LastSector = EfiEspPartition.LastSector;
                    EfiEspBackupPartition.PartitionGuid = EfiEspPartition.PartitionGuid;
                    EfiEspBackupPartition.PartitionTypeGuid = EfiEspPartition.PartitionTypeGuid;
                    GPT.Partitions.Remove(EfiEspPartition);
                    GPTChanged = true;
                }

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

                if (GPTChanged)
                {
                    GPT.Rebuild();
                    Part = new FlashPart();
                    Part.StartSector = 0;
                    Part.Stream = new MemoryStream(GPTChunk);
                    FlashParts.Add(Part);
                }

                if (EFIESPBackup != null)
                {
                    Part = new FlashPart();
                    Target = GPT.GetPartition("EFIESP");
                    Part.StartSector = (UInt32)Target.FirstSector;
                    Part.Stream = new MemoryStream(EFIESPBackup);
                    FlashParts.Add(Part);
                }

                // We should only clear NV if there was no backup NV to be restored and the current NV contains the SB unlock.
                bool NvCleared = false;
                PhoneInfo Info = ((NokiaFlashModel)Notifier.CurrentModel).ReadPhoneInfo();
                if ((NvBackupPartition == null) && !Info.UefiSecureBootEnabled)
                {
                    // ClearNV
                    Part = new FlashPart();
                    Target = GPT.GetPartition("UEFI_BS_NV");
                    Part.StartSector = (UInt32)Target.FirstSector;
                    Part.Stream = new MemoryStream(new byte[0x40000]);
                    FlashParts.Add(Part);
                    NvCleared = true;
                }

                WPinternalsStatus LastStatus = WPinternalsStatus.Undefined;
                ulong? MaxProgressValue = null;
                await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(Notifier, FFUPath, false, false, FlashParts, DoResetFirst, ClearFlashingStatusAtEnd: !NvCleared,
                    SetWorkingStatus: (m, s, v, a, st) =>
                    {
                        if (SetWorkingStatus != null)
                        {
                            if ((st == WPinternalsStatus.Scanning) || (st == WPinternalsStatus.WaitingForManualReset))
                                SetWorkingStatus(m, s, v, a, st);
                            else if ((LastStatus == WPinternalsStatus.Scanning) || (LastStatus == WPinternalsStatus.WaitingForManualReset) || (LastStatus == WPinternalsStatus.Undefined))
                            {
                                MaxProgressValue = v;
                                SetWorkingStatus("Flashing...", "The phone may reboot a couple of times. Just wait for it.", v, Status: WPinternalsStatus.Flashing);
                            }
                            LastStatus = st;
                        }
                    },
                    UpdateWorkingStatus: (m, s, v, st) =>
                    {
                        if (UpdateWorkingStatus != null)
                        {
                            if ((st == WPinternalsStatus.Scanning) || (st == WPinternalsStatus.WaitingForManualReset))
                                UpdateWorkingStatus(m, s, v, st);
                            else if ((LastStatus == WPinternalsStatus.Scanning) || (LastStatus == WPinternalsStatus.WaitingForManualReset))
                                SetWorkingStatus("Flashing...", "The phone may reboot a couple of times. Just wait for it.", MaxProgressValue, Status: WPinternalsStatus.Flashing);
                            else
                                UpdateWorkingStatus("Flashing...", "The phone may reboot a couple of times. Just wait for it.", v, Status: WPinternalsStatus.Flashing);
                            LastStatus = st;
                        }
                    });

                if (NvBackupPartition != null)
                {
                    // An old NV backup was restored and it possibly contained the IsFlashing flag.
                    // Can't clear it immeadiately, so we need another flash.

                    SetWorkingStatus("Flashing...", "The phone may reboot a couple of times. Just wait for it.", null, Status: WPinternalsStatus.Flashing);

                    // If last flash was a normal flash, with no forced crash at the end (!NvCleared), then we have to wait for device arrival, because it could still be detected as Flash-mode from previous flash.
                    // When phone was forcably crashed, it can be in emergency mode, or still rebooting. Then also wait for device arrival.
                    // But it is also possible that it is already in bootmgr mode after being crashed (Lumia 950 / 950XL). In that case don't wait for arrival.
                    if (!NvCleared || ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)))
                        await Notifier.WaitForArrival();

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                        ((NokiaFlashModel)Notifier.CurrentModel).SwitchToFlashAppContext();

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash)
                    {
                        await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(Notifier, FFUPath, false, false, null, DoResetFirst, ClearFlashingStatusAtEnd: true, ShowProgress: false);
                    }
                }

                LogFile.Log("Phone is relocked", LogType.FileAndConsole);
                ExitSuccess("The phone is relocked", "NOTE: Make sure the phone properly boots and shuts down at least once before you unlock it again");
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                ExitFailure("Error: " + Ex.Message, null);
            }
            finally
            {
                LogFile.EndAction("RelockPhone");
            }
        }

        internal static async Task LumiaV2ClearNV(System.Threading.SynchronizationContext UIContext, string FFUPath, bool DoResetFirst = true)
        {
            LogFile.BeginAction("ClearNV");
            try
            {
                LogFile.Log("Command: Clear NV", LogType.FileAndConsole);
                PhoneNotifierViewModel Notifier = new PhoneNotifierViewModel();
                UIContext.Send(s => Notifier.Start(), null);
                NokiaFlashModel FlashModel = (NokiaFlashModel)(await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash));
                List<FlashPart> Parts = new List<FlashPart>();

                // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
                // We need the fist sector if we want to write back the GPT.
                byte[] GPTChunk = GetGptChunk(FlashModel, 0x20000);
                GPT GPT = new GPT(GPTChunk);
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
                    UEFI_BS_NV = new Partition();
                    UEFI_BS_NV.Name = "UEFI_BS_NV";
                    UEFI_BS_NV.Attributes = BACKUP_BS_NV.Attributes;
                    UEFI_BS_NV.PartitionGuid = OriginalPartitionGuid;
                    UEFI_BS_NV.PartitionTypeGuid = OriginalPartitionTypeGuid;
                    UEFI_BS_NV.FirstSector = BACKUP_BS_NV.LastSector + 1;
                    UEFI_BS_NV.LastSector = UEFI_BS_NV.FirstSector + BACKUP_BS_NV.LastSector - BACKUP_BS_NV.FirstSector;
                    GPT.Partitions.Add(UEFI_BS_NV);
                    GPTChanged = true;
                }
                if (GPTChanged)
                {
                    GPT.Rebuild();
                    FlashPart Part = new FlashPart();
                    Part.StartSector = 0;
                    Part.Stream = new MemoryStream(GPTChunk);
                    Parts.Add(Part);
                }

                using (MemoryStream Space = new MemoryStream(new byte[0x40000]))
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
                    LogFile.Log("Profile FFU file: " + FFUPath, LogType.FileAndConsole);

                PhoneNotifierViewModel Notifier = new PhoneNotifierViewModel();
                UIContext.Send(s => Notifier.Start(), null);

                NokiaFlashModel FlashModel = (NokiaFlashModel)(await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash));

                PhoneInfo Info = FlashModel.ReadPhoneInfo();

                // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
                // We need the fist sector if we want to write back the GPT.
                byte[] GPTChunk = GetGptChunk(FlashModel, 0x20000);
                GPT GPT = new GPT(GPTChunk);

                Partition TargetPartition = GPT.GetPartition(PartitionName);
                if (TargetPartition == null)
                    throw new WPinternalsException("Target partition not found!");
                LogFile.Log("Target-partition found at sector: 0x" + TargetPartition.FirstSector.ToString("X8") + " - 0x" + TargetPartition.LastSector.ToString("X8"), LogType.FileAndConsole);

                bool IsUnlocked = false;
                bool GPTChanged = false;
                List<FlashPart> Parts = new List<FlashPart>();
                FlashPart Part;
                if (string.Compare(PartitionName, "EFIESP", true) == 0)
                {
                    byte[] EfiespBinary = File.ReadAllBytes(PartitionPath);
                    IsUnlocked = ((ByteOperations.ReadUInt32(EfiespBinary, 0x20) == (EfiespBinary.Length / 0x200 / 2)) && (ByteOperations.ReadAsciiString(EfiespBinary, (UInt32)(EfiespBinary.Length / 2) + 3, 8)) == "MSDOS5.0");

                    if (IsUnlocked)
                    {
                        Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                        if (IsUnlockedFlag == null)
                        {
                            IsUnlockedFlag = new Partition();
                            IsUnlockedFlag.Name = "IS_UNLOCKED";
                            IsUnlockedFlag.Attributes = 0;
                            IsUnlockedFlag.PartitionGuid = Guid.NewGuid();
                            IsUnlockedFlag.PartitionTypeGuid = Guid.NewGuid();
                            IsUnlockedFlag.FirstSector = 0x40;
                            IsUnlockedFlag.LastSector = 0x40;
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
                        Part = new FlashPart();
                        Part.StartSector = 0;
                        Part.Stream = new MemoryStream(GPTChunk);
                        Parts.Add(Part);
                    }
                }

                using (FileStream Stream = new FileStream(PartitionPath, FileMode.Open))
                {
                    if ((UInt64)Stream.Length != (TargetPartition.SizeInSectors * 0x200))
                        throw new WPinternalsException("Raw partition has wrong size. Size = 0x" + Stream.Length.ToString("X8") + ". Expected size = 0x" + (TargetPartition.SizeInSectors * 0x200).ToString("X8"));

                    Part = new FlashPart();
                    Part.StartSector = (UInt32)TargetPartition.FirstSector;
                    Part.Stream = Stream;
                    Parts.Add(Part);
                    await LumiaV2CustomFlash(Notifier, FFUPath, false, false, Parts, DoResetFirst, string.Compare(PartitionName, "UEFI_BS_NV", true) != 0);
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
                    LogFile.Log("FFU file: " + FFUPath, LogType.FileAndConsole);

                PhoneNotifierViewModel Notifier = new PhoneNotifierViewModel();
                UIContext.Send(s => Notifier.Start(), null);

                NokiaFlashModel FlashModel = (NokiaFlashModel)(await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash));

                PhoneInfo Info = FlashModel.ReadPhoneInfo();

                byte[] Data = System.IO.File.ReadAllBytes(DataPath);

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
            using (MemoryStream Stream = new MemoryStream(Data))
            {
                FlashPart Part = new FlashPart() { StartSector = StartSector, Stream = Stream };
                List<FlashPart> Parts = new List<FlashPart>();
                Parts.Add(Part);
                await LumiaV2CustomFlash(Notifier, FFUPath, PerformFullFlashFirst, SkipWrite, Parts, DoResetFirst, ClearFlashingStatusAtEnd, CheckSectorAlignment, ShowProgress, Experimental);
            }
        }

        internal async static Task LumiaV2CustomFlash(PhoneNotifierViewModel Notifier, string FFUPath, bool PerformFullFlashFirst, bool SkipWrite, UInt32 StartSector, Stream Data, bool DoResetFirst = true, bool ClearFlashingStatusAtEnd = true, bool CheckSectorAlignment = true, bool ShowProgress = true, bool Experimental = false) //, string LoaderPath = null)
        {
            FlashPart Part = new FlashPart() { StartSector = StartSector, Stream = Data };
            List<FlashPart> Parts = new List<FlashPart>();
            Parts.Add(Part);
            await LumiaV2CustomFlash(Notifier, FFUPath, PerformFullFlashFirst, SkipWrite, Parts, DoResetFirst, ClearFlashingStatusAtEnd, CheckSectorAlignment, ShowProgress, Experimental);
        }

        // Magic!
        internal async static Task LumiaV2CustomFlash(PhoneNotifierViewModel Notifier, string FFUPath, bool PerformFullFlashFirst, bool SkipWrite, List<FlashPart> FlashParts, bool DoResetFirst = true, bool ClearFlashingStatusAtEnd = true, bool CheckSectorAlignment = true, bool ShowProgress = true, bool Experimental = false, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null, string ProgrammerPath = null) //, string LoaderPath = null)
        {
            // Both SecurityHeader and StoreHeader need to be modified.
            // Those should both not fall in a memory-gap to allow modification.
            // The partial FFU header must be allocated in front of those headers, so the size of the partial header must be at least the size of the the SecurityHeader.
            // Hashes take more space than descriptors, so the SecurityHeader will always be the biggest.

            bool AutoEmergencyReset = true;
            bool Timeout;

            if (SetWorkingStatus == null) SetWorkingStatus = (m, s, v, a, st) => { };
            if (UpdateWorkingStatus == null) UpdateWorkingStatus = (m, s, v, st) => { };
            if (ExitSuccess == null) ExitSuccess = (m, s) => { };
            if (ExitFailure == null) ExitFailure = (m, s) => { };

            NokiaFlashModel Model = (NokiaFlashModel)Notifier.CurrentModel;
            PhoneInfo Info = Model.ReadPhoneInfo();

            string Type = Info.Type;
            if (ProgrammerPath == null)
            {
                ProgrammerPath = GetProgrammerPath(Info.RKH, Type);
                if (ProgrammerPath == null)
                    LogFile.Log("WARNING: No emergency programmer file found. Finding flash profile and rebooting phone may take a long time!", LogType.FileAndConsole);
            }
            List<FFUEntry> FFUs = null;
            FlashProfile Profile;
            if (FFUPath == null)
            {
                // Try to find an FFU from the repository for which there is also a known flashing profile
                FFUs = App.Config.FFURepository.Where(e => (Info.PlatformID.StartsWith(e.PlatformID, StringComparison.OrdinalIgnoreCase) && e.Exists())).ToList();
                foreach (FFUEntry CurrentEntry in FFUs)
                {
                    Profile = App.Config.GetProfile(Info.PlatformID, Info.Firmware, CurrentEntry.FirmwareVersion);
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
                throw new WPinternalsException("No valid profile FFU found in repository", "You can download necessary files in the &quot;Download&quot; section");

            FFU FFU = new FFU(FFUPath);
            UInt32 UpdateType = ByteOperations.ReadUInt32(FFU.StoreHeader, 0);
            if (UpdateType != 0)
                throw new WPinternalsException("Only Full Flash images supported");

            UInt32 ChunkCount = 1; // Always flash one extra chunk on the GPT (for purpose of testing and for making sure that first chunk does not contain all zero's).
            if (FlashParts != null)
            {
                foreach (FlashPart Part in FlashParts)
                {
                    if (Part.Stream == null)
                        throw new ArgumentException("Stream is null");
                    if (!Part.Stream.CanSeek)
                        throw new ArgumentException("Streams must be seekable");
                    if (((Part.StartSector * 0x200) % FFU.ChunkSize) != 0)
                        throw new ArgumentException("Invalid StartSector alignment");
                    if (CheckSectorAlignment)
                    {
                        if ((Part.Stream.Length % FFU.ChunkSize) != 0)
                            throw new ArgumentException("Invalid Data length");
                    }

                    ChunkCount += (UInt32)(Part.Stream.Length / FFU.ChunkSize);
                }
            }

            if ((Info.SecureFfuSupportedProtocolMask & ((ushort)FfuProtocol.ProtocolSyncV2)) == 0) // Exploit needs protocol v2 -> This check is not conclusive, because old phones also report support for this protocol, although it is really not supported.
                throw new WPinternalsException("Flash failed!", "Protocols not supported");
            if (Info.FlashAppProtocolVersionMajor < 2) // Old phones do not support the hack. These phones have Flash protocol 1.x.
                throw new WPinternalsException("Flash failed!", "Protocols not supported");
            UEFI UEFI = new UEFI(FFU.GetPartition("UEFI"));
            string BootMgrName = UEFI.EFIs.Where(efi => ((efi.Name != null) && (efi.Name.Contains("BootMgrApp")))).First().Name;
            UInt32 EstimatedSizeOfMemGap = (UInt32)UEFI.GetFile(BootMgrName).Length;
            byte Options = 0;
            if (SkipWrite)
                Options = (byte)FlashOptions.SkipWrite;
            if (!Info.SecureFfuEnabled || Info.Authenticated || Info.RdcPresent)
                Options = (byte)((FlashOptions)Options | FlashOptions.SkipSignatureCheck);

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

            byte[] GPTChunk = GetGptChunk(Model, (UInt32)FFU.ChunkSize);

            // Start with a reset
            if (DoResetFirst)
            {
                SetWorkingStatus("Initializing flash...", "Rebooting phone", null, Status: WPinternalsStatus.Initializing);

                // When in flash mode, it is not possible to reboot straight to flash.
                // Reboot and catch the phone in bootloader mode and then switch to flash context
                Model.ResetPhone();

                #region Properly recover from reset - many phones respond differently

                Timeout = false;
                try
                {
                    await Notifier.WaitForArrival().TimeoutAfter<IDisposable>(TimeSpan.FromSeconds(40));
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

                    SetWorkingStatus("You need to manually reset your phone now!", (Timeout ? "The phone is not responding. It might be in emergency mode, while you have no matching driver installed. " : "") + "To continue the unlock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates. The unlock-sequence will resume automatically.", null, false, WPinternalsStatus.WaitingForManualReset);

                    await Notifier.WaitForRemoval();

                    UpdateWorkingStatus("Initializing flash...", null, null);

                    await Notifier.WaitForArrival();
                }
                if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                {
                    if (ProgrammerPath != null)
                    {
                        QualcommSahara Sahara = new QualcommSahara((QualcommSerial)Notifier.CurrentModel);
                        await Sahara.Reset(ProgrammerPath);
                        await Notifier.WaitForArrival();
                    }
                    else
                    {
                        ((QualcommSerial)Notifier.CurrentModel).Close(); // Prevent "Resource in use";

                        Timeout = false;
                        if (AutoEmergencyReset)
                        {
                            try
                            {
                                await Notifier.WaitForArrival().TimeoutAfter<IDisposable>(TimeSpan.FromSeconds(40));
                            }
                            catch (TimeoutException)
                            {
                                Timeout = true;
                            }
                        }
                        if (!AutoEmergencyReset || Timeout)
                        {
                            AutoEmergencyReset = false;

                            LogFile.Log("The phone is in emergency mode and you didn't provide an emergency programmer", LogType.ConsoleOnly);
                            LogFile.Log("This phone also doesn't seem to reboot after a timeout, so you got to help a bit", LogType.ConsoleOnly);
                            LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                            LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                            LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                            LogFile.Log("To prevent this, provide an emergency programmer next time you will unlock a bootloader", LogType.ConsoleOnly);
                            LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                            SetWorkingStatus("You need to manually reset your phone now!", "The phone is in emergency mode and you didn't provide an emergency programmer. This phone also doesn't seem to reboot after a timeout, so you got to help a bit. Keep the phone connected to the PC. Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates. The unlock-sequence will resume automatically. To prevent this, provide an emergency programmer next time you will unlock a bootloader.", null, false, WPinternalsStatus.WaitingForManualReset);

                            await Notifier.WaitForRemoval();

                            UpdateWorkingStatus("Initializing flash...", null, null);

                            await Notifier.WaitForArrival();
                        }
                    }
                }

                #endregion

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader))
                    throw new WPinternalsException("Phone is in wrong mode");
                Model = (NokiaFlashModel)Notifier.CurrentModel;
                UpdateWorkingStatus("Initializing flash...", null, null);
            }

            try
            {
                // This will succeed on new models
                Model.SwitchToFlashAppContext();
                Model.DisableRebootTimeOut();
            }
            catch
            {
                // This will succeed on old models
                Model.ResetPhoneToFlashMode();
                await Notifier.WaitForArrival();
                Model = (NokiaFlashModel)Notifier.CurrentModel;
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

            Profile = App.Config.GetProfile(Info.PlatformID, Info.Firmware, FFU.GetFirmwareVersion());
            if (Profile == null)
                LogFile.Log("No flashing profile found", LogType.FileAndConsole);
            else
            {
                if (ShowProgress)
                    LogFile.Log("Flashing profile loaded", LogType.FileAndConsole);
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
                        SetWorkingStatus("Scanning for flashing-profile - attempt " + AttemptCount.ToString() + " of " + MaximumAttempts.ToString(), "Your phone may appear to be in a reboot-loop. This is expected behavior. Don't interfere this process.", (uint)MaximumAttempts, Status: WPinternalsStatus.Scanning);
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
                    Model.StartAsyncFlash();
                    Model.EndAsyncFlash(); // Ending Async flashing is not necessary for Lumia 950, but it is necessary for Lumia 640!
                }

                if (AllocateBackupBuffersOnPhone)
                {
                    Model.BackupPartitionToRam("MODEM_FSG");
                    Model.BackupPartitionToRam("MODEM_FS1");
                    Model.BackupPartitionToRam("MODEM_FS2");
                    Model.BackupPartitionToRam("SSD");
                    Model.BackupPartitionToRam("DPP");
                }

                HeaderOffset = 0;
                SecurityHeaderAllocation = null;
                ImageHeaderAllocation = null;
                StoreHeaderAllocation = null;
                PartialHeaderAllocation = null;
                UInt32 DestinationChunkIndex = 0;
                UInt32 DestinationChunkOffset = 0;

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
                UInt32 TotalChunkCount = ChunkCount;
                bool HeadersFull;
                int FlashingPhase = 0;
                int FlashingPhaseStartStreamIndex = -1;
                long FlashingPhaseStartStreamPosition = 0;
                UInt32 FlashingPhaseStartChunkIndex = 0;
                UInt32 FlashingPhaseChunkCount;
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
                            Model.SendFfuHeaderV2(LastHeaderV2Size + 1, 0, new byte[1], Options);
                        }

                        // CurrentGapFill is the amount of data we want to be allocated on the phone
                        // But we send less data, so the header won't be processed yet.
                        PartialHeader = new byte[UefiMemorySim.PageSize];
                        Model.SendFfuHeaderV2(CurrentGapFill, 0, PartialHeader, Options); // Fill memory gap -> This will fail on phones with Flash Protocol v1.x !! On Lumia 640 this will hang on receiving the response when EndAsyncFlash was not called.
                    }

                    using (System.IO.FileStream FfuFile = new System.IO.FileStream(FFU.Path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        // On every flashing phase we need to send the full header again to reset all the counters.
                        FfuFile.Read(FfuHeader, 0, (int)CombinedFFUHeaderSize);
                        Model.SendFfuHeaderV1(FfuHeader, Options);

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
                            TotalChunkCount += (UInt32)FFU.TotalChunkCount;

                            // Protocol v2
                            FlashPayload = new byte[Info.WriteBufferSize];

                            while (Position < (UInt64)FfuFile.Length)
                            {
                                UInt32 CommonFlashPayloadSize = Info.WriteBufferSize;
                                if (((UInt64)FfuFile.Length - Position) < CommonFlashPayloadSize)
                                {
                                    CommonFlashPayloadSize = (UInt32)((UInt64)FfuFile.Length - Position);
                                    FlashPayload = new byte[CommonFlashPayloadSize];
                                }

                                FfuFile.Read(FlashPayload, 0, (int)CommonFlashPayloadSize);
                                ChunkIndex += (int)(CommonFlashPayloadSize / FFU.ChunkSize);
                                Model.SendFfuPayloadV2(FlashPayload, ShowProgress ? (int)((double)(ChunkIndex + 1) * 100 / TotalChunkCount) : 0, 0);
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
                        HashTableSize = (UInt32)(((FFU.ImageHeader.Length + FFU.StoreHeader.Length) / FFU.ChunkSize) * 0x20);
                        NewHashOffset += HashTableSize;
                    }

                    // Determine number of chunks for this phase
                    UInt32 HashSpace = (UInt32)(FFU.SecurityHeader.Length - NewHashOffset);
                    UInt32 FreeHashCount = HashSpace / 0x20; // Round down automatically
                    UInt32 DescriptorSpace = (UInt32)(FFU.StoreHeader.Length - NewWriteDescriptorOffset);
                    UInt32 FreeDescriptorCount = DescriptorSpace / 0x10;
                    FlashingPhaseChunkCount = FreeHashCount < FreeDescriptorCount ? FreeHashCount : FreeDescriptorCount;
                    if ((ChunkCount - FlashingPhaseStartChunkIndex) <= FlashingPhaseChunkCount)
                        FlashingPhaseChunkCount = ChunkCount - FlashingPhaseStartChunkIndex;
                    else
                        HeadersFull = true;

                    HashTableSize += (FlashingPhaseChunkCount * 0x20);
                    WriteDescriptorCount += FlashingPhaseChunkCount;
                    WriteDescriptorLength += (FlashingPhaseChunkCount * 0x10);

                    if (!ClearFlashingStatusAtEnd || HeadersFull)
                        WriteDescriptorCount++;

                    // Write back new header values.
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xD4, WriteDescriptorLength);
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xD0, WriteDescriptorCount);
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, SecurityHeaderAllocation.ContentStart + 0x1C, HashTableSize);
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xEC, 0); // FlashOnlyTableLength - Make flash progress bar white immediately.
                    ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + 0xE8, 1); // FlashOnlyTableCount

                    UInt32 CustomChunkCount = FlashingPhaseChunkCount;

                    // Write new descriptors
                    // First write descriptor and hash for the first GPT chunk
                    if (!FlashInProgress && (FlashingPhaseChunkCount > 0))
                    {
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x00, 0x00000001); // Location count
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x04, 0x00000001); // Chunk count
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x08, 0x00000000); // Disk access method (0 = Begin, 2 = End)
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x0C, 0x00000000); // Chunk index = GPT
                        NewWriteDescriptorOffset += 0x10;
                        byte[] GPTHashValue = System.Security.Cryptography.SHA256.Create().ComputeHash(GPTChunk, 0, FFU.ChunkSize); // Hash is 0x20 bytes
                        System.Buffer.BlockCopy(GPTHashValue, 0, UefiMemorySim.Buffer, (int)(SecurityHeaderAllocation.ContentStart + NewHashOffset), 0x20);
                        NewHashOffset += 0x20;
                        CustomChunkCount--;
                    }

                    // TODO: Optimize: make multiple locations for chunks with same content.
                    Stream CurrentStream = null;
                    int StreamIndex = FlashingPhaseStartStreamIndex;
                    if (StreamIndex >= 0)
                    {
                        CurrentStream = FlashParts[StreamIndex].Stream;
                        CurrentStream.Seek(FlashingPhaseStartStreamPosition, SeekOrigin.Begin);
                    }
                    byte[] PayloadBuffer = new byte[FFU.ChunkSize];
                    for (int i = 0; i < CustomChunkCount; i++)
                    {
                        if ((CurrentStream == null) || (CurrentStream.Position == CurrentStream.Length))
                        {
                            StreamIndex++;
                            CurrentStream = FlashParts[StreamIndex].Stream;
                            CurrentStream.Seek(0, SeekOrigin.Begin);
                            DestinationChunkOffset = (UInt32)((Int64)FlashParts[StreamIndex].StartSector * 0x200 / FFU.ChunkSize);
                        }

                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x00, 0x00000001); // Location count
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x04, 0x00000001); // Chunk count
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x08, 0x00000000); // Disk access method (0 = Begin, 2 = End)
                        ByteOperations.WriteUInt32(UefiMemorySim.Buffer, StoreHeaderAllocation.ContentStart + NewWriteDescriptorOffset + 0x0C, DestinationChunkOffset); // Chunk index
                        NewWriteDescriptorOffset += 0x10;

                        // Write new hash
                        if ((CurrentStream.Length - CurrentStream.Position) < PayloadBuffer.Length)
                            Array.Clear(PayloadBuffer, 0, PayloadBuffer.Length);
                        CurrentStream.Read(PayloadBuffer, 0, FFU.ChunkSize);
                        byte[] HashValue = System.Security.Cryptography.SHA256.Create().ComputeHash(PayloadBuffer, 0, FFU.ChunkSize); // Hash is 0x20 bytes
                        System.Buffer.BlockCopy(HashValue, 0, UefiMemorySim.Buffer, (int)(SecurityHeaderAllocation.ContentStart + NewHashOffset), 0x20);
                        NewHashOffset += 0x20;

                        DestinationChunkOffset++;
                    }

                    int Step = 0;
                    try
                    {
                        // Send a small portion of header v2 at offset 0
                        // The payload is smaller than the total headersize, so that it won't start processing the header now.
                        // This will allocate new memory at the bottom of the memory-pool, but it will not reset the previously imported ffu header.
                        Step = 1;
                        PartialHeader = new byte[UefiMemorySim.PageSize];
                        Model.SendFfuHeaderV2(ExploitHeaderAllocationSize, 0, PartialHeader, Options); // SkipWrite = 1 (only works on engineering phones)

                        // Now we will send the rest of the exploit header, but we will increase the total size even higher, so that it still won't start processing the headers.
                        // We've send only a small first part of the header. The allocated header was bigger: ExploitHeaderAllocationSize.
                        // But we HAVE to send the whole header. We can't skip a part.
                        Step = 2;
                        UInt32 ExploitHeaderRemaining = SecurityHeaderAllocation.TailEnd + 1 - PartialHeaderAllocation.ContentStart - (UInt32)PartialHeader.Length;
                        HeaderOffset = (UInt32)PartialHeader.Length;
                        while (ExploitHeaderRemaining > 0)
                        {
                            UInt32 CurrentFill = ExploitHeaderRemaining;
                            if (CurrentFill > Info.WriteBufferSize)
                                CurrentFill = Info.WriteBufferSize;
                            PartialHeader = new byte[CurrentFill];
                            PartialHeaderAllocation.CopyFromThisAllocation(HeaderOffset, CurrentFill, PartialHeader, 0);
                            Model.SendFfuHeaderV2(HeaderOffset + CurrentFill + 1, HeaderOffset, PartialHeader, Options); // Phone may crash here. USB write is done. USB read might fail due to crash. Happens on my own Lumia 650.
                            LastHeaderV2Size = HeaderOffset + CurrentFill + 1;
                            ExploitHeaderRemaining -= CurrentFill;
                            HeaderOffset += CurrentFill;
                        }

                        // Send custom payload
                        // TODO: Optimize to send multiple chunks at once
                        Step = 3;
                        DestinationChunkIndex = (PerformFullFlashFirst && (FlashingPhase == 0)) ? (UInt32)FFU.TotalChunkCount : 0;

                        CurrentStream = null;
                        StreamIndex = FlashingPhaseStartStreamIndex;
                        if (StreamIndex >= 0)
                        {
                            CurrentStream = FlashParts[StreamIndex].Stream;
                            CurrentStream.Seek(FlashingPhaseStartStreamPosition, SeekOrigin.Begin);
                        }

                        for (int i = 0; i < FlashingPhaseChunkCount; i++)
                        {
                            string NewProgressText = null;
                            if (!FlashInProgress)
                            {
                                // First send the GPT chunk
                                Step = 4;
                                System.Buffer.BlockCopy(GPTChunk, 0, Buffer, 0, (int)FFU.ChunkSize);
                            }
                            else
                            {
                                Step = 5;
                                if ((CurrentStream == null) || (CurrentStream.Position == CurrentStream.Length))
                                {
                                    StreamIndex++;
                                    CurrentStream = FlashParts[StreamIndex].Stream;
                                    CurrentStream.Seek(0, SeekOrigin.Begin);
                                    NewProgressText = FlashParts[StreamIndex].ProgressText;
                                }

                                Step = 6;
                                if ((CurrentStream.Length - CurrentStream.Position) < Buffer.Length)
                                    Array.Clear(Buffer, 0, Buffer.Length);

                                Step = 7;
                                CurrentStream.Read(Buffer, 0, FFU.ChunkSize);
                            }

                            Step = 8;
                            // This may fail. Normally with WPinternalsException for Invalid Hash or Data not aligned.
                            // Or it may fail with a BadConnectionException when the phone crashes and drops the connection.
                            Model.SendFfuPayloadV1(Buffer, ShowProgress ? (int)((FlashingPhaseStartChunkIndex + DestinationChunkIndex + 1) * 100 / TotalChunkCount) : 0);
                            if (!FlashInProgress)
                            {
                                Step = 9;
                                if (ShowProgress)
                                    LogFile.Log("Flashing in progress!", LogType.FileAndConsole);
                                FlashInProgress = true;
                                Scanning = false;
                                SetWorkingStatus(null, null, TotalChunkCount, Status: WPinternalsStatus.Flashing);
                            }
                            UpdateWorkingStatus(NewProgressText, null, FlashingPhaseStartChunkIndex + DestinationChunkIndex + 1, WPinternalsStatus.Flashing);
                            NewProgressText = null;
                            DestinationChunkIndex++;
                        }

                        Step = 10;
                        FlashingPhaseStartChunkIndex += FlashingPhaseChunkCount;
                        FlashingPhaseStartStreamIndex = StreamIndex;
                        if (StreamIndex >= 0)
                            FlashingPhaseStartStreamPosition = CurrentStream.Position;

                        Step = 11;
                        if (!HeadersFull)
                        {
                            Step = 12;
                            App.Config.SetProfile(Info.Type, Info.PlatformID, Info.ProductCode, Info.Firmware, FFU.GetFirmwareVersion(), CurrentGapFill, ExploitHeaderAllocationSize, AssumeImageHeaderFallsInGap, AllocateAsyncBuffersOnPhone);
                            if (ShowProgress)
                                LogFile.Log("Custom flash succeeded!", LogType.FileAndConsole);
                            Success = true;
                        }
                    }
                    catch (BadConnectionException)
                    {
                        LogFile.Log("Connection to phone is lost - " +
                            Step.ToString() + " " +
                            StreamIndex.ToString() + " " +
                            (CurrentStream == null ? "0" : CurrentStream.Position.ToString()) + " " +
                            FlashingPhase.ToString() + " " +
                            FlashingPhaseStartChunkIndex.ToString() + " " +
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
                                FlashingPhaseStartChunkIndex.ToString() + " " +
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
                                FlashingPhaseStartChunkIndex.ToString() + " " +
                                DestinationChunkIndex.ToString());
                        }

                        PhoneNeedsReset = true;
                    }

                    if (FlashInProgress)
                        FlashingPhase++;
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
                        Model.ResetPhone();
                        WaitForReset = true;
                    }

                    if (WaitForReset)
                    {
                        #region Properly recover from reset between flash attempts - many phones respond differently

                        Timeout = false;
                        try
                        {
                            await Notifier.WaitForArrival().TimeoutAfter<IDisposable>(TimeSpan.FromSeconds(40));
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

                            UpdateWorkingStatus("You need to manually reset your phone now!", (Timeout ? "The phone is not responding. It might be in emergency mode, while you have no matching driver installed. " : "") + "To continue the unlock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates. The unlock-sequence will resume automatically.", null, WPinternalsStatus.WaitingForManualReset);

                            await Notifier.WaitForRemoval();

                            UpdateWorkingStatus("Scanning for flashing-profile - attempt " + AttemptCount.ToString() + " of " + MaximumAttempts.ToString(), "Your phone may appear to be in a reboot-loop. This is expected behavior. Don't interfere this process.", (uint)AttemptCount, Status: WPinternalsStatus.Scanning);

                            await Notifier.WaitForArrival();
                        }
                        if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                        {
                            if (ProgrammerPath != null)
                            {
                                QualcommSahara Sahara = new QualcommSahara((QualcommSerial)Notifier.CurrentModel);
                                await Sahara.Reset(ProgrammerPath);
                                await Notifier.WaitForArrival();
                            }
                            else
                            {
                                ((QualcommSerial)Notifier.CurrentModel).Close(); // Prevent "Resource in use";

                                Timeout = false;
                                if (AutoEmergencyReset)
                                {
                                    try
                                    {
                                        await Notifier.WaitForArrival().TimeoutAfter<IDisposable>(TimeSpan.FromSeconds(40));
                                    }
                                    catch (TimeoutException)
                                    {
                                        Timeout = true;
                                    }
                                }
                                if (!AutoEmergencyReset || Timeout)
                                {
                                    AutoEmergencyReset = false;

                                    LogFile.Log("The phone is in emergency mode and you didn't provide an emergency programmer", LogType.ConsoleOnly);
                                    LogFile.Log("This phone also doesn't seem to reboot after a timeout, so you got to help a bit", LogType.ConsoleOnly);
                                    LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                                    LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                                    LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                                    LogFile.Log("To prevent this, provide an emergency programmer next time you will unlock a bootloader", LogType.ConsoleOnly);
                                    LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                                    UpdateWorkingStatus("You need to manually reset your phone now!", "The phone is in emergency mode and you didn't provide an emergency programmer. This phone also doesn't seem to reboot after a timeout, so you got to help a bit. Keep the phone connected to the PC. Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates. The unlock-sequence will resume automatically. To prevent this, provide an emergency programmer next time you will unlock a bootloader.", null, WPinternalsStatus.WaitingForManualReset);

                                    await Notifier.WaitForRemoval();

                                    UpdateWorkingStatus("Scanning for flashing-profile - attempt " + AttemptCount.ToString() + " of " + MaximumAttempts.ToString(), "Your phone may appear to be in a reboot-loop. This is expected behavior. Don't interfere this process.", (uint)AttemptCount, Status: WPinternalsStatus.Scanning);

                                    await Notifier.WaitForArrival();
                                }
                            }
                        }

                        #endregion

                        // Sanity check: must be in flash mode
                        if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader))
                            break;

                        Model = (NokiaFlashModel)Notifier.CurrentModel;

                        // In case we are on an Engineering phone which isn't stuck in flashmode and booted to BootMgrApp
                        Model.SwitchToFlashAppContext();
                        Model.DisableRebootTimeOut();
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
                            AllocateAsyncBuffersOnPhone = false;
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
                    Model.SendFfuHeaderV2(LastHeaderV2Size + 1, 0, new byte[1], Options);
                    PartialHeader = new byte[UefiMemorySim.PageSize];
                    Model.SendFfuHeaderV2(CurrentGapFill, 0, PartialHeader, Options); // Fill memory gap
                }
                using (System.IO.FileStream FfuFile = new System.IO.FileStream(FFU.Path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    // On every flashing phase we need to send the full header again, because this triggers ffu_import_invalidate(), which is necessary to reset all the counters.
                    FfuFile.Read(FfuHeader, 0, (int)CombinedFFUHeaderSize);
                    Model.SendFfuHeaderV1(FfuHeader, Options);
                }
                PartialHeader = new byte[UefiMemorySim.PageSize];
                Model.SendFfuHeaderV2(ExploitHeaderAllocationSize, 0, PartialHeader, Options); // SkipWrite = 1 (only works on engineering phones)
                UInt32 ExploitHeaderRemaining = SecurityHeaderAllocation.TailEnd + 1 - PartialHeaderAllocation.ContentStart - (UInt32)PartialHeader.Length;
                HeaderOffset = (UInt32)PartialHeader.Length;
                while (ExploitHeaderRemaining > 0)
                {
                    UInt32 CurrentFill = ExploitHeaderRemaining;
                    if (CurrentFill > Info.WriteBufferSize)
                        CurrentFill = Info.WriteBufferSize;
                    PartialHeader = new byte[CurrentFill];
                    PartialHeaderAllocation.CopyFromThisAllocation(HeaderOffset, CurrentFill, PartialHeader, 0);
                    Model.SendFfuHeaderV2(HeaderOffset + CurrentFill + 1, HeaderOffset, PartialHeader, Options);
                    LastHeaderV2Size = HeaderOffset + CurrentFill + 1;
                    ExploitHeaderRemaining -= CurrentFill;
                    HeaderOffset += CurrentFill;
                }

                // Do the actual reset, which will result in a crash while cleaning up memory
                ((NokiaFlashModel)Notifier.CurrentModel).ResetPhone();

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
                    await Notifier.WaitForArrival().TimeoutAfter<IDisposable>(TimeSpan.FromSeconds(40));
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

                    SetWorkingStatus("You need to manually reset your phone now!", (Timeout ? "The phone is not responding. It might be in emergency mode, while you have no matching driver installed. " : "") + "To continue the unlock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates. The unlock-sequence will resume automatically.", null, false, WPinternalsStatus.WaitingForManualReset);

                    await Notifier.WaitForRemoval();

                    SetWorkingStatus("Rebooting phone...");
                }
                if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                {
                    if (ProgrammerPath != null)
                    {
                        QualcommSahara Sahara = new QualcommSahara((QualcommSerial)Notifier.CurrentModel);
                        await Sahara.Reset(ProgrammerPath);
                    }
                    else
                    {
                        ((QualcommSerial)Notifier.CurrentModel).Close(); // Prevent "Resource in use";

                        Timeout = false;
                        if (AutoEmergencyReset)
                        {
                            try
                            {
                                await Notifier.WaitForArrival().TimeoutAfter<IDisposable>(TimeSpan.FromSeconds(40));
                            }
                            catch (TimeoutException)
                            {
                                Timeout = true;
                            }
                        }
                        if (!AutoEmergencyReset || Timeout)
                        {
                            AutoEmergencyReset = false;

                            LogFile.Log("The phone is in emergency mode and you didn't provide an emergency programmer", LogType.ConsoleOnly);
                            LogFile.Log("This phone also doesn't seem to reboot after a timeout, so you got to help a bit", LogType.ConsoleOnly);
                            LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                            LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                            LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                            LogFile.Log("To prevent this, provide an emergency programmer next time you will unlock a bootloader", LogType.ConsoleOnly);
                            LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                            SetWorkingStatus("You need to manually reset your phone now!", "The phone is in emergency mode and you didn't provide an emergency programmer. This phone also doesn't seem to reboot after a timeout, so you got to help a bit. Keep the phone connected to the PC. Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates. The unlock-sequence will resume automatically. To prevent this, provide an emergency programmer next time you will unlock a bootloader.", null, false, WPinternalsStatus.WaitingForManualReset);

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
                ((NokiaFlashModel)Notifier.CurrentModel).ResetPhone();
            }

            if (Success)
                ExitSuccess("Flash succeeded!", null);
            else
                throw new WPinternalsException("Custom flash failed");
        }

        internal static string GetProgrammerPath(byte[] RKH, string Type)
        {
            IEnumerable<EmergencyFileEntry> RKHEntries = App.Config.EmergencyRepository.Where(e => (StructuralComparisons.StructuralEqualityComparer.Equals(e.RKH, RKH) && e.ProgrammerExists()));
            if (RKHEntries.Count() > 0)
            {
                if (RKHEntries.Count() == 1)
                    return RKHEntries.First().ProgrammerPath;
                else
                {
                    EmergencyFileEntry RKHEntry = RKHEntries.Where(e => string.Compare(e.Type, Type, false) == 0).FirstOrDefault();
                    if (RKHEntry != null)
                        return RKHEntry.ProgrammerPath;
                    else
                        return RKHEntries.First().ProgrammerPath; // Cannot be sure this is the right one!!
                }
            }
            else
            {
                EmergencyFileEntry TypeEntry = App.Config.EmergencyRepository.Where(e => ((string.Compare(e.Type, Type, false) == 0) && e.ProgrammerExists())).FirstOrDefault();
                if (TypeEntry != null)
                    return TypeEntry.ProgrammerPath;
                else
                    return null;
            }

        }

        // Assumes phone with Flash protocol v2
        // Assumes phone is in flash mode
        internal async static Task LumiaV2FlashArchive(PhoneNotifierViewModel Notifier, string ArchivePath, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            LogFile.BeginAction("FlashCustomROM");

            NokiaFlashModel FlashModel = (NokiaFlashModel)Notifier.CurrentModel;

            // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
            // We need the fist sector if we want to write back the GPT.
            byte[] GPTChunk = GetGptChunk(FlashModel, 0x20000);
            GPT GPT = new GPT(GPTChunk);

            Partition Target;
            FlashPart Part;
            List<FlashPart> Parts = new List<FlashPart>();
            ulong MainOSOldSectorCount = 0;
            ulong MainOSNewSectorCount = 0;
            ulong DataOldSectorCount = 0;
            ulong DataNewSectorCount = 0;
            ulong FirstMainOSSector = 0;
            int PartitionCount = 0;
            ulong TotalSizeSectors = 0;
            bool IsUnlocked = false;
            bool GPTChanged = false;

            if (SetWorkingStatus == null) SetWorkingStatus = (m, s, v, a, st) => { };
            if (UpdateWorkingStatus == null) UpdateWorkingStatus = (m, s, v, st) => { };
            if (ExitSuccess == null) ExitSuccess = (m, s) => { };
            if (ExitSuccess == null) ExitSuccess = (m, s) => { };

            SetWorkingStatus("Initializing flash...", null, null, Status: WPinternalsStatus.Initializing);

            try
            {
                using (FileStream FileStream = new FileStream(ArchivePath, FileMode.Open))
                {
                    using (ZipArchive Archive = new ZipArchive(FileStream, ZipArchiveMode.Read))
                    {
                        // Determine if there is a partition layout present
                        ZipArchiveEntry PartitionEntry = Archive.GetEntry("Partitions.xml");
                        if (PartitionEntry == null)
                        {
                            GPT.MergePartitions(null, true, Archive);
                            GPTChanged |= GPT.HasChanged;
                        }
                        else
                        {
                            using (Stream ZipStream = PartitionEntry.Open())
                            {
                                using (StreamReader ZipReader = new StreamReader(ZipStream))
                                {
                                    string PartitionXml = ZipReader.ReadToEnd();
                                    GPT.MergePartitions(PartitionXml, true, Archive);
                                    GPTChanged |= GPT.HasChanged;
                                }
                            }
                        }

                        // First determine if we need a new GPT!
                        foreach (ZipArchiveEntry Entry in Archive.Entries)
                        {
                            if (!Entry.FullName.Contains("/")) // No subfolders
                            {
                                string PartitionName = Entry.Name;
                                int Pos = PartitionName.IndexOf('.');
                                if (Pos >= 0)
                                    PartitionName = PartitionName.Substring(0, Pos);

                                Partition Partition = GPT.Partitions.Where(p => string.Compare(p.Name, PartitionName, true) == 0).FirstOrDefault();
                                if (Partition != null)
                                {
                                    using (DecompressedStream DecompressedStream = new DecompressedStream(Entry.Open()))
                                    {
                                        ulong StreamLengthInSectors = (ulong)Entry.Length / 0x200;
                                        try
                                        {
                                            StreamLengthInSectors = (ulong)DecompressedStream.Length / 0x200;
                                        }
                                        catch { }

                                        TotalSizeSectors += StreamLengthInSectors;
                                        PartitionCount++;

                                        if (string.Compare(PartitionName, "MainOS", true) == 0)
                                        {
                                            MainOSOldSectorCount = Partition.SizeInSectors;
                                            MainOSNewSectorCount = StreamLengthInSectors;
                                            FirstMainOSSector = Partition.FirstSector;
                                        }
                                        else if (string.Compare(PartitionName, "Data", true) == 0)
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
                                        else if (string.Compare(PartitionName, "EFIESP", true) == 0)
                                        {
                                            ulong EfiespLength = StreamLengthInSectors * 0x200;
                                            byte[] EfiespBinary = new byte[EfiespLength];
                                            DecompressedStream.Read(EfiespBinary, 0, (int)EfiespLength);
                                            IsUnlocked = ((ByteOperations.ReadUInt32(EfiespBinary, 0x20) == (EfiespBinary.Length / 0x200 / 2)) && (ByteOperations.ReadAsciiString(EfiespBinary, (UInt32)(EfiespBinary.Length / 2) + 3, 8)) == "MSDOS5.0");
                                            if (IsUnlocked)
                                            {
                                                Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                                                if (IsUnlockedFlag == null)
                                                {
                                                    IsUnlockedFlag = new Partition();
                                                    IsUnlockedFlag.Name = "IS_UNLOCKED";
                                                    IsUnlockedFlag.Attributes = 0;
                                                    IsUnlockedFlag.PartitionGuid = Guid.NewGuid();
                                                    IsUnlockedFlag.PartitionTypeGuid = Guid.NewGuid();
                                                    IsUnlockedFlag.FirstSector = 0x40;
                                                    IsUnlockedFlag.LastSector = 0x40;
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
                                    Partition MainOSPartition = GPT.Partitions.Where(p => string.Compare(p.Name, "MainOS", true) == 0).Single();
                                    Partition DataPartition = GPT.Partitions.Where(p => string.Compare(p.Name, "Data", true) == 0).Single();
                                    MainOSPartition.LastSector = MainOSPartition.FirstSector + MainOSNewSectorCount - 1;
                                    DataPartition.FirstSector = MainOSPartition.LastSector + 1;
                                    if ((DataPartition.FirstSector % 0x100) > 0)
                                        DataPartition.FirstSector = ((UInt64)((DataPartition.FirstSector + 0x100) / 0x100)) * 0x100;
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
                            UEFI_BS_NV = new Partition();
                            UEFI_BS_NV.Name = "UEFI_BS_NV";
                            UEFI_BS_NV.Attributes = BACKUP_BS_NV.Attributes;
                            UEFI_BS_NV.PartitionGuid = OriginalPartitionGuid;
                            UEFI_BS_NV.PartitionTypeGuid = OriginalPartitionTypeGuid;
                            UEFI_BS_NV.FirstSector = BACKUP_BS_NV.LastSector + 1;
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

                        if (GPTChanged)
                        {
                            GPT.Rebuild();
                            Part = new FlashPart();
                            Part.StartSector = 0;
                            Part.Stream = new MemoryStream(GPTChunk);
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
                                        PartitionName = PartitionName.Substring(0, Pos);

                                    Target = GPT.Partitions.Where(p => string.Compare(p.Name, PartitionName, true) == 0).FirstOrDefault();
                                    if (Target != null)
                                    {
                                        Part = new FlashPart();
                                        Part.StartSector = (UInt32)Target.FirstSector;
                                        Part.Stream = new SeekableStream(() => new DecompressedStream(Entry.Open()), Entry.Length);
                                        Part.ProgressText = "Flashing partition " + Target.Name;
                                        Parts.Add(Part);
                                        LogFile.Log("Partition name=" + PartitionName + ", startsector=0x" + Target.FirstSector.ToString("X8") + ", sectorcount = 0x" + (Entry.Length / 0x200).ToString("X8"), LogType.FileOnly);
                                    }
                                }
                            }

                            Parts = Parts.OrderBy(p => p.StartSector).ToList();
                            int Count = 1;
                            Parts.Where(p => ((p.ProgressText != null) && p.ProgressText.StartsWith("Flashing partition "))).ToList().ForEach((p) =>
                            {
                                p.ProgressText += " (" + Count.ToString() + "/" + PartitionCount.ToString() + ")";
                                Count++;
                            });

                            // Do actual flashing!
                            await LumiaV2CustomFlash(Notifier, null, false, false, Parts, true, false, false, true, false, SetWorkingStatus, UpdateWorkingStatus, ExitSuccess, ExitFailure);
                        }
                        else
                        {
                            LogFile.Log("Flash failed! No valid partitions found in the archive.", LogType.FileAndConsole);
                            ExitFailure("Flash failed!", "No valid partitions found in the archive");
                            return;
                        }
                    }
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

            if (SetWorkingStatus == null) SetWorkingStatus = (m, s, v, a, st) => { };
            if (UpdateWorkingStatus == null) UpdateWorkingStatus = (m, s, v, st) => { };
            if (ExitSuccess == null) ExitSuccess = (m, s) => { };
            if (ExitFailure == null) ExitFailure = (m, s) => { };

            try
            {
                await SwitchModeViewModel.SwitchToWithProgress(Notifier, PhoneInterfaces.Lumia_MassStorage, (msg, sub) => SetWorkingStatus(msg, sub, null, Status: WPinternalsStatus.SwitchingMode));
                SetWorkingStatus("Applying patches...", null, null, Status: WPinternalsStatus.Patching);
                App.PatchEngine.TargetPath = ((MassStorage)Notifier.CurrentModel).Drive + "\\";
                bool PatchResult = App.PatchEngine.Patch("SecureBootHack-MainOS");
                if (!PatchResult)
                    throw new WPinternalsException("Patch failed");
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
        
        // Magic!
        // Assumes phone with Flash protocol v2
        // Assumes phone is in flash mode
        internal async static Task LumiaV2UnlockBootloader(PhoneNotifierViewModel Notifier, string ProfileFFUPath, string EDEPath, string SupportedFFUPath, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            LogFile.BeginAction("UnlockBootloader");
            NokiaFlashModel FlashModel = (NokiaFlashModel)Notifier.CurrentModel;

            if (SetWorkingStatus == null) SetWorkingStatus = (m, s, v, a, st) => { };
            if (UpdateWorkingStatus == null) UpdateWorkingStatus = (m, s, v, st) => { };
            if (ExitSuccess == null) ExitSuccess = (m, s) => { };
            if (ExitFailure == null) ExitFailure = (m, s) => { };

            try
            {
                PhoneInfo Info = FlashModel.ReadPhoneInfo();
                bool IsBootLoaderSecure = !Info.Authenticated && !Info.RdcPresent && Info.SecureFfuEnabled;

                if (ProfileFFUPath == null)
                    throw new ArgumentNullException("Profile FFU path is missing");

                FFU ProfileFFU = new FFU(ProfileFFUPath);

                if (IsBootLoaderSecure)
                {
                    if (!Info.PlatformID.StartsWith(ProfileFFU.PlatformID, StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentNullException("Profile FFU has wrong Platform ID for connected phone");
                }

                FFU SupportedFFU = null;
                if (App.PatchEngine.PatchDefinitions.Where(p => p.Name == "SecureBootHack-V2-EFIESP").First().TargetVersions.Any(v => v.Description == ProfileFFU.GetOSVersion()))
                    SupportedFFU = ProfileFFU;
                else if (SupportedFFUPath == null)
                    throw new ArgumentNullException("Donor-FFU with supported OS version was not provided");
                else
                {
                    SupportedFFU = new FFU(SupportedFFUPath);
                    if (!App.PatchEngine.PatchDefinitions.Where(p => p.Name == "SecureBootHack-V2-EFIESP").First().TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                        throw new ArgumentNullException("Donor-FFU with supported OS version was not provided");
                }

                // TODO: Check EDE file

                LogFile.Log("Assembling data for unlock", LogType.FileAndConsole);
                SetWorkingStatus("Assembling data for unlock", null, null);
                byte[] UnlockedEFIESP = ProfileFFU.GetPartition("EFIESP");

                DiscUtils.Fat.FatFileSystem UnlockedEFIESPFileSystem = new DiscUtils.Fat.FatFileSystem(new MemoryStream(UnlockedEFIESP));

                if (SupportedFFU.Path != ProfileFFU.Path)
                {
                    LogFile.Log("Donor-FFU: " + SupportedFFU.Path);
                    byte[] SupportedEFIESP = SupportedFFU.GetPartition("EFIESP");
                    DiscUtils.Fat.FatFileSystem SupportedEFIESPFileSystem = new DiscUtils.Fat.FatFileSystem(new MemoryStream(SupportedEFIESP));
                    DiscUtils.SparseStream SupportedMobileStartupStream = SupportedEFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Open);
                    MemoryStream SupportedMobileStartupMemStream = new MemoryStream();
                    SupportedMobileStartupStream.CopyTo(SupportedMobileStartupMemStream);
                    byte[] SupportedMobileStartup = SupportedMobileStartupMemStream.ToArray();
                    SupportedMobileStartupMemStream.Close();
                    SupportedMobileStartupStream.Close();

                    // Save supported mobilestartup.efi
                    LogFile.Log("Taking mobilestartup.efi from donor-FFU");
                    Stream MobileStartupStream = UnlockedEFIESPFileSystem.OpenFile(@"Windows\System32\Boot\mobilestartup.efi", FileMode.Create, FileAccess.Write);
                    MobileStartupStream.Write(SupportedMobileStartup, 0, SupportedMobileStartup.Length);
                    MobileStartupStream.Close();
                }

                // Magic!
                // This patch contains multiple hacks to disable SecureBoot, disable Bootpolicies and allow Mass Storage Mode on retail phones
                App.PatchEngine.TargetImage = UnlockedEFIESPFileSystem;
                bool PatchResult = App.PatchEngine.Patch("SecureBootHack-V2-EFIESP");
                if (!PatchResult)
                    throw new WPinternalsException("Failed to patch bootloader");

                // Edit BCD
                LogFile.Log("Edit BCD");
                using (Stream BCDFileStream = UnlockedEFIESPFileSystem.OpenFile(@"efi\Microsoft\Boot\BCD", FileMode.Open, FileAccess.ReadWrite))
                {
                    using (DiscUtils.Registry.RegistryHive BCDHive = new DiscUtils.Registry.RegistryHive(BCDFileStream))
                    {
                        DiscUtils.BootConfig.Store BCDStore = new DiscUtils.BootConfig.Store(BCDHive.Root);
                        DiscUtils.BootConfig.BcdObject MobileStartupObject = BCDStore.GetObject(new Guid("{01de5a27-8705-40db-bad6-96fa5187d4a6}"));
                        DiscUtils.BootConfig.Element NoCodeIntegrityElement = MobileStartupObject.GetElement(0x16000048);
                        if (NoCodeIntegrityElement != null)
                            NoCodeIntegrityElement.Value = DiscUtils.BootConfig.ElementValue.ForBoolean(true);
                        else
                            MobileStartupObject.AddElement(0x16000048, DiscUtils.BootConfig.ElementValue.ForBoolean(true));

                        DiscUtils.BootConfig.BcdObject WinLoadObject = BCDStore.GetObject(new Guid("{7619dcc9-fafe-11d9-b411-000476eba25f}"));
                        NoCodeIntegrityElement = WinLoadObject.GetElement(0x16000048);
                        if (NoCodeIntegrityElement != null)
                            NoCodeIntegrityElement.Value = DiscUtils.BootConfig.ElementValue.ForBoolean(true);
                        else
                            WinLoadObject.AddElement(0x16000048, DiscUtils.BootConfig.ElementValue.ForBoolean(true));
                    }
                }

                UnlockedEFIESPFileSystem.Dispose();

                List<FlashPart> Parts = new List<FlashPart>();
                FlashPart Part;
                GPT GPT = FlashModel.ReadGPT();

                // Create backup-partition for EFIESP
                byte[] GPTChunk = GetGptChunk(FlashModel, (UInt32)ProfileFFU.ChunkSize);
                byte[] GPTChunkBackup = new byte[GPTChunk.Length];
                Buffer.BlockCopy(GPTChunk, 0, GPTChunkBackup, 0, GPTChunk.Length);
                GPT = new GPT(GPTChunk);
                bool GPTChanged = false;
                Partition BACKUP_EFIESP = GPT.GetPartition("BACKUP_EFIESP");
                Partition EFIESP;
                UInt32 OriginalEfiespSizeInSectors = (UInt32)GPT.GetPartition("EFIESP").SizeInSectors;
                UInt32 OriginalEfiespLastSector = (UInt32)GPT.GetPartition("EFIESP").LastSector;
                if (BACKUP_EFIESP == null)
                {
                    BACKUP_EFIESP = GPT.GetPartition("EFIESP");
                    Guid OriginalPartitionTypeGuid = BACKUP_EFIESP.PartitionTypeGuid;
                    Guid OriginalPartitionGuid = BACKUP_EFIESP.PartitionGuid;
                    BACKUP_EFIESP.Name = "BACKUP_EFIESP";
                    BACKUP_EFIESP.LastSector = BACKUP_EFIESP.FirstSector + ((OriginalEfiespSizeInSectors) / 2) - 1; // Original is 0x10000
                    BACKUP_EFIESP.PartitionGuid = Guid.NewGuid();
                    BACKUP_EFIESP.PartitionTypeGuid = Guid.NewGuid();
                    EFIESP = new Partition();
                    EFIESP.Name = "EFIESP";
                    EFIESP.Attributes = BACKUP_EFIESP.Attributes;
                    EFIESP.PartitionGuid = OriginalPartitionGuid;
                    EFIESP.PartitionTypeGuid = OriginalPartitionTypeGuid;
                    EFIESP.FirstSector = BACKUP_EFIESP.LastSector + 1;
                    EFIESP.LastSector = EFIESP.FirstSector + ((OriginalEfiespSizeInSectors) / 2) - 1; // Original is 0x10000
                    GPT.Partitions.Add(EFIESP);
                    GPTChanged = true;
                }
                EFIESP = GPT.GetPartition("EFIESP");
                if ((UInt64)UnlockedEFIESP.Length > (EFIESP.SizeInSectors * 0x200))
                {
                    byte[] HalfEFIESP = new byte[EFIESP.SizeInSectors * 0x200];
                    Buffer.BlockCopy(UnlockedEFIESP, 0, HalfEFIESP, 0, HalfEFIESP.Length);
                    UnlockedEFIESP = HalfEFIESP;
                    ByteOperations.WriteUInt32(UnlockedEFIESP, 0x20, (UInt32)EFIESP.SizeInSectors); // Correction of partitionsize
                }

                Partition TargetPartition = GPT.GetPartition("EFIESP");
                if (TargetPartition == null)
                    throw new WPinternalsException("EFIESP partition not found!");

                if ((UInt64)UnlockedEFIESP.Length != (TargetPartition.SizeInSectors * 0x200))
                    throw new WPinternalsException("New EFIESP partition has wrong size. Size = 0x" + UnlockedEFIESP.Length.ToString("X8") + ". Expected size = 0x" + (TargetPartition.SizeInSectors * 0x200).ToString("X8"));

                Part = new FlashPart();
                Part.StartSector = (UInt32)TargetPartition.FirstSector; // GPT is prepared for 64-bit sector-offset, but flash app isn't.
                Part.Stream = new MemoryStream(UnlockedEFIESP);
                Part.ProgressText = "Flashing unlocked bootloader (part 1)...";
                Parts.Add(Part);

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
                    UEFI_BS_NV = new Partition();
                    UEFI_BS_NV.Name = "UEFI_BS_NV";
                    UEFI_BS_NV.Attributes = BACKUP_BS_NV.Attributes;
                    UEFI_BS_NV.PartitionGuid = OriginalPartitionGuid;
                    UEFI_BS_NV.PartitionTypeGuid = OriginalPartitionTypeGuid;
                    UEFI_BS_NV.FirstSector = BACKUP_BS_NV.LastSector + 1;
                    UEFI_BS_NV.LastSector = UEFI_BS_NV.FirstSector + BACKUP_BS_NV.LastSector - BACKUP_BS_NV.FirstSector;
                    GPT.Partitions.Add(UEFI_BS_NV);
                    GPTChanged = true;
                }
                Part = new FlashPart();
                TargetPartition = GPT.GetPartition("UEFI_BS_NV");
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

                if (GPTChanged)
                {
                    GPT.Rebuild();
                    Part = new FlashPart();
                    Part.StartSector = 0;
                    Part.Stream = new MemoryStream(GPTChunk);
                    Parts.Add(Part);
                }
				
                await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(Notifier, ProfileFFU.Path, false, false, Parts, true, false, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                    await Notifier.WaitForArrival();

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                    throw new WPinternalsException("Error: Phone is in wrong mode");

                // Not going to retry in a loop because a second attempt will result in gears due to changed BootOrder.
                // Just inform user of problem and revert.
                // User can try again after revert.
                bool IsPhoneInBadMassStorageMode = false;
                string ErrorMessage = null;
                try
                {
                    await SwitchModeViewModel.SwitchToWithStatus(Notifier, PhoneInterfaces.Lumia_MassStorage, SetWorkingStatus, UpdateWorkingStatus);
                }
                catch (WPinternalsException Ex)
                {
                    ErrorMessage = "Error: " + Ex.Message;
                    LogFile.LogException(Ex);
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                }

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_BadMassStorage)
                {
                    SetWorkingStatus("You need to manually reset your phone now!", "The phone is currently in Mass Storage Mode, but the driver of the PC failed to start. Unfortunately this happens sometimes. You need to manually reset the phone now. Keep the phone connected to the PC. Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates. Windows Phone Internals will automatically start to revert the changes. After the phone is fully booted again, you can retry to unlock the bootloader.", null, false, WPinternalsStatus.WaitingForManualReset);
                    await Notifier.WaitForArrival(); // Should be detected in Bootmanager mode
                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                        IsPhoneInBadMassStorageMode = true;
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                {
                    // Probably the "BootOrder" prevents to boot to MobileStartup. Mass Storage mode depends on MobileStartup.
                    // In this case Bootarm boots straight to Winload. But Winload can't handle the change of the EFIESP partition. That will cause a bootloop.

                    SetWorkingStatus("Problem detected, rolling back...", ErrorMessage);
                    await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                    Parts = new List<FlashPart>();

                    // Restore original GPT, which will also reference the original NV.
                    Part = new FlashPart();
                    Part.StartSector = 0;
                    Part.Stream = new MemoryStream(GPTChunkBackup);
                    Parts.Add(Part);

                    await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(Notifier, ProfileFFU.Path, false, false, Parts, true, false, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);

                    // An old NV backup was restored and it possibly contained the IsFlashing flag.
                    // Can't clear it immeadiately, so we need another flash.
                    if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                        await Notifier.WaitForArrival();

                    if ((Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader) || (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash))
                    {
                        await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(Notifier, ProfileFFU.Path, false, false, null, true, true, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);
                    }

                    if (IsPhoneInBadMassStorageMode)
                        ExitFailure("Failed to unlock the bootloader due to misbahaving driver. Wait for phone to boot to Windows and then try again.", "The Mass Storage driver of the PC failed to start. Unfortunately this happens sometimes. After the phone is fully booted again, you can retry to unlock the bootloader.");
                    else
                        ExitFailure("Failed to unlock the bootloader", "It is not possible to unlock the bootloader straight after flashing. NOTE: Fully reboot the phone and then properly shutdown the phone, before you can try to unlock again!");

                    return;
                }

                SetWorkingStatus("Create backup partition...", null, null);

                MassStorage MassStorage = (MassStorage)Notifier.CurrentModel;
                GPTChunk = MassStorage.ReadSectors(0, 0x100);
                GPT = new GPT(GPTChunk);
                BACKUP_EFIESP = GPT.GetPartition("BACKUP_EFIESP");
                byte[] BackupEFIESP = MassStorage.ReadSectors(BACKUP_EFIESP.FirstSector, BACKUP_EFIESP.SizeInSectors);

                LogFile.Log("Unlocking backup partition", LogType.FileAndConsole);
                SetWorkingStatus("Unlocking backup partition", null, null);

                // Copy the backed up unlocked EFIESP for future use
                byte[] BackupUnlockedEFIESP = new byte[UnlockedEFIESP.Length];
                Buffer.BlockCopy(BackupEFIESP, 0, BackupUnlockedEFIESP, 0, BackupEFIESP.Length);

                DiscUtils.Fat.FatFileSystem UnlockedBackedEFIESPFileSystem = new DiscUtils.Fat.FatFileSystem(new MemoryStream(BackupUnlockedEFIESP));
                
                // Magic!
                // This patch contains multiple hacks to disable SecureBoot, disable Bootpolicies and allow Mass Storage Mode on retail phones
                App.PatchEngine.TargetImage = UnlockedBackedEFIESPFileSystem;
                PatchResult = App.PatchEngine.Patch("SecureBootHack-V2-EFIESP");

                // The patch to mobilestartup failed, get a new mobilestartup from the donor FFU instead
                if (!PatchResult)
                {
                    LogFile.Log("Donor-FFU: " + SupportedFFU.Path);
                    byte[] SupportedEFIESP = SupportedFFU.GetPartition("EFIESP");
                    DiscUtils.Fat.FatFileSystem SupportedEFIESPFileSystem = new DiscUtils.Fat.FatFileSystem(new MemoryStream(SupportedEFIESP));
                    DiscUtils.SparseStream SupportedMobileStartupStream = SupportedEFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Open);
                    MemoryStream SupportedMobileStartupMemStream = new MemoryStream();
                    SupportedMobileStartupStream.CopyTo(SupportedMobileStartupMemStream);
                    byte[] SupportedMobileStartup = SupportedMobileStartupMemStream.ToArray();
                    SupportedMobileStartupMemStream.Close();
                    SupportedMobileStartupStream.Close();

                    // Save supported mobilestartup.efi
                    LogFile.Log("Taking mobilestartup.efi from donor-FFU");
                    Stream MobileStartupStream = UnlockedBackedEFIESPFileSystem.OpenFile(@"Windows\System32\Boot\mobilestartup.efi", FileMode.Create, FileAccess.Write);
                    MobileStartupStream.Write(SupportedMobileStartup, 0, SupportedMobileStartup.Length);
                    MobileStartupStream.Close();

                    App.PatchEngine.TargetImage = UnlockedBackedEFIESPFileSystem;
                    PatchResult = App.PatchEngine.Patch("SecureBootHack-V2-EFIESP");

                    // We shouldn't be there
                    if (!PatchResult)
                        throw new WPinternalsException("Failed to patch bootloader");
                }

                // Edit BCD
                LogFile.Log("Edit BCD");
                using (Stream BCDFileStream = UnlockedBackedEFIESPFileSystem.OpenFile(@"efi\Microsoft\Boot\BCD", FileMode.Open, FileAccess.ReadWrite))
                {
                    using (DiscUtils.Registry.RegistryHive BCDHive = new DiscUtils.Registry.RegistryHive(BCDFileStream))
                    {
                        DiscUtils.BootConfig.Store BCDStore = new DiscUtils.BootConfig.Store(BCDHive.Root);
                        DiscUtils.BootConfig.BcdObject MobileStartupObject = BCDStore.GetObject(new Guid("{01de5a27-8705-40db-bad6-96fa5187d4a6}"));
                        DiscUtils.BootConfig.Element NoCodeIntegrityElement = MobileStartupObject.GetElement(0x16000048);
                        if (NoCodeIntegrityElement != null)
                            NoCodeIntegrityElement.Value = DiscUtils.BootConfig.ElementValue.ForBoolean(true);
                        else
                            MobileStartupObject.AddElement(0x16000048, DiscUtils.BootConfig.ElementValue.ForBoolean(true));

                        DiscUtils.BootConfig.BcdObject WinLoadObject = BCDStore.GetObject(new Guid("{7619dcc9-fafe-11d9-b411-000476eba25f}"));
                        NoCodeIntegrityElement = WinLoadObject.GetElement(0x16000048);
                        if (NoCodeIntegrityElement != null)
                            NoCodeIntegrityElement.Value = DiscUtils.BootConfig.ElementValue.ForBoolean(true);
                        else
                            WinLoadObject.AddElement(0x16000048, DiscUtils.BootConfig.ElementValue.ForBoolean(true));
                    }
                }

                UnlockedBackedEFIESPFileSystem.Dispose();

                SetWorkingStatus("Boot optimization...", null, null);

                App.PatchEngine.TargetPath = MassStorage.Drive + "\\";
                App.PatchEngine.Patch("SecureBootHack-MainOS"); // Don't care about result here. Some phones do not need this.

                LogFile.Log("The phone is currently in Mass Storage Mode", LogType.ConsoleOnly);
                LogFile.Log("To continue the unlock-sequence, the phone needs to be rebooted", LogType.ConsoleOnly);
                LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                SetWorkingStatus("You need to manually reset your phone now!", "The phone is currently in Mass Storage Mode. To continue the unlock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates. The unlock-sequence will resume automatically.", null, false, WPinternalsStatus.WaitingForManualReset);

                await Notifier.WaitForRemoval();

                SetWorkingStatus("Rebooting phone...");

                await Notifier.WaitForArrival();
                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    throw new WPinternalsException("Phone is in wrong mode");

                ((NokiaFlashModel)Notifier.CurrentModel).SwitchToFlashAppContext();

                // EFIESP is appended at the end of the GPT
                // BACKUP_EFIESP is at original location in GPT
                EFIESP = GPT.GetPartition("EFIESP");
                UInt32 OriginalEfiespFirstSector = (UInt32)BACKUP_EFIESP.FirstSector;
                BACKUP_EFIESP.Name = "EFIESP";
                BACKUP_EFIESP.LastSector = OriginalEfiespLastSector; // Do not hardcode the length of the partition, some phones have bigger EFIESP partitions than others.
                BACKUP_EFIESP.PartitionGuid = EFIESP.PartitionGuid;
                BACKUP_EFIESP.PartitionTypeGuid = EFIESP.PartitionTypeGuid;
                GPT.Partitions.Remove(EFIESP);

                Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                if (IsUnlockedFlag == null)
                {
                    IsUnlockedFlag = new Partition();
                    IsUnlockedFlag.Name = "IS_UNLOCKED";
                    IsUnlockedFlag.Attributes = 0;
                    IsUnlockedFlag.PartitionGuid = Guid.NewGuid();
                    IsUnlockedFlag.PartitionTypeGuid = Guid.NewGuid();
                    IsUnlockedFlag.FirstSector = 0x40;
                    IsUnlockedFlag.LastSector = 0x40;
                    GPT.Partitions.Add(IsUnlockedFlag);
                }

                Parts = new List<FlashPart>();
                GPT.Rebuild();
                Part = new FlashPart();
                Part.StartSector = 0;
                Part.Stream = new MemoryStream(GPTChunk);
                Part.ProgressText = "Flashing unlocked bootloader (part 2)...";
                Parts.Add(Part);
                Part = new FlashPart();
                Part.StartSector = OriginalEfiespFirstSector;
                Part.Stream = new MemoryStream(BackupUnlockedEFIESP); // We must keep the Oiriginal EFIESP, but unlocked, for many reasons
                Parts.Add(Part);
                Part = new FlashPart();
                Part.StartSector = OriginalEfiespFirstSector + ((OriginalEfiespSizeInSectors) / 2);
                Part.Stream = new MemoryStream(BackupEFIESP);
                Parts.Add(Part);
                
                await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(Notifier, ProfileFFU.Path, false, false, Parts, true, true, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);

                LogFile.Log("Bootloader unlocked!", LogType.FileAndConsole);
                ExitSuccess("Bootloader unlocked successfully!", null);
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                ExitFailure(Ex.Message, Ex is WPinternalsException ? ((WPinternalsException)Ex).SubMessage : null);
            }
            LogFile.EndAction("UnlockBootloader");
        }

        // Assumes phone with Flash protocol v2
        // Assumes phone is in flash mode
        internal async static Task LumiaV2FlashPartitions(PhoneNotifierViewModel Notifier, string EFIESPPath, string MainOSPath, string DataPath, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            NokiaFlashModel FlashModel = (NokiaFlashModel)Notifier.CurrentModel;

            // Use GetGptChunk() here instead of ReadGPT(), because ReadGPT() skips the first sector.
            // We need the fist sector if we want to write back the GPT.
            byte[] GPTChunk = GetGptChunk(FlashModel, 0x20000);
            GPT GPT = new GPT(GPTChunk);

            Partition Target;
            FlashPart Part;
            List<FlashPart> Parts = new List<FlashPart>();
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

            if (SetWorkingStatus == null) SetWorkingStatus = (m, s, v, a, st) => { };
            if (UpdateWorkingStatus == null) UpdateWorkingStatus = (m, s, v, st) => { };
            if (ExitSuccess == null) ExitSuccess = (m, s) => { };
            if (ExitFailure == null) ExitFailure = (m, s) => { };

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
                    IsUnlocked = ((ByteOperations.ReadUInt32(EfiespBinary, 0x20) == (EfiespBinary.Length / 0x200 / 2)) && (ByteOperations.ReadAsciiString(EfiespBinary, (UInt32)(EfiespBinary.Length / 2) + 3, 8)) == "MSDOS5.0");
                    if (IsUnlocked)
                    {
                        Partition IsUnlockedFlag = GPT.GetPartition("IS_UNLOCKED");
                        if (IsUnlockedFlag == null)
                        {
                            IsUnlockedFlag = new Partition();
                            IsUnlockedFlag.Name = "IS_UNLOCKED";
                            IsUnlockedFlag.Attributes = 0;
                            IsUnlockedFlag.PartitionGuid = Guid.NewGuid();
                            IsUnlockedFlag.PartitionTypeGuid = Guid.NewGuid();
                            IsUnlockedFlag.FirstSector = 0x40;
                            IsUnlockedFlag.LastSector = 0x40;
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
                                Partition MainOSPartition = GPT.Partitions.Where(p => string.Compare(p.Name, "MainOS", true) == 0).Single();
                                Partition DataPartition = GPT.Partitions.Where(p => string.Compare(p.Name, "Data", true) == 0).Single();
                                MainOSPartition.LastSector = MainOSPartition.FirstSector + MainOSNewSectorCount - 1;
                                DataPartition.FirstSector = MainOSPartition.LastSector + 1;
                                if ((DataPartition.FirstSector % 0x100) > 0)
                                    DataPartition.FirstSector = ((UInt64)((DataPartition.FirstSector + 0x100) / 0x100)) * 0x100;
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
                        UEFI_BS_NV = new Partition();
                        UEFI_BS_NV.Name = "UEFI_BS_NV";
                        UEFI_BS_NV.Attributes = BACKUP_BS_NV.Attributes;
                        UEFI_BS_NV.PartitionGuid = OriginalPartitionGuid;
                        UEFI_BS_NV.PartitionTypeGuid = OriginalPartitionTypeGuid;
                        UEFI_BS_NV.FirstSector = BACKUP_BS_NV.LastSector + 1;
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

                    if (GPTChanged)
                    {
                        GPT.Rebuild();
                        Part = new FlashPart();
                        Part.StartSector = 0;
                        Part.Stream = new MemoryStream(GPTChunk);
                        Parts.Add(Part);
                    }

                    int Count = 0;

                    Target = GPT.Partitions.Where(p => string.Compare(p.Name, "EFIESP", true) == 0).FirstOrDefault();
                    if ((EFIESPPath != null) && (Target != null))
                    {
                        Count++;
                        Part = new FlashPart();
                        Part.StartSector = (UInt32)Target.FirstSector;
                        Part.Stream = new FileStream(EFIESPPath, FileMode.Open);
                        Part.ProgressText = "Flashing partition EFIESP (" + Count.ToString() + " / " + PartitionCount.ToString() + ")";
                        Parts.Add(Part);
                        LogFile.Log("Partition name=EFIESP, startsector=0x" + Target.FirstSector.ToString("X8") + ", sectorcount = 0x" + (Part.Stream.Length / 0x200).ToString("X8"), LogType.FileOnly);
                    }

                    Target = GPT.Partitions.Where(p => string.Compare(p.Name, "MainOS", true) == 0).FirstOrDefault();
                    if ((MainOSPath != null) && (Target != null))
                    {
                        Count++;
                        Part = new FlashPart();
                        Part.StartSector = (UInt32)Target.FirstSector;
                        Part.Stream = new FileStream(MainOSPath, FileMode.Open);
                        Part.ProgressText = "Flashing partition MainOS (" + Count.ToString() + " / " + PartitionCount.ToString() + ")";
                        Parts.Add(Part);
                        LogFile.Log("Partition name=MainOS, startsector=0x" + Target.FirstSector.ToString("X8") + ", sectorcount = 0x" + (Part.Stream.Length / 0x200).ToString("X8"), LogType.FileOnly);
                    }

                    Target = GPT.Partitions.Where(p => string.Compare(p.Name, "Data", true) == 0).FirstOrDefault();
                    if ((DataPath != null) && (Target != null))
                    {
                        Count++;
                        Part = new FlashPart();
                        Part.StartSector = (UInt32)Target.FirstSector;
                        Part.Stream = new FileStream(DataPath, FileMode.Open);
                        Part.ProgressText = "Flashing partition Data (" + Count.ToString() + " / " + PartitionCount.ToString() + ")";
                        Parts.Add(Part);
                        LogFile.Log("Partition name=Data, startsector=0x" + Target.FirstSector.ToString("X8") + ", sectorcount = 0x" + (Part.Stream.Length / 0x200).ToString("X8"), LogType.FileOnly);
                    }

                    // Do actual flashing!
                    await LumiaV2CustomFlash(Notifier, null, false, false, Parts, true, false, false, true, false, SetWorkingStatus, UpdateWorkingStatus, ExitSuccess, ExitFailure);
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
        private static List<Allocation> Allocations = new List<Allocation>();
        private static List<FreeMemRange> FreeMemRanges = new List<FreeMemRange>();

        public static UInt32 RoundUpToPages(UInt32 Size)
        {
            UInt32 Result = Size + 0x18;
            if ((Size % PageSize) != 0)
                Size = ((Size / PageSize) + 1) * PageSize;
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
                System.Buffer.BlockCopy(Buffer, 0, NewBuffer, (int)Size, (int)CurrentBufferSize);
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
                for (int i = FreeMemRanges.Count() - 1; i >= 0; i--)
                {
                    if (FreeMemRanges[i].Size >= TotalSize)
                    {
                        NewAllocation = new Allocation();
                        NewAllocation.TotalStart = FreeMemRanges[i].End - TotalSize + 1;

                        if (FreeMemRanges[i].Size == TotalSize)
                            FreeMemRanges.RemoveAt(i);
                        else
                            FreeMemRanges[i].End -= TotalSize;

                        break;
                    }
                }

                if (NewAllocation == null)
                {
                    UInt32 FreeBuffer;

                    if (Allocations.Count() > 0)
                        FreeBuffer = Allocations[0].TotalStart;
                    else
                        FreeBuffer = CurrentBufferSize;

                    if (FreeBuffer < TotalSize)
                        ExtendBuffer(TotalSize - FreeBuffer);

                    NewAllocation = new Allocation();

                    if (Allocations.Count() > 0)
                        NewAllocation.TotalStart = Allocations[0].TotalStart - TotalSize;
                    else
                        FreeBuffer = CurrentBufferSize - TotalSize;
                }

                bool Added = false;
                for (int i = 0; i < Allocations.Count(); i++)
                {
                    if (NewAllocation.TotalStart < Allocations[i].TotalStart)
                    {
                        Allocations.Insert(i, NewAllocation);
                        Added = true;
                        break;
                    }
                }
                if (!Added)
                    Allocations.Add(NewAllocation);
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
                    TotalSize = ((TotalSize / PageSize) + 1) * PageSize;

                for (int i = FreeMemRanges.Count() - 1; i >= 0; i--)
                {
                    if (FreeMemRanges[i].Size >= TotalSize)
                    {
                        NewAllocation = new Allocation();
                        NewAllocation.TotalStart = FreeMemRanges[i].End - TotalSize + 1;

                        if (FreeMemRanges[i].Size == TotalSize)
                            FreeMemRanges.RemoveAt(i);
                        else
                            FreeMemRanges[i].End -= TotalSize;

                        break;
                    }
                }

                if (NewAllocation == null)
                {
                    UInt32 FreeBuffer;

                    if (Allocations.Count() > 0)
                        FreeBuffer = Allocations[0].TotalStart;
                    else
                        FreeBuffer = CurrentBufferSize;

                    if (FreeBuffer < TotalSize)
                        ExtendBuffer(TotalSize - FreeBuffer);

                    NewAllocation = new Allocation();

                    if (Allocations.Count() > 0)
                        NewAllocation.TotalStart = Allocations[0].TotalStart - TotalSize;
                    else
                        FreeBuffer = CurrentBufferSize - TotalSize;
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
                for (int i = 0; i < Allocations.Count(); i++)
                {
                    if (NewAllocation.TotalStart < Allocations[i].TotalStart)
                    {
                        Allocations.Insert(i, NewAllocation);
                        Added = true;
                        break;
                    }
                }
                if (!Added)
                    Allocations.Add(NewAllocation);
            }

            return NewAllocation;
        }

        internal static void FreePool(Allocation Allocation)
        {
            if (Allocations.Contains(Allocation))
            {
                Allocations.Remove(Allocation);

                if (Allocations.Count() == 0)
                {
                    FreeMemRanges.Clear();
                }
                else
                {
                    FreeMemRange NewFreeRange = new FreeMemRange();
                    NewFreeRange.Start = Allocation.TotalStart;
                    NewFreeRange.End = Allocation.TotalEnd;

                    bool Added = false;
                    int i;
                    for (i = 0; i < FreeMemRanges.Count(); i++)
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
                        i = FreeMemRanges.Count();
                    }

                    if ((i > 0) && (FreeMemRanges[i].Start == (FreeMemRanges[i - 1].End + 1)))
                    {
                        FreeMemRanges[i - 1].End = FreeMemRanges[i].End;
                        FreeMemRanges.RemoveAt(i);
                        i--;
                    }

                    if ((i < (FreeMemRanges.Count() - 1)) && (FreeMemRanges[i].End == (FreeMemRanges[i - 1].Start - 1)))
                    {
                        FreeMemRanges[i].End = FreeMemRanges[i + 1].End;
                        FreeMemRanges.RemoveAt(i + 1);
                    }

                    if ((Allocations.Count() > 0) && (FreeMemRanges[i].Start < Allocations[0].TotalStart))
                        FreeMemRanges.RemoveAt(i);
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
