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

using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        LumiaUnlockBoot
    };

    internal class LumiaUnlockBootViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel PhoneNotifier;
        private readonly Action Callback;
        private string FFUPath;
        private string LoadersPath;
        private string SBL3Path;
        private string ProfileFFUPath;
        private string EDEPath;
        private string SupportedFFUPath;
        private bool IsBootLoaderUnlocked;
        private byte[] RootKeyHash = null;
        private readonly Action SwitchToFlashRom;
        private readonly Action SwitchToUndoRoot;
        private readonly Action SwitchToDownload;
        private readonly bool DoUnlock;
        private MachineState State;
        private readonly object EvaluateViewStateLockObject = new();

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

        // Potentially blocking UI. Threadsafe.
        internal override void EvaluateViewState()
        {
            if (!IsActive)
            {
                return;
            }

            if (State == MachineState.LumiaUnlockBoot)
            {
                return;
            }

            if (IsSwitchingInterface)
            {
                return;
            }

            lock (EvaluateViewStateLockObject)
            {
                switch (PhoneNotifier.CurrentInterface)
                {
                    case PhoneInterfaces.Lumia_Normal:
                    case PhoneInterfaces.Lumia_Label:
                        IsSwitchingInterface = false;
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
                        break;
                    case PhoneInterfaces.Lumia_Flash:
                        // Display View with device info and request for resources
                        // Click on "Continue" will start processing all resources
                        // Processing may fail with error message
                        // Or processing will succeed and user will again be asked to continue with Bricking-procedure (to switch to emergency mode)

                        // This code is not always invoked by OnArrival event.
                        // So this is not always in a thread from the threadpool.
                        // So we need to avoid UI blocking code here.

                        IsSwitchingInterface = false;

                        int TestPos = 0;

                        try // In case phone reboots during the time that status is being read
                        {
                            // Some phones, like Lumia 928 verizon, do not support the Terminal interface!
                            // To read the RootKeyHash we use ReadParam("RRKH"), instead of GetTerminalResponse().RootKeyHash.
                            RootKeyHash = ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadParam("RRKH");

                            TestPos = 1;

                            UefiSecurityStatusResponse SecurityStatus = ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadSecurityStatus();
                            if (SecurityStatus != null)
                            {
                                IsBootLoaderUnlocked = SecurityStatus.AuthenticationStatus || SecurityStatus.RdcStatus || !SecurityStatus.SecureFfuEfuseStatus;
                            }

                            TestPos = 2;

                            LumiaFlashAppPhoneInfo FlashInfo = ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo();

                            if (SecurityStatus == null)
                            {
                                IsBootLoaderUnlocked = !FlashInfo.IsBootloaderSecure;
                            }

                            if (RootKeyHash == null)
                            {
                                RootKeyHash = FlashInfo.RKH ?? (new byte[32]);
                            }

                            TestPos = 3;

                            if (FlashInfo.FlashAppProtocolVersionMajor < 2)
                            {
                                // This action is executed after the resources are selected by the user.
                                void ReturnFunction(string FFUPath, string LoadersPath, string SBL3Path, string ProfileFFUPath, string EDEPath, string SupportedFFUPath, bool DoFixBoot)
                                {
                                    // Stop responding to device arrival here, because all connections are handled by subfunctions, not here.
                                    IsSwitchingInterface = true;
                                    State = MachineState.LumiaUnlockBoot;

                                    // This is a callback on the UI thread
                                    // Resources are confirmed by user
                                    this.FFUPath = FFUPath;
                                    this.LoadersPath = LoadersPath;
                                    this.SBL3Path = SBL3Path;
                                    this.SupportedFFUPath = SupportedFFUPath;
                                    StorePaths();

                                    LogFile.Log("Processing resources:");
                                    LogFile.Log("FFU: " + FFUPath);
                                    LogFile.Log("Loaders: " + LoadersPath);
                                    if (SBL3Path == null)
                                    {
                                        LogFile.Log("No SBL3 specified");
                                    }
                                    else
                                    {
                                        LogFile.Log("SBL3: " + SBL3Path);
                                    }

                                    ActivateSubContext(new BusyViewModel("Processing resources..."));

                                    if (DoUnlock)
                                    {
                                        Task.Run(async () => await LumiaUnlockBootloaderViewModel.LumiaV1UnlockFirmware(PhoneNotifier, FFUPath, LoadersPath, SBL3Path, SupportedFFUPath, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage));
                                    }
                                    else
                                    {
                                        Task.Run(async () => await LumiaUnlockBootloaderViewModel.LumiaV1RelockFirmware(PhoneNotifier, FFUPath, LoadersPath, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage));
                                    }
                                }

                                if (DoUnlock)
                                {
                                    ActivateSubContext(new BootUnlockResourcesViewModel("Lumia Flash mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, false));
                                }
                                else
                                {
                                    ActivateSubContext(new BootRestoreResourcesViewModel("Lumia Flash mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, false));
                                }
                            }
                            else
                            {
                                bool AlreadyUnlocked = false;
                                if (DoUnlock)
                                {
                                    LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;
                                    GPT GPT = FlashModel.ReadGPT();
                                    if ((GPT.GetPartition("IS_UNLOCKED") != null) || (GPT.GetPartition("BACKUP_EFIESP") != null))
                                    {
                                        //ExitMessage("Phone is already unlocked", null);
                                        //return;
                                        AlreadyUnlocked = true;
                                    }
                                }

                                TestPos = 4;

                                // Stop responding to device arrival here, because all connections are handled by subfunctions, not here.
                                IsSwitchingInterface = true;

                                // This action is executed after the resources are selected by the user.
                                void ReturnFunction(string FFUPath, string LoadersPath, string SBL3Path, string ProfileFFUPath, string EDEPath, string SupportedFFUPath, bool DoFixBoot)
                                {
                                    IsSwitchingInterface = true;
                                    State = MachineState.LumiaUnlockBoot;
                                    if (DoUnlock)
                                    {
                                        // This is a callback on the UI thread
                                        // Resources are confirmed by user
                                        this.ProfileFFUPath = ProfileFFUPath;
                                        this.EDEPath = EDEPath;
                                        this.SupportedFFUPath = SupportedFFUPath;
                                        StorePaths();

                                        if (DoFixBoot)
                                        {
                                            LogFile.Log("Fix Boot");
                                        }
                                        else
                                        {
                                            LogFile.Log("Unlock Bootloader");
                                        }

                                        LogFile.Log("Processing resources:");
                                        LogFile.Log("Profile FFU: " + ProfileFFUPath);
                                        LogFile.Log("EDE file: " + EDEPath);
                                        if (SupportedFFUPath != null)
                                        {
                                            LogFile.Log("Donor-FFU with supported OS version: " + SupportedFFUPath);
                                        }

                                        Task.Run(async () =>
                                            {
                                                if (DoFixBoot)
                                                {
                                                    await LumiaV2UnlockBootViewModel.LumiaV2FixBoot(PhoneNotifier, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage);
                                                }
                                                else if (!AlreadyUnlocked)
                                                {
                                                    await LumiaUnlockBootloaderViewModel.LumiaV2UnlockUEFI(PhoneNotifier, ProfileFFUPath, EDEPath, SupportedFFUPath, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage);
                                                }
                                                else
                                                {
                                                    await LumiaUnlockBootloaderViewModel.LumiaV2UnlockUEFI(PhoneNotifier, ProfileFFUPath, EDEPath, SupportedFFUPath, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage, true);
                                                }
                                            });
                                    }
                                    else
                                    {
                                        Task.Run(async () =>
                                        {
                                            FFU ProfileFFU = null;

                                            List<FFUEntry> FFUs = App.Config.FFURepository.Where(e => FlashInfo.PlatformID.StartsWith(e.PlatformID, StringComparison.OrdinalIgnoreCase) && e.Exists()).ToList();
                                            ProfileFFU = FFUs.Count > 0
                                                ? new FFU(FFUs[0].Path)
                                                : throw new WPinternalsException("Profile FFU missing", "No profile FFU has been found in the repository for your device. You can add a profile FFU within the download section of the tool or by using the command line.");

                                            LogFile.Log("Profile FFU: " + ProfileFFU.Path);

                                            await LumiaUnlockBootloaderViewModel.LumiaV2RelockUEFI(PhoneNotifier, ProfileFFU.Path, true, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage);
                                        });
                                    }
                                }

                                TestPos = 5;

                                IsSwitchingInterface = true;

                                Task.Run(async () =>
                                {
                                    bool ModernFlashApp = ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo().FlashAppProtocolVersionMajor >= 2;
                                    if (ModernFlashApp)
                                    {
                                        ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).SwitchToPhoneInfoAppContext();
                                    }
                                    else
                                    {
                                        ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).SwitchToPhoneInfoAppContextLegacy();
                                    }

                                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                                    {
                                        await PhoneNotifier.WaitForArrival();
                                    }

                                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                                    {
                                        throw new WPinternalsException("Unexpected Mode");
                                    }

                                    LumiaPhoneInfoAppModel LumiaPhoneInfoModel = (LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel;
                                    LumiaPhoneInfoAppPhoneInfo PhoneInfo = LumiaPhoneInfoModel.ReadPhoneInfo();

                                    IsSwitchingInterface = true;

                                    ModernFlashApp = PhoneInfo.PhoneInfoAppVersionMajor >= 2;
                                    if (ModernFlashApp)
                                    {
                                        LumiaPhoneInfoModel.SwitchToFlashAppContext();
                                    }
                                    else
                                    {
                                        LumiaPhoneInfoModel.ContinueBoot();
                                    }

                                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                                    {
                                        await PhoneNotifier.WaitForArrival();
                                    }

                                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                                    {
                                        throw new WPinternalsException("Unexpected Mode");
                                    }

                                    if (DoUnlock)
                                    {
                                        ActivateSubContext(new BootUnlockResourcesViewModel("Lumia Flash mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, true, FlashInfo.PlatformID, PhoneInfo.Type));
                                    }
                                    else
                                    {
                                        ActivateSubContext(new BootRestoreResourcesViewModel("Lumia Flash mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunction, Abort, IsBootLoaderUnlocked, true, FlashInfo.PlatformID, PhoneInfo.Type));
                                    }
                                });
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex, LogType.FileAndConsole, TestPos.ToString());
                        }
                        break;
                    case PhoneInterfaces.Qualcomm_Download:
                        IsSwitchingInterface = false;

                        // If resources are not confirmed yet, then display view with device info and request for resources.
                        QualcommDownload Download = new((QualcommSerial)PhoneNotifier.CurrentModel);
                        byte[] QualcommRootKeyHash;

                        try
                        {
                            QualcommRootKeyHash = Download.GetRKH();
                        }
                        catch (BadConnectionException)
                        {
                            // This is a Spec B device
                            break;
                        }

                        if (RootKeyHash == null)
                        {
                            RootKeyHash = QualcommRootKeyHash;
                        }
                        else if (!StructuralComparisons.StructuralEqualityComparer.Equals(RootKeyHash, QualcommRootKeyHash))
                        {
                            LogFile.Log("Error: Root Key Hash in Qualcomm Emergency mode does not match!");
                            ActivateSubContext(new MessageViewModel("Error: Root Key Hash in Qualcomm Emergency mode does not match!", Callback));
                            return;
                        }

                        // This action is executed after the user selected the resources.
                        Action<string, string, string, string, string, string, bool> ReturnFunctionD = (FFUPath, LoadersPath, SBL3Path, ProfileFFUPath, EDEPath, SupportedFFUPath, DoFixBoot) =>
                        {
                            IsSwitchingInterface = true;
                            State = MachineState.LumiaUnlockBoot;
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
                            {
                                LogFile.Log("No SBL3 specified");
                            }
                            else
                            {
                                LogFile.Log("SBL3: " + SBL3Path);
                            }

                            ActivateSubContext(new BusyViewModel("Processing resources..."));

                            if (DoUnlock)
                            {
                                Task.Run(async () => await LumiaUnlockBootloaderViewModel.LumiaV1UnlockFirmware(PhoneNotifier, FFUPath, LoadersPath, SBL3Path, SupportedFFUPath, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage));
                            }
                            else
                            {
                                Task.Run(async () => await LumiaUnlockBootloaderViewModel.LumiaV1RelockFirmware(PhoneNotifier, FFUPath, LoadersPath, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage));
                            }
                        };

                        if (DoUnlock)
                        {
                            ActivateSubContext(new BootUnlockResourcesViewModel("Qualcomm Emergency Download mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunctionD, Abort, IsBootLoaderUnlocked, false));
                        }
                        else
                        {
                            ActivateSubContext(new BootRestoreResourcesViewModel("Qualcomm Emergency Download mode", RootKeyHash, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, ReturnFunctionD, Abort, IsBootLoaderUnlocked, false));
                        }

                        break;
                    case PhoneInterfaces.Qualcomm_Flash:
                        {
                            IsSwitchingInterface = true;
                            State = MachineState.LumiaUnlockBoot;
                            ActivateSubContext(new BusyViewModel("Recovering resources..."));

                            LogFile.Log("Phone was unexpectedly detected in this mode while resources were not loaded yet.");
                            LogFile.Log("WPInternals tool probably crashed in previous session.");
                            LogFile.Log("Trying to recover resources from the registry.");

                            FFUPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "FFUPath", null);
                            SupportedFFUPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "SupportedFFUPath", null);
                            LoadersPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "LoadersPath", null);
                            SBL3Path = DoUnlock ? (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "SBL3Path", null) : null;

                            if (DoUnlock)
                            {
                                Task.Run(async () => await LumiaUnlockBootloaderViewModel.LumiaV1UnlockFirmware(PhoneNotifier, FFUPath, LoadersPath, SBL3Path, SupportedFFUPath, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage));
                            }
                            else
                            {
                                Task.Run(async () => await LumiaUnlockBootloaderViewModel.LumiaV1RelockFirmware(PhoneNotifier, FFUPath, LoadersPath, SetWorkingStatus, UpdateWorkingStatus, ExitMessage, ExitMessage));
                            }
                            break;
                        }
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

        private async void StorePaths()
        {
            RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"Software\WPInternals", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\WPInternals");

            if (FFUPath == null)
            {
                if (Key.GetValue("FFUPath") != null)
                {
                    Key.DeleteValue("FFUPath");
                }
            }
            else
            {
                Key.SetValue("FFUPath", FFUPath);
            }

            if (LoadersPath == null)
            {
                if (Key.GetValue("LoadersPath") != null)
                {
                    Key.DeleteValue("LoadersPath");
                }
            }
            else
            {
                Key.SetValue("LoadersPath", LoadersPath);
            }

            if (DoUnlock)
            {
                if (SBL3Path == null)
                {
                    if (Key.GetValue("SBL3Path") != null)
                    {
                        Key.DeleteValue("SBL3Path");
                    }
                }
                else
                {
                    Key.SetValue("SBL3Path", SBL3Path);
                }
            }

            if (ProfileFFUPath == null)
            {
                if (Key.GetValue("ProfileFFUPath") != null)
                {
                    Key.DeleteValue("ProfileFFUPath");
                }
            }
            else
            {
                Key.SetValue("ProfileFFUPath", ProfileFFUPath);

                App.Config.AddFfuToRepository(ProfileFFUPath);
            }

            if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Qualcomm_Download && PhoneNotifier.CurrentInterface != PhoneInterfaces.Qualcomm_Flash)
            {
                LumiaFlashAppModel Model = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;
                LumiaFlashAppPhoneInfo FlashInfo = Model.ReadPhoneInfo();

                bool OriginalIsSwitchingInterface = IsSwitchingInterface;
                IsSwitchingInterface = true;

                bool ModernFlashApp = ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo().FlashAppProtocolVersionMajor >= 2;
                if (ModernFlashApp)
                {
                    ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).SwitchToPhoneInfoAppContext();
                }
                else
                {
                    ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).SwitchToPhoneInfoAppContextLegacy();
                }

                if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                {
                    await PhoneNotifier.WaitForArrival();
                }

                if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                {
                    throw new WPinternalsException("Unexpected Mode");
                }

                LumiaPhoneInfoAppModel LumiaPhoneInfoModel = (LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel;
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

                if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                {
                    await PhoneNotifier.WaitForArrival();
                }

                if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                {
                    throw new WPinternalsException("Unexpected Mode");
                }

                IsSwitchingInterface = OriginalIsSwitchingInterface;

                if (EDEPath == null)
                {
                    if (Key.GetValue("EDEPath") != null)
                    {
                        Key.DeleteValue("EDEPath");
                    }
                }
                else
                {
                    Key.SetValue("EDEPath", EDEPath);

                    App.Config.AddEmergencyToRepository(PhoneInfo.Type, EDEPath, null);
                }
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

            FFUPath = null;
            LoadersPath = null;
            SBL3Path = null;

            Callback();
            ActivateSubContext(null);
        }

        internal void ExitMessage(string Message, string SubMessage)
        {
            // SecureBoot Unlock v2 is done. Reactivate phone arrival events.
            MessageViewModel SuccessMessageViewModel = new(Message, () =>
            {
                State = MachineState.Default;
                Exit();
            });
            SuccessMessageViewModel.SubMessage = SubMessage;
            ActivateSubContext(SuccessMessageViewModel);
        }

        private void NewDeviceArrived(ArrivalEventArgs Args)
        {
            // Do not start on a new thread, because EvaluateViewState will also create new ViewModels and those should be created on the UI thread.
            EvaluateViewState();
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

    internal class FlashResourcesViewModel : ContextViewModel
    {
        internal Action SwitchToFlashRom;
        internal Action SwitchToUndoRoot;
        internal Action SwitchToDownload;
        private readonly string PlatformID;
        private readonly string ProductType;
        private string ValidatedSupportedFfuPath = null;

        internal FlashResourcesViewModel(string CurrentMode, byte[] RootKeyHash, Action SwitchToFlashRom, Action SwitchToUndoRoot, Action SwitchToDownload, Action<string, string, string, string, string, string, bool> Result, Action Abort, bool IsBootLoaderUnlocked, bool TargetHasNewFlashProtocol, string PlatformID = null, string ProductType = null) : base()
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
                () => (!TargetHasNewFlashProtocol && (!IsSupportedFfuNeeded || (IsSupportedFfuValid && (SupportedFFUPath != null)))) || ((ProfileFFUPath != null) && (!IsSupportedFfuNeeded || (IsSupportedFfuValid && (SupportedFFUPath != null)))));
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
                SupportedFFUPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "SupportedFFUPath", null);
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
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
            }

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
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
            }

            List<FFUEntry> FFUs = App.Config.FFURepository.Where(e => PlatformID.StartsWith(e.PlatformID, StringComparison.OrdinalIgnoreCase) && e.Exists()).ToList();
            if (FFUs.Count > 0)
            {
                IsProfileFfuValid = true;
                ProfileFFUPath = FFUs[0].Path;
            }
            else
            {
                IsProfileFfuValid = false;
            }
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
                    {
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
            }

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
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
            }

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
            if (!TargetHasNewFlashProtocol)
            {
                if (this is BootRestoreResourcesViewModel)
                {
                    IsSupportedFfuNeeded = false;
                    return;
                }

                try
                {
                    FFU FFU = new(_FFUPath);
                    IsSupportedFfuNeeded = !App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V1.1-EFIESP").TargetVersions.Any(v => v.Description == FFU.GetOSVersion());
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
                            if (App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V1.1-EFIESP").TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                            {
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }

                    try
                    {
                        string TempSupportedFFUPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "SupportedFFUPath", null);
                        if (TempSupportedFFUPath != null)
                        {
                            SupportedFFU = new FFU(TempSupportedFFUPath);
                            if (App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V1.1-EFIESP").TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                            {
                                ValidatedSupportedFfuPath = TempSupportedFFUPath;
                                SupportedFFUPath = TempSupportedFFUPath;
                                IsSupportedFfuValid = true;
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }

                    List<FFUEntry> FFUs = App.Config.FFURepository.Where(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V1.1-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)).ToList();
                    if (FFUs.Count > 0)
                    {
                        ValidatedSupportedFfuPath = FFUs[0].Path;
                        SupportedFFUPath = FFUs[0].Path;
                        IsSupportedFfuValid = true;
                    }
                }
            }
            else
            {
                if ((this is BootRestoreResourcesViewModel) || (_ProfileFFUPath == null))
                {
                    IsSupportedFfuNeeded = false;
                    return;
                }

                try
                {
                    FFU ProfileFFU = new(_ProfileFFUPath);
                    IsSupportedFfuNeeded = !App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == ProfileFFU.GetOSVersion());
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
                            if (App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                            {
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }

                    try
                    {
                        string TempSupportedFFUPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\WPInternals", "SupportedFFUPath", null);
                        if (TempSupportedFFUPath != null)
                        {
                            SupportedFFU = new FFU(TempSupportedFFUPath);
                            if (App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion()))
                            {
                                ValidatedSupportedFfuPath = TempSupportedFFUPath;
                                SupportedFFUPath = TempSupportedFFUPath;
                                IsSupportedFfuValid = true;
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }

                    List<FFUEntry> FFUs = App.Config.FFURepository.Where(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)).ToList();
                    if (FFUs.Count > 0)
                    {
                        ValidatedSupportedFfuPath = FFUs[0].Path;
                        SupportedFFUPath = FFUs[0].Path;
                        IsSupportedFfuValid = true;
                    }
                }
            }
        }

        private void ValidateSupportedFfuPath()
        {
            try
            {
                if (IsSupportedFfuNeeded)
                {
                    if (SupportedFFUPath == null)
                    {
                        IsSupportedFfuValid = true; // No visible warning when there is no SupportedFFU selected yet.
                    }
                    else
                    {
                        if (!TargetHasNewFlashProtocol)
                        {
                            if (App.Config.FFURepository.Any(e => (e.Path == SupportedFFUPath) && App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V1.1-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                            {
                                IsSupportedFfuValid = true;
                            }
                            else
                            {
                                FFU SupportedFFU = new(SupportedFFUPath);
                                IsSupportedFfuValid = App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V1.1-EFIESP").TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion());
                            }
                        }
                        else
                        {
                            if (App.Config.FFURepository.Any(e => (e.Path == SupportedFFUPath) && App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
                            {
                                IsSupportedFfuValid = true;
                            }
                            else
                            {
                                FFU SupportedFFU = new(SupportedFFUPath);
                                IsSupportedFfuValid = App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == SupportedFFU.GetOSVersion());
                            }
                        }
                    }
                }
                else
                {
                    IsSupportedFfuValid = true;
                }

                if (IsSupportedFfuValid && (SupportedFFUPath != null))
                {
                    ValidatedSupportedFfuPath = SupportedFFUPath;
                }
            }
            catch
            {
                IsSupportedFfuValid = false;
            }
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
                OnPropertyChanged(nameof(IsSupportedFfuNeeded));
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
                OnPropertyChanged(nameof(IsSupportedFfuValid));
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
                OnPropertyChanged(nameof(IsProfileFfuValid));
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
                OnPropertyChanged(nameof(TargetHasNewFlashProtocol));
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
                OnPropertyChanged(nameof(IsBootLoaderUnlocked));
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
                OnPropertyChanged(nameof(FFUPath));
                SetSupportedFFUPath();
                OkCommand.RaiseCanExecuteChanged();
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
                    OnPropertyChanged(nameof(ProfileFFUPath));
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
                    OnPropertyChanged(nameof(SupportedFFUPath));

                    if (value != ValidatedSupportedFfuPath)
                    {
                        ValidateSupportedFfuPath();
                    }

                    OkCommand.RaiseCanExecuteChanged();
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
                OnPropertyChanged(nameof(LoadersPath));
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
                OnPropertyChanged(nameof(EDEPath));
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
                OnPropertyChanged(nameof(SBL3Path));
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
                OnPropertyChanged(nameof(CurrentMode));
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
                OnPropertyChanged(nameof(RootKeyHash));
            }
        }
        public DelegateCommand OkCommand { get; } = null;
        public DelegateCommand CancelCommand { get; } = null;
        public DelegateCommand FixCommand { get; } = null;
    }
}
