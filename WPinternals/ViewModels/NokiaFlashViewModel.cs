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

    internal class NokiaFlashViewModel : ContextViewModel
    {
        private readonly LumiaFlashAppModel CurrentModel;
        private readonly Action<PhoneInterfaces> RequestModeSwitch;
        internal Action SwitchToGettingStarted;
        private readonly object LockDeviceInfo = new();
        private bool DeviceInfoLoaded = false;

        internal NokiaFlashViewModel(NokiaPhoneModel CurrentModel, Action<PhoneInterfaces> RequestModeSwitch, Action SwitchToGettingStarted)
            : base()
        {
            this.CurrentModel = (LumiaFlashAppModel)CurrentModel;
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
                        //string ServiceTag = CurrentModel.ExecuteJsonMethodAsString("ReadServiceTag", "ServiceTag"); // error
                        //byte[] RfChipsetVersion = CurrentModel.ExecuteJsonMethodAsBytes("ReadRfChipsetVersion", "RfChipsetVersion"); // error
                        //byte[] Meid = CurrentModel.ExecuteJsonMethodAsBytes("ReadMeid", "Meid"); // error
                        //string Test = CurrentModel.ExecuteJsonMethodAsString("ReadManufacturingData", ""); -> This method is only possible in Label-mode.

                        UefiSecurityStatusResponse SecurityStatus = CurrentModel.ReadSecurityStatus();

                        UInt32? FlagsResult = CurrentModel.ReadSecurityFlags();
                        UInt32 SecurityFlags = 0;
                        if (FlagsResult != null)
                        {
                            SecurityFlags = (UInt32)CurrentModel.ReadSecurityFlags();
                            LogFile.Log("Security flags: 0x" + SecurityFlags.ToString("X8"));

                            FinalConfigDakStatus = CurrentModel.ReadFuseStatus(Fuse.Dak);
                            FinalConfigFastBootStatus = CurrentModel.ReadFuseStatus(Fuse.FastBoot);
                            FinalConfigFfuVerifyStatus = CurrentModel.ReadFuseStatus(Fuse.FfuVerify);
                            FinalConfigJtagStatus = CurrentModel.ReadFuseStatus(Fuse.Jtag);
                            FinalConfigOemIdStatus = CurrentModel.ReadFuseStatus(Fuse.OemId);
                            FinalConfigProductionDoneStatus = CurrentModel.ReadFuseStatus(Fuse.ProductionDone);
                            FinalConfigPublicIdStatus = CurrentModel.ReadFuseStatus(Fuse.PublicId);
                            FinalConfigRkhStatus = CurrentModel.ReadFuseStatus(Fuse.Rkh);
                            FinalConfigRpmWdogStatus = CurrentModel.ReadFuseStatus(Fuse.RpmWdog);
                            FinalConfigSecGenStatus = CurrentModel.ReadFuseStatus(Fuse.SecGen);
                            FinalConfigSecureBootStatus = CurrentModel.ReadFuseStatus(Fuse.SecureBoot);
                            FinalConfigShkStatus = CurrentModel.ReadFuseStatus(Fuse.Shk);
                            FinalConfigSimlockStatus = CurrentModel.ReadFuseStatus(Fuse.Simlock);
                            FinalConfigSpdmSecModeStatus = CurrentModel.ReadFuseStatus(Fuse.SpdmSecMode);
                            FinalConfigSsmStatus = CurrentModel.ReadFuseStatus(Fuse.Ssm);
                        }
                        else
                        {
                            LogFile.Log("Security flags could not be read");
                        }

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

                        if (SecurityStatus != null)
                        {
                            PlatformSecureBootStatus = SecurityStatus.PlatformSecureBootStatus;
                            LogFile.Log("Platform Secure Boot Status: " + PlatformSecureBootStatus.ToString());
                            UefiSecureBootStatus = SecurityStatus.UefiSecureBootStatus;
                            LogFile.Log("Uefi Secure Boot Status: " + UefiSecureBootStatus.ToString());
                            EffectiveSecureBootStatus = SecurityStatus.PlatformSecureBootStatus && SecurityStatus.UefiSecureBootStatus;
                            LogFile.Log("Effective Secure Boot Status: " + EffectiveSecureBootStatus.ToString());

                            BootloaderSecurityQfuseStatus = SecurityStatus.SecureFfuEfuseStatus;
                            LogFile.Log("Bootloader Security Qfuse Status: " + BootloaderSecurityQfuseStatus.ToString());
                            BootloaderSecurityAuthenticationStatus = SecurityStatus.AuthenticationStatus;
                            LogFile.Log("Bootloader Security Authentication Status: " + BootloaderSecurityAuthenticationStatus.ToString());
                            BootloaderSecurityRdcStatus = SecurityStatus.RdcStatus;
                            LogFile.Log("Bootloader Security Rdc Status: " + BootloaderSecurityRdcStatus.ToString());
                            EffectiveBootloaderSecurityStatus = SecurityStatus.SecureFfuEfuseStatus && !SecurityStatus.AuthenticationStatus && !SecurityStatus.RdcStatus;
                            LogFile.Log("Effective Bootloader Security Status: " + EffectiveBootloaderSecurityStatus.ToString());

                            NativeDebugStatus = !SecurityStatus.DebugStatus;
                            LogFile.Log("Native Debug Status: " + NativeDebugStatus.ToString());
                        }

                        byte[] CID = CurrentModel.ReadParam("CID");
                        byte[] EMS = CurrentModel.ReadParam("EMS");
                        if (CID != null && EMS != null)
                        {
                            UInt16 MID = (UInt16)((CID[0] << 8) + CID[1]);
                            UInt64 MemSize = (UInt64)(((UInt32)EMS[0] << 24) + ((UInt32)EMS[1] << 16) + ((UInt32)EMS[2] << 8) + EMS[3]) * 0x200;
                            double MemSizeDouble = (double)MemSize / 1024 / 1024 / 1024;
                            MemSizeDouble = (double)(int)(MemSizeDouble * 10) / 10;
                            string Manufacturer = null;
                            switch (MID)
                            {
                                case 0x0002:
                                case 0x0045:
                                    Manufacturer = "SanDisk";
                                    break;
                                case 0x0011:
                                    Manufacturer = "Toshiba";
                                    break;
                                case 0x0013:
                                    Manufacturer = "Micron";
                                    break;
                                case 0x0015:
                                    Manufacturer = "Samsung";
                                    break;
                                case 0x0090:
                                    Manufacturer = "Hynix";
                                    break;
                                case 0x0070:
                                    Manufacturer = "Kingston";
                                    break;
                                case 0x00EC:
                                    Manufacturer = "GigaDevice";
                                    break;
                            }
                            eMMC = Manufacturer == null ? MemSizeDouble.ToString() + " GB" : Manufacturer + " " + MemSizeDouble.ToString() + " GB";

                            SamsungWarningVisible = MID == 0x0015;
                        }
                        else
                        {
                            eMMC = "Unknown";
                            SamsungWarningVisible = true;
                        }

                        int? chargecurrent = CurrentModel.ReadCurrentChargeCurrent();

                        if (chargecurrent.HasValue)
                        {
                            ChargingStatus = chargecurrent < 0
                                ? CurrentModel.ReadCurrentChargeLevel() + "% - " + ((-1) * CurrentModel.ReadCurrentChargeCurrent()) + " mA (discharging)"
                                : CurrentModel.ReadCurrentChargeLevel() + "% - " + CurrentModel.ReadCurrentChargeCurrent() + " mA (charging)";

                            LogFile.Log("Charging status: " + ChargingStatus);
                        }
                        else
                        {
                            ChargingStatus = "Unknown";
                            LogFile.Log("Charging status: " + ChargingStatus);
                        }

                        LumiaFlashAppPhoneInfo Info = CurrentModel.ReadPhoneInfo(true);
                        BootloaderDescription = Info.FlashAppProtocolVersionMajor < 2 ? "Lumia Bootloader Spec A" : "Lumia Bootloader Spec B";

                        LogFile.Log("Bootloader: " + BootloaderDescription);

                        ProductCode = "";//TODO: FIXME: Info.ProductCode;
                        LogFile.Log("ProductCode: " + ProductCode);

                        ProductType = "";//TODO: FIXME: Info.Type;
                        LogFile.Log("ProductType: " + ProductType);

                        if (RootKeyHash == null)
                        {
                            LogFile.Log("Root Key Hash was null. Gathering information from an alternative source.");

                            RootKeyHash = Info.RKH;

                            if (RootKeyHash != null)
                            {
                                LogFile.Log("Root Key Hash: " + Converter.ConvertHexToString(RootKeyHash, " "));
                            }
                            else
                            {
                                RootKeyHash = new byte[32];
                                LogFile.Log("Root Key Hash: " + Converter.ConvertHexToString(RootKeyHash, " "));
                            }
                        }

                        if (PlatformName == null)
                        {
                            LogFile.Log("Platform Name was null. Gathering information from an alternative source.");

                            PlatformName = Info.PlatformID;
                            LogFile.Log("Platform Name: " + PlatformName);
                        }

                        if (SecurityStatus == null)
                        {
                            LogFile.Log("Security Status was null. Gathering information from an alternative source.");

                            PlatformSecureBootStatus = Info.PlatformSecureBootEnabled;
                            LogFile.Log("Platform Secure Boot Status: " + PlatformSecureBootStatus.ToString());
                            UefiSecureBootStatus = Info.UefiSecureBootEnabled;
                            LogFile.Log("Uefi Secure Boot Status: " + UefiSecureBootStatus.ToString());
                            EffectiveSecureBootStatus = Info.PlatformSecureBootEnabled && Info.UefiSecureBootEnabled;
                            LogFile.Log("Effective Secure Boot Status: " + EffectiveSecureBootStatus.ToString());

                            BootloaderSecurityQfuseStatus = Info.SecureFfuEnabled;
                            LogFile.Log("Bootloader Security Qfuse Status: " + BootloaderSecurityQfuseStatus.ToString());
                            BootloaderSecurityAuthenticationStatus = Info.Authenticated;
                            LogFile.Log("Bootloader Security Authentication Status: " + BootloaderSecurityAuthenticationStatus.ToString());
                            BootloaderSecurityRdcStatus = Info.RdcPresent;
                            LogFile.Log("Bootloader Security Rdc Status: " + BootloaderSecurityRdcStatus.ToString());
                            EffectiveBootloaderSecurityStatus = !Info.IsBootloaderSecure;
                            LogFile.Log("Effective Bootloader Security Status: " + EffectiveBootloaderSecurityStatus.ToString());

                            NativeDebugStatus = !Info.JtagDisabled;
                            LogFile.Log("Native Debug Status: " + NativeDebugStatus.ToString());
                        }
                    }
                    catch
                    {
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

        private string _ChargingStatus = null;
        public string ChargingStatus
        {
            get
            {
                return _ChargingStatus;
            }
            set
            {
                _ChargingStatus = value;
                OnPropertyChanged(nameof(ChargingStatus));
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

        private bool? _PlatformSecureBootStatus = null;
        public bool? PlatformSecureBootStatus
        {
            get
            {
                return _PlatformSecureBootStatus;
            }
            set
            {
                _PlatformSecureBootStatus = value;
                OnPropertyChanged(nameof(PlatformSecureBootStatus));
            }
        }

        private bool? _BootloaderSecurityQfuseStatus = null;
        public bool? BootloaderSecurityQfuseStatus
        {
            get
            {
                return _BootloaderSecurityQfuseStatus;
            }
            set
            {
                _BootloaderSecurityQfuseStatus = value;
                OnPropertyChanged(nameof(BootloaderSecurityQfuseStatus));
            }
        }

        private bool? _BootloaderSecurityRdcStatus = null;
        public bool? BootloaderSecurityRdcStatus
        {
            get
            {
                return _BootloaderSecurityRdcStatus;
            }
            set
            {
                _BootloaderSecurityRdcStatus = value;
                OnPropertyChanged(nameof(BootloaderSecurityRdcStatus));
            }
        }

        private bool? _BootloaderSecurityAuthenticationStatus = null;
        public bool? BootloaderSecurityAuthenticationStatus
        {
            get
            {
                return _BootloaderSecurityAuthenticationStatus;
            }
            set
            {
                _BootloaderSecurityAuthenticationStatus = value;
                OnPropertyChanged(nameof(BootloaderSecurityAuthenticationStatus));
            }
        }

        private bool? _UefiSecureBootStatus = null;
        public bool? UefiSecureBootStatus
        {
            get
            {
                return _UefiSecureBootStatus;
            }
            set
            {
                _UefiSecureBootStatus = value;
                OnPropertyChanged(nameof(UefiSecureBootStatus));
            }
        }

        private bool? _EffectiveSecureBootStatus = null;
        public bool? EffectiveSecureBootStatus
        {
            get
            {
                return _EffectiveSecureBootStatus;
            }
            set
            {
                _EffectiveSecureBootStatus = value;
                OnPropertyChanged(nameof(EffectiveSecureBootStatus));
            }
        }

        private bool? _EffectiveBootloaderSecurityStatus = null;
        public bool? EffectiveBootloaderSecurityStatus
        {
            get
            {
                return _EffectiveBootloaderSecurityStatus;
            }
            set
            {
                _EffectiveBootloaderSecurityStatus = value;
                OnPropertyChanged(nameof(EffectiveBootloaderSecurityStatus));
            }
        }

        private bool? _NativeDebugStatus = null;
        public bool? NativeDebugStatus
        {
            get
            {
                return _NativeDebugStatus;
            }
            set
            {
                _NativeDebugStatus = value;
                OnPropertyChanged(nameof(NativeDebugStatus));
            }
        }

        #region Final Config
        private bool? _FinalConfigSecureBootStatus = null;
        public bool? FinalConfigSecureBootStatus
        {
            get
            {
                return _FinalConfigSecureBootStatus;
            }
            set
            {
                _FinalConfigSecureBootStatus = value;
                OnPropertyChanged(nameof(FinalConfigSecureBootStatus));
            }
        }

        private bool? _FinalConfigFfuVerifyStatus = null;
        public bool? FinalConfigFfuVerifyStatus
        {
            get
            {
                return _FinalConfigFfuVerifyStatus;
            }
            set
            {
                _FinalConfigFfuVerifyStatus = value;
                OnPropertyChanged(nameof(FinalConfigFfuVerifyStatus));
            }
        }

        private bool? _FinalConfigJtagStatus = null;
        public bool? FinalConfigJtagStatus
        {
            get
            {
                return _FinalConfigJtagStatus;
            }
            set
            {
                _FinalConfigJtagStatus = value;
                OnPropertyChanged(nameof(FinalConfigJtagStatus));
            }
        }

        private bool? _FinalConfigShkStatus = null;
        public bool? FinalConfigShkStatus
        {
            get
            {
                return _FinalConfigShkStatus;
            }
            set
            {
                _FinalConfigShkStatus = value;
                OnPropertyChanged(nameof(FinalConfigShkStatus));
            }
        }

        private bool? _FinalConfigSimlockStatus = null;
        public bool? FinalConfigSimlockStatus
        {
            get
            {
                return _FinalConfigSimlockStatus;
            }
            set
            {
                _FinalConfigSimlockStatus = value;
                OnPropertyChanged(nameof(FinalConfigSimlockStatus));
            }
        }

        private bool? _FinalConfigProductionDoneStatus = null;
        public bool? FinalConfigProductionDoneStatus
        {
            get
            {
                return _FinalConfigProductionDoneStatus;
            }
            set
            {
                _FinalConfigProductionDoneStatus = value;
                OnPropertyChanged(nameof(FinalConfigProductionDoneStatus));
            }
        }

        private bool? _FinalConfigRkhStatus = null;
        public bool? FinalConfigRkhStatus
        {
            get
            {
                return _FinalConfigRkhStatus;
            }
            set
            {
                _FinalConfigRkhStatus = value;
                OnPropertyChanged(nameof(FinalConfigRkhStatus));
            }
        }

        private bool? _FinalConfigPublicIdStatus = null;
        public bool? FinalConfigPublicIdStatus
        {
            get
            {
                return _FinalConfigPublicIdStatus;
            }
            set
            {
                _FinalConfigPublicIdStatus = value;
                OnPropertyChanged(nameof(FinalConfigPublicIdStatus));
            }
        }

        private bool? _FinalConfigDakStatus = null;
        public bool? FinalConfigDakStatus
        {
            get
            {
                return _FinalConfigDakStatus;
            }
            set
            {
                _FinalConfigDakStatus = value;
                OnPropertyChanged(nameof(FinalConfigDakStatus));
            }
        }

        private bool? _FinalConfigSecGenStatus = null;
        public bool? FinalConfigSecGenStatus
        {
            get
            {
                return _FinalConfigSecGenStatus;
            }
            set
            {
                _FinalConfigSecGenStatus = value;
                OnPropertyChanged(nameof(FinalConfigSecGenStatus));
            }
        }

        private bool? _FinalConfigOemIdStatus = null;
        public bool? FinalConfigOemIdStatus
        {
            get
            {
                return _FinalConfigOemIdStatus;
            }
            set
            {
                _FinalConfigOemIdStatus = value;
                OnPropertyChanged(nameof(FinalConfigOemIdStatus));
            }
        }

        private bool? _FinalConfigFastBootStatus = null;
        public bool? FinalConfigFastBootStatus
        {
            get
            {
                return _FinalConfigFastBootStatus;
            }
            set
            {
                _FinalConfigFastBootStatus = value;
                OnPropertyChanged(nameof(FinalConfigFastBootStatus));
            }
        }

        private bool? _FinalConfigSpdmSecModeStatus = null;
        public bool? FinalConfigSpdmSecModeStatus
        {
            get
            {
                return _FinalConfigSpdmSecModeStatus;
            }
            set
            {
                _FinalConfigSpdmSecModeStatus = value;
                OnPropertyChanged(nameof(FinalConfigSpdmSecModeStatus));
            }
        }

        private bool? _FinalConfigRpmWdogStatus = null;
        public bool? FinalConfigRpmWdogStatus
        {
            get
            {
                return _FinalConfigRpmWdogStatus;
            }
            set
            {
                _FinalConfigRpmWdogStatus = value;
                OnPropertyChanged(nameof(FinalConfigRpmWdogStatus));
            }
        }

        private bool? _FinalConfigSsmStatus = null;
        public bool? FinalConfigSsmStatus
        {
            get
            {
                return _FinalConfigSsmStatus;
            }
            set
            {
                _FinalConfigSsmStatus = value;
                OnPropertyChanged(nameof(FinalConfigSsmStatus));
            }
        }
        #endregion

        internal void RebootTo(string Mode)
        {
            switch (Mode)
            {
                case "Normal":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Normal);
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
