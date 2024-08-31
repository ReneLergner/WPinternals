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
    internal class LumiaPhoneInfoAppPhoneInfo
    {
        public PhoneInfoState State = PhoneInfoState.Empty;

        public string Type;        // Extended info
        public string ProductCode; // Extended info
        public string Imei;        // Extended info

        public FlashAppType App;

        public byte PhoneInfoAppVersionMajor;
        public byte PhoneInfoAppVersionMinor;
        public byte PhoneInfoAppProtocolVersionMajor;
        public byte PhoneInfoAppProtocolVersionMinor;

        internal void Log(LogType Type)
        {
            if (State == PhoneInfoState.Extended)
            {
                if (this.Type != null)
                {
                    LogFile.Log($"Phone type: {this.Type}", Type);
                }

                if (ProductCode != null)
                {
                    LogFile.Log($"Product code: {ProductCode}", Type);
                }

                if (Type != LogType.ConsoleOnly && (Imei != null))
                {
                    LogFile.Log($"IMEI: {Imei}", LogType.FileOnly);
                }
            }

            switch (App)
            {
                case FlashAppType.PhoneInfoApp:
                    LogFile.Log($"Phone info app: {PhoneInfoAppVersionMajor}.{PhoneInfoAppVersionMinor}", Type);
                    LogFile.Log($"Phone info protocol: {PhoneInfoAppProtocolVersionMajor}.{PhoneInfoAppProtocolVersionMinor}", Type);
                    break;
            }
        }
    }
}
