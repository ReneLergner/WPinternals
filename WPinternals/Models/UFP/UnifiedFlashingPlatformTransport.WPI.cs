using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using WPinternals;
using WPinternals.HelperClasses;

namespace UnifiedFlashingPlatform
{
    public partial class UnifiedFlashingPlatformModel
    {
        private readonly PhoneInfo Info = new();

        public void FlashSectors(uint StartSector, byte[] Data, byte TargetDevice = 0, int Progress = 0)
        {
            // Start sector is in UInt32, so max size of eMMC is 2 TB.

            byte[] Request = new byte[Data.Length + 0x40];

            const string Header = FlashSignature; // NOKF
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Request[0x05] = TargetDevice; // Target device: 0: eMMC, 1: SDIO, 2: Other ???, 3: ???
            Buffer.BlockCopy(BigEndian.GetBytes(StartSector, 4), 0, Request, 0x0B, 4); // Start sector
            Buffer.BlockCopy(BigEndian.GetBytes(Data.Length / 0x200, 4), 0, Request, 0x0F, 4); // Sector count
            Request[0x13] = (byte)Progress; // Progress (0 - 100)
            Request[0x18] = 0; // Verify needed
            Request[0x19] = 0; // Skip write

            Buffer.BlockCopy(Data, 0, Request, 0x40, Data.Length);

            _ = ExecuteRawMethod(Request);
        }

        public void Hello()
        {
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, HelloSignature);
            byte[] Response = ExecuteRawMethod(Request) ?? throw new BadConnectionException();

            if (ByteOperations.ReadAsciiString(Response, 0, 4) != HelloSignature)
            {
                throw new WPinternalsException("Bad response from phone!", "The phone did not answer properly to the Hello message sent.");
            }
        }

        public void ResetPhone()
        {
            Debug.WriteLine("Rebooting phone");
            try
            {
                byte[] Request = new byte[4];
                ByteOperations.WriteAsciiString(Request, 0, RebootSignature);
                ExecuteRawVoidMethod(Request);
            }
            catch
            {
                Debug.WriteLine("Sending reset-request failed");
                Debug.WriteLine("Assuming automatic reset already in progress");
            }
        }

        public GPT ReadGPT()
        {
            // If this function is used with a locked BootMgr v1, 
            // then the mode-switching should be done outside this function, 
            // because the context-switches that are used here are not supported on BootMgr v1.

            // Only works in BootLoader-mode or on unlocked bootloaders in Flash-mode!!

            /*PhoneInfo Info = ReadPhoneInfo(ExtendedInfo: false);
            FlashAppType OriginalAppType = Info.App;
            bool Switch = (Info.App != FlashAppType.BootManager) && Info.IsBootloaderSecure;
            if (Switch)
            {
                SwitchToBootManagerContext();
            }*/

            byte[] Request = new byte[0x04];
            const string Header = GetGPTSignature;

            System.Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);

            byte[]? Buffer = ExecuteRawMethod(Request);
            if ((Buffer == null) || (Buffer.Length < 0x4408))
            {
                throw new InvalidOperationException("Unable to read GPT!");
            }

            ushort Error = (ushort)((Buffer[6] << 8) + Buffer[7]);
            if (Error > 0)
            {
                throw new NotSupportedException($"ReadGPT: Error 0x{Error:X4}");
            }

            // Length: 0x4400 for 512 (0x200) Sector Size (from sector 0 to sector 34)
            // Length: 0x6000 for 4096 (0x1000) Sector Size (from sector 0 to sector 6)

            uint ReturnedGPTBufferLength = (uint)Buffer.Length - 8;
            uint SectorSize = Buffer.Length == 0x4408
                ? 512
                : Buffer.Length == 0x6008
                    ? (uint)4096
                    : throw new NotSupportedException($"ReadGPT: Unsupported output size! 0x{ReturnedGPTBufferLength:X4}");

            byte[] GPTBuffer = new byte[ReturnedGPTBufferLength - SectorSize];
            System.Buffer.BlockCopy(Buffer, 8 + (int)SectorSize, GPTBuffer, 0, (int)ReturnedGPTBufferLength - (int)SectorSize);

            /*if (Switch)
            {
                if (OriginalAppType == FlashAppType.FlashApp)
                {
                    SwitchToFlashAppContext();
                }
                else
                {
                    SwitchToPhoneInfoAppContext();
                }
            }*/

            return new GPT(GPTBuffer);//, SectorSize);  // NOKT message header and MBR are ignored
        }

        private static void ThrowFlashError(int ErrorCode)
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

            WPinternalsException Ex = new("Flash failed!")
            {
                SubMessage = $"Error 0x{ErrorCode:X4}: {SubMessage}"
            };

            throw Ex;
        }

        public void SendFfuHeaderV1(byte[] FfuHeader, int Progress = 0, byte Options = 0)
        {
            byte[] Request = new byte[FfuHeader.Length + 0x20];

            const string Header = SecureFlashSignature;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BigEndian.GetBytes(0x0001, 2), 0, Request, 0x06, 2); // Protocol version = 0x0001
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
            Request[0x0B] = 1; // Subblock count = 1
            Buffer.BlockCopy(BigEndian.GetBytes(0x0000000B, 4), 0, Request, 0x0C, 4); // Subblock type for header = 0x0B
            Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length + 0x0C, 4), 0, Request, 0x10, 4); // Subblock length = length of header + 0x0C
            Buffer.BlockCopy(BigEndian.GetBytes(0x00000000, 4), 0, Request, 0x14, 4); // Header type = 0
            Buffer.BlockCopy(BigEndian.GetBytes(FfuHeader.Length, 4), 0, Request, 0x18, 4); // Payload length = length of header
            Request[0x1C] = Options; // Header options = 0

            Buffer.BlockCopy(FfuHeader, 0, Request, 0x20, FfuHeader.Length);

            byte[] Response = ExecuteRawMethod(Request) ?? throw new BadConnectionException();

            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }
        }

        public void SendFfuHeaderV2(uint TotalHeaderLength, uint OffsetForThisPart, byte[] FfuHeader, int Progress = 0, byte Options = 0)
        {
            byte[] Request = new byte[FfuHeader.Length + 0x3C];

            const string Header = SecureFlashSignature;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BigEndian.GetBytes(0x0002, 2), 0, Request, 0x06, 2); // Protocol version = 0x0002
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
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

            byte[] Response = ExecuteRawMethod(Request) ?? throw new BadConnectionException();

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
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolSyncV1, 2), 0, Request, 0x06, 2); // Protocol version = 0x0001
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
            Request[0x0B] = 1; // Subblock count = 1

            Buffer.BlockCopy(BigEndian.GetBytes(0x0000000C, 4), 0, Request, 0x0C, 4); // Subblock type for ChunkData = 0x0C
            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length + 0x08, 4), 0, Request, 0x10, 4); // Subblock length = length of chunk + 0x08

            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length, 4), 0, Request, 0x14, 4); // Payload length = length of chunk
            Request[0x18] = Options; // Data options = 0 (1 = verify)

            Buffer.BlockCopy(FfuChunk, 0, Request, 0x1C, FfuChunk.Length);

            byte[] Response = ExecuteRawMethod(Request) ?? throw new BadConnectionException();

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
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BigEndian.GetBytes((int)FfuProtocol.ProtocolSyncV2, 2), 0, Request, 0x06, 2); // Protocol
            Request[0x08] = (byte)Progress; // Progress = 0% (0 - 100)
            Request[0x0B] = 1; // Subblock count = 1

            Buffer.BlockCopy(BigEndian.GetBytes(0x0000001B, 4), 0, Request, 0x0C, 4); // Subblock type for Payload v2 = 0x1B
            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length + 0x0C, 4), 0, Request, 0x10, 4); // Subblock length = length of chunk + 0x08

            Buffer.BlockCopy(BigEndian.GetBytes(FfuChunk.Length, 4), 0, Request, 0x14, 4); // Payload length = length of chunk
            Request[0x18] = Options; // Data options = 0 (1 = verify)

            Buffer.BlockCopy(FfuChunk, 0, Request, 0x20, FfuChunk.Length);

            byte[] Response = ExecuteRawMethod(Request) ?? throw new BadConnectionException();

            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }
        }

        public void SendFfuPayloadV3(byte[] FfuChunk, uint WriteDescriptorIndex, uint CRC, int Progress = 0, byte Options = 0)
        {
            byte[] Request = new byte[FfuChunk.Length + 0x20];

            const string Header = SecureFlashSignature;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
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

            byte[] Response = ExecuteRawMethod(Request) ?? throw new BadConnectionException();

            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }
        }

        public PhoneInfo ReadPhoneInfo()
        {
            // NOKV = Info Query

            bool PhoneInfoLogged = Info.State != PhoneInfoState.Empty;
            PhoneInfo Result = Info;

            if (Result.State == PhoneInfoState.Empty)
            {
                byte[] Request = new byte[4];
                ByteOperations.WriteAsciiString(Request, 0, InfoQuerySignature);
                byte[]? Response = ExecuteRawMethod(Request);
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
                        ushort SubblockLength = BigEndian.ToUInt16(Response, SubblockOffset + 0x01);
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
                                if (Result.App == FlashAppType.FlashApp)
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
                            case 0x0F: // Supported but check parsing below pls
                                SubblockVersion = Response[SubblockPayloadOffset]; // 0x03
                                Result.PlatformSecureBootEnabled = Response[SubblockPayloadOffset + 0x01] == 0x01;
                                Result.SecureFfuEnabled = Response[SubblockPayloadOffset + 0x02] == 0x01;
                                Result.JtagDisabled = Response[SubblockPayloadOffset + 0x03] == 0x01;
                                Result.RdcPresent = Response[SubblockPayloadOffset + 0x04] == 0x01;
                                Result.Authenticated = (Response[SubblockPayloadOffset + 0x05] == 0x01) || (Response[SubblockPayloadOffset + 0x05] == 0x02);
                                Result.UefiSecureBootEnabled = Response[SubblockPayloadOffset + 0x06] == 0x01;
                                Result.SecondaryHardwareKeyPresent = Response[SubblockPayloadOffset + 0x07] == 0x01;
                                break;
                            case 0x10: // Also check to be sure
                                SubblockVersion = Response[SubblockPayloadOffset]; // 0x01
                                Result.SecureFfuSupportedProtocolMask = BigEndian.ToUInt16(Response, SubblockPayloadOffset + 0x01);
                                break;
                            case 0x1F: // Recheck too
                                Result.MmosOverUsbSupported = Response[SubblockPayloadOffset] == 1;
                                break;
                            case 0x20:
                                // CRC header info
                                break;
                            case 0x22:
                                uint SectorCount = BigEndian.ToUInt32(Response, SubblockPayloadOffset);
                                uint SectorSize = BigEndian.ToUInt32(Response, SubblockPayloadOffset + 4);
                                ushort FlashType = BigEndian.ToUInt16(Response, SubblockPayloadOffset + 8);
                                ushort FlashTypeIndex = BigEndian.ToUInt16(Response, SubblockPayloadOffset + 10);
                                uint Unknown = BigEndian.ToUInt32(Response, SubblockPayloadOffset + 12);
                                string DevicePath = ByteOperations.ReadUnicodeString(Response, (uint)SubblockPayloadOffset + 16, (uint)SubblockLength - 16).Trim([' ', '\0']);
                                Result.BootDevices.Add((SectorCount, SectorSize, FlashType, FlashTypeIndex, Unknown, DevicePath));
                                break;
                            case 0x23:
                                byte[] Bytes = Response[SubblockPayloadOffset..(SubblockPayloadOffset + SubblockLength)];

                                ushort ManufacturerLength = BitConverter.ToUInt16(Bytes[0..2].Reverse().ToArray());
                                ushort FamilyLength = BitConverter.ToUInt16(Bytes[2..4].Reverse().ToArray());
                                ushort ProductNameLength = BitConverter.ToUInt16(Bytes[4..6].Reverse().ToArray());
                                ushort ProductVersionLength = BitConverter.ToUInt16(Bytes[6..8].Reverse().ToArray());
                                ushort SKUNumberLength = BitConverter.ToUInt16(Bytes[8..10].Reverse().ToArray());
                                ushort BaseboardManufacturerLength = BitConverter.ToUInt16(Bytes[10..12].Reverse().ToArray());
                                ushort BaseboardProductLength = BitConverter.ToUInt16(Bytes[12..14].Reverse().ToArray());

                                int CurrentOffset = 14;
                                Result.Manufacturer = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + ManufacturerLength)]);
                                CurrentOffset += ManufacturerLength;
                                Result.Family = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + FamilyLength)]);
                                CurrentOffset += FamilyLength;
                                Result.ProductName = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + ProductNameLength)]);
                                CurrentOffset += ProductNameLength;
                                Result.ProductVersion = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + ProductVersionLength)]);
                                CurrentOffset += ProductVersionLength;
                                Result.SKUNumber = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + SKUNumberLength)]);
                                CurrentOffset += SKUNumberLength;
                                Result.BaseboardManufacturer = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + BaseboardManufacturerLength)]);
                                CurrentOffset += BaseboardManufacturerLength;
                                Result.BaseboardProduct = Encoding.ASCII.GetString(Bytes[CurrentOffset..(CurrentOffset + BaseboardProductLength)]);
                                break;
                            case 0x24:
                                Result.LargestMemoryRegion = BitConverter.ToUInt64(Response[SubblockPayloadOffset..(SubblockPayloadOffset + 8)].Reverse().ToArray());
                                break;
                            case 0x25:
                                Result.AppType = (AppType)Response[SubblockPayloadOffset];
                                break;
                            default:
                                Debug.WriteLine($"Unknown Subblock: ID: 0x{SubblockID:X2} Length: 0x{SubblockLength:X4}");
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
                Result.Log();
            }

            return Result;
        }

        public enum FlashAppType
        {
            FlashApp = 2
        };

        public enum PhoneInfoState
        {
            Empty,
            Basic
        };

        public class PhoneInfo
        {
            public PhoneInfoState State = PhoneInfoState.Empty;

            public FlashAppType App;

            public byte FlashAppVersionMajor;
            public byte FlashAppVersionMinor;
            public byte FlashAppProtocolVersionMajor;
            public byte FlashAppProtocolVersionMinor;

            public uint TransferSize;
            public bool MmosOverUsbSupported;
            public uint SdCardSizeInSectors;
            public uint WriteBufferSize;
            public uint EmmcSizeInSectors;
            public string? PlatformID;
            public ushort SecureFfuSupportedProtocolMask;
            public bool AsyncSupport;

            public bool PlatformSecureBootEnabled;
            public bool SecureFfuEnabled;
            public bool JtagDisabled;
            public bool RdcPresent;
            public bool Authenticated;
            public bool UefiSecureBootEnabled;
            public bool SecondaryHardwareKeyPresent;

            public string? Manufacturer;
            public string? Family;
            public string? ProductName;
            public string? ProductVersion;
            public string? SKUNumber;
            public string? BaseboardManufacturer;
            public string? BaseboardProduct;
            public ulong LargestMemoryRegion;
            public AppType AppType;
            public List<(uint SectorCount, uint SectorSize, ushort FlashType, ushort FlashIndex, uint Unknown, string DevicePath)> BootDevices = [];

            public bool IsBootloaderSecure;

            public void Log()
            {
                switch (App)
                {
                    case FlashAppType.FlashApp:
                        Debug.WriteLine($"Flash app: {FlashAppVersionMajor}.{FlashAppVersionMinor}");
                        Debug.WriteLine($"Flash protocol: {FlashAppProtocolVersionMajor}.{FlashAppProtocolVersionMinor}");
                        break;
                }

                Debug.WriteLine($"SecureBoot: {((!PlatformSecureBootEnabled || !UefiSecureBootEnabled) ? "Disabled" : "Enabled")} (Platform Secure Boot: {(PlatformSecureBootEnabled ? "Enabled" : "Disabled")}, UEFI Secure Boot: {(UefiSecureBootEnabled ? "Enabled" : "Disabled")})");

                Debug.WriteLine($"Flash app security: {(!IsBootloaderSecure ? "Disabled" : "Enabled")}");

                Debug.WriteLine($"Flash app security: {(!IsBootloaderSecure ? "Disabled" : "Enabled")} (FFU security: {(SecureFfuEnabled ? "Enabled" : "Disabled")}, RDC: {(RdcPresent ? "Present" : "Not found")}, Authenticated: {(Authenticated ? "True" : "False")})");

                Debug.WriteLine($"JTAG: {(JtagDisabled ? "Disabled" : "Enabled")}");
            }
        }

        public void Shutdown()
        {
            byte[] Request = new byte[4];
            const string Header = ShutdownSignature;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }
    }
}
