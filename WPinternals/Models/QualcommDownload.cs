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
using System.Linq;

namespace WPinternals
{
    internal class QualcommDownload
    {
        private readonly QualcommSerial Serial;

        public QualcommDownload(QualcommSerial Serial)
        {
            this.Serial = Serial;
        }

        public bool IsAlive()
        {
            try
            {
                Serial.SendCommand([0x06], [0x02]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SendToPhoneMemory(UInt32 Address, Stream Data, UInt32 Length = UInt32.MaxValue)
        {
            long Remaining = Length > (Data.Length - Data.Position) ? Data.Length - Data.Position : Length;
            UInt32 CurrentLength;
            byte[] Buffer = new byte[0x107];
            Buffer[0] = 0x0F;
            System.Buffer.BlockCopy(BitConverter.GetBytes((UInt16)0x100).Reverse().ToArray(), 0, Buffer, 5, 2); // Length is in Big Endian
            UInt32 CurrentAddress = Address;
            while (Remaining > 0)
            {
                System.Buffer.BlockCopy(BitConverter.GetBytes(CurrentAddress).Reverse().ToArray(), 0, Buffer, 1, 4); // Address is in Big Endian

                CurrentLength = Remaining >= 0x100 ? 0x100 : (UInt32)Remaining;

                CurrentLength = (UInt32)Data.Read(Buffer, 7, (int)CurrentLength);
                Serial.SendCommand(Buffer, [0x02]);

                CurrentAddress += CurrentLength;
                Remaining -= CurrentLength;
            }
        }

        public void SendToPhoneMemory(UInt32 Address, byte[] Data, UInt32 Offset = 0, UInt32 Length = UInt32.MaxValue)
        {
            long Remaining;
            if (Offset > (Data.Length - 1))
            {
                throw new ArgumentException("Wrong offset");
            }

            Remaining = Length > (Data.Length - Offset) ? Data.Length - Offset : Length;

            UInt32 CurrentLength;
            UInt32 CurrentOffset = Offset;
            byte[] Buffer = new byte[0x107];
            UInt32 CurrentAddress = Address;
            byte[] CurrentBytes;
            while (Remaining > 0)
            {
                if (Remaining >= 0x100)
                {
                    CurrentLength = 0x100;
                    CurrentBytes = Buffer;
                }
                else
                {
                    CurrentLength = (UInt32)Remaining;
                    CurrentBytes = new byte[CurrentLength + 7];
                }
                CurrentBytes[0] = 0x0F;
                System.Buffer.BlockCopy(BitConverter.GetBytes(CurrentAddress).Reverse().ToArray(), 0, CurrentBytes, 1, 4); // Address is in Big Endian
                System.Buffer.BlockCopy(BitConverter.GetBytes((UInt16)CurrentLength).Reverse().ToArray(), 0, CurrentBytes, 5, 2); // Length is in Big Endian
                System.Buffer.BlockCopy(Data, (int)CurrentOffset, CurrentBytes, 7, (int)CurrentLength);

                Serial.SendCommand(CurrentBytes, [0x02]);

                CurrentAddress += CurrentLength;
                CurrentOffset += CurrentLength;
                Remaining -= CurrentLength;
            }
        }

        public void StartBootloader(UInt32 Address)
        {
            byte[] Buffer = new byte[5];
            Buffer[0] = 0x05;
            System.Buffer.BlockCopy(BitConverter.GetBytes(Address).Reverse().ToArray(), 0, Buffer, 1, 4); // Address is in Big Endian
            Serial.SendCommand(Buffer, [0x02]);
        }

        // Reset interface. Interface becomes unresponsive.
        public void Reset()
        {
            Serial.SendCommand([0x0A], [0x02]);
        }

        // This also resets interface. This does not actually reboot the phone. The interface becomes unresponsive.
        public void Shutdown()
        {
            Serial.SendCommand([0x0E], [0x02]);
        }

        // This command only works on 9008 interface.
        public byte[] GetRKH()
        {
            byte[] Response = Serial.SendCommand([0x18], [0x18, 0x01, 0x00]);
            byte[] Result = new byte[0x20];
            Buffer.BlockCopy(Response, 3, Result, 0, 0x20);
            return Result;
        }

        public void CloseSerial()
        {
            Serial.Close();
        }
    }
}
