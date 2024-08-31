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
    internal class LumiaBootManagerPhoneInfo
    {
        public PhoneInfoState State = PhoneInfoState.Empty;

        public FlashAppType App;

        public byte FlashAppVersionMajor;
        public byte FlashAppVersionMinor;
        public byte FlashAppProtocolVersionMajor;
        public byte FlashAppProtocolVersionMinor;

        public byte BootManagerVersionMajor;
        public byte BootManagerVersionMinor;
        public byte BootManagerProtocolVersionMajor;
        public byte BootManagerProtocolVersionMinor;

        public UInt32 TransferSize;
        public bool MmosOverUsbSupported;

        internal void Log(LogType Type)
        {
            switch (App)
            {
                case FlashAppType.BootManager:
                    LogFile.Log($"Bootmanager: {BootManagerVersionMajor}.{BootManagerVersionMinor}", Type);
                    LogFile.Log($"Bootmanager protocol: {BootManagerProtocolVersionMajor}.{BootManagerProtocolVersionMinor}", Type);
                    LogFile.Log($"Flash app: {FlashAppVersionMajor}.{FlashAppVersionMinor}", Type);
                    LogFile.Log($"Flash protocol: {FlashAppProtocolVersionMajor}.{FlashAppProtocolVersionMinor}", Type);
                    LogFile.Log($"Flash app: {FlashAppVersionMajor}.{FlashAppVersionMinor}", Type);
                    LogFile.Log($"Flash protocol: {FlashAppProtocolVersionMajor}.{FlashAppProtocolVersionMinor}", Type);
                    break;
            }
        }
    }
}
