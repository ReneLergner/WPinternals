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
    internal class LumiaBootManagerAppModel : NokiaFlashModel
    {
        internal readonly LumiaBootManagerPhoneInfo BootManagerInfo = new();

        internal enum SecureBootKeyType : byte
        {
            Retail = 0,
            Engineering = 1
        }

        //
        // Not valid commands
        //
        /* NOK    */
        private const string Signature = "NOK";
        /* NOKX   */
        private const string ExtendedMessageSignature = $"{Signature}X";
        /* NOKXB  */
        private const string LumiaBootManagerExtendedMessageSignature = $"{ExtendedMessageSignature}B";

        //
        // Normal commands
        //
        /* NOKA   */
        private const string ContinueBootSignature = $"{Signature}A";
        /* NOKB   */
        private const string RPMBSignature = $"{Signature}B";
        /* NOKC   */
        private const string BatteryStatusSignature = $"{Signature}C";
        /* NOKD   */
        private const string DisableTimeoutsSignature = $"{Signature}D";
        /* NOKI   */
        private const string HelloSignature = $"{Signature}I";
        /* NOKM   */
        private const string RebootToMassStorageSignature = $"{Signature}M";
        /* NOKP   */
        private const string RebootToPhoneInfoAppSignature = $"{Signature}P";
        /* NOKR   */
        private const string RebootSignature = $"{Signature}R";
        /* NOKS   */
        private const string RebootToFlashAppSignature = $"{Signature}S";
        /* NOKT   */
        private const string GetGPTSignature = $"{Signature}T";
        /* NOKV   */
        private const string InfoQuerySignature = $"{Signature}V";
        /* NOKW   */
        private const string WriteBootFlagFileSignature = $"{Signature}W";
        /* NOKY   */
        private const string MMOSStartCommandSignature = $"{Signature}Y";
        /* NOKZ   */
        private const string ShutdownSignature = $"{Signature}Z";

        //
        // Lumia Boot Manager extended commands
        //
        /* NOKXBD */
        private const string PlatformSecureBootEnableSignature = $"{LumiaBootManagerExtendedMessageSignature}D";
        /* NOKXBH */
        private const string WriteRootCertificateHashSignature = $"{LumiaBootManagerExtendedMessageSignature}H";
        /* NOKXBK */
        private const string UEFIKeysProvisionSignature = $"{LumiaBootManagerExtendedMessageSignature}K";
        /* NOKXBR */
        private const string ReadManufacturingStateSignature = $"{LumiaBootManagerExtendedMessageSignature}R";
        /* NOKXBU */
        private const string FlushVariablesSignature = $"{LumiaBootManagerExtendedMessageSignature}U";
        /* NOKXBW */
        private const string WriteManufacturingStateSignature = $"{LumiaBootManagerExtendedMessageSignature}W";

        public LumiaBootManagerAppModel(string DevicePath) : base(DevicePath)
        {
        }

        internal void ContinueBoot()
        {
            LogFile.Log("Continue boot...");
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, ContinueBootSignature);
            ExecuteRawVoidMethod(Request);
        }

        internal LumiaBootManagerPhoneInfo ReadPhoneInfo(bool ExtendedInfo = true)
        {
            // NOKH = Get Phone Info (IMEI and info from Product.dat) - Not available on some phones, like Lumia 640.
            // NOKV = Info Query

            bool PhoneInfoLogged = BootManagerInfo.State != PhoneInfoState.Empty;
            ReadPhoneInfoBootManager();

            LumiaBootManagerPhoneInfo Result = BootManagerInfo;

            if (!PhoneInfoLogged)
            {
                Result.Log(LogType.FileOnly);
            }

            return Result;
        }

        internal LumiaBootManagerPhoneInfo ReadPhoneInfoBootManager()
        {
            // NOKH = Get Phone Info (IMEI and info from Product.dat) - Not available on some phones, like Lumia 640.
            // NOKV = Info Query

            LumiaBootManagerPhoneInfo Result = BootManagerInfo;

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
                    }

                    byte SubblockCount = Response[10];
                    int SubblockOffset = 11;

                    for (int i = 0; i < SubblockCount; i++)
                    {
                        byte SubblockID = Response[SubblockOffset + 0x00];

                        LogFile.Log($"{Result.App} SubblockID: 0x{SubblockID:X}");

                        UInt16 SubblockLength = BigEndian.ToUInt16(Response, SubblockOffset + 0x01);
                        int SubblockPayloadOffset = SubblockOffset + 3;
                        byte SubblockVersion;
                        switch (SubblockID)
                        {
                            case 0x01:
                                Result.TransferSize = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                break;
                            case 0x04:
                                Result.FlashAppProtocolVersionMajor = Response[SubblockPayloadOffset + 0x00];
                                Result.FlashAppProtocolVersionMinor = Response[SubblockPayloadOffset + 0x01];
                                Result.FlashAppVersionMajor = Response[SubblockPayloadOffset + 0x02];
                                Result.FlashAppVersionMinor = Response[SubblockPayloadOffset + 0x03];
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

            return Result;
        }

        internal GPT ReadGPT()
        {
            // If this function is used with a locked BootMgr v1, 
            // then the mode-switching should be done outside this function, 
            // because the context-switches that are used here are not supported on BootMgr v1.

            // Only works in BootLoader-mode or on unlocked bootloaders in Flash-mode!!

            byte[] Request = new byte[0x04];
            const string Header = GetGPTSignature;

            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);

            byte[] Buffer = ExecuteRawMethod(Request);
            if ((Buffer == null) || (Buffer.Length < 0x4408))
            {
                throw new InvalidOperationException("Unable to read GPT!");
            }

            UInt16 Error = (UInt16)((Buffer[6] << 8) + Buffer[7]);
            if (Error > 0)
            {
                throw new NotSupportedException("ReadGPT: Error 0x" + Error.ToString("X4"));
            }

            byte[] GPTBuffer = new byte[Buffer.Length - 0x208];
            System.Buffer.BlockCopy(Buffer, 0x208, GPTBuffer, 0, 0x4200);

            return new GPT(GPTBuffer);  // NOKT message header and MBR are ignored
        }

        internal byte[] GetGptChunk(UInt32 Size) // TODO!
        {
            // This function is also used to generate a dummy chunk to flash for testing.
            // The dummy chunk will contain the GPT, so it can be flashed to the first sectors for testing.
            byte[] GPTChunk = new byte[Size];

            byte[] Request = new byte[0x04];
            const string Header = "NOKT";

            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);

            byte[] Buffer = ExecuteRawMethod(Request);
            if ((Buffer == null) || (Buffer.Length < 0x4408))
            {
                throw new InvalidOperationException("Unable to read GPT!");
            }

            UInt16 Error = (UInt16)((Buffer[6] << 8) + Buffer[7]);
            if (Error > 0)
            {
                throw new NotSupportedException("ReadGPT: Error 0x" + Error.ToString("X4"));
            }

            System.Buffer.BlockCopy(Buffer, 8, GPTChunk, 0, 0x4400);

            return GPTChunk;
        }

        internal void ProvisionSecureBootKeys(SecureBootKeyType KeyType) // Only for Flashmode, not BootManager mode.
        {
            byte[] Request = new byte[8];
            ByteOperations.WriteAsciiString(Request, 0, UEFIKeysProvisionSignature);
            Request[6] = 0; // Options
            Request[7] = (byte)KeyType;
            byte[] Response = ExecuteRawMethod(Request);
            UInt32 Status = ByteOperations.ReadUInt32(Response, 6);
            if (Status != 0)
            {
                ThrowFlashError((int)Status);
            }
        }

        private void ThrowFlashError(int ErrorCode)
        {
            string SubMessage = ErrorCode switch
            {
                0x0008 => "Unsupported protocol / Invalid options",
                0x000F => "Invalid sub block count",
                0x0010 => "Invalid sub block length",
                0x0012 => "Authentication required",
                0x000E => "Invalid sub block type",
                0x0013 => "Failed async message",
                0x1000 => "Invalid header type",
                0x1001 => "FFU header contain unknown extra data",
                0x0001 => "Couldn't allocate memory",
                0x1106 => "Security header validation failed",
                0x1105 => "Invalid hash table size",
                0x1104 => "Invalid catalog size",
                0x1103 => "Invalid chunk size",
                0x1102 => "Unsupported algorithm",
                0x1101 => "Invalid struct size",
                0x1100 => "Invalid signature",
                0x1202 => "Invalid struct size",
                0x1203 => "Unsupported algorithm",
                0x1204 => "Invalid chunk size",
                0x1005 => "Data not aligned correctly",
                0x0009 => "Locate protocol failed",
                0x1003 => "Hash mismatch",
                0x1006 => "Couldn't find hash from security header for index",
                0x1004 => "Security header import missing / All FFU headers have not been imported",
                0x1304 => "Invalid platform ID",
                0x1307 => "Invalid write descriptor info",
                0x1306 => "Invalid write descriptor info",
                0x1305 => "Invalid block size",
                0x1303 => "Unsupported FFU version",
                0x1302 => "Unsupported struct version",
                0x1301 => "Invalid update type",
                0x100B => "Too much payload data, all data has already been written",
                0x1008 => "Internal error",
                0x1007 => "Payload data does not contain all data",
                0x0004 => "Flash write failed",
                0x000D => "Flash verify failed",
                0x0002 => "Flash read failed",
                _ => "Unknown error",
            };
            WPinternalsException Ex = new("Flash failed!");
            Ex.SubMessage = "Error 0x" + ErrorCode.ToString("X4") + ": " + SubMessage;

            throw Ex;
        }

        public void Shutdown()
        {
            byte[] Request = new byte[4];
            const string Header = ShutdownSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        internal void ResetPhone()
        {
            LogFile.Log("Rebooting phone", LogType.FileAndConsole);
            try
            {
                byte[] Request = new byte[4];
                ByteOperations.WriteAsciiString(Request, 0, RebootSignature);
                ExecuteRawVoidMethod(Request);
            }
            catch
            {
                LogFile.Log("Sending reset-request failed", LogType.FileOnly);
                LogFile.Log("Assuming automatic reset already in progress", LogType.FileOnly);
            }
        }

        internal void ResetPhoneToFlashMode()
        {
            LumiaBootManagerPhoneInfo info = ReadPhoneInfoBootManager();

            bool ModernFlashApp = info.BootManagerVersionMajor >= 2;

            // This only works when the phone is in BootMgr mode. If it is already in FlashApp, it will not reboot. It only makes the phone unresponsive.
            LogFile.Log("Rebooting phone to Flash mode...");
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, RebootToFlashAppSignature);
            ExecuteRawVoidMethod(Request);

            if (ModernFlashApp)
            {
                DisableRebootTimeOut();

                info.App = FlashAppType.FlashApp;

                RaiseInterfaceChanged(PhoneInterfaces.Lumia_Flash);
            }
        }

        internal void SwitchToPhoneInfoAppContextLegacy()
        {
            LumiaBootManagerPhoneInfo info = ReadPhoneInfoBootManager();

            bool ModernFlashApp = info.BootManagerVersionMajor >= 2;

            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, RebootToPhoneInfoAppSignature);
            ExecuteRawVoidMethod(Request);

            if (ModernFlashApp)
            {
                DisableRebootTimeOut();

                info.App = FlashAppType.PhoneInfoApp;

                RaiseInterfaceChanged(PhoneInterfaces.Lumia_PhoneInfo);
            }
        }

        internal void RebootToFlashApp()
        {
            LumiaBootManagerPhoneInfo info = ReadPhoneInfoBootManager();

            bool ModernFlashApp = info.BootManagerVersionMajor >= 2;

            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, RebootToFlashAppSignature); // This will let the phone charge
            ExecuteRawVoidMethod(Request); // On phone with bootloader Spec A this triggers a reboot, so DisableRebootTimeOut() cannot be called immediately.

            if (ModernFlashApp)
            {
                DisableRebootTimeOut();

                info.App = FlashAppType.FlashApp;

                RaiseInterfaceChanged(PhoneInterfaces.Lumia_Flash);
            }
        }
    }
}
