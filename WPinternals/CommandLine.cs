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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WPinternals
{
    internal static class CommandLine
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const UInt32 StdOutputHandle = 0xFFFFFFF5;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(UInt32 nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern void SetStdHandle(UInt32 nStdHandle, IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private const int MY_CODE_PAGE = 437;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 0x3;

        internal static bool IsConsoleVisible = false;
        internal static bool IsNewConsoleCreated = false;

        private static IntPtr hConsoleWnd;

        [DllImport("User32.Dll", EntryPoint = "PostMessageA")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

        private const int VK_RETURN = 0x0D;
        private const int WM_KEYDOWN = 0x100;

        /// <summary>
        /// When the main window should not be shown, this function should call ExitProcess and it should not return.
        /// </summary>
        internal static async Task ParseCommandLine(System.Threading.SynchronizationContext UIContext)
        {
            FFU FFU = null;
            PhoneNotifierViewModel Notifier;
            LumiaFlashAppModel FlashModel;
            LumiaBootManagerAppModel BootMgrModel;
            LumiaPhoneInfoAppModel PhoneInfoModel;
            NokiaPhoneModel NormalModel;
            LumiaFlashAppPhoneInfo FlashInfo;
            LumiaPhoneInfoAppPhoneInfo PhoneInfo;
            LumiaBootManagerPhoneInfo BootManagerInfo;
            string ProductType;
            string ProductCode;
            string OperatorCode;
            string DownloadFolder;
            string FFUFilePath;
            string URL;
            string[] URLs;
            Uri URI;
            string FFUFileName;
            string EmergencyFileName;
            string EmergencyFilePath;
            string ProgrammerPath = "";
            string PayloadPath = "";
            string EfiEspImagePath = null;
            string MainOsImagePath = null;
            DiscUtils.Fat.FatFileSystem UnlockedEFIESPFileSystem;
            DiscUtils.Ntfs.NtfsFileSystem UnlockedMainOsFileSystem;
            bool PatchResult;

            try
            {
                string[] args = Environment.GetCommandLineArgs();

                if (args.Length == 1)
                {
                    return;
                }

                switch (args[1].ToLower().TrimStart(['-', '/']))
                {
#if DEBUG
                    case "test":
                        LogFile.BeginAction("Test");
                        await TestCode.Test(UIContext);
                        LogFile.EndAction("Test");
                        break;
#endif
                    case "flashpartition":
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -FlashPartition <Partition name> <Partition file> <Optional: FFU file>");
                        }

                        if (args.Length >= 5)
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FlashPartition(UIContext, args[4], args[2], args[3]);
                        }
                        else
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FlashPartition(UIContext, null, args[2], args[3]);
                        }

                        break;
                    case "flashraw":
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -FlashRaw <Start sector> <Raw file> <Optional: FFU file>");
                        }

                        UInt64 StartSector = 0;
                        try
                        {
                            StartSector = args[2].StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                                ? Convert.ToUInt64(args[2][2..], 16)
                                : Convert.ToUInt64(args[2], 10);
                        }
                        catch
                        {
                            LogFile.Log("Bad start sector", LogType.ConsoleOnly);
                            break;
                        }
                        if (args.Length >= 5)
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FlashRaw(UIContext, StartSector, args[3], args[4]);
                        }
                        else
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FlashRaw(UIContext, StartSector, args[3], null);
                        }

                        break;
                    case "flashpartitionimmediately":
                        if (args.Length < 5)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -FlashPartition <Partition name> <Partition file> <FFU Path>");
                        }

                        await LumiaV2UnlockBootViewModel.LumiaV2FlashPartition(UIContext, args[4], args[2], args[3], false);
                        break;
                    case "readgpt":
                        LogFile.BeginAction("ReadGPT");
                        try
                        {
                            Notifier = new PhoneNotifierViewModel();
                            UIContext.Send(s => Notifier.Start(), null);

                            BootMgrModel = (LumiaBootManagerAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Bootloader); // This also works for Bootloader Spec A

                            GPT GPT = BootMgrModel.ReadGPT(); // May throw NotSupportedException
                            foreach (Partition Partition in GPT.Partitions)
                            {
                                LogFile.Log(Partition.Name.PadRight(20) + "0x" + Partition.FirstSector.ToString("X8") + " - 0x" + Partition.LastSector.ToString("X8") + "    " + Partition.Volume, LogType.ConsoleOnly);
                            }

                            Notifier.Stop();
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("ReadGPT");
                        }
                        break;
                    case "backupgpt":
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -BackupGPT <Path to xml-file>");
                        }

                        LogFile.BeginAction("BackupGPT");
                        try
                        {
                            Notifier = new PhoneNotifierViewModel();
                            UIContext.Send(s => Notifier.Start(), null);
                            BootMgrModel = (LumiaBootManagerAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Bootloader);
                            GPT GPT = BootMgrModel.ReadGPT(); // May throw NotSupportedException
                            string DirPath = Path.GetDirectoryName(args[2]);
                            if (!string.IsNullOrEmpty(DirPath) && !Directory.Exists(DirPath))
                            {
                                Directory.CreateDirectory(DirPath);
                            }
                            GPT.WritePartitions(args[2]);
                            Notifier.Stop();
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("BackupGPT");
                        }
                        break;
                    case "convertgpt":
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -ConvertGPT <Path to GPT-file> <Path to xml-file>");
                        }

                        LogFile.BeginAction("ConvertGPT");
                        try
                        {
                            using var stream = File.OpenRead(args[2]);
                            byte[] GPTBuffer = new byte[34 * 0x200];
                            stream.Read(GPTBuffer, 0, 34 * 0x200);
                            GPT GPT = new(GPTBuffer);// May throw NotSupportedException
                            string DirPath = Path.GetDirectoryName(args[3]);
                            if (!string.IsNullOrEmpty(DirPath) && !Directory.Exists(DirPath))
                            {
                                Directory.CreateDirectory(DirPath);
                            }
                            GPT.WritePartitions(args[3]);
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("ConvertGPT");
                        }
                        break;
                    case "restoregpt":
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -RestoreGPT <Path to xml-file>");
                        }

                        LogFile.BeginAction("RestoreGPT");
                        try
                        {
                            Notifier = new PhoneNotifierViewModel();
                            UIContext.Send(s => Notifier.Start(), null);
                            BootMgrModel = (LumiaBootManagerAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Bootloader);
                            byte[] GptChunk = BootMgrModel.GetGptChunk(0x20000);
                            GPT GPT = new(GptChunk);
                            string Xml = File.ReadAllText(args[2]);
                            GPT.MergePartitions(Xml, false);
                            GPT.Rebuild();
                            await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(Notifier, null, false, false, 0, GptChunk, true, true);
                            Notifier.Stop();
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("RestoreGPT");
                        }
                        break;
                    case "mergegpt":
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -MergeGPT <Path to input-xml-file> <Path to input-xml-file> <Optional: Path to output-xml-file> Or use: WPinternals.exe -MergeGPT <Path to input-xml-file> <Path to ZIP-file including Partitions.xml> <Optional: Path to output-xml-file>");
                        }

                        LogFile.BeginAction("MergeGPT");
                        try
                        {
                            GPT GPT = GPT.ReadPartitions(args[2]);

                            ZipArchive Archive = null;
                            FileStream s = null;
                            try
                            {
                                s = new FileStream(args[3], FileMode.Open, FileAccess.Read);
                                Archive = new ZipArchive(s);
                            }
                            catch (Exception ex)
                            {
                                LogFile.LogException(ex, LogType.FileOnly);
                            }

                            if (Archive == null)
                            {
                                s?.Close();

                                // Assume Xml-file
                                GPT.MergePartitionsFromFile(args[3], true);
                            }
                            else
                            {
                                ZipArchiveEntry PartitionEntry = Archive.GetEntry("Partitions.xml");
                                if (PartitionEntry == null)
                                {
                                    GPT.MergePartitions(null, true, Archive);
                                }
                                else
                                {
                                    using Stream ZipStream = PartitionEntry.Open();
                                    using StreamReader ZipReader = new(ZipStream);
                                    string PartitionXml = ZipReader.ReadToEnd();
                                    GPT.MergePartitions(PartitionXml, true, Archive);
                                }
                            }

                            Archive?.Dispose();

                            if (args.Length >= 5)
                            {
                                GPT.WritePartitions(args[4]);
                            }

                            foreach (Partition Partition in GPT.Partitions)
                            {
                                LogFile.Log(Partition.Name.PadRight(20) + "0x" + Partition.FirstSector.ToString("X8") + " - 0x" + Partition.LastSector.ToString("X8") + "    " + Partition.Volume, LogType.ConsoleOnly);
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("MergeGPT");
                        }
                        break;
                    case "dumpffu":
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -DumpFFU <FFU Path> <Destination Path> <Optional: Partition Name>");
                        }

                        FFU = new FFU(args[2]);
                        if (args.Length < 5)
                        {
                            foreach (Partition Partition in FFU.GPT.Partitions)
                            {
                                if (FFU.IsPartitionPresentInFFU(Partition.Name))
                                {
                                    FFU.WritePartition(Partition.Name, Path.Combine(args[3], Partition.Name + ".bin"));
                                }
                            }
                        }
                        else
                        {
                            Partition Target = FFU.GPT.GetPartition(args[4]);
                            if ((Target == null) || (!FFU.IsPartitionPresentInFFU(Target.Name)))
                            {
                                throw new InvalidOperationException("Partition not found in FFU!");
                            }

                            FFU.WritePartition(Target.Name, Path.Combine(args[3], Target.Name + ".bin"));
                        }
                        break;
                    case "dumpuefi":
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -DumpUEFI <UEFI binary or FFU file> <Destination Path>");
                        }

                        byte[] UefiBinary;
                        if (FFU.IsFFU(args[2]))
                        {
                            FFU = new FFU(args[2]);
                            UefiBinary = FFU.GetPartition("UEFI");
                        }
                        else
                        {
                            UefiBinary = File.ReadAllBytes(args[2]);
                        }

                        UEFI UEFI = new(UefiBinary);

                        foreach (EFI Efi in UEFI.EFIs)
                        {
                            byte[] EfiBinary = UEFI.GetFile(Efi.Guid);
                            string Name = Efi.Name ?? Efi.Guid.ToString();

                            if (!Name.Contains('.'))
                            {
                                Name += Efi.Type switch
                                {
                                    5 or 7 => ".dll",
                                    9 => ".exe",
                                    _ => ".bin",
                                };
                            }
                            string EfiPath = Path.Combine(args[3], Name);
                            string DirPath = Path.GetDirectoryName(EfiPath);
                            if (!string.IsNullOrEmpty(DirPath) && !Directory.Exists(DirPath))
                            {
                                Directory.CreateDirectory(DirPath);
                            }
                            File.WriteAllBytes(EfiPath, EfiBinary);
                        }
                        break;
                    case "testprogrammer":
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -TestProgrammer <Path to ede-file>");
                        }

                        await TestCode.TestProgrammer(UIContext, args[2]);
                        break;
                    case "findflashingprofile":
                        Notifier = new PhoneNotifierViewModel();
                        UIContext.Send(s => Notifier.Start(), null);
                        if (args.Length > 2)
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FindFlashingProfile(Notifier, args[2]);
                        }
                        else
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FindFlashingProfile(Notifier, null);
                        }

                        Notifier.Stop();
                        break;
                    case "findflashingprofileexperimental":
                        Notifier = new PhoneNotifierViewModel();
                        UIContext.Send(s => Notifier.Start(), null);
                        if (args.Length > 2)
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FindFlashingProfile(Notifier, args[2], Experimental: true);
                        }
                        else
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FindFlashingProfile(Notifier, null, Experimental: true);
                        }

                        Notifier.Stop();
                        break;
                    case "findflashingprofilenorestart":
                        Notifier = new PhoneNotifierViewModel();
                        UIContext.Send(s => Notifier.Start(), null);
                        if (args.Length > 2)
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FindFlashingProfile(Notifier, args[2], false);
                        }
                        else
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2FindFlashingProfile(Notifier, null, false);
                        }

                        Notifier.Stop();
                        break;
                    case "enabletestsigning":
                        if (args.Length > 2)
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2EnableTestSigning(UIContext, args[2]);
                        }
                        else
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2EnableTestSigning(UIContext, null);
                        }

                        break;
                    case "enabletestsigningnorestart":
                        if (args.Length > 2)
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2EnableTestSigning(UIContext, args[2], false);
                        }
                        else
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2EnableTestSigning(UIContext, null, false);
                        }

                        break;
                    case "clearnv":
                        if (args.Length > 2)
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2ClearNV(UIContext, args[2]);
                        }
                        else
                        {
                            await LumiaV2UnlockBootViewModel.LumiaV2ClearNV(UIContext, null);
                        }

                        break;
                    case "switchtomassstoragemode":
                        LogFile.BeginAction("SwitchToMassStorageMode");
                        try
                        {
                            Notifier = new PhoneNotifierViewModel();
                            UIContext.Send(s => Notifier.Start(), null);
                            LogFile.Log("Command: Switch to Mass Storage Mode", LogType.FileAndConsole);
                            if (args.Length > 2)
                            {
                                await LumiaV2UnlockBootViewModel.LumiaV2SwitchToMassStorageMode(Notifier, args[2]);
                            }
                            else
                            {
                                await LumiaV2UnlockBootViewModel.LumiaV2SwitchToMassStorageMode(Notifier, null);
                            }

                            Notifier.Stop();
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("SwitchToMassStorageMode");
                        }
                        break;
                    case "relockphone":
                        Notifier = new PhoneNotifierViewModel();
                        try
                        {
                            UIContext.Send(s => Notifier.Start(), null);
                            FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                            FlashInfo = FlashModel.ReadPhoneInfo();
                            FlashInfo.Log(LogType.ConsoleOnly);

                            FFU ProfileFFU = null;
                            FFU CurrentFFU;
                            for (int i = 2; i <= 3; i++)
                            {
                                if (args.Length > i)
                                {
                                    CurrentFFU = new FFU(args[i]);
                                    string CurrentVersion = CurrentFFU.GetOSVersion();
                                    string PlatformID = CurrentFFU.PlatformID;

                                    // Check if the current FFU matches the connected phone, so that the FFU can be used for profiling.
                                    if (FlashInfo.PlatformID.StartsWith(PlatformID, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ProfileFFU = CurrentFFU;
                                    }
                                }
                            }

                            if (ProfileFFU == null)
                            {
                                List<FFUEntry> FFUs = App.Config.FFURepository.Where(e => FlashInfo.PlatformID.StartsWith(e.PlatformID, StringComparison.OrdinalIgnoreCase) && e.Exists()).ToList();
                                ProfileFFU = FFUs.Count > 0
                                    ? new FFU(FFUs[0].Path)
                                    : throw new WPinternalsException("Profile FFU missing", "No profile FFU has been found in the repository for your device. You can add a profile FFU within the download section of the tool or by using the command line.");
                            }
                            LogFile.Log("Profile FFU: " + ProfileFFU.Path);

                            UIContext.Send(s => Notifier.Start(), null);

                            await LumiaUnlockBootloaderViewModel.LumiaRelockUEFI(Notifier, ProfileFFU.Path);
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        Notifier.Stop();
                        break;
                    case "addffu":
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -AddFFU <Path to FFU-file>");
                        }

                        App.Config.AddFfuToRepository(args[2]);
                        break;
                    case "removeffu":
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -RemoveFFU <Path to FFU-file>");
                        }

                        App.Config.RemoveFfuFromRepository(args[2]);
                        break;
                    case "addemergency":
                        if (args.Length < 5)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -AddEmergnency <Type> <Path to Programmer-file> <Path to Payload-file>");
                        }

                        App.Config.AddEmergencyToRepository(args[2], args[3], args[4]);
                        break;
                    case "removeemergency":
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -RemoveEmergency <Path to Programmer-file>");
                        }

                        App.Config.RemoveEmergencyFromRepository(args[2]);
                        break;
                    case "listrepository":
                        int Count = 0;
                        LogFile.Log("", LogType.ConsoleOnly);
                        LogFile.Log("FFU Repository:", LogType.ConsoleOnly);
                        foreach (FFUEntry Entry in App.Config.FFURepository)
                        {
                            LogFile.Log("", LogType.ConsoleOnly);
                            LogFile.Log("FFU " + Count.ToString() + ":", LogType.ConsoleOnly);
                            LogFile.Log("File: " + Entry.Path + (Entry.Exists() ? "" : " (file is missing)"), LogType.ConsoleOnly);
                            LogFile.Log("Platform ID: " + Entry.PlatformID, LogType.ConsoleOnly);
                            if (Entry.FirmwareVersion != null)
                            {
                                LogFile.Log("Firmware version: " + Entry.FirmwareVersion, LogType.ConsoleOnly);
                            }

                            if (Entry.OSVersion != null)
                            {
                                LogFile.Log("OS version: " + Entry.OSVersion, LogType.ConsoleOnly);
                            }

                            Count++;
                        }
                        LogFile.Log("", LogType.ConsoleOnly);
                        LogFile.Log("Emergency Repository:", LogType.ConsoleOnly);
                        Count = 0;
                        foreach (EmergencyFileEntry Entry in App.Config.EmergencyRepository)
                        {
                            LogFile.Log("", LogType.ConsoleOnly);
                            LogFile.Log("Emergency " + Count.ToString() + ":", LogType.ConsoleOnly);
                            LogFile.Log("Type: " + Entry.Type, LogType.ConsoleOnly);
                            LogFile.Log("Programmer file: " + Entry.ProgrammerPath + (Entry.ProgrammerExists() ? "" : " (file is missing)"), LogType.ConsoleOnly);
                            if (Entry.PayloadPath != null)
                            {
                                LogFile.Log("Payload file: " + Entry.PayloadPath + (Entry.PayloadExists() ? "" : " (file is missing)"), LogType.ConsoleOnly);
                            }

                            Count++;
                        }
                        break;
                    case "showffu":
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -ShowFFU <Path to FFU-file>");
                        }

                        string FFUPath = args[2];
                        LogFile.Log("FFU: " + FFUPath, LogType.ConsoleOnly);
                        FFU = new FFU(FFUPath);

                        // Show basics
                        LogFile.Log("Platform ID: " + FFU.PlatformID, LogType.ConsoleOnly);
                        string Firmware = FFU.GetFirmwareVersion();
                        if (Firmware != null)
                        {
                            LogFile.Log("Firmware version: " + Firmware, LogType.ConsoleOnly);
                        }

                        string OSVersion = FFU.GetOSVersion();
                        if (OSVersion != null)
                        {
                            LogFile.Log("OS version: " + OSVersion, LogType.ConsoleOnly);
                        }

                        // Show partitions from GPT (also show which partitions are in the FFU payload)
                        LogFile.Log("", LogType.ConsoleOnly);
                        LogFile.Log("Partition table:", LogType.ConsoleOnly);
                        LogFile.Log("Name".PadRight(20) + "Start-sector".PadRight(20) + "End-sector".PadRight(20) + "Present in FFU", LogType.ConsoleOnly);
                        foreach (Partition p in FFU.GPT.Partitions)
                        {
                            LogFile.Log(p.Name.PadRight(20) + ("0x" + p.FirstSector.ToString("X16")).PadRight(20) + ("0x" + p.LastSector.ToString("X16")).PadRight(20) + (FFU.IsPartitionPresentInFFU(p.Name) ? "Yes" : "No"), LogType.ConsoleOnly);
                        }
                        break;
                    case "showphoneinfo":
                        LogFile.Log("Command: Show phone info", LogType.FileAndConsole);
                        Notifier = new PhoneNotifierViewModel();
                        UIContext.Send(s => Notifier.Start(), null);
                        FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                        FlashInfo = FlashModel.ReadPhoneInfo();
                        FlashInfo.Log(LogType.ConsoleOnly);
                        Notifier.Stop();
                        break;
                    case "unlockbootloader":
                        LogFile.BeginAction("UnlockBootloader");
                        try
                        {
                            LogFile.Log("Command: Unlock Bootloader", LogType.FileAndConsole);
                            Notifier = new PhoneNotifierViewModel();
                            UIContext.Send(s => Notifier.Start(), null);
                            FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                            FlashInfo = FlashModel.ReadPhoneInfo();
                            FlashInfo.Log(LogType.ConsoleOnly);

                            FFU ProfileFFU = null;
                            FFU SupportedFFU = null;
                            FFU CurrentFFU;
                            for (int i = 2; i <= 3; i++)
                            {
                                if (args.Length > i)
                                {
                                    CurrentFFU = new FFU(args[i]);
                                    string CurrentVersion = CurrentFFU.GetOSVersion();
                                    string PlatformID = CurrentFFU.PlatformID;

                                    // Check if the current FFU matches the connected phone, so that the FFU can be used for profiling.
                                    if (FlashInfo.PlatformID.StartsWith(PlatformID, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ProfileFFU = CurrentFFU;
                                    }

                                    // Check if the current FFU is supported for unlocking.
                                    if (App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == CurrentVersion))
                                    {
                                        SupportedFFU = CurrentFFU;
                                    }
                                }
                            }

                            if (ProfileFFU == null)
                            {
                                List<FFUEntry> FFUs = App.Config.FFURepository.Where(e => FlashInfo.PlatformID.StartsWith(e.PlatformID, StringComparison.OrdinalIgnoreCase) && e.Exists()).ToList();
                                ProfileFFU = FFUs.Count > 0
                                    ? new FFU(FFUs[0].Path)
                                    : throw new WPinternalsException("Profile FFU missing", "No profile FFU has been found in the repository for your device. You can add a profile FFU within the download section of the tool or by using the command line.");
                            }
                            LogFile.Log("Profile FFU: " + ProfileFFU.Path);

                            if (SupportedFFU == null)
                            {
                                List<FFUEntry> FFUs = App.Config.FFURepository.Where(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)).ToList();
                                SupportedFFU = FFUs.Count > 0
                                    ? new FFU(FFUs[0].Path)
                                    : throw new WPinternalsException("No donor-FFU found with supported OS version", "No donor-FFU has been found in the repository with a supported OS version. You can add a donor-FFU within the download section of the tool or by using the command line. A donor-FFU can be for a different device and a different CPU than your device. It is only used to gather Operating System specific binaries to be patched and used as part of the unlock process.");
                            }

                            await LumiaUnlockBootloaderViewModel.LumiaUnlockUEFI(Notifier, ProfileFFU.Path, null, SupportedFFU.Path);

                            Notifier.Stop();
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("UnlockBootloader");
                        }
                        break;
                    case "flashcustomrom":
                        LogFile.BeginAction("FlashCustomROM");
                        try
                        {
                            LogFile.Log("Command: Flash Custom ROM", LogType.FileAndConsole);
                            if (args.Length < 3)
                            {
                                throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -FlashCustomROM <Path to Custom ROM ZIP-file>");
                            }

                            string CustomRomPath = args[2];
                            LogFile.Log("Custom ROM: " + CustomRomPath, LogType.FileAndConsole);
                            Notifier = new PhoneNotifierViewModel();
                            UIContext.Send(s => Notifier.Start(), null);
                            FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                            FlashInfo = FlashModel.ReadPhoneInfo();
                            FlashInfo.Log(LogType.ConsoleOnly);
                            LogFile.Log("Preparing to flash Custom ROM", LogType.FileAndConsole);
                            await LumiaV2UnlockBootViewModel.LumiaV2FlashArchive(Notifier, CustomRomPath);
                            LogFile.Log("Custom ROM flashed successfully", LogType.FileAndConsole);
                            Notifier.Stop();
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("FlashCustomROM");
                        }
                        break;
                    case "flashffu":
                        LogFile.BeginAction("FlashFFU");
                        try
                        {
                            LogFile.Log("Command: Flash FFU", LogType.FileAndConsole);
                            if (args.Length < 3)
                            {
                                throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -FlashFFU <Path to FFU-file>");
                            }

                            FFUPath = args[2];
                            LogFile.Log("FFU file: " + FFUPath, LogType.FileAndConsole);
                            Notifier = new PhoneNotifierViewModel();
                            UIContext.Send(s => Notifier.Start(), null);
                            FlashModel = (LumiaFlashAppModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Flash);
                            FlashInfo = FlashModel.ReadPhoneInfo();
                            FlashInfo.Log(LogType.ConsoleOnly);
                            LogFile.Log("Flashing FFU...", LogType.FileAndConsole);
                            await Task.Run(() => FlashModel.FlashFFU(new FFU(FFUPath), true, (byte)(!FlashInfo.IsBootloaderSecure ? FlashOptions.SkipSignatureCheck : 0)));
                            LogFile.Log("FFU flashed successfully", LogType.FileAndConsole);
                            Notifier.Stop();
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("FlashFFU");
                        }
                        break;
                    case "fixbootafterunlockingbootloader":
                        LogFile.BeginAction("FixBoot");
                        try
                        {
                            LogFile.Log("Command: Fix boot after unlocking bootloader", LogType.FileAndConsole);
                            Notifier = new PhoneNotifierViewModel();
                            UIContext.Send(s => Notifier.Start(), null);
                            string Drive = await LumiaV2UnlockBootViewModel.LumiaV2SwitchToMassStorageMode(Notifier, null);
                            Notifier.Stop();
                            App.PatchEngine.TargetPath = Drive + "\\";
                            PatchResult = App.PatchEngine.Patch("SecureBootHack-MainOS");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Patch failed", "An error occured while patching Operating System files on the MainOS partition of your phone. Make sure your phone runs a supported Operating System version.");
                            }

                            LogFile.Log("Fixed bootloader", LogType.FileAndConsole);
                            LogFile.Log("The phone is left in Mass Storage mode", LogType.FileAndConsole);
                            LogFile.Log("Press and hold the power-button of the phone for at least 10 seconds to reset the phone", LogType.FileAndConsole);
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("FixBoot");
                        }
                        break;
                    case "enablerootaccess":
                        LogFile.BeginAction("EnableRootAccess");
                        try
                        {
                            LogFile.Log("Command: Enable root access on the phone", LogType.FileAndConsole);
                            Notifier = new PhoneNotifierViewModel();
                            UIContext.Send(s => Notifier.Start(), null);
                            string Drive = await LumiaV2UnlockBootViewModel.LumiaV2SwitchToMassStorageMode(Notifier, null);
                            Notifier.Stop();
                            App.PatchEngine.TargetPath = Drive + "\\EFIESP\\";
                            PatchResult = App.PatchEngine.Patch("SecureBootHack-V2-EFIESP");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Patch failed", "An error occured while patching Operating System files on the EFIESP partition of your phone. Make sure no boot files have been tampered with and you use the latest version of the tool. This error cannot be caused by an incorrect Operating System version as the tool automatically uses replacement if the version isn't supported.");
                            }

                            App.PatchEngine.TargetPath = Drive + "\\";
                            PatchResult = App.PatchEngine.Patch("SecureBootHack-MainOS");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Patch failed", "An error occured while patching Operating System files on the MainOS partition of your phone. Make sure your phone runs a supported Operating System version.");
                            }

                            PatchResult = App.PatchEngine.Patch("RootAccess-MainOS");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Patch failed", "An error occured while modifying Operating System files on the MainOS partition of your phone for Root Access. Make sure your phone runs a supported Operating System version.");
                            }

                            LogFile.Log("Root Access enabled!", LogType.FileAndConsole);
                            LogFile.Log("The phone is left in Mass Storage mode", LogType.FileAndConsole);
                            LogFile.Log("Press and hold the power-button of the phone for at least 10 seconds to reset the phone", LogType.FileAndConsole);
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                        }
                        finally
                        {
                            LogFile.EndAction("EnableRootAccess");
                        }
                        break;
                    case "unlockbootloaderonimage":
                        LogFile.Log("Command: Unlock bootloader on image", LogType.FileAndConsole);
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -UnlockBootLoaderOnImage <EFIESP image file> <Optional: MainOS image file> <Optional: donor-FFU file with supported version of bootfiles>");
                        }

                        FFUFilePath = null;
                        EfiEspImagePath = args[2];
                        if (args.Length > 3)
                        {
                            if (FFU.IsFFU(args[3]))
                            {
                                FFUFilePath = args[3];
                            }
                            else
                            {
                                MainOsImagePath = args[3];
                            }
                        }
                        if (args.Length > 4)
                        {
                            FFUFilePath = args[4];
                        }

                        using (FileStream FileSystemStream = new(EfiEspImagePath, FileMode.Open, FileAccess.ReadWrite))
                        {
                            UnlockedEFIESPFileSystem = new DiscUtils.Fat.FatFileSystem(FileSystemStream);

                            if (FFUFilePath != null)
                            {
                                FFU SupportedFFU = new(FFUFilePath);
                                LogFile.Log("Donor FFU: " + SupportedFFU.Path);
                                byte[] SupportedEFIESP = SupportedFFU.GetPartition("EFIESP");
                                DiscUtils.Fat.FatFileSystem SupportedEFIESPFileSystem = new(new MemoryStream(SupportedEFIESP));
                                DiscUtils.Streams.SparseStream SupportedMobileStartupStream = SupportedEFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Open);
                                MemoryStream SupportedMobileStartupMemStream = new();
                                SupportedMobileStartupStream.CopyTo(SupportedMobileStartupMemStream);
                                byte[] SupportedMobileStartup = SupportedMobileStartupMemStream.ToArray();
                                SupportedMobileStartupMemStream.Close();
                                SupportedMobileStartupStream.Close();

                                // Save supported mobilestartup.efi
                                LogFile.Log("Taking mobilestartup.efi from donor-FFU");
                                Stream MobileStartupStream = UnlockedEFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Create, FileAccess.Write);
                                MobileStartupStream.Write(SupportedMobileStartup, 0, SupportedMobileStartup.Length);
                                MobileStartupStream.Close();
                            }

                            App.PatchEngine.TargetImage = UnlockedEFIESPFileSystem;
                            PatchResult = App.PatchEngine.Patch("SecureBootHack-V2-EFIESP");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Failed to patch bootloader", "An error occured while patching Operating System files on the EFIESP partition provided. Make sure no boot files have been tampered with and you use the latest version of the tool. This error cannot be caused by an incorrect Operating System version as the tool automatically uses replacement if the version isn't supported, unless the replacement files have been tampered with or are not compatible.");
                            }

                            // Edit BCD
                            LogFile.Log("Edit BCD");
                            using Stream BCDFileStream = UnlockedEFIESPFileSystem.OpenFile(@"\efi\Microsoft\Boot\BCD", FileMode.Open, FileAccess.ReadWrite);
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

                        if (MainOsImagePath != null)
                        {
                            using FileStream FileSystemStream = new(MainOsImagePath, FileMode.Open, FileAccess.ReadWrite);
                            UnlockedMainOsFileSystem = new DiscUtils.Ntfs.NtfsFileSystem(FileSystemStream);

                            App.PatchEngine.TargetImage = UnlockedMainOsFileSystem;
                            PatchResult = App.PatchEngine.Patch("SecureBootHack-MainOS");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Failed to patch MainOS", "An error occured while patching Operating System files on the MainOS partition you provided. Make sure your phone runs a supported Operating System version.");
                            }
                        }
                        LogFile.Log("Bootloader unlocked on image", LogType.FileAndConsole);
                        break;
                    case "enablerootaccessonimage":
                        LogFile.Log("Command: Enable root access on image", LogType.FileAndConsole);
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -EnableRootAccessOnImage <EFIESP image file> <MainOS image file> <Optional: donor-FFU file with supported version of bootfiles>");
                        }

                        FFUFilePath = null;
                        EfiEspImagePath = args[2];
                        MainOsImagePath = args[3];
                        if (args.Length > 4)
                        {
                            FFUFilePath = args[4];
                        }

                        using (FileStream FileSystemStream = new(EfiEspImagePath, FileMode.Open, FileAccess.ReadWrite))
                        {
                            UnlockedEFIESPFileSystem = new DiscUtils.Fat.FatFileSystem(FileSystemStream);

                            if (FFUFilePath != null)
                            {
                                FFU SupportedFFU = new(FFUFilePath);
                                LogFile.Log("Supported FFU: " + SupportedFFU.Path);
                                byte[] SupportedEFIESP = SupportedFFU.GetPartition("EFIESP");
                                DiscUtils.Fat.FatFileSystem SupportedEFIESPFileSystem = new(new MemoryStream(SupportedEFIESP));
                                DiscUtils.Streams.SparseStream SupportedMobileStartupStream = SupportedEFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Open);
                                MemoryStream SupportedMobileStartupMemStream = new();
                                SupportedMobileStartupStream.CopyTo(SupportedMobileStartupMemStream);
                                byte[] SupportedMobileStartup = SupportedMobileStartupMemStream.ToArray();
                                SupportedMobileStartupMemStream.Close();
                                SupportedMobileStartupStream.Close();

                                // Save supported mobilestartup.efi
                                LogFile.Log("Taking mobilestartup.efi from donor-FFU");
                                Stream MobileStartupStream = UnlockedEFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Create, FileAccess.Write);
                                MobileStartupStream.Write(SupportedMobileStartup, 0, SupportedMobileStartup.Length);
                                MobileStartupStream.Close();
                            }

                            App.PatchEngine.TargetImage = UnlockedEFIESPFileSystem;
                            PatchResult = App.PatchEngine.Patch("SecureBootHack-V2-EFIESP");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Failed to patch bootloader", "An error occured while patching Operating System files on the EFIESP partition provided. Make sure no boot files have been tampered with and you use the latest version of the tool. This error cannot be caused by an incorrect Operating System version as the tool automatically uses replacement if the version isn't supported, unless the replacement files have been tampered with or are not compatible.");
                            }

                            // Edit BCD
                            LogFile.Log("Edit BCD");
                            using Stream BCDFileStream = UnlockedEFIESPFileSystem.OpenFile(@"\efi\Microsoft\Boot\BCD", FileMode.Open, FileAccess.ReadWrite);
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

                        using (FileStream FileSystemStream = new(MainOsImagePath, FileMode.Open, FileAccess.ReadWrite))
                        {
                            UnlockedMainOsFileSystem = new DiscUtils.Ntfs.NtfsFileSystem(FileSystemStream);

                            App.PatchEngine.TargetImage = UnlockedMainOsFileSystem;
                            PatchResult = App.PatchEngine.Patch("SecureBootHack-MainOS");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Failed to patch MainOS", "An error occured while patching Operating System files on the MainOS partition you provided. Make sure your phone runs a supported Operating System version.");
                            }

                            PatchResult = App.PatchEngine.Patch("RootAccess-MainOS");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Failed to patch MainOS", "An error occured while modifying Operating System files on the MainOS partition you provided for Root Access. Make sure your phone runs a supported Operating System version.");
                            }
                        }
                        LogFile.Log("Root access enabled on image", LogType.FileAndConsole);
                        break;
                    case "unlockbootloaderonmountedimage":
                        LogFile.Log("Command: Unlock bootloader on mounted image", LogType.FileAndConsole);
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -UnlockBootLoaderOnMountedImage <Directory of mounted EFIESP image file> <Optional: Directory of mounted MainOS image file> <Optional: donor-FFU file with supported version of bootfiles>");
                        }

                        FFUFilePath = null;
                        EfiEspImagePath = args[2];
                        if (args.Length > 3)
                        {
                            if (FFU.IsFFU(args[3]))
                            {
                                FFUFilePath = args[3];
                            }
                            else
                            {
                                MainOsImagePath = args[3];
                            }
                        }
                        if (args.Length > 4)
                        {
                            FFUFilePath = args[4];
                        }

                        if (FFUFilePath != null)
                        {
                            FFU SupportedFFU = new(FFUFilePath);
                            LogFile.Log("Donor-FFU: " + SupportedFFU.Path);
                            byte[] SupportedEFIESP = SupportedFFU.GetPartition("EFIESP");
                            DiscUtils.Fat.FatFileSystem SupportedEFIESPFileSystem = new(new MemoryStream(SupportedEFIESP));
                            DiscUtils.Streams.SparseStream SupportedMobileStartupStream = SupportedEFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Open);
                            MemoryStream SupportedMobileStartupMemStream = new();
                            SupportedMobileStartupStream.CopyTo(SupportedMobileStartupMemStream);
                            byte[] SupportedMobileStartup = SupportedMobileStartupMemStream.ToArray();
                            SupportedMobileStartupMemStream.Close();
                            SupportedMobileStartupStream.Close();

                            // Save supported mobilestartup.efi
                            LogFile.Log("Taking mobilestartup.efi from donor-FFU");
                            Stream MobileStartupStream = File.Open(Path.Combine(EfiEspImagePath, @"Windows\System32\Boot\mobilestartup.efi"), FileMode.Create, FileAccess.Write);
                            MobileStartupStream.Write(SupportedMobileStartup, 0, SupportedMobileStartup.Length);
                            MobileStartupStream.Close();
                        }

                        App.PatchEngine.TargetPath = EfiEspImagePath;
                        PatchResult = App.PatchEngine.Patch("SecureBootHack-V2-EFIESP");
                        if (!PatchResult)
                        {
                            throw new WPinternalsException("Failed to patch bootloader", "An error occured while patching Operating System files on the EFIESP partition provided. Make sure no boot files have been tampered with and you use the latest version of the tool. This error cannot be caused by an incorrect Operating System version as the tool automatically uses replacement if the version isn't supported, unless the replacement files have been tampered with or are not compatible.");
                        }

                        // Edit BCD
                        LogFile.Log("Edit BCD");
                        using (Stream BCDFileStream = File.Open(Path.Combine(EfiEspImagePath, @"efi\Microsoft\Boot\BCD"), FileMode.Open, FileAccess.ReadWrite))
                        {
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

                        if (MainOsImagePath != null)
                        {
                            App.PatchEngine.TargetPath = MainOsImagePath;
                            PatchResult = App.PatchEngine.Patch("SecureBootHack-MainOS");
                            if (!PatchResult)
                            {
                                throw new WPinternalsException("Failed to patch MainOS", "An error occured while patching Operating System files on the MainOS partition you provided. Make sure your phone runs a supported Operating System version.");
                            }
                        }
                        LogFile.Log("Bootloader unlocked on image", LogType.FileAndConsole);
                        break;
                    case "enablerootaccessonmountedimage":
                        LogFile.Log("Command: Enable root access on mounted image", LogType.FileAndConsole);
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -EnableRootAccessOnMountedImage <Directory of mounted EFIESP image file> <Directory of mounted MainOS image file> <Optional: donor-FFU file with supported version of bootfiles>");
                        }

                        FFUFilePath = null;
                        EfiEspImagePath = args[2];
                        MainOsImagePath = args[3];
                        if (args.Length > 4)
                        {
                            FFUFilePath = args[4];
                        }

                        if (FFUFilePath != null)
                        {
                            FFU SupportedFFU = new(FFUFilePath);
                            LogFile.Log("Supported FFU: " + SupportedFFU.Path);
                            byte[] SupportedEFIESP = SupportedFFU.GetPartition("EFIESP");
                            DiscUtils.Fat.FatFileSystem SupportedEFIESPFileSystem = new(new MemoryStream(SupportedEFIESP));
                            DiscUtils.Streams.SparseStream SupportedMobileStartupStream = SupportedEFIESPFileSystem.OpenFile(@"\Windows\System32\Boot\mobilestartup.efi", FileMode.Open);
                            MemoryStream SupportedMobileStartupMemStream = new();
                            SupportedMobileStartupStream.CopyTo(SupportedMobileStartupMemStream);
                            byte[] SupportedMobileStartup = SupportedMobileStartupMemStream.ToArray();
                            SupportedMobileStartupMemStream.Close();
                            SupportedMobileStartupStream.Close();

                            // Save supported mobilestartup.efi
                            LogFile.Log("Taking mobilestartup.efi from donor-FFU");
                            Stream MobileStartupStream = File.Open(Path.Combine(EfiEspImagePath, @"Windows\System32\Boot\mobilestartup.efi"), FileMode.Create, FileAccess.Write);
                            MobileStartupStream.Write(SupportedMobileStartup, 0, SupportedMobileStartup.Length);
                            MobileStartupStream.Close();
                        }

                        App.PatchEngine.TargetPath = EfiEspImagePath;
                        PatchResult = App.PatchEngine.Patch("SecureBootHack-V2-EFIESP");
                        if (!PatchResult)
                        {
                            throw new WPinternalsException("Failed to patch bootloader", "An error occured while patching Operating System files on the EFIESP partition provided. Make sure no boot files have been tampered with and you use the latest version of the tool. This error cannot be caused by an incorrect Operating System version as the tool automatically uses replacement if the version isn't supported, unless the replacement files have been tampered with or are not compatible.");
                        }

                        // Edit BCD
                        LogFile.Log("Edit BCD");
                        using (Stream BCDFileStream = File.Open(Path.Combine(EfiEspImagePath, @"efi\Microsoft\Boot\BCD"), FileMode.Open, FileAccess.ReadWrite))
                        {
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

                        App.PatchEngine.TargetPath = MainOsImagePath;
                        PatchResult = App.PatchEngine.Patch("SecureBootHack-MainOS");
                        if (!PatchResult)
                        {
                            throw new WPinternalsException("Failed to patch MainOS", "An error occured while patching Operating System files on the MainOS partition you provided. Make sure your phone runs a supported Operating System version.");
                        }

                        PatchResult = App.PatchEngine.Patch("RootAccess-MainOS");
                        if (!PatchResult)
                        {
                            throw new WPinternalsException("Failed to patch MainOS", "An error occured while modifying Operating System files on the MainOS partition you provided for Root Access. Make sure your phone runs a supported Operating System version.");
                        }

                        LogFile.Log("Root access enabled on image", LogType.FileAndConsole);
                        break;
                    case "downloadffu":
                        LogFile.Log("Command: Download FFU", LogType.FileAndConsole);
                        Notifier = new PhoneNotifierViewModel();
                        UIContext.Send(s => Notifier.Start(), null);
                        if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Normal)
                        {
                            NormalModel = (NokiaPhoneModel)Notifier.CurrentModel;
                            ProductCode = NormalModel.ExecuteJsonMethodAsString("ReadProductCode", "ProductCode");
                        }
                        else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                        {
                            (Notifier.CurrentModel as LumiaBootManagerAppModel).SwitchToPhoneInfoAppContext();

                            if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                            {
                                await Notifier.WaitForArrival();
                            }

                            if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                            {
                                throw new WPinternalsException("Unexpected Mode");
                            }

                            PhoneInfoModel = (LumiaPhoneInfoAppModel)Notifier.CurrentModel;
                            PhoneInfo = PhoneInfoModel.ReadPhoneInfo();
                            ProductCode = PhoneInfo.ProductCode;
                        }
                        else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_PhoneInfo)
                        {
                            PhoneInfoModel = (LumiaPhoneInfoAppModel)Notifier.CurrentModel;
                            PhoneInfo = PhoneInfoModel.ReadPhoneInfo();
                            ProductCode = PhoneInfo.ProductCode;
                        }
                        else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash)
                        {
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

                            PhoneInfoModel = (LumiaPhoneInfoAppModel)Notifier.CurrentModel;
                            PhoneInfo = PhoneInfoModel.ReadPhoneInfo();
                            ProductCode = PhoneInfo.ProductCode;
                        }
                        else
                        {
                            NormalModel = (NokiaPhoneModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Normal);
                            ProductCode = NormalModel.ExecuteJsonMethodAsString("ReadProductCode", "ProductCode");
                        }
                        URL = LumiaDownloadModel.SearchFFU(null, ProductCode, null, out ProductType);
                        DownloadFolder = args.Length >= 3
                            ? args[4]
                            : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                        if (!Directory.Exists(DownloadFolder))
                        {
                            Directory.CreateDirectory(DownloadFolder);
                        }

                        LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                        LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                        URI = new Uri(URL);
                        FFUFileName = Path.GetFileName(URI.LocalPath);
                        LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                        FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                        LogFile.Log("Downloading...", LogType.FileAndConsole);
                        using (System.Net.WebClient myWebClient = new())
                        {
                            await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                        }
                        LogFile.Log("Download finished", LogType.FileAndConsole);
                        App.Config.AddFfuToRepository(FFUFilePath);
                        Notifier.Stop();
                        break;
                    case "downloadffubyoperatorcode":
                        LogFile.Log("Command: Download FFU by Operator Code", LogType.FileAndConsole);
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -DownloadFFUbyOperatorCode <Product type> <Operator code> <Optional: Download folder>");
                        }

                        ProductType = args[2];
                        LogFile.Log("Product type: " + ProductType, LogType.FileAndConsole);
                        OperatorCode = args[3];
                        LogFile.Log("Operator code: " + OperatorCode, LogType.FileAndConsole);
                        DownloadFolder = args.Length >= 5
                            ? args[4]
                            : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                        if (!Directory.Exists(DownloadFolder))
                        {
                            Directory.CreateDirectory(DownloadFolder);
                        }

                        LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                        URL = LumiaDownloadModel.SearchFFU(ProductType, null, OperatorCode);
                        LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                        URI = new Uri(URL);
                        FFUFileName = Path.GetFileName(URI.LocalPath);
                        LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                        FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                        LogFile.Log("Downloading...", LogType.FileAndConsole);
                        using (System.Net.WebClient myWebClient = new())
                        {
                            await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                        }
                        LogFile.Log("Download finished", LogType.FileAndConsole);
                        App.Config.AddFfuToRepository(FFUFilePath);
                        break;
                    case "downloadffubyproductcode":
                        LogFile.Log("Command: Download FFU by Product Code", LogType.FileAndConsole);
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -DownloadFFUbyProductCode <Product code> <Optional: Download folder>");
                        }

                        ProductCode = args[2];
                        LogFile.Log("Product code: " + ProductCode, LogType.FileAndConsole);
                        URL = LumiaDownloadModel.SearchFFU(null, ProductCode, null, out ProductType);
                        DownloadFolder = args.Length >= 4
                            ? args[3]
                            : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                        if (!Directory.Exists(DownloadFolder))
                        {
                            Directory.CreateDirectory(DownloadFolder);
                        }

                        LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                        LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                        URI = new Uri(URL);
                        FFUFileName = Path.GetFileName(URI.LocalPath);
                        LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                        FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                        LogFile.Log("Downloading...", LogType.FileAndConsole);
                        using (System.Net.WebClient myWebClient = new())
                        {
                            await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                        }
                        LogFile.Log("Download finished", LogType.FileAndConsole);
                        App.Config.AddFfuToRepository(FFUFilePath);
                        break;
                    case "downloadffubyproducttype":
                        LogFile.Log("Command: Download FFU by Product Type", LogType.FileAndConsole);
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -DownloadFFUbyProductType <Product type> <Optional: Download folder>");
                        }

                        ProductType = args[2];
                        LogFile.Log("Product type: " + ProductType, LogType.FileAndConsole);
                        DownloadFolder = args.Length >= 4
                            ? args[3]
                            : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                        if (!Directory.Exists(DownloadFolder))
                        {
                            Directory.CreateDirectory(DownloadFolder);
                        }

                        LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                        URL = LumiaDownloadModel.SearchFFU(ProductType, null, null);
                        LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                        URI = new Uri(URL);
                        FFUFileName = Path.GetFileName(URI.LocalPath);
                        LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                        FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                        LogFile.Log("Downloading...", LogType.FileAndConsole);
                        using (System.Net.WebClient myWebClient = new())
                        {
                            await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                        }
                        LogFile.Log("Download finished", LogType.FileAndConsole);
                        App.Config.AddFfuToRepository(FFUFilePath);
                        break;
                    case "searchffubyproducttype":
                        LogFile.Log("Command: Search FFU by Product Type", LogType.FileAndConsole);
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -SearchFFUbyProductType <Lumia model>");
                        }

                        ProductType = args[2];
                        LogFile.Log("Lumia model: " + ProductType, LogType.FileAndConsole);
                        URL = LumiaDownloadModel.SearchFFU(ProductType, null, null);
                        LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                        URI = new Uri(URL);
                        FFUFileName = Path.GetFileName(URI.LocalPath);
                        LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                        break;
                    case "downloademergency":
                        LogFile.Log("Command: Download Emergency files", LogType.FileAndConsole);
                        Notifier = new PhoneNotifierViewModel();
                        UIContext.Send(s => Notifier.Start(), null);
                        if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Normal)
                        {
                            NormalModel = (NokiaPhoneModel)Notifier.CurrentModel;
                            ProductType = NormalModel.ExecuteJsonMethodAsString("ReadManufacturerModelName", "ManufacturerModelName");
                            if (ProductType.Contains('_'))
                            {
                                ProductType = ProductType.Substring(0, ProductType.IndexOf('_'));
                            }
                        }
                        else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                        {
                            BootMgrModel = (LumiaBootManagerAppModel)Notifier.CurrentModel;
                            BootManagerInfo = BootMgrModel.ReadPhoneInfo();
                            //ProductType = BootManagerInfo.Type; // TODO: FIXME
                            ProductType = "";
                        }
                        else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash)
                        {
                            FlashModel = (LumiaFlashAppModel)Notifier.CurrentModel;
                            FlashInfo = FlashModel.ReadPhoneInfo();
                            //ProductType = FlashInfo.Type; // TODO: FIXME
                            ProductType = "";
                        }
                        else
                        {
                            NormalModel = (NokiaPhoneModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Normal);
                            ProductType = NormalModel.ExecuteJsonMethodAsString("ReadManufacturerModelName", "ManufacturerModelName");
                            if (ProductType.Contains('_'))
                            {
                                ProductType = ProductType.Substring(0, ProductType.IndexOf('_'));
                            }
                        }
                        URLs = LumiaDownloadModel.SearchEmergencyFiles(ProductType);
                        if (URLs != null)
                        {
                            DownloadFolder = args.Length >= 3
                                ? args[2]
                                : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                            if (!Directory.Exists(DownloadFolder))
                            {
                                Directory.CreateDirectory(DownloadFolder);
                            }

                            LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                            for (int i = 0; i < URLs.Length; i++)
                            {
                                LogFile.Log("URL: " + URLs[i], LogType.FileAndConsole);
                                URI = new Uri(URLs[i]);
                                EmergencyFileName = Path.GetFileName(URI.LocalPath);
                                LogFile.Log("File: " + EmergencyFileName, LogType.FileAndConsole);
                                EmergencyFilePath = Path.Combine(DownloadFolder, EmergencyFileName);
                                if (i == 0)
                                {
                                    ProgrammerPath = EmergencyFilePath;
                                }
                                else
                                {
                                    PayloadPath = EmergencyFilePath;
                                }

                                LogFile.Log("Downloading...", LogType.FileAndConsole);
                                using (System.Net.WebClient myWebClient = new())
                                {
                                    await myWebClient.DownloadFileTaskAsync(URLs[i], EmergencyFilePath);
                                }
                                LogFile.Log("Download finished", LogType.FileAndConsole);
                            }
                            App.Config.AddEmergencyToRepository(ProductType, ProgrammerPath, PayloadPath);
                        }
                        Notifier.Stop();
                        break;
                    case "downloademergencybyproducttype":
                        LogFile.Log("Command: Download Emergency files", LogType.FileAndConsole);
                        if (args.Length < 3)
                        {
                            throw new WPinternalsException("Wrong number of arguments. Usage: WPinternals.exe -DownloadEmergencyByProductType <Product type> <Optional: Download folder>");
                        }

                        ProductType = args[2];
                        URLs = LumiaDownloadModel.SearchEmergencyFiles(ProductType);
                        if (URLs != null)
                        {
                            DownloadFolder = args.Length >= 4
                                ? args[3]
                                : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                            if (!Directory.Exists(DownloadFolder))
                            {
                                Directory.CreateDirectory(DownloadFolder);
                            }

                            LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                            for (int i = 0; i < URLs.Length; i++)
                            {
                                LogFile.Log("URL: " + URLs[i], LogType.FileAndConsole);
                                URI = new Uri(URLs[i]);
                                EmergencyFileName = Path.GetFileName(URI.LocalPath);
                                LogFile.Log("File: " + EmergencyFileName, LogType.FileAndConsole);
                                EmergencyFilePath = Path.Combine(DownloadFolder, EmergencyFileName);
                                if (i == 0)
                                {
                                    ProgrammerPath = EmergencyFilePath;
                                }
                                else
                                {
                                    PayloadPath = EmergencyFilePath;
                                }

                                LogFile.Log("Downloading...", LogType.FileAndConsole);
                                using (System.Net.WebClient myWebClient = new())
                                {
                                    await myWebClient.DownloadFileTaskAsync(URLs[i], EmergencyFilePath);
                                }
                                LogFile.Log("Download finished", LogType.FileAndConsole);
                            }
                            App.Config.AddEmergencyToRepository(ProductType, ProgrammerPath, PayloadPath);
                        }
                        break;
                    case "downloadall":
                        LogFile.Log("Command: Download all", LogType.FileAndConsole);
                        Notifier = new PhoneNotifierViewModel();
                        UIContext.Send(s => Notifier.Start(), null);
                        if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Normal)
                        {
                            NormalModel = (NokiaPhoneModel)Notifier.CurrentModel;
                            ProductCode = NormalModel.ExecuteJsonMethodAsString("ReadProductCode", "ProductCode");
                        }
                        else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
                        {
                            BootMgrModel = (LumiaBootManagerAppModel)Notifier.CurrentModel;
                            BootManagerInfo = BootMgrModel.ReadPhoneInfo();
                            //ProductCode = BootManagerInfo.ProductCode; // TODO: FIXME
                            ProductCode = "";
                        }
                        else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash)
                        {
                            FlashModel = (LumiaFlashAppModel)Notifier.CurrentModel;
                            FlashInfo = FlashModel.ReadPhoneInfo();
                            //ProductCode = FlashInfo.ProductCode; // TODO: FIXME
                            ProductCode = "";
                        }
                        else
                        {
                            NormalModel = (NokiaPhoneModel)await SwitchModeViewModel.SwitchTo(Notifier, PhoneInterfaces.Lumia_Normal);
                            ProductCode = NormalModel.ExecuteJsonMethodAsString("ReadProductCode", "ProductCode");
                        }
                        URL = LumiaDownloadModel.SearchFFU(null, ProductCode, null, out ProductType);
                        DownloadFolder = args.Length >= 3
                            ? args[2]
                            : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                        if (!Directory.Exists(DownloadFolder))
                        {
                            Directory.CreateDirectory(DownloadFolder);
                        }

                        LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                        LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                        URI = new Uri(URL);
                        FFUFileName = Path.GetFileName(URI.LocalPath);
                        LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                        FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                        LogFile.Log("Downloading...", LogType.FileAndConsole);
                        using (System.Net.WebClient myWebClient = new())
                        {
                            await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                        }
                        LogFile.Log("Download finished", LogType.FileAndConsole);
                        App.Config.AddFfuToRepository(FFUFilePath);

                        URLs = LumiaDownloadModel.SearchEmergencyFiles(ProductType);
                        if (URLs != null)
                        {
                            for (int i = 0; i < URLs.Length; i++)
                            {
                                LogFile.Log("URL: " + URLs[i], LogType.FileAndConsole);
                                URI = new Uri(URLs[i]);
                                EmergencyFileName = Path.GetFileName(URI.LocalPath);
                                LogFile.Log("File: " + EmergencyFileName, LogType.FileAndConsole);
                                EmergencyFilePath = Path.Combine(DownloadFolder, EmergencyFileName);
                                if (i == 0)
                                {
                                    ProgrammerPath = EmergencyFilePath;
                                }
                                else
                                {
                                    PayloadPath = EmergencyFilePath;
                                }

                                LogFile.Log("Downloading...", LogType.FileAndConsole);
                                using (System.Net.WebClient myWebClient = new())
                                {
                                    await myWebClient.DownloadFileTaskAsync(URLs[i], EmergencyFilePath);
                                }
                                LogFile.Log("Download finished", LogType.FileAndConsole);
                            }
                            App.Config.AddEmergencyToRepository(ProductType, ProgrammerPath, PayloadPath);
                        }

                        if (!App.Config.FFURepository.Any(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                        {
                            ProductType = "RM-1085";

                            URL = LumiaDownloadModel.SearchFFU(ProductType, null, null);
                            DownloadFolder = args.Length >= 3
                                ? args[2]
                                : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                            if (!Directory.Exists(DownloadFolder))
                            {
                                Directory.CreateDirectory(DownloadFolder);
                            }

                            LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                            LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                            URI = new Uri(URL);
                            FFUFileName = Path.GetFileName(URI.LocalPath);
                            LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                            FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                            LogFile.Log("Downloading...", LogType.FileAndConsole);
                            using (System.Net.WebClient myWebClient = new())
                            {
                                await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                            }
                            LogFile.Log("Download finished", LogType.FileAndConsole);
                            App.Config.AddFfuToRepository(FFUFilePath);

                            if (!App.Config.FFURepository.Any(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                            {
                                throw new WPinternalsException("Unable to find compatible FFU", "No donor-FFU has been found in the repository with a supported OS version. You can add a donor-FFU within the download section of the tool or by using the command line. A donor-FFU can be for a different device and a different CPU than your device. It is only used to gather Operating System specific binaries to be patched and used as part of the unlock process.");
                            }
                        }
                        Notifier.Stop();
                        break;
                    case "downloadallbyproducttype":
                        LogFile.Log("Command: Download all by Product Type", LogType.FileAndConsole);
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -DownloadAllByProductType <Product type> <Optional: Download folder>");
                        }

                        ProductType = args[2];
                        LogFile.Log("Product type: " + ProductType, LogType.FileAndConsole);
                        DownloadFolder = args.Length >= 4
                            ? args[3]
                            : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                        if (!Directory.Exists(DownloadFolder))
                        {
                            Directory.CreateDirectory(DownloadFolder);
                        }

                        LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                        URL = LumiaDownloadModel.SearchFFU(ProductType, null, null);
                        LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                        URI = new Uri(URL);
                        FFUFileName = Path.GetFileName(URI.LocalPath);
                        LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                        FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                        LogFile.Log("Downloading...", LogType.FileAndConsole);
                        using (System.Net.WebClient myWebClient = new())
                        {
                            await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                        }
                        LogFile.Log("Download finished", LogType.FileAndConsole);
                        App.Config.AddFfuToRepository(FFUFilePath);

                        URLs = LumiaDownloadModel.SearchEmergencyFiles(ProductType);
                        if (URLs != null)
                        {
                            for (int i = 0; i < URLs.Length; i++)
                            {
                                LogFile.Log("URL: " + URLs[i], LogType.FileAndConsole);
                                URI = new Uri(URLs[i]);
                                EmergencyFileName = Path.GetFileName(URI.LocalPath);
                                LogFile.Log("File: " + EmergencyFileName, LogType.FileAndConsole);
                                EmergencyFilePath = Path.Combine(DownloadFolder, EmergencyFileName);
                                if (i == 0)
                                {
                                    ProgrammerPath = EmergencyFilePath;
                                }
                                else
                                {
                                    PayloadPath = EmergencyFilePath;
                                }

                                LogFile.Log("Downloading...", LogType.FileAndConsole);
                                using (System.Net.WebClient myWebClient = new())
                                {
                                    await myWebClient.DownloadFileTaskAsync(URLs[i], EmergencyFilePath);
                                }
                                LogFile.Log("Download finished", LogType.FileAndConsole);
                            }
                            App.Config.AddEmergencyToRepository(ProductType, ProgrammerPath, PayloadPath);
                        }

                        if (!App.Config.FFURepository.Any(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                        {
                            ProductType = "RM-1085";

                            URL = LumiaDownloadModel.SearchFFU(ProductType, null, null);
                            DownloadFolder = args.Length >= 4
                                ? args[3]
                                : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                            if (!Directory.Exists(DownloadFolder))
                            {
                                Directory.CreateDirectory(DownloadFolder);
                            }

                            LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                            LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                            URI = new Uri(URL);
                            FFUFileName = Path.GetFileName(URI.LocalPath);
                            LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                            FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                            LogFile.Log("Downloading...", LogType.FileAndConsole);
                            using (System.Net.WebClient myWebClient = new())
                            {
                                await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                            }
                            LogFile.Log("Download finished", LogType.FileAndConsole);
                            App.Config.AddFfuToRepository(FFUFilePath);

                            if (!App.Config.FFURepository.Any(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                            {
                                throw new WPinternalsException("Unable to find compatible FFU", "No donor-FFU has been found in the repository with a supported OS version. You can add a donor-FFU within the download section of the tool or by using the command line. A donor-FFU can be for a different device and a different CPU than your device. It is only used to gather Operating System specific binaries to be patched and used as part of the unlock process.");
                            }
                        }
                        break;
                    case "downloadallbyproductcode":
                        LogFile.Log("Command: Download all by Product Code", LogType.FileAndConsole);
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -DownloadAllByProductCode <Product code> <Optional: Download folder>");
                        }

                        ProductCode = args[2];
                        LogFile.Log("Product code: " + ProductCode, LogType.FileAndConsole);
                        URL = LumiaDownloadModel.SearchFFU(null, ProductCode, null, out ProductType);
                        DownloadFolder = args.Length >= 4
                            ? args[3]
                            : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                        if (!Directory.Exists(DownloadFolder))
                        {
                            Directory.CreateDirectory(DownloadFolder);
                        }

                        LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                        LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                        URI = new Uri(URL);
                        FFUFileName = Path.GetFileName(URI.LocalPath);
                        LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                        FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                        LogFile.Log("Downloading...", LogType.FileAndConsole);
                        using (System.Net.WebClient myWebClient = new())
                        {
                            await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                        }
                        LogFile.Log("Download finished", LogType.FileAndConsole);
                        App.Config.AddFfuToRepository(FFUFilePath);

                        URLs = LumiaDownloadModel.SearchEmergencyFiles(ProductType);
                        if (URLs != null)
                        {
                            for (int i = 0; i < URLs.Length; i++)
                            {
                                LogFile.Log("URL: " + URLs[i], LogType.FileAndConsole);
                                URI = new Uri(URLs[i]);
                                EmergencyFileName = Path.GetFileName(URI.LocalPath);
                                LogFile.Log("File: " + EmergencyFileName, LogType.FileAndConsole);
                                EmergencyFilePath = Path.Combine(DownloadFolder, EmergencyFileName);
                                if (i == 0)
                                {
                                    ProgrammerPath = EmergencyFilePath;
                                }
                                else
                                {
                                    PayloadPath = EmergencyFilePath;
                                }

                                LogFile.Log("Downloading...", LogType.FileAndConsole);
                                using (System.Net.WebClient myWebClient = new())
                                {
                                    await myWebClient.DownloadFileTaskAsync(URLs[i], EmergencyFilePath);
                                }
                                LogFile.Log("Download finished", LogType.FileAndConsole);
                            }
                            App.Config.AddEmergencyToRepository(ProductType, ProgrammerPath, PayloadPath);
                        }

                        if (!App.Config.FFURepository.Any(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                        {
                            ProductType = "RM-1085";

                            URL = LumiaDownloadModel.SearchFFU(ProductType, null, null);
                            DownloadFolder = args.Length >= 4
                                ? args[3]
                                : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                            if (!Directory.Exists(DownloadFolder))
                            {
                                Directory.CreateDirectory(DownloadFolder);
                            }

                            LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                            LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                            URI = new Uri(URL);
                            FFUFileName = Path.GetFileName(URI.LocalPath);
                            LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                            FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                            LogFile.Log("Downloading...", LogType.FileAndConsole);
                            using (System.Net.WebClient myWebClient = new())
                            {
                                await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                            }
                            LogFile.Log("Download finished", LogType.FileAndConsole);
                            App.Config.AddFfuToRepository(FFUFilePath);

                            if (!App.Config.FFURepository.Any(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                            {
                                throw new WPinternalsException("Unable to find compatible FFU", "No donor-FFU has been found in the repository with a supported OS version. You can add a donor-FFU within the download section of the tool or by using the command line. A donor-FFU can be for a different device and a different CPU than your device. It is only used to gather Operating System specific binaries to be patched and used as part of the unlock process.");
                            }
                        }
                        break;
                    case "downloadallbyoperatorcode":
                        LogFile.Log("Command: Download FFU by Operator Code", LogType.FileAndConsole);
                        if (args.Length < 4)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -DownloadFFUbyOperatorCode <Product type> <Operator code> <Optional: Download folder>");
                        }

                        ProductType = args[2];
                        LogFile.Log("Product type: " + ProductType, LogType.FileAndConsole);
                        OperatorCode = args[3];
                        LogFile.Log("Operator code: " + OperatorCode, LogType.FileAndConsole);
                        DownloadFolder = args.Length >= 5
                            ? args[4]
                            : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                        if (!Directory.Exists(DownloadFolder))
                        {
                            Directory.CreateDirectory(DownloadFolder);
                        }

                        LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                        URL = LumiaDownloadModel.SearchFFU(ProductType, null, OperatorCode);
                        LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                        URI = new Uri(URL);
                        FFUFileName = Path.GetFileName(URI.LocalPath);
                        LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                        FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                        LogFile.Log("Downloading...", LogType.FileAndConsole);
                        using (System.Net.WebClient myWebClient = new())
                        {
                            await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                        }
                        LogFile.Log("Download finished", LogType.FileAndConsole);
                        App.Config.AddFfuToRepository(FFUFilePath);

                        URLs = LumiaDownloadModel.SearchEmergencyFiles(ProductType);
                        if (URLs != null)
                        {
                            for (int i = 0; i < URLs.Length; i++)
                            {
                                LogFile.Log("URL: " + URLs[i], LogType.FileAndConsole);
                                URI = new Uri(URLs[i]);
                                EmergencyFileName = Path.GetFileName(URI.LocalPath);
                                LogFile.Log("File: " + EmergencyFileName, LogType.FileAndConsole);
                                EmergencyFilePath = Path.Combine(DownloadFolder, EmergencyFileName);
                                if (i == 0)
                                {
                                    ProgrammerPath = EmergencyFilePath;
                                }
                                else
                                {
                                    PayloadPath = EmergencyFilePath;
                                }

                                LogFile.Log("Downloading...", LogType.FileAndConsole);
                                using (System.Net.WebClient myWebClient = new())
                                {
                                    await myWebClient.DownloadFileTaskAsync(URLs[i], EmergencyFilePath);
                                }
                                LogFile.Log("Download finished", LogType.FileAndConsole);
                            }
                            App.Config.AddEmergencyToRepository(ProductType, ProgrammerPath, PayloadPath);
                        }

                        if (!App.Config.FFURepository.Any(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                        {
                            ProductType = "RM-1085";

                            URL = LumiaDownloadModel.SearchFFU(ProductType, null, null);
                            DownloadFolder = args.Length >= 5
                                ? args[4]
                                : Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\Repository\\" + ProductType.ToUpper());

                            if (!Directory.Exists(DownloadFolder))
                            {
                                Directory.CreateDirectory(DownloadFolder);
                            }

                            LogFile.Log("Download folder: " + DownloadFolder, LogType.FileAndConsole);
                            LogFile.Log("URL: " + URL, LogType.FileAndConsole);
                            URI = new Uri(URL);
                            FFUFileName = Path.GetFileName(URI.LocalPath);
                            LogFile.Log("File: " + FFUFileName, LogType.FileAndConsole);
                            FFUFilePath = Path.Combine(DownloadFolder, FFUFileName);
                            LogFile.Log("Downloading...", LogType.FileAndConsole);
                            using (System.Net.WebClient myWebClient = new())
                            {
                                await myWebClient.DownloadFileTaskAsync(URL, FFUFilePath);
                            }
                            LogFile.Log("Download finished", LogType.FileAndConsole);
                            App.Config.AddFfuToRepository(FFUFilePath);

                            if (!App.Config.FFURepository.Any(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                            {
                                throw new WPinternalsException("Unable to find compatible FFU", "No donor-FFU has been found in the repository with a supported OS version. You can add a donor-FFU within the download section of the tool or by using the command line. A donor-FFU can be for a different device and a different CPU than your device. It is only used to gather Operating System specific binaries to be patched and used as part of the unlock process.");
                            }
                        }
                        break;
                    case "rewritepartitionsfrommassstorage":
                        if (args.Length < 2)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -RewritePartitionsFromMassStorage <Path to folder containing img partitions> \n The name of the imgs must be the partition names. For example, DPP.img will get written to the DPP partition.");
                        }

                        await TestCode.RewriteParts(args[2]);
                        break;
                    case "restoregptusingedl":
                        if (args.Length < 3)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -RestoreGPTUsingEDL <Path to GPT.bin to be flashed at sector 1 (minus the MBR at sector 0)> <Loaders path>");
                        }

                        await TestCode.RecoverBadGPT(args[2], args[3]);
                        break;
                    case "restoregptusingmassstorage":
                        if (args.Length < 2)
                        {
                            throw new ArgumentException("Wrong number of arguments. Usage: WPinternals.exe -RestoreGPTUsingMassStorage <Path to GPT.bin to be flashed at sector 1 (minus the MBR at sector 0)>");
                        }

                        await TestCode.RewriteGPT(args[2]);
                        break;
                    default:
                        LogFile.Log("", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals commandline usage:", LogType.ConsoleOnly);
                        LogFile.Log("", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -ShowPhoneInfo", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -AddFFU <FFU file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -AddEmergency <Product type> <EDE file> <EDP file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -RemoveFFU <FFU file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -RemoveEmergency <EDE file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -ListRepository", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -FindFlashingProfile <Optional: Profile FFU file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -UnlockBootloader", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: donor-FFU file with supported version of bootfiles>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -FixBootAfterUnlockingBootloader", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -EnableRootAccess", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -UnlockBootLoaderOnImage <EFIESP image file>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: MainOS image file>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: donor-FFU file with supported version of bootfiles>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -EnableRootAccessOnImage <EFIESP image file> <MainOS image file>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: donor-FFU file with supported version of bootfiles>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -UnlockBootLoaderOnMountedImage <Directory of mounted EFIESP image file>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Directory of mounted MainOS image file>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: donor-FFU file with supported version of bootfiles>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -EnableRootAccessOnMountedImage <Directory of mounted EFIESP image file>", LogType.ConsoleOnly);
                        LogFile.Log("                <Directory of mounted MainOS image file>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: donor-FFU file with supported version of bootfiles>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -EnableTestSigning", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -RelockPhone", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -SwitchToMassStorageMode", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -FlashFFU <FFU file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -FlashCustomROM <ZIP file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -FlashPartition <Partition name> <Partition file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -FlashRaw <Start sector> <Raw file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -ClearNV", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -ReadGPT", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -BackupGPT <Path to xml-file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -ConvertGPT <Path to GPT-file> <Path to xml-file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -RestoreGPT <Path to xml-file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -MergeGPT <Path to input-xml-file> <Path to input-xml-file>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Path to output-xml-file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -MergeGPT <Path to input-xml-file>", LogType.ConsoleOnly);
                        LogFile.Log("                <Path to ZIP file which includes Partitions.xml>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Path to output-xml-file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -ShowFFU <FFU file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DumpFFU <FFU file> <Destination folder> <Optional: Partition Name>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadFFU <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadFFUbyProductType <Product type>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadFFUbyProductCode <Product code>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadFFUbyOperatorCode <Product type> <Operator code>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadEmergency <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadEmergencyByProductType <Product type>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadAll <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadAllByProductType <Product type>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadAllByProductCode <Product code>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DownloadAllByOperatorCode <Product type> <Operator code>", LogType.ConsoleOnly);
                        LogFile.Log("                <Optional: Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -DumpUEFI <UEFI binary or FFU file> <Destination folder>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -TestProgrammer <EDE file>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -RewritePartitionsFromMassStorage <Path to folder containing img partitions>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -RestoreGPTUsingEDL <Path to GPT.bin to be flashed at sector 1 (minus the MBR at sector 0)> <Loaders path>", LogType.ConsoleOnly);
                        LogFile.Log("WPinternals -RestoreGPTUsingMassStorage <Path to GPT.bin to be flashed at sector 1 (minus the MBR at sector 0)>", LogType.ConsoleOnly);
                        break;
                }
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
            }

            if (Environment.GetCommandLineArgs().Length > 1)
            {
                CloseConsole();
            }
        }

        // http://stackoverflow.com/questions/472282/show-console-in-windows-application
        // https://stackoverflow.com/questions/807998/how-do-i-create-a-c-sharp-app-that-decides-itself-whether-to-show-as-a-console-o
        // http://www.csharp411.com/console-output-from-winforms-application/#comment-76
        // https://stackoverflow.com/questions/1305257/using-attachconsole-user-must-hit-enter-to-get-regular-command-line
        internal static void OpenConsole()
        {
            if (IsConsoleVisible)
            {
                return;
            }

            if (AttachConsole(-1))
            {
                Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r"); // Prompt was already printed. Clear that line.

                /*
                Other possibility to clear line:

                System.Console.CursorLeft = 0;
                char[] bl = System.Linq.Enumerable.ToArray<char>(System.Linq.Enumerable.Repeat<char>(' ', System.Console.WindowWidth - 1));
                System.Console.Write(bl);
                System.Console.CursorLeft = 0;
                */

                hConsoleWnd = GetForegroundWindow();
            }
            else
            {
                AllocConsole();
                IsNewConsoleCreated = true;

                hConsoleWnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                // Support VS 2017 debugger without VS host process:
                try
                {
                    // Console.OpenStandardOutput eventually calls into GetStdHandle. As per MSDN documentation of GetStdHandle: http://msdn.microsoft.com/en-us/library/windows/desktop/ms683231(v=vs.85).aspx will return the redirected handle and not the allocated console:
                    // "The standard handles of a process may be redirected by a call to  SetStdHandle, in which case  GetStdHandle returns the redirected handle. If the standard handles have been redirected, you can specify the CONIN$ value in a call to the CreateFile function to get a handle to a console's input buffer. Similarly, you can specify the CONOUT$ value to get a handle to a console's active screen buffer."
                    // Get the handle to CONOUT$.    
                    IntPtr stdHandle = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0);
                    Microsoft.Win32.SafeHandles.SafeFileHandle safeFileHandle = new(stdHandle, true);
                    FileStream fileStream = new(safeFileHandle, FileAccess.Write);
                    Encoding encoding = Encoding.GetEncoding(MY_CODE_PAGE);
                    StreamWriter standardOutput = new(fileStream, encoding);
                    standardOutput.AutoFlush = true;
                    Console.SetOut(standardOutput);
                }
                catch
                {
                }
            }

            IsConsoleVisible = true;

            Console.CancelKeyPress += Console_CancelKeyPress;

            LogFile.LogApplicationVersion();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            LogFile.Log("Operation canceled!", LogType.FileOnly);
            LogFile.EndAction();

#if PREVIEW
            Uploader.WaitForUploads();
#endif
        }

        internal static void CloseConsole(bool Exit = true)
        {
            if (IsConsoleVisible)
            {
                if (IsNewConsoleCreated)
                {
                    Console.WriteLine("Press any key to close...");
                    Console.ReadKey();
                }

                PostMessage(hConsoleWnd, WM_KEYDOWN, VK_RETURN, 0);

                IsConsoleVisible = false;

                if (Exit)
                {
#if PREVIEW
                    Uploader.WaitForUploads();
#endif

                    Environment.Exit(0);
                }
            }
        }
    }
}
