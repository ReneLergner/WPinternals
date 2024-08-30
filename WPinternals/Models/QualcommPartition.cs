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
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace WPinternals
{
    internal enum QualcommPartitionHeaderType
    {
        Long,
        Short
    };

    internal class QualcommPartition
    {
        internal byte[] Binary;
        internal uint HeaderOffset;
        internal QualcommPartitionHeaderType HeaderType;
        internal uint ImageOffset;
        internal uint ImageAddress;
        internal uint ImageSize;
        internal uint CodeSize;
        internal uint SignatureAddress;
        internal uint SignatureSize;
        internal uint SignatureOffset;
        internal uint CertificatesAddress;
        internal uint CertificatesSize;
        internal uint CertificatesOffset;
        internal byte[] RootKeyHash = null;

        internal QualcommPartition(string Path) : this(File.ReadAllBytes(Path)) { }

        internal QualcommPartition(byte[] Binary, uint Offset = 0)
        {
#if DEBUG
            System.Diagnostics.Debug.Print("Loader: " + Converter.ConvertHexToString(SHA256.HashData(Binary.AsSpan(0, Binary.Length)), ""));
#endif

            this.Binary = Binary;

            byte[] LongHeaderPattern = [0xD1, 0xDC, 0x4B, 0x84, 0x34, 0x10, 0xD7, 0x73, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
            byte[] LongHeaderMask = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

            if (ByteOperations.FindPattern(Binary, Offset, 4, [0x7F, 0x45, 0x4C, 0x46], [0x00, 0x00, 0x00, 0x00], null) == 0)
            {
                // This is an ELF image
                // First program header is a reference to the elf-header
                // Second program header is a reference to the signed hash-table
                HeaderType = QualcommPartitionHeaderType.Short;
                UInt32 ProgramHeaderOffset;
                UInt16 ProgramHeaderEntrySize;
                UInt32 HashTableProgramHeaderOffset;
                if (Binary[Offset + 0x04] == 1)
                {
                    // 32-bit elf image
                    ProgramHeaderOffset = Offset + ByteOperations.ReadUInt32(Binary, Offset + 0x1c);
                    ProgramHeaderEntrySize = ByteOperations.ReadUInt16(Binary, Offset + 0x2a);
                    HashTableProgramHeaderOffset = ProgramHeaderOffset + ProgramHeaderEntrySize;
                    ImageOffset = Offset + ByteOperations.ReadUInt32(Binary, HashTableProgramHeaderOffset + 0x04);
                    HeaderOffset = ImageOffset + 8;
                }
                else if (Binary[Offset + 0x04] == 2)
                {
                    // 64-bit elf image
                    ProgramHeaderOffset = Offset + ByteOperations.ReadUInt32(Binary, Offset + 0x20);
                    ProgramHeaderEntrySize = ByteOperations.ReadUInt16(Binary, Offset + 0x36);
                    HashTableProgramHeaderOffset = ProgramHeaderOffset + ProgramHeaderEntrySize;
                    ImageOffset = Offset + (UInt32)ByteOperations.ReadUInt64(Binary, HashTableProgramHeaderOffset + 0x08);
                    HeaderOffset = ImageOffset + 8;
                }
                else
                {
                    throw new WPinternalsException("Invalid programmer", "The type of elf image could not be determined from the provided programmer.");
                }
            }
            else if (ByteOperations.FindPattern(Binary, Offset, (uint)LongHeaderPattern.Length, LongHeaderPattern, LongHeaderMask, null) == null)
            {
                HeaderType = QualcommPartitionHeaderType.Short;
                ImageOffset = Offset;
                HeaderOffset = ImageOffset + 8;
            }
            else
            {
                HeaderType = QualcommPartitionHeaderType.Long;
                ImageOffset = Offset;
                HeaderOffset = ImageOffset + (uint)LongHeaderPattern.Length;
            }

            if (ByteOperations.ReadUInt32(Binary, HeaderOffset + 0X00) != 0)
            {
                ImageOffset = ByteOperations.ReadUInt32(Binary, HeaderOffset + 0X00);
            }
            else if (HeaderType == QualcommPartitionHeaderType.Short)
            {
                ImageOffset += 0x28;
            }
            else
            {
                ImageOffset += 0x50;
            }

            ImageAddress = ByteOperations.ReadUInt32(Binary, HeaderOffset + 0X04);
            ImageSize = ByteOperations.ReadUInt32(Binary, HeaderOffset + 0X08);
            CodeSize = ByteOperations.ReadUInt32(Binary, HeaderOffset + 0X0C);
            SignatureAddress = ByteOperations.ReadUInt32(Binary, HeaderOffset + 0X10);
            SignatureSize = ByteOperations.ReadUInt32(Binary, HeaderOffset + 0X14);
            SignatureOffset = SignatureAddress - ImageAddress + ImageOffset;
            CertificatesAddress = ByteOperations.ReadUInt32(Binary, HeaderOffset + 0X18);
            CertificatesSize = ByteOperations.ReadUInt32(Binary, HeaderOffset + 0X1C);
            CertificatesOffset = CertificatesAddress - ImageAddress + ImageOffset;

            using MemoryStream fileStream = new(Binary);
            using BinaryReader reader = new(fileStream);

            List<byte[]> Signatures = [];
            uint LastOffset = 0;

            for (uint i = 0; i < fileStream.Length - 6; i++)
            {
                fileStream.Seek(i, SeekOrigin.Begin);

                ushort offset0 = reader.ReadUInt16();
                short offset1 = (short)((reader.ReadByte() << 8) | reader.ReadByte());
                ushort offset2 = reader.ReadUInt16();

                if (offset0 == 0x8230 && offset1 >= 0 && offset2 == 0x8230)
                {
                    uint CertificateSize = (uint)offset1 + 4; // Header Size is 4

                    bool IsCertificatePartOfExistingChain = LastOffset == 0 || LastOffset == i;
                    if (!IsCertificatePartOfExistingChain)
                    {
                        break;
                    }

                    LastOffset = i + CertificateSize;

                    fileStream.Seek(i, SeekOrigin.Begin);
                    Signatures.Add(reader.ReadBytes((int)CertificateSize));
                }
            }

            if (Signatures.Count > 0)
            {
                byte[] RootCertificate = Signatures[^1];

                for (int i = 0; i < Signatures.Count; i++)
                {
                    if (i + 1 != Signatures.Count)
                    {
#if DEBUG
                        System.Diagnostics.Debug.Print("Cert: " + Converter.ConvertHexToString(SHA256.HashData(Signatures[i]), ""));
#endif
                    }
                    else
                    {
                        // This is the last certificate. So this is the root key.
                        RootKeyHash = SHA256.HashData(Signatures[i]);

#if DEBUG
                        System.Diagnostics.Debug.Print("RKH: " + Converter.ConvertHexToString(RootKeyHash, ""));
#endif
                    }
                }
            }
        }
    }
}
