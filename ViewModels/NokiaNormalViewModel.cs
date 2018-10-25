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
    // Create this class on the UI thread, after the main-window of the application is initialized.
    // It is necessary to create the object on the UI thread, because notification events to the View need to be fired on that thread.
    // The Model for this ViewModel communicates over USB and for that it uses the hWnd of the main window.
    // Therefore the main window must be created before the ViewModel is created.

    internal class NokiaNormalViewModel : ContextViewModel
    {
        private NokiaPhoneModel CurrentModel;
        private Action<PhoneInterfaces> RequestModeSwitch;

        internal NokiaNormalViewModel(NokiaPhoneModel CurrentModel, Action<PhoneInterfaces> RequestModeSwitch)
            : base()
        {
            this.CurrentModel = CurrentModel;
            this.RequestModeSwitch = RequestModeSwitch;

            new Thread(() => StartLoadDeviceInfo()).Start();
        }

        private void StartLoadDeviceInfo()
        {
            try
            {
                Operator = CurrentModel.ExecuteJsonMethodAsString("ReadOperatorName", "OperatorName"); // 000-NL
                LogFile.Log("Operator: " + Operator);
                ManufacturerModelName = CurrentModel.ExecuteJsonMethodAsString("ReadManufacturerModelName", "ManufacturerModelName"); // RM-821_eu_denmark_251
                LogFile.Log("Manufacturer Model Name: " + ManufacturerModelName);
                ProductCode = CurrentModel.ExecuteJsonMethodAsString("ReadProductCode", "ProductCode"); // 059Q9D7
                LogFile.Log("Product Code: " + ProductCode);
                Firmware = CurrentModel.ExecuteJsonMethodAsString("ReadSwVersion", "SwVersion"); // 3051.40000.1349.0007
                LogFile.Log("Firmware: " + Firmware);

                IMEI = CurrentModel.ExecuteJsonMethodAsString("ReadSerialNumber", "SerialNumber"); // IMEI
                LogFile.Log("IMEI: " + IMEI);
                PublicID = CurrentModel.ExecuteJsonMethodAsBytes("ReadPublicId", "PublicId"); // 0x14 bytes: a5 e5 ...
                LogFile.Log("Public ID: " + Converter.ConvertHexToString(PublicID, " "));
                BluetoothMac = CurrentModel.ExecuteJsonMethodAsBytes("ReadBtId", "BtId"); // 6 bytes: bc c6 ...
                LogFile.Log("Bluetooth MAC: " + Converter.ConvertHexToString(BluetoothMac, " "));
                WlanMac = CurrentModel.ExecuteJsonMethodAsBytes("ReadWlanMacAddress", "WlanMacAddress1"); // 6 bytes
                LogFile.Log("WLAN MAC: " + Converter.ConvertHexToString(WlanMac, " "));

                bool? ProductionDone = CurrentModel.ExecuteJsonMethodAsBoolean("ReadProductionDoneState", "ProductionDone");
                if (ProductionDone == null)
                    IsBootloaderSecurityEnabled = (CurrentModel.ExecuteJsonMethodAsString("GetSecurityMode", "SecMode").IndexOf("Restricted", StringComparison.OrdinalIgnoreCase) >= 0);
                else
                    IsBootloaderSecurityEnabled = ((CurrentModel.ExecuteJsonMethodAsString("GetSecurityMode", "SecMode").IndexOf("Restricted", StringComparison.OrdinalIgnoreCase) >= 0) && (bool)CurrentModel.ExecuteJsonMethodAsBoolean("ReadProductionDoneState", "ProductionDone"));
                LogFile.Log("Bootloader Security: " + ((bool)IsBootloaderSecurityEnabled ? "Enabled" : "Disabled"));
                IsSimLocked = CurrentModel.ExecuteJsonMethodAsBoolean("ReadSimlockActive", "SimLockActive");
                LogFile.Log("Simlock: " + ((bool)IsSimLocked ? "Active" : "Unlocked"));
            }
            catch { }
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
                OnPropertyChanged("ProductCode");
            }
        }

        private string _ManufacturerModelName = null;
        public string ManufacturerModelName
        {
            get
            {
                return _ManufacturerModelName;
            }
            set
            {
                _ManufacturerModelName = value;
                OnPropertyChanged("ManufacturerModelName");
            }
        }

        private string _Operator = null;
        public string Operator
        {
            get
            {
                return _Operator;
            }
            set
            {
                _Operator = value;
                OnPropertyChanged("Operator");
            }
        }

        private string _Firmware = null;
        public string Firmware
        {
            get
            {
                return _Firmware;
            }
            set
            {
                _Firmware = value;
                OnPropertyChanged("Firmware");
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
                OnPropertyChanged("IMEI");
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
                OnPropertyChanged("PublicID");
            }
        }

        private byte[] _WlanMac = null;
        public byte[] WlanMac
        {
            get
            {
                return _WlanMac;
            }
            set
            {
                _WlanMac = value;
                OnPropertyChanged("WlanMac");
            }
        }

        private byte[] _BluetoothMac = null;
        public byte[] BluetoothMac
        {
            get
            {
                return _BluetoothMac;
            }
            set
            {
                _BluetoothMac = value;
                OnPropertyChanged("BluetoothMac");
            }
        }

        private bool? _IsBootloaderSecurityEnabled = null;
        public bool? IsBootloaderSecurityEnabled
        {
            get
            {
                return _IsBootloaderSecurityEnabled;
            }
            set
            {
                _IsBootloaderSecurityEnabled = value;
                OnPropertyChanged("IsBootloaderSecurityEnabled");
            }
        }

        private bool? _IsSimLocked = null;
        public bool? IsSimLocked
        {
            get
            {
                return _IsSimLocked;
            }
            set
            {
                _IsSimLocked = value;
                OnPropertyChanged("IsSimLocked");
            }
        }

        public void RebootTo(string Mode)
        {
            switch (Mode)
            {
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
