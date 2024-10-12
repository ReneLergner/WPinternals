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
    internal static class BigEndian
    {
        public static byte[] GetBytes(object Value)
        {
            byte[] Bytes;
            if (Value is short)
            {
                Bytes = BitConverter.GetBytes((short)Value);
            }
            else if (Value is ushort)
            {
                Bytes = BitConverter.GetBytes((ushort)Value);
            }
            else if (Value is int)
            {
                Bytes = BitConverter.GetBytes((int)Value);
            }
            else
            {
                Bytes = Value is uint ? BitConverter.GetBytes((uint)Value) : throw new NotSupportedException();
            }

            byte[] Result = new byte[Bytes.Length];
            for (int i = 0; i < Bytes.Length; i++)
            {
                Result[i] = Bytes[Bytes.Length - 1 - i];
            }

            return Result;
        }

        public static byte[] GetBytes(object Value, int Width)
        {
            byte[] Result;
            byte[] BigEndianBytes = GetBytes(Value);
            if (BigEndianBytes.Length == Width)
            {
                return BigEndianBytes;
            }
            else if (BigEndianBytes.Length > Width)
            {
                Result = new byte[Width];
                Buffer.BlockCopy(BigEndianBytes, BigEndianBytes.Length - Width, Result, 0, Width);
                return Result;
            }
            else
            {
                Result = new byte[Width];
                Buffer.BlockCopy(BigEndianBytes, 0, Result, Width - BigEndianBytes.Length, BigEndianBytes.Length);
                return Result;
            }
        }

        public static ushort ToUInt16(byte[] Buffer, int Offset)
        {
            byte[] Bytes = new byte[2];
            for (int i = 0; i < 2; i++)
            {
                Bytes[i] = Buffer[Offset + 1 - i];
            }

            return BitConverter.ToUInt16(Bytes, 0);
        }

        public static short ToInt16(byte[] Buffer, int Offset)
        {
            byte[] Bytes = new byte[2];
            for (int i = 0; i < 2; i++)
            {
                Bytes[i] = Buffer[Offset + 1 - i];
            }

            return BitConverter.ToInt16(Bytes, 0);
        }

        public static uint ToUInt32(byte[] Buffer, int Offset)
        {
            byte[] Bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                Bytes[i] = Buffer[Offset + 3 - i];
            }

            return BitConverter.ToUInt32(Bytes, 0);
        }

        public static int ToInt32(byte[] Buffer, int Offset)
        {
            byte[] Bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                Bytes[i] = Buffer[Offset + 3 - i];
            }

            return BitConverter.ToInt32(Bytes, 0);
        }
    }
}
