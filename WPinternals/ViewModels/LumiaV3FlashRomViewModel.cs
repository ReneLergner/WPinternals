using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WPinternals
{
    internal class LumiaV3FlashRomViewModel : ContextViewModel
    {
        private static void RoundUpToChunks(Stream stream, UInt32 chunkSize)
        {
            Int64 Size = stream.Length;
            if ((Size % chunkSize) > 0)
            {
                Int64 padding = (UInt32)(((Size / chunkSize) + 1) * chunkSize) - Size;
                stream.Write(new byte[padding], 0, (Int32)padding);
            }
        }

        internal class ImageHeader
        {
            public UInt32 Size = 24;
            public string Signature = "ImageFlash  ";
            public UInt32 ManifestLength;
            public UInt32 ChunkSize = 128;
        }

        public class StoreHeader
        {
            public UInt32 UpdateType = 0; // Full update

            public UInt16 MajorVersion = 1;
            public UInt16 MinorVersion = 0;
            public UInt16 FullFlashMajorVersion = 2;
            public UInt16 FullFlashMinorVersion = 0;

            // Size is 0xC0
            public string PlatformId;

            public UInt32 BlockSizeInBytes = 131072;
            public UInt32 WriteDescriptorCount;
            public UInt32 WriteDescriptorLength;
            public UInt32 ValidateDescriptorCount = 0;
            public UInt32 ValidateDescriptorLength = 0;
            public UInt32 InitialTableIndex = 0;
            public UInt32 InitialTableCount = 0;
            public UInt32 FlashOnlyTableIndex = 0; // Should be the index of the critical partitions, but for now we don't implement that
            public UInt32 FlashOnlyTableCount = 1;
            public UInt32 FinalTableIndex; //= WriteDescriptorCount - FinalTableCount;
            public UInt32 FinalTableCount = 0;
        }

        public class SecurityHeader
        {
            public UInt32 Size = 32;
            public string Signature = "SignedImage ";
            public UInt32 ChunkSizeInKb = 128;
            public UInt32 HashAlgorithm = 32780; // SHA256 algorithm id
            public UInt32 CatalogSize;
            public UInt32 HashTableSize;
        }

        internal static class ManifestIni
        {
            internal static string BuildUpManifest(FullFlash fullFlash, Store store)
            {
                string Manifest = "[FullFlash]\r\n";
                if (!string.IsNullOrEmpty(fullFlash.AntiTheftVersion))
                {
                    Manifest += "AntiTheftVersion  = " + fullFlash.AntiTheftVersion + "\r\n";
                }

                if (!string.IsNullOrEmpty(fullFlash.OSVersion))
                {
                    Manifest += "OSVersion         = " + fullFlash.OSVersion + "\r\n";
                }

                if (!string.IsNullOrEmpty(fullFlash.Description))
                {
                    Manifest += "Description       = " + fullFlash.Description + "\r\n";
                }

                if (!string.IsNullOrEmpty(fullFlash.Version))
                {
                    Manifest += "Version           = " + fullFlash.Version + "\r\n";
                }

                if (!string.IsNullOrEmpty(fullFlash.DevicePlatformId0))
                {
                    Manifest += "DevicePlatformId0 = " + fullFlash.DevicePlatformId0 + "\r\n";
                }

                Manifest += "\r\n[Store]\r\n";
                Manifest += "SectorSize     = " + store.SectorSize + "\r\n";
                Manifest += "MinSectorCount = " + store.MinSectorCount + "\r\n";
                Manifest += "\r\n";

                return Manifest;
            }
        }

        internal class FullFlash
        {
            public string AntiTheftVersion = "1.1"; // Allow flashing on all devices
            public string OSVersion;
            public string Description = "Update on: " + DateTime.Now.ToString("u") + "::\r\n";
            public string Version = "2.0";
            public string DevicePlatformId0;
        }

        internal class Store
        {
            public UInt32 SectorSize;
            public UInt32 MinSectorCount;
        }

        // V3 exploit
        // Magic
        //
        private static byte[] GenerateCatalogFile(byte[] hashData)
        {
            byte[] catalog_first_part = [0x30, 0x82, 0x01, 0x44, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x07, 0x02, 0xA0, 0x82, 0x01, 0x35, 0x30, 0x82, 0x01, 0x31, 0x02, 0x01, 0x01, 0x31, 0x00, 0x30, 0x82, 0x01, 0x26, 0x06, 0x09, 0x2B, 0x06, 0x01, 0x04, 0x01, 0x82, 0x37, 0x0A, 0x01, 0xA0, 0x82, 0x01, 0x17, 0x30, 0x82, 0x01, 0x13, 0x30, 0x0C, 0x06, 0x0A, 0x2B, 0x06, 0x01, 0x04, 0x01, 0x82, 0x37, 0x0C, 0x01, 0x01, 0x04, 0x10, 0xA8, 0xCA, 0xD9, 0x7D, 0xBF, 0x6D, 0x67, 0x4D, 0xB1, 0x4D, 0x62, 0xFB, 0xE6, 0x26, 0x22, 0xD4, 0x17, 0x0D, 0x32, 0x30, 0x30, 0x31, 0x31, 0x30, 0x31, 0x32, 0x31, 0x32, 0x32, 0x37, 0x5A, 0x30, 0x0E, 0x06, 0x0A, 0x2B, 0x06, 0x01, 0x04, 0x01, 0x82, 0x37, 0x0C, 0x01, 0x02, 0x05, 0x00, 0x30, 0x81, 0xD1, 0x30, 0x81, 0xCE, 0x04, 0x1E, 0x48, 0x00, 0x61, 0x00, 0x73, 0x00, 0x68, 0x00, 0x54, 0x00, 0x61, 0x00, 0x62, 0x00, 0x6C, 0x00, 0x65, 0x00, 0x2E, 0x00, 0x62, 0x00, 0x6C, 0x00, 0x6F, 0x00, 0x62, 0x00, 0x00, 0x00, 0x31, 0x81, 0xAB, 0x30, 0x45, 0x06, 0x0A, 0x2B, 0x06, 0x01, 0x04, 0x01, 0x82, 0x37, 0x02, 0x01, 0x04, 0x31, 0x37, 0x30, 0x35, 0x30, 0x10, 0x06, 0x0A, 0x2B, 0x06, 0x01, 0x04, 0x01, 0x82, 0x37, 0x02, 0x01, 0x19, 0xA2, 0x02, 0x80, 0x00, 0x30, 0x21, 0x30, 0x09, 0x06, 0x05, 0x2B, 0x0E, 0x03, 0x02, 0x1A, 0x05, 0x00, 0x04, 0x14];
            byte[] catalog_second_part = [0x30, 0x62, 0x06, 0x0A, 0x2B, 0x06, 0x01, 0x04, 0x01, 0x82, 0x37, 0x0C, 0x02, 0x02, 0x31, 0x54, 0x30, 0x52, 0x1E, 0x4C, 0x00, 0x7B, 0x00, 0x44, 0x00, 0x45, 0x00, 0x33, 0x00, 0x35, 0x00, 0x31, 0x00, 0x41, 0x00, 0x34, 0x00, 0x32, 0x00, 0x2D, 0x00, 0x38, 0x00, 0x45, 0x00, 0x35, 0x00, 0x39, 0x00, 0x2D, 0x00, 0x31, 0x00, 0x31, 0x00, 0x44, 0x00, 0x30, 0x00, 0x2D, 0x00, 0x38, 0x00, 0x43, 0x00, 0x34, 0x00, 0x37, 0x00, 0x2D, 0x00, 0x30, 0x00, 0x30, 0x00, 0x43, 0x00, 0x30, 0x00, 0x34, 0x00, 0x46, 0x00, 0x43, 0x00, 0x32, 0x00, 0x39, 0x00, 0x35, 0x00, 0x45, 0x00, 0x45, 0x00, 0x7D, 0x02, 0x02, 0x02, 0x00, 0x31, 0x00];

            byte[] hash = new SHA1Managed().ComputeHash(hashData);

            byte[] catalog = new byte[catalog_first_part.Length + hash.Length + catalog_second_part.Length];
            Buffer.BlockCopy(catalog_first_part, 0, catalog, 0, catalog_first_part.Length);
            Buffer.BlockCopy(hash, 0, catalog, catalog_first_part.Length, hash.Length);
            Buffer.BlockCopy(catalog_second_part, 0, catalog, catalog_first_part.Length + hash.Length, catalog_second_part.Length);

            return catalog;
        }

        // V3 exploit
        // Magic
        //
        internal async static Task LumiaV3CustomFlash(PhoneNotifierViewModel Notifier, List<FlashPart> FlashParts, bool CheckSectorAlignment = true, SetWorkingStatus SetWorkingStatus = null, UpdateWorkingStatus UpdateWorkingStatus = null, ExitSuccess ExitSuccess = null, ExitFailure ExitFailure = null)
        {
            if (SetWorkingStatus == null)
            {
                SetWorkingStatus = (m, s, v, a, st) => { };
            }

            if (UpdateWorkingStatus == null)
            {
                UpdateWorkingStatus = (m, s, v, st) => { };
            }

            if (ExitSuccess == null)
            {
                ExitSuccess = (m, s) => { };
            }

            if (ExitFailure == null)
            {
                ExitFailure = (m, s) => { };
            }

            const uint chunkSize = 131072u;
            const int chunkSizes = 131072;

            if (FlashParts != null)
            {
                foreach (FlashPart Part in FlashParts)
                {
                    if (Part.Stream == null)
                    {
                        throw new ArgumentException("Stream is null");
                    }

                    if (!Part.Stream.CanSeek)
                    {
                        throw new ArgumentException("Streams must be seekable");
                    }

                    if ((Part.StartSector * 0x200 % chunkSize) != 0)
                    {
                        throw new ArgumentException("Invalid StartSector alignment");
                    }

                    if (CheckSectorAlignment && (Part.Stream.Length % chunkSize) != 0)
                    {
                        throw new ArgumentException("Invalid Data length");
                    }
                }
            }

            try
            {
                LumiaFlashAppModel Model = (LumiaFlashAppModel)Notifier.CurrentModel;
                LumiaFlashAppPhoneInfo Info = Model.ReadPhoneInfo();

                if ((Info.SecureFfuSupportedProtocolMask & ((ushort)FfuProtocol.ProtocolSyncV2)) == 0) // Exploit needs protocol v2 -> This check is not conclusive, because old phones also report support for this protocol, although it is really not supported.
                {
                    throw new WPinternalsException("Flash failed!", "Protocols not supported. The phone reports that it does not support the Protocol Sync V2.");
                }

                if (Info.FlashAppProtocolVersionMajor < 2) // Old phones do not support the hack. These phones have Flash protocol 1.x.
                {
                    throw new WPinternalsException("Flash failed!", "Protocols not supported. The phone reports that Flash App communication protocol is lower than 2. Reported version by the phone: " + Info.FlashAppProtocolVersionMajor + ".");
                }

                if (Info.UefiSecureBootEnabled)
                {
                    throw new WPinternalsException("Flash failed!", "UEFI Secureboot must be disabled for the Flash V3 exploit to work.");
                }

                // The payloads must be ordered by the number of locations
                //
                // FlashApp processes payloads like this:
                // - First payloads which are with one location, those can be sent in bulk
                // - Then payloads with more than one location, those should not be sent in bulk
                //
                // If you do not order payloads like this, you will get an error, most likely hash mismatch
                //
                LumiaV2UnlockBootViewModel.FlashingPayload[] payloads = [];
                if (FlashParts != null)
                {
                    payloads =
                    [
                        .. LumiaV2UnlockBootViewModel.GetNonOptimizedPayloads(FlashParts, chunkSizes, Info.WriteBufferSize / chunkSize, SetWorkingStatus, UpdateWorkingStatus).OrderBy(x => x.TargetLocations.Length),
                    ];
                }

                MemoryStream Headerstream1 = new();

                // ==============================
                // Header 1 start

                ImageHeader image = new();
                FullFlash ffimage = new();
                Store simage = new();

                // Todo make this read the image itself
                ffimage.OSVersion = "10.0.11111.0";
                ffimage.DevicePlatformId0 = Info.PlatformID;
                ffimage.AntiTheftVersion = "1.1";

                simage.SectorSize = 512;
                simage.MinSectorCount = Info.EmmcSizeInSectors;

                //Logging.Log("Generating image manifest...");
                string manifest = ManifestIni.BuildUpManifest(ffimage, simage);//, partitions);

                byte[] TextBytes = Encoding.ASCII.GetBytes(manifest);

                image.ManifestLength = (UInt32)TextBytes.Length;

                byte[] ImageHeaderBuffer = new byte[0x18];

                ByteOperations.WriteUInt32(ImageHeaderBuffer, 0, image.Size);
                ByteOperations.WriteAsciiString(ImageHeaderBuffer, 0x04, image.Signature);
                ByteOperations.WriteUInt32(ImageHeaderBuffer, 0x10, image.ManifestLength);
                ByteOperations.WriteUInt32(ImageHeaderBuffer, 0x14, image.ChunkSize);

                Headerstream1.Write(ImageHeaderBuffer, 0, 0x18);
                Headerstream1.Write(TextBytes, 0, TextBytes.Length);

                RoundUpToChunks(Headerstream1, chunkSize);

                // Header 1 stop + round
                // ==============================

                MemoryStream Headerstream2 = new();

                // ==============================
                // Header 2 start

                StoreHeader store = new();

                store.WriteDescriptorCount = (UInt32)payloads.Length;
                store.FinalTableIndex = (UInt32)payloads.Length - store.FinalTableCount;
                store.PlatformId = Info.PlatformID;

                foreach (LumiaV2UnlockBootViewModel.FlashingPayload payload in payloads)
                {
                    store.WriteDescriptorLength += payload.GetStoreHeaderSize();
                }

                byte[] GPTChunk = Model.GetGptChunk(0x20000);
                GPT GPT = new(GPTChunk);
                UInt64 PlatEnd = 0;
                if (GPT.Partitions.Any(x => x.Name == "PLAT"))
                {
                    PlatEnd = GPT.GetPartition("PLAT").LastSector;
                }

                foreach (LumiaV2UnlockBootViewModel.FlashingPayload payload in payloads)
                {
                    if (payload.TargetLocations[0] > PlatEnd)
                    {
                        break;
                    }

                    store.FlashOnlyTableIndex++;
                }

                byte[] StoreHeaderBuffer = new byte[0xF8];
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0, store.UpdateType);
                ByteOperations.WriteUInt16(StoreHeaderBuffer, 0x04, store.MajorVersion);
                ByteOperations.WriteUInt16(StoreHeaderBuffer, 0x06, store.MinorVersion);
                ByteOperations.WriteUInt16(StoreHeaderBuffer, 0x08, store.FullFlashMajorVersion);
                ByteOperations.WriteUInt16(StoreHeaderBuffer, 0x0A, store.FullFlashMinorVersion);
                ByteOperations.WriteAsciiString(StoreHeaderBuffer, 0x0C, store.PlatformId);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xCC, store.BlockSizeInBytes);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xD0, store.WriteDescriptorCount);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xD4, store.WriteDescriptorLength);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xD8, store.ValidateDescriptorCount);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xDC, store.ValidateDescriptorLength);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xE0, store.InitialTableIndex);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xE4, store.InitialTableCount);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xE8, store.FlashOnlyTableIndex);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xEC, store.FlashOnlyTableCount);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xF0, store.FinalTableIndex);
                ByteOperations.WriteUInt32(StoreHeaderBuffer, 0xF4, store.FinalTableCount);
                Headerstream2.Write(StoreHeaderBuffer, 0, 0xF8);

                byte[] descriptorsBuffer = new byte[store.WriteDescriptorLength];

                UInt32 NewWriteDescriptorOffset = 0;
                foreach (LumiaV2UnlockBootViewModel.FlashingPayload payload in payloads)
                {
                    ByteOperations.WriteUInt32(descriptorsBuffer, NewWriteDescriptorOffset + 0x00, (UInt32)payload.TargetLocations.Length); // Location count
                    ByteOperations.WriteUInt32(descriptorsBuffer, NewWriteDescriptorOffset + 0x04, payload.ChunkCount);                      // Chunk count
                    NewWriteDescriptorOffset += 0x08;

                    foreach (UInt32 location in payload.TargetLocations)
                    {
                        ByteOperations.WriteUInt32(descriptorsBuffer, NewWriteDescriptorOffset + 0x00, 0x00000000);                          // Disk access method (0 = Begin, 2 = End)
                        ByteOperations.WriteUInt32(descriptorsBuffer, NewWriteDescriptorOffset + 0x04, location);                            // Chunk index
                        NewWriteDescriptorOffset += 0x08;
                    }
                }

                Headerstream2.Write(descriptorsBuffer, 0, (Int32)store.WriteDescriptorLength);

                RoundUpToChunks(Headerstream2, chunkSize);

                // Header 2 stop + round
                // ==============================

                SecurityHeader security = new();

                Headerstream1.Seek(0, SeekOrigin.Begin);
                Headerstream2.Seek(0, SeekOrigin.Begin);

                security.HashTableSize = 0x20 * (UInt32)((Headerstream1.Length + Headerstream2.Length) / chunkSize);

                foreach (LumiaV2UnlockBootViewModel.FlashingPayload payload in payloads)
                {
                    security.HashTableSize += payload.GetSecurityHeaderSize();
                }

                byte[] HashTable = new byte[security.HashTableSize];
                BinaryWriter bw = new(new MemoryStream(HashTable));

                SHA256 crypto = SHA256.Create();
                for (Int32 i = 0; i < Headerstream1.Length / chunkSize; i++)
                {
                    byte[] buffer = new byte[chunkSize];
                    Headerstream1.Read(buffer, 0, (Int32)chunkSize);
                    byte[] hash = crypto.ComputeHash(buffer);
                    bw.Write(hash, 0, hash.Length);
                }

                for (Int32 i = 0; i < Headerstream2.Length / chunkSize; i++)
                {
                    byte[] buffer = new byte[chunkSize];
                    Headerstream2.Read(buffer, 0, (Int32)chunkSize);
                    byte[] hash = crypto.ComputeHash(buffer);
                    bw.Write(hash, 0, hash.Length);
                }

                foreach (LumiaV2UnlockBootViewModel.FlashingPayload payload in payloads)
                {
                    bw.Write(payload.ChunkHashes[0], 0, payload.ChunkHashes[0].Length);
                }

                bw.Close();

                //Logging.Log("Generating image catalog...");
                byte[] catalog = GenerateCatalogFile(HashTable);

                security.CatalogSize = (UInt32)catalog.Length;

                byte[] SecurityHeaderBuffer = new byte[0x20];

                ByteOperations.WriteUInt32(SecurityHeaderBuffer, 0, security.Size);
                ByteOperations.WriteAsciiString(SecurityHeaderBuffer, 0x04, security.Signature);
                ByteOperations.WriteUInt32(SecurityHeaderBuffer, 0x10, security.ChunkSizeInKb);
                ByteOperations.WriteUInt32(SecurityHeaderBuffer, 0x14, security.HashAlgorithm);
                ByteOperations.WriteUInt32(SecurityHeaderBuffer, 0x18, security.CatalogSize);
                ByteOperations.WriteUInt32(SecurityHeaderBuffer, 0x1C, security.HashTableSize);

                MemoryStream retstream = new();

                retstream.Write(SecurityHeaderBuffer, 0, 0x20);

                retstream.Write(catalog, 0, (Int32)security.CatalogSize);
                retstream.Write(HashTable, 0, (Int32)security.HashTableSize);

                RoundUpToChunks(retstream, chunkSize);

                Headerstream1.Seek(0, SeekOrigin.Begin);
                Headerstream2.Seek(0, SeekOrigin.Begin);

                byte[] buff = new byte[Headerstream1.Length];
                Headerstream1.Read(buff, 0, (Int32)Headerstream1.Length);

                Headerstream1.Close();

                retstream.Write(buff, 0, buff.Length);

                buff = new byte[Headerstream2.Length];
                Headerstream2.Read(buff, 0, (Int32)Headerstream2.Length);

                Headerstream2.Close();

                retstream.Write(buff, 0, buff.Length);

                // --------

                // Go back to the beginning
                retstream.Seek(0, SeekOrigin.Begin);

                byte[] FfuHeader = new byte[retstream.Length];
                await retstream.ReadAsync(FfuHeader, 0, (Int32)retstream.Length);
                retstream.Close();

                Byte Options = 0;
                if (!Info.IsBootloaderSecure)
                {
                    Options = (Byte)((FlashOptions)Options | FlashOptions.SkipSignatureCheck);
                }

                LogFile.Log("Flash in progress...", LogType.ConsoleOnly);
                SetWorkingStatus("Flashing...", null, (UInt64?)payloads.Length, Status: WPinternalsStatus.Flashing);

                Model.SendFfuHeaderV1(FfuHeader, Options);

                UInt64 counter = 0;
                Int32 numberOfPayloadsToSendAtOnce = 1;
                if ((Info.SecureFfuSupportedProtocolMask & (UInt16)FfuProtocol.ProtocolSyncV2) != 0)
                {
                    numberOfPayloadsToSendAtOnce = (Int32)Math.Round((Double)Info.WriteBufferSize / chunkSize);
                }

                byte[] payloadBuffer;

                for (Int32 i = 0; i < payloads.Length; i+= numberOfPayloadsToSendAtOnce)
                {
                    if (i + numberOfPayloadsToSendAtOnce - 1 >= payloads.Length)
                    {
                        numberOfPayloadsToSendAtOnce = payloads.Length - i;
                    }

                    payloadBuffer = new byte[numberOfPayloadsToSendAtOnce * chunkSizes];

                    string ProgressText = "Flashing resources";

                    for (Int32 j = 0; j < numberOfPayloadsToSendAtOnce; j++)
                    {
                        LumiaV2UnlockBootViewModel.FlashingPayload payload = payloads[i + j];

                        UInt32 StreamIndex = payload.StreamIndexes[0];
                        FlashPart flashPart = FlashParts[(Int32)StreamIndex];

                        if (flashPart.ProgressText != null)
                        {
                            ProgressText = flashPart.ProgressText;
                        }

                        Stream Stream = flashPart.Stream;
                        Stream.Seek(payload.StreamLocations[0], SeekOrigin.Begin);
                        Stream.Read(payloadBuffer, j * chunkSizes, chunkSizes);
                        counter++;
                    }

                    if ((Info.SecureFfuSupportedProtocolMask & (ushort)FfuProtocol.ProtocolSyncV2) != 0)
                    {
                        Model.SendFfuPayloadV2(payloadBuffer, Int32.Parse((counter * 100 / (UInt64)payloads.Length).ToString()));
                    }
                    else
                    {
                        Model.SendFfuPayloadV1(payloadBuffer, Int32.Parse((counter * 100 / (UInt64)payloads.Length).ToString()));
                    }

                    UpdateWorkingStatus(ProgressText, null, counter, WPinternalsStatus.Flashing);
                }

                Model.ResetPhone();

                await Notifier.WaitForRemoval();

                ExitSuccess("Flash succeeded!", null);
            }
            catch
            {
                throw new WPinternalsException("Custom flash failed");
            }
        }
    }
}
