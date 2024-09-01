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
    internal enum SecureBootKeyType : byte
    {
        Retail = 0,
        Engineering = 1
    }

    [Flags]
    internal enum Fuse
    {
        SecureBoot = 1,
        FfuVerify = 2,
        Jtag = 4,
        Shk = 8,
        Simlock = 16,
        ProductionDone = 32,
        Rkh = 64,
        PublicId = 128,
        Dak = 256,
        SecGen = 512,
        OemId = 1024,
        FastBoot = 2048,
        SpdmSecMode = 4096,
        RpmWdog = 8192,
        Ssm = 16384
    }

    internal enum FfuProtocol
    {
        ProtocolSyncV1 = 1,
        ProtocolAsyncV1 = 2,
        ProtocolSyncV2 = 4,
        ProtocolAsyncV2 = 8,
        ProtocolAsyncV3 = 16
    }

    [Flags]
    internal enum FlashOptions : byte
    {
        SkipWrite = 1,
        SkipHashCheck = 2,
        SkipIdCheck = 4,
        SkipSignatureCheck = 8
    }

    internal class LumiaFlashAppModel : NokiaFlashModel
    {
        internal readonly LumiaFlashAppPhoneInfo FlashAppInfo = new();
        private UefiSecurityStatusResponse _SecurityStatus = null;

        //
        // Not valid commands
        //
        /* NOK    */
        private const string Signature = "NOK";
        /* NOKX   */
        private const string ExtendedMessageSignature = $"{Signature}X";
        /* NOKXF  */
        private const string LumiaFlashAppExtendedMessageSignature = $"{ExtendedMessageSignature}F";

        //
        // Normal commands
        //
        /* NOKB   */ // TODO
        private const string RPMBSignature = $"{Signature}B";
        /* NOKD   */
        private const string DisableTimeoutsSignature = $"{Signature}D";
        /* NOKE   */
        private const string EnableSecureFFUConfigSignature = $"{Signature}E";
        /* NOKF   */
        private const string FlashSignature = $"{Signature}F";
        /* NOKG   */
        private const string FactoryResetSignature = $"{Signature}G";
        /* NOKI   */
        private const string HelloSignature = $"{Signature}I";
        /* NOKJ   */
        private const string JTAGDisableSignature = $"{Signature}J";
        /* NOKK   */
        private const string KickWatchdogSignature = $"{Signature}K";
        /* NOKL   */
        private const string LoadFlashAppSignature = $"{Signature}L";
        /* NOKM   */
        private const string RebootToMassStorageSignature = $"{Signature}M";
        /* NOKN   */
        private const string AuthenticateSignature = $"{Signature}N";
        /* NOKP   */
        private const string RebootToPhoneInfoAppSignature = $"{Signature}P";
        /* NOKR   */
        private const string RebootSignature = $"{Signature}R";
        /* NOKT   */
        private const string GetGPTSignature = $"{Signature}T";
        /* NOKV   */
        private const string InfoQuerySignature = $"{Signature}V";
        /* NOKZ   */
        private const string ShutdownSignature = $"{Signature}Z";

        //
        // Lumia Flash App extended commands
        //
        /* NOKXFB */
        private const string BackupSignature = $"{LumiaFlashAppExtendedMessageSignature}B";
        /* NOKXFC */
        private const string CertificateSignature = $"{LumiaFlashAppExtendedMessageSignature}C";
        /* NOKXFD */
        private const string PlatformSecureBootEnableSignature = $"{LumiaFlashAppExtendedMessageSignature}D";
        /* NOKXFE */
        private const string FlashEraseSignature = $"{LumiaFlashAppExtendedMessageSignature}E";
        /* NOKXFF */
        private const string AsyncFlashModeSignature = $"{LumiaFlashAppExtendedMessageSignature}F";
        /* NOKXFG */
        private const string CreateGoldenBackupSignature = $"{LumiaFlashAppExtendedMessageSignature}G";
        /* NOKXFH */
        private const string WriteRootCertificateHashSignature = $"{LumiaFlashAppExtendedMessageSignature}H";
        /* NOKXFK */
        private const string UEFIKeysProvisionSignature = $"{LumiaFlashAppExtendedMessageSignature}K";
        /* NOKXFL */
        private const string LoadSignature = $"{LumiaFlashAppExtendedMessageSignature}L";
        /* NOKXFP */
        private const string FlashPartitionEraseSignature = $"{LumiaFlashAppExtendedMessageSignature}P";
        /* NOKXFR */
        private const string ReadParamSignature = $"{LumiaFlashAppExtendedMessageSignature}R";
        /* NOKXFS */
        private const string SecureFlashSignature = $"{LumiaFlashAppExtendedMessageSignature}S";
        /* NOKXFT */
        private const string TerminalChallengeSignature = $"{LumiaFlashAppExtendedMessageSignature}T";
        /* NOKXFW */
        private const string WriteParamSignature = $"{LumiaFlashAppExtendedMessageSignature}W";
        /* NOKXFY */
        private const string MMOSStartCommandSignature = $"{LumiaFlashAppExtendedMessageSignature}Y";

        public LumiaFlashAppModel(string DevicePath) : base(DevicePath)
        {
        }

        internal LumiaFlashAppPhoneInfo ReadPhoneInfo(bool ExtendedInfo = true)
        {
            // NOKH = Get Phone Info (IMEI and info from Product.dat) - Not available on some phones, like Lumia 640.
            // NOKV = Info Query

            bool PhoneInfoLogged = FlashAppInfo.State != PhoneInfoState.Empty;
            ReadPhoneInfoFlashApp();

            LumiaFlashAppPhoneInfo Result = FlashAppInfo;

            if (ExtendedInfo && (Result.State == PhoneInfoState.Basic))
            {
                if (Result.App == FlashAppType.FlashApp)
                {
                    Result.Firmware = ReadStringParam("FVER");
                    Result.RKH = ReadParam("RRKH");
                }

                Result.State = PhoneInfoState.Extended;
            }

            if (!PhoneInfoLogged)
            {
                Result.Log(LogType.FileOnly);
            }

            return Result;
        }

        internal LumiaFlashAppPhoneInfo ReadPhoneInfoFlashApp()
        {
            // NOKH = Get Phone Info (IMEI and info from Product.dat) - Not available on some phones, like Lumia 640.
            // NOKV = Info Query

            LumiaFlashAppPhoneInfo Result = FlashAppInfo;

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
                        case FlashAppType.FlashApp:
                            Result.FlashAppProtocolVersionMajor = Response[6];
                            Result.FlashAppProtocolVersionMinor = Response[7];
                            Result.FlashAppVersionMajor = Response[8];
                            Result.FlashAppVersionMinor = Response[9];
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
                            case 0x02:
                                Result.WriteBufferSize = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                break;
                            case 0x03:
                                Result.EmmcSizeInSectors = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                break;
                            case 0x04:
                                Result.SdCardSizeInSectors = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
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

            Result.IsBootloaderSecure = !(FlashAppInfo.Authenticated || FlashAppInfo.RdcPresent || !FlashAppInfo.SecureFfuEnabled);

            return Result;
        }

        internal GPT ReadGPT()
        {
            // If this function is used with a locked BootMgr v1, 
            // then the mode-switching should be done outside this function, 
            // because the context-switches that are used here are not supported on BootMgr v1.

            // Only works in BootLoader-mode or on unlocked bootloaders in Flash-mode!!

            LumiaFlashAppPhoneInfo Info = ReadPhoneInfo(ExtendedInfo: false);
            FlashAppType OriginalAppType = Info.App;
            bool Switch = Info.IsBootloaderSecure;
            if (Switch)
            {
                throw new InvalidOperationException("Bootloader is not unlocked!");
            }

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

            LumiaFlashAppPhoneInfo Info = ReadPhoneInfo(ExtendedInfo: false);
            FlashAppType OriginalAppType = Info.App;
            bool Switch = Info.IsBootloaderSecure;
            if (Switch)
            {
                throw new InvalidOperationException("Bootloader is not unlocked!");
            }

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
                ThrowFlashError(Error);
                //throw new NotSupportedException("ReadGPT: Error 0x" + Error.ToString("X4"));
            }

            System.Buffer.BlockCopy(Buffer, 8, GPTChunk, 0, 0x4400);

            return GPTChunk;
        }

        public byte[] ReadParam(string Param)
        {
            byte[] Request = new byte[0x0B];
            const string Header = ReadParamSignature;

            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10))
            {
                return null;
            }

            byte[] Result = new byte[Response[0x10]];
            Buffer.BlockCopy(Response, 0x11, Result, 0, Response[0x10]);
            return Result;
        }

        public string ReadStringParam(string Param)
        {
            byte[] Bytes = ReadParam(Param);
            if (Bytes == null)
            {
                return null;
            }

            return System.Text.Encoding.ASCII.GetString(Bytes).Trim(['\0']);
        }

        public uint? ReadSecurityFlags()
        {
            byte[] Response = ReadParam("FCS");
            if ((Response == null) || (Response.Length != 4))
            {
                return null;
            }

            // This value is in big endian
            return (UInt32)((Response[0] << 24) | (Response[1] << 16) | (Response[2] << 8) | Response[3]);
        }

        public uint? ReadCurrentChargeLevel()
        {
            byte[] Response = ReadParam("CS");
            if ((Response == null) || (Response.Length != 8))
            {
                return null;
            }

            // This value is in big endian
            return (UInt32)((Response[0] << 24) | (Response[1] << 16) | (Response[2] << 8) | Response[3]) + 1;
        }

        public int? ReadCurrentChargeCurrent()
        {
            byte[] Response = ReadParam("CS");
            if ((Response == null) || (Response.Length != 8))
            {
                return null;
            }

            // This value is in big endian and needs to be XOR'd with 0xFFFFFFFF
            return (Int32)(((Response[4] << 24) | (Response[5] << 16) | (Response[6] << 8) | Response[7]) ^ 0xFFFFFFFF) + 1;
        }

        public UefiSecurityStatusResponse ReadSecurityStatus()
        {
            if (_SecurityStatus != null)
            {
                return _SecurityStatus;
            }

            byte[] Response = ReadParam("SS");
            if (Response == null)
            {
                return null;
            }

            UefiSecurityStatusResponse Result = new();

            Result.IsTestDevice = Response[0];
            Result.PlatformSecureBootStatus = Convert.ToBoolean(Response[1]);
            Result.SecureFfuEfuseStatus = Convert.ToBoolean(Response[2]);
            Result.DebugStatus = Convert.ToBoolean(Response[3]);
            Result.RdcStatus = Convert.ToBoolean(Response[4]);
            Result.AuthenticationStatus = Convert.ToBoolean(Response[5]);
            Result.UefiSecureBootStatus = Convert.ToBoolean(Response[6]);
            Result.CryptoHardwareKey = Convert.ToBoolean(Response[7]);

            _SecurityStatus = Result;

            return Result;
        }

        public FlashVersion GetFlashVersion()
        {
            byte[] Response = ReadParam("FAI");
            if ((Response == null) || (Response.Length < 6))
            {
                return null;
            }

            FlashVersion Result = new();

            Result.ProtocolMajor = Response[1];
            Result.ProtocolMinor = Response[2];
            Result.ApplicationMajor = Response[3];
            Result.ApplicationMinor = Response[4];

            return Result;
        }

        internal UInt16 ReadSecureFfuSupportedProtocolMask()
        {
            return BigEndian.ToUInt16(ReadParam("SFPI"), 0);
        }

        public TerminalResponse GetTerminalResponse()
        {
            byte[] AsskMask = [1, 0, 16, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 64];
            byte[] Request = new byte[0xAC];
            const string Header = TerminalChallengeSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[7] = 1;
            Buffer.BlockCopy(BigEndian.GetBytes(0x18, 4), 0, Request, 0x08, 4); // Subblocktype = 0x18
            Buffer.BlockCopy(BigEndian.GetBytes(0x9C, 4), 0, Request, 0x0C, 4); // Subblocklength = 0x9C
            Buffer.BlockCopy(BigEndian.GetBytes(0x00, 4), 0, Request, 0x10, 4); // AsicIndex = 0x00
            Buffer.BlockCopy(AsskMask, 0, Request, 0x14, 0x10);
            byte[] TerminalResponse = ExecuteRawMethod(Request);
            if ((TerminalResponse?.Length > 0x20) && (BigEndian.ToUInt32(TerminalResponse, 0x14) == (TerminalResponse.Length - 0x18)) && (BitConverter.ToUInt32(TerminalResponse, 0x1C) == (TerminalResponse.Length - 0x20)))
            {
                // Parse Terminal Response from offset 0x18
                return Terminal.Parse(TerminalResponse, 0x18);
            }
            else
            {
                return null;
            }
        }

        public void SendFfuHeaderV1(byte[] FfuHeader, byte Options = 0)
        {
            byte[] Request = new byte[FfuHeader.Length + 0x20];

            const string Header = SecureFlashSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BigEndian.GetBytes(0x0001, 2), 0, Request, 0x06, 2); // Protocol version = 0x0001
            Request[0x08] = 0; // Progress = 0%
            Request[0x0B] = 1; // Subblock count = 1
            Buffer.BlockCopy(BigEndian.GetBytes(0x0000000B, 4), 0, Request, 0x0C, 4); // Subblock type for header = 0x0B
            Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length + 0x0C, 4), 0, Request, 0x10, 4); // Subblock length = length of header + 0x0C
            Buffer.BlockCopy(BigEndian.GetBytes(0x00000000, 4), 0, Request, 0x14, 4); // Header type = 0
            Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length, 4), 0, Request, 0x18, 4); // Payload length = length of header
            Request[0x1C] = Options; // Header options = 0

            Buffer.BlockCopy(FfuHeader, 0, Request, 0x20, FfuHeader.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
            {
                throw new BadConnectionException();
            }

            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }
        }

        public void SendFfuHeaderV2(UInt32 TotalHeaderLength, UInt32 OffsetForThisPart, byte[] FfuHeader, byte Options = 0)
        {
            byte[] Request = new byte[FfuHeader.Length + 0x3C];

            const string Header = SecureFlashSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolSyncV2, 2), 0, Request, 0x06, 2); // Protocol version = 0x0001
            Request[0x08] = 0; // Progress = 0%
            Request[0x0B] = 1; // Subblock count = 1

            Buffer.BlockCopy(BigEndian.GetBytes(0x00000021, 4), 0, Request, 0x0C, 4); // Subblock type for header v2 = 0x21
            Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length + 0x28, 4), 0, Request, 0x10, 4); // Subblock starts at 0x14, payload starts at 0x3C.

            Buffer.BlockCopy(BigEndian.GetBytes(0x00000000, 4), 0, Request, 0x14, 4); // Header type = 0
            Buffer.BlockCopy(BigEndian.GetBytes(TotalHeaderLength, 4), 0, Request, 0x18, 4); // Payload length = length of header
            Request[0x1C] = Options; // Header options = 0

            Buffer.BlockCopy(BigEndian.GetBytes(OffsetForThisPart, 4), 0, Request, 0x1D, 4);
            Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length, 4), 0, Request, 0x21, 4);
            Request[0x25] = 0; // No Erase

            Buffer.BlockCopy(FfuHeader, 0, Request, 0x3C, FfuHeader.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
            {
                throw new BadConnectionException();
            }

            if (Response.Length == 4)
            {
                throw new WPinternalsException("Flash protocol v2 not supported", "The device reported that the Flash protocol v2 was not supported while sending the FFU header.");
            }

            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }
        }

        public void SendFfuPayloadV1(byte[] FfuChunk, int Progress = 0, byte Options = 0)
        {
            byte[] Request = new byte[FfuChunk.Length + 0x1C];

            const string Header = SecureFlashSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolSyncV1, 2), 0, Request, 0x06, 2); // Protocol version = 0x0001
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
            Request[0x0B] = 1; // Subblock count = 1

            Buffer.BlockCopy(BigEndian.GetBytes(0x0000000C, 4), 0, Request, 0x0C, 4); // Subblock type for ChunkData = 0x0C
            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length + 0x08, 4), 0, Request, 0x10, 4); // Subblock length = length of chunk + 0x08

            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length, 4), 0, Request, 0x14, 4); // Payload length = length of chunk
            Request[0x18] = Options; // Data options = 0 (1 = verify)

            Buffer.BlockCopy(FfuChunk, 0, Request, 0x1C, FfuChunk.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
            {
                throw new BadConnectionException();
            }

            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }
        }

        public void SendFfuPayloadV2(byte[] FfuChunk, int Progress = 0, byte Options = 0)
        {
            byte[] Request = new byte[FfuChunk.Length + 0x20];

            const string Header = SecureFlashSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolSyncV2, 2), 0, Request, 0x06, 2); // Protocol
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
            Request[0x0B] = 1; // Subblock count = 1

            Buffer.BlockCopy(BigEndian.GetBytes(0x0000001B, 4), 0, Request, 0x0C, 4); // Subblock type for Payload v2 = 0x1B
            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length + 0x0C, 4), 0, Request, 0x10, 4); // Subblock length = length of chunk + 0x08

            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length, 4), 0, Request, 0x14, 4); // Payload length = length of chunk
            Request[0x18] = Options; // Data options = 0 (1 = verify)

            Buffer.BlockCopy(FfuChunk, 0, Request, 0x20, FfuChunk.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
            {
                throw new BadConnectionException();
            }

            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }
        }

        public void SendFfuPayloadV3(byte[] FfuChunk, UInt32 WriteDescriptorIndex, UInt32 CRC, int Progress = 0, byte Options = 0)
        {
            byte[] Request = new byte[FfuChunk.Length + 0x20];

            const string Header = SecureFlashSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolAsyncV3, 2), 0, Request, 0x06, 2); // Protocol
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
            Request[0x0B] = 1; // Subblock count = 1

            Buffer.BlockCopy(BigEndian.GetBytes(0x0000001D, 4), 0, Request, 0x0C, 4); // Subblock type for Payload v2 = 0x1B
            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length + 0x2C, 4), 0, Request, 0x10, 4); // Subblock length = length of chunk + 0x08

            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length, 4), 0, Request, 0x14, 4); // Payload length = length of chunk
            Request[0x18] = Options; // Data options = 0 (1 = verify)
            Buffer.BlockCopy(BigEndian.GetBytes(WriteDescriptorIndex, 4), 0, Request, 0x19, 4); // Payload length = length of chunk
            Buffer.BlockCopy(BigEndian.GetBytes(CRC, 4), 0, Request, 0x1D, 4); // Payload length = length of chunk

            Buffer.BlockCopy(FfuChunk, 0, Request, 0x40, FfuChunk.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
            {
                throw new BadConnectionException();
            }

            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }
        }

        public void BackupPartitionToRam(string PartitionName)
        {
            PartitionName = PartitionName.ToUpper();
            if (new string[] { "MODEM_FSG", "MODEM_FS1", "MODEM_FS2", "SSD", "DPP" }.Any(s => s == PartitionName))
            {
                byte[] Request = new byte[84];
                const string Header = BackupSignature;
                Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
                Request[0x07] = 1; // Subblock count = 1
                Request[0x08] = 6; // Subblock ID = 6 = Create Partition Backup to RAM
                Buffer.BlockCopy(BigEndian.GetBytes(73, 2), 0, Request, 0x09, 2); // Subblock length = Length of data in subblock including fillers (subblock-ID-field and subblock-length-field are not counted for the subblock-length)
                System.Text.Encoding.Unicode.GetBytes(PartitionName);

                byte[] PartitionBytes = System.Text.Encoding.Unicode.GetBytes(PartitionName);
                Buffer.BlockCopy(PartitionBytes, 0, Request, 0x0C, PartitionBytes.Length);
                Request[0x0C + PartitionBytes.Length + 0x00] = 0; // Trailing zero
                Request[0x0C + PartitionBytes.Length + 0x01] = 0;

                byte[] Response = ExecuteRawMethod(Request);
                int ResultCode = (Response[6] << 8) + Response[7];
                if (ResultCode != 0)
                {
                    ThrowFlashError(ResultCode);
                }
            }
            else
            {
                throw new WPinternalsException("Specified partition cannot be backupped to RAM", "Partition name: \"" + PartitionName + "\".");
            }
        }

        public void LoadMmosBinary(UInt32 TotalLength, UInt32 Offset, bool SkipMmosSupportCheck, byte[] MmosPart)
        {
            byte[] Request = new byte[MmosPart.Length + 0x20];
            const string Header = LoadSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[0x07] = 1; // Subblock count = 1
            Buffer.BlockCopy(BigEndian.GetBytes(0x0000001E, 4), 0, Request, 0x08, 4); // Subblock ID = Load MMOS Binary = 0x1E
            Buffer.BlockCopy(BigEndian.GetBytes(MmosPart.Length + 0x10, 4), 0, Request, 0x0C, 4); // Subblock length = Payload-length + 0x10
            Buffer.BlockCopy(BigEndian.GetBytes(TotalLength, 4), 0, Request, 0x10, 4);
            Buffer.BlockCopy(BigEndian.GetBytes(Offset, 4), 0, Request, 0x14, 4);
            if (SkipMmosSupportCheck)
            {
                Request[0x18] = 1;
            }

            Buffer.BlockCopy(BigEndian.GetBytes(MmosPart.Length, 4), 0, Request, 0x1C, 4);
            Buffer.BlockCopy(MmosPart, 0, Request, 0x20, MmosPart.Length);

            byte[] Response = ExecuteRawMethod(Request);
            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }
        }

        internal void ErasePartition(string PartitionName)
        {
            // Partition "Data" can always be erased.
            // Other partitions can only be erased when a valid RDC certificate is present or full SX authentication was performed.
            if (PartitionName.Length > 0x23)
            {
                throw new ArgumentException("PartitionName cannot exceed 0x23 chars!");
            }

            byte[] Request = new byte[0x50];
            const string Header = FlashPartitionEraseSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[0x06] = 1; // Protocol version must be 1
            Request[0x07] = 0; // Device type = 0

            byte[] PartitionBytes = System.Text.Encoding.Unicode.GetBytes(PartitionName);
            Buffer.BlockCopy(PartitionBytes, 0, Request, 8, PartitionBytes.Length);
            Request[0x08 + PartitionBytes.Length + 0x00] = 0; // Trailing zero
            Request[0x08 + PartitionBytes.Length + 0x01] = 0;

            ExecuteRawMethod(Request);
        }

        internal void StartAsyncFlash()
        {
            byte[] Request = new byte[14];
            ByteOperations.WriteAsciiString(Request, 0, AsyncFlashModeSignature + "S");
            Request[8] = 1; // Protocol version must be 1
            Request[9] = 0; // Protocol type must be 0
            ExecuteRawMethod(Request);
        }

        internal void EndAsyncFlash()
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, AsyncFlashModeSignature + "E");
            ExecuteRawMethod(Request);
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

        public bool? ReadFuseStatus(Fuse fuse)
        {
            uint? flags = ReadSecurityFlags();
            if (!flags.HasValue)
            {
                return null;
            }

            var finalconfig = (Fuse)flags.Value;
            return finalconfig.HasFlag(fuse);
        }

        internal bool? IsBootLoaderUnlocked()
        {
            UefiSecurityStatusResponse SecurityStatus = ReadSecurityStatus();
            if (SecurityStatus != null)
            {
                return SecurityStatus.AuthenticationStatus || SecurityStatus.RdcStatus || !SecurityStatus.SecureFfuEfuseStatus;
            }

            return null;
        }

        public void FlashSectors(UInt32 StartSector, byte[] Data, int Progress = 0)
        {
            // Start sector is in UInt32, so max size of eMMC is 2 TB.

            byte[] Request = new byte[Data.Length + 0x40];

            const string Header = FlashSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[0x05] = 0; // Device type = 0
            Buffer.BlockCopy(BigEndian.GetBytes(StartSector, 4), 0, Request, 0x0B, 4); // Start sector
            Buffer.BlockCopy(BigEndian.GetBytes(Data.Length / 0x200, 4), 0, Request, 0x0F, 4); // Sector count
            Request[0x13] = (byte)Progress; // Progress (0 - 100)
            Request[0x18] = 0; // Do Verify
            Request[0x19] = 0; // Is Test

            Buffer.BlockCopy(Data, 0, Request, 0x40, Data.Length);

            ExecuteRawMethod(Request);
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

        internal void SwitchToPhoneInfoAppContextLegacy()
        {
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, RebootToPhoneInfoAppSignature);
            ExecuteRawVoidMethod(Request);
        }

        internal void WriteGPT(GPT NewGPT)
        {
            bool? unlocked = IsBootLoaderUnlocked();
            if (unlocked == false)
            {
                throw new InvalidOperationException("Bootloader is not unlocked!");
            }

            byte[] Buffer = NewGPT.Rebuild();

            UInt32? HeaderOffset = ByteOperations.FindAscii(Buffer, "EFI PART");
            if (HeaderOffset != 0)
            {
                throw new BadImageFormatException();
            }

            FlashSectors(1, Buffer);
        }

        public void FlashMMOS(string MMOSPath, ProgressUpdater UpdaterPerChunk)
        {
            LogFile.BeginAction("FlashMMOS");

            ProgressUpdater Progress = UpdaterPerChunk;

            LumiaFlashAppPhoneInfo Info = ReadPhoneInfo();
            if (!Info.MmosOverUsbSupported)
            {
                throw new WPinternalsException("Flash failed!", "Protocols not supported. The device reports that loading Microsoft Manufacturing Operating System over USB is not supported.");
            }

            FileInfo info = new(MMOSPath);
            uint length = uint.Parse(info.Length.ToString());

            int offset = 0;
            int maximumBufferSize = (int)Info.WriteBufferSize;

            uint chunkCount = (uint)Math.Truncate((decimal)length / maximumBufferSize);

            using FileStream MMOSFile = new(MMOSPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            for (int i = 0; i < chunkCount; i++)
            {
                Progress.IncreaseProgress(1);

                byte[] data = new byte[maximumBufferSize];
                MMOSFile.Read(data, 0, maximumBufferSize);

                LoadMmosBinary(length, (uint)offset, false, data);

                offset += maximumBufferSize;
            }

            if (length - offset != 0)
            {
                Progress.IncreaseProgress(1);

                byte[] data = new byte[length - offset];
                MMOSFile.Read(data, 0, (int)(length - offset));

                LoadMmosBinary(length, (uint)offset, false, data);
            }

            SwitchToMmosContext();
            ResetPhone();

            LogFile.EndAction("FlashMMOS");
        }

        public void FlashFFU(string FFUPath, bool ResetAfterwards = true, byte Options = 0)
        {
            FlashFFU(new FFU(FFUPath), ResetAfterwards, Options);
        }

        public void FlashFFU(FFU FFU, bool ResetAfterwards = true, byte Options = 0)
        {
            FlashFFU(FFU, null, ResetAfterwards, Options);
        }

        public void FlashFFU(FFU FFU, ProgressUpdater UpdaterPerChunk, bool ResetAfterwards = true, byte Options = 0)
        {
            LogFile.BeginAction("FlashFFU");

            ProgressUpdater Progress = UpdaterPerChunk;

            LumiaFlashAppPhoneInfo Info = ReadPhoneInfo();
            if ((Info.SecureFfuSupportedProtocolMask & ((ushort)FfuProtocol.ProtocolSyncV1 | (ushort)FfuProtocol.ProtocolSyncV2)) == 0)
            {
                throw new WPinternalsException("Flash failed!", "Protocols not supported. The device reports that both Protocol Sync v1 and Protocol Sync v2 are not supported for FFU flashing. Is this an old device?");
            }

            UInt64 CombinedFFUHeaderSize = FFU.HeaderSize;
            byte[] FfuHeader = new byte[CombinedFFUHeaderSize];
            using (FileStream FfuFile = new(FFU.Path, FileMode.Open, FileAccess.Read))
            {
                FfuFile.Read(FfuHeader, 0, (int)CombinedFFUHeaderSize);
                SendFfuHeaderV1(FfuHeader, Options);

                UInt64 Position = CombinedFFUHeaderSize;
                byte[] Payload;
                int ChunkCount = 0;

                if ((Info.SecureFfuSupportedProtocolMask & (ushort)FfuProtocol.ProtocolSyncV2) == 0)
                {
                    // Protocol v1
                    Payload = new byte[FFU.ChunkSize];

                    while (Position < (UInt64)FfuFile.Length)
                    {
                        FfuFile.Read(Payload, 0, Payload.Length);
                        ChunkCount++;
                        SendFfuPayloadV1(Payload, (int)((double)ChunkCount * 100 / FFU.TotalChunkCount), 0);
                        Position += (ulong)Payload.Length;

                        Progress?.IncreaseProgress(1);
                    }
                }
                else
                {
                    // Protocol v2
                    Payload = new byte[Info.WriteBufferSize];

                    while (Position < (UInt64)FfuFile.Length)
                    {
                        UInt32 PayloadSize = Info.WriteBufferSize;
                        if (((UInt64)FfuFile.Length - Position) < PayloadSize)
                        {
                            PayloadSize = (UInt32)((UInt64)FfuFile.Length - Position);
                            Payload = new byte[PayloadSize];
                        }

                        FfuFile.Read(Payload, 0, (int)PayloadSize);
                        ChunkCount += (int)(PayloadSize / FFU.ChunkSize);
                        SendFfuPayloadV2(Payload, (int)((double)ChunkCount * 100 / FFU.TotalChunkCount), 0);
                        Position += PayloadSize;

                        Progress?.IncreaseProgress((ulong)(PayloadSize / FFU.ChunkSize));
                    }
                }
            }

            if (ResetAfterwards)
            {
                ResetPhone();
            }

            LogFile.EndAction("FlashFFU");
        }

        internal void FlashRawPartition(string Path, string PartitionName)
        {
            FlashRawPartition(Path, null, PartitionName, null, null);
        }

        internal void FlashRawPartition(string Path, string PartitionName, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            FlashRawPartition(Path, null, PartitionName, ProgressUpdateCallback, null);
        }

        internal void FlashRawPartition(string Path, string PartitionName, ProgressUpdater UpdaterPerSector)
        {
            FlashRawPartition(Path, null, PartitionName, null, UpdaterPerSector);
        }

        internal void FlashRawPartition(Stream Stream, string PartitionName, ProgressUpdater UpdaterPerSector)
        {
            FlashRawPartition(null, Stream, PartitionName, null, UpdaterPerSector);
        }

        internal void FlashRawPartition(byte[] Buffer, string PartitionName, ProgressUpdater UpdaterPerSector)
        {
            FlashRawPartition(null, new MemoryStream(Buffer, false), PartitionName, null, UpdaterPerSector);
        }

        internal void FlashRawPartition(GPT GPT, string Path, string PartitionName)
        {
            FlashRawPartition(GPT, Path, null, PartitionName, null, null);
        }

        internal void FlashRawPartition(GPT GPT, string Path, string PartitionName, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            FlashRawPartition(GPT, Path, null, PartitionName, ProgressUpdateCallback, null);
        }

        internal void FlashRawPartition(GPT GPT, string Path, string PartitionName, ProgressUpdater UpdaterPerSector)
        {
            FlashRawPartition(GPT, Path, null, PartitionName, null, UpdaterPerSector);
        }

        internal void FlashRawPartition(GPT GPT, Stream Stream, string PartitionName, ProgressUpdater UpdaterPerSector)
        {
            FlashRawPartition(GPT, null, Stream, PartitionName, null, UpdaterPerSector);
        }

        internal void FlashRawPartition(GPT GPT, byte[] Buffer, string PartitionName, ProgressUpdater UpdaterPerSector)
        {
            FlashRawPartition(GPT, null, new MemoryStream(Buffer, false), PartitionName, null, UpdaterPerSector);
        }

        private void FlashRawPartition(string Path, Stream Stream, string PartitionName, Action<int, TimeSpan?> ProgressUpdateCallback, ProgressUpdater UpdaterPerSector)
        {
            GPT GPT = ReadGPT();
            FlashRawPartition(Path, Stream, PartitionName, ProgressUpdateCallback, UpdaterPerSector);
        }

        private void FlashRawPartition(GPT GPT, string Path, Stream Stream, string PartitionName, Action<int, TimeSpan?> ProgressUpdateCallback, ProgressUpdater UpdaterPerSector)
        {
            bool? unlocked = IsBootLoaderUnlocked();
            if (unlocked == false)
            {
                throw new InvalidOperationException("Bootloader is not unlocked!");
            }

            Partition Partition = GPT.Partitions.First((p) => p.Name == PartitionName);
            ulong PartitionSize = (Partition.LastSector - Partition.FirstSector + 1) * 0x200;

            Stream InputStream = null;

            if (Path != null)
            {
                InputStream = new DecompressedStream(File.Open(Path, FileMode.Open));
            }
            else if (Stream != null)
            {
                InputStream = Stream is DecompressedStream ? Stream : new DecompressedStream(Stream);
            }

            if (InputStream != null)
            {
                using (InputStream)
                {
                    UInt64? InputStreamLength = null;
                    try
                    {
                        InputStreamLength = (UInt64)InputStream.Length;
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }

                    if ((InputStreamLength != null) && ((UInt64)InputStream.Length > PartitionSize))
                    {
                        throw new InvalidOperationException("Partition can not be flashed, because its size is too big!");
                    }

                    ProgressUpdater Progress = UpdaterPerSector;
                    if ((Progress == null) && (ProgressUpdateCallback != null) && (InputStreamLength != null))
                    {
                        Progress = new ProgressUpdater((UInt64)(InputStreamLength / 0x200), ProgressUpdateCallback);
                    }

                    int ProgressPercentage = 0;

                    const int FlashBufferSize = 0x200000; // Flash 8 GB phone -> buffersize 0x200000 = 11:45 min, buffersize 0x20000 = 12:30 min
                    byte[] FlashBuffer = new byte[FlashBufferSize];
                    int BytesRead;
                    UInt64 i = 0;
                    do
                    {
                        BytesRead = InputStream.Read(FlashBuffer, 0, FlashBufferSize);

                        byte[] FlashBufferFinalSize;
                        if (BytesRead > 0)
                        {
                            if (BytesRead == FlashBufferSize)
                            {
                                FlashBufferFinalSize = FlashBuffer;
                            }
                            else
                            {
                                FlashBufferFinalSize = new byte[BytesRead];
                                Buffer.BlockCopy(FlashBuffer, 0, FlashBufferFinalSize, 0, BytesRead);
                            }

                            FlashSectors((UInt32)(Partition.FirstSector + (i / 0x200)), FlashBufferFinalSize, ProgressPercentage);
                        }

                        if (Progress != null)
                        {
                            Progress.IncreaseProgress((UInt64)FlashBuffer.Length / 0x200);
                            ProgressPercentage = Progress.ProgressPercentage;
                        }

                        i += FlashBufferSize;
                    }
                    while (BytesRead == FlashBufferSize);
                }
            }
        }
    }
}