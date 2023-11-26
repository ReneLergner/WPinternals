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

// SevenZip LZMA SDK: http://www.7-zip.org/download.html
// Usage: http://stackoverflow.com/questions/7646328/how-to-use-the-7z-sdk-to-compress-and-decompress-a-file

using SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace WPinternals
{
    internal static class LZMA
    {
        internal static byte[] Decompress(byte[] Input, UInt32 Offset, UInt32 InputSize)
        {
            byte[] Properties = new byte[5];
            Buffer.BlockCopy(Input, (int)Offset, Properties, 0, 5);

            UInt64 OutputSize = ByteOperations.ReadUInt64(Input, Offset + 5);

            SevenZip.Compression.LZMA.Decoder Coder = new();
            Coder.SetDecoderProperties(Properties);

            MemoryStream InStream = new(Input, (int)Offset + 0x0D, (int)InputSize - 0x0D);

            byte[] Output = new byte[OutputSize];
            MemoryStream OutStream = new(Output, true);

            Coder.Code(InStream, OutStream, (Int64)InputSize - 0x0D, (Int64)OutputSize, null);

            OutStream.Flush();
            OutStream.Close();
            InStream.Close();

            return Output;
        }

        internal static byte[] Compress(byte[] Input, UInt32 Offset, UInt32 InputSize)
        {
            SevenZip.Compression.LZMA.Encoder Coder = new();

            MemoryStream InStream = new(Input, (int)Offset, (int)InputSize);
            MemoryStream OutStream = new();

            // Write the encoder properties
            Coder.WriteCoderProperties(OutStream);

            // Write the decompressed file size
            OutStream.Write(BitConverter.GetBytes(InStream.Length), 0, 8);

            // Encode the file
            Coder.Code(InStream, OutStream, InputSize, -1, null);

            byte[] Output = new byte[OutStream.Length];
            Buffer.BlockCopy(OutStream.GetBuffer(), 0, Output, 0, (int)OutStream.Length);

            OutStream.Flush();
            OutStream.Close();
            InStream.Close();

            return Output;
        }
    }

    public class LZMACompressionStream : Stream
    {
        private readonly SevenZip.Compression.LZMA.Encoder Encoder = null;
        private readonly SevenZip.Compression.LZMA.Decoder Decoder = null;
        private readonly PumpStream BufferStream;
        private readonly Stream stream;
        private readonly bool LeaveOpen;
        private readonly Thread WorkThread;
        private readonly CancellationTokenSource source;
        private readonly CancellationToken token;

        public LZMACompressionStream(Stream stream, CompressionMode mode, bool LeaveOpen, int DictionarySize, int PosStateBits,
           int LitContextBits, int LitPosBits, int Algorithm, int NumFastBytes, string MatchFinder, bool EndMarker)
        {
            this.stream = stream;
            this.LeaveOpen = LeaveOpen;
            BufferStream = new PumpStream();
            source = new CancellationTokenSource();
            token = source.Token;

            if (mode == CompressionMode.Compress)
            {
                Encoder = new SevenZip.Compression.LZMA.Encoder();
                if (DictionarySize != 0)
                {
                    Encoder.SetCoderProperties(
                      [CoderPropID.DictionarySize, CoderPropID.PosStateBits, CoderPropID.LitContextBits,
                        CoderPropID.LitPosBits, CoderPropID.Algorithm, CoderPropID.NumFastBytes, CoderPropID.MatchFinder, CoderPropID.EndMarker],
                      [DictionarySize, PosStateBits, LitContextBits, LitPosBits, Algorithm, NumFastBytes, MatchFinder, EndMarker]);
                }

                Encoder.WriteCoderProperties(stream);
                WorkThread = new Thread(new ThreadStart(Encode));
            }
            else
            {
                byte[] DecoderProperties = new byte[5];
                stream.Read(DecoderProperties, 0, 5);
                Decoder = new SevenZip.Compression.LZMA.Decoder();
                Decoder.SetDecoderProperties(DecoderProperties);
                WorkThread = new Thread(new ThreadStart(Decode));
            }

            WorkThread.Start();
        }

        public LZMACompressionStream(Stream stream, CompressionMode mode, bool LeaveOpen)
            : this(stream, mode, LeaveOpen, 0, 0, 0, 0, 0, 0, null, false)
        {
        }

        private void Encode()
        {
            Encoder.Code(BufferStream, stream, -1, -1, null, token);
            if (!LeaveOpen)
            {
                stream.Close();
            }
        }

        private void Decode()
        {
            Decoder.Code(stream, BufferStream, -1, -1, null, token);
            BufferStream.Close();
            if (!LeaveOpen)
            {
                stream.Close();
            }
        }

        public override void Close()
        {
            if (Encoder != null)
            {
                BufferStream.Close();
            }
            else if (WorkThread.IsAlive)
            {
                source?.Cancel();
                WorkThread.Join();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return BufferStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BufferStream.Write(buffer, offset, count);
        }

        public override bool CanRead { get { return Decoder != null; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return Encoder != null; } }
        public override void Flush() { }
        public override long Length { get { return 0; } }
        public override long Position { get { return 0; } set { } }
        public override long Seek(long offset, SeekOrigin origin) { return 0; }
        public override void SetLength(long value) { }
    }

    public class PumpStream : Stream
    {
        private readonly Queue<byte[]> BufferQueue;
        private int BufferOffset;
        private readonly long MaxBufferSize;
        private long BufferSize;
        private bool Closed;
        private bool EOF;

        public PumpStream(long MaxBufferSize, int ReadTimeout, int WriteTimeout)
        {
            this.MaxBufferSize = MaxBufferSize;
            this.ReadTimeout = ReadTimeout;
            this.WriteTimeout = WriteTimeout;
            BufferQueue = new Queue<byte[]>();
            BufferOffset = 0;
            BufferSize = 0;
            Closed = false;
            EOF = false;
        }

        public PumpStream()
            : this(16777216, Timeout.Infinite, Timeout.Infinite)
        {
        }

        public new void Dispose()
        {
            BufferQueue.Clear();
        }

        public override void Close()
        {
            Closed = true;
            lock (BufferQueue)
            {
                Monitor.Pulse(BufferQueue);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int BytesRead = 0;
            lock (BufferQueue)
            {
                while (BytesRead < count && !EOF)
                {
                    if (BufferQueue.Count > 0)
                    {
                        byte[] b = BufferQueue.Peek();

                        if ((b.Length - BufferOffset) <= (count - BytesRead))
                        {
                            Array.Copy(b, BufferOffset, buffer, offset + BytesRead, b.Length - BufferOffset);

                            BufferQueue.Dequeue();
                            BufferSize -= b.Length;
                            Monitor.Pulse(BufferQueue);

                            BytesRead += b.Length - BufferOffset;
                            BufferOffset = 0;
                        }
                        else
                        {
                            Array.Copy(b, BufferOffset, buffer, offset + BytesRead, count - BytesRead);

                            BufferOffset += count - BytesRead;
                            BytesRead += count - BytesRead;
                        }
                    }
                    else
                    {
                        if (!Closed)
                        {
                            if (!Monitor.Wait(BufferQueue, ReadTimeout))
                            {
                                throw new IOException("Could not read from stream: Timeout expired waiting for data to be written.");
                            }
                        }
                        else
                        {
                            EOF = true;
                        }
                    }
                }
            }

            return BytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (BufferQueue)
            {
                while (BufferSize >= MaxBufferSize)
                {
                    if (!Monitor.Wait(BufferQueue, WriteTimeout))
                    {
                        throw new IOException("Could not write to stream: Timeout expired waiting for data to be read.");
                    }
                }

                byte[] b = new byte[count];
                Array.Copy(buffer, offset, b, 0, count);
                BufferQueue.Enqueue(b);
                BufferSize += b.Length;

                Monitor.Pulse(BufferQueue);
            }
        }

        public override int ReadTimeout { get; set; }
        public override int WriteTimeout { get; set; }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override void Flush() { }
        public override long Length { get { return 0; } }
        public override long Position { get { return 0; } set { } }
        public override long Seek(long offset, SeekOrigin origin) { return 0; }
        public override void SetLength(long value) { }
    }
}
