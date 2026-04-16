/*
* MIT License
* 
* Copyright (c) 2024 The DuoWOA authors
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/
using MadWizard.WinUSBNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnifiedFlashingPlatform.UEFI;
using WPinternals.HelperClasses;

namespace UnifiedFlashingPlatform
{
    public partial class UnifiedFlashingPlatformModel : IDisposable
    {
        private bool Disposed = false;
        private readonly USBDevice USBDevice;
        private readonly USBPipe InputPipe;
        private readonly USBPipe OutputPipe;
        private readonly object UsbLock = new();

        public UnifiedFlashingPlatformModel(string DevicePath)
        {
            USBDevice = new USBDevice(DevicePath);

            foreach (USBPipe Pipe in USBDevice.Pipes)
            {
                if (Pipe.IsIn)
                {
                    InputPipe = Pipe;
                }

                if (Pipe.IsOut)
                {
                    OutputPipe = Pipe;
                }
            }

            if (InputPipe == null || OutputPipe == null)
            {
                throw new Exception("Invalid USB device!");
            }
        }

        public byte[]? ExecuteRawMethod(byte[] RawMethod)
        {
            return ExecuteRawMethod(RawMethod, RawMethod.Length);
        }

        public byte[]? ExecuteRawMethod(byte[] RawMethod, int Length)
        {
            byte[] Buffer = new byte[0xF000]; // Should be at least 0x4408 for receiving the GPT packet.
            byte[]? Result = null;
            lock (UsbLock)
            {
                OutputPipe.Write(RawMethod, 0, Length);
                try
                {
                    int OutputLength = InputPipe.Read(Buffer);
                    Result = new byte[OutputLength];
                    System.Buffer.BlockCopy(Buffer, 0, Result, 0, OutputLength);
                }
                catch { } // Reboot command looses connection
            }
            return Result;
        }

        public void ExecuteRawVoidMethod(byte[] RawMethod)
        {
            ExecuteRawVoidMethod(RawMethod, RawMethod.Length);
        }

        public void ExecuteRawVoidMethod(byte[] RawMethod, int Length)
        {
            lock (UsbLock)
            {
                OutputPipe.Write(RawMethod, 0, Length);
            }
        }

        public void Relock()
        {
            byte[] Request = new byte[7];
            const string Header = RelockSignature; // NOKXFO
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public void MassStorage()
        {
            byte[] Request = new byte[7];
            const string Header = MassStorageSignature; // NOKM
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public void RebootPhone()
        {
            byte[] Request = new byte[7];
            const string Header = $"{SwitchModeSignature}R"; // NOKXCBR
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public void SwitchToUFP()
        {
            byte[] Request = new byte[7];
            const string Header = $"{SwitchModeSignature}U"; // NOKXCBU
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public void ContinueBoot()
        {
            byte[] Request = new byte[7];
            const string Header = $"{SwitchModeSignature}W"; // NOKXCBW
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public void PowerOff()
        {
            byte[] Request = new byte[7];
            const string Header = $"{SwitchModeSignature}Z"; // NOKXCBZ
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }

        public void TransitionToUFPBootApp()
        {
            byte[] Request = new byte[7];
            const string Header = $"{SwitchModeSignature}T"; // NOKXCBT
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }

        public void DisplayCustomMessage(string Message, ushort Row)
        {
            byte[] MessageBuffer = Encoding.Unicode.GetBytes(Message);
            byte[] Request = new byte[8 + MessageBuffer.Length];
            const string Header = DisplayCustomMessageSignature; // NOKXCM

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(Row).Reverse().ToArray(), 0, Request, 6, 2);
            Buffer.BlockCopy(MessageBuffer, 0, Request, 8, MessageBuffer.Length);

            _ = ExecuteRawMethod(Request);
        }

        public void ClearScreen()
        {
            byte[] Request = new byte[6];
            const string Header = ClearScreenSignature; // NOKXCC
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            _ = ExecuteRawMethod(Request);
        }

        public byte[]? Echo(byte[] DataPayload)
        {
            byte[] Request = new byte[10 + DataPayload.Length];
            const string Header = EchoSignature; // NOKXCE

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(DataPayload.Length).Reverse().ToArray(), 0, Request, 6, 4);
            Buffer.BlockCopy(DataPayload, 0, Request, 10, DataPayload.Length);

            byte[]? Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 6 + DataPayload.Length))
            {
                return null;
            }

            byte[] Result = new byte[DataPayload.Length];
            Buffer.BlockCopy(Response, 6, Result, 0, DataPayload.Length);
            return Result;
        }

        public FILE_INFO[]? GetDirectoryEntries(string PartitionName, string DirectoryName)
        {
            ulong? size = ReadDirectoryEntriesSize(PartitionName, DirectoryName);
            if (size == null)
            {
                return null;
            }

            return GetDirectoryEntries(PartitionName, DirectoryName, size.Value);
        }

        private FILE_INFO[]? GetDirectoryEntries(string PartitionName, string DirectoryName, ulong DataStructSize)
        {
            if (PartitionName.Length > 35)
            {
                return null;
            }

            byte[] PartitionNameBuffer = Encoding.Unicode.GetBytes(PartitionName);
            byte[] DirectoryNameBuffer = Encoding.Unicode.GetBytes(DirectoryName);

            byte[] Request = new byte[1114];
            const string Header = GetDirectoryEntriesSignature; // NOKXCD

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);

            Buffer.BlockCopy(PartitionNameBuffer, 0, Request, 6, PartitionNameBuffer.Length);
            Buffer.BlockCopy(DirectoryNameBuffer, 0, Request, 78, DirectoryNameBuffer.Length); // 512 max size (1024 for unicode)

            Buffer.BlockCopy(BigEndian.GetBytes((int)DataStructSize, 4), 0, Request, 1102, 4);

            // TODO: Investigate data size
            //Buffer.BlockCopy(BigEndian.GetBytes(360, 8), 0, Request, 1106, 8);

            byte[]? Response = ExecuteRawMethod(Request);
            if ((Response == null) || (Response.Length < 0x10))
            {
                return null;
            }

            int ResultCode = (Response[6] << 8) + Response[7];
            if (ResultCode != 0)
            {
                ThrowFlashError(ResultCode);
            }

            int ResponseLength = BigEndian.ToInt32(Response, 8);

            byte[] Result = new byte[ResponseLength];
            Buffer.BlockCopy(Response, 12, Result, 0, ResponseLength);

            List<FILE_INFO> directoryEntries = [];

            int j = 0;
            while (j != Result.Length)
            {
                FILE_INFO directoryEntry = FILE_INFO.ReadFromBuffer(Result, j);
                j += (int)directoryEntry.Size;

                directoryEntries.Add(directoryEntry);
            }

            return [.. directoryEntries];
        }

        public void TelemetryStart()
        {
            byte[] Request = new byte[4];
            const string Header = TelemetryStartSignature; // NOKS
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }

        public void TelemetryEnd()
        {
            byte[] Request = new byte[4];
            const string Header = TelemetryEndSignature; // NOKN
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawVoidMethod(Request);
        }

        // WIP!
        public string? ReadLog()
        {
            byte[] Request = new byte[0x13];
            const string Header = GetLogsSignature;
            ulong BufferSize = 0xE000 - 0xC;

            ulong Length = ReadLogSize(DeviceLogType.Flashing)!.Value;
            if (Length == 0)
            {
                return null;
            }

            string LogContent = "";

            for (ulong i = 0; i < Length; i += BufferSize)
            {
                if (i + BufferSize > Length)
                {
                    BufferSize = Length - i;
                }
                uint BufferSizeInt = (uint)BufferSize;

                Buffer.BlockCopy(Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
                Request[6] = 1;
                Buffer.BlockCopy(BitConverter.GetBytes(BufferSizeInt).Reverse().ToArray(), 0, Request, 7, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(i).Reverse().ToArray(), 0, Request, 11, 8);

                byte[]? Response = ExecuteRawMethod(Request);
                if ((Response == null) || (Response.Length < 0xC))
                {
                    return null;
                }

                int ResultLength = Response.Length - 0xC;
                byte[] Result = new byte[ResultLength];
                Buffer.BlockCopy(Response, 0xC, Result, 0, ResultLength);

                string PartialLogContent = Encoding.ASCII.GetString(Result);

                LogContent += PartialLogContent;
            }

            return LogContent;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~UnifiedFlashingPlatformModel()
        {
            Dispose(false);
        }

        public void Close()
        {
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
    }
}