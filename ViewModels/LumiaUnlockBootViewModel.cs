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

using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WPinternals
{
    // Create this class on the UI thread, after the main-window of the application is initialized.
    // It is necessary to create the object on the UI thread, because notification events to the View need to be fired on that thread.
    // The Model for this ViewModel communicates over USB and for that it uses the hWnd of the main window.
    // Therefore the main window must be created before the ViewModel is created.

    internal enum MachineState
    {
        Default,
        LumiaSpecAGetGPT,
        LumiaSpecBUnlockBoot
    };

    internal class LumiaUnlockBootViewModel : ContextViewModel
    {
        private PhoneNotifierViewModel PhoneNotifier;
        private Action Callback;
        private bool IsFlashingDone = false;
        private string FFUPath;
        private string LoadersPath;
        private string SBL3Path;
        private string ProfileFFUPath;
        private string EDEPath;
        private string SupportedFFUPath;
        private bool IsBootLoaderUnlocked;
        private byte[] RootKeyHash = null;
        private List<QualcommPartition> PossibleLoaders;
        private GPT NewGPT;
        private byte[] GPT;
        private byte[] ExtraSector;
        private byte[] SBL2;
        private byte[] SBL3;
        private byte[] UEFI;
        private FFU FFU;
        private Action SwitchToFlashRom;
        private Action SwitchToUndoRoot;
        private Action SwitchToDownload;
        private bool DoUnlock;
        private MachineState State;
        private object EvaluateViewStateLockObject = new object();

        internal LumiaUnlockBootViewModel(PhoneNotifierViewModel PhoneNotifier, Action SwitchToFlashRom, Action SwitchToUndoRoot, Action SwitchToDownload, bool DoUnlock, Action Callback)
            : base()
        {
            IsSwitchingInterface = false;
            IsFlashModeOperation = true;

            this.PhoneNotifier = PhoneNotifier;
            this.SwitchToFlashRom = SwitchToFlashRom;
            this.SwitchToUndoRoot = SwitchToUndoRoot;
            this.SwitchToDownload = SwitchToDownload;
            this.DoUnlock = DoUnlock;
            this.Callback = Callback;

            State = MachineState.Default;

            this.PhoneNotifier.NewDeviceArrived += NewDeviceArrived;

            // ViewState will be evaluated as soon as this object is set as DataContext
        }

        ~LumiaUnlockBootViewModel()
        {
            PhoneNotifier.NewDeviceArrived -= NewDeviceArrived;
        }

        internal void SendLoader()
        {
            // Assume 9008 mode
            if (!((PhoneNotifier.CurrentModel is QualcommSerial) && (PossibleLoaders != null) && (PossibleLoaders.Count > 0)))
                return;

            ActivateSubContext(new BusyViewModel("Sending loader..."));
            LogFile.Log("Sending loader");

            QualcommSerial Serial = (QualcommSerial)PhoneNotifier.CurrentModel;
            QualcommDownload Download = new QualcommDownload(Serial);
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
                    catch { }

                    if (Result)
                        break;

                    Attempt++;
                }
                Serial.Close();

                if (!Result)
                    LogFile.Log("Loader failed");
            }
            else
            {
                LogFile.Log("Failed to communicate to Qualcomm Emergency Download mode");
                throw new BadConnectionException();
            }
        }

        // Potentially blocking UI. Threadsafe.
        internal override void EvaluateViewState()
        {
            if (!IsActive)
                return;

            if ((State == MachineState.LumiaSpecAGetGPT) || (State == MachineState.LumiaSpecBUnlockBoot))
                return;

            lock (EvaluateViewStateLockObject)
            {
                switch (PhoneNotifier.CurrentInterface)
                {
                    case PhoneInterfaces.Lumia_Normal:
                    case PhoneInterfaces.Lumia_Label:
                        IsSwitchingInterface = false;
                        if (IsFlashingDone)
                        {
                            if (DoUnlock)
                            {
                                LogFile.Log("Bootloader successfully unlocked!");
                                ActivateSubContext(new MessageViewModel("Bootloader succesfully unlocked!", Exit));
                            }
                            else
                            {
                                LogFile.Log("Bootloader successfully restored!");
                                ActivateSubContext(new MessageViewModel("Bootloader succesfully restored!", Exit));
                            }
                        }
                        else
                        {
                            if (DoUnlock)
                            {
                                // Display View to switch to Flash mode
                                LogFile.Log("Start unlock. Phone needs to switch to Flash-mode");
                                ActivateSubContext(new MessageViewModel("In order to start unlocking the bootloader, the phone needs to be switched to Flash-mode.", SwitchToFlashMode, Exit));
                            }
                            else
                            {
                                // Display View to switch to Flash mode
                                LogFile.Log("Start boot restore. Phone needs to switch to Flash-mode");
                                ActivateSubContext(new MessageViewModel("In order to start restoring the bootloader, the phone needs to be switched to Flash-mode.", SwitchToFlashMode, Exit));
                            }
                        }
                        break;
                    case PhoneInterfaces.Lumia_Flash:
                        // Display View with device info and request for resources
                        // Click on "Continue" will start processing all resources
                        // Processing may fail with error message
                        // Or processing will succeed and user will again be asked to continue with Bricking-procedure (to switch to emergency mode)

                        // This code is not always invoked by OnArrival event.
                        // So this is not always in a thread from the threadpool.
                        // So we need to avoid UI blocking code here.

                        // Flash Param "FS" is the Flash Status (4 bytes, Big Endian DWORD, values: 0 / 1)
                        // When flashing is done and phone is still in flash-mode, write raw dummy sector and restart phone
                        NokiaFlashModel FlashModel = (NokiaFlashModel)PhoneNotifier.CurrentModel;
                        if (IsFlashingDone && (FlashModel.ReadParam("FS")[3] > 0))
                        {
                            if (DoUnlock)
                            {
                                IsSwitchingInterface = true;
                                LogFile.Log("Phone detected in Flash-in-progress-mode. Escaping from Flash-mode.;");
                                ActivateSubContext(new BusyViewModel("Escaping from Flash mode..."));

                                new Thread(() =>
                                    {
                                        RecoverFromFlashMode();
                                    }).Start();
                            }
                            else
                            {
                                IsSwitchingInterface = false;
                                ActivateSubContext(new MessageViewModel("Bootloader restored. You can now flash a stock ROM.", SwitchToFlashRom));
                            }
                        }
                        else
                        {
                            IsSwitchingInterface = false;

                            int TestPos = 0;

                            try // In case phone reboots during the time that status is being read
                            {
                                // Some phones, like Lumia 928 verizon, do not support the Terminal interface!
                                // To read the RootKeyHash we use ReadParam("RRKH"), instead of GetTerminalResponse().RootKeyHash.
                                RootKeyHash = ((NokiaFlashModel)PhoneNotifier.CurrentModel).ReadParam("RRKH");

                                TestPos = 1;

                                UefiSecurityStatusResponse SecurityStatus = ((NokiaFlashModel)PhoneNotifier.CurrentModel).ReadSecurityStatus();
                                IsBootLoaderUnlocked = (SecurityStatus.AuthenticationStatus || SecurityStatus.RdcStatus || !SecurityStatus.SecureFfuEfuseStatus);

                                TestPos = 2;

                                PhoneInfo Info = ((NokiaFlashModel)PhoneNotifier.CurrentModel).ReadPhoneInfo();

                                TestPos = 3;

                                if (Info.FlashAppProtocolVersionMajor < 2)
                                {
                                    // This action is executed after the resources are selected by the user.
                                    Action<string, string, string, string, string, string, bool> ReturnFunction = (FFUPath, LoadersPath, SBL3Path, ProfileFFUPath, EDEPath, SupportedFFUPath, DoFixBoot) =>
                                    {
                                        // This is a callback on the UI thread
                                        // Resources are confirmed by user
                                        this.FFUPath = FFUPath;
                                            this.LoadersPath = LoadersPath;
                                            this.SBL3Path = SBL3Path;
                                            StorePaths();

                                            LogFile.Log("Processing resources:");
                                            LogFile.Log("FFU: " + FFUPath);
                                            LogFile.Log("Loaders: " + LoadersPath);
                                            if (SBL3Path == null)
                                                LogFile.Log("No SBL3 specified");
                                            else
                                                LogFile.Log("SBL3: " + SBL3Path);

                                        ActivateSubContext(new BusyViewModel("Processing resources..."));

                                        new Thread(() =>
                                            {
                                                bool ResourcesVerified = false;
                                                try
                                                {
                                                    ResourcesVerified = EvaluateResources();
                                                    if (!ResourcesVerified)
                                                    {
                                                        LogFile.Log("Processing resources failed.");
                                                    ActivateSubContext(new MessageViewModel("Invalid resources.", Abort));
                                                    }
                                                }
                                                catch (Exception Ex)
                                                {
                                                    LogFile.LogException(Ex);
                                                ActivateSubContext(new MessageViewModel(Ex.Message, Abort));
                                                }

                                                if (ResourcesVerified)
                                                {
                                                    if (IsBootLoaderUnlocked)
                                                    {
                                                        CustomFlashBootLoader();
                                                    }
                                                    else
                                                        PerformSoftBrick();
                                                }
                                            }).Start();
                                    };

                                    if (DoUnlock)
                                        ActivateSubContext(new BootUnlockResourcesViewModel("Lumia Flash mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, false));
                                    else
                                        ActivateSubContext(new BootRestoreResourcesViewModel("Lumia Flash mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, false));
                                }
                                else
                                {
                                    if (DoUnlock)
                                    {
                                        GPT GPT = FlashModel.ReadGPT();
                                        if ((GPT.GetPartition("IS_UNLOCKED") != null) || (GPT.GetPartition("BACKUP_EFIESP") != null))
                                        {
                                            ExitMessage("Phone is already unlocked", null);
                                            return;
                                        }
                                    }

                                    TestPos = 4;

                                    // Stop responding to device arrival here, because all connections are handled by subfunctions, not here.
                                    IsSwitchingInterface = true;

                                    // This action is executed after the resources are selected by the user.
                                    Action<string, string, string, string, string, string, bool> ReturnFunction = (FFUPath, LoadersPath, SBL3Path, ProfileFFUPath, EDEPath, SupportedFFUPath, DoFixBoot) =>
                                    {
                                        State = MachineState.LumiaSpecBUnlockBoot;
                                        if (DoUnlock)
                                        {
                                            // This is a callback on the UI thread
                                            // Resources are confirmed by user
                                            this.ProfileFFUPath = ProfileFFUPath;
                                            this.EDEPath = EDEPath;
                                            this.SupportedFFUPath = SupportedFFUPath;
                                            StorePaths();

                                            if (DoFixBoot)
                                                LogFile.Log("Fix Boot");
                                            else
                                                LogFile.Log("Unlock Bootloader");

                                            LogFile.Log("Processing resources:");
                                            LogFile.Log("Profile FFU: " + ProfileFFUPath);
                                            LogFile.Log("EDE file: " + EDEPath);
                                            if (SupportedFFUPath != null)
                                                LogFile.Log("Donor-FFU with supported OS version: " + SupportedFFUPath);

                                            Task.Run(async () =>
                                                {
                                                    if (DoFixBoot)
                                                        await LumiaV2UnlockBootViewModel.LumiaV2FixBoot(PhoneNotifier, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage);
                                                    else
                                                        await LumiaV2UnlockBootViewModel.LumiaV2UnlockBootloader(PhoneNotifier, ProfileFFUPath, EDEPath, SupportedFFUPath, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage);
                                                });
                                        }
                                        else
                                        {
                                            Task.Run(async () =>
                                            {
                                                await LumiaV2UnlockBootViewModel.LumiaV2RelockPhone(PhoneNotifier, null, true, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage);
                                            });
                                        }
                                    };

                                    TestPos = 5;

                                    if (DoUnlock)
                                        ActivateSubContext(new BootUnlockResourcesViewModel("Lumia Flash mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, true, Info.PlatformID, Info.Type));
                                    else
                                        ActivateSubContext(new BootRestoreResourcesViewModel("Lumia Flash mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, true, Info.PlatformID, Info.Type));
                                }
                            }
                            catch (Exception Ex)
                            {
                                LogFile.LogException(Ex, LogType.FileAndConsole, TestPos.ToString());
                            }
                        }
                        break;
                    case PhoneInterfaces.Qualcomm_Download:
                        IsSwitchingInterface = false;

                        // If resources are not confirmed yet, then display view with device info and request for resources.
                        QualcommDownload Download = new QualcommDownload((QualcommSerial)PhoneNotifier.CurrentModel);
                        byte[] QualcommRootKeyHash = Download.GetRKH();
                        if (RootKeyHash == null)
                            RootKeyHash = QualcommRootKeyHash;
                        else if (!StructuralComparisons.StructuralEqualityComparer.Equals(RootKeyHash, QualcommRootKeyHash))
                        {
                            LogFile.Log("Error: Root Key Hash in Qualcomm Emergency mode does not match!");
                            ActivateSubContext(new MessageViewModel("Error: Root Key Hash in Qualcomm Emergency mode does not match!", Callback));
                            return;
                        }

                        if ((this.FFUPath == null) || (this.PossibleLoaders == null) || (this.PossibleLoaders.Count == 0))
                        {
                            // This action is executed after the user selected the resources.
                            Action<string, string, string, string, string, string, bool> ReturnFunction = (FFUPath, LoadersPath, SBL3Path, ProfileFFUPath, EDEPath, SupportedFFUPath, DoFixBoot) =>
                            {
                                // This is a callback on the UI thread
                                // Resources are confirmed by user
                                this.FFUPath = FFUPath;
                                this.LoadersPath = LoadersPath;
                                this.SBL3Path = SBL3Path;
                                StorePaths();

                                LogFile.Log("Processing resources:");
                                LogFile.Log("FFU: " + FFUPath);
                                LogFile.Log("Loaders: " + LoadersPath);
                                if (SBL3Path == null)
                                    LogFile.Log("No SBL3 specified");
                                else
                                    LogFile.Log("SBL3: " + SBL3Path);

                                ActivateSubContext(new BusyViewModel("Processing resources..."));

                                new Thread(() =>
                                {
                                    bool ResourcesVerified = false;
                                    try
                                    {
                                        ResourcesVerified = EvaluateResources();
                                        if (!ResourcesVerified)
                                        {
                                            LogFile.Log("Processing resources failed.");
                                            ActivateSubContext(new MessageViewModel("Invalid resources.", Abort));
                                        }
                                    }
                                    catch (Exception Ex)
                                    {
                                        LogFile.LogException(Ex);
                                        ActivateSubContext(new MessageViewModel(Ex.Message, Abort));
                                    }

                                    if (ResourcesVerified)
                                        SendLoader();
                                }).Start();
                            };

                            if (DoUnlock)
                                ActivateSubContext(new BootUnlockResourcesViewModel("Qualcomm Emergency Download mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, false));
                            else
                                ActivateSubContext(new BootRestoreResourcesViewModel("Qualcomm Emergency Download mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, false));
                        }
                        else
                            new Thread(() =>
                            {
                                SendLoader();
                            }).Start();
                        break;
                    case PhoneInterfaces.Qualcomm_Flash:
                        new Thread(() =>
                        {
                            FlashBootLoader();
                        }).Start();
                        break;
                    case PhoneInterfaces.Lumia_Bootloader:
                        IsSwitchingInterface = true;
                        if (IsFlashingDone)
                        {
                            LogFile.Log("Booting phone");
                            ActivateSubContext(new BusyViewModel("Booting phone..."));
                        }
                        break;
                    default:
                        // Show View "Waiting for connection"
                        IsSwitchingInterface = false;
                        ActivateSubContext(null);
                        break;
                }
            }
        }

        private void SwitchToFlashMode()
        {
            // SwitchModeViewModel must be created on the UI thread
            IsSwitchingInterface = true;
            UIContext.Post(async (t) =>
                {
                    LogFile.Log("Switching to Flash-mode");

                    try
                    {
                        await SwitchModeViewModel.SwitchToWithProgress(PhoneNotifier, PhoneInterfaces.Lumia_Flash,
                            (msg, sub) =>
                                ActivateSubContext(new BusyViewModel(msg, sub)));
                    }
                    catch (Exception Ex)
                    {
                        ActivateSubContext(new MessageViewModel(Ex.Message, Callback));
                    }
                }, null);
        }

        private void Abort()
        {
            // SwitchModeViewModel must be created on the UI thread
            IsSwitchingInterface = false;
            UIContext.Post((t) =>
                {
                    StorePaths();
                    LogFile.Log("Aborting.");
                    Exit();
                }, null);
        }

        private void StorePaths()
        {
            RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"Software\WPInternals", true);
            if (Key == null)
                Key = Registry.CurrentUser.CreateSubKey(@"Software\WPInternals");

            if (FFUPath == null)
            {
                if (Key.GetValue("FFUPath") != null)
                    Key.DeleteValue("FFUPath");
            }
            else
                Key.SetValue("FFUPath", FFUPath);
            
            if (LoadersPath == null)
            {
                if (Key.GetValue("LoadersPath") != null)
                    Key.DeleteValue("LoadersPath");
            }
            else
                Key.SetValue("LoadersPath", LoadersPath);

            if (DoUnlock)
            {
                if (SBL3Path == null)
                {
                    if (Key.GetValue("SBL3Path") != null)
                        Key.DeleteValue("SBL3Path");
                }
                else
                    Key.SetValue("SBL3Path", SBL3Path);
            }

            NokiaFlashModel Model = (NokiaFlashModel)PhoneNotifier.CurrentModel;
            PhoneInfo Info = Model.ReadPhoneInfo();

            if (ProfileFFUPath == null)
            {
                if (Key.GetValue("ProfileFFUPath") != null)
                    Key.DeleteValue("ProfileFFUPath");
            }
            else
            {
                Key.SetValue("ProfileFFUPath", ProfileFFUPath);

                App.Config.AddFfuToRepository(ProfileFFUPath);
            }

            if (EDEPath == null)
            {
                if (Key.GetValue("EDEPath") != null)
                    Key.DeleteValue("EDEPath");
            }
            else
            {
                Key.SetValue("EDEPath", EDEPath);

                App.Config.AddEmergencyToRepository(Info.Type, EDEPath, null);
            }

            if (SupportedFFUPath != null)
            {
                Key.SetValue("SupportedFFUPath", SupportedFFUPath);

                App.Config.AddFfuToRepository(SupportedFFUPath);
            }

            Key.Close();
        }

        private void Exit()
        {
            IsSwitchingInterface = false; // From here on a device will be forced to Flash mode again on this screen which is meant for flashing
            IsFlashingDone = false;

            FFUPath = null;
            LoadersPath = null;
            PossibleLoaders = null;
            SBL3Path = null;

            Callback();
            ActivateSubContext(null);
        }

        internal void ExitMessage(string Message, string SubMessage)
        {
            // SecureBoot Unlock v2 is done. Reactivate phone arrival events.
            MessageViewModel SuccessMessageViewModel = new MessageViewModel(Message, () => {
                State = MachineState.Default;
                Exit();
            });
            SuccessMessageViewModel.SubMessage = SubMessage;
            ActivateSubContext(SuccessMessageViewModel);
        }

        // Potentially blocking UI. Threadsafe.
        private bool EvaluateResources(bool Emergency = false)
        {
            bool Result = true;

            bool DumpPartitions = false;
            string DumpFilePrefix = Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\") + DateTime.Now.ToString("yyyy-MM-dd hh.mm.ss") + " - ";

            if ((FFUPath == null) || (FFUPath.Length == 0))
                throw new Exception("Error: Path for FFU-file is mandatory.");

            if (DoUnlock && ((LoadersPath == null) || (LoadersPath.Length == 0)))
                throw new Exception("Error: Path for Loaders is mandatory.");

            if (PhoneNotifier.CurrentModel is NokiaFlashModel)
            {
                FlashVersion FlashVersion = ((NokiaFlashModel)PhoneNotifier.CurrentModel).GetFlashVersion();
                if (FlashVersion == null)
                    throw new Exception("Error: The version of the Flash Application on the phone could not be determined.");
                if ((FlashVersion.ApplicationMajor < 1) || ((FlashVersion.ApplicationMajor == 1) && (FlashVersion.ApplicationMinor < 28)))
                    throw new Exception("Error: The version of the Flash Application on the phone is too old. Update your phone using Windows Updates or flash a newer ROM to your phone. Then try again.");
            }

            try
            {
                FFU = new FFU(FFUPath);
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                throw new Exception("Error: Parsing FFU-file failed.");
            }

            if (DumpPartitions)
            {
                try
                {
                    File.WriteAllBytes(DumpFilePrefix + "01.bin", FFU.GetSectors(0, 34)); // Original GPT
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Writing binary for logging failed.");
                }
            }

            State = MachineState.LumiaSpecAGetGPT; // Stop handling arrival notifications in this screen
            IsSwitchingInterface = true; // Stop handling arrival notifications in MainViewModel
            SwitchModeViewModel.SwitchTo(PhoneNotifier, PhoneInterfaces.Lumia_Bootloader).Wait();
            NewGPT = ((NokiaFlashModel)PhoneNotifier.CurrentModel).ReadGPT();
            SwitchModeViewModel.SwitchTo(PhoneNotifier, PhoneInterfaces.Lumia_Flash).Wait();
            IsSwitchingInterface = true;
            State = MachineState.Default;

            // Make sure all partitions are in range of the emergency flasher.
            NewGPT.RestoreBackupPartitions();

            // Magic!
            // SecureBoot hack for Bootloader Spec A
            if (DoUnlock)
            {
                try
                {
                    this.GPT = NewGPT.InsertHack();
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing partitions failed.");
                }

                if (DumpPartitions)
                {
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "02.bin", this.GPT); // Patched GPT
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
                }
            }
            else
            {
                NewGPT.RemoveHack();
                this.GPT = NewGPT.Rebuild();
            }

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

            if (DumpPartitions)
            {
                try
                {
                    File.WriteAllBytes(DumpFilePrefix + "03.bin", SBL1.Binary); // Original SBL1
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Writing binary for logging failed.");
                }
            }

            if (!Emergency)
            {
                if (RootKeyHash == null)
                {
                    Result = false;
                    throw new Exception("Error: Root Key Hash could not be retrieved from the phone.");
                }
                if (SBL1.RootKeyHash == null)
                {
                    Result = false;
                    throw new Exception("Error: Root Key Hash could not be retrieved from FFU file.");
                }
                if (!StructuralComparisons.StructuralEqualityComparer.Equals(RootKeyHash, SBL1.RootKeyHash))
                {
                    LogFile.Log("Phone: " + Converter.ConvertHexToString(RootKeyHash, ""));
                    LogFile.Log("SBL1: " + Converter.ConvertHexToString(SBL1.RootKeyHash, ""));
                    Result = false;
                    throw new Exception("Error: Root Key Hash from phone and from FFU file do not match!");
                }
            }

            SBL2 SBL2 = null;
            try
            {
                SBL2 = new SBL2(FFU.GetPartition("SBL2"));
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                throw new Exception("Error: Parsing SBL2 failed.");
            }

            if (DumpPartitions)
            {
                try
                {
                    File.WriteAllBytes(DumpFilePrefix + "05.bin", SBL2.Binary); // Original SBL2
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Writing binary for logging failed.");
                }
            }

            if (DoUnlock)
            {
                try
                {
                    this.SBL2 = SBL2.Patch();
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Patching SBL2 failed.");
                }

                if (DumpPartitions)
                {
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "06.bin", SBL2.Binary); // Patched SBL2
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
                }
            }
            else
            {
                this.SBL2 = SBL2.Binary;
            }

            this.ExtraSector = null;
            if (DoUnlock)
            {
                try
                {
                    byte[] PartitionHeader = new byte[0x0C];
                    Buffer.BlockCopy(SBL2.Binary, 0, PartitionHeader, 0, 0x0C);
                    this.ExtraSector = SBL1.GenerateExtraSector(PartitionHeader);
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Code generation failed.");
                }

                if (DumpPartitions)
                {
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "04.bin", this.ExtraSector); // Extra sector
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
                }
            }
    
            SBL3 SBL3;
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
                SBL3 = OriginalSBL3;
                LogFile.Log("Taking SBL3 from FFU");
            }
            else
            {
                SBL3 = null;
                try
                {
                    SBL3 = new SBL3(SBL3Path);
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Parsing external SBL3 failed.");
                }

                if (SBL3.Binary.Length > OriginalSBL3.Binary.Length)
                {
                    throw new Exception("Error: Selected SBL3 is too large.");
                }
                LogFile.Log("Taking selected SBL3");
            }

            if (DumpPartitions)
            {
                try
                {
                    File.WriteAllBytes(DumpFilePrefix + "07.bin", SBL3.Binary); // Original SBL3
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Writing binary for logging failed.");
                }
            }

            if (DoUnlock)
            {
                try
                {
                    this.SBL3 = SBL3.Patch();
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Patching SBL3 failed.");
                }

                if (DumpPartitions)
                {
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "08.bin", SBL3.Binary); // Patched SBL3
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
                }
            }
            else
            {
                this.SBL3 = SBL3.Binary;
            }

            UEFI UEFI = null; 
            try
            {
                UEFI = new UEFI(FFU.GetPartition("UEFI"));
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex);
                throw new Exception("Error: Parsing UEFI failed.");
            }

            if (DumpPartitions)
            {
                try
                {
                    File.WriteAllBytes(DumpFilePrefix + "09.bin", UEFI.Binary); // Original UEFI
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Writing binary for logging failed.");
                }
            }

            if (DoUnlock)
            {
                try
                {
                    this.UEFI = UEFI.Patch();
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    throw new Exception("Error: Patching UEFI failed.");
                }

                if (DumpPartitions)
                {
                    try
                    {
                        File.WriteAllBytes(DumpFilePrefix + "0A.bin", UEFI.Binary); // Patched UEFI
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        throw new Exception("Error: Writing binary for logging failed.");
                    }
                }
            }
            else
            {
                this.UEFI = UEFI.Binary;
            }

            if (!IsBootLoaderUnlocked)
            {
                try
                {
                    PossibleLoaders = QualcommLoaders.GetPossibleLoadersForRootKeyHash(LoadersPath, this.RootKeyHash);
                    if (PossibleLoaders.Count == 0)
                    {
                        Result = false;
                        throw new Exception("Error: No matching loaders found for RootKeyHash.");
                    }
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    Result = false;
                    throw new Exception("Error: Unexpected error during scanning for loaders.");
                }
            }

            return Result;
        }

        // TODO: Add logging
        private void PerformSoftBrick()
        {
            IsSwitchingInterface = true;
            ActivateSubContext(new BusyViewModel("Switching to Emergency Download mode..."));

            // Send FFU headers
            UInt64 CombinedFFUHeaderSize = this.FFU.HeaderSize;
            byte[] FfuHeader = new byte[CombinedFFUHeaderSize];
            System.IO.FileStream FfuFile = new System.IO.FileStream(FFUPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            FfuFile.Read(FfuHeader, 0, (int)CombinedFFUHeaderSize);
            FfuFile.Close();
            try
            {
                ((NokiaFlashModel)PhoneNotifier.CurrentModel).SendFfuHeaderV1(FfuHeader);
            }
            catch (Exception Ex)
            {
                IsSwitchingInterface = false;
                LogFile.LogException(Ex);
                ActivateSubContext(new MessageViewModel("Error using FFU. Try an FFU image which matches the phone.", Abort));
                return;
            }

            // Send 1 empty chunk (according to layout in FFU headers, it will be written to first and last chunk)
            byte[] EmptyChunk = new byte[0x20000];
            Array.Clear(EmptyChunk, 0, 0x20000);
            ((NokiaFlashModel)PhoneNotifier.CurrentModel).SendFfuPayloadV1(EmptyChunk);

            // Reboot to Qualcomm Emergency mode
            byte[] RebootCommand = new byte[] { 0x4E, 0x4F, 0x4B, 0x52 }; // NOKR
            ((NokiaFlashModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
        }

        void NewDeviceArrived(ArrivalEventArgs Args)
        {
            // Do not start on a new thread, because EvaluateViewState will also create new ViewModels and those should be created on the UI thread.
            EvaluateViewState();
        }

        private void RecoverFromFlashMode()
        {
            IsSwitchingInterface = true;
            LogFile.Log("Recover from Flash-mode");

            // Flash dummy sector (only allowed when phone is authenticated)
            byte[] EmptySector = new byte[0x200];
            Array.Clear(EmptySector, 0, 0x200);
            ((NokiaFlashModel)PhoneNotifier.CurrentModel).FlashSectors(0x22, EmptySector);

            // Reboot to Qualcomm Emergency mode
            byte[] RebootCommand = new byte[] { 0x4E, 0x4F, 0x4B, 0x52 }; // NOKR
            ((NokiaFlashModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
        }

        // Expected to be launched on worker-thread.
        internal void FlashBootLoader()
        {
            IsSwitchingInterface = true;
            LogFile.Log("Start flashing in Qualcomm Emergency Flash mode");

            if (this.FFU == null)
            {
                ActivateSubContext(new BusyViewModel("Recovering resources..."));

                LogFile.Log("Phone was unexpectedly detected in this mode while resources were not loaded yet.");
                LogFile.Log("WPInternals tool probably crashed in previous session.");
                LogFile.Log("Trying to recover resources from the registry.");

                // In case tool was terminated
                FFUPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "FFUPath", null);
                LoadersPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "LoadersPath", null);
                if (DoUnlock)
                    SBL3Path = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "SBL3Path", null);
                else
                    SBL3Path = null;

                try
                {
                    EvaluateResources(Emergency:true);
                }
                catch (Exception Ex)
                {
                    LogFile.LogException(Ex);
                    ActivateSubContext(new MessageViewModel(Ex.Message, Abort));
                }
            }

            QualcommSerial Serial = (QualcommSerial)PhoneNotifier.CurrentModel;
            Serial.EncodeCommands = false;

            QualcommFlasher Flasher = new QualcommFlasher(Serial);

            UInt64 TotalSectorCount = (UInt64)1 + 0x21 + 1 +
                (UInt64)(SBL2.Length / 0x200) +
                (UInt64)(SBL3.Length / 0x200) +
                (UInt64)(UEFI.Length / 0x200) +
                NewGPT.GetPartition("SBL1").SizeInSectors - 1 +
                NewGPT.GetPartition("TZ").SizeInSectors +
                NewGPT.GetPartition("RPM").SizeInSectors +
                NewGPT.GetPartition("WINSECAPP").SizeInSectors;

            if (DoUnlock)
                ActivateSubContext(new BusyViewModel("Flashing unlocked bootloader...", MaxProgressValue: TotalSectorCount, UIContext: UIContext));
            else
                ActivateSubContext(new BusyViewModel("Flashing original bootloader...", MaxProgressValue: TotalSectorCount, UIContext: UIContext));

            ProgressUpdater Progress = ((BusyViewModel)SubContextViewModel).ProgressUpdater;

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
                Length = 0x1E7FE00 - Start;
            LogFile.Log("Flash WINSECAPP at 0x" + ((UInt32)NewGPT.GetPartition("WINSECAPP").FirstSector * 0x200).ToString("X8"));
            Flasher.Flash(Start, FFU.GetPartition("WINSECAPP"), Progress, 0, Length);

            Flasher.ClosePartition();

            IsFlashingDone = true;
            LogFile.Log("Partition closed. Flashing ready. Rebooting.");

            ActivateSubContext(new BusyViewModel("Flashing done. Rebooting..."));
            Flasher.Reboot();

            Flasher.CloseSerial();
        }

        // Expected to be launched on worker-thread.
        internal void CustomFlashBootLoader()
        {
            IsSwitchingInterface = true;
            LogFile.Log("Start flashing in Custom Flash mode");

            NokiaFlashModel CurrentModel = (NokiaFlashModel)PhoneNotifier.CurrentModel;

            UInt64 TotalSectorCount = (UInt64)0x21 + 1 +
                (UInt64)(SBL2.Length / 0x200) +
                (UInt64)(SBL3.Length / 0x200) +
                (UInt64)(UEFI.Length / 0x200);

            if (DoUnlock)
                ActivateSubContext(new BusyViewModel("Flashing unlocked bootloader...", MaxProgressValue: TotalSectorCount, UIContext: UIContext));
            else
                ActivateSubContext(new BusyViewModel("Flashing original bootloader...", MaxProgressValue: TotalSectorCount, UIContext: UIContext));

            ProgressUpdater Progress = ((BusyViewModel)SubContextViewModel).ProgressUpdater;

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

            IsFlashingDone = true;

            ActivateSubContext(new BusyViewModel("Flashing done. Rebooting..."));
            byte[] RebootCommand = new byte[] { 0x4E, 0x4F, 0x4B, 0x52 }; // NOKR
            CurrentModel.ExecuteRawVoidMethod(RebootCommand);
        }
    }

    internal class BootUnlockResourcesViewModel : FlashResourcesViewModel
    {
        internal BootUnlockResourcesViewModel(string CurrentMode, byte[] RootKeyHash, Action SwitchToFlashRom, Action SwitchToUndoRoot, Action SwitchToDownload, Action<string, string, string, string, string, string, bool> Result, Action Abort, bool IsBootLoaderUnlocked, bool TargetHasNewFlashProtocol, string PlatformID = null, string ProductType = null)
            : base(CurrentMode, RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, Result, Abort, IsBootLoaderUnlocked, TargetHasNewFlashProtocol, PlatformID, ProductType)
        {
            SBL3Path = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "SBL3Path", null);
        }
    }

    internal class BootRestoreResourcesViewModel : FlashResourcesViewModel
    {
        internal BootRestoreResourcesViewModel(string CurrentMode, byte[] RootKeyHash, Action SwitchToFlashRom, Action SwitchToUndoRoot, Action SwitchToDownload, Action<string, string, string, string, string, string, bool> Result, Action Abort, bool IsBootLoaderUnlocked, bool TargetHasNewFlashProtocol, string PlatformID = null, string ProductType = null)
            : base(CurrentMode, RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, Result, Abort, IsBootLoaderUnlocked, TargetHasNewFlashProtocol, PlatformID, ProductType) 
        {
            SBL3Path = null;
        }
    }

    internal class FlashResourcesViewModel: ContextViewModel
    {
        internal Action SwitchToFlashRom;
        internal Action SwitchToUndoRoot;
        internal Action SwitchToDownload;
        private string PlatformID;
        private string ProductType;
        private string ValidatedSupportedFfuPath = null;

        internal FlashResourcesViewModel(string CurrentMode, byte[] RootKeyHash, Action SwitchToFlashRom, Action SwitchToUndoRoot, Action SwitchToDownload, Action<string, string, string, string, string, string, bool> Result, Action Abort, bool IsBootLoaderUnlocked, bool TargetHasNewFlashProtocol, string PlatformID = null, string ProductType = null): base()
        {
            IsSwitchingInterface = true;
            this.CurrentMode = CurrentMode;
            this.RootKeyHash = RootKeyHash;
            this.PlatformID = PlatformID;
            this.ProductType = ProductType;
            this.SwitchToFlashRom = SwitchToFlashRom;
            this.SwitchToUndoRoot = SwitchToUndoRoot;
            this.SwitchToDownload = SwitchToDownload;
            this.IsBootLoaderUnlocked = IsBootLoaderUnlocked;
            OkCommand = new DelegateCommand(() => Result(FFUPath, LoadersPath, SBL3Path, ProfileFFUPath, EDEPath, IsSupportedFfuNeeded ? SupportedFFUPath : null, false),
                () => (!TargetHasNewFlashProtocol || ((ProfileFFUPath != null)  &&  (!IsSupportedFfuNeeded || (IsSupportedFfuValid && (SupportedFFUPath != null))))));
            FixCommand = new DelegateCommand(() => Result(null, null, null, null, null, null, true));
            CancelCommand = new DelegateCommand(Abort);
            this.TargetHasNewFlashProtocol = TargetHasNewFlashProtocol;

            if (TargetHasNewFlashProtocol)
            {
                SetProfileFFUPath();
                SetEDEPath();
            }
            else
            {
                FFUPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "FFUPath", null);
                LoadersPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "LoadersPath", null);
            }
        }

        private void SetProfileFFUPath()
        {
            FFU ProfileFFU;

            try
            {
                if (_ProfileFFUPath != null)
                {
                    ProfileFFU = new FFU(_ProfileFFUPath);
                    if (PlatformID.StartsWith(ProfileFFU.PlatformID, StringComparison.OrdinalIgnoreCase))
                    {
                        IsProfileFfuValid = true;
                        return;
                    }
                }
            }
            catch { }

            try
            {
                string TempProfileFFUPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "ProfileFFUPath", null);
                if (TempProfileFFUPath != null)
                {
                    ProfileFFU = new FFU(TempProfileFFUPath);
                    if (PlatformID.StartsWith(ProfileFFU.PlatformID, StringComparison.OrdinalIgnoreCase))
                    {
                        ProfileFFUPath = TempProfileFFUPath;
                        IsProfileFfuValid = true;
                        return;
                    }
                }
            }
            catch { }

            List<FFUEntry> FFUs = App.Config.FFURepository.Where(e => (PlatformID.StartsWith(e.PlatformID, StringComparison.OrdinalIgnoreCase) && e.Exists())).ToList();
            if (FFUs.Count() > 0)
            {
                IsProfileFfuValid = true;
                ProfileFFUPath = FFUs[0].Path;
            }
            else
                IsProfileFfuValid = false;
        }

        private void SetEDEPath()
        {
            QualcommPartition Programmer;
            string TempEDEPath;

            try
            {
                if (_EDEPath != null)
                {
                    Programmer = new QualcommPartition(_EDEPath);
                    if (ByteOperations.Compare(Programmer.RootKeyHash, RootKeyHash))
                        return;
                }
            }
            catch { }

            try
            {
                TempEDEPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "EDEPath", null);
                if (TempEDEPath != null)
                {
                    Programmer = new QualcommPartition(TempEDEPath);
                    if (ByteOperations.Compare(Programmer.RootKeyHash, RootKeyHash))
                    {
                        EDEPath = TempEDEPath;
                        return;
                    }
                }
            }
            catch { }

            TempEDEPath = LumiaV2UnlockBootViewModel.GetProgrammerPath(RootKeyHash, ProductType);
            if (TempEDEPath != null)
            {
                Programmer = new QualcommPartition(TempEDEPath);
                if (ByteOperations.Compare(Programmer.RootKeyHash, RootKeyHash))
                {
                    EDEPath = TempEDEPath;
                    return;
                }
            }
        }

        private void SetSupportedFFUPath()
        {
            if ((this is BootRestoreResourcesViewModel) || (_ProfileFFUPath == null))
            {
                IsSupportedFfuNeeded = false;
                return;
            }

            try
            {
                FFU ProfileFFU = new FFU(_ProfileFFUPath);
                IsSupportedFfuNeeded = !(App.PatchEngine.PatchDefinitions.Where(p => p.Name == "SecureBootHack-V2-EFIESP").First().TargetVersions.Any(v => v.Description == ProfileFFU.GetOSVersion()));
            }
            catch
            {
                IsSupportedFfuNeeded = false;
                return;
            }

            if (IsSupportedFfuNeeded)
            {
                FFU SupportedFFU;

                try
                {
                    if (_SupportedFFUPath != null)
                    {
                        SupportedFFU = new FFU(_SupportedFFUPath);
                        if (App.PatchEngine.PatchDefinitions.Where(p => p.Name == "SecureBootHack-V2-EFIESP").First().TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                            return;
                    }
                }
                catch { }

                try
                {
                    string TempSupportedFFUPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "SupportedFFUPath", null);
                    if (TempSupportedFFUPath != null)
                    {
                        SupportedFFU = new FFU(TempSupportedFFUPath);
                        if (App.PatchEngine.PatchDefinitions.Where(p => p.Name == "SecureBootHack-V2-EFIESP").First().TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                        {
                            ValidatedSupportedFfuPath = TempSupportedFFUPath;
                            SupportedFFUPath = TempSupportedFFUPath;
                            IsSupportedFfuValid = true;
                            return;
                        }
                    }
                }
                catch { }

                List<FFUEntry> FFUs = App.Config.FFURepository.Where(e => App.PatchEngine.PatchDefinitions.Where(p => p.Name == "SecureBootHack-V2-EFIESP").First().TargetVersions.Any(v => v.Description == e.OSVersion)).ToList();
                if (FFUs.Count() > 0)
                {
                    ValidatedSupportedFfuPath = FFUs[0].Path;
                    SupportedFFUPath = FFUs[0].Path;
                    IsSupportedFfuValid = true;
                }
            }
        }

        private void ValidateSupportedFfuPath()
        {
            if (IsSupportedFfuNeeded)
            {
                if (SupportedFFUPath == null)
                {
                    IsSupportedFfuValid = true; // No visible warning when there is no SupportedFFU selected yet.
                }
                else
                {
                    if (App.Config.FFURepository.Any(e => ((e.Path == SupportedFFUPath) && (App.PatchEngine.PatchDefinitions.Where(p => p.Name == "SecureBootHack-V2-EFIESP").First().TargetVersions.Any(v => v.Description == e.OSVersion)))))
                    {
                        IsSupportedFfuValid = true;
                    }
                    else
                    {
                        FFU SupportedFFU = new FFU(SupportedFFUPath);
                        if (App.PatchEngine.PatchDefinitions.Where(p => p.Name == "SecureBootHack-V2-EFIESP").First().TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                        {
                            IsSupportedFfuValid = true;
                        }
                        else
                        {
                            IsSupportedFfuValid = false;
                        }
                    }
                }
            }
            else
            {
                IsSupportedFfuValid = true;
            }

            if (IsSupportedFfuValid && (SupportedFFUPath != null))
                ValidatedSupportedFfuPath = SupportedFFUPath;
        }

        private bool _IsSupportedFfuNeeded = false;
        public bool IsSupportedFfuNeeded
        {
            get
            {
                return _IsSupportedFfuNeeded;
            }
            set
            {
                _IsSupportedFfuNeeded = value;
                OnPropertyChanged("IsSupportedFfuNeeded");
                OkCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _IsSupportedFfuValid = true;
        public bool IsSupportedFfuValid
        {
            get
            {
                return _IsSupportedFfuValid;
            }
            set
            {
                _IsSupportedFfuValid = value;
                OnPropertyChanged("IsSupportedFfuValid");
                OkCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _IsProfileFfuValid = true;
        public bool IsProfileFfuValid
        {
            get
            {
                return _IsProfileFfuValid;
            }
            set
            {
                _IsProfileFfuValid = value;
                OnPropertyChanged("IsProfileFfuValid");
            }
        }

        private bool _TargetHasNewFlashProtocol = false;
        public bool TargetHasNewFlashProtocol
        {
            get
            {
                return _TargetHasNewFlashProtocol;
            }
            set
            {
                _TargetHasNewFlashProtocol = value;
                OnPropertyChanged("TargetHasNewFlashProtocol");
                OkCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _IsBootLoaderUnlocked = false;
        public bool IsBootLoaderUnlocked
        {
            get
            {
                return _IsBootLoaderUnlocked;
            }
            set
            {
                _IsBootLoaderUnlocked = value;
                OnPropertyChanged("IsBootLoaderUnlocked");
            }
        }

        private string _FFUPath = null;
        public string FFUPath
        {
            get
            {
                return _FFUPath;
            }
            set
            {
                _FFUPath = value;
                OnPropertyChanged("FFUPath");
            }
        }

        private string _ProfileFFUPath = null;
        public string ProfileFFUPath
        {
            get
            {
                return _ProfileFFUPath;
            }
            set
            {
                if (_ProfileFFUPath != value)
                {
                    _ProfileFFUPath = value;
                    OnPropertyChanged("ProfileFFUPath");
                    SetSupportedFFUPath();
                    OkCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _SupportedFFUPath = null;
        public string SupportedFFUPath
        {
            get
            {
                return _SupportedFFUPath;
            }
            set
            {
                if (_SupportedFFUPath != value)
                {
                    _SupportedFFUPath = value;
                    OnPropertyChanged("SupportedFFUPath");

                    if (value != ValidatedSupportedFfuPath)
                        ValidateSupportedFfuPath();
                }
            }
        }

        private string _LoadersPath = null;
        public string LoadersPath
        {
            get
            {
                return _LoadersPath;
            }
            set
            {
                _LoadersPath = value;
                OnPropertyChanged("LoadersPath");
            }
        }

        private string _EDEPath = null;
        public string EDEPath
        {
            get
            {
                return _EDEPath;
            }
            set
            {
                _EDEPath = value;
                OnPropertyChanged("EDEPath");
            }
        }

        private string _SBL3Path = null;
        public string SBL3Path
        {
            get
            {
                return _SBL3Path;
            }
            set
            {
                _SBL3Path = value;
                OnPropertyChanged("SBL3Path");
            }
        }

        private string _CurrentMode = null;
        public string CurrentMode
        {
            get
            {
                return _CurrentMode;
            }
            set
            {
                _CurrentMode = value;
                OnPropertyChanged("CurrentMode");
            }
        }

        private byte[] _RootKeyHash = null;
        public byte[] RootKeyHash
        {
            get
            {
                return _RootKeyHash;
            }
            set
            {
                _RootKeyHash = value;
                OnPropertyChanged("RootKeyHash");
            }
        }

        private DelegateCommand _OkCommand = null;
        public DelegateCommand OkCommand
        {
            get
            {
                return _OkCommand;
            }
            private set
            {
                _OkCommand = value;
            }
        }

        private DelegateCommand _CancelCommand = null;
        public DelegateCommand CancelCommand
        {
            get
            {
                return _CancelCommand;
            }
            private set
            {
                _CancelCommand = value;
            }
        }

        private DelegateCommand _FixCommand = null;
        public DelegateCommand FixCommand
        {
            get
            {
                return _FixCommand;
            }
            private set
            {
                _FixCommand = value;
            }
        }
    }
}
