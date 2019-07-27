// Copyright (c) 2018, Rene Lergner - wpinternals.net - @Heathcliff74xda
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
    internal enum FfuProtocol
    {
        ProtocolSyncV1 = 1,
        ProtocolAsyncV1 = 2,
        ProtocolSyncV2 = 4,
        ProtocolAsyncV2 = 8,
        ProtocolAsyncV3 = 16
    }

    internal enum FlashOptions : byte
    {
        SkipWrite = 1,
        SkipHashCheck = 2,
        SkipIdCheck = 4,
        SkipSignatureCheck = 8
    }

    internal delegate void InterfaceChangedHandler(PhoneInterfaces NewInterface);

    internal class NokiaFlashModel : NokiaPhoneModel
    {
        private UefiSecurityStatusResponse _SecurityStatus = null;
        private PhoneInfo Info = new PhoneInfo();

        internal event InterfaceChangedHandler InterfaceChanged = delegate { };

        public NokiaFlashModel(string DevicePath) : base(DevicePath) { }

        public byte[] ReadParam(string Param)
        {
            byte[] Request = new byte[0x0B];
            const string Header = "NOKXFR";

            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Param), 0, Request, 7, Param.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10)) return null;

            byte[] Result = new byte[Response[0x10]];
            System.Buffer.BlockCopy(Response, 0x11, Result, 0, Response[0x10]);
            return Result;
        }

        public string ReadStringParam(string Param)
        {
            byte[] Bytes = ReadParam(Param);
            if (Bytes == null) return null;
            return System.Text.ASCIIEncoding.ASCII.GetString(Bytes).Trim(new char[] { '\0' });
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

        public bool? ReadFuseStatus(Fuse fuse)
        {
            uint? flags = ReadSecurityFlags();
            if (!flags.HasValue)
                return null;
            
            var finalconfig = (Fuse)flags.Value;
            return finalconfig.HasFlag(fuse);
        }

        public uint? ReadSecurityFlags()
        {
            byte[] Response = ReadParam("FCS");
            if ((Response == null) || (Response.Length != 4)) return null;

            // This value is in big endian
            return (UInt32)((Response[0] << 24) | (Response[1] << 16) | (Response[2] << 8) | Response[3]);
        }

        public uint? ReadCurrentChargeLevel()
        {
            byte[] Response = ReadParam("CS");
            if ((Response == null) || (Response.Length != 8)) return null;

            // This value is in big endian
            return (UInt32)((Response[0] << 24) | (Response[1] << 16) | (Response[2] << 8) | Response[3]) + 1;
        }

        public int? ReadCurrentChargeCurrent()
        {
            byte[] Response = ReadParam("CS");
            if ((Response == null) || (Response.Length != 8)) return null;

            // This value is in big endian and needs to be XOR'd with 0xFFFFFFFF
            return (Int32)(((Response[4] << 24) | (Response[5] << 16) | (Response[6] << 8) | Response[7]) ^ 0xFFFFFFFF) + 1;
        }

        public UefiSecurityStatusResponse ReadSecurityStatus()
        {
            if (_SecurityStatus != null)
                return _SecurityStatus;

            byte[] Response = ReadParam("SS");
            if (Response == null) return null;

            UefiSecurityStatusResponse Result = new UefiSecurityStatusResponse();

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

        internal bool IsBootLoaderUnlocked()
        {
            UefiSecurityStatusResponse SecurityStatus = ReadSecurityStatus();
            return (SecurityStatus.AuthenticationStatus || SecurityStatus.RdcStatus || !SecurityStatus.SecureFfuEfuseStatus);
        }

        public TerminalResponse GetTerminalResponse()
        {
            byte[] AsskMask = new byte[0x10] { 1, 0, 16, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 64 };
            byte[] Request = new byte[0xAC];
            string Header = "NOKXFT";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[7] = 1;
            System.Buffer.BlockCopy(BigEndian.GetBytes(0x18, 4), 0, Request, 0x08, 4); // Subblocktype = 0x18
            System.Buffer.BlockCopy(BigEndian.GetBytes(0x9C, 4), 0, Request, 0x0C, 4); // Subblocklength = 0x9C
            System.Buffer.BlockCopy(BigEndian.GetBytes(0x00, 4), 0, Request, 0x10, 4); // AsicIndex = 0x00
            System.Buffer.BlockCopy(AsskMask, 0, Request, 0x14, 0x10);
            byte[] TerminalResponse = ExecuteRawMethod(Request);
            if ((TerminalResponse != null) && (TerminalResponse.Length > 0x20) && (BigEndian.ToUInt32(TerminalResponse, 0x14) == (TerminalResponse.Length - 0x18)) && (BitConverter.ToUInt32(TerminalResponse, 0x1C) == (TerminalResponse.Length - 0x20)))
            {
                // Parse Terminal Response from offset 0x18
                TerminalResponse ParsedResponse = Terminal.Parse(TerminalResponse, 0x18);
                return ParsedResponse;
            }
            else return null;
        }

        public void SendFfuHeaderV1(byte[] FfuHeader, byte Options = 0)
        {
            byte[] Request = new byte[FfuHeader.Length + 0x20];
            
            string Header = "NOKXFS";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            System.Buffer.BlockCopy(BigEndian.GetBytes(0x0001, 2), 0, Request, 0x06, 2); // Protocol version = 0x0001
            Request[0x08] = 0; // Progress = 0%
            Request[0x0B] = 1; // Subblock count = 1
            System.Buffer.BlockCopy(BigEndian.GetBytes(0x0000000B, 4), 0, Request, 0x0C, 4); // Subblock type for header = 0x0B
            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length + 0x0C, 4), 0, Request, 0x10, 4); // Subblock length = length of header + 0x0C
            System.Buffer.BlockCopy(BigEndian.GetBytes(0x00000000, 4), 0, Request, 0x14, 4); // Header type = 0
            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length, 4), 0, Request, 0x18, 4); // Payload length = length of header
            Request[0x1C] = Options; // Header options = 0

            Buffer.BlockCopy(FfuHeader, 0, Request, 0x20, FfuHeader.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
                throw new BadConnectionException();
            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
                ThrowFlashError(ResultCode);
        }

        public void SendFfuHeaderV2(UInt32 TotalHeaderLength, UInt32 OffsetForThisPart, byte[] FfuHeader, byte Options = 0)
        {
            byte[] Request = new byte[FfuHeader.Length + 0x3C];

            string Header = "NOKXFS";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            System.Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolSyncV2, 2), 0, Request, 0x06, 2); // Protocol version = 0x0001
            Request[0x08] = 0; // Progress = 0%
            Request[0x0B] = 1; // Subblock count = 1

            System.Buffer.BlockCopy(BigEndian.GetBytes(0x00000021, 4), 0, Request, 0x0C, 4); // Subblock type for header v2 = 0x21
            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length + 0x28, 4), 0, Request, 0x10, 4); // Subblock starts at 0x14, payload starts at 0x3C.

            System.Buffer.BlockCopy(BigEndian.GetBytes(0x00000000, 4), 0, Request, 0x14, 4); // Header type = 0
            System.Buffer.BlockCopy(BigEndian.GetBytes(TotalHeaderLength, 4), 0, Request, 0x18, 4); // Payload length = length of header
            Request[0x1C] = Options; // Header options = 0

            System.Buffer.BlockCopy(BigEndian.GetBytes(OffsetForThisPart, 4), 0, Request, 0x1D, 4);
            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length, 4), 0, Request, 0x21, 4);
            Request[0x25] = 0; // No Erase

            Buffer.BlockCopy(FfuHeader, 0, Request, 0x3C, FfuHeader.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
                throw new BadConnectionException();
            if (Response.Length == 4)
                throw new WPinternalsException("Flash protocol v2 not supported");
            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
                ThrowFlashError(ResultCode);
        }

        public void SendFfuPayloadV1(byte[] FfuChunk, int Progress = 0, byte Options = 0)
        {
            byte[] Request = new byte[FfuChunk.Length + 0x1C];

            string Header = "NOKXFS";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            System.Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolSyncV1, 2), 0, Request, 0x06, 2); // Protocol version = 0x0001
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
            Request[0x0B] = 1; // Subblock count = 1

            System.Buffer.BlockCopy(BigEndian.GetBytes(0x0000000C, 4), 0, Request, 0x0C, 4); // Subblock type for ChunkData = 0x0C
            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length + 0x08, 4), 0, Request, 0x10, 4); // Subblock length = length of chunk + 0x08

            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length, 4), 0, Request, 0x14, 4); // Payload length = length of chunk
            Request[0x18] = Options; // Data options = 0 (1 = verify)

            Buffer.BlockCopy(FfuChunk, 0, Request, 0x1C, FfuChunk.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
                throw new BadConnectionException();
            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
                ThrowFlashError(ResultCode);
        }

        public void SendFfuPayloadV2(byte[] FfuChunk, int Progress = 0, byte Options = 0)
        {
            byte[] Request = new byte[FfuChunk.Length + 0x20];

            string Header = "NOKXFS";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            System.Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolSyncV2, 2), 0, Request, 0x06, 2); // Protocol
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
            Request[0x0B] = 1; // Subblock count = 1

            System.Buffer.BlockCopy(BigEndian.GetBytes(0x0000001B, 4), 0, Request, 0x0C, 4); // Subblock type for Payload v2 = 0x1B
            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length + 0x0C, 4), 0, Request, 0x10, 4); // Subblock length = length of chunk + 0x08
            
            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length, 4), 0, Request, 0x14, 4); // Payload length = length of chunk
            Request[0x18] = Options; // Data options = 0 (1 = verify)

            Buffer.BlockCopy(FfuChunk, 0, Request, 0x20, FfuChunk.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
                throw new BadConnectionException();
            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
                ThrowFlashError(ResultCode);
        }

        public void SendFfuPayloadV3(byte[] FfuChunk, UInt32 WriteDescriptorIndex, UInt32 CRC, int Progress = 0, byte Options = 0)
        {
            byte[] Request = new byte[FfuChunk.Length + 0x20];

            string Header = "NOKXFS";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            System.Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolAsyncV3, 2), 0, Request, 0x06, 2); // Protocol
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
            Request[0x0B] = 1; // Subblock count = 1

            System.Buffer.BlockCopy(BigEndian.GetBytes(0x0000001D, 4), 0, Request, 0x0C, 4); // Subblock type for Payload v2 = 0x1B
            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length + 0x2C, 4), 0, Request, 0x10, 4); // Subblock length = length of chunk + 0x08

            System.Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length, 4), 0, Request, 0x14, 4); // Payload length = length of chunk
            Request[0x18] = Options; // Data options = 0 (1 = verify)
            System.Buffer.BlockCopy(BigEndian.GetBytes(WriteDescriptorIndex, 4), 0, Request, 0x19, 4); // Payload length = length of chunk
            System.Buffer.BlockCopy(BigEndian.GetBytes(CRC, 4), 0, Request, 0x1D, 4); // Payload length = length of chunk

            Buffer.BlockCopy(FfuChunk, 0, Request, 0x40, FfuChunk.Length);

            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
                throw new BadConnectionException();
            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
                ThrowFlashError(ResultCode);
        }

        public void BackupPartitionToRam(string PartitionName)
        {
            PartitionName = PartitionName.ToUpper();
            if (new string[] { "MODEM_FSG", "MODEM_FS1", "MODEM_FS2", "SSD", "DPP" }.Any(s => s == PartitionName))
            {
                byte[] Request = new byte[84];
                string Header = "NOKXFB";
                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
                Request[0x07] = 1; // Subblock count = 1
                Request[0x08] = 6; // Subblock ID = 6 = Create Partition Backup to RAM
                System.Buffer.BlockCopy(BigEndian.GetBytes(73, 2), 0, Request, 0x09, 2); // Subblock length = Length of data in subblock including fillers (subblock-ID-field and subblock-length-field are not counted for the subblock-length)
                System.Text.UnicodeEncoding.Unicode.GetBytes(PartitionName);

                byte[] PartitionBytes = System.Text.Encoding.Unicode.GetBytes(PartitionName);
                Buffer.BlockCopy(PartitionBytes, 0, Request, 0x0C, PartitionBytes.Length);
                Request[0x0C + PartitionBytes.Length + 0x00] = 0; // Trailing zero
                Request[0x0C + PartitionBytes.Length + 0x01] = 0;

                byte[] Response = ExecuteRawMethod(Request);
                int ResultCode = (Response[6] << 8) + Response[7];
                if (ResultCode != 0)
                    ThrowFlashError(ResultCode);
            }
            else
                throw new WPinternalsException("Specified partition cannot be backupped to RAM");
        }

        public void LoadMmosBinary(UInt32 TotalLength, UInt32 Offset, bool SkipMmosSupportCheck, byte[] MmosPart)
        {
            byte[] Request = new byte[MmosPart.Length + 0x20];
            string Header = "NOKXFL";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[0x07] = 1; // Subblock count = 1
            System.Buffer.BlockCopy(BigEndian.GetBytes(0x0000001E, 4), 0, Request, 0x08, 4); // Subblock ID = Load MMOS Binary = 0x1E
            System.Buffer.BlockCopy(BigEndian.GetBytes(MmosPart.Length + 0x10, 4), 0, Request, 0x0C, 4); // Subblock length = Payload-length + 0x10
            System.Buffer.BlockCopy(BigEndian.GetBytes(TotalLength, 4), 0, Request, 0x10, 4);
            System.Buffer.BlockCopy(BigEndian.GetBytes(Offset, 4), 0, Request, 0x14, 4);
            if (SkipMmosSupportCheck)
                Request[0x18] = 1;
            System.Buffer.BlockCopy(BigEndian.GetBytes(MmosPart.Length, 4), 0, Request, 0x1C, 4);
            Buffer.BlockCopy(MmosPart, 0, Request, 0x20, MmosPart.Length);

            byte[] Response = ExecuteRawMethod(Request);
            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
                ThrowFlashError(ResultCode);
        }
        
        internal void SwitchToMmosContext()
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, "NOKXCBA");
            ExecuteRawVoidMethod(Request);
            
            ResetDevice();

            Dispose(true);
        }
        
        private void ThrowFlashError(int ErrorCode)
        {
            string SubMessage;

            switch (ErrorCode)
            {
                case 0x0008: SubMessage = "Unsupported protocol / Invalid options"; break;
                case 0x000F: SubMessage = "Invalid sub block count"; break;
                case 0x0010: SubMessage = "Invalid sub block length"; break;
                case 0x0012: SubMessage = "Authentication required"; break;
                case 0x000E: SubMessage = "Invalid sub block type"; break;
                case 0x0013: SubMessage = "Failed async message"; break;
                case 0x1000: SubMessage = "Invalid header type"; break;
                case 0x1001: SubMessage = "FFU header contain unknown extra data"; break;
                case 0x0001: SubMessage = "Couldn't allocate memory"; break;
                case 0x1106: SubMessage = "Security header validation failed"; break;
                case 0x1105: SubMessage = "Invalid hash table size"; break;
                case 0x1104: SubMessage = "Invalid catalog size"; break;
                case 0x1103: SubMessage = "Invalid chunk size"; break;
                case 0x1102: SubMessage = "Unsupported algorithm"; break;
                case 0x1101: SubMessage = "Invalid struct size"; break;
                case 0x1100: SubMessage = "Invalid signature"; break;
                case 0x1202: SubMessage = "Invalid struct size"; break;
                case 0x1203: SubMessage = "Unsupported algorithm"; break;
                case 0x1204: SubMessage = "Invalid chunk size"; break;
                case 0x1005: SubMessage = "Data not aligned correctly"; break;
                case 0x0009: SubMessage = "Locate protocol failed"; break;
                case 0x1003: SubMessage = "Hash mismatch"; break;
                case 0x1006: SubMessage = "Couldn't find hash from security header for index"; break;
                case 0x1004: SubMessage = "Security header import missing / All FFU headers have not been imported"; break;
                case 0x1304: SubMessage = "Invalid platform ID"; break;
                case 0x1307: SubMessage = "Invalid write descriptor info"; break;
                case 0x1306: SubMessage = "Invalid write descriptor info"; break;
                case 0x1305: SubMessage = "Invalid block size"; break;
                case 0x1303: SubMessage = "Unsupported FFU version"; break;
                case 0x1302: SubMessage = "Unsupported struct version"; break;
                case 0x1301: SubMessage = "Invalid update type"; break;
                case 0x100B: SubMessage = "Too much payload data, all data has already been written"; break;
                case 0x1008: SubMessage = "Internal error"; break;
                case 0x1007: SubMessage = "Payload data does not contain all data"; break;
                case 0x0004: SubMessage = "Flash write failed"; break;
                case 0x000D: SubMessage = "Flash verify failed"; break;
                case 0x0002: SubMessage = "Flash read failed"; break;
                default: SubMessage = "Unknown error"; break;
            }

            WPinternalsException Ex = new WPinternalsException("Flash failed!");
            Ex.SubMessage = "Error 0x" + ErrorCode.ToString("X4") + ": " + SubMessage;

            throw Ex;
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

            PhoneInfo Info = ReadPhoneInfo();
            if ((Info.SecureFfuSupportedProtocolMask & ((ushort)FfuProtocol.ProtocolSyncV1 | (ushort)FfuProtocol.ProtocolSyncV2)) == 0)
                throw new WPinternalsException("Flash failed!", "Protocols not supported");

            UInt64 CombinedFFUHeaderSize = FFU.HeaderSize;
            byte[] FfuHeader = new byte[CombinedFFUHeaderSize];
            using (System.IO.FileStream FfuFile = new System.IO.FileStream(FFU.Path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
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

                        if (Progress != null)
                            Progress.IncreaseProgress(1);
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

                        if (Progress != null)
                            Progress.IncreaseProgress((ulong)(PayloadSize / FFU.ChunkSize));
                    }
                }
            }

            if (ResetAfterwards)
                ResetPhone();

            LogFile.EndAction("FlashFFU");
        }

        public void FlashMMOS(string MMOSPath, ProgressUpdater UpdaterPerChunk)
        {
            LogFile.BeginAction("FlashMMOS");

            ProgressUpdater Progress = UpdaterPerChunk;

            PhoneInfo Info = ReadPhoneInfo();
            if (!Info.MmosOverUsbSupported)
                throw new WPinternalsException("Flash failed!", "Protocols not supported");

            FileInfo info = new FileInfo(MMOSPath);
            uint length = uint.Parse(info.Length.ToString());

            int offset = 0;
            int maximumbuffersize = 0x00240000;

            uint totalcounts = (uint)Math.Truncate((decimal)length / maximumbuffersize);

            using (System.IO.FileStream MMOSFile = new System.IO.FileStream(MMOSPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                for (int i = 1; i <= (uint)Math.Truncate((decimal)length / maximumbuffersize); i++)
                {
                    Progress.IncreaseProgress(1);
                    byte[] data = new byte[maximumbuffersize];
                    MMOSFile.Read(data, 0, maximumbuffersize);

                    LoadMmosBinary(length, (uint)offset, false, data);

                    offset += maximumbuffersize;
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
            }
            
            LogFile.EndAction("FlashMMOS");
        }

        public void FlashSectors(UInt32 StartSector, byte[] Data, int Progress = 0)
        {
            // Start sector is in UInt32, so max size of eMMC is 2 TB.

            byte[] Request = new byte[Data.Length + 0x40];

            string Header = "NOKF";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[0x05] = 0; // Device type = 0
            System.Buffer.BlockCopy(BigEndian.GetBytes(StartSector, 4), 0, Request, 0x0B, 4); // Start sector
            System.Buffer.BlockCopy(BigEndian.GetBytes(Data.Length / 0x200, 4), 0, Request, 0x0F, 4); // Sector count
            Request[0x13] = (byte)Progress; // Progress (0 - 100)
            Request[0x18] = 0; // Do Verify
            Request[0x19] = 0; // Is Test

            Buffer.BlockCopy(Data, 0, Request, 0x40, Data.Length);

            ExecuteRawMethod(Request);
        }

        public void DisableRebootTimeOut()
        {
            byte[] Request = new byte[4];
            string Header = "NOKD";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        public void Shutdown()
        {
            byte[] Request = new byte[4];
            string Header = "NOKZ";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        public FlashVersion GetFlashVersion()
        {
            byte[] Response = ReadParam("FAI");
            if ((Response == null) || (Response.Length < 6)) return null;

            FlashVersion Result = new FlashVersion();

            Result.ProtocolMajor = Response[1];
            Result.ProtocolMinor = Response[2];
            Result.ApplicationMajor = Response[3];
            Result.ApplicationMinor = Response[4];

            return Result;
        }

        internal GPT ReadGPT()
        {
            // If this function is used with a locked BootMgr v1, 
            // then the mode-switching should be done outside this function, 
            // because the context-switches that are used here are not supported on BootMgr v1.
            
            // Only works in BootLoader-mode or on unlocked bootloaders in Flash-mode!!
            
            PhoneInfo Info = ReadPhoneInfo(ExtendedInfo: false);
            FlashAppType OriginalAppType = Info.App;
            bool Switch = ((Info.App != FlashAppType.BootManager) && Info.SecureFfuEnabled && !Info.Authenticated && !Info.RdcPresent);
            if (Switch)
                SwitchToBootManagerContext();

            byte[] Request = new byte[0x04];
            const string Header = "NOKT";

            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);

            byte[] Buffer = ExecuteRawMethod(Request);
            if ((Buffer == null) || (Buffer.Length < 0x4408))
                throw new InvalidOperationException("Unable to read GPT!");

            UInt16 Error = (UInt16)((Buffer[6] << 8) + Buffer[7]);
            if (Error > 0)
                throw new NotSupportedException("ReadGPT: Error 0x" + Error.ToString("X4"));

            byte[] GPTBuffer = new byte[Buffer.Length - 0x208];
            System.Buffer.BlockCopy(Buffer, 0x208, GPTBuffer, 0, 0x4200);

            if (Switch)
            {
                if (OriginalAppType == FlashAppType.FlashApp)
                    SwitchToFlashAppContext();
                else
                    SwitchToPhoneInfoAppContext();
            }

            return new GPT(GPTBuffer);  // NOKT message header and MBR are ignored
        }

        internal void WriteGPT(GPT NewGPT)
        {
            if (!IsBootLoaderUnlocked())
                throw new InvalidOperationException("Bootloader is not unlocked!");

            byte[] Buffer = NewGPT.Rebuild();

            UInt32? HeaderOffset = ByteOperations.FindAscii(Buffer, "EFI PART");
            if (HeaderOffset != 0)
                throw new BadImageFormatException();

            FlashSectors(1, Buffer);
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

        private void FlashRawPartition(string Path, Stream Stream, string PartitionName, Action<int, TimeSpan?> ProgressUpdateCallback, ProgressUpdater UpdaterPerSector)
        {
            if (!IsBootLoaderUnlocked())
                throw new InvalidOperationException("Bootloader is not unlocked!");

            GPT GPT = ReadGPT();

            Partition Partition = GPT.Partitions.Where((p) => p.Name == PartitionName).First();
            ulong PartitionSize = (Partition.LastSector - Partition.FirstSector + 1) * 0x200;

            Stream InputStream = null;

            if (Path != null)
                InputStream = new DecompressedStream(File.Open(Path, FileMode.Open));
            else if (Stream != null)
            {
                if (Stream is DecompressedStream)
                    InputStream = Stream;
                else
                    InputStream = new DecompressedStream(Stream);
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
                    catch { }

                    if ((InputStreamLength != null) && ((UInt64)InputStream.Length > PartitionSize))
                        throw new InvalidOperationException("Partition can not be flashed, because its size is too big!");

                    ProgressUpdater Progress = UpdaterPerSector;
                    if ((Progress == null) && (ProgressUpdateCallback != null) && (InputStreamLength != null))
                        Progress = new ProgressUpdater((UInt64)(InputStreamLength / 0x200), ProgressUpdateCallback);
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
                                FlashBufferFinalSize = FlashBuffer;
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

        internal void ErasePartition(string PartitionName)
        {
            // Partition "Data" can always be erased.
            // Other partitions can only be erased when a valid RDC certificate is present or full SX authentication was performed.
            if (PartitionName.Length > 0x23)
                throw new ArgumentException("PartitionName cannot exceed 0x23 chars!");

            byte[] Request = new byte[0x50];
            string Header = "NOKXFP";
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[0x06] = 1; // Protocol version must be 1
            Request[0x07] = 0; // Device type = 0

            byte[] PartitionBytes = System.Text.Encoding.Unicode.GetBytes(PartitionName);
            Buffer.BlockCopy(PartitionBytes, 0, Request, 8, PartitionBytes.Length);
            Request[0x08 + PartitionBytes.Length + 0x00] = 0; // Trailing zero
            Request[0x08 + PartitionBytes.Length + 0x01] = 0;

            ExecuteRawMethod(Request);
        }

        internal FlashAppType GetFlashAppType()
        {
            byte[] Request;

            Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, "NOKV");
            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU"))
                throw new NotSupportedException();
            return (FlashAppType)Response[5];
        }

        internal PhoneInfo ReadPhoneInfo(bool ExtendedInfo = true)
        {
            // NOKH = Get Phone Info (IMEI and info from Product.dat) - Not available on some phones, like Lumia 640.
            // NOKV = Info Query

            bool PhoneInfoLogged = Info.State != PhoneInfoState.Empty;
            PhoneInfo Result = Info;

            if (Result.State == PhoneInfoState.Empty)
            {
                byte[] Request;
                Request = new byte[4];
                ByteOperations.WriteAsciiString(Request, 0, "NOKV");
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
                            case 0x1F:
                                Result.MmosOverUsbSupported = (Response[SubblockPayloadOffset] == 1);
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
                            case 0x02:
                                Result.WriteBufferSize = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                break;
                            case 0x03:
                                Result.EmmcSizeInSectors = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                break;
                            case 0x05:
                                Result.PlatformID = ByteOperations.ReadAsciiString(Response, (uint)SubblockPayloadOffset, (uint)SubblockLength).Trim(new char[] { ' ', '\0' });
                                break;
                            case 0x0D:
                                Result.AsyncSupport = (Response[SubblockPayloadOffset + 1] == 1);
                                break;
                            case 0x0F:
                                SubblockVersion = Response[SubblockPayloadOffset]; // 0x03
                                Result.PlatformSecureBootEnabled = (Response[SubblockPayloadOffset + 0x01] == 0x01);
                                Result.SecureFfuEnabled = (Response[SubblockPayloadOffset + 0x02] == 0x01);
                                Result.JtagDisabled = (Response[SubblockPayloadOffset + 0x03] == 0x01);
                                Result.RdcPresent = (Response[SubblockPayloadOffset + 0x04] == 0x01);
                                Result.Authenticated = ((Response[SubblockPayloadOffset + 0x05] == 0x01) || (Response[SubblockPayloadOffset + 0x05] == 0x02));
                                Result.UefiSecureBootEnabled = (Response[SubblockPayloadOffset + 0x06] == 0x01);
                                Result.SecondaryHardwareKeyPresent = (Response[SubblockPayloadOffset + 0x07] == 0x01);
                                break;
                            case 0x10:
                                SubblockVersion = Response[SubblockPayloadOffset]; // 0x01
                                Result.SecureFfuSupportedProtocolMask = BigEndian.ToUInt16(Response, SubblockPayloadOffset + 0x01);
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

            if ((ExtendedInfo) && (Result.State == PhoneInfoState.Basic))
            {
                FlashAppType OriginalType = Result.App;

                try
                {
                    SwitchToPhoneInfoAppContext(); // May throw NotSupportedException

                    Result.Type = ReadPhoneInfoVariable("TYPE");
                    Result.ProductCode = ReadPhoneInfoVariable("CTR");
                    Result.Imei = ReadPhoneInfoVariable("IMEI");

                    SwitchToFlashAppContext();
                    DisableRebootTimeOut();
                }
                catch { }

                if (Result.App == FlashAppType.FlashApp)
                {
                    Result.Firmware = ReadStringParam("FVER");
                    Result.RKH = ReadParam("RRKH");
                }

                try
                {
                    if (OriginalType == FlashAppType.BootManager)
                    {
                        SwitchToBootManagerContext();
                    }
                }
                catch { }

                Result.State = PhoneInfoState.Extended;
            }

            if (!PhoneInfoLogged)
                Result.Log(LogType.FileOnly);

            return Result;
        }

        internal void ResetPhone()
        {
            LogFile.Log("Rebooting phone", LogType.FileAndConsole);
            try
            {
                byte[] Request = new byte[4];
                ByteOperations.WriteAsciiString(Request, 0, "NOKR");
                ExecuteRawVoidMethod(Request);
            }
            catch
            {
                LogFile.Log("Sending reset-request failed", LogType.FileOnly);
                LogFile.Log("Assuming automatic reset already in progress", LogType.FileOnly);
            }
        }

        internal void ContinueBoot()
        {
            LogFile.Log("Continue boot...");
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, "NOKA");
            ExecuteRawVoidMethod(Request);
        }

        internal void ResetPhoneToFlashMode()
        {
            // This only works when the phone is in BootMgr mode. If it is already in FlashApp, it will not reboot. It only makes the phone unresponsive.
            LogFile.Log("Rebooting phone to Flash mode...");
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, "NOKS");
            ExecuteRawVoidMethod(Request);
        }

        internal void Hello()
        {
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, "NOKI");
            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
                throw new BadConnectionException();
            if (ByteOperations.ReadAsciiString(Response, 0, 4) != "NOKI")
                throw new WPinternalsException("Bad response from phone!");
        }

        internal UInt16 ReadSecureFfuSupportedProtocolMask()
        {
            return BigEndian.ToUInt16(ReadParam("SFPI"), 0);
        }

        internal void SwitchToPhoneInfoAppContext()
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, "NOKXCBP");
            byte[] Response = ExecuteRawMethod(Request);
            if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
                throw new NotSupportedException();
            UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);
            if (Error > 0)
                throw new NotSupportedException("SwitchToPhoneInfoAppContext: Error 0x" + Error.ToString("X4"));
            DisableRebootTimeOut();
            Info.App = FlashAppType.PhoneInfoApp;
            InterfaceChanged(PhoneInterfaces.Lumia_Flash);
        }

        internal void SwitchToFlashAppContext()
        {
            // SwitchToFlashAppContext() should only be used with BootMgr v2
            // For switching from BootMgr to FlashApp, it will use NOKS
            // That will switch to a charging state, whereas a normal context switch will not start charging
            // The implementation of NOKS in BootMgr mode has changed in BootMgr v2
            // It does not disconnect / reconnect anymore and the apptype is changed immediately
            // NOKS still doesnt return a status

            byte[] Request;

            if (Info.State == PhoneInfoState.Empty)
                ReadPhoneInfo(ExtendedInfo: false);

            if (Info.App == FlashAppType.BootManager)
            {
                if (Info.FlashAppProtocolVersionMajor < 2)
                {
                    // A phone with Bootloader Spec A cannot be switched from BootMgr to FlashApp.
                    // NOKS will make the phone unresponsive and let you wait for a new arrival, but that would require a PhoneNotifier and that is not available in this context.
                    // And NOKXCBF is not supported at all.
                    return;
                }

                Request = new byte[4];
                ByteOperations.WriteAsciiString(Request, 0, "NOKS"); // This will let the phone charge
                ExecuteRawVoidMethod(Request); // On phone with bootloader Spec A this triggers a reboot, so DisableRebootTimeOut() cannot be called immediately.
            }
            else if (Info.App == FlashAppType.PhoneInfoApp)
            {
                Request = new byte[7];
                ByteOperations.WriteAsciiString(Request, 0, "NOKXCBF"); // This will stop charging the phone
                byte[] Response = ExecuteRawMethod(Request);
                if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
                    throw new NotSupportedException();
                UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);
                if (Error > 0)
                    throw new NotSupportedException("SwitchToFlashAppContext: Error 0x" + Error.ToString("X4"));
            }

            DisableRebootTimeOut();

            Info.App = FlashAppType.FlashApp;

            // If current Info class was retrieved while in BootMgr mode, then we need to invalidate this data, because it is incomplete. 
            if (Info.PlatformID == null)
                Info.State = PhoneInfoState.Empty;

            InterfaceChanged(PhoneInterfaces.Lumia_Flash);
        }

        internal void SwitchToBootManagerContext(bool DisableTimeOut = true)
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, "NOKXCBB");
            byte[] Response = ExecuteRawMethod(Request);
            if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
                throw new NotSupportedException();
            UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);
            if (Error > 0)
                throw new NotSupportedException("SwitchToBootManagerContext: Error 0x" + Error.ToString("X4"));
            if (DisableTimeOut)
                DisableRebootTimeOut();
            Info.App = FlashAppType.BootManager;
            InterfaceChanged(PhoneInterfaces.Lumia_Bootloader);
        }

        internal string ReadPhoneInfoVariable(string VariableName)
        {
            // This function assumes the phone is in Phone Info App context

            byte[] Request = new byte[16];
            ByteOperations.WriteAsciiString(Request, 0, "NOKXPH" + VariableName + "\0"); // BTR or CTR, CTR is public ProductCode
            byte[] Response = ExecuteRawMethod(Request);
            UInt16 Length = BigEndian.ToUInt16(Response, 6);
            string Result = ByteOperations.ReadAsciiString(Response, 8, Length).Trim(new char[] { ' ', '\0' });
            return Result;
        }

        internal string ReadProductCode()
        {
            SwitchToPhoneInfoAppContext();
            string Result = ReadPhoneInfoVariable("CTR");
            SwitchToFlashAppContext();
            return Result;
        }

        internal void StartAsyncFlash()
        {
            byte[] Request = new byte[14];
            ByteOperations.WriteAsciiString(Request, 0, "NOKXFFS");
            Request[8] = 1; // Protocol version must be 1
            Request[9] = 0; // Protocol type must be 0
            ExecuteRawMethod(Request);
        }

        internal void EndAsyncFlash()
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, "NOKXFFE");
            ExecuteRawMethod(Request);
        }

        internal enum SecureBootKeyType: byte
        {
            Retail = 0,
            Engineering = 1
        }

        internal void ProvisionSecureBootKeys(SecureBootKeyType KeyType) // Only for Flashmode, not BootManager mode.
        {
            byte[] Request = new byte[8];
            ByteOperations.WriteAsciiString(Request, 0, "NOKXFK");
            Request[6] = 0; // Options
            Request[7] = (byte)KeyType;
            byte[] Response = ExecuteRawMethod(Request);
            UInt32 Status = ByteOperations.ReadUInt32(Response, 6);
            if (Status != 0)
                ThrowFlashError((int)Status);
        }
    }

    internal enum FlashAppType
    {
        BootManager = 1,
        FlashApp = 2,
        PhoneInfoApp = 3
    };

    internal enum PhoneInfoState
    {
        Empty,
        Basic,
        Extended
    };

    internal class PhoneInfo
    {
        public PhoneInfoState State = PhoneInfoState.Empty;

        public string Type;        // Extended info
        public string ProductCode; // Extended info
        public string Imei;        // Extended info
        public string Firmware;    // Extended info
        public byte[] RKH;         // Extended info
        
        public FlashAppType App;

        public byte FlashAppVersionMajor;
        public byte FlashAppVersionMinor;
        public byte FlashAppProtocolVersionMajor;
        public byte FlashAppProtocolVersionMinor;

        public byte BootManagerVersionMajor;
        public byte BootManagerVersionMinor;
        public byte BootManagerProtocolVersionMajor;
        public byte BootManagerProtocolVersionMinor;

        public byte PhoneInfoAppVersionMajor;
        public byte PhoneInfoAppVersionMinor;
        public byte PhoneInfoAppProtocolVersionMajor;
        public byte PhoneInfoAppProtocolVersionMinor;

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

        internal void Log(LogType Type)
        {
            if (State == PhoneInfoState.Extended)
            {
                if (this.Type != null)
                    LogFile.Log("Phone type: " + this.Type, Type);
                if (ProductCode != null)
                    LogFile.Log("Product code: " + ProductCode, Type);
                if (RKH != null)
                    LogFile.Log("Root key hash: " + Converter.ConvertHexToString(RKH, ""), Type);
                if (Firmware.Length > 0)
                    LogFile.Log("Firmware version: " + Firmware, Type);
                if (!(Type == LogType.ConsoleOnly) && (Imei != null))
                    LogFile.Log("IMEI: " + Imei, LogType.FileOnly);
            }

            switch (App)
            {
                case FlashAppType.BootManager:
                    LogFile.Log("Bootmanager: " + BootManagerVersionMajor + "." + BootManagerVersionMinor, Type);
                    LogFile.Log("Bootmanager protocol: " + BootManagerProtocolVersionMajor + "." + BootManagerProtocolVersionMinor, Type);
                    LogFile.Log("Flash app: " + FlashAppVersionMajor + "." + FlashAppVersionMinor, Type);
                    LogFile.Log("Flash protocol: " + FlashAppProtocolVersionMajor + "." + FlashAppProtocolVersionMinor, Type);
                    break;
                case FlashAppType.FlashApp:
                    LogFile.Log("Flash app: " + FlashAppVersionMajor + "." + FlashAppVersionMinor, Type);
                    LogFile.Log("Flash protocol: " + FlashAppProtocolVersionMajor + "." + FlashAppProtocolVersionMinor, Type);
                    break;
                case FlashAppType.PhoneInfoApp:
                    LogFile.Log("Phone info app: " + PhoneInfoAppVersionMajor + "." + PhoneInfoAppVersionMinor, Type);
                    LogFile.Log("Phone info protocol: " + PhoneInfoAppProtocolVersionMajor + "." + PhoneInfoAppProtocolVersionMinor, Type);
                    break;
            }

            LogFile.Log("SecureBoot: " + ((!PlatformSecureBootEnabled || !UefiSecureBootEnabled) ? "Disabled" : "Enabled"), Type);

            if ((Type == LogType.ConsoleOnly) || (Type == LogType.FileAndConsole))
                LogFile.Log("Flash app security: " + ((!SecureFfuEnabled || RdcPresent || Authenticated) ? "Disabled" : "Enabled"), LogType.ConsoleOnly);
            if ((Type == LogType.FileOnly) || (Type == LogType.FileAndConsole))
                LogFile.Log("Flash app security: " + ((!SecureFfuEnabled || RdcPresent || Authenticated) ? "Disabled" : "Enabled") + " (FFU security: " + (SecureFfuEnabled ? "Enabled" : "Disabled") + ", RDC: " + (RdcPresent ? "Present" : "Not found") + ", Authenticated: " + (Authenticated ? "True" : "False") + ")", LogType.FileOnly);

            LogFile.Log("JTAG: " + (JtagDisabled ? "Disabled" : "Enabled"), Type);
        }
    }

    internal class UefiSecurityStatusResponse
    {
        public byte IsTestDevice;
        public bool PlatformSecureBootStatus;
        public bool SecureFfuEfuseStatus;
        public bool DebugStatus;
        public bool RdcStatus;
        public bool AuthenticationStatus;
        public bool UefiSecureBootStatus;
        public bool CryptoHardwareKey;
    }

    internal class FlashVersion
    {
        public int ApplicationMajor;
        public int ApplicationMinor;
        public int ProtocolMajor;
        public int ProtocolMinor;
    }
}
