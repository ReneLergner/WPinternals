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
using UnifiedFlashingPlatform;
using WPinternals.HelperClasses;

namespace WPinternals
{
    // Create this class on the UI thread, after the main-window of the application is initialized.
    // It is necessary to create the object on the UI thread, because notification events to the View need to be fired on that thread.
    // The Model for this ViewModel communicates over USB and for that it uses the hWnd of the main window.
    // Therefore the main window must be created before the ViewModel is created.

    internal class UFPViewModel : ContextViewModel
    {
        private readonly UnifiedFlashingPlatformModel CurrentModel;
        private readonly Action<PhoneInterfaces> RequestModeSwitch;
        private readonly object LockDeviceInfo = new();
        private bool DeviceInfoLoaded = false;

        internal UFPViewModel(UnifiedFlashingPlatformModel CurrentModel, Action<PhoneInterfaces> RequestModeSwitch)
            : base()
        {
            this.RequestModeSwitch = RequestModeSwitch;

            this.CurrentModel = CurrentModel;

            new Thread(() => StartLoadDeviceInfo()).Start();
        }

        private void StartLoadDeviceInfo()
        {
            lock (LockDeviceInfo)
            {
                if (!DeviceInfoLoaded)
                {
                    try
                    {
                        PlatformName = CurrentModel.ReadStringParam("DPI");
                        LogFile.Log("Platform Name: " + PlatformName);

                        // Some phones do not support the Terminal interface! (928 verizon)
                        // Instead read param RRKH to get the RKH.
                        PublicID = null;
                        byte[] RawPublicID = CurrentModel.ReadParam("PID");
                        if (RawPublicID?.Length > 4)
                        {
                            PublicID = new byte[RawPublicID.Length - 4];
                            Array.Copy(RawPublicID, 4, PublicID, 0, RawPublicID.Length - 4);
                            LogFile.Log("Public ID: " + Converter.ConvertHexToString(PublicID, " "));
                        }
                        else
                        {
                            PublicID = new byte[20];
                            LogFile.Log("Public ID: " + Converter.ConvertHexToString(PublicID, " "));
                        }
                        RootKeyHash = CurrentModel.ReadParam("RRKH");
                        if (RootKeyHash != null)
                        {
                            LogFile.Log("Root Key Hash: " + Converter.ConvertHexToString(RootKeyHash, " "));
                        }

                        byte[] EMS = CurrentModel.ReadParam("EMS");
                        if (EMS != null)
                        {
                            UInt64 MemSize = (UInt64)(((UInt32)EMS[0] << 24) + ((UInt32)EMS[1] << 16) + ((UInt32)EMS[2] << 8) + EMS[3]) * 0x200;
                            double MemSizeDouble = (double)MemSize / 1024 / 1024 / 1024;
                            MemSizeDouble = (double)(int)(MemSizeDouble * 10) / 10;
                            string Manufacturer = null;

                            eMMC = Manufacturer == null ? MemSizeDouble.ToString() + " GB" : Manufacturer + " " + MemSizeDouble.ToString() + " GB";
                        }
                        else
                        {
                            eMMC = "Unknown";
                            SamsungWarningVisible = true;
                        }

                        UnifiedFlashingPlatformModel.PhoneInfo Info = CurrentModel.ReadPhoneInfo();
                        BootloaderDescription = Info.FlashAppProtocolVersionMajor < 2 ? "Lumia Bootloader Spec A" : "Lumia Bootloader Spec B";

                        LogFile.Log("Bootloader: " + BootloaderDescription);

                        ProductCode = "";//TODO: FIXME: Info.ProductCode;
                        LogFile.Log("ProductCode: " + ProductCode);

                        ProductType = "";//TODO: FIXME: Info.Type;
                        LogFile.Log("ProductType: " + ProductType);

                        if (PlatformName == null)
                        {
                            LogFile.Log("Platform Name was null. Gathering information from an alternative source.");

                            PlatformName = Info.PlatformID;
                            LogFile.Log("Platform Name: " + PlatformName);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                        LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                        LogFile.Log(ex.Message, LogType.FileAndConsole);
                        LogFile.Log(ex.StackTrace, LogType.FileAndConsole);

                        LogFile.Log("Reading status from Flash interface was aborted.");
                    }
                    DeviceInfoLoaded = true;
                }
            }
        }

        private byte[] _PublicID = null;
        public byte[] PublicID
        {
            get
            {
                return _PublicID;
            }
            set
            {
                _PublicID = value;
                OnPropertyChanged(nameof(PublicID));
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

        private string _PlatformName = null;
        public string PlatformName
        {
            get
            {
                return _PlatformName;
            }
            set
            {
                _PlatformName = value;
                OnPropertyChanged(nameof(PlatformName));
            }
        }

        private string _ProductType = null;
        public string ProductType
        {
            get
            {
                return _ProductType;
            }
            set
            {
                _ProductType = value;
                OnPropertyChanged(nameof(ProductType));
            }
        }

        private string _ProductCode = null;
        public string ProductCode
        {
            get
            {
                return _ProductCode;
            }
            set
            {
                _ProductCode = value;
                OnPropertyChanged(nameof(ProductCode));
            }
        }

        private string _eMMC = null;
        public string eMMC
        {
            get
            {
                return _eMMC;
            }
            set
            {
                _eMMC = value;
                OnPropertyChanged(nameof(eMMC));
            }
        }

        private string _BootloaderDescription = null;
        public string BootloaderDescription
        {
            get
            {
                return _BootloaderDescription;
            }
            set
            {
                _BootloaderDescription = value;
                OnPropertyChanged(nameof(BootloaderDescription));
            }
        }

        private bool _SamsungWarningVisible = false;
        public bool SamsungWarningVisible
        {
            get
            {
                return _SamsungWarningVisible;
            }
            set
            {
                _SamsungWarningVisible = value;
                OnPropertyChanged(nameof(SamsungWarningVisible));
            }
        }

        public void RebootTo(string Mode)
        {
            switch (Mode)
            {
                case "Normal":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Normal);
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
