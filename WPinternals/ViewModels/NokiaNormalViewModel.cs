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
    // Create this class on the UI thread, after the main-window of the application is initialized.
    // It is necessary to create the object on the UI thread, because notification events to the View need to be fired on that thread.
    // The Model for this ViewModel communicates over USB and for that it uses the hWnd of the main window.
    // Therefore the main window must be created before the ViewModel is created.

    internal class NokiaNormalViewModel : ContextViewModel
    {
        private readonly NokiaPhoneModel CurrentModel;
        private readonly Action<PhoneInterfaces> RequestModeSwitch;

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
                HWID = CurrentModel.ExecuteJsonMethodAsString("ReadHwVersion", "HWVersion"); // 1002
                LogFile.Log("HWID: " + HWID);

                IMEI = CurrentModel.ExecuteJsonMethodAsString("ReadSerialNumber", new System.Collections.Generic.Dictionary<string, object>() { { "SubscriptionId", 0 } }, "SerialNumber"); // IMEI
                string IMEI2 = CurrentModel.ExecuteJsonMethodAsString("ReadSerialNumber", new System.Collections.Generic.Dictionary<string, object>() { { "SubscriptionId", 1 } }, "SerialNumber"); // IMEI 2

                if (!string.IsNullOrEmpty(IMEI2))
                {
                    IMEI += "\n" + IMEI2;
                }

                LogFile.Log("IMEI: " + IMEI);
                PublicID = CurrentModel.ExecuteJsonMethodAsBytes("ReadPublicId", "PublicId"); // 0x14 bytes: a5 e5 ...
                if (PublicID != null)
                {
                    LogFile.Log("Public ID: " + Converter.ConvertHexToString(PublicID, " "));
                }
                BluetoothMac = CurrentModel.ExecuteJsonMethodAsBytes("ReadBtId", "BtId"); // 6 bytes: bc c6 ...
                if (BluetoothMac != null)
                {
                    LogFile.Log("Bluetooth MAC: " + Converter.ConvertHexToString(BluetoothMac, " "));
                }
                WlanMac1 = CurrentModel.ExecuteJsonMethodAsBytes("ReadWlanMacAddress", "WlanMacAddress1"); // 6 bytes
                if (WlanMac1 != null)
                {
                    LogFile.Log("WLAN MAC 1: " + Converter.ConvertHexToString(WlanMac1, " "));
                }
                WlanMac2 = CurrentModel.ExecuteJsonMethodAsBytes("ReadWlanMacAddress", "WlanMacAddress2"); // 6 bytes
                if (WlanMac2 != null)
                {
                    LogFile.Log("WLAN MAC 2: " + Converter.ConvertHexToString(WlanMac2, " "));
                }
                WlanMac3 = CurrentModel.ExecuteJsonMethodAsBytes("ReadWlanMacAddress", "WlanMacAddress3"); // 6 bytes
                if (WlanMac3 != null)
                {
                    LogFile.Log("WLAN MAC 3: " + Converter.ConvertHexToString(WlanMac3, " "));
                }
                WlanMac4 = CurrentModel.ExecuteJsonMethodAsBytes("ReadWlanMacAddress", "WlanMacAddress4"); // 6 bytes
                if (WlanMac4 != null)
                {
                    LogFile.Log("WLAN MAC 4: " + Converter.ConvertHexToString(WlanMac4, " "));
                }

                bool? ProductionDone = CurrentModel.ExecuteJsonMethodAsBoolean("ReadProductionDoneState", "ProductionDone");
                IsBootloaderSecurityEnabled = ProductionDone == null
                    ? CurrentModel.ExecuteJsonMethodAsString("GetSecurityMode", "SecMode").Contains("Restricted", StringComparison.OrdinalIgnoreCase)
                    : CurrentModel.ExecuteJsonMethodAsString("GetSecurityMode", "SecMode").Contains("Restricted", StringComparison.OrdinalIgnoreCase) && (bool)CurrentModel.ExecuteJsonMethodAsBoolean("ReadProductionDoneState", "ProductionDone");

                LogFile.Log("Bootloader Security: " + ((bool)IsBootloaderSecurityEnabled ? "Enabled" : "Disabled"));
                IsSimLocked = CurrentModel.ExecuteJsonMethodAsBoolean("ReadSimlockActive", "SimLockActive");
                LogFile.Log("Simlock: " + ((bool)IsSimLocked ? "Active" : "Unlocked"));

                string BootPolicy = CurrentModel.ExecuteJsonMethodAsString("GetUefiCertificateStatus", "BootPolicy");
                string Db = CurrentModel.ExecuteJsonMethodAsString("GetUefiCertificateStatus", "Db");
                string Dbx = CurrentModel.ExecuteJsonMethodAsString("GetUefiCertificateStatus", "Dbx");
                string Kek = CurrentModel.ExecuteJsonMethodAsString("GetUefiCertificateStatus", "Kek");
                string Pk = CurrentModel.ExecuteJsonMethodAsString("GetUefiCertificateStatus", "Pk");

                this.BootPolicy = BootPolicy;
                LogFile.Log("Boot policy: " + BootPolicy);
                this.Db = Db;
                LogFile.Log("DB: " + Db);
                this.Dbx = Dbx;
                LogFile.Log("DBX: " + Dbx);
                this.Kek = Kek;
                LogFile.Log("KEK: " + Kek);
                this.Pk = Pk;
                LogFile.Log("PK: " + Pk);
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
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
                OnPropertyChanged(nameof(ManufacturerModelName));
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
                OnPropertyChanged(nameof(Operator));
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
                OnPropertyChanged(nameof(Firmware));
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

        private string _BootPolicy = null;
        public string BootPolicy
        {
            get
            {
                return _BootPolicy;
            }
            set
            {
                _BootPolicy = value;
                OnPropertyChanged(nameof(BootPolicy));
            }
        }

        private string _Db = null;
        public string Db
        {
            get
            {
                return _Db;
            }
            set
            {
                _Db = value;
                OnPropertyChanged(nameof(Db));
            }
        }

        private string _Dbx = null;
        public string Dbx
        {
            get
            {
                return _Dbx;
            }
            set
            {
                _Dbx = value;
                OnPropertyChanged(nameof(Dbx));
            }
        }

        private string _Kek = null;
        public string Kek
        {
            get
            {
                return _Kek;
            }
            set
            {
                _Kek = value;
                OnPropertyChanged(nameof(Kek));
            }
        }

        private string _Pk = null;
        public string Pk
        {
            get
            {
                return _Pk;
            }
            set
            {
                _Pk = value;
                OnPropertyChanged(nameof(Pk));
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

        private byte[] _WlanMac1 = null;
        public byte[] WlanMac1
        {
            get
            {
                return _WlanMac1;
            }
            set
            {
                _WlanMac1 = value;
                OnPropertyChanged(nameof(WlanMac1));
            }
        }

        private byte[] _WlanMac2 = null;
        public byte[] WlanMac2
        {
            get
            {
                return _WlanMac2;
            }
            set
            {
                _WlanMac2 = value;
                OnPropertyChanged(nameof(WlanMac2));
            }
        }

        private byte[] _WlanMac3 = null;
        public byte[] WlanMac3
        {
            get
            {
                return _WlanMac3;
            }
            set
            {
                _WlanMac3 = value;
                OnPropertyChanged(nameof(WlanMac3));
            }
        }

        private byte[] _WlanMac4 = null;
        public byte[] WlanMac4
        {
            get
            {
                return _WlanMac4;
            }
            set
            {
                _WlanMac4 = value;
                OnPropertyChanged(nameof(WlanMac4));
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
                OnPropertyChanged(nameof(BluetoothMac));
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
                OnPropertyChanged(nameof(IsBootloaderSecurityEnabled));
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
                OnPropertyChanged(nameof(IsSimLocked));
            }
        }

        public void RebootTo(string Mode)
        {
            switch (Mode)
            {
                case "Flash":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Flash);
                    break;
                case "PhoneInfo":
                    RequestModeSwitch(PhoneInterfaces.Lumia_PhoneInfo);
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
