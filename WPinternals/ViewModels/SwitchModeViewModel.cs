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
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;

namespace WPinternals
{
    internal delegate void ModeSwitchProgressHandler(string Message, string SubMessage);
    internal delegate void ModeSwitchErrorHandler(string Message);
    internal delegate void ModeSwitchSuccessHandler(IDisposable NewModel, PhoneInterfaces NewInterface);

    internal class SwitchModeViewModel : ContextViewModel
    {
        protected PhoneNotifierViewModel PhoneNotifier;
        protected IDisposable CurrentModel;
        protected PhoneInterfaces? CurrentMode;
        protected PhoneInterfaces? TargetMode;
        protected bool IsSwitching = false;
        internal event ModeSwitchProgressHandler ModeSwitchProgress = delegate { };
        internal event ModeSwitchErrorHandler ModeSwitchError = delegate { };
        internal event ModeSwitchSuccessHandler ModeSwitchSuccess = delegate { };
        internal new SetWorkingStatus SetWorkingStatus = (m, s, v, a, st) => { };
        internal new UpdateWorkingStatus UpdateWorkingStatus = (m, s, v, st) => { };
        private string MassStorageWarning = null;

        internal SwitchModeViewModel(PhoneNotifierViewModel PhoneNotifier, PhoneInterfaces? TargetMode)
            : base()
        {
            if ((PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash) && (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Bootloader) && (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Label) && (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Normal))
            {
                throw new ArgumentException();
            }

            this.PhoneNotifier = PhoneNotifier;
            this.CurrentModel = (NokiaPhoneModel)PhoneNotifier.CurrentModel;
            this.CurrentMode = PhoneNotifier.CurrentInterface;
            this.TargetMode = TargetMode;

            if (this.CurrentMode == null)
            {
                LogFile.Log("Waiting for phone to connect...", LogType.FileAndConsole);
                PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
            }
            else
            {
                // Make sure this ViewModel has its View loaded before we continue,
                // or else loading of Views can get mixed up.
                if (SynchronizationContext.Current == null)
                {
                    StartSwitch();
                }
                else
                {
                    SynchronizationContext.Current.Post((s) => ((SwitchModeViewModel)s).StartSwitch(), this);
                }
            }
        }

        internal SwitchModeViewModel(PhoneNotifierViewModel PhoneNotifier, PhoneInterfaces? TargetMode, ModeSwitchProgressHandler ModeSwitchProgress, ModeSwitchErrorHandler ModeSwitchError, ModeSwitchSuccessHandler ModeSwitchSuccess, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null)
            : base()
        {
            if ((PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader) && (TargetMode == PhoneInterfaces.Lumia_Flash))
            {
                LumiaBootManagerPhoneInfo Info = ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo(false);
                if (Info.BootManagerProtocolVersionMajor >= 2)
                {
                    try
                    {
                        // The implementation of SwitchToFlashAppContext() is improved
                        // SwitchToFlashAppContext() should only be used with BootMgr v2
                        // For switching from BootMgr to FlashApp, it will use NOKS
                        // That will switch to a charging state, whereas a normal context switch will not start charging
                        // The implementation of NOKS in BootMgr mode has changed in BootMgr v2
                        // It does not disconnect / reconnect anymore and the apptype is changed immediately
                        // NOKS still doesnt return a status
                        // BootMgr v1 uses normal NOKS and waits for arrival of FlashApp
                        ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).SwitchToFlashAppContext();

                        // But this was called as a real switch, so we will raise an arrival event.
                        PhoneNotifier.CurrentInterface = PhoneInterfaces.Lumia_Flash;
                        PhoneNotifier.NotifyArrival();
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }
                }
            }

            if (PhoneNotifier.CurrentInterface == TargetMode)
            {
                ModeSwitchSuccess(PhoneNotifier.CurrentModel, (PhoneInterfaces)PhoneNotifier.CurrentInterface);
            }
            else
            {
                this.PhoneNotifier = PhoneNotifier;
                this.CurrentModel = (NokiaPhoneModel)PhoneNotifier.CurrentModel;
                this.CurrentMode = PhoneNotifier.CurrentInterface;
                this.TargetMode = TargetMode;
                if (ModeSwitchProgress != null)
                {
                    this.ModeSwitchProgress += ModeSwitchProgress;
                }

                if (ModeSwitchError != null)
                {
                    this.ModeSwitchError += ModeSwitchError;
                }

                if (ModeSwitchSuccess != null)
                {
                    this.ModeSwitchSuccess += ModeSwitchSuccess;
                }

                if (SetWorkingStatus != null)
                {
                    this.SetWorkingStatus = SetWorkingStatus;
                }

                if (UpdateWorkingStatus != null)
                {
                    this.UpdateWorkingStatus = UpdateWorkingStatus;
                }

                if (this.CurrentMode == null)
                {
                    LogFile.Log("Waiting for phone to connect...", LogType.FileAndConsole);
                    PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                }
                else
                {
                    // Make sure this ViewModel has its View loaded before we continue,
                    // or else loading of Views can get mixed up.
                    if (SynchronizationContext.Current == null)
                    {
                        StartSwitch();
                    }
                    else
                    {
                        SynchronizationContext.Current.Post((s) => ((SwitchModeViewModel)s).StartSwitch(), this);
                    }
                }
            }
        }

        private void ModeSwitchProgressWrapper(string Message, string SubMessage)
        {
            if ((UIContext == null) || (SynchronizationContext.Current == UIContext))
            {
                ModeSwitchProgress(Message, SubMessage);
                SetWorkingStatus(Message, SubMessage);
            }
            else
            {
                UIContext.Post(s =>
                {
                    ModeSwitchProgress(Message, SubMessage);
                    SetWorkingStatus(Message, SubMessage);
                }, null);
            }
        }

        private void ModeSwitchErrorWrapper(string Message)
        {
            IsSwitching = false;
            if ((UIContext == null) || (SynchronizationContext.Current == UIContext))
            {
                ModeSwitchError(Message);
            }
            else
            {
                UIContext.Post(s => ModeSwitchError(Message), null);
            }
        }

        private void ModeSwitchSuccessWrapper()
        {
            IsSwitching = false;
            if ((UIContext == null) || (SynchronizationContext.Current == UIContext))
            {
                if (PhoneNotifier.CurrentInterface == null)
                {
                    ModeSwitchErrorWrapper("Phone disconnected");
                }
                else
                {
                    ModeSwitchSuccess(PhoneNotifier.CurrentModel, (PhoneInterfaces)PhoneNotifier.CurrentInterface);
                }
            }
            else
            {
                if (PhoneNotifier.CurrentInterface == null)
                {
                    UIContext.Post(s => ModeSwitchErrorWrapper("Phone disconnected"), null);
                }
                else
                {
                    UIContext.Post(s => ModeSwitchSuccess(PhoneNotifier.CurrentModel, (PhoneInterfaces)PhoneNotifier.CurrentInterface), null);
                }
            }
        }

        ~SwitchModeViewModel()
        {
            if (PhoneNotifier != null)
            {
                PhoneNotifier.NewDeviceArrived -= NewDeviceArrived;
            }
        }

        internal void StartSwitch()
        {
            IsSwitching = true;

            byte[] BootModeFlagCommand = [0x4E, 0x4F, 0x4B, 0x58, 0x46, 0x57, 0x00, 0x55, 0x42, 0x46, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00]; // NOKFW UBF
            byte[] RebootCommand = [0x4E, 0x4F, 0x4B, 0x52]; // NOKR
            byte[] RebootCommandResult;

            bool ModernFlashApp;

            // Make switch and set message or navigate to error
            switch (CurrentMode)
            {
                case PhoneInterfaces.Lumia_Normal:
                case PhoneInterfaces.Lumia_Label:
                    string DeviceMode;

                    switch (TargetMode)
                    {
                        case PhoneInterfaces.Lumia_Normal:
                            DeviceMode = "Normal";
                            IsSwitchingInterface = true;
                            ModeSwitchProgressWrapper("Rebooting phone to Normal mode...", null);
                            LogFile.Log("Rebooting phone to Normal mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Bootloader:
                            DeviceMode = "Normal";
                            IsSwitchingInterface = true;
                            ModeSwitchProgressWrapper("Rebooting phone to Bootloader mode...", null);
                            LogFile.Log("Rebooting phone to Bootloader mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Flash:
                            DeviceMode = "Flash";
                            IsSwitchingInterface = true;
                            ModeSwitchProgressWrapper("Rebooting phone to Flash mode...", null);
                            LogFile.Log("Rebooting phone to Flash mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Label:
                            DeviceMode = "Flash";
                            IsSwitchingInterface = true;
                            ModeSwitchProgressWrapper("First rebooting phone to Flash mode...", null);
                            LogFile.Log("First rebooting phone to Flash mode (then attempt Label mode)", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_MassStorage:
                            DeviceMode = "Flash";
                            IsSwitchingInterface = true;
                            ModeSwitchProgressWrapper("First rebooting phone to Flash mode...", null);
                            LogFile.Log("First rebooting phone to Flash mode (then attempt Mass Storage mode)", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Qualcomm_Download: // Only available in new Lumia's
                            DeviceMode = "Flash";
                            IsSwitchingInterface = true;
                            ModeSwitchProgressWrapper("First rebooting phone to Flash mode...", null);
                            LogFile.Log("First rebooting phone to Flash mode (then attempt Qualcomm Download mode", LogType.FileAndConsole);
                            break;
                        default:
                            return;
                    }

                    Dictionary<string, object> Params = new();
                    Params.Add("DeviceMode", DeviceMode);
                    Params.Add("ResetMethod", "HwReset");
                    try
                    {
                        ((NokiaPhoneModel)PhoneNotifier.CurrentModel).ExecuteJsonMethodAsync("SetDeviceMode", Params);
                        PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        ModeSwitchErrorWrapper("Failed to switch to Qualcomm Download mode");
                        IsSwitchingInterface = false;
                    }
                    break;
                case PhoneInterfaces.Lumia_Flash:
                    IsSwitchingInterface = true;
                    switch (TargetMode)
                    {
                        case null:
                            ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).Shutdown();
                            ModeSwitchProgressWrapper("Please disconnect your device. Waiting...", null);
                            LogFile.Log("Please disconnect your device. Waiting...", LogType.FileAndConsole);
                            new Thread(() =>
                            {
                                PhoneNotifier.WaitForRemoval().Wait();
                                ModeSwitchSuccessWrapper();
                            }).Start();
                            break;
                        case PhoneInterfaces.Lumia_Normal:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
                            ModeSwitchProgressWrapper("Rebooting phone to Normal mode...", null);
                            LogFile.Log("Rebooting phone to Normal mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Bootloader:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
                            ModeSwitchProgressWrapper("Rebooting phone to Bootloader mode...", null);
                            LogFile.Log("Rebooting phone to Bootloader mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_PhoneInfo:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ModernFlashApp = ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo().FlashAppProtocolVersionMajor >= 2;
                            if (ModernFlashApp)
                            {
                                ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).SwitchToPhoneInfoAppContext();
                            }
                            else
                            {
                                ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).SwitchToPhoneInfoAppContextLegacy();
                            }
                            ModeSwitchProgressWrapper("Rebooting phone to Phone Info mode...", null);
                            LogFile.Log("Rebooting phone to Phone Info mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Label:
                            SwitchFromFlashToLabelMode();
                            break;
                        case PhoneInterfaces.Lumia_Flash: // attempt to boot from limited flash to full flash
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            byte[] RebootToFlashCommand = [0x4E, 0x4F, 0x4B, 0x53]; // NOKS
                            ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootToFlashCommand);
                            ModeSwitchProgressWrapper("Rebooting phone to Flash mode...", null);
                            LogFile.Log("Rebooting phone to Flash mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_MassStorage:
                            SwitchFromFlashToMassStorageMode();
                            break;
                        case PhoneInterfaces.Qualcomm_Download:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            byte[] RebootToQualcommDownloadCommand = [0x4E, 0x4F, 0x4B, 0x58, 0x43, 0x42, 0x45]; // NOKXCBE // TODO
                            RebootCommandResult = ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(RebootToQualcommDownloadCommand);
                            if (RebootCommandResult?.Length == 4) // This means fail: NOKU (unknow command)
                            {
                                IsSwitchingInterface = false;
                                ModeSwitchErrorWrapper("Failed to switch to Qualcomm Download mode");
                            }
                            else
                            {
                                ModeSwitchProgressWrapper("Rebooting phone to Qualcomm Download mode...", null);
                                LogFile.Log("Rebooting phone to Qualcomm Download mode", LogType.FileAndConsole);
                            }
                            break;
                        default:
                            return;
                    }
                    break;
                case PhoneInterfaces.Lumia_PhoneInfo:
                    IsSwitchingInterface = true;
                    switch (TargetMode)
                    {
                        case PhoneInterfaces.Lumia_Normal:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
                            ModeSwitchProgressWrapper("Rebooting phone to Normal mode...", null);
                            LogFile.Log("Rebooting phone to Normal mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Bootloader:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ModernFlashApp = ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo().PhoneInfoAppVersionMajor >= 2;
                            if (ModernFlashApp)
                            {
                                ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).SwitchToBootManagerContext();
                            }
                            ModeSwitchProgressWrapper("Rebooting phone to Bootloader mode...", null);
                            LogFile.Log("Rebooting phone to Bootloader mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_PhoneInfo: // attempt to boot from limited phone info to full phone info
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ModernFlashApp = ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo().PhoneInfoAppVersionMajor >= 2;
                            if (ModernFlashApp)
                            {
                                ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).SwitchToPhoneInfoAppContext();
                            }
                            ModeSwitchProgressWrapper("Rebooting phone to Phone Info mode...", null);
                            LogFile.Log("Rebooting phone to Phone Info mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Label:
                            SwitchFromPhoneInfoToLabelMode();
                            break;
                        case PhoneInterfaces.Lumia_Flash:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ModernFlashApp = ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo().PhoneInfoAppVersionMajor >= 2;
                            if (ModernFlashApp)
                            {
                                ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).SwitchToFlashAppContext();
                            }
                            else
                            {
                                ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ContinueBoot();
                            }
                            ModeSwitchProgressWrapper("Rebooting phone to Flash mode...", null);
                            LogFile.Log("Rebooting phone to Flash mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_MassStorage:
                            SwitchFromPhoneInfoToMassStorageMode();
                            break;
                        case PhoneInterfaces.Qualcomm_Download:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            byte[] RebootToQualcommDownloadCommand = [0x4E, 0x4F, 0x4B, 0x58, 0x43, 0x42, 0x45]; // NOKXCBE // TODO
                            RebootCommandResult = ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(RebootToQualcommDownloadCommand);
                            if (RebootCommandResult?.Length == 4) // This means fail: NOKU (unknow command)
                            {
                                IsSwitchingInterface = false;
                                ModeSwitchErrorWrapper("Failed to switch to Qualcomm Download mode");
                            }
                            else
                            {
                                ModeSwitchProgressWrapper("Rebooting phone to Qualcomm Download mode...", null);
                                LogFile.Log("Rebooting phone to Qualcomm Download mode", LogType.FileAndConsole);
                            }
                            break;
                        default:
                            return;
                    }
                    break;
                case PhoneInterfaces.Lumia_Bootloader:
                    IsSwitchingInterface = true;
                    switch (TargetMode)
                    {
                        case null:
                            ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).Shutdown();
                            ModeSwitchProgressWrapper("Please disconnect your device. Waiting...", null);
                            LogFile.Log("Please disconnect your device. Waiting...", LogType.FileAndConsole);
                            new Thread(() =>
                            {
                                PhoneNotifier.WaitForRemoval().Wait();
                                ModeSwitchSuccessWrapper();
                            }).Start();
                            break;
                        case PhoneInterfaces.Lumia_Normal:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
                            ModeSwitchProgressWrapper("Rebooting phone to Normal mode...", null);
                            LogFile.Log("Rebooting phone to Normal mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Bootloader:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
                            ModeSwitchProgressWrapper("Rebooting phone to Bootloader mode...", null);
                            LogFile.Log("Rebooting phone to Bootloader mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Label:
                            SwitchFromFlashToLabelMode();
                            break;
                        case PhoneInterfaces.Lumia_Flash: // attempt to boot from limited flash to full flash
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            byte[] RebootToFlashCommand = [0x4E, 0x4F, 0x4B, 0x53]; // NOKS
                            ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootToFlashCommand);
                            ModeSwitchProgressWrapper("Rebooting phone to Flash mode...", null);
                            LogFile.Log("Rebooting phone to Flash mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_MassStorage:
                            SwitchFromFlashToMassStorageMode();
                            break;
                        case PhoneInterfaces.Qualcomm_Download:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            byte[] RebootToQualcommDownloadCommand = [0x4E, 0x4F, 0x4B, 0x58, 0x43, 0x42, 0x45]; // NOKXCBE // TODO
                            RebootCommandResult = ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(RebootToQualcommDownloadCommand);
                            if (RebootCommandResult?.Length == 4) // This means fail: NOKU (unknow command)
                            {
                                IsSwitchingInterface = false;
                                ModeSwitchErrorWrapper("Failed to switch to Qualcomm Download mode");
                            }
                            else
                            {
                                ModeSwitchProgressWrapper("Rebooting phone to Qualcomm Download mode...", null);
                                LogFile.Log("Rebooting phone to Qualcomm Download mode", LogType.FileAndConsole);
                            }
                            break;
                        default:
                            return;
                    }
                    break;
                case PhoneInterfaces.Lumia_MassStorage:
                    IsSwitchingInterface = true;
                    switch (TargetMode)
                    {
                        case PhoneInterfaces.Lumia_Normal:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                            ((MassStorage)PhoneNotifier.CurrentModel).Reboot();
                            ModeSwitchProgressWrapper("Rebooting phone to Normal mode...", null);
                            LogFile.Log("Rebooting phone to Normal mode", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Label:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrivedFromMassStorageMode;
                            ((MassStorage)PhoneNotifier.CurrentModel).Reboot();
                            ModeSwitchProgressWrapper("Rebooting phone to Label mode...", null);
                            LogFile.Log("Rebooting phone to Label mode...", LogType.FileAndConsole);
                            break;
                        case PhoneInterfaces.Lumia_Flash:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrivedFromMassStorageMode;
                            ((MassStorage)PhoneNotifier.CurrentModel).Reboot();
                            ModeSwitchProgressWrapper("Rebooting phone to Flash mode...", null);
                            LogFile.Log("Rebooting phone to Flash mode...", LogType.FileAndConsole);
                            break;
                        case null:
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrivedFromMassStorageMode;
                            ((MassStorage)PhoneNotifier.CurrentModel).Reboot();
                            ModeSwitchProgressWrapper("First rebooting phone to Flash mode...", null);
                            LogFile.Log("First rebooting phone to Bootloader mode...", LogType.FileAndConsole);
                            break;
                        default:
                            return;
                    }
                    break;
                case PhoneInterfaces.Qualcomm_Download:
                    // TODO: don't know how to switch from Qualcomm Download mode to other mode
                    break;
            }
        }

        private void NewDeviceArrivedFromMassStorageMode(ArrivalEventArgs Args)
        {
            PhoneNotifier.NewDeviceArrived -= NewDeviceArrivedFromMassStorageMode;

            CurrentModel = Args.NewModel;
            CurrentMode = Args.NewInterface;

            // After the mass storage mode reboot command, the phone must be in Bootloader mode.
            // If it isn't, something unexpected happened and the phone can't be switched.
            //
            if (CurrentMode == PhoneInterfaces.Lumia_Bootloader)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await SwitchToWithStatus(PhoneNotifier, TargetMode, SetWorkingStatus, UpdateWorkingStatus);
                        ModeSwitchSuccessWrapper();
                    }
                    catch
                    {
                        switch (TargetMode)
                        {
                            case PhoneInterfaces.Lumia_Flash:
                                ModeSwitchErrorWrapper("Failed to switch to Flash mode");
                                break;
                            case PhoneInterfaces.Lumia_Bootloader:
                                ModeSwitchErrorWrapper("Failed to switch to Boot Manager mode");
                                break;
                            case PhoneInterfaces.Lumia_PhoneInfo:
                                ModeSwitchErrorWrapper("Failed to switch to Phone Info mode");
                                break;
                            case PhoneInterfaces.Lumia_Label:
                                ModeSwitchErrorWrapper("Failed to switch to Label mode");
                                break;
                            case PhoneInterfaces.Lumia_Normal:
                                ModeSwitchErrorWrapper("Failed to switch to Normal mode");
                                break;
                            case null:
                                ModeSwitchSuccessWrapper();
                                break;
                        }
                    }
                });
            }
            else
            {
                switch (TargetMode)
                {
                    case PhoneInterfaces.Lumia_Flash:
                        ModeSwitchErrorWrapper("Failed to switch to Flash mode");
                        break;
                    case PhoneInterfaces.Lumia_Bootloader:
                        ModeSwitchErrorWrapper("Failed to switch to Boot Manager mode");
                        break;
                    case PhoneInterfaces.Lumia_PhoneInfo:
                        ModeSwitchErrorWrapper("Failed to switch to Phone Info mode");
                        break;
                    case PhoneInterfaces.Lumia_Label:
                        ModeSwitchErrorWrapper("Failed to switch to Label mode");
                        break;
                    case PhoneInterfaces.Lumia_Normal:
                        ModeSwitchErrorWrapper("Failed to switch to Normal mode");
                        break;
                    case null:
                        ModeSwitchErrorWrapper("Failed to shutdown");
                        break;
                }
            }
        }

        private void NewDeviceArrived(ArrivalEventArgs Args)
        {
            PhoneNotifier.NewDeviceArrived -= NewDeviceArrived;

            CurrentModel = Args.NewModel;
            CurrentMode = Args.NewInterface;

            if ((CurrentMode == PhoneInterfaces.Lumia_Bootloader) && (TargetMode == PhoneInterfaces.Lumia_Flash))
            {
                try
                {
                    // Going from BootMgr to FlashApp
                    // SwitchToFlashAppContext() will only switch context. Phone will not charge.
                    // ResetPhoneToFlashMode() reboots to real flash app. Phone will charge. Works when in BootMgrApp, not when already in FlashApp.

                    ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).ResetPhoneToFlashMode();
                    CurrentMode = PhoneInterfaces.Lumia_Flash;
                    PhoneNotifier.NotifyArrival();
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
            }

            if (CurrentMode == TargetMode)
            {
                if (TargetMode == PhoneInterfaces.Lumia_Bootloader)
                {
                    ((NokiaFlashModel)PhoneNotifier.CurrentModel).DisableRebootTimeOut();
                }

                ModeSwitchSuccessWrapper();
            }
            else if (!IsSwitching)
            {
                StartSwitch();
            }
            else if ((CurrentMode == PhoneInterfaces.Lumia_Bootloader) && (TargetMode == PhoneInterfaces.Lumia_Normal))
            {
                // Do nothing, because booting to Normal shortly goes into Flash mode too.
                // Just wait to arrive in Normal mode;
                PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
            }
            else if ((CurrentMode == PhoneInterfaces.Lumia_Flash) && (TargetMode == PhoneInterfaces.Lumia_MassStorage))
            {
                SwitchFromFlashToMassStorageMode(Continuation: true);
            }
            else if ((CurrentMode == PhoneInterfaces.Lumia_Flash) && (TargetMode == PhoneInterfaces.Lumia_Label))
            {
                SwitchFromFlashToLabelMode(Continuation: true);
            }
            else if ((CurrentMode == PhoneInterfaces.Lumia_Flash) && (TargetMode == PhoneInterfaces.Qualcomm_Download))
            {
                byte[] RebootCommand = [0x4E, 0x4F, 0x4B, 0x52];
                byte[] RebootToQualcommDownloadCommand = [0x4E, 0x4F, 0x4B, 0x58, 0x43, 0x42, 0x45]; // NOKXCBE // TODO
                IsSwitchingInterface = true;
                LogFile.Log("Sending command for rebooting to Emergency Download mode");
                byte[] RebootCommandResult = ((NokiaPhoneModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(RebootToQualcommDownloadCommand);
                if (RebootCommandResult?.Length >= 8)
                {
                    int ResultCode = (RebootCommandResult[6] << 8) + RebootCommandResult[7];
                    LogFile.Log("Resultcode: 0x" + ResultCode.ToString("X4"));
                }
                if (RebootCommandResult?.Length == 4) // This means fail: NOKU (unknow command)
                {
                    ModeSwitchErrorWrapper("Failed to switch to Qualcomm Download mode");
                    IsSwitchingInterface = false;
                }
                else
                {
                    PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
                    ModeSwitchProgressWrapper("And now rebooting phone to Qualcomm Download mode...", null);
                    LogFile.Log("Rebooting phone to Qualcomm Download mode");
                }
            }
            else
            {
                switch (TargetMode)
                {
                    case PhoneInterfaces.Lumia_Normal:
                        ModeSwitchErrorWrapper("Failed to switch to Normal mode");
                        break;
                    case PhoneInterfaces.Lumia_Flash:
                        ModeSwitchErrorWrapper("Failed to switch to Flash mode");
                        break;
                    case PhoneInterfaces.Lumia_Bootloader:
                        ModeSwitchErrorWrapper("Failed to switch to Boot Manager mode");
                        break;
                    case PhoneInterfaces.Lumia_PhoneInfo:
                        ModeSwitchErrorWrapper("Failed to switch to Phone Info mode");
                        break;
                    case PhoneInterfaces.Lumia_Label:
                        ModeSwitchErrorWrapper("Failed to switch to Label mode");
                        break;
                    case PhoneInterfaces.Lumia_MassStorage:
                        ModeSwitchErrorWrapper("Failed to switch to Mass Storage mode");
                        break;
                }
            }
        }

        private void SwitchFromPhoneInfoToLabelMode(bool Continuation = false)
        {
            if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
            {
                throw new WPinternalsException("Unexpected Mode");
            }

            string ProgressText = Continuation ? "And now preparing to boot the phone to Label mode..." : "Preparing to boot the phone to Label mode...";

            LumiaPhoneInfoAppPhoneInfo PhoneInfoAppInfo = ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo(ExtendedInfo: true);

            bool ModernFlashApp = ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo().PhoneInfoAppVersionMajor >= 2;
            if (ModernFlashApp)
            {
                ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).SwitchToFlashAppContext();
            }
            else
            {
                ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ContinueBoot();
            }

            Task.Run(async () =>
            {
                if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                {
                    await PhoneNotifier.WaitForArrival();
                }

                void Finish()
                {
                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        throw new WPinternalsException("Unexpected Mode");
                    }

                    LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;
                    LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo(ExtendedInfo: true);

                    if (Info.MmosOverUsbSupported)
                    {
                        LogFile.BeginAction("SwitchToLabelMode");

                        try
                        {
                            ModeSwitchProgressWrapper(ProgressText, null);

                            string TempFolder = $@"{Environment.GetEnvironmentVariable("TEMP")}\WPInternals";

                            if (PhoneInfoAppInfo.Type == "RM-1152")
                            {
                                PhoneInfoAppInfo.Type = "RM-1151";
                            }

                            (string ENOSWFileUrl, string DPLFileUrl) = LumiaDownloadModel.SearchENOSW(PhoneInfoAppInfo.Type, Info.Firmware);

                            UIContext?.Post(d => SetWorkingStatus($"Downloading {PhoneInfoAppInfo.Type} Test Mode package...", MaxProgressValue: 100), null);

                            DownloadEntry downloadEntry = new(ENOSWFileUrl, TempFolder, [ENOSWFileUrl], ENOSWDownloadCompleted, Info.Firmware);

                            downloadEntry.PropertyChanged += (object sender, System.ComponentModel.PropertyChangedEventArgs e) =>
                            {
                                if (e.PropertyName == "Progress")
                                {
                                    int progress = (sender as DownloadEntry)?.Progress ?? 0;
                                    ulong.TryParse(progress.ToString(), out ulong progressret);
                                    UIContext?.Post(d => UpdateWorkingStatus(null, CurrentProgressValue: progressret), null);
                                }
                            };
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            ModeSwitchErrorWrapper(Ex.Message);
                        }

                        LogFile.EndAction("SwitchToLabelMode");
                    }
                    else
                    {
                        PhoneNotifier.NewDeviceArrived += NewDeviceArrived;

                        byte[] BootModeFlagCommand = [0x4E, 0x4F, 0x4B, 0x58, 0x46, 0x57, 0x00, 0x55, 0x42, 0x46, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00]; // NOKFW UBF
                        byte[] RebootCommand = [0x4E, 0x4F, 0x4B, 0x52]; // NOKR

                        BootModeFlagCommand[0x0F] = 0x59;
                        ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(BootModeFlagCommand);
                        ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
                        ModeSwitchProgressWrapper("Rebooting phone to Label mode", null);
                        LogFile.Log("Rebooting phone to Label mode", LogType.FileAndConsole);
                    }
                }

                UIContext?.Post(d => Finish(), null);
            });
        }

        private void SwitchFromFlashToLabelMode(bool Continuation = false)
        {
            string ProgressText = Continuation ? "And now preparing to boot the phone to Label mode..." : "Preparing to boot the phone to Label mode...";

            if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
            {
                throw new WPinternalsException("Unexpected Mode");
            }

            LumiaFlashAppPhoneInfo Info = ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo(ExtendedInfo: true);

            bool ModernFlashApp = ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo().FlashAppProtocolVersionMajor >= 2;
            if (ModernFlashApp)
            {
                ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).SwitchToPhoneInfoAppContext();
            }
            else
            {
                ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).SwitchToPhoneInfoAppContextLegacy();
            }

            Task.Run(async () =>
            {
                if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                {
                    await PhoneNotifier.WaitForArrival();
                }

                if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                {
                    throw new WPinternalsException("Unexpected Mode");
                }

                LumiaPhoneInfoAppModel LumiaPhoneInfoAppModel = (LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel;
                LumiaPhoneInfoAppPhoneInfo PhoneInfoAppInfo = LumiaPhoneInfoAppModel.ReadPhoneInfo(ExtendedInfo: true);

                ModernFlashApp = PhoneInfoAppInfo.PhoneInfoAppVersionMajor >= 2;
                if (ModernFlashApp)
                {
                    LumiaPhoneInfoAppModel.SwitchToFlashAppContext();
                }
                else
                {
                    LumiaPhoneInfoAppModel.ContinueBoot();
                }

                if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                {
                    await PhoneNotifier.WaitForArrival();
                }

                void Finish()
                {
                    if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        throw new WPinternalsException("Unexpected Mode");
                    }

                    LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;

                    if (Info.MmosOverUsbSupported)
                    {
                        LogFile.BeginAction("SwitchToLabelMode");

                        try
                        {
                            ModeSwitchProgressWrapper(ProgressText, null);

                            string TempFolder = $@"{Environment.GetEnvironmentVariable("TEMP")}\WPInternals";

                            if (PhoneInfoAppInfo.Type == "RM-1152")
                            {
                                PhoneInfoAppInfo.Type = "RM-1151";
                            }

                            (string ENOSWFileUrl, string DPLFileUrl) = LumiaDownloadModel.SearchENOSW(PhoneInfoAppInfo.Type, Info.Firmware);

                            UIContext?.Post(d => SetWorkingStatus($"Downloading {PhoneInfoAppInfo.Type} Test Mode package...", MaxProgressValue: 100), null);

                            DownloadEntry downloadEntry = new(ENOSWFileUrl, TempFolder, [ENOSWFileUrl], ENOSWDownloadCompleted, Info.Firmware);

                            downloadEntry.PropertyChanged += (object sender, System.ComponentModel.PropertyChangedEventArgs e) =>
                            {
                                if (e.PropertyName == "Progress")
                                {
                                    int progress = (sender as DownloadEntry)?.Progress ?? 0;
                                    ulong.TryParse(progress.ToString(), out ulong progressret);
                                    UIContext?.Post(d => UpdateWorkingStatus(null, CurrentProgressValue: progressret), null);
                                }
                            };
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            ModeSwitchErrorWrapper(Ex.Message);
                        }

                        LogFile.EndAction("SwitchToLabelMode");
                    }
                    else
                    {
                        PhoneNotifier.NewDeviceArrived += NewDeviceArrived;

                        byte[] BootModeFlagCommand = [0x4E, 0x4F, 0x4B, 0x58, 0x46, 0x57, 0x00, 0x55, 0x42, 0x46, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00]; // NOKFW UBF
                        byte[] RebootCommand = [0x4E, 0x4F, 0x4B, 0x52]; // NOKR

                        BootModeFlagCommand[0x0F] = 0x59;
                        ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(BootModeFlagCommand);
                        ((LumiaFlashAppModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
                        ModeSwitchProgressWrapper("Rebooting phone to Label mode", null);
                        LogFile.Log("Rebooting phone to Label mode", LogType.FileAndConsole);
                    }
                }

                UIContext?.Post(d => Finish(), null);
            });
        }

        private void ENOSWDownloadCompleted(string[] URLs, object State)
        {
            string Firmware = (string)State;
            string ENOSWFileUrl = URLs[0];
            string Name = DownloadsViewModel.GetFileNameFromURL(ENOSWFileUrl);
            string TempFolder = $@"{Environment.GetEnvironmentVariable("TEMP")}\WPInternals";
            LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;

            Task.Run(() =>
            {
                ModeSwitchProgressWrapper("Initializing Flash...", null);

                string MMOSPath = Path.Combine(TempFolder, Name);

                App.Config.AddSecWimToRepository(MMOSPath, Firmware);

                PhoneNotifier.NewDeviceArrived += NewDeviceArrived;

                LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo();

                FileInfo info = new(MMOSPath);
                uint length = uint.Parse(info.Length.ToString());

                int maximumBufferSize = (int)Info.WriteBufferSize;

                uint chunkCount = (uint)Math.Truncate((decimal)length / maximumBufferSize);

                UIContext?.Post(d => SetWorkingStatus("Flashing Test Mode package...", MaxProgressValue: 100), null);

                ProgressUpdater progressUpdater = new(chunkCount + 1, (int i, TimeSpan? time) => UpdateWorkingStatus(null, CurrentProgressValue: (ulong)i));

                FlashModel.FlashMMOS(MMOSPath, progressUpdater);

                ModeSwitchProgressWrapper("And now booting phone to MMOS...", "If the phone stays on the lightning cog screen for a while, you may need to unplug and replug the phone to continue the boot process.");
            });
        }

        private void SwitchFromPhoneInfoToMassStorageMode(bool Continuation = false)
        {
            string ProgressText = Continuation ? "And now rebooting phone to Mass Storage mode..." : "Rebooting phone to Mass Storage mode...";

            new Thread(async () =>
            {
                bool ModernFlashApp = ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ReadPhoneInfo().PhoneInfoAppVersionMajor >= 2;
                if (ModernFlashApp)
                {
                    ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).SwitchToFlashAppContext();
                }
                else
                {
                    ((LumiaPhoneInfoAppModel)PhoneNotifier.CurrentModel).ContinueBoot();
                }
                await PhoneNotifier.WaitForArrival();

                LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;
                LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo(ExtendedInfo: false);

                MassStorageWarning = null;
                if (Info.FlashAppProtocolVersionMajor < 2)
                {
                    MassStorageWarning = "Switching to Mass Storage mode should take about 10 seconds. The phone should be unlocked using an Engineering SBL3 to enable Mass Storage mode. When you unlocked the bootloader, but you did not use an Engineering SBL3, an attempt to boot to Mass Storage mode may result in an unresponsive state. Installing drivers for this interface may also cause to hang the PC. So when this switch is taking too long, you should reboot both the PC and the phone. To reboot the phone, you have to perform a soft-reset. Press and hold the volume-down-button and the power-button at the same time for at least 10 seconds. This will trigger a power-cycle and the phone will reboot. Once fully booted, the phone may show strange behavior, like complaining about mail-accounts, showing old text-messages, inability to load https-websites, etc. This is expected behavior, because the time-settings of the phone are incorrect. Just wait a few seconds for the phone to get a data-connection and have the date/time synced. After that the strange behavior will stop automatically and normal operation is resumed.";
                }
                else
                {
                    MassStorageWarning = "When the screen of the phone is black for a while, it could be that the phone is already in Mass Storage Mode, but there is no drive-letter assigned. To resolve this issue, open Device Manager and manually assign a drive-letter to the MainOS partition of your phone, or open a command-prompt and type: diskpart automount enable.";
                    if (App.IsPnPEventLogMissing)
                    {
                        MassStorageWarning += " It is also possible that the phone is in Mass Storage mode, but the Mass Storage driver on this PC failed. Your PC does not have an eventlog to detect this misbehaviour. But in this case you will see a device with an exclamation mark in Device Manager and then you need to manually reset the phone by pressing and holding the power-button for at least 10 seconds until it vibrates and reboots. After that Windows Phone Internals will revert the changes. After the phone has rebooted to the OS, you can retry to unlock the bootloader.";
                    }
                }

                bool IsOldLumia = Info.FlashAppProtocolVersionMajor < 2;
                bool IsNewLumia = Info.FlashAppProtocolVersionMajor >= 2;
                bool IsUnlockedNew = false;
                if (IsNewLumia)
                {
                    GPT GPT = FlashModel.ReadGPT();
                    IsUnlockedNew = (GPT.GetPartition("IS_UNLOCKED") != null) || (GPT.GetPartition("BACKUP_EFIESP") != null) || (GPT.GetPartition("BACKUP_BS_NV") != null);
                }
                bool IsOriginalEngineeringLumia = !Info.IsBootloaderSecure && !IsUnlockedNew;

                if (IsOldLumia || IsOriginalEngineeringLumia)
                {
                    byte[] BootModeFlagCommand = [0x4E, 0x4F, 0x4B, 0x58, 0x46, 0x57, 0x00, 0x55, 0x42, 0x46, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00]; // NOKFW UBF
                    byte[] RebootCommand = [0x4E, 0x4F, 0x4B, 0x52];
                    byte[] RebootToMassStorageCommand = [0x4E, 0x4F, 0x4B, 0x4D]; // NOKM
                    IsSwitchingInterface = true;
                    byte[] RebootCommandResult = ((NokiaPhoneModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(RebootToMassStorageCommand);
                    if (RebootCommandResult?.Length == 4) // This means fail: NOKU (unknown command)
                    {
                        BootModeFlagCommand[0x0F] = 0x4D;
                        byte[] BootFlagResult = ((NokiaPhoneModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(BootModeFlagCommand);
                        UInt16 ResultCode = BitConverter.ToUInt16(BootFlagResult, 6);
                        if (ResultCode == 0)
                        {
                            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;

                            ((NokiaPhoneModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
                            ModeSwitchProgressWrapper(ProgressText, MassStorageWarning);
                            LogFile.Log("Rebooting phone to Mass Storage mode");
                        }
                        else
                        {
                            ModeSwitchErrorWrapper("Failed to switch to Mass Storage mode");
                            IsSwitchingInterface = false;
                        }
                    }
                    else
                    {
                        PhoneNotifier.NewDeviceArrived += NewDeviceArrived;

                        ModeSwitchProgressWrapper(ProgressText, MassStorageWarning);
                        LogFile.Log("Rebooting phone to Mass Storage mode");
                    }
                }
                else if (IsUnlockedNew)
                {
                    new Thread(async () =>
                    {
                        LogFile.BeginAction("SwitchToMassStorageMode");

                        try
                        {
                            // Implementation of writing a partition with SecureBoot variable to the phone
                            ModeSwitchProgressWrapper(ProgressText, MassStorageWarning);
                            LogFile.Log("Preparing phone for Mass Storage Mode", LogType.FileAndConsole);
                            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                            // Magic!
                            // The SBMSM resource is a compressed version of a raw NV-variable-partition.
                            // In this partition the SecureBoot variable is disabled and an extra variable is added which triggers Mass Storage Mode on next reboot.
                            // It overwrites the variable in a different NV-partition than where this variable is stored usually.
                            // This normally leads to endless-loops when the NV-variables are enumerated.
                            // But the partition contains an extra hack to break out the endless loops.
                            using (var stream = assembly.GetManifestResourceStream("WPinternals.SBMSM"))
                            {
                                using DecompressedStream dec = new(stream);
                                using MemoryStream SB = new(); // Must be a seekable stream!
                                dec.CopyTo(SB);

                                // We don't need to check for the BACKUP_BS_NV partition here,
                                // because the SecureBoot flag is disabled here.
                                // So either the NV was already backupped or already overwritten.

                                GPT GPT = FlashModel.ReadGPT();
                                Partition Target = GPT.GetPartition("UEFI_BS_NV");

                                // We've been reading the GPT, so we let the phone reset once more to be sure that memory maps are the same
                                WPinternalsStatus LastStatus = WPinternalsStatus.Undefined;
                                List<FlashPart> Parts = new();
                                FlashPart Part = new();
                                Part.StartSector = (uint)Target.FirstSector;
                                Part.Stream = SB;
                                Parts.Add(Part);
                                await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(PhoneNotifier, null, false, false, Parts, DoResetFirst: true, ClearFlashingStatusAtEnd: false, ShowProgress: false,
                                    SetWorkingStatus: (m, s, v, a, st) =>
                                    {
                                        if (SetWorkingStatus != null)
                                        {
                                            if ((st == WPinternalsStatus.Scanning) || (st == WPinternalsStatus.WaitingForManualReset))
                                            {
                                                SetWorkingStatus(m, s, v, a, st);
                                            }
                                            else if ((LastStatus == WPinternalsStatus.Scanning) || (LastStatus == WPinternalsStatus.WaitingForManualReset))
                                            {
                                                SetWorkingStatus(ProgressText, MassStorageWarning);
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
                                                SetWorkingStatus(ProgressText, MassStorageWarning);
                                            }

                                            LastStatus = st;
                                        }
                                    });
                            }

                            if (PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_BadMassStorage)
                            {
                                throw new WPinternalsException("Phone is in Mass Storage mode, but the driver on PC failed to start");
                            }

                            // Wait for bootloader
                            if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                            {
                                LogFile.Log("Waiting for Mass Storage Mode (1)...", LogType.FileOnly);
                                await PhoneNotifier.WaitForArrival();
                            }

                            if (PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_BadMassStorage)
                            {
                                throw new WPinternalsException("Phone is in Mass Storage mode, but the driver on PC failed to start");
                            }

                            // Wait for mass storage mode
                            if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                            {
                                LogFile.Log("Waiting for Mass Storage Mode (2)...", LogType.FileOnly);
                                await PhoneNotifier.WaitForArrival();
                            }

                            if (PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_BadMassStorage)
                            {
                                throw new WPinternalsException("Phone is in Mass Storage mode, but the driver on PC failed to start");
                            }

                            MassStorage Storage = null;
                            if (PhoneNotifier.CurrentModel is MassStorage)
                            {
                                Storage = (MassStorage)PhoneNotifier.CurrentModel;
                            }

                            if (Storage == null)
                            {
                                ModeSwitchErrorWrapper("Failed to switch to Mass Storage Mode");
                            }
                            else
                            {
                                ModeSwitchSuccessWrapper();
                            }
                        }
                        catch (Exception Ex)
                        {
                            LogFile.LogException(Ex);
                            ModeSwitchErrorWrapper(Ex.Message);
                        }

                        LogFile.EndAction("SwitchToMassStorageMode");
                    }).Start();
                }
                else
                {
                    ModeSwitchErrorWrapper("Bootloader was not unlocked. First unlock bootloader before you try to switch to Mass Storage Mode.");
                }
            }).Start();
        }

        private void SwitchFromFlashToMassStorageMode(bool Continuation = false)
        {
            string ProgressText = Continuation ? "And now rebooting phone to Mass Storage mode..." : "Rebooting phone to Mass Storage mode...";
            if (CurrentMode == PhoneInterfaces.Lumia_Bootloader)
            {
                try
                {
                    ((LumiaBootManagerAppModel)PhoneNotifier.CurrentModel).SwitchToFlashAppContext();
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
            }
            LumiaFlashAppModel FlashModel = (LumiaFlashAppModel)PhoneNotifier.CurrentModel;
            LumiaFlashAppPhoneInfo Info = FlashModel.ReadPhoneInfo(ExtendedInfo: false);

            MassStorageWarning = null;
            if (Info.FlashAppProtocolVersionMajor < 2)
            {
                MassStorageWarning = "Switching to Mass Storage mode should take about 10 seconds. The phone should be unlocked using an Engineering SBL3 to enable Mass Storage mode. When you unlocked the bootloader, but you did not use an Engineering SBL3, an attempt to boot to Mass Storage mode may result in an unresponsive state. Installing drivers for this interface may also cause to hang the PC. So when this switch is taking too long, you should reboot both the PC and the phone. To reboot the phone, you have to perform a soft-reset. Press and hold the volume-down-button and the power-button at the same time for at least 10 seconds. This will trigger a power-cycle and the phone will reboot. Once fully booted, the phone may show strange behavior, like complaining about mail-accounts, showing old text-messages, inability to load https-websites, etc. This is expected behavior, because the time-settings of the phone are incorrect. Just wait a few seconds for the phone to get a data-connection and have the date/time synced. After that the strange behavior will stop automatically and normal operation is resumed.";
            }
            else
            {
                MassStorageWarning = "When the screen of the phone is black for a while, it could be that the phone is already in Mass Storage Mode, but there is no drive-letter assigned. To resolve this issue, open Device Manager and manually assign a drive-letter to the MainOS partition of your phone, or open a command-prompt and type: diskpart automount enable.";
                if (App.IsPnPEventLogMissing)
                {
                    MassStorageWarning += " It is also possible that the phone is in Mass Storage mode, but the Mass Storage driver on this PC failed. Your PC does not have an eventlog to detect this misbehaviour. But in this case you will see a device with an exclamation mark in Device Manager and then you need to manually reset the phone by pressing and holding the power-button for at least 10 seconds until it vibrates and reboots. After that Windows Phone Internals will revert the changes. After the phone has rebooted to the OS, you can retry to unlock the bootloader.";
                }
            }

            bool IsOldLumia = Info.FlashAppProtocolVersionMajor < 2;
            bool IsNewLumia = Info.FlashAppProtocolVersionMajor >= 2;
            bool IsUnlockedNew = false;
            if (IsNewLumia)
            {
                GPT GPT = FlashModel.ReadGPT();
                IsUnlockedNew = (GPT.GetPartition("IS_UNLOCKED") != null) || (GPT.GetPartition("BACKUP_EFIESP") != null) || (GPT.GetPartition("BACKUP_BS_NV") != null);
            }
            bool IsOriginalEngineeringLumia = !Info.IsBootloaderSecure && !IsUnlockedNew;

            if (IsOldLumia || IsOriginalEngineeringLumia)
            {
                byte[] BootModeFlagCommand = [0x4E, 0x4F, 0x4B, 0x58, 0x46, 0x57, 0x00, 0x55, 0x42, 0x46, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00]; // NOKFW UBF
                byte[] RebootCommand = [0x4E, 0x4F, 0x4B, 0x52];
                byte[] RebootToMassStorageCommand = [0x4E, 0x4F, 0x4B, 0x4D]; // NOKM
                IsSwitchingInterface = true;
                byte[] RebootCommandResult = ((NokiaPhoneModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(RebootToMassStorageCommand);
                if (RebootCommandResult?.Length == 4) // This means fail: NOKU (unknown command)
                {
                    BootModeFlagCommand[0x0F] = 0x4D;
                    byte[] BootFlagResult = ((NokiaPhoneModel)PhoneNotifier.CurrentModel).ExecuteRawMethod(BootModeFlagCommand);
                    UInt16 ResultCode = BitConverter.ToUInt16(BootFlagResult, 6);
                    if (ResultCode == 0)
                    {
                        PhoneNotifier.NewDeviceArrived += NewDeviceArrived;

                        ((NokiaPhoneModel)PhoneNotifier.CurrentModel).ExecuteRawVoidMethod(RebootCommand);
                        ModeSwitchProgressWrapper(ProgressText, MassStorageWarning);
                        LogFile.Log("Rebooting phone to Mass Storage mode");
                    }
                    else
                    {
                        ModeSwitchErrorWrapper("Failed to switch to Mass Storage mode");
                        IsSwitchingInterface = false;
                    }
                }
                else
                {
                    PhoneNotifier.NewDeviceArrived += NewDeviceArrived;

                    ModeSwitchProgressWrapper(ProgressText, MassStorageWarning);
                    LogFile.Log("Rebooting phone to Mass Storage mode");
                }
            }
            else if (IsUnlockedNew)
            {
                new Thread(async () =>
                {
                    LogFile.BeginAction("SwitchToMassStorageMode");

                    try
                    {
                        // Implementation of writing a partition with SecureBoot variable to the phone
                        ModeSwitchProgressWrapper(ProgressText, MassStorageWarning);
                        LogFile.Log("Preparing phone for Mass Storage Mode", LogType.FileAndConsole);
                        var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                        // Magic!
                        // The SBMSM resource is a compressed version of a raw NV-variable-partition.
                        // In this partition the SecureBoot variable is disabled and an extra variable is added which triggers Mass Storage Mode on next reboot.
                        // It overwrites the variable in a different NV-partition than where this variable is stored usually.
                        // This normally leads to endless-loops when the NV-variables are enumerated.
                        // But the partition contains an extra hack to break out the endless loops.
                        using (var stream = assembly.GetManifestResourceStream("WPinternals.SBMSM"))
                        {
                            using DecompressedStream dec = new(stream);
                            using MemoryStream SB = new(); // Must be a seekable stream!
                            dec.CopyTo(SB);

                            // We don't need to check for the BACKUP_BS_NV partition here,
                            // because the SecureBoot flag is disabled here.
                            // So either the NV was already backupped or already overwritten.

                            GPT GPT = FlashModel.ReadGPT();
                            Partition Target = GPT.GetPartition("UEFI_BS_NV");

                            // We've been reading the GPT, so we let the phone reset once more to be sure that memory maps are the same
                            WPinternalsStatus LastStatus = WPinternalsStatus.Undefined;
                            List<FlashPart> Parts = new();
                            FlashPart Part = new();
                            Part.StartSector = (uint)Target.FirstSector;
                            Part.Stream = SB;
                            Parts.Add(Part);
                            await LumiaV2UnlockBootViewModel.LumiaV2CustomFlash(PhoneNotifier, null, false, false, Parts, DoResetFirst: true, ClearFlashingStatusAtEnd: false, ShowProgress: false,
                                SetWorkingStatus: (m, s, v, a, st) =>
                                {
                                    if (SetWorkingStatus != null)
                                    {
                                        if ((st == WPinternalsStatus.Scanning) || (st == WPinternalsStatus.WaitingForManualReset))
                                        {
                                            SetWorkingStatus(m, s, v, a, st);
                                        }
                                        else if ((LastStatus == WPinternalsStatus.Scanning) || (LastStatus == WPinternalsStatus.WaitingForManualReset))
                                        {
                                            SetWorkingStatus(ProgressText, MassStorageWarning);
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
                                            SetWorkingStatus(ProgressText, MassStorageWarning);
                                        }

                                        LastStatus = st;
                                    }
                                });
                        }

                        if (PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_BadMassStorage)
                        {
                            throw new WPinternalsException("Phone is in Mass Storage mode, but the driver on PC failed to start");
                        }

                        // Wait for bootloader
                        if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                        {
                            LogFile.Log("Waiting for Mass Storage Mode (1)...", LogType.FileOnly);
                            await PhoneNotifier.WaitForArrival();
                        }

                        if (PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_BadMassStorage)
                        {
                            throw new WPinternalsException("Phone is in Mass Storage mode, but the driver on PC failed to start");
                        }

                        // Wait for mass storage mode
                        if (PhoneNotifier.CurrentInterface != PhoneInterfaces.Lumia_MassStorage)
                        {
                            LogFile.Log("Waiting for Mass Storage Mode (2)...", LogType.FileOnly);
                            await PhoneNotifier.WaitForArrival();
                        }

                        if (PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_BadMassStorage)
                        {
                            throw new WPinternalsException("Phone is in Mass Storage mode, but the driver on PC failed to start");
                        }

                        MassStorage Storage = null;
                        if (PhoneNotifier.CurrentModel is MassStorage)
                        {
                            Storage = (MassStorage)PhoneNotifier.CurrentModel;
                        }

                        if (Storage == null)
                        {
                            ModeSwitchErrorWrapper("Failed to switch to Mass Storage Mode");
                        }
                        else
                        {
                            ModeSwitchSuccessWrapper();
                        }
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                        ModeSwitchErrorWrapper(Ex.Message);
                    }

                    LogFile.EndAction("SwitchToMassStorageMode");
                }).Start();
            }
            else
            {
                ModeSwitchErrorWrapper("Bootloader was not unlocked. First unlock bootloader before you try to switch to Mass Storage Mode.");
            }
        }

        internal async static Task<IDisposable> SwitchTo(PhoneNotifierViewModel Notifier, PhoneInterfaces? TargetMode, string RequestedVolumeForMassStorage = "MainOS")
        {
            return await SwitchToWithProgress(Notifier, TargetMode, null, RequestedVolumeForMassStorage);
        }

        internal async static Task<IDisposable> SwitchToWithProgress(PhoneNotifierViewModel Notifier, PhoneInterfaces? TargetMode, ModeSwitchProgressHandler ModeSwitchProgress, string RequestedVolumeForMassStorage = "MainOS")
        {
            if (Notifier.CurrentInterface == TargetMode)
            {
                return Notifier.CurrentModel;
            }

            IDisposable Result = null;
            string LocalErrorMessage = null;

            AsyncAutoResetEvent Event = new(false);

            SwitchModeViewModel Switch = new(
                Notifier,
                TargetMode,
                ModeSwitchProgress,
                (string ErrorMessage) =>
                    {
                        LocalErrorMessage = ErrorMessage;
                        Event.Set();
                    },
                (IDisposable NewModel, PhoneInterfaces NewInterface) =>
                    {
                        Result = NewModel;
                        Event.Set();
                    }
                );

            await Event.WaitAsync(Timeout.InfiniteTimeSpan);

            if (LocalErrorMessage != null)
            {
                throw new WPinternalsException(LocalErrorMessage);
            }

            return Result;
        }

        internal async static Task<IDisposable> SwitchToWithStatus(PhoneNotifierViewModel Notifier, PhoneInterfaces? TargetMode, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, string RequestedVolumeForMassStorage = "MainOS")
        {
            if (Notifier.CurrentInterface == TargetMode)
            {
                return Notifier.CurrentModel;
            }

            IDisposable Result = null;
            string LocalErrorMessage = null;

            AsyncAutoResetEvent Event = new(false);

            SwitchModeViewModel Switch = new(
                Notifier,
                TargetMode,
                null,
                (string ErrorMessage) =>
                {
                    LocalErrorMessage = ErrorMessage;
                    Event.Set();
                },
                (IDisposable NewModel, PhoneInterfaces NewInterface) =>
                {
                    Result = NewModel;
                    Event.Set();
                }, SetWorkingStatus, UpdateWorkingStatus
            );

            await Event.WaitAsync(Timeout.InfiniteTimeSpan);

            if (LocalErrorMessage != null)
            {
                throw new WPinternalsException(LocalErrorMessage);
            }

            return Result;
        }
    }
}
