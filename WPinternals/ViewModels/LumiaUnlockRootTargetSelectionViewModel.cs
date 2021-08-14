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
    internal class LumiaUnlockRootTargetSelectionViewModel : LumiaRootAccessTargetSelectionViewModel
    {
        public LumiaUnlockRootTargetSelectionViewModel(PhoneNotifierViewModel PhoneNotifier, Action SwitchToUnlockBoot, Action SwitchToDumpRom, Action SwitchToFlashRom, Action UnlockPhoneCallback, Action<string, string> UnlockImageCallback)
            : base(PhoneNotifier, SwitchToUnlockBoot, SwitchToDumpRom, SwitchToFlashRom, UnlockPhoneCallback, UnlockImageCallback) { }
    }

    internal class LumiaUndoRootTargetSelectionViewModel : LumiaRootAccessTargetSelectionViewModel
    {
        public LumiaUndoRootTargetSelectionViewModel(PhoneNotifierViewModel PhoneNotifier, Action SwitchToUnlockBoot, Action SwitchToDumpRom, Action SwitchToFlashRom, Action UnlockPhoneCallback, Action<string, string> UnlockImageCallback)
            : base(PhoneNotifier, SwitchToUnlockBoot, SwitchToDumpRom, SwitchToFlashRom, UnlockPhoneCallback, UnlockImageCallback) { }
    }

    internal class LumiaRootAccessTargetSelectionViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel PhoneNotifier;
        internal Action SwitchToUnlockBoot;
        internal Action SwitchToDumpRom;
        internal Action SwitchToFlashRom;
        private readonly Action UnlockPhoneCallback;
        private readonly Action<string, string> UnlockImageCallback;

        internal LumiaRootAccessTargetSelectionViewModel(PhoneNotifierViewModel PhoneNotifier, Action SwitchToUnlockBoot, Action SwitchToDumpRom, Action SwitchToFlashRom, Action UnlockPhoneCallback, Action<string, string> UnlockImageCallback)
            : base()
        {
            this.PhoneNotifier = PhoneNotifier;
            this.SwitchToDumpRom = SwitchToDumpRom;
            this.SwitchToFlashRom = SwitchToFlashRom;
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;
            this.UnlockPhoneCallback = UnlockPhoneCallback;
            this.UnlockImageCallback = UnlockImageCallback;

            this.PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
            this.PhoneNotifier.DeviceRemoved += DeviceRemoved;

            new Thread(() => EvaluateViewState()).Start();
        }

        private string _EFIESPMountPoint;
        public string EFIESPMountPoint
        {
            get
            {
                return _EFIESPMountPoint;
            }
            set
            {
                if (value != _EFIESPMountPoint)
                {
                    _EFIESPMountPoint = value;
                    OnPropertyChanged(nameof(EFIESPMountPoint));
                }
            }
        }

        private string _MainOSMountPoint;
        public string MainOSMountPoint
        {
            get
            {
                return _MainOSMountPoint;
            }
            set
            {
                if (value != _MainOSMountPoint)
                {
                    _MainOSMountPoint = value;
                    OnPropertyChanged(nameof(MainOSMountPoint));
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
                    OnPropertyChanged(nameof(IsPhoneInMassStorage));
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

        private DelegateCommand _UnlockPhoneCommand;
        public DelegateCommand UnlockPhoneCommand
        {
            get
            {
                return _UnlockPhoneCommand ??= new DelegateCommand(() => UnlockPhoneCallback(), () => !IsPhoneDisconnected);
            }
        }

        private DelegateCommand _UnlockImageCommand;
        public DelegateCommand UnlockImageCommand
        {
            get
            {
                return _UnlockImageCommand ??= new DelegateCommand(() => UnlockImageCallback(EFIESPMountPoint, MainOSMountPoint), () => (EFIESPMountPoint != null) || (MainOSMountPoint != null));
            }
        }

        ~LumiaRootAccessTargetSelectionViewModel()
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
            IsPhoneInMassStorage = PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_MassStorage;
            IsPhoneInOtherMode = !IsPhoneDisconnected && !IsPhoneInMassStorage;
            UnlockPhoneCommand.RaiseCanExecuteChanged();
            UnlockImageCommand.RaiseCanExecuteChanged();
        }
    }
}
