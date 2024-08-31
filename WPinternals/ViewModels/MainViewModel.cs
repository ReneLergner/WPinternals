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
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WPinternals
{
    public enum NavigationSubject
    {
        Info,
        Mode,
        Install,
        About
    };

    public enum PhoneInterfaces
    {
        Lumia_Normal,
        Lumia_Flash,
        Lumia_Label,
        Lumia_MassStorage,
        Lumia_Bootloader,
        Qualcomm_Download,
        Qualcomm_Flash,
        Lumia_BadMassStorage,
        Lumia_PhoneInfo
    };

    // Create this class on the UI thread, after the main-window of the application is initialized.
    // It is necessary to create the object on the UI thread, because notification events to the View need to be fired on that thread.
    // The Model for this ViewModel communicates over USB and for that it uses the hWnd of the main window.
    // Therefore the main window must be created before the ViewModel is created.
    internal class MainViewModel : INotifyPropertyChanged
    {
        public PhoneInterfaces? CurrentInterface = null;
        public PhoneInterfaces? LastInterface = null;
        public NokiaPhoneModel CurrentModel = null;
        public PhoneNotifierViewModel PhoneNotifier;
        public LumiaInfoViewModel InfoViewModel;
        public LumiaModeViewModel ModeViewModel;
        public LumiaUnlockBootViewModel BootUnlockViewModel;
        public LumiaUnlockBootViewModel BootRestoreViewModel;
        public LumiaUnlockRootViewModel RootUnlockViewModel;
        public LumiaUnlockRootViewModel RootRestoreViewModel;
        public BackupViewModel BackupViewModel;
        public RestoreViewModel RestoreViewModel;
        public LumiaFlashRomViewModel LumiaFlashRomViewModel;
        public DumpRomViewModel DumpRomViewModel;
        public DownloadsViewModel DownloadsViewModel;
        private GettingStartedViewModel _GettingStartedViewModel = null;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                if (MainSyncContext == SynchronizationContext.Current)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
                else
                {
                    MainSyncContext.Post(s => PropertyChanged(this, new PropertyChangedEventArgs(propertyName)), null);
                }
            }
        }

        private ContextViewModel _ContextViewModel;
        public ContextViewModel ContextViewModel
        {
            get
            {
                return _ContextViewModel;
            }
            set
            {
                if (_ContextViewModel != value)
                {
                    if (_ContextViewModel != null)
                    {
                        _ContextViewModel.IsActive = false;
                    }

                    _ContextViewModel = value;
                    _ContextViewModel?.Activate();
                    OnPropertyChanged(nameof(ContextViewModel));
                }
            }
        }

        private readonly SynchronizationContext MainSyncContext;

        public MainViewModel()
        {
            MainSyncContext = SynchronizationContext.Current;

            LogFile.LogApplicationVersion();

            // Set global callback for cases where Dependency Injection is not possible.
            App.NavigateToGettingStarted = () => GettingStartedCommand.Execute(null);
            App.NavigateToUnlockBoot = () => BootUnlockCommand.Execute(null);

            if (Registry.CurrentUser.OpenSubKey("Software\\WPInternals") == null)
            {
                Registry.CurrentUser.OpenSubKey("Software", true).CreateSubKey("WPInternals");
            }

            if (Registration.IsPrerelease && (Registry.CurrentUser.OpenSubKey("Software\\WPInternals").GetValue("NdaAccepted") == null))
            {
                this.ContextViewModel = new DisclaimerAndNdaViewModel(Disclaimer_Accepted);
            }
            else if (Registry.CurrentUser.OpenSubKey("Software\\WPInternals").GetValue("DisclaimerAccepted") == null)
            {
                this.ContextViewModel = new DisclaimerViewModel(Disclaimer_Accepted);
            }
            else if (Registration.IsPrerelease && !Registration.IsRegistered())
            {
                ContextViewModel = new RegistrationViewModel(Registration_Completed, Registration_Failed);
            }
            else
            {
                StartOperation();
            }
        }

        private void Disclaimer_Accepted()
        {
            ContextViewModel = null;

            if (Registration.IsPrerelease && !Registration.IsRegistered())
            {
                ContextViewModel = new RegistrationViewModel(Registration_Completed, Registration_Failed);
            }
            else
            {
                StartOperation();
            }
        }

        private void Registration_Completed()
        {
            ContextViewModel = null;
            StartOperation();
        }

        private void Registration_Failed()
        {
            ContextViewModel = new MessageViewModel("Registration failed", () => Environment.Exit(0));
            ((MessageViewModel)ContextViewModel).SubMessage = "Check your filewall settings";
        }

        public void StartOperation()
        {
            IsMenuEnabled = true;

            _GettingStartedViewModel = new GettingStartedViewModel(
                () =>
                {
                    ContextViewModel = new DisclaimerViewModel(
                        () => ContextViewModel = _GettingStartedViewModel
                        );
                },
                SwitchToUnlockBoot,
                SwitchToUnlockRoot,
                SwitchToBackup,
                SwitchToDumpFFU,
                SwitchToFlashRom,
                SwitchToDownload
            );
            this.ContextViewModel = _GettingStartedViewModel;

            PhoneNotifier = new PhoneNotifierViewModel();
            PhoneNotifier.NewDeviceArrived += PhoneNotifier_NewDeviceArrived;
            PhoneNotifier.DeviceRemoved += PhoneNotifier_DeviceRemoved;

            InfoViewModel = new LumiaInfoViewModel(PhoneNotifier, (TargetInterface) =>
            {
                ModeViewModel.OnModeSwitchRequested(TargetInterface);
                ContextViewModel = ModeViewModel;
            },
            () => ContextViewModel = _GettingStartedViewModel);
            InfoViewModel.ActivateSubContext(null);

            ModeViewModel = new LumiaModeViewModel(PhoneNotifier, SwitchToInfoViewModel);
            ModeViewModel.ActivateSubContext(null);

            BootUnlockViewModel = new LumiaUnlockBootViewModel(PhoneNotifier, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, true, SwitchToInfoViewModel);
            BootUnlockViewModel.ActivateSubContext(null);

            BootRestoreViewModel = new LumiaUnlockBootViewModel(PhoneNotifier, SwitchToFlashRom, SwitchToUndoRoot, SwitchToDownload, false, SwitchToInfoViewModel);
            BootRestoreViewModel.ActivateSubContext(null);

            RootUnlockViewModel = new LumiaUnlockRootViewModel(PhoneNotifier, SwitchToUnlockBoot, SwitchToDumpFFU, SwitchToFlashRom, true, SwitchToInfoViewModel);
            RootUnlockViewModel.ActivateSubContext(null);

            RootRestoreViewModel = new LumiaUnlockRootViewModel(PhoneNotifier, SwitchToUnlockBoot, SwitchToDumpFFU, SwitchToFlashRom, false, SwitchToInfoViewModel);
            RootRestoreViewModel.ActivateSubContext(null);

            BackupViewModel = new BackupViewModel(PhoneNotifier, SwitchToUnlockBoot, SwitchToInfoViewModel);
            BackupViewModel.ActivateSubContext(null);

            RestoreViewModel = new RestoreViewModel(PhoneNotifier, SwitchToDifferentInterface, SwitchToUnlockBoot, SwitchToFlashRom, SwitchToInfoViewModel);
            RestoreViewModel.ActivateSubContext(null);

            LumiaFlashRomViewModel = new LumiaFlashRomViewModel(PhoneNotifier, SwitchToUnlockBoot, SwitchToUnlockRoot, SwitchToDumpFFU, SwitchToBackup, SwitchToInfoViewModel);
            LumiaFlashRomViewModel.ActivateSubContext(null);

            DumpRomViewModel = new DumpRomViewModel(SwitchToUnlockBoot, SwitchToUnlockRoot, SwitchToFlashRom);
            DumpRomViewModel.ActivateSubContext(null);

            DownloadsViewModel = new DownloadsViewModel(PhoneNotifier);
            App.DownloadManager = DownloadsViewModel;

            PhoneNotifier.Start();
        }

        internal void SwitchToInfoViewModel()
        {
            ContextViewModel = InfoViewModel;
        }

        internal void SwitchToUnlockBoot()
        {
            ContextViewModel = BootUnlockViewModel;
        }

        internal void SwitchToRestoreBoot()
        {
            ContextViewModel = BootRestoreViewModel;
        }

        internal void SwitchToUnlockRoot()
        {
            ContextViewModel = RootUnlockViewModel;
        }

        internal void SwitchToUndoRoot()
        {
            ContextViewModel = RootRestoreViewModel;
        }

        internal void SwitchToBackup()
        {
            ContextViewModel = BackupViewModel;
        }

        internal void SwitchToFlashRom()
        {
            ContextViewModel = LumiaFlashRomViewModel;
        }

        internal void SwitchToDumpFFU()
        {
            ContextViewModel = DumpRomViewModel;
        }

        internal void SwitchToDownload()
        {
            ContextViewModel = DownloadsViewModel;
        }

        internal void SwitchToDifferentInterface(PhoneInterfaces TargetInterface)
        {
            ModeViewModel.OnModeSwitchRequested(TargetInterface);
            ContextViewModel = ModeViewModel;
        }

        private void PhoneNotifier_DeviceRemoved()
        {
            InfoViewModel.ActivateSubContext(null);
        }

        private void PhoneNotifier_NewDeviceArrived(ArrivalEventArgs Args)
        {
            PhoneInterfaces? PreviousInterface = LastInterface;
            LastInterface = Args.NewInterface;

            if (App.InterruptBoot && (Args.NewInterface == PhoneInterfaces.Lumia_Bootloader))
            {
                App.InterruptBoot = false;
                LogFile.Log("Found Lumia BootMgr and user forced to interrupt the boot process. Force to Flash-mode.");
                Task.Run(() => SwitchModeViewModel.SwitchTo(PhoneNotifier, PhoneInterfaces.Lumia_Flash));
            }
            else
            {
                if (Args.NewInterface != PhoneInterfaces.Qualcomm_Download)
                {
                    App.InterruptBoot = false;
                }

                if (ContextViewModel == null)
                {
                    ContextViewModel = InfoViewModel;
                }
                else if (ContextViewModel.IsFlashModeOperation)
                {
                    if ((!ContextViewModel.IsSwitchingInterface) && (Args.NewInterface == PhoneInterfaces.Lumia_Bootloader))
                    {
                        // The current screen is marked as "Flash operation".
                        // When the bootloader is detected at this stage, it means a phone is booting and
                        // it is possible that the phone is in a non-booting stage (not possible to boot past UEFI).
                        // We will try to boot straight to Flash-mode, so that it will be possible to flash a new ROM.
                        LogFile.Log("Found Lumia BootMgr while mode is not being switched. Screen is marked as Flash Operation. Force to Flash-mode.");
                        Task.Run(() => SwitchModeViewModel.SwitchTo(PhoneNotifier, PhoneInterfaces.Lumia_Flash));
                    }
                }
                else
                {
                    if ((!ContextViewModel.IsSwitchingInterface) && (Args.NewInterface != PhoneInterfaces.Lumia_Bootloader))
                    {
                        ContextViewModel = InfoViewModel;
                    }
                }
            }
        }

        private ICommand _InfoCommand = null;
        public ICommand InfoCommand
        {
            get
            {
                return _InfoCommand ??= new DelegateCommand(() => ContextViewModel = InfoViewModel);
            }
        }

        private ICommand _ModeCommand = null;
        public ICommand ModeCommand
        {
            get
            {
                return _ModeCommand ??= new DelegateCommand(() => ContextViewModel = ModeViewModel);
            }
        }

        private ICommand _BootUnlockCommand = null;
        public ICommand BootUnlockCommand
        {
            get
            {
                return _BootUnlockCommand ??= new DelegateCommand(() => ContextViewModel = BootUnlockViewModel);
            }
        }

        private ICommand _BootRestoreCommand = null;
        public ICommand BootRestoreCommand
        {
            get
            {
                return _BootRestoreCommand ??= new DelegateCommand(() => ContextViewModel = BootRestoreViewModel);
            }
        }

        private ICommand _RootUnlockCommand = null;
        public ICommand RootUnlockCommand
        {
            get
            {
                return _RootUnlockCommand ??= new DelegateCommand(() => ContextViewModel = RootUnlockViewModel);
            }
        }

        private ICommand _RootUndoCommand = null;
        public ICommand RootUndoCommand
        {
            get
            {
                return _RootUndoCommand ??= new DelegateCommand(() => ContextViewModel = RootRestoreViewModel);
            }
        }

        private ICommand _BackupCommand = null;
        public ICommand BackupCommand
        {
            get
            {
                return _BackupCommand ??= new DelegateCommand(() => ContextViewModel = BackupViewModel);
            }
        }

        private ICommand _RestoreCommand = null;
        public ICommand RestoreCommand
        {
            get
            {
                return _RestoreCommand ??= new DelegateCommand(() => ContextViewModel = RestoreViewModel);
            }
        }

        private ICommand _LumiaFlashRomCommand = null;
        public ICommand LumiaFlashRomCommand
        {
            get
            {
                return _LumiaFlashRomCommand ??= new DelegateCommand(() => ContextViewModel = LumiaFlashRomViewModel);
            }
        }

        private ICommand _DumpRomCommand = null;
        public ICommand DumpRomCommand
        {
            get
            {
                return _DumpRomCommand ??= new DelegateCommand(() => ContextViewModel = DumpRomViewModel);
            }
        }

        private ICommand _AboutCommand = null;
        public ICommand AboutCommand
        {
            get
            {
                return _AboutCommand ??= new DelegateCommand(() => ContextViewModel = new AboutViewModel());
            }
        }

        private ICommand _DonateCommand = null;
        public ICommand DonateCommand
        {
            get
            {
                return _DonateCommand ??= new DelegateCommand(() =>
                {
                    Process process = new();
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.FileName = "https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=VY8N7BCBT9CS4";
                    process.Start();
                });
            }
        }

        private ICommand _GettingStartedCommand = null;
        public ICommand GettingStartedCommand
        {
            get
            {
                return _GettingStartedCommand ??= new DelegateCommand(() => ContextViewModel = _GettingStartedViewModel);
            }
        }

        private ICommand _DownloadCommand = null;
        public ICommand DownloadCommand
        {
            get
            {
                return _DownloadCommand ??= new DelegateCommand(() => ContextViewModel = DownloadsViewModel);
            }
        }

        private bool _IsMenuEnabled = false;
        public bool IsMenuEnabled
        {
            get
            {
                return _IsMenuEnabled;
            }
            set
            {
                _IsMenuEnabled = value;
                OnPropertyChanged(nameof(IsMenuEnabled));
            }
        }
    }
}
