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
using System.Threading;

namespace WPinternals
{
    internal class LumiaFlashRomSourceSelectionViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel PhoneNotifier;
        private readonly Action<string, string, string> FlashPartitionsCallback;
        private readonly Action<string> FlashFFUCallback;
        private readonly Action<string> FlashMMOSCallback;
        private readonly Action<string> FlashArchiveCallback;
        internal Action SwitchToUnlockBoot;
        internal Action SwitchToUnlockRoot;
        internal Action SwitchToDumpFFU;
        internal Action SwitchToBackup;

        internal LumiaFlashRomSourceSelectionViewModel(PhoneNotifierViewModel PhoneNotifier, Action SwitchToUnlockBoot, Action SwitchToUnlockRoot, Action SwitchToDumpFFU, Action SwitchToBackup, Action<string, string, string> FlashPartitionsCallback, Action<string> FlashArchiveCallback, Action<string> FlashFFUCallback, Action<string> FlashMMOSCallback)
            : base()
        {
            this.PhoneNotifier = PhoneNotifier;
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;
            this.SwitchToUnlockRoot = SwitchToUnlockRoot;
            this.SwitchToDumpFFU = SwitchToDumpFFU;
            this.SwitchToBackup = SwitchToBackup;
            this.FlashPartitionsCallback = FlashPartitionsCallback;
            this.FlashArchiveCallback = FlashArchiveCallback;
            this.FlashFFUCallback = FlashFFUCallback;
            this.FlashMMOSCallback = FlashMMOSCallback;

            this.PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
            this.PhoneNotifier.DeviceRemoved += DeviceRemoved;

            new Thread(() => EvaluateViewState()).Start();
        }

        private string _EFIESPPath;
        public string EFIESPPath
        {
            get
            {
                return _EFIESPPath;
            }
            set
            {
                if (value != _EFIESPPath)
                {
                    _EFIESPPath = value;
                    OnPropertyChanged(nameof(EFIESPPath));
                }
            }
        }

        private string _MainOSPath;
        public string MainOSPath
        {
            get
            {
                return _MainOSPath;
            }
            set
            {
                if (value != _MainOSPath)
                {
                    _MainOSPath = value;
                    OnPropertyChanged(nameof(MainOSPath));
                }
            }
        }

        private string _DataPath;
        public string DataPath
        {
            get
            {
                return _DataPath;
            }
            set
            {
                if (value != _DataPath)
                {
                    _DataPath = value;
                    OnPropertyChanged(nameof(DataPath));
                }
            }
        }

        private string _ArchivePath;
        public string ArchivePath
        {
            get
            {
                return _ArchivePath;
            }
            set
            {
                if (value != _ArchivePath)
                {
                    _ArchivePath = value;
                    OnPropertyChanged(nameof(ArchivePath));
                }
            }
        }

        private string _FFUPath;
        public string FFUPath
        {
            get
            {
                return _FFUPath;
            }
            set
            {
                if (value != _FFUPath)
                {
                    _FFUPath = value;
                    OnPropertyChanged(nameof(FFUPath));
                }
            }
        }

        private string _MMOSPath;
        public string MMOSPath
        {
            get
            {
                return _MMOSPath;
            }
            set
            {
                if (value != _MMOSPath)
                {
                    _MMOSPath = value;
                    OnPropertyChanged(nameof(MMOSPath));
                }
            }
        }

        private bool _IsPhoneDisconnected;
        public bool IsPhoneDisconnected
        {
            get
            {
                return _IsPhoneDisconnected;
            }
            set
            {
                if (value != _IsPhoneDisconnected)
                {
                    _IsPhoneDisconnected = value;
                    OnPropertyChanged(nameof(IsPhoneDisconnected));
                }
            }
        }

        private bool _IsPhoneInFlashMode;
        public bool IsPhoneInFlashMode
        {
            get
            {
                return _IsPhoneInFlashMode;
            }
            set
            {
                if (value != _IsPhoneInFlashMode)
                {
                    _IsPhoneInFlashMode = value;
                    OnPropertyChanged(nameof(IsPhoneInFlashMode));
                }
            }
        }

        private bool _IsPhoneInOtherMode;
        public bool IsPhoneInOtherMode
        {
            get
            {
                return _IsPhoneInOtherMode;
            }
            set
            {
                if (value != _IsPhoneInOtherMode)
                {
                    _IsPhoneInOtherMode = value;
                    OnPropertyChanged(nameof(IsPhoneInOtherMode));
                }
            }
        }

        private DelegateCommand _FlashPartitionsCommand;
        public DelegateCommand FlashPartitionsCommand
        {
            get
            {
                return _FlashPartitionsCommand ??= new DelegateCommand(() => FlashPartitionsCallback(EFIESPPath, MainOSPath, DataPath), () => ((EFIESPPath != null) || (MainOSPath != null) || (DataPath != null)) && (PhoneNotifier.CurrentInterface != null));
            }
        }

        private DelegateCommand _FlashFFUCommand;
        public DelegateCommand FlashFFUCommand
        {
            get
            {
                return _FlashFFUCommand ??= new DelegateCommand(() => FlashFFUCallback(FFUPath), () => (FFUPath != null) && (PhoneNotifier.CurrentInterface != null));
            }
        }

        private DelegateCommand _FlashMMOSCommand;
        public DelegateCommand FlashMMOSCommand
        {
            get
            {
                return _FlashMMOSCommand ??= new DelegateCommand(() => FlashMMOSCallback(MMOSPath), () => (MMOSPath != null) && (PhoneNotifier.CurrentInterface != null));
            }
        }

        private DelegateCommand _FlashArchiveCommand;
        public DelegateCommand FlashArchiveCommand
        {
            get
            {
                return _FlashArchiveCommand ??= new DelegateCommand(() => FlashArchiveCallback(ArchivePath), () => (ArchivePath != null) && (PhoneNotifier.CurrentInterface != null));
            }
        }

        ~LumiaFlashRomSourceSelectionViewModel()
        {
            PhoneNotifier.NewDeviceArrived -= NewDeviceArrived;
        }

        private void NewDeviceArrived(ArrivalEventArgs Args)
        {
            new Thread(() =>
                {
                    try
                    {
                        EvaluateViewState();
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }
                }).Start();
        }

        private void DeviceRemoved()
        {
            new Thread(() => EvaluateViewState()).Start();
        }

        internal override void EvaluateViewState()
        {
            IsPhoneDisconnected = PhoneNotifier.CurrentInterface == null;
            IsPhoneInFlashMode = PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_Flash;
            IsPhoneInOtherMode = !IsPhoneDisconnected && !IsPhoneInFlashMode;
            FlashPartitionsCommand.RaiseCanExecuteChanged();
            FlashArchiveCommand.RaiseCanExecuteChanged();
            FlashFFUCommand.RaiseCanExecuteChanged();
            FlashMMOSCommand.RaiseCanExecuteChanged();
        }
    }
}
