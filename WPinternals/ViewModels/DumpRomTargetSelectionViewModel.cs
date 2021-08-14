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
    internal class DumpRomTargetSelectionViewModel : ContextViewModel
    {
        private readonly Action<string, string, bool, string, bool, string, bool> DumpCallback;
        internal Action SwitchToUnlockBoot;
        internal Action SwitchToUnlockRoot;
        internal Action SwitchToFlashRom;

        internal DumpRomTargetSelectionViewModel(Action SwitchToUnlockBoot, Action SwitchToUnlockRoot, Action SwitchToFlashRom, Action<string, string, bool, string, bool, string, bool> DumpCallback)
            : base()
        {
            this.SwitchToUnlockBoot = SwitchToUnlockBoot;
            this.SwitchToUnlockRoot = SwitchToUnlockRoot;
            this.SwitchToFlashRom = SwitchToFlashRom;
            this.DumpCallback = DumpCallback;

            new Thread(() => EvaluateViewState()).Start();
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

        private bool _CompressEFIESP;
        public bool CompressEFIESP
        {
            get
            {
                return _CompressEFIESP;
            }
            set
            {
                if (value != _CompressEFIESP)
                {
                    _CompressEFIESP = value;
                    OnPropertyChanged(nameof(CompressEFIESP));
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

        private bool _CompressMainOS;
        public bool CompressMainOS
        {
            get
            {
                return _CompressMainOS;
            }
            set
            {
                if (value != _CompressMainOS)
                {
                    _CompressMainOS = value;
                    OnPropertyChanged(nameof(CompressMainOS));
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

        private bool _CompressData = true;
        public bool CompressData
        {
            get
            {
                return _CompressData;
            }
            set
            {
                if (value != _CompressData)
                {
                    _CompressData = value;
                    OnPropertyChanged(nameof(CompressData));
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

        private DelegateCommand _DumpCommand;
        public DelegateCommand DumpCommand
        {
            get
            {
                return _DumpCommand ??= new DelegateCommand(() => DumpCallback(FFUPath, EFIESPPath, CompressEFIESP, MainOSPath, CompressMainOS, DataPath, CompressData), () => (FFUPath != null) && ((EFIESPPath != null) || (MainOSPath != null) || (DataPath != null)));
            }
        }

        internal override void EvaluateViewState()
        {
            DumpCommand.RaiseCanExecuteChanged();
        }
    }
}
