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

namespace WPinternals
{
    internal class LumiaFlashAppPhoneInfo
    {
        public PhoneInfoState State = PhoneInfoState.Empty;

        public string Firmware;    // Extended info
        public byte[] RKH;         // Extended info

        public FlashAppType App;

        public byte FlashAppVersionMajor;
        public byte FlashAppVersionMinor;
        public byte FlashAppProtocolVersionMajor;
        public byte FlashAppProtocolVersionMinor;

        public UInt32 TransferSize;
        public bool MmosOverUsbSupported;
        public UInt32 SdCardSizeInSectors;
        public UInt32 WriteBufferSize;
        public UInt32 EmmcSizeInSectors;
        public string PlatformID;
        public UInt16 SecureFfuSupportedProtocolMask;
        public bool AsyncSupport;

        public bool PlatformSecureBootEnabled;
        public bool SecureFfuEnabled;
        public bool JtagDisabled;
        public bool RdcPresent;
        public bool Authenticated;
        public bool UefiSecureBootEnabled;
        public bool SecondaryHardwareKeyPresent;

        public bool IsBootloaderSecure;

        internal void Log(LogType Type)
        {
            if (State == PhoneInfoState.Extended)
            {
                if (RKH != null)
                {
                    LogFile.Log($"Root key hash: {Converter.ConvertHexToString(RKH, "")}", Type);
                }

                if (Firmware?.Length > 0)
                {
                    LogFile.Log($"Firmware version: {Firmware}", Type);
                }
            }

            switch (App)
            {
                case FlashAppType.FlashApp:
                    LogFile.Log($"Flash app: {FlashAppVersionMajor}.{FlashAppVersionMinor}", Type);
                    LogFile.Log($"Flash protocol: {FlashAppProtocolVersionMajor}.{FlashAppProtocolVersionMinor}", Type);
                    break;
            }

            LogFile.Log($"SecureBoot: {((!PlatformSecureBootEnabled || !UefiSecureBootEnabled) ? "Disabled" : "Enabled")} (Platform Secure Boot: {(PlatformSecureBootEnabled ? "Enabled" : "Disabled")}, UEFI Secure Boot: {(UefiSecureBootEnabled ? "Enabled" : "Disabled")})", Type);

            if ((Type == LogType.ConsoleOnly) || (Type == LogType.FileAndConsole))
            {
                LogFile.Log($"Flash app security: {(!IsBootloaderSecure ? "Disabled" : "Enabled")}", LogType.ConsoleOnly);
            }

            if ((Type == LogType.FileOnly) || (Type == LogType.FileAndConsole))
            {
                LogFile.Log($"Flash app security: {(!IsBootloaderSecure ? "Disabled" : "Enabled")} (FFU security: {(SecureFfuEnabled ? "Enabled" : "Disabled")}, RDC: {(RdcPresent ? "Present" : "Not found")}, Authenticated: {(Authenticated ? "True" : "False")})", LogType.FileOnly);
            }

            LogFile.Log($"JTAG: {(JtagDisabled ? "Disabled" : "Enabled")}", Type);
        }
    }
}
