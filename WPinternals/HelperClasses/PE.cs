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
//
// Some of the classes and functions in this file were found online.
// Where possible the original authors are referenced.

using System;

namespace WPinternals.HelperClasses
{
    internal static class PE
    {
        internal static byte[] GetResource(byte[] PEfile, int[] Index)
        {
            // Explanation of PE header here:
            // https://msdn.microsoft.com/en-us/library/ms809762.aspx?f=255&MSPPError=-2147217396

            uint PEPointer = ByteOperations.ReadUInt32(PEfile, 0x3C);
            ushort OptionalHeaderSize = ByteOperations.ReadUInt16(PEfile, PEPointer + 0x14);
            uint SectionTablePointer = PEPointer + 0x18 + OptionalHeaderSize;
            ushort SectionCount = ByteOperations.ReadUInt16(PEfile, PEPointer + 0x06);
            uint? ResourceSectionEntryPointer = null;
            for (int i = 0; i < SectionCount; i++)
            {
                string SectionName = ByteOperations.ReadAsciiString(PEfile, (uint)(SectionTablePointer + i * 0x28), 8);
                int e = SectionName.IndexOf('\0');
                if (e >= 0)
                {
                    SectionName = SectionName.Substring(0, e);
                }

                if (SectionName == ".rsrc")
                {
                    ResourceSectionEntryPointer = (uint)(SectionTablePointer + i * 0x28);
                    break;
                }
            }
            if (ResourceSectionEntryPointer == null)
            {
                throw new WPinternalsException("Resource-section not found");
            }

            uint ResourceRawSize = ByteOperations.ReadUInt32(PEfile, (uint)ResourceSectionEntryPointer + 0x10);
            uint ResourceRawPointer = ByteOperations.ReadUInt32(PEfile, (uint)ResourceSectionEntryPointer + 0x14);
            uint ResourceVirtualPointer = ByteOperations.ReadUInt32(PEfile, (uint)ResourceSectionEntryPointer + 0x0C);

            uint p = ResourceRawPointer;
            for (int i = 0; i < Index.Length; i++)
            {
                ushort ResourceNamedEntryCount = ByteOperations.ReadUInt16(PEfile, p + 0x0c);
                ushort ResourceIdEntryCount = ByteOperations.ReadUInt16(PEfile, p + 0x0e);
                for (int j = ResourceNamedEntryCount; j < ResourceNamedEntryCount + ResourceIdEntryCount; j++)
                {
                    uint ResourceID = ByteOperations.ReadUInt32(PEfile, (uint)(p + 0x10 + j * 8));
                    uint NextPointer = ByteOperations.ReadUInt32(PEfile, (uint)(p + 0x10 + j * 8 + 4));
                    if (ResourceID == (uint)Index[i])
                    {
                        // Check high bit
                        if ((NextPointer & 0x80000000) == 0 != (i == Index.Length - 1))
                        {
                            throw new WPinternalsException("Bad resource path");
                        }

                        p = ResourceRawPointer + (NextPointer & 0x7fffffff);
                        break;
                    }
                }
            }

            uint ResourceValuePointer = ByteOperations.ReadUInt32(PEfile, p) - ResourceVirtualPointer + ResourceRawPointer;
            uint ResourceValueSize = ByteOperations.ReadUInt32(PEfile, p + 4);

            byte[] ResourceValue = new byte[ResourceValueSize];
            Array.Copy(PEfile, ResourceValuePointer, ResourceValue, 0, ResourceValueSize);

            return ResourceValue;
        }

        internal static Version GetFileVersion(byte[] PEfile)
        {
            byte[] version = GetResource(PEfile, [(int)ResourceType.RT_VERSION, 1, 1033]);

            // RT_VERSION format:
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms647001(v=vs.85).aspx
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms646997(v=vs.85).aspx
            const uint FixedFileInfoPointer = 0x28;
            ushort Major = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x0A);
            ushort Minor = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x08);
            ushort Build = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x0E);
            ushort Revision = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x0C);

            return new Version(Major, Minor, Build, Revision);
        }

        internal static Version GetProductVersion(byte[] PEfile)
        {
            byte[] version = GetResource(PEfile, [(int)ResourceType.RT_VERSION, 1, 1033]);

            // RT_VERSION format:
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms647001(v=vs.85).aspx
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms646997(v=vs.85).aspx
            const uint FixedFileInfoPointer = 0x28;
            ushort Major = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x12);
            ushort Minor = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x10);
            ushort Build = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x16);
            ushort Revision = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x14);

            return new Version(Major, Minor, Build, Revision);
        }
    }
}
