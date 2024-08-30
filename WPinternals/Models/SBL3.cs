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

namespace WPinternals
{
    internal class SBL3
    {
        internal byte[] Binary;

        internal SBL3(byte[] Binary)
        {
            this.Binary = Binary;
        }

        internal SBL3(string FileName)
        {
            Binary = null;

            // First try to parse as FFU
            try
            {
                if (FFU.IsFFU(FileName))
                {
                    FFU FFUFile = new(FileName);
                    Binary = FFUFile.GetPartition("SBL3");
                }
            }
            catch (Exception ex)
            {
                LogFile.LogException(ex, LogType.FileOnly);
            }

            // If not succeeded, then try to parse it as raw image
            if (Binary == null)
            {
                byte[] SBL3Pattern = [0x18, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x8F, 0xFF, 0xFF, 0xFF, 0xFF];
                byte[] SBL3Mask = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF];

                UInt32? Offset = ByteOperations.FindPatternInFile(FileName, SBL3Pattern, SBL3Mask, out byte[] SBL3Header);

                if (Offset != null)
                {
                    UInt32 Length = ByteOperations.ReadUInt32(SBL3Header, 0x10) + 0x28; // SBL3 Image Size + Header Size
                    Binary = new byte[Length];

                    FileStream Stream = new(FileName, FileMode.Open, FileAccess.Read);
                    Stream.Seek((long)Offset, SeekOrigin.Begin);
                    Stream.Read(Binary, 0, (int)Length);
                    Stream.Close();
                }
            }
        }

        // Magic!
        internal byte[] Patch()
        {
            UInt32? PatchOffset = ByteOperations.FindPattern(Binary,
                [0x04, 0x00, 0x9F, 0xE5, 0x28, 0x00, 0xD0, 0xE5, 0x1E, 0xFF, 0x2F, 0xE1],
                null, null);

            if (PatchOffset == null)
            {
                throw new BadImageFormatException();
            }

            Buffer.BlockCopy(new byte[] { 0x00, 0x00, 0xA0, 0xE3 }, 0, Binary, (int)PatchOffset + 4, 4);

            return Binary;
        }
    }
}
