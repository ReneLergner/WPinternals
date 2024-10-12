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
    // This class is written by: Eugene Beresovsky
    // https://stackoverflow.com/questions/13035925/stream-wrapper-to-make-stream-seekable/28036366#28036366
    internal class ReadSeekableStream : Stream
    {
        private long _underlyingPosition;
        private readonly byte[] _seekBackBuffer;
        private int _seekBackBufferCount;
        private int _seekBackBufferIndex;
        private readonly Stream _underlyingStream;

        public ReadSeekableStream(Stream underlyingStream, int seekBackBufferSize)
        {
            if (!underlyingStream.CanRead)
            {
                throw new Exception("Provided stream " + underlyingStream + " is not readable");
            }

            _underlyingStream = underlyingStream;
            _seekBackBuffer = new byte[seekBackBufferSize];
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
            int copiedFromBackBufferCount = 0;
            if (_seekBackBufferIndex < _seekBackBufferCount)
            {
                copiedFromBackBufferCount = Math.Min(count, _seekBackBufferCount - _seekBackBufferIndex);
                Buffer.BlockCopy(_seekBackBuffer, _seekBackBufferIndex, buffer, offset, copiedFromBackBufferCount);
                offset += copiedFromBackBufferCount;
                count -= copiedFromBackBufferCount;
                _seekBackBufferIndex += copiedFromBackBufferCount;
            }
            int bytesReadFromUnderlying = 0;
            if (count > 0)
            {
                bytesReadFromUnderlying = _underlyingStream.Read(buffer, offset, count);
                if (bytesReadFromUnderlying > 0)
                {
                    _underlyingPosition += bytesReadFromUnderlying;

                    int copyToBufferCount = Math.Min(bytesReadFromUnderlying, _seekBackBuffer.Length);
                    int copyToBufferOffset = Math.Min(_seekBackBufferCount, _seekBackBuffer.Length - copyToBufferCount);
                    int bufferBytesToMove = Math.Min(_seekBackBufferCount - 1, copyToBufferOffset);

                    if (bufferBytesToMove > 0)
                    {
                        Buffer.BlockCopy(_seekBackBuffer, _seekBackBufferCount - bufferBytesToMove, _seekBackBuffer, 0, bufferBytesToMove);
                    }

                    Buffer.BlockCopy(buffer, offset, _seekBackBuffer, copyToBufferOffset, copyToBufferCount);
                    _seekBackBufferCount = Math.Min(_seekBackBuffer.Length, _seekBackBufferCount + copyToBufferCount);
                    _seekBackBufferIndex = _seekBackBufferCount;
                }
            }
            return copiedFromBackBufferCount + bytesReadFromUnderlying;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.End)
            {
                return SeekFromEnd((int)Math.Max(0, -offset));
            }

            long relativeOffset = origin == SeekOrigin.Current
                ? offset
                : offset - Position;

            if (relativeOffset == 0)
            {
                return Position;
            }
            else if (relativeOffset > 0)
            {
                return SeekForward(relativeOffset);
            }
            else
            {
                return SeekBackwards(-relativeOffset);
            }
        }

        private long SeekForward(long origOffset)
        {
            long offset = origOffset;
            int seekBackBufferLength = _seekBackBuffer.Length;

            int backwardSoughtBytes = _seekBackBufferCount - _seekBackBufferIndex;
            int seekForwardInBackBuffer = (int)Math.Min(offset, backwardSoughtBytes);
            offset -= seekForwardInBackBuffer;
            _seekBackBufferIndex += seekForwardInBackBuffer;

            if (offset > 0)
            {
                // first completely fill seekBackBuffer to remove special cases from while loop below
                if (_seekBackBufferCount < seekBackBufferLength)
                {
                    int maxRead = seekBackBufferLength - _seekBackBufferCount;
                    if (offset < maxRead)
                    {
                        maxRead = (int)offset;
                    }

                    int bytesRead = _underlyingStream.Read(_seekBackBuffer, _seekBackBufferCount, maxRead);
                    _underlyingPosition += bytesRead;
                    _seekBackBufferCount += bytesRead;
                    _seekBackBufferIndex = _seekBackBufferCount;
                    if (bytesRead < maxRead)
                    {
                        if (_seekBackBufferCount < offset)
                        {
                            throw new NotSupportedException("Reached end of stream seeking forward " + origOffset + " bytes");
                        }

                        return Position;
                    }
                    offset -= bytesRead;
                }

                // now alternate between filling tempBuffer and seekBackBuffer
                bool fillTempBuffer = true;
                byte[] tempBuffer = new byte[seekBackBufferLength];
                while (offset > 0)
                {
                    int maxRead = offset < seekBackBufferLength ? (int)offset : seekBackBufferLength;
                    int bytesRead = _underlyingStream.Read(fillTempBuffer ? tempBuffer : _seekBackBuffer, 0, maxRead);
                    _underlyingPosition += bytesRead;
                    int bytesReadDiff = maxRead - bytesRead;
                    offset -= bytesRead;
                    if (bytesReadDiff > 0 /* reached end-of-stream */ || offset == 0)
                    {
                        if (fillTempBuffer)
                        {
                            if (bytesRead > 0)
                            {
                                Buffer.BlockCopy(_seekBackBuffer, bytesRead, _seekBackBuffer, 0, bytesReadDiff);
                                Buffer.BlockCopy(tempBuffer, 0, _seekBackBuffer, bytesReadDiff, bytesRead);
                            }
                        }
                        else
                        {
                            if (bytesRead > 0)
                            {
                                Buffer.BlockCopy(_seekBackBuffer, 0, _seekBackBuffer, bytesReadDiff, bytesRead);
                            }

                            Buffer.BlockCopy(tempBuffer, bytesRead, _seekBackBuffer, 0, bytesReadDiff);
                        }
                        if (offset > 0)
                        {
                            throw new NotSupportedException("Reached end of stream seeking forward " + origOffset + " bytes");
                        }
                    }
                    fillTempBuffer = !fillTempBuffer;
                }
            }
            return Position;
        }

        private long SeekBackwards(long offset)
        {
            int intOffset = (int)offset;
            if (offset > int.MaxValue || intOffset > _seekBackBufferIndex)
            {
                throw new NotSupportedException("Cannot currently seek backwards more than " + _seekBackBufferIndex + " bytes");
            }

            _seekBackBufferIndex -= intOffset;
            return Position;
        }

        private long SeekFromEnd(long offset)
        {
            int intOffset = (int)offset;
            int seekBackBufferLength = _seekBackBuffer.Length;
            if (offset > int.MaxValue || intOffset > seekBackBufferLength)
            {
                throw new NotSupportedException("Cannot seek backwards from end more than " + seekBackBufferLength + " bytes");
            }

            // first completely fill seekBackBuffer to remove special cases from while loop below
            if (_seekBackBufferCount < seekBackBufferLength)
            {
                int maxRead = seekBackBufferLength - _seekBackBufferCount;
                int bytesRead = _underlyingStream.Read(_seekBackBuffer, _seekBackBufferCount, maxRead);
                _underlyingPosition += bytesRead;
                _seekBackBufferCount += bytesRead;
                _seekBackBufferIndex = Math.Max(0, _seekBackBufferCount - intOffset);
                if (bytesRead < maxRead)
                {
                    if (_seekBackBufferCount < intOffset)
                    {
                        throw new NotSupportedException("Could not seek backwards from end " + intOffset + " bytes");
                    }

                    return Position;
                }
            }
            else
            {
                _seekBackBufferIndex = _seekBackBufferCount;
            }

            // now alternate between filling tempBuffer and seekBackBuffer
            bool fillTempBuffer = true;
            byte[] tempBuffer = new byte[seekBackBufferLength];
            while (true)
            {
                int bytesRead = _underlyingStream.Read(fillTempBuffer ? tempBuffer : _seekBackBuffer, 0, seekBackBufferLength);
                _underlyingPosition += bytesRead;
                int bytesReadDiff = seekBackBufferLength - bytesRead;
                if (bytesReadDiff > 0) // reached end-of-stream
                {
                    if (fillTempBuffer)
                    {
                        if (bytesRead > 0)
                        {
                            Buffer.BlockCopy(_seekBackBuffer, bytesRead, _seekBackBuffer, 0, bytesReadDiff);
                            Buffer.BlockCopy(tempBuffer, 0, _seekBackBuffer, bytesReadDiff, bytesRead);
                        }
                    }
                    else
                    {
                        if (bytesRead > 0)
                        {
                            Buffer.BlockCopy(_seekBackBuffer, 0, _seekBackBuffer, bytesReadDiff, bytesRead);
                        }

                        Buffer.BlockCopy(tempBuffer, bytesRead, _seekBackBuffer, 0, bytesReadDiff);
                    }
                    _seekBackBufferIndex -= intOffset;
                    return Position;
                }
                fillTempBuffer = !fillTempBuffer;
            }
        }

        public override long Position
        {
            get
            {
                return _underlyingPosition - (_seekBackBufferCount - _seekBackBufferIndex);
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
                return _underlyingStream.CanTimeout;
            }
        }
        public override bool CanWrite
        {
            get
            {
                return _underlyingStream.CanWrite;
            }
        }
        public override long Length
        {
            get
            {
                return _underlyingStream.Length;
            }
        }
        public override void SetLength(long value)
        {
            _underlyingStream.SetLength(value);
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            _underlyingStream.Write(buffer, offset, count);
        }
        public override void Flush()
        {
            _underlyingStream.Flush();
        }
        public override void Close()
        {
            _underlyingStream.Close();
        }
        public new void Dispose()
        {
            _underlyingStream.Dispose();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _underlyingStream.Dispose();
            }
        }
    }
}
