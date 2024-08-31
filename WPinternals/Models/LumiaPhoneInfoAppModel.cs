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
using System.IO;
using System.Linq;

namespace WPinternals
{
    internal class LumiaPhoneInfoAppModel : NokiaFlashModel
    {
        //
        // Not valid commands
        //
        /* NOK    */
        private const string Signature = "NOK";
        /* NOKX   */
        private const string ExtendedMessageSignature = $"{Signature}X";
        /* NOKXP  */
        private const string PhoneInfoAppExtendedMessageSignature = $"{ExtendedMessageSignature}P";

        //
        // Normal commands
        //
        /* NOKA   */
        private const string ContinueBootSignature = $"{Signature}A";
        /* NOKD   */
        private const string DisableTimeoutsSignature = $"{Signature}D";
        /* NOKH   */
        private const string GetPhoneInfoSignature = $"{Signature}H";
        /* NOKI   */
        private const string HelloSignature = $"{Signature}I";
        /* NOKV   */
        private const string InfoQuerySignature = $"{Signature}V";

        //
        // Phone Info App extended commands
        //
        /* NOKXPH */
        private const string GetVariableSignature = $"{PhoneInfoAppExtendedMessageSignature}H";

        public LumiaPhoneInfoAppModel(string DevicePath) : base(DevicePath)
        {
        }

        internal void ContinueBoot()
        {
            LogFile.Log("Continue boot...");
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, ContinueBootSignature);
            ExecuteRawVoidMethod(Request);
        }

        internal string GetPhoneInfo()
        {
            // NOKH = Get Phone Info (IMEI and info from Product.dat) - Not available on some phones, like Lumia 640.
            // NOKV = Info Query

            if (Info.FlashAppProtocolVersionMajor >= 2)
            {
                return null;
            }

            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, GetPhoneInfoSignature);
            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU"))
            {
                throw new NotSupportedException();
            }

            UInt16 Length = BigEndian.ToUInt16(Response, 0x04);

            string PhoneInfoData = ByteOperations.ReadAsciiString(Response, 0x8, Length);

            return PhoneInfoData;
        }

        internal PhoneInfo ReadPhoneInfo(bool ExtendedInfo = true)
        {
            // NOKH = Get Phone Info (IMEI and info from Product.dat) - Not available on some phones, like Lumia 640.
            // NOKV = Info Query

            bool PhoneInfoLogged = Info.State != PhoneInfoState.Empty;
            ReadPhoneInfoCommon();

            PhoneInfo Result = Info;

            if (ExtendedInfo && (Result.State == PhoneInfoState.Basic))
            {
                try
                {
                    Result.Type = ReadPhoneInfoVariable("TYPE");
                    Result.ProductCode = ReadPhoneInfoVariable("CTR");
                    Result.Imei = ReadPhoneInfoVariable("IMEI");
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }

                Result.State = PhoneInfoState.Extended;
            }

            if (!PhoneInfoLogged)
            {
                Result.Log(LogType.FileOnly);
            }

            return Result;
        }

        internal string ReadPhoneInfoVariable(string VariableName)
        {
            // This function assumes the phone is in Phone Info App context

            byte[] Request = new byte[16];
            ByteOperations.WriteAsciiString(Request, 0, GetVariableSignature + VariableName + "\0"); // BTR or CTR, CTR is public ProductCode
            byte[] Response = ExecuteRawMethod(Request);
            UInt16 Length = BigEndian.ToUInt16(Response, 6);
            return ByteOperations.ReadAsciiString(Response, 8, Length).Trim([' ', '\0']);
        }

        internal string ReadProductCode()
        {
            string Result = ReadPhoneInfoVariable("CTR");
            SwitchToFlashAppContext();
            return Result;
        }
    }
}
