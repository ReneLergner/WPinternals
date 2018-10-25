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
using System.Threading;

namespace WPinternals
{
    internal class BackupTargetSelectionViewModel: ContextViewModel
    {
        private PhoneNotifierViewModel PhoneNotifier;
        private Action<string, string, string> BackupCallback;
        private Action<string> BackupArchiveCallback;
        internal Action SwitchToUnlockBoot;

        internal BackupTargetSelectionViewModel(PhoneNotifierViewModel PhoneNotifier, Action SwitchToUnlockBoot, Action<string> BackupArchiveCallback, Action<string, string, string> BackupCallback)
            : base()
        {
            this.PhoneNotifier = PhoneNotifier;
            this.BackupCallback = BackupCallback;
            this.BackupArchiveCallback = BackupArchiveCallback;
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;

            this.PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
            this.PhoneNotifier.DeviceRemoved += DeviceRemoved;

            new Thread(() => EvaluateViewState()).Start();
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
                    OnPropertyChanged("ArchivePath");
                }
            }
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
                    OnPropertyChanged("EFIESPPath");
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
                    OnPropertyChanged("MainOSPath");
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
                    OnPropertyChanged("DataPath");
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
                    OnPropertyChanged("IsPhoneDisconnected");
                }
            }
        }

        private bool _IsPhoneInMassStorage;
        public bool IsPhoneInMassStorage
        {
            get
            {
                return _IsPhoneInMassStorage;
            }
            set
            {
                if (value != _IsPhoneInMassStorage)
                {
                    _IsPhoneInMassStorage = value;
                    OnPropertyChanged("IsPhoneInMassStorage");
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
                    OnPropertyChanged("IsPhoneInOtherMode");
                }
            }
        }

        private DelegateCommand _BackupArchiveCommand;
        public DelegateCommand BackupArchiveCommand
        {
            get
            {
                if (_BackupArchiveCommand == null)
                {
                    _BackupArchiveCommand = new DelegateCommand(() => { BackupArchiveCallback(ArchivePath); }, () => ((ArchivePath != null) && (PhoneNotifier.CurrentInterface != null)));
                }
                return _BackupArchiveCommand;
            }
        }

        private DelegateCommand _BackupCommand;
        public DelegateCommand BackupCommand
        {
            get
            {
                if (_BackupCommand == null)
                {
                    _BackupCommand = new DelegateCommand(() => { BackupCallback(EFIESPPath, MainOSPath, DataPath); }, () => (((EFIESPPath != null) || (MainOSPath != null) || (DataPath != null)) && (PhoneNotifier.CurrentInterface != null)));
                }
                return _BackupCommand;
            }
        }

        ~BackupTargetSelectionViewModel()
        {
            PhoneNotifier.NewDeviceArrived -= NewDeviceArrived;
        }

        void NewDeviceArrived(ArrivalEventArgs Args)
        {
            new Thread(() => EvaluateViewState()).Start();
        }

        void DeviceRemoved()
        {
            new Thread(() => EvaluateViewState()).Start();
        }

        internal override void EvaluateViewState()
        {
            IsPhoneDisconnected = PhoneNotifier.CurrentInterface == null;
            IsPhoneInMassStorage = PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_MassStorage;
            IsPhoneInOtherMode = (!IsPhoneDisconnected && !IsPhoneInMassStorage);
            BackupCommand.RaiseCanExecuteChanged();
            BackupArchiveCommand.RaiseCanExecuteChanged();
        }
    }
}
