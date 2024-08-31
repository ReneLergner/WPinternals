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
        private readonly PhoneInfo Info = new();

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
            PhoneInfo Result = Info;

            if (Result.State == PhoneInfoState.Empty)
            {
                byte[] Request = new byte[4];
                ByteOperations.WriteAsciiString(Request, 0, InfoQuerySignature);
                byte[] Response = ExecuteRawMethod(Request);
                if ((Response != null) && (ByteOperations.ReadAsciiString(Response, 0, 4) != "NOKU"))
                {
                    Result.App = (FlashAppType)Response[5];

                    switch (Result.App)
                    {
                        case FlashAppType.BootManager:
                            Result.BootManagerProtocolVersionMajor = Response[6];
                            Result.BootManagerProtocolVersionMinor = Response[7];
                            Result.BootManagerVersionMajor = Response[8];
                            Result.BootManagerVersionMinor = Response[9];
                            break;
                        case FlashAppType.FlashApp:
                            Result.FlashAppProtocolVersionMajor = Response[6];
                            Result.FlashAppProtocolVersionMinor = Response[7];
                            Result.FlashAppVersionMajor = Response[8];
                            Result.FlashAppVersionMinor = Response[9];
                            break;
                        case FlashAppType.PhoneInfoApp:
                            Result.PhoneInfoAppProtocolVersionMajor = Response[6];
                            Result.PhoneInfoAppProtocolVersionMinor = Response[7];
                            Result.PhoneInfoAppVersionMajor = Response[8];
                            Result.PhoneInfoAppVersionMinor = Response[9];
                            break;
                    }

                    byte SubblockCount = Response[10];
                    int SubblockOffset = 11;

                    for (int i = 0; i < SubblockCount; i++)
                    {
                        byte SubblockID = Response[SubblockOffset + 0x00];
                        UInt16 SubblockLength = BigEndian.ToUInt16(Response, SubblockOffset + 0x01);
                        int SubblockPayloadOffset = SubblockOffset + 3;
                        byte SubblockVersion;
                        switch (SubblockID)
                        {
                            case 0x01:
                                Result.TransferSize = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                break;
                            case 0x02:
                                Result.WriteBufferSize = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                break;
                            case 0x03:
                                Result.EmmcSizeInSectors = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                break;
                            case 0x04:
                                if (Result.App == FlashAppType.BootManager)
                                {
                                    Result.FlashAppProtocolVersionMajor = Response[SubblockPayloadOffset + 0x00];
                                    Result.FlashAppProtocolVersionMinor = Response[SubblockPayloadOffset + 0x01];
                                    Result.FlashAppVersionMajor = Response[SubblockPayloadOffset + 0x02];
                                    Result.FlashAppVersionMinor = Response[SubblockPayloadOffset + 0x03];
                                }
                                else if (Result.App == FlashAppType.FlashApp)
                                {
                                    Result.SdCardSizeInSectors = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                }
                                break;
                            case 0x05:
                                Result.PlatformID = ByteOperations.ReadAsciiString(Response, (uint)SubblockPayloadOffset, SubblockLength).Trim([' ', '\0']);
                                break;
                            case 0x0D:
                                Result.AsyncSupport = Response[SubblockPayloadOffset + 1] == 1;
                                break;
                            case 0x0F:
                                SubblockVersion = Response[SubblockPayloadOffset]; // 0x03
                                Result.PlatformSecureBootEnabled = Response[SubblockPayloadOffset + 0x01] == 0x01;
                                Result.SecureFfuEnabled = Response[SubblockPayloadOffset + 0x02] == 0x01;
                                Result.JtagDisabled = Response[SubblockPayloadOffset + 0x03] == 0x01;
                                Result.RdcPresent = Response[SubblockPayloadOffset + 0x04] == 0x01;
                                Result.Authenticated = (Response[SubblockPayloadOffset + 0x05] == 0x01) || (Response[SubblockPayloadOffset + 0x05] == 0x02);
                                Result.UefiSecureBootEnabled = Response[SubblockPayloadOffset + 0x06] == 0x01;
                                Result.SecondaryHardwareKeyPresent = Response[SubblockPayloadOffset + 0x07] == 0x01;
                                break;
                            case 0x10:
                                SubblockVersion = Response[SubblockPayloadOffset]; // 0x01
                                Result.SecureFfuSupportedProtocolMask = BigEndian.ToUInt16(Response, SubblockPayloadOffset + 0x01);
                                break;
                            case 0x1F:
                                Result.MmosOverUsbSupported = Response[SubblockPayloadOffset] == 1;
                                break;
                            case 0x20:
                                // CRC header info
                                break;
                        }
                        SubblockOffset += SubblockLength + 3;
                    }
                }

                Result.State = PhoneInfoState.Basic;
            }

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

            Result.IsBootloaderSecure = !(Info.Authenticated || Info.RdcPresent || !Info.SecureFfuEnabled);

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
