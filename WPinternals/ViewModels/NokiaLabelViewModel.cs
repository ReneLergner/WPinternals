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
using System.Threading;

namespace WPinternals
{
    // Create this class on the UI thread, after the main-window of the application is initialized.
    // It is necessary to create the object on the UI thread, because notification events to the View need to be fired on that thread.
    // The Model for this ViewModel communicates over USB and for that it uses the hWnd of the main window.
    // Therefore the main window must be created before the ViewModel is created.

    internal class NokiaLabelViewModel : ContextViewModel
    {
        private readonly NokiaPhoneModel CurrentModel;
        private readonly Action<PhoneInterfaces> RequestModeSwitch;

        internal NokiaLabelViewModel(NokiaPhoneModel CurrentModel, Action<PhoneInterfaces> RequestModeSwitch)
            : base()
        {
            this.RequestModeSwitch = RequestModeSwitch;

            this.CurrentModel = CurrentModel;

            new Thread(() => StartLoadDeviceInfo()).Start();
        }

        private void StartLoadDeviceInfo()
        {
            //byte[] Imsi = CurrentModel.ExecuteJsonMethodAsBytes("ReadImsi", "Imsi"); // 9 bytes: 08 29 40 40 ...
            //string BatteryLevel = CurrentModel.ExecuteJsonMethodAsString("ReadBatteryLevel", "BatteryLevel");
            //string SystemAsicVersion = CurrentModel.ExecuteJsonMethodAsString("ReadSystemAsicVersion", "SystemAsicVersion"); // 8960 -> Chip SOC version
            //string OperatorName = CurrentModel.ExecuteJsonMethodAsString("ReadOperatorName", "OperatorName"); // 000-DK
            //string ManufacturerModelName = CurrentModel.ExecuteJsonMethodAsString("ReadManufacturerModelName", "ManufacturerModelName"); // RM-821_eu_denmark_251
            //string AkVersion = CurrentModel.ExecuteJsonMethodAsString("ReadAkVersion", "AkVersion"); // 9200.10521
            //string BspVersion = CurrentModel.ExecuteJsonMethodAsString("ReadBspVersion", "BspVersion"); // 3051.40000
            //string ProductCode = CurrentModel.ExecuteJsonMethodAsString("ReadProductCode", "ProductCode"); // 059Q9D7
            //string SecurityMode = CurrentModel.ExecuteJsonMethodAsString("GetSecurityMode", "SecMode"); // Restricted
            //string SerialNumber = CurrentModel.ExecuteJsonMethodAsString("ReadSerialNumber", "SerialNumber"); // 356355051883955 = IMEI
            //string SwVersion = CurrentModel.ExecuteJsonMethodAsString("ReadSwVersion", "SwVersion"); // 3051.40000.1349.0007
            //string ModuleCode = CurrentModel.ExecuteJsonMethodAsString("ReadModuleCode", "ModuleCode"); // 0205137
            //byte[] PublicId = CurrentModel.ExecuteJsonMethodAsBytes("ReadPublicId", "PublicId"); // 0x14 bytes: a5 e5 ...
            //string Psn = CurrentModel.ExecuteJsonMethodAsString("ReadPsn", "Psn"); // CEP737370
            //string HwVersion = CurrentModel.ExecuteJsonMethodAsString("ReadHwVersion", "HWVersion"); // 6504 = 6.5.0.4
            //byte[] BtId = CurrentModel.ExecuteJsonMethodAsBytes("ReadBtId", "BtId"); // 6 bytes: bc c6 ...
            //byte[] WlanMacAddress1 = CurrentModel.ExecuteJsonMethodAsBytes("ReadWlanMacAddress", "WlanMacAddress1"); // 6 bytes
            //byte[] WlanMacAddress2 = CurrentModel.ExecuteJsonMethodAsBytes("ReadWlanMacAddress", "WlanMacAddress2"); // same
            //byte[] WlanMacAddress3 = CurrentModel.ExecuteJsonMethodAsBytes("ReadWlanMacAddress", "WlanMacAddress3"); // same
            //bool SimlockActive = CurrentModel.ExecuteJsonMethodAsBoolean("ReadSimlockActive", "SimLockActive"); // false
            //string Version = CurrentModel.ExecuteJsonMethodAsString("GetVersion", "HelloString"); // Resultvars: HelloString Version BuildDate BuildType
            //string ServiceTag = CurrentModel.ExecuteJsonMethodAsString("ReadServiceTag", "ServiceTag"); // error
            //byte[] RfChipsetVersion = CurrentModel.ExecuteJsonMethodAsBytes("ReadRfChipsetVersion", "RfChipsetVersion"); // error
            //byte[] Meid = CurrentModel.ExecuteJsonMethodAsBytes("ReadMeid", "Meid"); // error
            //string Test = CurrentModel.ExecuteJsonMethodAsString("ReadManufacturingData", ""); -> This method is only possible in Label-mode.

            byte[] AsskMask = [1, 0, 16, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 64];
            byte[] Challenge = new byte[0x88];
            Dictionary<string, object> Params = new();
            Params.Add("AsskMask", AsskMask);
            Params.Add("Challenge", Challenge);
            Params.Add("AsicIndex", 0);
            byte[] TerminalResponseBytes = CurrentModel.ExecuteJsonMethodAsBytes("TerminalChallenge", Params, "TerminalResponse");
            if (TerminalResponseBytes != null)
            {
                TerminalResponse TerminalResponse = Terminal.Parse(TerminalResponseBytes, 0);
                if (TerminalResponse != null)
                {
                    PublicID = TerminalResponse.PublicId;
                    LogFile.Log("Public ID: " + Converter.ConvertHexToString(PublicID, " "));
                    RootKeyHash = TerminalResponse.RootKeyHash;
                    LogFile.Log("RootKeyHash: " + Converter.ConvertHexToString(RootKeyHash, " "));
                }
            }

            ManufacturerModelName = CurrentModel.ExecuteJsonMethodAsString("ReadManufacturerModelName", "ManufacturerModelName"); // RM-821_eu_denmark_251
            LogFile.Log("Manufacturer Model Name: " + ManufacturerModelName);
            ProductCode = CurrentModel.ExecuteJsonMethodAsString("ReadProductCode", "ProductCode"); // 059Q9D7
            LogFile.Log("Product Code: " + ProductCode);
            Firmware = CurrentModel.ExecuteJsonMethodAsString("ReadSwVersion", "SwVersion"); // 3051.40000.1349.0007
            LogFile.Log("Firmware: " + Firmware);

            IMEI = CurrentModel.ExecuteJsonMethodAsString("ReadSerialNumber", "SerialNumber"); // IMEI
            LogFile.Log("IMEI: " + IMEI);
            BluetoothMac = CurrentModel.ExecuteJsonMethodAsBytes("ReadBtId", "BtId"); // 6 bytes: bc c6 ...

            if (BluetoothMac != null)
            {
                LogFile.Log("Bluetooth MAC: " + Converter.ConvertHexToString(BluetoothMac, " "));
            }

            WlanMac = CurrentModel.ExecuteJsonMethodAsBytes("ReadWlanMacAddress", "WlanMacAddress1"); // 6 bytes

            if (WlanMac != null)
            {
                LogFile.Log("WLAN MAC: " + Converter.ConvertHexToString(WlanMac, " "));
            }

            IsBootloaderSecurityEnabled = CurrentModel.ExecuteJsonMethodAsBoolean("ReadProductionDoneState", "ProductionDone") ?? false;
            LogFile.Log("Bootloader Security: " + ((bool)IsBootloaderSecurityEnabled ? "Enabled" : "Disabled"));

            Params = new Dictionary<string, object>
            {
                { "ID", 3534 },
                { "NVData", new byte[] { 0 } }
            };
            CurrentModel.ExecuteJsonMethod("WriteNVData", Params); // Error: 150

            Params = new Dictionary<string, object>
            {
                { "ID", 3534 }
            };
            byte[] NV3534 = CurrentModel.ExecuteJsonMethodAsBytes("ReadNVData", Params, "NVData"); // Error: value not written
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
                OnPropertyChanged(nameof(WlanMac));
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
