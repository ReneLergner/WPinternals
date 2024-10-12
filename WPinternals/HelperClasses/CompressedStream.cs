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
using System.IO;
using System.IO.Compression;
using System.Text;

namespace WPinternals.HelperClasses
{
    // For writing a compressed stream
    internal class CompressedStream : Stream
    {
        private readonly uint HeaderSize;
        private ulong WritePosition;
        private readonly GZipStream UnderlyingStream;

        internal CompressedStream(Stream OutputStream, ulong TotalDecompressedStreamLength)
        {
            // Write header
            HeaderSize = (uint)(0x12 + "CompressedPartition".Length);
            OutputStream.WriteByte(0xFF);
            OutputStream.Write(Encoding.ASCII.GetBytes("CompressedPartition"), 0, "CompressedPartition".Length);
            OutputStream.WriteByte(0x00);
            OutputStream.Write(BitConverter.GetBytes((uint)1), 0, 4); // Format version = 1
            OutputStream.Write(BitConverter.GetBytes(HeaderSize), 0, 4); // Headersize
            OutputStream.Write(BitConverter.GetBytes(TotalDecompressedStreamLength), 0, 8);

            UnderlyingStream = new GZipStream(OutputStream, CompressionLevel.Optimal, false);
            WritePosition = 0;
        }

        public override bool CanRead
        {
            get
            {
                return false;
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
            throw new NotSupportedException();
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }
        public override long Position
        {
            get
            {
                return (long)WritePosition;
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
                return (long)WritePosition;
            }
        }
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            WritePosition += (ulong)count;
            UnderlyingStream.Write(buffer, offset, count);
        }
        public override void Flush()
        {
            UnderlyingStream.Flush();
        }
        public override void Close()
        {
            UnderlyingStream.Close();
        }
        public new void Dispose()
        {
            UnderlyingStream.Dispose();
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
