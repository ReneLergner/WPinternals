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

using MadWizard.WinUSBNet;
using System;
using System.IO.Ports;

namespace WPinternals
{
    internal class QualcommSerial : IDisposable
    {
        private bool Disposed = false;
        private readonly SerialPort Port = null;
        private readonly USBDevice USBDevice = null;
        private readonly CRC16 CRC16;

        public bool EncodeCommands = true;
        public bool DecodeResponses = true;

        public QualcommSerial(string DevicePath)
        {
            CRC16 = new CRC16(0x1189, 0xFFFF, 0xFFFF);

            string[] DevicePathElements = DevicePath.Split(['#']);
            if (string.Equals(DevicePathElements[3], "{86E0D1E0-8089-11D0-9CE4-08003E301F73}", StringComparison.CurrentCultureIgnoreCase))
            {
                string PortName = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\USB\" + DevicePathElements[1] + @"\" + DevicePathElements[2] + @"\Device Parameters", "PortName", null);
                if (PortName != null)
                {
                    Port = new SerialPort(PortName, 115200)
                    {
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };
                    Port.Open();
                }
            }
            else
            {
                try
                {
                    this.USBDevice = new USBDevice(DevicePath);
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
            }
        }

        public void SendData(byte[] Data)
        {
            byte[] FormattedData = EncodeCommands ? FormatCommand(Data) : Data;
            Port?.Write(FormattedData, 0, FormattedData.Length);
            if (USBDevice != null)
            {
                USBDevice.OutputPipe.Write(FormattedData);
            }
        }

        public byte[] SendCommand(byte[] Command, byte[] ResponsePattern)
        {
            byte[] FormattedCommand = EncodeCommands ? FormatCommand(Command) : Command;
            Port?.Write(FormattedCommand, 0, FormattedCommand.Length);
            if (USBDevice != null)
            {
                USBDevice.OutputPipe.Write(FormattedCommand);
            }

            return GetResponse(ResponsePattern);
        }

        internal byte[] GetResponse(byte[] ResponsePattern, int Length = 0x2000)
        {
            byte[] ResponseBuffer = new byte[Length];
            Length = 0;
            bool IsIncomplete = false;

            do
            {
                IsIncomplete = false;

                try
                {
                    int BytesRead = 0;

                    if (Port != null)
                    {
                        BytesRead = Port.Read(ResponseBuffer, Length, ResponseBuffer.Length - Length);
                    }

                    if (USBDevice != null)
                    {
                        BytesRead = USBDevice.InputPipe.Read(ResponseBuffer);
                    }

                    if (BytesRead == 0)
                    {
                        LogFile.Log("Emergency mode of phone is ignoring us", LogType.FileAndConsole);
                        throw new BadMessageException();
                    }

                    Length += BytesRead;
                    byte[] DecodedResponse;
                    if (DecodeResponses)
                    {
                        DecodedResponse = DecodeResponse(ResponseBuffer, (UInt32)Length);
                    }
                    else
                    {
                        DecodedResponse = new byte[Length];
                        Buffer.BlockCopy(ResponseBuffer, 0, DecodedResponse, 0, Length);
                    }

                    if (ResponsePattern != null)
                    {
                        for (int i = 0; i < ResponsePattern.Length; i++)
                        {
                            if (DecodedResponse[i] != ResponsePattern[i])
                            {
                                byte[] LogResponse = new byte[DecodedResponse.Length < 0x10 ? DecodedResponse.Length : 0x10];
                                LogFile.Log("Qualcomm serial response: " + Converter.ConvertHexToString(LogResponse, ""), LogType.FileOnly);
                                LogFile.Log("Expected: " + Converter.ConvertHexToString(ResponsePattern, ""), LogType.FileOnly);
                                throw new BadMessageException();
                            }
                        }
                    }

                    return DecodedResponse;
                }
                catch (IncompleteMessageException)
                {
                    IsIncomplete = true;
                }
                catch (Exception ex) // Will be rethrown as BadConnectionException
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
            }
            while (IsIncomplete);

            Port?.DiscardInBuffer();
            if (USBDevice != null)
            {
                USBDevice.InputPipe.Flush();
            }

            throw new BadConnectionException();
        }

        private byte[] FormatCommand(byte[] Command)
        {
            if ((Command == null) || (Command.Length == 0))
            {
                throw new BadMessageException();
            }

            byte[] Decoded = new byte[(Command.Length * 2) + 4];
            int Length = 0;

            Decoded[Length++] = 0x7E;

            for (int i = 0; i < Command.Length; i++)
            {
                if ((Command[i] == 0x7D) || (Command[i] == 0x7E))
                {
                    Decoded[Length++] = 0x7D;
                    Decoded[Length++] = (byte)(Command[i] ^ 0x20);
                }
                else
                {
                    Decoded[Length++] = Command[i];
                }
            }

            UInt16 Checksum = CRC16.CalculateChecksum(Command);
            if (((byte)(Checksum & 0xFF) == 0x7D) || ((byte)(Checksum & 0xFF) == 0x7E))
            {
                Decoded[Length++] = 0x7D;
                Decoded[Length++] = (byte)((Checksum & 0xFF) ^ 0x20);
            }
            else
            {
                Decoded[Length++] = (byte)(Checksum & 0xFF);
            }

            if (((byte)(Checksum >> 8) == 0x7D) || ((byte)(Checksum >> 8) == 0x7E))
            {
                Decoded[Length++] = 0x7D;
                Decoded[Length++] = (byte)((Checksum >> 8) ^ 0x20);
            }
            else
            {
                Decoded[Length++] = (byte)(Checksum >> 8);
            }

            Decoded[Length++] = 0x7E;

            if (Length > 0)
            {
                byte[] Result = new byte[Length];
                Buffer.BlockCopy(Decoded, 0, Result, 0, Length);
                return Result;
            }
            else
            {
                return null;
            }
        }

        private byte[] DecodeResponse(byte[] Response, UInt32 Length)
        {
            if ((Response == null) || (Response.Length == 0) || (Response[0] != 0x7E))
            {
                throw new BadMessageException();
            }

            UInt32 SourceLength = Length;
            Length = 0;
            UInt32 SourcePos = 1;

            byte[] Message = new byte[SourceLength];

            while (SourcePos < SourceLength)
            {
                if (Response[SourcePos] == 0x7E)
                {
                    break;
                }

                Message[Length++] = Response[SourcePos] == 0x7D ? (byte)(Response[++SourcePos] ^ 0x20) : Response[SourcePos];

                SourcePos++;
            }

            if (SourcePos == SourceLength)
            {
                throw new IncompleteMessageException();
            }

            if (Length < 3)
            {
                throw new BadMessageException();
            }

            byte[] TrimmedMessage = new byte[Length - 2];
            Buffer.BlockCopy(Message, 0, TrimmedMessage, 0, (int)(Length - 2));

            UInt16 Checksum = CRC16.CalculateChecksum(TrimmedMessage);
            if (((byte)(Checksum & 0xFF) != Message[Length - 2]) || ((byte)(Checksum >> 8) != Message[Length - 1]))
            {
                throw new BadMessageException();
            }

            return TrimmedMessage;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~QualcommSerial()
        {
            Dispose(false);
        }

        public void Close()
        {
            Port?.Close();
            USBDevice?.Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return;
            }

            if (disposing)
            {
                // Other disposables
            }

            // Clean unmanaged resources here.
            Close();

            Disposed = true;
        }

        internal void SetTimeOut(int v)
        {
            if (USBDevice != null)
            {
                USBDevice.ControlPipeTimeout = v;
            }

            if (Port != null)
            {
                Port.ReadTimeout = v;
                Port.WriteTimeout = v;
            }
        }
    }

    public class IncompleteMessageException : Exception { public IncompleteMessageException() { } public IncompleteMessageException(string message) : base(message) { } public IncompleteMessageException(string message, Exception innerException) : base(message, innerException) { } }
    public class BadMessageException : Exception { public BadMessageException() { } public BadMessageException(string message) : base(message) { } public BadMessageException(string message, Exception innerException) : base(message, innerException) { } }
    public class BadConnectionException : Exception { public BadConnectionException() { } public BadConnectionException(string message) : base(message) { } public BadConnectionException(string message, Exception innerException) : base(message, innerException) { } }

    public class CRC16
    {
        private readonly UInt16[] ChecksumTable =
            [
                0x0000, 0x1189, 0x2312, 0x329b, 0x4624, 0x57ad, 0x6536, 0x74bf,
                0x8c48, 0x9dc1, 0xaf5a, 0xbed3, 0xca6c, 0xdbe5, 0xe97e, 0xf8f7,
                0x1081, 0x0108, 0x3393, 0x221a, 0x56a5, 0x472c, 0x75b7, 0x643e,
                0x9cc9, 0x8d40, 0xbfdb, 0xae52, 0xdaed, 0xcb64, 0xf9ff, 0xe876,
                0x2102, 0x308b, 0x0210, 0x1399, 0x6726, 0x76af, 0x4434, 0x55bd,
                0xad4a, 0xbcc3, 0x8e58, 0x9fd1, 0xeb6e, 0xfae7, 0xc87c, 0xd9f5,
                0x3183, 0x200a, 0x1291, 0x0318, 0x77a7, 0x662e, 0x54b5, 0x453c,
                0xbdcb, 0xac42, 0x9ed9, 0x8f50, 0xfbef, 0xea66, 0xd8fd, 0xc974,
                0x4204, 0x538d, 0x6116, 0x709f, 0x0420, 0x15a9, 0x2732, 0x36bb,
                0xce4c, 0xdfc5, 0xed5e, 0xfcd7, 0x8868, 0x99e1, 0xab7a, 0xbaf3,
                0x5285, 0x430c, 0x7197, 0x601e, 0x14a1, 0x0528, 0x37b3, 0x263a,
                0xdecd, 0xcf44, 0xfddf, 0xec56, 0x98e9, 0x8960, 0xbbfb, 0xaa72,
                0x6306, 0x728f, 0x4014, 0x519d, 0x2522, 0x34ab, 0x0630, 0x17b9,
                0xef4e, 0xfec7, 0xcc5c, 0xddd5, 0xa96a, 0xb8e3, 0x8a78, 0x9bf1,
                0x7387, 0x620e, 0x5095, 0x411c, 0x35a3, 0x242a, 0x16b1, 0x0738,
                0xffcf, 0xee46, 0xdcdd, 0xcd54, 0xb9eb, 0xa862, 0x9af9, 0x8b70,
                0x8408, 0x9581, 0xa71a, 0xb693, 0xc22c, 0xd3a5, 0xe13e, 0xf0b7,
                0x0840, 0x19c9, 0x2b52, 0x3adb, 0x4e64, 0x5fed, 0x6d76, 0x7cff,
                0x9489, 0x8500, 0xb79b, 0xa612, 0xd2ad, 0xc324, 0xf1bf, 0xe036,
                0x18c1, 0x0948, 0x3bd3, 0x2a5a, 0x5ee5, 0x4f6c, 0x7df7, 0x6c7e,
                0xa50a, 0xb483, 0x8618, 0x9791, 0xe32e, 0xf2a7, 0xc03c, 0xd1b5,
                0x2942, 0x38cb, 0x0a50, 0x1bd9, 0x6f66, 0x7eef, 0x4c74, 0x5dfd,
                0xb58b, 0xa402, 0x9699, 0x8710, 0xf3af, 0xe226, 0xd0bd, 0xc134,
                0x39c3, 0x284a, 0x1ad1, 0x0b58, 0x7fe7, 0x6e6e, 0x5cf5, 0x4d7c,
                0xc60c, 0xd785, 0xe51e, 0xf497, 0x8028, 0x91a1, 0xa33a, 0xb2b3,
                0x4a44, 0x5bcd, 0x6956, 0x78df, 0x0c60, 0x1de9, 0x2f72, 0x3efb,
                0xd68d, 0xc704, 0xf59f, 0xe416, 0x90a9, 0x8120, 0xb3bb, 0xa232,
                0x5ac5, 0x4b4c, 0x79d7, 0x685e, 0x1ce1, 0x0d68, 0x3ff3, 0x2e7a,
                0xe70e, 0xf687, 0xc41c, 0xd595, 0xa12a, 0xb0a3, 0x8238, 0x93b1,
                0x6b46, 0x7acf, 0x4854, 0x59dd, 0x2d62, 0x3ceb, 0x0e70, 0x1ff9,
                0xf78f, 0xe606, 0xd49d, 0xc514, 0xb1ab, 0xa022, 0x92b9, 0x8330,
                0x7bc7, 0x6a4e, 0x58d5, 0x495c, 0x3de3, 0x2c6a, 0x1ef1, 0x0f78
            ];

        private readonly UInt16 Seed, FinalXor;

        public CRC16(UInt16 Polynomial, UInt16 Seed, UInt16 FinalXor)
        {
            this.Seed = Seed;
            this.FinalXor = FinalXor;
        }

        public UInt16 CalculateChecksum(byte[] Bytes)
        {
            UInt16 Crc = Seed;
            for (int i = 0; i < Bytes.Length; ++i)
            {
                Crc = (UInt16)((Crc >> 8) ^ ChecksumTable[(byte)(Crc ^ Bytes[i])]); // Qualcomm implementation
            }
            return (UInt16)(Crc ^ FinalXor);
        }
    }
}
