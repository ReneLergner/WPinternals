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
using System.Linq;
using System.Threading;

namespace WPinternals
{
    // Create this class on the UI thread, after the main-window of the application is initialized.
    // It is necessary to create the object on the UI thread, because notification events to the View need to be fired on that thread.
    // The Model for this ViewModel communicates over USB and for that it uses the hWnd of the main window.
    // Therefore the main window must be created before the ViewModel is created.

    internal class NokiaPhoneInfoViewModel : ContextViewModel
    {
        private readonly LumiaPhoneInfoAppModel CurrentModel;
        private readonly Action<PhoneInterfaces> RequestModeSwitch;
        internal Action SwitchToGettingStarted;
        private readonly object LockDeviceInfo = new();
        private bool DeviceInfoLoaded = false;

        internal NokiaPhoneInfoViewModel(NokiaPhoneModel CurrentModel, Action<PhoneInterfaces> RequestModeSwitch, Action SwitchToGettingStarted)
            : base()
        {
            this.CurrentModel = (LumiaPhoneInfoAppModel)CurrentModel;
            this.RequestModeSwitch = RequestModeSwitch;
            this.SwitchToGettingStarted = SwitchToGettingStarted;
        }
        
        // Device info should be loaded only one time and only when the ViewModel is active
        internal override void EvaluateViewState()
        {
            if (IsActive)
            {
                new Thread(() => StartLoadDeviceInfo()).Start();
            }
        }

        private void StartLoadDeviceInfo()
        {
            lock (LockDeviceInfo)
            {
                if (!DeviceInfoLoaded)
                {
                    try
                    {
                        /*
                         * Version: 1.1.1.3
                         * TYPE: RM-885
                         * BTR: 059R0M0
                         * LPSN: ...
                         * HWID: 1000
                         * CTR: 059S4B1
                         * MC: 0205354
                         * IMEI: ...
                         */
                        string PhoneInfoData = CurrentModel.GetPhoneInfo();
                        if (!string.IsNullOrEmpty(PhoneInfoData))
                        {
                            string[] Variables = PhoneInfoData.Split("\n");
                            Dictionary<string, string> FormattedVariables = [];
                            foreach (string Variable in Variables)
                            {
                                if (!Variable.Contains(":"))
                                {
                                    continue;
                                }

                                FormattedVariables.Add(Variable.Split(":")[0].Trim(), Variable.Split(":")[1].Trim());
                            }

                            HWID = FormattedVariables["HWID"];
                            LogFile.Log("HWID: " + HWID);
                        }

                        LumiaPhoneInfoAppPhoneInfo Info = CurrentModel.ReadPhoneInfo(true);
                        BootloaderDescription = Info.PhoneInfoAppVersionMajor < 2 ? "Lumia Bootloader Spec A" : "Lumia Bootloader Spec B";

                        LogFile.Log("Bootloader: " + BootloaderDescription);

                        ProductCode = Info.ProductCode;
                        LogFile.Log("ProductCode: " + ProductCode);

                        ProductType = Info.Type;
                        LogFile.Log("ProductType: " + ProductType);

                        IMEI = Info.Imei;
                        LogFile.Log("IMEI: " + ProductType);
                    }
                    catch
                    {
                        LogFile.Log("Reading status from Flash interface was aborted.");
                    }
                    DeviceInfoLoaded = true;
                }
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

        private string _HWID = null;
        public string HWID
        {
            get
            {
                return _HWID;
            }
            set
            {
                _HWID = value;
                OnPropertyChanged(nameof(HWID));
            }
        }

        private string _IMEI = null;
        public string IMEI
        {
            get
            {
                return _IMEI;
            }
            set
            {
                _IMEI = value;
                OnPropertyChanged(nameof(IMEI));
            }
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
                case "BootMgr":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Bootloader);
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
