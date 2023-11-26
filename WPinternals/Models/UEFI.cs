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
using System.Linq;

namespace WPinternals
{
    internal class EFI
    {
        internal Guid Guid;
        internal string Name;
        internal int Type;
        internal UInt32 Size;
        internal UInt32 FileOffset;
        internal UInt32 SectionOffset;
        internal UInt32 BinaryOffset;
    }

    internal class UEFI
    {
        internal byte[] Binary;
        private byte[] DecompressedImage;
        internal List<EFI> EFIs = new();
        private readonly byte PaddingByteValue = 0xFF;
        private readonly UInt32 DecompressedVolumeSectionHeaderOffset;
        private readonly UInt32 DecompressedVolumeHeaderOffset;
        private readonly UInt32 VolumeSize;
        private readonly UInt16 VolumeHeaderSize;
        private readonly UInt32 FileHeaderOffset;
        private readonly UInt32 SectionHeaderOffset;
        private readonly UInt32 CompressedSubImageOffset;
        private UInt32 CompressedSubImageSize;

        // First 0x28 bytes are Qualcomm partition header
        // Inside the attributes of the VolumeHeader, the Volume-alignment is set to 8 (on Windows Phone UEFI images)
        // The Volume always starts right after the Qualcomm header at position 0x28.
        // So the VolumeHeader-alignment is always complied.
        private readonly UInt32 VolumeHeaderOffset = 0x28;

        internal UEFI(byte[] UefiBinary)
        {
            Binary = UefiBinary;

            string VolumeHeaderMagic;
            UInt32? Offset = ByteOperations.FindAscii(Binary, "_FVH");
            VolumeHeaderOffset = Offset == null ? throw new BadImageFormatException() : (UInt32)Offset - 0x28;

            if (!VerifyVolumeChecksum(Binary, VolumeHeaderOffset))
            {
                throw new BadImageFormatException();
            }

            VolumeSize = ByteOperations.ReadUInt32(Binary, VolumeHeaderOffset + 0x20); // TODO: This is actually a QWORD
            VolumeHeaderSize = ByteOperations.ReadUInt16(Binary, VolumeHeaderOffset + 0x30);
            PaddingByteValue = (ByteOperations.ReadUInt32(Binary, VolumeHeaderOffset + 0x2C) & 0x00000800) > 0 ? (byte)0xFF : (byte)0x00; // EFI_FVB_ERASE_POLARITY = 0x00000800

            // In the volume look for a file of type EFI_FV_FILETYPE_FIRMWARE_VOLUME_IMAGE (0x0B)

            FileHeaderOffset = VolumeHeaderOffset + VolumeHeaderSize;
            bool VolumeFound = false;
            int FileType;
            UInt32 FileSize;
            do
            {
                if (!VerifyFileChecksum(Binary, FileHeaderOffset))
                {
                    throw new BadImageFormatException();
                }

                FileType = ByteOperations.ReadUInt8(Binary, FileHeaderOffset + 0x12);
                FileSize = ByteOperations.ReadUInt24(Binary, FileHeaderOffset + 0x14);

                if (FileType == 0x0B) // EFI_FV_FILETYPE_FIRMWARE_VOLUME_IMAGE
                {
                    VolumeFound = true;
                }
                else
                {
                    FileHeaderOffset += FileSize;

                    // FileHeaderOffset in Volume-body must be Align 8
                    // In the file-header-attributes the file-alignment relative to the start of the volume is always set to 1,
                    // so that alignment can be ignored.
                    FileHeaderOffset = ByteOperations.Align(VolumeHeaderOffset + VolumeHeaderSize, FileHeaderOffset, 8);
                }
            }
            while (!VolumeFound && (FileHeaderOffset < (VolumeHeaderOffset + VolumeSize)));

            if (!VolumeFound)
            {
                throw new BadImageFormatException();
            }

            // Look in file for section of type EFI_SECTION_GUID_DEFINED (0x02)

            SectionHeaderOffset = FileHeaderOffset + 0x18;
            int SectionType;
            UInt32 SectionSize;
            UInt16 SectionHeaderSize = 0;

            bool DecompressedVolumeFound = false;
            do
            {
                SectionType = ByteOperations.ReadUInt8(Binary, SectionHeaderOffset + 0x03);
                SectionSize = ByteOperations.ReadUInt24(Binary, SectionHeaderOffset + 0x00);

                if (SectionType == 0x02) // EFI_SECTION_GUID_DEFINED
                {
                    SectionHeaderSize = ByteOperations.ReadUInt16(Binary, SectionHeaderOffset + 0x14);
                    DecompressedVolumeFound = true;
                }
                else
                {
                    SectionHeaderOffset += SectionSize;

                    // SectionHeaderOffset in File-body must be Align 4
                    SectionHeaderOffset = ByteOperations.Align(FileHeaderOffset + 0x18, SectionHeaderOffset, 4);
                }
            }
            while (!DecompressedVolumeFound && (SectionHeaderOffset < (FileHeaderOffset + FileSize)));

            if (!DecompressedVolumeFound)
            {
                throw new BadImageFormatException();
            }

            // Decompress subvolume
            CompressedSubImageOffset = SectionHeaderOffset + SectionHeaderSize;
            CompressedSubImageSize = SectionSize - SectionHeaderSize;

            // DECOMPRESS HERE
            DecompressedImage = LZMA.Decompress(Binary, CompressedSubImageOffset, CompressedSubImageSize);

            // Extracted volume contains Sections at its root level

            DecompressedVolumeSectionHeaderOffset = 0;
            DecompressedVolumeFound = false;
            do
            {
                SectionType = ByteOperations.ReadUInt8(DecompressedImage, DecompressedVolumeSectionHeaderOffset + 0x03);
                SectionSize = ByteOperations.ReadUInt24(DecompressedImage, DecompressedVolumeSectionHeaderOffset + 0x00);
                SectionHeaderSize = ByteOperations.ReadUInt16(DecompressedImage, DecompressedVolumeSectionHeaderOffset + 0x14);

                if (SectionType == 0x17) // EFI_SECTION_FIRMWARE_VOLUME_IMAGE
                {
                    DecompressedVolumeFound = true;
                }
                else
                {
                    DecompressedVolumeSectionHeaderOffset += SectionSize;

                    // SectionHeaderOffset in File-body must be Align 4
                    DecompressedVolumeSectionHeaderOffset = ByteOperations.Align(FileHeaderOffset + 0x18, DecompressedVolumeSectionHeaderOffset, 4);
                }
            }
            while (!DecompressedVolumeFound && (DecompressedVolumeSectionHeaderOffset < DecompressedImage.Length));

            if (!DecompressedVolumeFound)
            {
                throw new BadImageFormatException();
            }

            DecompressedVolumeHeaderOffset = DecompressedVolumeSectionHeaderOffset + 4;

            // PARSE COMPRESSED VOLUME
            VolumeHeaderMagic = ByteOperations.ReadAsciiString(DecompressedImage, DecompressedVolumeHeaderOffset + 0x28, 0x04);
            if (VolumeHeaderMagic != "_FVH")
            {
                throw new BadImageFormatException();
            }

            if (!VerifyVolumeChecksum(DecompressedImage, DecompressedVolumeHeaderOffset))
            {
                throw new BadImageFormatException();
            }

            Int32 DecompressedVolumeSize = ByteOperations.ReadInt32(DecompressedImage, DecompressedVolumeHeaderOffset + 0x20); // TODO: This is actually a QWORD
            UInt16 DecompressedVolumeHeaderSize = ByteOperations.ReadUInt16(DecompressedImage, DecompressedVolumeHeaderOffset + 0x30);

            // The files in this decompressed volume are the real EFI's.
            UInt32 DecompressedFileHeaderOffset = DecompressedVolumeHeaderOffset + DecompressedVolumeHeaderSize;
            EFI CurrentEFI;
            do
            {
                if ((DecompressedFileHeaderOffset + 0x18) >= (DecompressedVolumeHeaderOffset + DecompressedVolumeSize))
                {
                    break;
                }

                bool ContentFound = false;
                for (int i = 0; i < 0x18; i++)
                {
                    if (DecompressedImage[DecompressedFileHeaderOffset + i] != PaddingByteValue)
                    {
                        ContentFound = true;
                        break;
                    }
                }
                if (!ContentFound)
                {
                    break;
                }

                FileSize = ByteOperations.ReadUInt24(DecompressedImage, DecompressedFileHeaderOffset + 0x14);

                if ((DecompressedFileHeaderOffset + FileSize) >= (DecompressedVolumeHeaderOffset + DecompressedVolumeSize))
                {
                    break;
                }

                if (!VerifyFileChecksum(DecompressedImage, DecompressedFileHeaderOffset))
                {
                    throw new BadImageFormatException();
                }

                CurrentEFI = new EFI
                {
                    Type = ByteOperations.ReadUInt8(DecompressedImage, DecompressedFileHeaderOffset + 0x12)
                };
                byte[] FileGuidBytes = new byte[0x10];
                Buffer.BlockCopy(DecompressedImage, (int)DecompressedFileHeaderOffset + 0x00, FileGuidBytes, 0, 0x10);
                CurrentEFI.Guid = new Guid(FileGuidBytes);

                // Parse sections of the EFI
                CurrentEFI.FileOffset = DecompressedFileHeaderOffset;
                UInt32 DecompressedSectionHeaderOffset = DecompressedFileHeaderOffset + 0x18;
                do
                {
                    SectionType = ByteOperations.ReadUInt8(DecompressedImage, DecompressedSectionHeaderOffset + 0x03);
                    SectionSize = ByteOperations.ReadUInt24(DecompressedImage, DecompressedSectionHeaderOffset + 0x00);

                    // SectionTypes that are relevant here:
                    // 0x10 = PE File
                    // 0x19 = RAW
                    // 0x15 = Description
                    // Not all section headers in the UEFI specs are 4 bytes long,
                    // but the sections that are used in Windows Phone EFI's all have a header of 4 bytes.
                    if (SectionType == 0x15)
                    {
                        CurrentEFI.Name = ByteOperations.ReadUnicodeString(DecompressedImage, DecompressedSectionHeaderOffset + 0x04, SectionSize - 0x04).TrimEnd([(char)0, ' ']);
                    }
                    else if ((SectionType == 0x10) || (SectionType == 0x19))
                    {
                        CurrentEFI.SectionOffset = DecompressedSectionHeaderOffset;
                        CurrentEFI.BinaryOffset = DecompressedSectionHeaderOffset + 0x04;
                        CurrentEFI.Size = SectionSize - 0x04;
                    }

                    DecompressedSectionHeaderOffset += SectionSize;

                    // SectionHeaderOffset in File-body must be Align 4
                    DecompressedSectionHeaderOffset = ByteOperations.Align(DecompressedFileHeaderOffset + 0x18, DecompressedSectionHeaderOffset, 4);
                }
                while (DecompressedSectionHeaderOffset < (DecompressedFileHeaderOffset + FileSize));

                DecompressedFileHeaderOffset += FileSize;

                // FileHeaderOffset in Volume-body must be Align 8
                // In the file-header-attributes the file-alignment relative to the start of the volume is always set to 1,
                // so that alignment can be ignored.
                DecompressedFileHeaderOffset = ByteOperations.Align(DecompressedVolumeHeaderOffset + DecompressedVolumeHeaderSize, DecompressedFileHeaderOffset, 8);

                EFIs.Add(CurrentEFI);
            }
            while (DecompressedFileHeaderOffset < (DecompressedVolumeHeaderOffset + DecompressedVolumeSize));
        }

        internal byte[] GetFile(string Name)
        {
            EFI File = EFIs.Find(f => string.Equals(Name, f.Name, StringComparison.CurrentCultureIgnoreCase) || string.Equals(Name, f.Guid.ToString(), StringComparison.CurrentCultureIgnoreCase));
            if (File == null)
            {
                return null;
            }

            byte[] Bytes = new byte[File.Size];
            Buffer.BlockCopy(DecompressedImage, (int)File.BinaryOffset, Bytes, 0, (int)File.Size);

            return Bytes;
        }

        internal byte[] GetFile(Guid Guid)
        {
            EFI File = EFIs.Find(f => Guid == f.Guid);
            if (File == null)
            {
                return null;
            }

            byte[] Bytes = new byte[File.Size];
            Buffer.BlockCopy(DecompressedImage, (int)File.BinaryOffset, Bytes, 0, (int)File.Size);

            return Bytes;
        }

        internal void ReplaceFile(string Name, byte[] Binary)
        {
            EFI File = EFIs.Find(f => string.Equals(Name, f.Name, StringComparison.CurrentCultureIgnoreCase) || string.Equals(Name, f.Guid.ToString(), StringComparison.CurrentCultureIgnoreCase));
            if (File == null)
            {
                throw new ArgumentOutOfRangeException();
            }

            UInt32 OldBinarySize = File.Size;
            UInt32 NewBinarySize = (UInt32)Binary.Length;

            UInt32 OldSectionPadding = ByteOperations.Align(0, OldBinarySize, 4) - OldBinarySize;
            UInt32 NewSectionPadding = ByteOperations.Align(0, NewBinarySize, 4) - NewBinarySize;

            UInt32 OldFileSize = ByteOperations.ReadUInt24(DecompressedImage, File.FileOffset + 0x14);
            UInt32 NewFileSize = OldFileSize - OldBinarySize - OldSectionPadding + NewBinarySize + NewSectionPadding;

            UInt32 OldFilePadding = ByteOperations.Align(0, OldFileSize, 8) - OldFileSize;
            UInt32 NewFilePadding = ByteOperations.Align(0, NewFileSize, 8) - NewFileSize;

            if ((OldBinarySize + OldSectionPadding) != (NewBinarySize + NewSectionPadding))
            {
                byte[] NewImage = new byte[DecompressedImage.Length - OldFileSize - OldFilePadding + NewFileSize + NewFilePadding]; // Also preserve space for File-alignement here

                // Copy Volume-head and File-head
                Buffer.BlockCopy(DecompressedImage, 0, NewImage, 0, (int)File.BinaryOffset);

                // Copy new binary
                Buffer.BlockCopy(Binary, 0, NewImage, (int)File.BinaryOffset, Binary.Length);

                // Insert section-padding
                for (int i = 0; i < NewSectionPadding; i++)
                {
                    NewImage[File.BinaryOffset + NewBinarySize + i] = PaddingByteValue;
                }

                // Copy file-tail
                Buffer.BlockCopy(
                    DecompressedImage,
                    (int)(File.BinaryOffset + OldBinarySize + OldSectionPadding),
                    NewImage,
                    (int)(File.BinaryOffset + NewBinarySize + NewSectionPadding),
                    (int)(File.FileOffset + OldFileSize - File.BinaryOffset - OldBinarySize - OldSectionPadding));

                // Insert file-padding
                for (int i = 0; i < NewFilePadding; i++)
                {
                    NewImage[File.BinaryOffset + NewFileSize + i] = PaddingByteValue;
                }

                // Copy volume-tail
                Buffer.BlockCopy(
                    DecompressedImage,
                    (int)(File.FileOffset + OldFileSize + OldFilePadding),
                    NewImage,
                    (int)(File.FileOffset + NewFileSize + NewFilePadding),
                    (int)(DecompressedImage.Length - File.FileOffset - OldFileSize - OldFilePadding));

                Int32 NewOffset = (int)(NewFileSize + NewFilePadding) - (int)(OldFileSize - OldFilePadding);

                // Fix section-size
                ByteOperations.WriteUInt24(NewImage, File.SectionOffset, (UInt32)(ByteOperations.ReadUInt24(NewImage, File.SectionOffset) + NewOffset));

                // Fix file-size
                ByteOperations.WriteUInt24(NewImage, File.FileOffset + 0x14, (UInt32)(ByteOperations.ReadUInt24(NewImage, File.FileOffset + 0x14) + NewOffset));

                // Fix volume-size - TODO: This is actually a QWORD
                ByteOperations.WriteUInt32(NewImage, DecompressedVolumeHeaderOffset + 0x20, (UInt32)(ByteOperations.ReadUInt32(NewImage, DecompressedVolumeHeaderOffset + 0x20) + NewOffset));

                // Fix section-size
                ByteOperations.WriteUInt24(NewImage, DecompressedVolumeSectionHeaderOffset, (UInt32)(ByteOperations.ReadUInt24(NewImage, DecompressedVolumeSectionHeaderOffset) + NewOffset));

                DecompressedImage = NewImage;

                // Modify all sizes in EFI's
                foreach (EFI CurrentFile in EFIs)
                {
                    if (CurrentFile.FileOffset > File.FileOffset)
                    {
                        CurrentFile.FileOffset = (UInt32)(CurrentFile.FileOffset + NewOffset);
                        CurrentFile.SectionOffset = (UInt32)(CurrentFile.SectionOffset + NewOffset);
                        CurrentFile.BinaryOffset = (UInt32)(CurrentFile.BinaryOffset + NewOffset);
                    }
                }
            }
            else
            {
                Buffer.BlockCopy(Binary, 0, DecompressedImage, (int)File.BinaryOffset, Binary.Length);
                for (int i = 0; i < NewSectionPadding; i++)
                {
                    DecompressedImage[File.BinaryOffset + Binary.Length + i] = PaddingByteValue;
                }
            }

            // Calculate File-checksum
            CalculateFileChecksum(DecompressedImage, File.FileOffset);

            // Calculate Volume-checksum
            CalculateVolumeChecksum(DecompressedImage, DecompressedVolumeHeaderOffset);
        }

        internal byte[] Rebuild()
        {
            // The new binary will include the Qualcomm header, but not the signature and certificates, because they won't match anyway.
            byte[] NewBinary = new byte[Binary.Length];
            Buffer.BlockCopy(Binary, 0, NewBinary, 0, (int)CompressedSubImageOffset);

            ByteOperations.WriteUInt32(NewBinary, 0x10, ByteOperations.ReadUInt32(NewBinary, 0x14)); // Complete image size - does not include signature and certs anymore
            ByteOperations.WriteUInt32(NewBinary, 0x18, 0x00000000); // Address of signature
            ByteOperations.WriteUInt32(NewBinary, 0x1C, 0x00000000); // Signature length
            ByteOperations.WriteUInt32(NewBinary, 0x20, 0x00000000); // Address of certificate
            ByteOperations.WriteUInt32(NewBinary, 0x24, 0x00000000); // Certificate length

            // Compress volume
            byte[] NewCompressedImage = LZMA.Compress(DecompressedImage, 0, (UInt32)DecompressedImage.Length);

            // Replace compressed volume and add correct padding
            // First copy new image
            Buffer.BlockCopy(NewCompressedImage, 0, NewBinary, (int)CompressedSubImageOffset, NewCompressedImage.Length);

            // Determine padding
            UInt32 OldSectionPadding = ByteOperations.Align(0, CompressedSubImageSize, 4) - CompressedSubImageSize;
            UInt32 NewSectionPadding = ByteOperations.Align(0, (UInt32)NewCompressedImage.Length, 4) - (UInt32)NewCompressedImage.Length;
            UInt32 OldFileSize = ByteOperations.ReadUInt24(Binary, FileHeaderOffset + 0x14);

            // Filesize includes fileheader. But it does not include the padding-bytes. Not even the padding bytes of the last section.
            UInt32 NewFileSize;
            if ((CompressedSubImageOffset + CompressedSubImageSize + OldSectionPadding) >= (FileHeaderOffset + OldFileSize))
            {
                // Compressed image is the last section of this file
                NewFileSize = CompressedSubImageOffset - FileHeaderOffset + (UInt32)NewCompressedImage.Length;
            }
            else
            {
                // Compressed image is NOT the last section of this file
                NewFileSize = OldFileSize - CompressedSubImageSize - OldSectionPadding + (UInt32)NewCompressedImage.Length + NewSectionPadding;
            }

            // Add section padding
            for (int i = 0; i < NewSectionPadding; i++)
            {
                NewBinary[CompressedSubImageOffset + NewCompressedImage.Length + i] = PaddingByteValue;
            }

            // If there are more bytes after the section padding of the compressed image, then copy the trailing sections
            if (((Int32)FileHeaderOffset + OldFileSize - CompressedSubImageOffset - CompressedSubImageSize - OldSectionPadding) > 0)
            {
                Buffer.BlockCopy(Binary, (int)(CompressedSubImageOffset + CompressedSubImageSize + OldSectionPadding), NewBinary,
                    (int)(CompressedSubImageOffset + NewCompressedImage.Length + NewSectionPadding),
                    (int)(FileHeaderOffset + OldFileSize - CompressedSubImageOffset - CompressedSubImageSize - OldSectionPadding));
            }

            // Add file padding
            // Filesize does not include last section padding or file padding
            UInt32 OldFilePadding = ByteOperations.Align(0, OldFileSize, 8) - OldFileSize;
            UInt32 NewFilePadding = ByteOperations.Align(0, NewFileSize, 8) - NewFileSize;
            for (int i = 0; i < NewFilePadding; i++)
            {
                NewBinary[FileHeaderOffset + NewFileSize + i] = PaddingByteValue;
            }

            if (NewCompressedImage.Length > CompressedSubImageSize)
            {
                Buffer.BlockCopy(Binary, (int)(FileHeaderOffset + OldFileSize + OldFilePadding), NewBinary, (int)(FileHeaderOffset + NewFileSize + NewFilePadding),
                    (int)(VolumeHeaderOffset + VolumeSize - FileHeaderOffset - NewFileSize - NewFilePadding));
            }
            else
            {
                Buffer.BlockCopy(Binary, (int)(FileHeaderOffset + OldFileSize + OldFilePadding), NewBinary, (int)(FileHeaderOffset + NewFileSize + NewFilePadding),
                    (int)(VolumeHeaderOffset + VolumeSize - FileHeaderOffset - OldFileSize - OldFilePadding));
                for (int i = (int)(VolumeHeaderOffset + VolumeSize - OldFileSize - OldFilePadding + NewFileSize + NewFilePadding); i < VolumeHeaderOffset + VolumeSize; i++)
                {
                    NewBinary[i] = PaddingByteValue;
                }
            }
            CompressedSubImageSize = (UInt32)NewCompressedImage.Length;

            // Fix section
            ByteOperations.WriteUInt24(NewBinary, SectionHeaderOffset, CompressedSubImageSize + ByteOperations.ReadUInt16(Binary, SectionHeaderOffset + 0x14));

            // Fix file
            ByteOperations.WriteUInt24(NewBinary, FileHeaderOffset + 0x14, NewFileSize);
            CalculateFileChecksum(NewBinary, FileHeaderOffset);

            // Fix volume (volume size is fixed)
            CalculateVolumeChecksum(NewBinary, VolumeHeaderOffset);

            Binary = NewBinary;
            return Binary;
        }

        private void CalculateVolumeChecksum(byte[] Image, UInt32 Offset)
        {
            UInt16 VolumeHeaderSize = ByteOperations.ReadUInt16(Image, Offset + 0x30);
            ByteOperations.WriteUInt16(Image, Offset + 0x32, 0); // Clear checksum
            UInt16 NewChecksum = ByteOperations.CalculateChecksum16(Image, Offset, VolumeHeaderSize);
            ByteOperations.WriteUInt16(Image, Offset + 0x32, NewChecksum);
        }

        private bool VerifyVolumeChecksum(byte[] Image, UInt32 Offset)
        {
            UInt16 VolumeHeaderSize = ByteOperations.ReadUInt16(Image, Offset + 0x30);
            byte[] Header = new byte[VolumeHeaderSize];
            Buffer.BlockCopy(Image, (int)Offset, Header, 0, VolumeHeaderSize);
            ByteOperations.WriteUInt16(Header, 0x32, 0); // Clear checksum
            UInt16 CurrentChecksum = ByteOperations.ReadUInt16(Image, Offset + 0x32);
            UInt16 NewChecksum = ByteOperations.CalculateChecksum16(Header, 0, VolumeHeaderSize);
            return CurrentChecksum == NewChecksum;
        }

        private void CalculateFileChecksum(byte[] Image, UInt32 Offset)
        {
            const UInt16 FileHeaderSize = 0x18;
            UInt32 FileSize = ByteOperations.ReadUInt24(Image, Offset + 0x14);

            ByteOperations.WriteUInt16(Image, Offset + 0x10, 0); // Clear checksum
            byte NewChecksum = ByteOperations.CalculateChecksum8(Image, Offset, (UInt32)FileHeaderSize - 1);
            ByteOperations.WriteUInt8(Image, Offset + 0x10, NewChecksum); // File-Header checksum

            byte FileAttribs = ByteOperations.ReadUInt8(Image, Offset + 0x13);
            if ((FileAttribs & 0x40) > 0)
            {
                // Calculate file checksum
                byte CalculatedFileChecksum = ByteOperations.CalculateChecksum8(Image, Offset + FileHeaderSize, FileSize - FileHeaderSize);
                ByteOperations.WriteUInt8(Image, Offset + 0x11, CalculatedFileChecksum);
            }
            else
            {
                // Fixed file checksum
                ByteOperations.WriteUInt8(Image, Offset + 0x11, 0xAA);
            }
        }

        private bool VerifyFileChecksum(byte[] Image, UInt32 Offset)
        {
            // This function only checks fixed checksum-values 0x55 and 0xAA.

            const UInt16 FileHeaderSize = 0x18;
            UInt32 FileSize = ByteOperations.ReadUInt24(Image, Offset + 0x14);

            byte[] Header = new byte[FileHeaderSize - 1];
            Buffer.BlockCopy(Image, (int)Offset, Header, 0, FileHeaderSize - 1);
            ByteOperations.WriteUInt16(Header, 0x10, 0); // Clear checksum
            byte CurrentHeaderChecksum = ByteOperations.ReadUInt8(Image, Offset + 0x10);
            byte CalculatedHeaderChecksum = ByteOperations.CalculateChecksum8(Header, 0, (UInt32)FileHeaderSize - 1);

            if (CurrentHeaderChecksum != CalculatedHeaderChecksum)
            {
                return false;
            }

            byte FileAttribs = ByteOperations.ReadUInt8(Image, Offset + 0x13);
            byte CurrentFileChecksum = ByteOperations.ReadUInt8(Image, Offset + 0x11);
            if ((FileAttribs & 0x40) > 0)
            {
                // Calculate file checksum
                byte CalculatedFileChecksum = ByteOperations.CalculateChecksum8(Image, Offset + FileHeaderSize, FileSize - FileHeaderSize);
                if (CurrentFileChecksum != CalculatedFileChecksum)
                {
                    return false;
                }
            }
            else
            {
                // Fixed file checksum
                if ((CurrentFileChecksum != 0xAA) && (CurrentFileChecksum != 0x55))
                {
                    return false;
                }
            }

            return true;
        }

        private void ClearEfiChecksum(byte[] EfiFile)
        {
            UInt32 PEHeaderOffset = ByteOperations.ReadUInt32(EfiFile, 0x3C);
            ByteOperations.WriteUInt32(EfiFile, PEHeaderOffset + 0x58, 0);
        }

        // Magic!
        internal byte[] Patch()
        {
            byte[] SecurityDxe = GetFile("SecurityDxe");
            ClearEfiChecksum(SecurityDxe);

            UInt32? PatchOffset = ByteOperations.FindPattern(SecurityDxe,
                [0xF0, 0x41, 0x2D, 0xE9, 0xFF, 0xFF, 0xB0, 0xE1, 0x28, 0xD0, 0x4D, 0xE2, 0xFF, 0xFF, 0xA0, 0xE1, 0x00, 0x00, 0xFF, 0x13, 0x20, 0xFF, 0xA0, 0xE3],
                [0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0xFF, 0x00, 0x00],
                null);

            if (PatchOffset == null)
            {
                throw new BadImageFormatException();
            }

            Buffer.BlockCopy(new byte[] { 0x00, 0x00, 0xA0, 0xE3, 0x1E, 0xFF, 0x2F, 0xE1 }, 0, SecurityDxe, (int)PatchOffset, 8);

            ReplaceFile("SecurityDxe", SecurityDxe);

            byte[] SecurityServicesDxe = GetFile("SecurityServicesDxe");
            ClearEfiChecksum(SecurityServicesDxe);

            PatchOffset = ByteOperations.FindPattern(SecurityServicesDxe,
                [0x10, 0xFF, 0xFF, 0xE5, 0x80, 0xFF, 0x10, 0xE3, 0xFF, 0xFF, 0xFF, 0x0A],
                [0x00, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x00],
                null);

            if (PatchOffset == null)
            {
                throw new BadImageFormatException();
            }

            ByteOperations.WriteUInt8(SecurityServicesDxe, (UInt32)PatchOffset + 0x0B, 0xEA);

            PatchOffset = ByteOperations.FindPattern(SecurityServicesDxe,
                [0x11, 0xFF, 0xFF, 0xE5, 0x40, 0xFF, 0x10, 0xE3, 0xFF, 0xFF, 0xFF, 0x0A],
                [0x00, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x00],
                null);

            if (PatchOffset == null)
            {
                throw new BadImageFormatException();
            }

            ByteOperations.WriteUInt8(SecurityServicesDxe, (UInt32)PatchOffset + 0x0B, 0xEA);

            ReplaceFile("SecurityServicesDxe", SecurityServicesDxe);

            return Rebuild();
        }
    }
}
