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
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace WPinternals.HelperClasses
{
    // For reading a compressed stream or normal stream
    internal class DecompressedStream : Stream
    {
        private readonly Stream UnderlyingStream;
        private readonly bool IsSourceCompressed;
        private readonly ulong DecompressedLength;
        private long ReadPosition = 0;

        // For reading a compressed stream
        internal DecompressedStream(Stream InputStream)
        {
            UnderlyingStream = new ReadSeekableStream(InputStream, 0x100);

            byte[] Signature = new byte["CompressedPartition".Length + 2];
            Signature[0x00] = 0xFF;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes("CompressedPartition"), 0, Signature, 0x01, "CompressedPartition".Length);
            Signature["CompressedPartition".Length + 1] = 0x00;

            int PrimaryHeaderSize = 0x0A + "CompressedPartition".Length;
            byte[] SignatureRead = new byte[Signature.Length];
            UnderlyingStream.Read(SignatureRead, 0, Signature.Length);

            IsSourceCompressed = StructuralComparisons.StructuralEqualityComparer.Equals(Signature, SignatureRead);
            if (IsSourceCompressed)
            {
                byte[] FormatVersionBytes = new byte[4];
                UnderlyingStream.Read(FormatVersionBytes, 0, 4);
                if (BitConverter.ToUInt32(FormatVersionBytes, 0) > 1) // Max supported format version = 1
                {
                    throw new InvalidDataException();
                }

                byte[] HeaderSizeBytes = new byte[4];
                UnderlyingStream.Read(HeaderSizeBytes, 0, 4);
                uint HeaderSize = BitConverter.ToUInt32(HeaderSizeBytes, 0);

                if (HeaderSize >= Signature.Length + 0x10)
                {
                    byte[] DecompressedLengthBytes = new byte[8];
                    UnderlyingStream.Read(DecompressedLengthBytes, 0, 8);
                    DecompressedLength = BitConverter.ToUInt64(DecompressedLengthBytes, 0);
                }
                else
                {
                    throw new InvalidDataException();
                }

                uint HeaderBytesRemaining = (uint)(HeaderSize - Signature.Length - 0x10);
                if (HeaderBytesRemaining > 0)
                {
                    byte[] HeaderBytes = new byte[HeaderBytesRemaining];
                    UnderlyingStream.Read(HeaderBytes, 0, (int)HeaderBytesRemaining);
                }

                UnderlyingStream = new GZipStream(UnderlyingStream, CompressionMode.Decompress, false);
            }
            else
            {
                UnderlyingStream.Position = 0;
            }
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int RealCount = UnderlyingStream.Read(buffer, offset, count);
            ReadPosition += RealCount;
            return RealCount;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public override long Position
        {
            get
            {
                return ReadPosition;
            }
            set
            {
                throw new NotSupportedException();
            }
        }
        public override bool CanTimeout
        {
            get
            {
                return UnderlyingStream.CanTimeout;
            }
        }
        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }
        public override long Length
        {
            get
            {
                if (IsSourceCompressed)
                {
                    return (long)DecompressedLength;
                }
                else
                {
                    return UnderlyingStream.Length;
                }
            }
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
        public override void Flush()
        {
            UnderlyingStream.Flush();
        }
        public override void Close()
        {
            UnderlyingStream.Close();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnderlyingStream.Dispose();
            }
        }
    }
}
