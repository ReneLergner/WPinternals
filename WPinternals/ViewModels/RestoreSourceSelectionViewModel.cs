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
    internal class RestoreSourceSelectionViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel PhoneNotifier;
        private readonly Action<string, string, string> RestoreCallback;
        private readonly Action<PhoneInterfaces> RequestModeSwitch;
        internal Action SwitchToUnlockBoot;
        internal Action SwitchToFlashRom;

        internal RestoreSourceSelectionViewModel(PhoneNotifierViewModel PhoneNotifier, Action<PhoneInterfaces> RequestModeSwitch, Action SwitchToUnlockBoot, Action SwitchToFlashRom, Action<string, string, string> RestoreCallback)
            : base()
        {
            this.PhoneNotifier = PhoneNotifier;
            this.RestoreCallback = RestoreCallback;
            this.RequestModeSwitch = RequestModeSwitch;
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;
            this.SwitchToFlashRom = SwitchToFlashRom;

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

        private DelegateCommand _RestoreCommand;
        public DelegateCommand RestoreCommand
        {
            get
            {
                return _RestoreCommand ??= new DelegateCommand(() => RestoreCallback(EFIESPPath, MainOSPath, DataPath), () => ((EFIESPPath != null) || (MainOSPath != null) || (DataPath != null)) && (PhoneNotifier.CurrentInterface != null));
            }
        }

        ~RestoreSourceSelectionViewModel()
        {
            PhoneNotifier.NewDeviceArrived -= NewDeviceArrived;
        }

        private void NewDeviceArrived(ArrivalEventArgs Args)
        {
            new Thread(() => EvaluateViewState()).Start();
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
            RestoreCommand.RaiseCanExecuteChanged();
        }

        internal void RebootTo(string Mode)
        {
            switch (Mode)
            {
                case "Normal":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Normal);
                    break;
                case "Flash":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Flash);
                    break;
                case "Label":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Label);
                    break;
                case "MassStorage":
                    RequestModeSwitch(PhoneInterfaces.Lumia_MassStorage);
                    break;
                default:
                    return;
            }
        }
    }
}
