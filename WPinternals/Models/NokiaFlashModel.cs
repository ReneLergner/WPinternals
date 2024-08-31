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
    internal delegate void InterfaceChangedHandler(PhoneInterfaces NewInterface, string DevicePath);

    internal class NokiaFlashModel : NokiaPhoneModel
    {
        private string _devicePath;

        private readonly PhoneInfo Info = new();

        internal event InterfaceChangedHandler InterfaceChanged = delegate { };

        //
        // Not valid commands
        //
        /* NOK    */
        private const string Signature = "NOK";
        /* NOKX   */
        private const string ExtendedMessageSignature = $"{Signature}X";
        /* NOKXC  */
        private const string CommonExtendedMessageSignature = $"{ExtendedMessageSignature}C";

        //
        // Common extended commands
        //
        /* NOKXCB */
        private const string SwitchModeSignature = $"{CommonExtendedMessageSignature}B";
        /* NOKXCE */
        private const string EchoSignature = $"{CommonExtendedMessageSignature}E";

        public NokiaFlashModel(string DevicePath) : base(DevicePath)
        {
            _devicePath = DevicePath;
        }

        internal void SwitchToBootManagerContext(bool DisableTimeOut = true)
        {
            PhoneInfo info = ReadPhoneInfoCommon();
            bool ModernFlashApp = info.FlashAppProtocolVersionMajor >= 2;

            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, SwitchModeSignature + "B");
            byte[] Response = ExecuteRawMethod(Request);
            if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
            {
                throw new NotSupportedException();
            }

            UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);
            if (Error > 0)
            {
                throw new NotSupportedException("SwitchToBootManagerContext: Error 0x" + Error.ToString("X4"));
            }

            if (ModernFlashApp)
            {
                DisableRebootTimeOut();

                Info.App = FlashAppType.BootManager;

                // If current Info class was retrieved while in BootMgr mode, then we need to invalidate this data, because it is incomplete. 
                if (Info.PlatformID == null)
                {
                    Info.State = PhoneInfoState.Empty;
                }

                InterfaceChanged(PhoneInterfaces.Lumia_Bootloader, _devicePath);
            }
        }

        internal void SwitchToPhoneInfoAppContext()
        {
            PhoneInfo info = ReadPhoneInfoCommon();
            bool ModernFlashApp = info.FlashAppProtocolVersionMajor >= 2;

            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, SwitchModeSignature + "P");
            byte[] Response = ExecuteRawMethod(Request);
            if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
            {
                throw new NotSupportedException();
            }

            UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);
            if (Error > 0)
            {
                throw new NotSupportedException("SwitchToPhoneInfoAppContext: Error 0x" + Error.ToString("X4"));
            }

            if (ModernFlashApp)
            {
                DisableRebootTimeOut();

                Info.App = FlashAppType.PhoneInfoApp;

                // If current Info class was retrieved while in BootMgr mode, then we need to invalidate this data, because it is incomplete. 
                if (Info.PlatformID == null)
                {
                    Info.State = PhoneInfoState.Empty;
                }

                InterfaceChanged(PhoneInterfaces.Lumia_PhoneInfo, _devicePath);
            }
        }

        internal void SwitchToMmosContext()
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, SwitchModeSignature + "A");
            ExecuteRawVoidMethod(Request);

            ResetDevice();

            Dispose(true);
        }

        internal void SwitchToFlashAppContext()
        {
            PhoneInfo info = ReadPhoneInfoCommon();
            bool ModernFlashApp = info.FlashAppProtocolVersionMajor >= 2;

            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, SwitchModeSignature + "F"); // This will stop charging the phone
            byte[] Response = ExecuteRawMethod(Request);
            if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
            {
                throw new NotSupportedException();
            }

            UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);
            if (Error > 0)
            {
                throw new NotSupportedException("SwitchToFlashAppContext: Error 0x" + Error.ToString("X4"));
            }

            if (ModernFlashApp)
            {
                DisableRebootTimeOut();

                Info.App = FlashAppType.FlashApp;

                // If current Info class was retrieved while in BootMgr mode, then we need to invalidate this data, because it is incomplete. 
                if (Info.PlatformID == null)
                {
                    Info.State = PhoneInfoState.Empty;
                }

                InterfaceChanged(PhoneInterfaces.Lumia_Flash, _devicePath);
            }
        }


        //
        // Normal commands
        //
        /* NOKD   */
        private const string DisableTimeoutsSignature = $"{Signature}D";
        /* NOKI   */
        private const string HelloSignature = $"{Signature}I";
        /* NOKV   */
        private const string InfoQuerySignature = $"{Signature}V";

        internal FlashAppType GetFlashAppType()
        {
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, InfoQuerySignature);
            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU"))
            {
                throw new NotSupportedException();
            }

            return (FlashAppType)Response[5];
        }


        internal PhoneInfo ReadPhoneInfoCommon()
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

            Result.IsBootloaderSecure = !(Info.Authenticated || Info.RdcPresent || !Info.SecureFfuEnabled);

            if (!PhoneInfoLogged)
            {
                Result.Log(LogType.FileOnly);
            }

            return Result;
        }

        public void DisableRebootTimeOut()
        {
            byte[] Request = new byte[4];
            const string Header = DisableTimeoutsSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        internal void Hello()
        {
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, HelloSignature);
            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
            {
                throw new BadConnectionException();
            }

            if (ByteOperations.ReadAsciiString(Response, 0, 4) != HelloSignature)
            {
                throw new WPinternalsException("Bad response from phone!", "The phone did not answer properly to the Hello message sent.");
            }
        }
    }
}
