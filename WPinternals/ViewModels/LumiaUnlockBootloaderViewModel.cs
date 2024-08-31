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

//#define DUMPPARTITIONS

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WPinternals
{
    internal static class LumiaUnlockBootloaderViewModel
    {
        // TODO: Add logging
        private static void PerformSoftBrick(PhoneNotifierViewModel Notifier, FFU FFU)
        {
            LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)Notifier.CurrentModel;

            // Send FFU headers
            UInt64 CombinedFFUHeaderSize = FFU.HeaderSize;
            byte[] FfuHeader = new byte[CombinedFFUHeaderSize];
            FileStream FfuFile = new(FFU.Path, FileMode.Open, FileAccess.Read);
            FfuFile.Read(FfuHeader, 0, (int)CombinedFFUHeaderSize);
            FfuFile.Close();

            FlashModel.SendFfuHeaderV1(FfuHeader);

            // Send 1 empty chunk (according to layout in FFU headers, it will be written to first and last chunk)
            byte[] EmptyChunk = new byte[0x20000];
            Array.Clear(EmptyChunk, 0, 0x20000);
            FlashModel.SendFfuPayloadV1(EmptyChunk);

            // Reboot to Qualcomm Emergency mode
            byte[] RebootCommand = [0x4E, 0x4F, 0x4B, 0x52]; // NOKR
            FlashModel.ExecuteRawVoidMethod(RebootCommand);
        }

        private static void SendLoader(PhoneNotifierViewModel PhoneNotifier, List<QualcommPartition> PossibleLoaders)
        {
            // Assume 9008 mode
            if (!((PhoneNotifier.CurrentModel is QualcommSerial) && (PossibleLoaders?.Count > 0)))
            {
                return;
            }

            LogFile.Log("Sending loader");

            QualcommSerial Serial = (QualcommSerial)PhoneNotifier.CurrentModel;
            QualcommDownload Download = new(Serial);
            if (Download.IsAlive())
            {
                int Attempt = 1;
                bool Result = false;
                foreach (QualcommPartition Loader in PossibleLoaders)
                {
                    LogFile.Log("Attempt " + Attempt.ToString());

                    try
                    {
                        Download.SendToPhoneMemory(0x2A000000, Loader.Binary);
                        Download.StartBootloader(0x2A000000);
                        Result = true;
                        LogFile.Log("Loader sent successfully");
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }

                    if (Result)
                    {
                        break;
                    }

                    Attempt++;
                }
                Serial.Close();

                if (!Result)
                {
                    LogFile.Log("Loader failed");
                }
            }
            else
            {
                LogFile.Log("Failed to communicate to Qualcomm Emergency Download mode");
                throw new BadConnectionException();
            }
        }

        internal static async Task LumiaV2RelockUEFI(PhoneNotifierViewModel Notifier, string FFUPath = null, bool DoResetFirst = true, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
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

            void tmpExitFailure(string m, string s = null) => ExitFailure(m, s);

            async void tmpExitSuccess(string m, string s = null)
            {
                SetWorkingStatus("Booting phone...");
                await Notifier.WaitForArrival();

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                {
                    await Notifier.WaitForArrival();
                }

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                {
                    await Notifier.WaitForArrival();
                }

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                {
                    ExitFailure("Failed to relock phone", "Your phone is half relocked. You may need to reflash a stock ROM");
                    return;
                }

                ExitSuccess("Bootloader restored successfully!");
            }

            await LumiaRelockUEFI(Notifier, FFUPath, DoResetFirst, SetWorkingStatus, UpdateWorkingStatus, tmpExitSuccess, tmpExitFailure);
        }

        internal static async Task LumiaV2UnlockUEFI(PhoneNotifierViewModel Notifier, string ProfileFFUPath, string EDEPath, string SupportedFFUPath, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null, bool ReUnlockDevice = false)
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

            void tmpExitFailure(string m, string s = null) => ExitFailure(m, s);

            async void tmpExitSuccess(string m, string s = null)
            {
                SetWorkingStatus("Booting phone...");
                await Notifier.WaitForArrival();

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                {
                    await Notifier.WaitForArrival();
                }

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                {
                    await Notifier.WaitForArrival();
                }

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                {
                    ExitFailure("Failed to unlock phone", "Your phone is half unlocked. You may need to reflash a stock ROM");
                    return;
                }

                ExitSuccess("Bootloader unlocked successfully!", null);
            }

            await LumiaUnlockUEFI(Notifier, ProfileFFUPath, EDEPath, SupportedFFUPath, SetWorkingStatus, UpdateWorkingStatus, tmpExitSuccess, tmpExitFailure, ReUnlockDevice: ReUnlockDevice);
        }

        // Magic!
        // Platform Secure Boot Hack for Spec A devices
        //
        // Assumes phone in Flash mode
        //               in Qualcomm Dload
        //               in Qualcomm Flash
        //
        internal static async Task LumiaV1RelockFirmware(PhoneNotifierViewModel Notifier, string FFUPath, string LoadersPath, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            LogFile.BeginAction("RelockBootloader");

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
                if (Notifier.CurrentModel is LumiaFlashAppModel)
                {
                    await LumiaRelockUEFI(Notifier, FFUPath, true, SetWorkingStatus, UpdateWorkingStatus, null, (string Message, string SubMessage) =>
                    {
                        ExitFailure(Message, SubMessage);
                        LogFile.EndAction("RelockBootloader");
                        return;
                    });

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader && Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                    {
                        await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                    }
                }

                LogFile.Log("Assembling data for relock", LogType.FileAndConsole);
                SetWorkingStatus("Assembling data for relock", null, null);

                if (string.IsNullOrEmpty(FFUPath))
                {
                    throw new ArgumentNullException("FFU path is missing");
                }

                if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download && string.IsNullOrEmpty(LoadersPath))
                {
                    throw new Exception("Error: Path for Loaders is mandatory.");
                }

                string DumpFilePrefix = Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\") + DateTime.Now.ToString("yyyy-MM-dd hh.mm.ss") + " - ";
                bool IsBootLoaderUnlocked = false;

                FFU FFU = null;
                try
                {
                    FFU = new FFU(FFUPath);
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing FFU-file failed.");
                }

                if (Notifier.CurrentModel is LumiaFlashAppModel)
                {
                    FlashVersion FlashVersion = ((LumiaFlashAppModel)Notifier.CurrentModel).GetFlashVersion();
                    if (FlashVersion == null)
                    {
                        throw new Exception("Error: The version of the Flash Application on the phone could not be determined.");
                    }

                    if ((FlashVersion.ApplicationMajor < 1) || ((FlashVersion.ApplicationMajor == 1) && (FlashVersion.ApplicationMinor < 28)))
                    {
                        throw new Exception("Error: The version of the Flash Application on the phone is too old. Update your phone using Windows Updates or flash a newer ROM to your phone. Then try again.");
                    }

                    UefiSecurityStatusResponse SecurityStatus = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadSecurityStatus();
                    IsBootLoaderUnlocked = SecurityStatus.AuthenticationStatus || SecurityStatus.RdcStatus || !SecurityStatus.SecureFfuEfuseStatus;
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "01.bin", FFU.GetSectors(0, 34)); // Original GPT
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                GPT NewGPT = null;
                if (Notifier.CurrentModel is LumiaFlashAppModel)
                {
                    ((LumiaFlashAppModel)Notifier.CurrentModel).ResetPhone();

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    {
                        throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in bootloader mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                    }

                    NewGPT = ((LumiaBootManagerAppModel)Notifier.CurrentModel).ReadGPT();

                    await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                    }
                }
                else
                {
                    NewGPT = FFU.GPT;
                }

                // Make sure all partitions are in range of the emergency flasher.
                NewGPT.RestoreBackupPartitions();

                byte[] GPT = null;
                try
                {
                    NewGPT.RemoveHack();
                    GPT = NewGPT.Rebuild();
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing partitions failed.");
                }

                Partition IsUnlockedFlag = NewGPT.GetPartition("IS_UNLOCKED_SBL3");
                if (IsUnlockedFlag != null)
                {
                    NewGPT.Partitions.Remove(IsUnlockedFlag);
                    GPT = NewGPT.Rebuild();
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "02.bin", GPT); // Patched GPT
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                SBL1 SBL1 = null;
                try
                {
                    SBL1 = new SBL1(FFU.GetPartition("SBL1"));
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing SBL1 failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "03.bin", SBL1.Binary); // Original SBL1
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                byte[] RootKeyHash = null;
                if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                {
                    QualcommDownload Download = new((QualcommSerial)Notifier.CurrentModel);
                    RootKeyHash = Download.GetRKH();
                }
                else if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Flash)
                {
                    RootKeyHash = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadParam("RRKH");
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Flash)
                {
                    if (RootKeyHash == null)
                    {
                        throw new Exception("Error: Root Key Hash could not be retrieved from the phone.");
                    }

                    // Make sure the RootKeyHash is not blank
                    // If the RootKeyHash is blank, this is an engineering device, and it will accept any RKH
                    // We expect the user to know what he is doing in such case and we will ignore checks
                    if (!StructuralComparisons.StructuralEqualityComparer.Equals(RootKeyHash, new byte[RootKeyHash.Length]))
                    {
                        if (SBL1.RootKeyHash == null)
                        {
                            throw new Exception("Error: Root Key Hash could not be retrieved from FFU file.");
                        }
                        if (!StructuralComparisons.StructuralEqualityComparer.Equals(RootKeyHash, SBL1.RootKeyHash))
                        {
                            LogFile.Log("Phone: " + Converter.ConvertHexToString(RootKeyHash, ""));
                            LogFile.Log("SBL1: " + Converter.ConvertHexToString(SBL1.RootKeyHash, ""));
                            throw new Exception("Error: Root Key Hash from phone and from FFU file do not match!");
                        }
                    }
                }

                SBL2 SBL2Partition = null;
                try
                {
                    SBL2Partition = new SBL2(FFU.GetPartition("SBL2"));
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing SBL2 failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "05.bin", SBL2Partition.Binary); // Original SBL2
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                byte[] SBL2 = SBL2Partition.Binary;

                SBL3 SBL3Partition;

                try
                {
                    SBL3Partition = new SBL3(FFU.GetPartition("SBL3"));
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing SBL3 from FFU failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "07.bin", SBL3Partition.Binary); // Original SBL3
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                byte[] SBL3 = SBL3Partition.Binary;

                UEFI UEFIPartition = null;
                try
                {
                    UEFIPartition = new UEFI(FFU.GetPartition("UEFI"));
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing UEFI failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "09.bin", UEFIPartition.Binary); // Original UEFI
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                byte[] UEFI = UEFIPartition.Binary;

                List<QualcommPartition> PossibleLoaders = null;
                if (!IsBootLoaderUnlocked || Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                {
                    try
                    {
                        PossibleLoaders = QualcommLoaders.GetPossibleLoadersForRootKeyHash(LoadersPath, RootKeyHash);
                        if (PossibleLoaders.Count == 0)
                        {
                            throw new Exception("Error: No matching loaders found for RootKeyHash.");
                        }
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Unexpected error during scanning for loaders.");
                    }
                }

                if (IsBootLoaderUnlocked)
                // Flash phone in Flash app
                {
                    LumiaFlashAppModel CurrentModel = (LumiaFlashAppModel)Notifier.CurrentModel;
                    LogFile.Log("Start flashing in Custom Flash mode");

                    UInt64 TotalSectorCount = (UInt64)0x21 + 1 +
                        (UInt64)(SBL2.Length / 0x200) +
                        (UInt64)(SBL3.Length / 0x200) +
                        (UInt64)(UEFI.Length / 0x200);

                    SetWorkingStatus("Flashing original bootloader...", MaxProgressValue: 100, Status: WPinternalsStatus.Flashing);
                    ProgressUpdater Progress = new(TotalSectorCount, (int ProgressPercentage, TimeSpan? TimeSpan) => UpdateWorkingStatus("Flashing original bootloader...", CurrentProgressValue: (ulong)ProgressPercentage, Status: WPinternalsStatus.Flashing));

                    LogFile.Log("Flash GPT at 0x" + ((UInt32)0x200).ToString("X8"));
                    CurrentModel.FlashSectors(1, GPT, 0);
                    Progress.SetProgress(0x21);

                    LogFile.Log("Flash SBL2 at 0x" + ((UInt32)NewGPT.GetPartition("SBL2").FirstSector * 0x200).ToString("X8"));
                    CurrentModel.FlashRawPartition(SBL2, "SBL2", Progress);
                    LogFile.Log("Flash SBL3 at 0x" + ((UInt32)NewGPT.GetPartition("SBL3").FirstSector * 0x200).ToString("X8"));
                    CurrentModel.FlashRawPartition(SBL3, "SBL3", Progress);
                    LogFile.Log("Flash UEFI at 0x" + ((UInt32)NewGPT.GetPartition("UEFI").FirstSector * 0x200).ToString("X8"));
                    CurrentModel.FlashRawPartition(UEFI, "UEFI", Progress);

                    // phone is in flash mode, we can exit
                }
                else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash || Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download || Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Flash)
                {
                    // Switch to DLOAD
                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash)
                    {
                        SetWorkingStatus("Switching to Emergency Download mode...");
                        PerformSoftBrick(Notifier, FFU);
                        if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Download)
                        {
                            await Notifier.WaitForArrival();
                        }

                        if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Download)
                        {
                            throw new WPinternalsException("Phone failed to switch to emergency download mode.");
                        }
                    }

                    // Send loader
                    if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                    {
                        SetWorkingStatus("Sending loader...");
                        SendLoader(Notifier, PossibleLoaders);
                        if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Flash)
                        {
                            await Notifier.WaitForArrival();
                        }

                        if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Flash)
                        {
                            throw new WPinternalsException("Phone failed to switch to emergency flash mode.");
                        }
                    }

                    // Flash bootloader
                    QualcommSerial Serial = (QualcommSerial)Notifier.CurrentModel;
                    Serial.EncodeCommands = false;

                    QualcommFlasher Flasher = new(Serial);

                    UInt64 TotalSectorCount = (UInt64)1 + 0x21 + 1 +
                        (UInt64)(SBL2.Length / 0x200) +
                        (UInt64)(SBL3.Length / 0x200) +
                        (UInt64)(UEFI.Length / 0x200) +
                        NewGPT.GetPartition("SBL1").SizeInSectors - 1 +
                        NewGPT.GetPartition("TZ").SizeInSectors +
                        NewGPT.GetPartition("RPM").SizeInSectors +
                        NewGPT.GetPartition("WINSECAPP").SizeInSectors;

                    SetWorkingStatus("Flashing original bootloader...", MaxProgressValue: 100, Status: WPinternalsStatus.Flashing);
                    ProgressUpdater Progress = new(TotalSectorCount, (int ProgressPercentage, TimeSpan? TimeSpan) => UpdateWorkingStatus("Flashing original bootloader...", CurrentProgressValue: (ulong)ProgressPercentage, Status: WPinternalsStatus.Flashing));

                    Flasher.Hello();
                    Flasher.SetSecurityMode(0);
                    Flasher.OpenPartition(0x21);

                    LogFile.Log("Partition opened.");

                    byte[] MBR = FFU.GetSectors(0, 1);

                    LogFile.Log("Flash MBR at 0x" + ((UInt32)0).ToString("X8"));
                    Flasher.Flash(0, MBR, Progress, 0, 0x200);

                    LogFile.Log("Flash GPT at 0x" + ((UInt32)0x200).ToString("X8"));
                    Flasher.Flash(0x200, GPT, Progress, 0, 0x41FF); // Bad bounds-check in the flash-loader prohibits to write the last byte.

                    LogFile.Log("Flash SBL2 at 0x" + ((UInt32)NewGPT.GetPartition("SBL2").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("SBL2").FirstSector * 0x200, SBL2, Progress);
                    LogFile.Log("Flash SBL3 at 0x" + ((UInt32)NewGPT.GetPartition("SBL3").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("SBL3").FirstSector * 0x200, SBL3, Progress);
                    LogFile.Log("Flash UEFI at 0x" + ((UInt32)NewGPT.GetPartition("UEFI").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("UEFI").FirstSector * 0x200, UEFI, Progress);

                    // To minimize risk of brick also flash these partitions:
                    LogFile.Log("Flash SBL1 at 0x" + ((UInt32)NewGPT.GetPartition("SBL1").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("SBL1").FirstSector * 0x200, FFU.GetPartition("SBL1"), Progress, 0, ((UInt32)NewGPT.GetPartition("SBL1").SizeInSectors - 1) * 0x200);
                    LogFile.Log("Flash TZ at 0x" + ((UInt32)NewGPT.GetPartition("TZ").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("TZ").FirstSector * 0x200, FFU.GetPartition("TZ"), Progress);
                    LogFile.Log("Flash RPM at 0x" + ((UInt32)NewGPT.GetPartition("RPM").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("RPM").FirstSector * 0x200, FFU.GetPartition("RPM"), Progress);

                    // Workaround for bad bounds-check in flash-loader
                    UInt32 Length = (UInt32)FFU.GetPartition("WINSECAPP").Length;
                    UInt32 Start = (UInt32)NewGPT.GetPartition("WINSECAPP").FirstSector * 0x200;
                    if ((Start + Length) > 0x1E7FE00)
                    {
                        Length = 0x1E7FE00 - Start;
                    }

                    LogFile.Log("Flash WINSECAPP at 0x" + ((UInt32)NewGPT.GetPartition("WINSECAPP").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash(Start, FFU.GetPartition("WINSECAPP"), Progress, 0, Length);

                    Flasher.ClosePartition();

                    LogFile.Log("Partition closed. Flashing ready. Rebooting.");

                    // Reboot phone to Flash app
                    SetWorkingStatus("Flashing done. Rebooting...");
                    Flasher.Reboot();

                    Flasher.CloseSerial();

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader && Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader && Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                    }

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                    {
                        await SwitchModeViewModel.SwitchToWithStatus(Notifier, PhoneInterfaces.Lumia_Flash, SetWorkingStatus, UpdateWorkingStatus);
                    }
                }
                else
                {
                    throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in flash, download, or emergency flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                }

                SetWorkingStatus("Rebooting phone...");
                await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Bootloader);

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader && Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                {
                    await Notifier.WaitForArrival();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader && Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                {
                    throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                }

                LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)Notifier.CurrentModel;
                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash && FlashModel.ReadParam("FS")[3] > 0)
                {
                    ExitSuccess("Bootloader is restored", "NOTE: You need to flash a stock ROM because you recovered a phone from a bootloader unlock failure.");
                }
                else
                {
                    SetWorkingStatus("Booting phone...");

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                    {
                        ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                    {
                        ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                    {
                        ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                    {
                        ExitFailure("Failed to relock phone", "Your phone is half relocked. You may need to reflash a stock ROM");
                        LogFile.EndAction("RelockBootloader");
                        return;
                    }

                    LogFile.Log("Bootloader restored!", LogType.FileAndConsole);
                    ExitSuccess("Bootloader restored successfully!");
                }
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                ExitFailure("Error: " + Ex.Message, null);
            }
            finally
            {
                LogFile.EndAction("RelockBootloader");
            }
        }

        // Magic!
        // Platform Secure Boot Hack for Spec A devices
        //
        // Assumes phone in Flash mode
        //               in Qualcomm Dload
        //               in Qualcomm Flash
        //
        internal static async Task LumiaV1UnlockFirmware(PhoneNotifierViewModel Notifier, string FFUPath, string LoadersPath, string SBL3Path, string SupportedFFUPath, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            LogFile.BeginAction("UnlockBootloader");

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
                LogFile.Log("Assembling data for unlock", LogType.FileAndConsole);
                SetWorkingStatus("Assembling data for unlock", null, null);

                if (string.IsNullOrEmpty(FFUPath))
                {
                    throw new ArgumentNullException("FFU path is missing");
                }

                string DumpFilePrefix = Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\") + DateTime.Now.ToString("yyyy-MM-dd hh.mm.ss") + " - ";
                bool IsBootLoaderUnlocked = false;

                FFU FFU = null;
                try
                {
                    FFU = new FFU(FFUPath);
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing FFU-file failed.");
                }

                if (Notifier.CurrentModel is LumiaFlashAppModel)
                {
                    FlashVersion FlashVersion = ((LumiaFlashAppModel)Notifier.CurrentModel).GetFlashVersion();
                    if (FlashVersion == null)
                    {
                        throw new Exception("Error: The version of the Flash Application on the phone could not be determined.");
                    }

                    if ((FlashVersion.ApplicationMajor < 1) || ((FlashVersion.ApplicationMajor == 1) && (FlashVersion.ApplicationMinor < 28)))
                    {
                        throw new Exception("Error: The version of the Flash Application on the phone is too old. Update your phone using Windows Updates or flash a newer ROM to your phone. Then try again.");
                    }

                    UefiSecurityStatusResponse SecurityStatus = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadSecurityStatus();
                    IsBootLoaderUnlocked = SecurityStatus.AuthenticationStatus || SecurityStatus.RdcStatus || !SecurityStatus.SecureFfuEfuseStatus;
                }

                if (!IsBootLoaderUnlocked && string.IsNullOrEmpty(LoadersPath))
                {
                    throw new Exception("Error: Path for Loaders is mandatory.");
                }

                FFU SupportedFFU = null;
                if (App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V1.1-EFIESP").TargetVersions.Any(v => v.Description == FFU.GetOSVersion()))
                {
                    SupportedFFU = FFU;
                }
                else if (SupportedFFUPath == null)
                {
                    throw new ArgumentNullException("Donor-FFU with supported OS version was not provided");
                }
                else
                {
                    try
                    {
                        SupportedFFU = new FFU(SupportedFFUPath);
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Parsing Supported FFU-file failed.");
                    }
                    if (!App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V1.1-EFIESP").TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                    {
                        throw new ArgumentNullException("Donor-FFU with supported OS version was not provided");
                    }
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "01.bin", FFU.GetSectors(0, 34)); // Original GPT
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                GPT NewGPT = null;
                if (Notifier.CurrentModel is LumiaFlashAppModel)
                {
                    ((LumiaFlashAppModel)Notifier.CurrentModel).ResetPhone();

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    {
                        throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in bootloader mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                    }

                    NewGPT = ((LumiaBootManagerAppModel)Notifier.CurrentModel).ReadGPT();

                    await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                    }
                }
                else
                {
                    NewGPT = FFU.GPT;
                }

                // Make sure all partitions are in range of the emergency flasher.
                NewGPT.RestoreBackupPartitions();

                byte[] GPT = null;
                try
                {
                    GPT = NewGPT.InsertHack();
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing partitions failed.");
                }

                if (SBL3Path != null)
                {
                    Partition IsUnlockedFlag = NewGPT.GetPartition("IS_UNLOCKED_SBL3");
                    if (IsUnlockedFlag == null)
                    {
                        IsUnlockedFlag = new Partition
                        {
                            Name = "IS_UNLOCKED_SBL3",
                            Attributes = 0,
                            PartitionGuid = Guid.NewGuid(),
                            PartitionTypeGuid = Guid.NewGuid(),
                            FirstSector = 0x40,
                            LastSector = 0x40
                        };
                        NewGPT.Partitions.Add(IsUnlockedFlag);
                        GPT = NewGPT.Rebuild();
                    }
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "02.bin", GPT); // Patched GPT
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                SBL1 SBL1 = null;
                try
                {
                    SBL1 = new SBL1(FFU.GetPartition("SBL1"));
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing SBL1 failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "03.bin", SBL1.Binary); // Original SBL1
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                byte[] RootKeyHash = null;
                if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                {
                    QualcommDownload Download = new((QualcommSerial)Notifier.CurrentModel);
                    RootKeyHash = Download.GetRKH();
                }
                else if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Flash)
                {
                    RootKeyHash = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadParam("RRKH");
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Flash)
                {
                    if (RootKeyHash == null)
                    {
                        throw new Exception("Error: Root Key Hash could not be retrieved from the phone.");
                    }

                    // Make sure the RootKeyHash is not blank
                    // If the RootKeyHash is blank, this is an engineering device, and it will accept any RKH
                    // We expect the user to know what he is doing in such case and we will ignore checks
                    if (!StructuralComparisons.StructuralEqualityComparer.Equals(RootKeyHash, new byte[RootKeyHash.Length]))
                    {
                        if (SBL1.RootKeyHash == null)
                        {
                            throw new Exception("Error: Root Key Hash could not be retrieved from FFU file.");
                        }
                        if (!StructuralComparisons.StructuralEqualityComparer.Equals(RootKeyHash, SBL1.RootKeyHash))
                        {
                            LogFile.Log("Phone: " + Converter.ConvertHexToString(RootKeyHash, ""));
                            LogFile.Log("SBL1: " + Converter.ConvertHexToString(SBL1.RootKeyHash, ""));
                            throw new Exception("Error: Root Key Hash from phone and from FFU file do not match!");
                        }
                    }
                }

                SBL2 SBL2Partition = null;
                try
                {
                    SBL2Partition = new SBL2(FFU.GetPartition("SBL2"));
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing SBL2 failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "05.bin", SBL2Partition.Binary); // Original SBL2
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                byte[] SBL2;
                try
                {
                    SBL2 = SBL2Partition.Patch();
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Patching SBL2 failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "06.bin", SBL2Partition.Binary); // Patched SBL2
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                byte[] ExtraSector = null;
                try
                {
                    byte[] PartitionHeader = new byte[0x0C];
                    Buffer.BlockCopy(SBL2Partition.Binary, 0, PartitionHeader, 0, 0x0C);
                    ExtraSector = SBL1.GenerateExtraSector(PartitionHeader);
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Code generation failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "04.bin", ExtraSector); // Extra sector
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                SBL3 SBL3Partition;
                SBL3 OriginalSBL3;

                try
                {
                    OriginalSBL3 = new SBL3(FFU.GetPartition("SBL3"));
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing SBL3 from FFU failed.");
                }

                if (SBL3Path == null)
                {
                    SBL3Partition = OriginalSBL3;
                    LogFile.Log("Taking SBL3 from FFU");
                }
                else
                {
                    SBL3Partition = null;
                    try
                    {
                        SBL3Partition = new SBL3(SBL3Path);
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Parsing external SBL3 failed.");
                    }

                    if (SBL3Partition.Binary.Length > OriginalSBL3.Binary.Length)
                    {
                        throw new Exception("Error: Selected SBL3 is too large.");
                    }
                    LogFile.Log("Taking selected SBL3");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "07.bin", SBL3Partition.Binary); // Original SBL3
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                byte[] SBL3;
                try
                {
                    SBL3 = SBL3Partition.Patch();
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Patching SBL3 failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "08.bin", SBL3Partition.Binary); // Patched SBL3
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                UEFI UEFIPartition = null;
                try
                {
                    UEFIPartition = new UEFI(FFU.GetPartition("UEFI"));
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing UEFI failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "09.bin", UEFIPartition.Binary); // Original UEFI
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                byte[] UEFI;
                try
                {
                    UEFI = UEFIPartition.Patch();
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Patching UEFI failed.");
                }

#if DUMPPARTITIONS
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "0A.bin", UEFIPartition.Binary); // Patched UEFI
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
#endif

                List<QualcommPartition> PossibleLoaders = null;
                if (!IsBootLoaderUnlocked || Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                {
                    try
                    {
                        PossibleLoaders = QualcommLoaders.GetPossibleLoadersForRootKeyHash(LoadersPath, RootKeyHash);
                        if (PossibleLoaders.Count == 0)
                        {
                            throw new Exception("Error: No matching loaders found for RootKeyHash.");
                        }
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Unexpected error during scanning for loaders.");
                    }
                }

                if (IsBootLoaderUnlocked)
                // Flash phone in Flash app
                {
                    LumiaFlashAppModel CurrentModel = (LumiaFlashAppModel)Notifier.CurrentModel;
                    LogFile.Log("Start flashing in Custom Flash mode");

                    UInt64 TotalSectorCount = (UInt64)0x21 + 1 +
                        (UInt64)(SBL2.Length / 0x200) +
                        (UInt64)(SBL3.Length / 0x200) +
                        (UInt64)(UEFI.Length / 0x200);

                    SetWorkingStatus("Flashing unlocked bootloader (part 1)...", MaxProgressValue: 100, Status: WPinternalsStatus.Flashing);
                    ProgressUpdater Progress = new(TotalSectorCount, (int ProgressPercentage, TimeSpan? TimeSpan) => UpdateWorkingStatus("Flashing unlocked bootloader (part 1)...", CurrentProgressValue: (ulong)ProgressPercentage, Status: WPinternalsStatus.Flashing));

                    LogFile.Log("Flash GPT at 0x" + ((UInt32)0x200).ToString("X8"));
                    CurrentModel.FlashSectors(1, GPT, 0);
                    Progress.SetProgress(0x21);

                    if (ExtraSector != null)
                    {
                        LogFile.Log("Flash EXT at 0x" + ((UInt32)NewGPT.GetPartition("HACK").FirstSector * 0x200).ToString("X8"));
                        CurrentModel.FlashRawPartition(ExtraSector, "HACK", Progress);
                    }

                    LogFile.Log("Flash SBL2 at 0x" + ((UInt32)NewGPT.GetPartition("SBL2").FirstSector * 0x200).ToString("X8"));
                    CurrentModel.FlashRawPartition(SBL2, "SBL2", Progress);
                    LogFile.Log("Flash SBL3 at 0x" + ((UInt32)NewGPT.GetPartition("SBL3").FirstSector * 0x200).ToString("X8"));
                    CurrentModel.FlashRawPartition(SBL3, "SBL3", Progress);
                    LogFile.Log("Flash UEFI at 0x" + ((UInt32)NewGPT.GetPartition("UEFI").FirstSector * 0x200).ToString("X8"));
                    CurrentModel.FlashRawPartition(UEFI, "UEFI", Progress);

                    // phone is in flash mode, we can exit
                }
                else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash || Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download || Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Flash)
                {
                    // Switch to DLOAD
                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash)
                    {
                        SetWorkingStatus("Switching to Emergency Download mode...");
                        PerformSoftBrick(Notifier, FFU);
                        if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Download)
                        {
                            await Notifier.WaitForArrival();
                        }

                        if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Download)
                        {
                            throw new WPinternalsException("Phone failed to switch to emergency download mode.");
                        }
                    }

                    // Send loader
                    if (Notifier.CurrentInterface == PhoneInterfaces.Qualcomm_Download)
                    {
                        SetWorkingStatus("Sending loader...");
                        SendLoader(Notifier, PossibleLoaders);
                        if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Flash)
                        {
                            await Notifier.WaitForArrival();
                        }

                        if (Notifier.CurrentInterface != PhoneInterfaces.Qualcomm_Flash)
                        {
                            throw new WPinternalsException("Phone failed to switch to emergency flash mode.");
                        }
                    }

                    // Flash bootloader
                    QualcommSerial Serial = (QualcommSerial)Notifier.CurrentModel;
                    Serial.EncodeCommands = false;

                    QualcommFlasher Flasher = new(Serial);

                    UInt64 TotalSectorCount = (UInt64)1 + 0x21 + 1 +
                        (UInt64)(SBL2.Length / 0x200) +
                        (UInt64)(SBL3.Length / 0x200) +
                        (UInt64)(UEFI.Length / 0x200) +
                        NewGPT.GetPartition("SBL1").SizeInSectors - 1 +
                        NewGPT.GetPartition("TZ").SizeInSectors +
                        NewGPT.GetPartition("RPM").SizeInSectors +
                        NewGPT.GetPartition("WINSECAPP").SizeInSectors;

                    SetWorkingStatus("Flashing unlocked bootloader (part 1)...", MaxProgressValue: 100, Status: WPinternalsStatus.Flashing);
                    ProgressUpdater Progress = new(TotalSectorCount, (int ProgressPercentage, TimeSpan? TimeSpan) => UpdateWorkingStatus("Flashing unlocked bootloader (part 1)...", CurrentProgressValue: (ulong)ProgressPercentage, Status: WPinternalsStatus.Flashing));

                    Flasher.Hello();
                    Flasher.SetSecurityMode(0);
                    Flasher.OpenPartition(0x21);

                    LogFile.Log("Partition opened.");

                    byte[] MBR = FFU.GetSectors(0, 1);

                    if (ExtraSector != null)
                    {
                        LogFile.Log("Flash EXT at 0x" + ((UInt32)NewGPT.GetPartition("HACK").FirstSector * 0x200).ToString("X8"));
                        Flasher.Flash((uint)NewGPT.GetPartition("HACK").FirstSector * 0x200, ExtraSector, Progress);
                    }

                    LogFile.Log("Flash MBR at 0x" + ((UInt32)0).ToString("X8"));
                    Flasher.Flash(0, MBR, Progress, 0, 0x200);

                    LogFile.Log("Flash GPT at 0x" + ((UInt32)0x200).ToString("X8"));
                    Flasher.Flash(0x200, GPT, Progress, 0, 0x41FF); // Bad bounds-check in the flash-loader prohibits to write the last byte.

                    LogFile.Log("Flash SBL2 at 0x" + ((UInt32)NewGPT.GetPartition("SBL2").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("SBL2").FirstSector * 0x200, SBL2, Progress);
                    LogFile.Log("Flash SBL3 at 0x" + ((UInt32)NewGPT.GetPartition("SBL3").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("SBL3").FirstSector * 0x200, SBL3, Progress);
                    LogFile.Log("Flash UEFI at 0x" + ((UInt32)NewGPT.GetPartition("UEFI").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("UEFI").FirstSector * 0x200, UEFI, Progress);

                    // To minimize risk of brick also flash these partitions:
                    LogFile.Log("Flash SBL1 at 0x" + ((UInt32)NewGPT.GetPartition("SBL1").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("SBL1").FirstSector * 0x200, FFU.GetPartition("SBL1"), Progress, 0, ((UInt32)NewGPT.GetPartition("SBL1").SizeInSectors - 1) * 0x200);
                    LogFile.Log("Flash TZ at 0x" + ((UInt32)NewGPT.GetPartition("TZ").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("TZ").FirstSector * 0x200, FFU.GetPartition("TZ"), Progress);
                    LogFile.Log("Flash RPM at 0x" + ((UInt32)NewGPT.GetPartition("RPM").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash((uint)NewGPT.GetPartition("RPM").FirstSector * 0x200, FFU.GetPartition("RPM"), Progress);

                    // Workaround for bad bounds-check in flash-loader
                    UInt32 Length = (UInt32)FFU.GetPartition("WINSECAPP").Length;
                    UInt32 Start = (UInt32)NewGPT.GetPartition("WINSECAPP").FirstSector * 0x200;
                    if ((Start + Length) > 0x1E7FE00)
                    {
                        Length = 0x1E7FE00 - Start;
                    }

                    LogFile.Log("Flash WINSECAPP at 0x" + ((UInt32)NewGPT.GetPartition("WINSECAPP").FirstSector * 0x200).ToString("X8"));
                    Flasher.Flash(Start, FFU.GetPartition("WINSECAPP"), Progress, 0, Length);

                    Flasher.ClosePartition();

                    LogFile.Log("Partition closed. Flashing ready. Rebooting.");

                    // Reboot phone to Flash app
                    SetWorkingStatus("Flashing done. Rebooting...");
                    Flasher.Reboot();

                    Flasher.CloseSerial();

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader && Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader && Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                    }

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                    {
                        await SwitchModeViewModel.SwitchToWithStatus(Notifier, PhoneInterfaces.Lumia_Flash, SetWorkingStatus, UpdateWorkingStatus);
                    }

                    // phone is in flash mode, we can exit
                }
                else
                {
                    throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in flash, download or emergency flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                }

                await LumiaUnlockUEFI(Notifier, FFUPath, LoadersPath, SupportedFFUPath, SetWorkingStatus, UpdateWorkingStatus, null, (string Message, string SubMessage) =>
                {
                    ExitFailure(Message, SubMessage);
                    LogFile.EndAction("UnlockBootloader");
                    return;
                });

                SetWorkingStatus("Booting phone...");

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                {
                    await Notifier.WaitForArrival();
                }

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                {
                    await Notifier.WaitForArrival();
                }

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Normal)
                {
                    ExitFailure("Failed to unlock phone", "Your phone is half unlocked. You may need to reflash a stock ROM");
                    LogFile.EndAction("UnlockBootloader");
                    return;
                }

                LogFile.Log("Bootloader unlocked!", LogType.FileAndConsole);
                ExitSuccess("Bootloader unlocked successfully!", null);
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                ExitFailure("Error: " + Ex.Message, null);
            }
            finally
            {
                LogFile.EndAction("UnlockBootloader");
            }
        }

        // Magic!
        // UEFI Secure Boot Hack for Spec A and Spec B devices
        //
        // Assumes phone in Flash mode
        //
        internal static async Task LumiaRelockUEFI(PhoneNotifierViewModel Notifier, string FFUPath = null, bool DoResetFirst = true, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
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

            LogFile.BeginAction("RelockPhone");
            try
            {
                GPT GPT = null;
                Partition Target = null;
                LumiaFlashAppModel FlashModel = null;

                LogFile.Log("Command: Relock phone", LogType.FileAndConsole);

                if (Notifier.CurrentInterface == null)
                {
                    await Notifier.WaitForArrival();
                }

                byte[] EFIESPBackup = null;

                LumiaFlashAppPhoneInfo Info = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadPhoneInfo();
                bool IsSpecB = Info.FlashAppProtocolVersionMajor >= 2;
                bool UndoEFIESPPadding = false;

                byte[] GPTChunk;

                if (!IsSpecB)
                {
                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        ((LumiaFlashAppModel)Notifier.CurrentModel).ResetPhone();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    {
                        throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in bootloader mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                    }

                    GPTChunk = ((LumiaBootManagerAppModel)Notifier.CurrentModel).GetGptChunk(0x20000);
                }
                else
                {
                    GPTChunk = ((LumiaFlashAppModel)Notifier.CurrentModel).GetGptChunk(0x20000);
                }

                GPT = new GPT(GPTChunk);
                bool GPTChanged = false;
                Partition IsUnlockedPartitionSBL3 = GPT.GetPartition("IS_UNLOCKED_SBL3");
                if (IsUnlockedPartitionSBL3 == null)
                {
                    Partition BackNV = GPT.GetPartition("BACKUP_BS_NV");
                    if (BackNV != null)
                    {
                        UndoEFIESPPadding = true;
                    }
                }

                if (!IsSpecB)
                {
                    await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        throw new WPinternalsException("Phone is in an unexpected mode.", "The phone should have been detected in flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                    }
                }

                if (IsSpecB || IsUnlockedPartitionSBL3 != null)
                {
                    try
                    {
                        if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                        {
                            await SwitchModeViewModel.SwitchToWithStatus(Notifier, PhoneInterfaces.Lumia_MassStorage, SetWorkingStatus, UpdateWorkingStatus);
                        }

                        if (!(Notifier.CurrentModel is MassStorage))
                        {
                            throw new WPinternalsException("Failed to switch to Mass Storage mode");
                        }

                        SetWorkingStatus("Patching...", null, null, Status: WPinternalsStatus.Patching);

                        // Now relock the phone
                        MassStorage Storage = (MassStorage)Notifier.CurrentModel;

                        App.PatchEngine.TargetPath = Storage.Drive + "\\EFIESP\\";
                        App.PatchEngine.Restore("SecureBootHack-V2-EFIESP");
                        App.PatchEngine.Restore("SecureBootHack-V1.1-EFIESP");
                        App.PatchEngine.Restore("SecureBootHack-V1-EFIESP");

                        App.PatchEngine.TargetPath = Storage.Drive + "\\";
                        App.PatchEngine.Restore("SecureBootHack-MainOS");
                        App.PatchEngine.Restore("RootAccess-MainOS");

                        // Edit BCD
                        LogFile.Log("Edit BCD");
                        using (Stream BCDFileStream = new FileStream(Storage.Drive + @"\EFIESP\efi\Microsoft\Boot\BCD", FileMode.Open, FileAccess.ReadWrite))
                        {
                            using DiscUtils.Registry.RegistryHive BCDHive = new(BCDFileStream);
                            DiscUtils.BootConfig.Store BCDStore = new(BCDHive.Root);
                            DiscUtils.BootConfig.BcdObject MobileStartupObject = BCDStore.GetObject(new Guid("{01de5a27-8705-40db-bad6-96fa5187d4a6}"));
                            DiscUtils.BootConfig.Element NoCodeIntegrityElement = MobileStartupObject.GetElement(0x16000048);
                            if (NoCodeIntegrityElement != null)
                            {
                                MobileStartupObject.RemoveElement(0x16000048);
                            }

                            DiscUtils.BootConfig.BcdObject WinLoadObject = BCDStore.GetObject(new Guid("{7619dcc9-fafe-11d9-b411-000476eba25f}"));
                            NoCodeIntegrityElement = WinLoadObject.GetElement(0x16000048);
                            if (NoCodeIntegrityElement != null)
                            {
                                WinLoadObject.RemoveElement(0x16000048);
                            }
                        }

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
                        if (ByteOperations.ReadAsciiString(EFIESP, (UInt32)(EFIESP.Length / 2) + 3, 8) == "MSDOS5.0")
                        {
                            EFIESPBackup = new byte[EfiespSizeInSectors * 0x200 / 2];
                            Buffer.BlockCopy(EFIESP, (Int32)EfiespSizeInSectors * 0x200 / 2, EFIESPBackup, 0, (Int32)EfiespSizeInSectors * 0x200 / 2);
                        }

                        if (ByteOperations.ReadUInt16(EFIESP, 0xE) == LumiaGetFirstEFIESPSectorCount(GPT, new FFU(FFUPath), IsSpecB))
                        {
                            UndoEFIESPPadding = true;
                        }

                        if (Storage.DoesDeviceSupportReboot())
                        {
                            SetWorkingStatus("Rebooting phone...");
                            await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                        }
                        else
                        {
                            LogFile.Log("The phone is currently in Mass Storage Mode", LogType.ConsoleOnly);
                            LogFile.Log("To continue the relock-sequence, the phone needs to be rebooted", LogType.ConsoleOnly);
                            LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                            LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                            LogFile.Log("The relock-sequence will resume automatically", LogType.ConsoleOnly);
                            LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                            SetWorkingStatus("You need to manually reset your phone now!", "The phone is currently in Mass Storage Mode. To continue the relock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. The relock-sequence will resume automatically.", null, false, WPinternalsStatus.WaitingForManualReset);

                            await Notifier.WaitForRemoval();

                            SetWorkingStatus("Rebooting phone...");

                            await Notifier.WaitForArrival();
                        }
                    }
                    catch
                    {
                        // If switching to mass storage mode failed, then we just skip that part. This might be a half unlocked phone.
                        LogFile.Log("Skipping Mass Storage mode", LogType.FileAndConsole);
                    }
                }

                // Phone can also be in normal mode if switching to Mass Storage Mode had failed.
                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Normal)
                {
                    await SwitchModeViewModel.SwitchToWithStatus(Notifier, PhoneInterfaces.Lumia_Flash, SetWorkingStatus, UpdateWorkingStatus);
                }

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                {
                    await Notifier.WaitForArrival();
                }

                SetWorkingStatus("Flashing...", "The phone may reboot a couple of times. Just wait for it.", null, Status: WPinternalsStatus.Initializing);

                ((LumiaBootManagerAppModel)Notifier.CurrentModel).SwitchToFlashAppContext();

                List<FlashPart> FlashParts = new();

                if (UndoEFIESPPadding)
                {
                    FlashParts = LumiaGenerateUndoEFIESPFlashPayload(GPT, new FFU(FFUPath), IsSpecB);
                }

                FlashPart Part;

                FlashModel = (LumiaFlashAppModel)Notifier.CurrentModel;

                // Remove IS_UNLOCKED flag in GPT
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
                    LogFile.Log("BS Removed");
                }

                if (GPTChanged)
                {
                    GPT.Rebuild();
                    Part = new FlashPart
                    {
                        StartSector = 0,
                        Stream = new MemoryStream(GPTChunk)
                    };
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
                Info = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadPhoneInfo();
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
                await LumiaFlashParts(Notifier, FFUPath, false, false, FlashParts, DoResetFirst, ClearFlashingStatusAtEnd: !NvCleared,
                    SetWorkingStatus: (m, s, v, a, st) =>
                    {
                        if (SetWorkingStatus != null)
                        {
                            if ((st == WPinternalsStatus.Scanning) || (st == WPinternalsStatus.WaitingForManualReset))
                            {
                                SetWorkingStatus(m, s, v, a, st);
                            }
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
                            {
                                UpdateWorkingStatus(m, s, v, st);
                            }
                            else if ((LastStatus == WPinternalsStatus.Scanning) || (LastStatus == WPinternalsStatus.WaitingForManualReset))
                            {
                                SetWorkingStatus("Flashing...", "The phone may reboot a couple of times. Just wait for it.", MaxProgressValue, Status: WPinternalsStatus.Flashing);
                            }
                            else
                            {
                                UpdateWorkingStatus("Flashing...", "The phone may reboot a couple of times. Just wait for it.", v, Status: WPinternalsStatus.Flashing);
                            }

                            LastStatus = st;
                        }
                    });

                if (NvBackupPartition != null && IsSpecB)
                {
                    // An old NV backup was restored and it possibly contained the IsFlashing flag.
                    // Can't clear it immeadiately, so we need another flash.

                    SetWorkingStatus("Flashing...", "The phone may reboot a couple of times. Just wait for it.", null, Status: WPinternalsStatus.Flashing);

                    // If last flash was a normal flash, with no forced crash at the end (!NvCleared), then we have to wait for device arrival, because it could still be detected as Flash-mode from previous flash.
                    // When phone was forcably crashed, it can be in emergency mode, or still rebooting. Then also wait for device arrival.
                    // But it is also possible that it is already in bootmgr mode after being crashed (Lumia 950 / 950XL). In that case don't wait for arrival.
                    if (!NvCleared || ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)))
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                    {
                        ((LumiaBootManagerAppModel)Notifier.CurrentModel).SwitchToFlashAppContext();
                    }

                    if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash)
                    {
                        await LumiaFlashParts(Notifier, FFUPath, false, false, null, DoResetFirst, ClearFlashingStatusAtEnd: true, ShowProgress: false);
                    }
                }

                LogFile.Log("Phone is relocked", LogType.FileAndConsole);
                LogFile.EndAction("RelockPhone");
                ExitSuccess("Phone is relocked", null);
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                LogFile.EndAction("RelockPhone");
                ExitFailure("Error: " + Ex.Message, null);
            }
        }

        // Magic!
        // UEFI Secure Boot Hack for Spec A and Spec B devices
        //
        // Assumes phone in Flash mode
        //
        internal static async Task LumiaUnlockUEFI(PhoneNotifierViewModel Notifier, string ProfileFFUPath, string EDEPath, string SupportedFFUPath, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null, bool ExperimentalSpecBEFIESPUnlock = false, bool ExperimentalSpecAEFIESPUnlock = true, bool ReUnlockDevice = false)
        {
            LogFile.BeginAction("UnlockBootloader");
            LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)Notifier.CurrentModel;

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
                LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo();
                bool IsSpecB = Info.FlashAppProtocolVersionMajor >= 2;

                if (ProfileFFUPath == null)
                {
                    throw new ArgumentNullException("Profile FFU path is missing");
                }

                FFU ProfileFFU = new(ProfileFFUPath);

                if (Info.IsBootloaderSecure && !Info.PlatformID.StartsWith(ProfileFFU.PlatformID, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentNullException("Profile FFU has wrong Platform ID for connected phone");
                }

                string Patch = "SecureBootHack-V1.1-EFIESP";
                if (IsSpecB)
                {
                    Patch = "SecureBootHack-V2-EFIESP";
                }

                FFU SupportedFFU = null;
                if (App.PatchEngine.PatchDefinitions.First(p => p.Name == Patch).TargetVersions.Any(v => v.Description == ProfileFFU.GetOSVersion()))
                {
                    SupportedFFU = ProfileFFU;
                }
                else if (SupportedFFUPath == null)
                {
                    throw new ArgumentNullException("Donor-FFU with supported OS version was not provided");
                }
                else
                {
                    SupportedFFU = new FFU(SupportedFFUPath);
                    if (!App.PatchEngine.PatchDefinitions.First(p => p.Name == Patch).TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                    {
                        throw new ArgumentNullException("Donor-FFU with supported OS version was not provided");
                    }
                }

                // TODO: Check EDE file

                LogFile.Log("Assembling data for unlock", LogType.FileAndConsole);
                SetWorkingStatus("Assembling data for unlock", null, null);
                byte[] UnlockedEFIESP = ProfileFFU.GetPartition("EFIESP");

                LumiaPatchEFIESP(SupportedFFU, UnlockedEFIESP, IsSpecB);

                byte[] GPTChunk = FlashModel.GetGptChunk((UInt32)ProfileFFU.ChunkSize);
                byte[] GPTChunkBackup = new byte[GPTChunk.Length];
                Buffer.BlockCopy(GPTChunk, 0, GPTChunkBackup, 0, GPTChunk.Length);
                GPT GPT = new(GPTChunk);
                bool GPTChanged = false;

                LogFile.Log("Enabling Test Signing", LogType.FileAndConsole);
                SetWorkingStatus("Enabling Test Signing", null, null);

                List<FlashPart> Parts = new();
                FlashPart Part;

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
                Partition TargetPartition = GPT.GetPartition("UEFI_BS_NV");
                Part.StartSector = (UInt32)TargetPartition.FirstSector; // GPT is prepared for 64-bit sector-offset, but flash app isn't.
                string SBRes = IsSpecB ? "WPinternals.SB" : "WPinternals.SBA";
                Part.Stream = new SeekableStream(() =>
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                    // Magic!
                    // The SB(A) resource is a compressed version of a raw NV-variable-partition.
                    // In this partition the SecureBoot variable is disabled.
                    // It overwrites the variable in a different NV-partition than where this variable is stored usually.
                    // This normally leads to endless-loops when the NV-variables are enumerated.
                    // But the partition contains an extra hack to break out the endless loops.
                    var stream = assembly.GetManifestResourceStream(SBRes);

                    return new DecompressedStream(stream);
                });
                Parts.Add(Part);

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

                foreach (FlashPart _part in Parts)
                {
                    _part.ProgressText = "Enabling Test Signing...";
                }

                await LumiaFlashParts(Notifier, ProfileFFU.Path, false, false, Parts, true, false, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                {
                    await Notifier.WaitForArrival();
                }

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                {
                    throw new WPinternalsException("Error: Phone is in wrong mode", "The phone should have been detected in bootloader or flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                }

                if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).SwitchToFlashAppContext();
                    FlashModel = ((LumiaFlashAppModel)Notifier.CurrentModel);
                }

                GPTChanged = false;

                // Create backup-partition for EFIESP

                bool SBL3Eng = GPT.GetPartition("IS_UNLOCKED_SBL3") != null;

                bool ShouldApplyOldEFIESPMethod = IsSpecB ? !ExperimentalSpecBEFIESPUnlock : !ExperimentalSpecAEFIESPUnlock;
                if (!IsSpecB && !SBL3Eng)
                {
                    ShouldApplyOldEFIESPMethod = false;
                }

                Parts = ShouldApplyOldEFIESPMethod ? new List<FlashPart>() : LumiaGenerateEFIESPFlashPayload(UnlockedEFIESP, GPT, ProfileFFU, IsSpecB);
                Part = null;

                UInt32 OriginalEfiespSizeInSectors = (UInt32)GPT.GetPartition("EFIESP").SizeInSectors;
                UInt32 OriginalEfiespLastSector = (UInt32)GPT.GetPartition("EFIESP").LastSector;
                if (ShouldApplyOldEFIESPMethod)
                {
                    Partition BACKUP_EFIESP = GPT.GetPartition("BACKUP_EFIESP");
                    Partition EFIESP;

                    if (BACKUP_EFIESP == null && !ReUnlockDevice)
                    {
                        /*
                         * Before:
                         * 
                         *  ___________________________________________
                         * |                                           |
                         * |                  EFIESP                   |
                         * |                 Original                  |
                         * |___________________________________________|
                         * 
                         */

                        /*
                         * After:
                         * 
                         *  ___________________________________________
                         * |                    |                      |
                         * |    BACKUP_EFIESP   |        EFIESP        |
                         * |      Original      |       Unlocked       |
                         * |____________________|______________________|
                         * 
                         */

                        BACKUP_EFIESP = GPT.GetPartition("EFIESP");
                        Guid OriginalPartitionTypeGuid = BACKUP_EFIESP.PartitionTypeGuid;
                        Guid OriginalPartitionGuid = BACKUP_EFIESP.PartitionGuid;
                        BACKUP_EFIESP.Name = "BACKUP_EFIESP";
                        BACKUP_EFIESP.LastSector = BACKUP_EFIESP.FirstSector + (OriginalEfiespSizeInSectors / 2) - 1; // Original is 0x10000
                        BACKUP_EFIESP.PartitionGuid = Guid.NewGuid();
                        BACKUP_EFIESP.PartitionTypeGuid = Guid.NewGuid();
                        EFIESP = new Partition
                        {
                            Name = "EFIESP",
                            Attributes = BACKUP_EFIESP.Attributes,
                            PartitionGuid = OriginalPartitionGuid,
                            PartitionTypeGuid = OriginalPartitionTypeGuid,
                            FirstSector = BACKUP_EFIESP.LastSector + 1
                        };
                        EFIESP.LastSector = EFIESP.FirstSector + (OriginalEfiespSizeInSectors / 2) - 1; // Original is 0x10000
                        GPT.Partitions.Add(EFIESP);
                        GPTChanged = true;
                    }
                    if (BACKUP_EFIESP == null && ReUnlockDevice)
                    {
                        /*
                         * Before:
                         * 
                         *  ___________________________________________
                         * |                    |                      |
                         * |       EFIESP             BACKUP_EFIESP    |
                         * |      Unlocked              Original       |
                         * |____________________|______________________|
                         * 
                         */

                        /*
                         * After:
                         * 
                         *  ___________________________________________
                         * |                    |                      |
                         * |       EFIESP       |     BACKUP_EFIESP    |
                         * |      Unlocked      |       Original       |
                         * |____________________|______________________|
                         * 
                         */

                        EFIESP = GPT.GetPartition("EFIESP");
                        EFIESP.LastSector = EFIESP.FirstSector + (OriginalEfiespSizeInSectors / 2) - 1; // Original is 0x10000

                        BACKUP_EFIESP = new Partition
                        {
                            Name = "BACKUP_EFIESP",
                            Attributes = EFIESP.Attributes,
                            PartitionGuid = Guid.NewGuid(),
                            PartitionTypeGuid = Guid.NewGuid(),
                            FirstSector = EFIESP.LastSector + 1
                        };
                        BACKUP_EFIESP.LastSector = BACKUP_EFIESP.FirstSector + (OriginalEfiespSizeInSectors / 2) - 1; // Original is 0x10000
                        GPT.Partitions.Add(BACKUP_EFIESP);
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

                    Partition EFIESPPartition = GPT.GetPartition("EFIESP");
                    if (EFIESPPartition == null)
                    {
                        throw new WPinternalsException("EFIESP partition not found!", "No EFIESP partition was found in the provided FFU's GPT.");
                    }

                    if ((UInt64)UnlockedEFIESP.Length != (EFIESPPartition.SizeInSectors * 0x200))
                    {
                        throw new WPinternalsException("New EFIESP partition has wrong size. Size = 0x" + UnlockedEFIESP.Length.ToString("X8") + ". Expected size = 0x" + (EFIESPPartition.SizeInSectors * 0x200).ToString("X8"));
                    }

                    Part = new FlashPart
                    {
                        StartSector = (UInt32)EFIESPPartition.FirstSector, // GPT is prepared for 64-bit sector-offset, but flash app isn't.
                        Stream = new MemoryStream(UnlockedEFIESP)
                    };
                    Parts.Add(Part);
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

                foreach (FlashPart _part in Parts)
                {
                    _part.ProgressText = IsSpecB ? "Flashing unlocked bootloader (part 1)..." : "Flashing unlocked bootloader (part 2)...";
                }

                await LumiaFlashParts(Notifier, ProfileFFU.Path, false, false, Parts, true, true, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                {
                    await Notifier.WaitForArrival();
                }

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                {
                    throw new WPinternalsException("Error: Phone is in wrong mode", "The phone should have been detected in bootloader or flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                }

                if (!IsSpecB && !SBL3Eng)
                {
                    ((LumiaFlashAppModel)Notifier.CurrentModel).ResetPhone();

                    LogFile.Log("Bootloader unlocked!", LogType.FileAndConsole);
                    ExitSuccess("Bootloader unlocked successfully!", null);

                    return;
                }

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
                    SetWorkingStatus("You need to manually reset your phone now!", "The phone is currently in Mass Storage Mode, but the driver of the PC failed to start. Unfortunately this happens sometimes. You need to manually reset the phone now. Keep the phone connected to the PC. Windows Phone Internals will automatically start to revert the changes. After the phone is fully booted again, you can retry to unlock the bootloader.", null, false, WPinternalsStatus.WaitingForManualReset);
                    await Notifier.WaitForArrival(); // Should be detected in Bootmanager mode
                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                    {
                        IsPhoneInBadMassStorageMode = true;
                    }
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                {
                    // Probably the "BootOrder" prevents to boot to MobileStartup. Mass Storage mode depends on MobileStartup.
                    // In this case Bootarm boots straight to Winload. But Winload can't handle the change of the EFIESP partition. That will cause a bootloop.

                    SetWorkingStatus("Problem detected, rolling back...", ErrorMessage);
                    await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                    Parts = new List<FlashPart>();

                    // Restore original GPT, which will also reference the original NV.
                    Part = new FlashPart
                    {
                        StartSector = 0,
                        Stream = new MemoryStream(GPTChunkBackup)
                    };
                    Parts.Add(Part);

                    await LumiaFlashParts(Notifier, ProfileFFU.Path, false, false, Parts, true, false, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);

                    // An old NV backup was restored and it possibly contained the IsFlashing flag.
                    // Can't clear it immeadiately, so we need another flash.
                    if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                    {
                        await Notifier.WaitForArrival();
                    }

                    if ((Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader) || (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash))
                    {
                        await LumiaFlashParts(Notifier, ProfileFFU.Path, false, false, null, true, true, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);
                    }

                    if (IsPhoneInBadMassStorageMode)
                    {
                        ExitFailure("Failed to unlock the bootloader due to misbehaving driver. Wait for phone to boot to Windows and then try again.", "The Mass Storage driver of the PC failed to start. Unfortunately this happens sometimes. After the phone is fully booted again, you can retry to unlock the bootloader.");
                    }
                    else
                    {
                        ExitFailure("Failed to unlock the bootloader", "It is not possible to unlock the bootloader straight after flashing. NOTE: Fully reboot the phone and then properly shutdown the phone, before you can try to unlock again!");
                    }

                    return;
                }

                SetWorkingStatus("Create backup partition...", null, null);

                MassStorage MassStorage = (MassStorage)Notifier.CurrentModel;
                GPTChunk = MassStorage.ReadSectors(0, 0x100);
                GPT = new GPT(GPTChunk);

                if (ShouldApplyOldEFIESPMethod)
                {
                    Partition BACKUP_EFIESP = GPT.GetPartition("BACKUP_EFIESP");
                    byte[] BackupEFIESP = MassStorage.ReadSectors(BACKUP_EFIESP.FirstSector, BACKUP_EFIESP.SizeInSectors);

                    // Copy the backed up unlocked EFIESP for future use
                    byte[] BackupUnlockedEFIESP = new byte[UnlockedEFIESP.Length];
                    Buffer.BlockCopy(BackupEFIESP, 0, BackupUnlockedEFIESP, 0, BackupEFIESP.Length);

                    try
                    {
                        LumiaPatchEFIESP(SupportedFFU, BackupUnlockedEFIESP, IsSpecB);
                    }
                    catch (Exception ex)
                    {
                        LogFile.Log("Exception: " + ex.GetType().ToString(), LogType.FileOnly);
                        LogFile.Log("It seems that the backed up EFIESP partition is invalid.", LogType.FileOnly);
                        LogFile.Log("Using the FFU partition as a failsafe.", LogType.FileOnly);

                        BackupEFIESP = ProfileFFU.GetPartition("EFIESP");

                        // Copy the backed up unlocked EFIESP for future use
                        BackupUnlockedEFIESP = new byte[UnlockedEFIESP.Length];
                        Buffer.BlockCopy(BackupEFIESP, 0, BackupUnlockedEFIESP, 0, UnlockedEFIESP.Length);

                        LumiaPatchEFIESP(SupportedFFU, BackupUnlockedEFIESP, IsSpecB);
                    }

                    SetWorkingStatus("Boot optimization...", null, null);

                    App.PatchEngine.TargetPath = MassStorage.Drive + "\\";
                    App.PatchEngine.Patch("SecureBootHack-MainOS"); // Don't care about result here. Some phones do not need this.

                    LogFile.Log("The phone is currently in Mass Storage Mode", LogType.ConsoleOnly);
                    LogFile.Log("To continue the unlock-sequence, the phone needs to be rebooted", LogType.ConsoleOnly);
                    LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                    LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                    LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                    LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                    SetWorkingStatus("You need to manually reset your phone now!", "The phone is currently in Mass Storage Mode. To continue the unlock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. The unlock-sequence will resume automatically.", null, false, WPinternalsStatus.WaitingForManualReset);

                    await Notifier.WaitForRemoval();

                    SetWorkingStatus("Rebooting phone...");

                    await Notifier.WaitForArrival();
                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                    {
                        throw new WPinternalsException("Phone is in wrong mode", "The phone should have been detected in bootloader mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                    } ((LumiaBootManagerAppModel)Notifier.CurrentModel).SwitchToFlashAppContext();

                    UInt32 OriginalEfiespFirstSector;
                    if (!ReUnlockDevice)
                    {
                        /*
                         * Before:
                         * 
                         *  ___________________________________________
                         * |                    |                      |
                         * |    BACKUP_EFIESP   |        EFIESP        |
                         * |      Original      |       Unlocked       |
                         * |____________________|______________________|
                         * 
                         */

                        /*
                         * After:
                         * 
                         *  ___________________________________________
                         * |                    |                      |
                         * |       EFIESP             BACKUP_EFIESP    |
                         * |      Unlocked              Original       |
                         * |____________________|______________________|
                         * 
                         */

                        // EFIESP is appended at the end of the GPT
                        // BACKUP_EFIESP is at original location in GPT
                        Partition EFIESP = GPT.GetPartition("EFIESP");
                        OriginalEfiespFirstSector = (UInt32)BACKUP_EFIESP.FirstSector;
                        BACKUP_EFIESP.Name = "EFIESP";
                        BACKUP_EFIESP.LastSector = OriginalEfiespLastSector;
                        BACKUP_EFIESP.PartitionGuid = EFIESP.PartitionGuid;
                        BACKUP_EFIESP.PartitionTypeGuid = EFIESP.PartitionTypeGuid;
                        GPT.Partitions.Remove(EFIESP);
                    }
                    else
                    {
                        /*
                         * Before:
                         * 
                         *  ___________________________________________
                         * |                    |                      |
                         * |       EFIESP       |     BACKUP_EFIESP    |
                         * |      Unlocked      |       Original       |
                         * |____________________|______________________|
                         * 
                         */

                        /*
                         * After:
                         * 
                         *  ___________________________________________
                         * |                    |                      |
                         * |       EFIESP             BACKUP_EFIESP    |
                         * |      Unlocked              Original       |
                         * |____________________|______________________|
                         * 
                         */

                        // EFIESP is expended to its full size
                        // BACKUP_EFIESP is removed
                        Partition EFIESP = GPT.GetPartition("EFIESP");
                        OriginalEfiespFirstSector = (UInt32)EFIESP.FirstSector;
                        EFIESP.LastSector = OriginalEfiespLastSector;
                        GPT.Partitions.Remove(BACKUP_EFIESP);
                    }

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
                    }

                    Parts = new List<FlashPart>();
                    GPT.Rebuild();
                    Part = new FlashPart
                    {
                        StartSector = 0,
                        Stream = new MemoryStream(GPTChunk)
                    };
                    Parts.Add(Part);
                    Part = new FlashPart
                    {
                        StartSector = OriginalEfiespFirstSector,
                        Stream = new MemoryStream(BackupUnlockedEFIESP) // We must keep the Oiriginal EFIESP, but unlocked, for many reasons
                    };
                    Parts.Add(Part);
                    Part = new FlashPart
                    {
                        StartSector = OriginalEfiespFirstSector + (OriginalEfiespSizeInSectors / 2),
                        Stream = new MemoryStream(BackupEFIESP)
                    };
                    Parts.Add(Part);

                    foreach (FlashPart _part in Parts)
                    {
                        _part.ProgressText = "Flashing unlocked bootloader (part 2)...";
                    }

                    await LumiaFlashParts(Notifier, ProfileFFU.Path, false, false, Parts, true, true, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);
                }
                else
                {
                    ulong FirstSector = GPT.GetPartition("EFIESP").FirstSector;
                    ulong SectorCount = LumiaGetFirstEFIESPSectorCount(GPT, ProfileFFU, IsSpecB);
                    byte[] BackupEFIESPAllocation = MassStorage.ReadSectors(FirstSector, SectorCount);

                    // The backed up buffer includes our changed header done previously to have two EFIESPs in a single partition
                    // If we want to read the original partition we need to revert our changes to the first sector.
                    UnlockedEFIESP = new byte[GPT.GetPartition("EFIESP").SizeInSectors * 0x200];
                    Buffer.BlockCopy(BackupEFIESPAllocation, 0, UnlockedEFIESP, 0, BackupEFIESPAllocation.Length);
                    ByteOperations.WriteUInt16(UnlockedEFIESP, 0xE, ByteOperations.ReadUInt16(ProfileFFU.GetPartition("EFIESP"), 0xE));

                    LogFile.Log("Unlocking backup partition", LogType.FileAndConsole);
                    SetWorkingStatus("Unlocking backup partition", null, null);

                    LumiaPatchEFIESP(SupportedFFU, UnlockedEFIESP, IsSpecB);

                    SetWorkingStatus("Boot optimization...", null, null);

                    App.PatchEngine.TargetPath = MassStorage.Drive + "\\";
                    App.PatchEngine.Patch("SecureBootHack-MainOS"); // Don't care about result here. Some phones do not need this.

                    if (MassStorage.DoesDeviceSupportReboot())
                    {
                        SetWorkingStatus("Rebooting phone...");
                        await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                    }
                    else
                    {
                        LogFile.Log("The phone is currently in Mass Storage Mode", LogType.ConsoleOnly);
                        LogFile.Log("To continue the unlock-sequence, the phone needs to be rebooted", LogType.ConsoleOnly);
                        LogFile.Log("Keep the phone connected to the PC", LogType.ConsoleOnly);
                        LogFile.Log("Reboot the phone manually by pressing and holding the power-button of the phone for about 10 seconds until it vibrates", LogType.ConsoleOnly);
                        LogFile.Log("The unlock-sequence will resume automatically", LogType.ConsoleOnly);
                        LogFile.Log("Waiting for manual reset of the phone...", LogType.ConsoleOnly);

                        SetWorkingStatus("You need to manually reset your phone now!", "The phone is currently in Mass Storage Mode. To continue the unlock-sequence, the phone needs to be rebooted. Keep the phone connected to the PC. The unlock-sequence will resume automatically.", null, false, WPinternalsStatus.WaitingForManualReset);

                        await Notifier.WaitForRemoval();

                        SetWorkingStatus("Rebooting phone...");

                        await Notifier.WaitForArrival();
                        if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader)
                        {
                            throw new WPinternalsException("Phone is in wrong mode", "The phone should have been detected in bootloader mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                        }
                    }
                    ((LumiaBootManagerAppModel)Notifier.CurrentModel).SwitchToFlashAppContext();

                    Parts = LumiaGenerateEFIESPFlashPayload(UnlockedEFIESP, GPT, ProfileFFU, IsSpecB);

                    foreach (FlashPart _part in Parts)
                    {
                        _part.ProgressText = IsSpecB ? "Flashing unlocked bootloader (part 2)..." : "Flashing unlocked bootloader (part 3)...";
                    }

                    await LumiaFlashParts(Notifier, ProfileFFU.Path, false, false, Parts, true, true, true, true, false, SetWorkingStatus, UpdateWorkingStatus, null, null, EDEPath);

                    if (!IsSpecB)
                    {
                        ((LumiaFlashAppModel)Notifier.CurrentModel).ResetPhone();
                    }
                }

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                {
                    await Notifier.WaitForArrival();
                }

                if ((Notifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash))
                {
                    throw new WPinternalsException("Error: Phone is in wrong mode", "The phone should have been detected in bootloader or flash mode. Instead it has been detected in " + Notifier.CurrentInterface.ToString() + " mode.");
                }

                LogFile.Log("Bootloader unlocked!", LogType.FileAndConsole);
                LogFile.EndAction("UnlockBootloader");
                ExitSuccess("Bootloader unlocked!", null);
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                LogFile.EndAction("UnlockBootloader");
                ExitFailure(Ex.Message, Ex is WPinternalsException ? ((WPinternalsException)Ex).SubMessage : null);
            }
        }

        // Magic!
        // This function generates a flashing payload which allows us to write a new modified EFIESP to the phone, without changing the current EFIESP.
        // This way we always have a backup copy of the original EFIESP partition, at the beginning of the partition, and our new EFIESP, at the end of the partition!
        // The new EFIESP partition is also usable instantly without going to mass storage mode and attempt a partition place swap.
        // This hack is usable on Spec A devices unlocked, engineering phones, and Spec B phones with the Custom flash exploit.
        // Sector alignment and data length are ensured for the Custom flash exploit
        // This hack doesn't require us to modify the GPT of the device at all, the new EFIESP is written around the middle of the old one,
        // while keeping the first half of the partition intact, except the very first chunk
        private static List<FlashPart> LumiaGenerateEFIESPFlashPayload(byte[] NewEFIESP, GPT DeviceGPT, FFU DeviceFFU, bool IsSpecB)
        {
            const uint SectorSize = 512;

            Partition EFIESP = DeviceGPT.GetPartition("EFIESP");
            UInt16 ReservedOGSectors = ByteOperations.ReadUInt16(DeviceFFU.GetPartition("EFIESP"), 0xE);

            UInt16 ReservedSectors = LumiaGetFirstEFIESPSectorCount(DeviceGPT, DeviceFFU, IsSpecB);
            Int32 EFIESPFirstPartSize = IsSpecB ? DeviceFFU.ChunkSize : (int)SectorSize * ReservedOGSectors;

            byte[] FirstSector = DeviceFFU.GetPartition("EFIESP").Take(EFIESPFirstPartSize).ToArray();
            ByteOperations.WriteUInt16(FirstSector, 0xE, ReservedSectors);

            byte[] SecondEFIESP = NewEFIESP.Skip((int)SectorSize * ReservedOGSectors).Take((int)(NewEFIESP.Length - (ReservedSectors * SectorSize))).ToArray();

            List<FlashPart> Parts = new();

            FlashPart Part = new();
            Part.StartSector = (uint)EFIESP.FirstSector;
            Part.Stream = new MemoryStream(FirstSector);
            Parts.Add(Part);

            Part = new FlashPart
            {
                StartSector = (uint)(EFIESP.FirstSector + ReservedSectors),
                Stream = new MemoryStream(SecondEFIESP)
            };
            Parts.Add(Part);

            return Parts;
        }

        // Magic!
        // This function generates a flashing payload which allows us to get back the original device EFIESP without ever going to mass storage mode.
        private static List<FlashPart> LumiaGenerateUndoEFIESPFlashPayload(GPT DeviceGPT, FFU DeviceFFU, bool IsSpecB)
        {
            const uint SectorSize = 512;

            Partition EFIESP = DeviceGPT.GetPartition("EFIESP");
            UInt16 ReservedOGSectors = ByteOperations.ReadUInt16(DeviceFFU.GetPartition("EFIESP"), 0xE);

            UInt16 ReservedSectors = LumiaGetFirstEFIESPSectorCount(DeviceGPT, DeviceFFU, IsSpecB);
            Int32 EFIESPFirstPartSize = IsSpecB ? DeviceFFU.ChunkSize : (int)SectorSize * ReservedOGSectors;

            byte[] FirstSector = DeviceFFU.GetPartition("EFIESP").Take(EFIESPFirstPartSize).ToArray();

            List<FlashPart> Parts = new();

            FlashPart Part = new();
            Part.StartSector = (uint)EFIESP.FirstSector;
            Part.Stream = new MemoryStream(FirstSector);
            Parts.Add(Part);

            return Parts;
        }

        // Magic!
        // This function gets the first sector of the new EFIESP location without ever going to mass storage mode.
        private static UInt16 LumiaGetFirstEFIESPSectorCount(GPT DeviceGPT, FFU DeviceFFU, bool IsSpecB)
        {
            const uint SectorSize = 512;
            Partition EFIESP = DeviceGPT.GetPartition("EFIESP");
            UInt16 ReservedOGSectors = ByteOperations.ReadUInt16(DeviceFFU.GetPartition("EFIESP"), 0xE);

            UInt64 numberofsectors = EFIESP.SizeInSectors;
            UInt64 halfnumberofsectors = numberofsectors / 2;
            UInt64 allocatednumberofsectors = halfnumberofsectors - ReservedOGSectors + 1;

            UInt16 ReservedSectors = 0xFFFF;

            if (allocatednumberofsectors < ReservedSectors)
            {
                UInt64 totalnumberofadditionalsectors = ReservedSectors - allocatednumberofsectors;
                ReservedSectors -= (ushort)totalnumberofadditionalsectors;
            }

            if (IsSpecB && (ReservedSectors % (DeviceFFU.ChunkSize / SectorSize) != 0))
            {
                ReservedSectors -= (ushort)(ReservedSectors % (DeviceFFU.ChunkSize / SectorSize));
            }

            return ReservedSectors;
        }

        private static async Task LumiaFlashParts(PhoneNotifierViewModel Notifier, string FFUPath, bool PerformFullFlashFirst, bool SkipWrite, List<FlashPart> Parts, bool DoResetFirst = true, bool ClearFlashingStatusAtEnd = true, bool CheckSectorAlignment = true, bool ShowProgress = true, bool Experimental = false, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null, string EDEPath = null)
        {
            LumiaFlashAppPhoneInfo Info = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadPhoneInfo();
            bool IsSpecA = Info.FlashAppProtocolVersionMajor < 2;

            if (IsSpecA)
            {
                LumiaV1FlashParts(Notifier, Parts, SetWorkingStatus, UpdateWorkingStatus);
            }
            else
            {
                await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(Notifier, FFUPath, PerformFullFlashFirst, SkipWrite, Parts, DoResetFirst, ClearFlashingStatusAtEnd, CheckSectorAlignment, ShowProgress, Experimental, SetWorkingStatus, UpdateWorkingStatus, ExitSuccess, ExitFailure, EDEPath);
            }
        }

        private static void LumiaV1FlashParts(PhoneNotifierViewModel Notifier, List<FlashPart> FlashParts, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null)
        {
            SetWorkingStatus("Initializing flash...", null, 100, Status: WPinternalsStatus.Initializing);

            LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)Notifier.CurrentModel;

            UInt64 InputStreamLength = 0;
            UInt64 totalwritten = 0;
            int ProgressPercentage = 0;

            if (FlashParts != null)
            {
                foreach (FlashPart Part in FlashParts)
                {
                    InputStreamLength += (ulong)Part.Stream.Length;
                }

                foreach (FlashPart Part in FlashParts)
                {
                    Stream InputStream = new DecompressedStream(Part.Stream);

                    if (InputStream != null)
                    {
                        using (InputStream)
                        {
                            const int FlashBufferSize = 0x200000; // Flash 8 GB phone -> buffersize 0x200000 = 11:45 min, buffersize 0x20000 = 12:30 min
                            byte[] FlashBuffer = new byte[FlashBufferSize];
                            int BytesRead;
                            UInt64 i = 0;
                            do
                            {
                                BytesRead = InputStream.Read(FlashBuffer, 0, FlashBufferSize);

                                byte[] FlashBufferFinalSize;
                                if (BytesRead > 0)
                                {
                                    if (BytesRead == FlashBufferSize)
                                    {
                                        FlashBufferFinalSize = FlashBuffer;
                                    }
                                    else
                                    {
                                        FlashBufferFinalSize = new byte[BytesRead];
                                        Buffer.BlockCopy(FlashBuffer, 0, FlashBufferFinalSize, 0, BytesRead);
                                    }

                                    FlashModel.FlashSectors((UInt32)(Part.StartSector + (i / 0x200)), FlashBufferFinalSize, ProgressPercentage);
                                }

                                UpdateWorkingStatus(Part.ProgressText, null, (uint)ProgressPercentage, WPinternalsStatus.Flashing);
                                totalwritten += (UInt64)FlashBuffer.Length / 0x200;
                                ProgressPercentage = (int)((double)totalwritten / (InputStreamLength / 0x200) * 100);

                                i += FlashBufferSize;
                            }
                            while (BytesRead == FlashBufferSize);
                        }
                    }
                }
            }

            UpdateWorkingStatus(null, null, 100, WPinternalsStatus.Flashing);
        }

        private static void LumiaPatchEFIESP(FFU SupportedFFU, byte[] EFIESPPartition, bool SpecB)
        {
            using DiscUtils.Fat.FatFileSystem EFIESPFileSystem = new(new MemoryStream(EFIESPPartition));
            App.PatchEngine.TargetImage = EFIESPFileSystem;

            string PatchDefinition = "SecureBootHack-V1.1-EFIESP";
            if (SpecB)
            {
                PatchDefinition = "SecureBootHack-V2-EFIESP";
            }

            bool PatchResult = App.PatchEngine.Patch(PatchDefinition);
            if (!PatchResult)
            {
                LogFile.Log("Donor-FFU: " + SupportedFFU.Path);
                byte[] SupportedEFIESP = SupportedFFU.GetPartition("EFIESP");

                using (DiscUtils.Fat.FatFileSystem SupportedEFIESPFileSystem = new(new MemoryStream(SupportedEFIESP)))
                using (DiscUtils.Streams.SparseStream SupportedMobileStartupStream = SupportedEFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Open))
                using (MemoryStream SupportedMobileStartupMemStream = new())
                using (Stream MobileStartupStream = EFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Create, FileAccess.Write))
                {
                    SupportedMobileStartupStream.CopyTo(SupportedMobileStartupMemStream);
                    byte[] SupportedMobileStartup = SupportedMobileStartupMemStream.ToArray();

                    // Save supported mobilestartup.efi
                    LogFile.Log("Taking mobilestartup.efi from donor-FFU");
                    MobileStartupStream.Write(SupportedMobileStartup, 0, SupportedMobileStartup.Length);
                }

                PatchResult = App.PatchEngine.Patch(PatchDefinition);
                if (!PatchResult)
                {
                    throw new WPinternalsException("Failed to patch bootloader", "An error occured while patching Operating System files on the EFIESP partition provided. Make sure no boot files have been tampered with and you use the latest version of the tool. This error cannot be caused by an incorrect Operating System version as the tool automatically uses replacement if the version isn't supported, unless the replacement files have been tampered with or are not compatible.");
                }
            }

            LogFile.Log("Edit BCD");
            using Stream BCDFileStream = EFIESPFileSystem.OpenFile(@"\efi\Microsoft\Boot\BCD", FileMode.Open, FileAccess.ReadWrite);
            using DiscUtils.Registry.RegistryHive BCDHive = new(BCDFileStream);
            DiscUtils.BootConfig.Store BCDStore = new(BCDHive.Root);
            DiscUtils.BootConfig.BcdObject MobileStartupObject = BCDStore.GetObject(new Guid("{01de5a27-8705-40db-bad6-96fa5187d4a6}"));
            DiscUtils.BootConfig.Element NoCodeIntegrityElement = MobileStartupObject.GetElement(0x16000048);
            if (NoCodeIntegrityElement != null)
            {
                NoCodeIntegrityElement.Value = DiscUtils.BootConfig.ElementValue.ForBoolean(true);
            }
            else
            {
                MobileStartupObject.AddElement(0x16000048, DiscUtils.BootConfig.ElementValue.ForBoolean(true));
            }

            DiscUtils.BootConfig.BcdObject WinLoadObject = BCDStore.GetObject(new Guid("{7619dcc9-fafe-11d9-b411-000476eba25f}"));
            NoCodeIntegrityElement = WinLoadObject.GetElement(0x16000048);
            if (NoCodeIntegrityElement != null)
            {
                NoCodeIntegrityElement.Value = DiscUtils.BootConfig.ElementValue.ForBoolean(true);
            }
            else
            {
                WinLoadObject.AddElement(0x16000048, DiscUtils.BootConfig.ElementValue.ForBoolean(true));
            }
        }
    }
}
