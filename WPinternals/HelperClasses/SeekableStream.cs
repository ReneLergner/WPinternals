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

namespace WPinternals.HelperClasses
{
    internal class SeekableStream : Stream
    {
        private Stream UnderlyingStream;
        private long ReadPosition = 0;
        private readonly Func<Stream> StreamInitializer;
        private readonly long UnderlyingStreamLength;

        // For reading a compressed stream
        internal SeekableStream(Func<Stream> StreamInitializer, long? Length = null)
        {
            this.StreamInitializer = StreamInitializer;
            UnderlyingStream = StreamInitializer();
            if (Length != null)
            {
                UnderlyingStreamLength = (long)Length;
            }
            else
            {
                try
                {
                    UnderlyingStreamLength = UnderlyingStream.Length;
                }
                catch (Exception ex)
                {
                    LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                    LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                    LogFile.Log(ex.Message, LogType.FileAndConsole);
                    LogFile.Log(ex.StackTrace, LogType.FileAndConsole);

                    throw new ArgumentException("Unknown stream length");
                }
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
                return true;
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
            if (UnderlyingStream.CanSeek)
            {
                ReadPosition = UnderlyingStream.Seek(offset, origin);
                return ReadPosition;
            }
            else
            {
                long NewPosition = 0;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        NewPosition = offset;
                        break;
                    case SeekOrigin.Current:
                        NewPosition = ReadPosition + offset;
                        break;
                    case SeekOrigin.End:
                        NewPosition = UnderlyingStreamLength - offset;
                        break;
                }
                if (NewPosition < 0 || NewPosition > UnderlyingStreamLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }

                if (NewPosition < ReadPosition)
                {
                    UnderlyingStream.Close();
                    UnderlyingStream = StreamInitializer();
                    ReadPosition = 0;
                }
                ulong Remaining;
                byte[] Buffer = new byte[16384];
                while (ReadPosition < NewPosition)
                {
                    Remaining = (ulong)(NewPosition - ReadPosition);
                    if (Remaining > (ulong)Buffer.Length)
                    {
                        Remaining = (ulong)Buffer.Length;
                    }

                    UnderlyingStream.Read(Buffer, 0, (int)Remaining);
                    ReadPosition += (long)Remaining;
                }
                return ReadPosition;
            }
        }
        public override long Position
        {
            get
            {
                return ReadPosition;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
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
                return false;
            }
        }
        public override long Length
        {
            get
            {
                return UnderlyingStreamLength;
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
            throw new NotSupportedException();
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
