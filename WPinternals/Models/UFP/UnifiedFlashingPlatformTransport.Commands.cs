namespace UnifiedFlashingPlatform
{
    public partial class UnifiedFlashingPlatformModel
    {
        //
        // Not valid commands
        //
        /* NOK    */
        private const string Signature = "NOK";
        /* NOKX   */
        private const string ExtendedMessageSignature = $"{Signature}X";
        /* NOKXC  */
        private const string CommonExtendedMessageSignature = $"{ExtendedMessageSignature}C";
        /* NOKXF  */
        private const string UFPExtendedMessageSignature = $"{ExtendedMessageSignature}F";

        //
        // Normal commands
        //
        /* NOKF   */
        private const string FlashSignature = $"{Signature}F";
        /* NOKI   */
        private const string HelloSignature = $"{Signature}I";
        /* NOKM   */
        private const string MassStorageSignature = $"{Signature}M";
        /* NOKN   */
        private const string TelemetryEndSignature = $"{Signature}N";
        /* NOKR   */
        private const string RebootSignature = $"{Signature}R";
        /* NOKS   */
        private const string TelemetryStartSignature = $"{Signature}S";
        /* NOKT   */
        private const string GetGPTSignature = $"{Signature}T";
        /* NOKV   */
        private const string InfoQuerySignature = $"{Signature}V";
        /* NOKZ   */
        private const string ShutdownSignature = $"{Signature}Z";

        //
        // Common extended commands
        //
        /* NOKXCB */
        private const string SwitchModeSignature = $"{CommonExtendedMessageSignature}B";
        /* NOKXCC */
        private const string ClearScreenSignature = $"{CommonExtendedMessageSignature}C";
        /* NOKXCD */
        private const string GetDirectoryEntriesSignature = $"{CommonExtendedMessageSignature}D";
        /* NOKXCE */
        private const string EchoSignature = $"{CommonExtendedMessageSignature}E";
        /* NOKXCF */
        private const string GetFileSignature = $"{CommonExtendedMessageSignature}F";
        /* NOKXCM */
        private const string DisplayCustomMessageSignature = $"{CommonExtendedMessageSignature}M";
        /* NOKXCP */
        private const string PutFileSignature = $"{CommonExtendedMessageSignature}P";
        /* NOKXCT */
        private const string BenchmarkTestsSignature = $"{CommonExtendedMessageSignature}T";

        //
        // UFP extended commands
        //
        /* NOKXFF */
        private const string AsyncFlashModeSignature = $"{UFPExtendedMessageSignature}F";
        /* NOKXFI */
        private const string UnlockSignature = $"{UFPExtendedMessageSignature}I";
        /* NOKXFO */
        private const string RelockSignature = $"{UFPExtendedMessageSignature}O";
        /* NOKXFR */
        private const string ReadParamSignature = $"{UFPExtendedMessageSignature}R";
        /* NOKXFS */
        private const string SecureFlashSignature = $"{UFPExtendedMessageSignature}S";
        /* NOKXFT */
        private const string TelemetryReadSignature = $"{UFPExtendedMessageSignature}T";
        /* NOKXFW */
        private const string WriteParamSignature = $"{UFPExtendedMessageSignature}W";
        /* NOKXFX */
        private const string GetLogsSignature = $"{UFPExtendedMessageSignature}X";

        //
        // UFP Read Params
        //
        private const string AppTypeReadParamSignature = "APPT";
        private const string ResetProtectionReadParamSignature = "ATRP";
        private const string BitlockerStateReadParamSignature = "BITL";
        private const string BuildInfoReadParamSignature = "BNFO";
        private const string CurrentBootOptionReadParamSignature = "CUFO";
        private const string AsyncProtocolSupportReadParamSignature = "DAS\0";
        private const string DirectoryEntriesSizeReadParamSignature = "DES\0";
        private const string DevicePlatformIDReadParamSignature = "DPI\0";
        private const string DevicePropertiesReadParamSignature = "DPR\0";
        private const string DeviceTargetInfoReadParamSignature = "DTI\0";
        private const string DataVerifySpeedReadParamSignature = "DTSP";
        private const string DeviceIDReadParamSignature = "DUI\0";
        private const string EMMCTestResultReadParamSignature = "EMMT";
        private const string EMMCSizeReadParamSignature = "EMS\0";
        private const string EMMCWriteSpeedReadParamSignature = "EMWS";
        private const string FlashAppInfoReadParamSignature = "FAI\0";
        private const string FlashAppOptionsReadParamSignature = "FO\0\0";
        private const string FlashingStatusReadParamSignature = "FS\0\0";
        private const string FileSizeReadParamSignature = "FZ\0\0";
        private const string SecureBootStatusReadParamSignature = "GSBS";
        private const string GetUEFIVariableReadParamSignature = "GUFV";
        private const string GetUEFIVariableSizeReadParamSignature = "GUVS";
        private const string LargestMemoryRegionReadParamSignature = "LGMR";
        private const string LogSizeReadParamSignature = "LZ\0\0";
        private const string MACAddressReadParamSignature = "MAC\0";
        private const string ModeDataReadParamSignature = "MODE";
        private const string ProcessorManufacturerReadParamSignature = "pm\0\0";
        private const string SDCardSizeReadParamSignature = "SDS\0";
        private const string SupportedSecureFFUProtocolsReadParamSignature = "SFPI";
        private const string SMBIOSDataReadParamSignature = "SMBD";
        private const string SerialNumberReadParamSignature = "SN\0\0";
        private const string SizeOfSystemMemoryReadParamSignature = "SOSM";
        private const string SecurityStatusReadParamSignature = "SS\0\0";
        private const string TelemetryLogSizeReadParamSignature = "TELS";
        private const string TransferSizeReadParamSignature = "TS\0\0";
        private const string UEFIBootFlagReadParamSignature = "UBF\0";
        private const string UEFIBootOptionsReadParamSignature = "UEBO";
        private const string UnlockIDReadParamSignature = "UKID";
        private const string UnlockTokenFilesReadParamSignature = "UKTF";
        private const string USBSpeedReadParamSignature = "USBS";
        private const string WriteBufferSizeReadParamSignature = "WBS\0";

        //
        // UFP Write Params
        //
        private const string BootOptionOptionalDataWriteParamSignature = "BOCL";
        private const string BootOptionAsFirstEntryWriteParamSignature = "BOF\0";
        private const string BootOptionAsLastEntryWriteParamSignature = "BOL\0";
        private const string FlashOptionsWriteParamSignature = "FO\0\0";
        private const string LogInsertWriteParamSignature = "LI\0\0";
        private const string ModeWriteParamSignature = "MODE";
        private const string OneTimeBootSequenceWriteParamSignature = "OBU\0";
        private const string SettingUEFIVariableWriteParamSignature = "SUFV";
    }
}
