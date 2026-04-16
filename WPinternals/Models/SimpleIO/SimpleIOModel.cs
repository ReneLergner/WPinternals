/*
* MIT License
* 
* Copyright (c) 2026 The DuoWOA authors
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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Resources;
using System.Text;

namespace WPinternals.Models.SimpleIO
{
    public partial class SimpleIOModel : IDisposable
    {
        // TODO: Use Locks!
        // TODO: Timeouts!!
        // TODO: Handle timeout
        // TODO: Check functionality on true proto
        // TODO: WIM Transfers!
        // TODO: FFU Transfers!
        // TODO: Missing command implementations nowhere to be found!

        private bool Disposed = false;
        private readonly USBDevice USBDevice;
        private readonly USBPipe InputPipe;
        private readonly USBPipe OutputPipe;
        private readonly object UsbLock = new();

        private bool HasCachedV2Results = false;
        private (long curPosition, Guid guid, bool supportsFastFlash, bool supportsCompatFastFlash, int clientVersion, Guid DeviceUniqueID, string DeviceFriendlyName) CachedV2Results = (0, new Guid(), false, false, 0, new Guid(), "");

        public SimpleIOModel(string DevicePath)
        {
            USBDevice = new USBDevice(DevicePath);

            // LineSetState, 1
            // Not sure why this is needed
            byte LineSetState = 34;
            USBDevice.ControlOut(33, LineSetState, 1, 0);

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

        public (long curPosition, Guid guid, bool supportsFastFlash, bool supportsCompatFastFlash, int clientVersion, Guid DeviceUniqueID, string DeviceFriendlyName) GetIdV2()
        {
            if (HasCachedV2Results)
            {
                return CachedV2Results;
            }

            int num = 0;

            (long curPosition, Guid guid, bool supportsFastFlash, bool supportsCompatFastFlash, int clientVersion, Guid DeviceUniqueID, string DeviceFriendlyName) ID;

            do
            {
                ID = GetId();
                num++;
            }
            while (!ID.supportsFastFlash && !ID.supportsCompatFastFlash && ID.clientVersion < 2 && num < COMPATFLASH_MagicSequence);

            CachedV2Results = ID;
            HasCachedV2Results = true;

            return ID;
        }

        private const int COMPATFLASH_MagicSequence = 1000;

        private const byte INDEX_SUPPORTCOMPATFLASH = 15;
        private const byte INDEX_SUPPORTV2CMDS = 14;
        private const byte INDEX_SUPPORTFASTFLASH = 0;

        // 1
        public (long curPosition, Guid guid, bool supportsFastFlash, bool supportsCompatFastFlash, int clientVersion, Guid DeviceUniqueID, string DeviceFriendlyName) GetId()
        {
            bool supportsFastFlash = false;
            bool supportsCompatFastFlash = false;
            int clientVersion = 1;

            byte[] buffer = ExecuteRawMethod([(byte)SioOpcode.SioId])!;
            using MemoryStream stream = new(buffer);
            using BinaryReader binaryReader = new(stream);

            long curPosition = binaryReader.ReadInt64();
            byte[] guidBuffer = binaryReader.ReadBytes(16);
            Guid guid = new(guidBuffer);

            if (guidBuffer[INDEX_SUPPORTFASTFLASH] >= 1)
            {
                supportsFastFlash = true;
            }
            else if (guidBuffer[INDEX_SUPPORTCOMPATFLASH] == 1)
            {
                supportsCompatFastFlash = true;
            }

            if (guidBuffer[INDEX_SUPPORTV2CMDS] >= 1)
            {
                clientVersion = guidBuffer[INDEX_SUPPORTV2CMDS] + 1;
            }

            Guid DeviceUniqueID = new(binaryReader.ReadBytes(16));
            string DeviceFriendlyName = binaryReader.ReadString();

            return (curPosition, guid, supportsFastFlash, supportsCompatFastFlash, clientVersion, DeviceUniqueID, DeviceFriendlyName);
        }

        // 7
        public bool ContinueBoot()
        {
            byte[] buffer = ExecuteRawMethod([(byte)SioOpcode.SioSkip])!;
            return buffer[0] == 3;
        }

        // 8
        public bool EndTransfer()
        {
            HasCachedV2Results = false;
            GetIdV2();

            if (CachedV2Results.curPosition == 0L)
            {
                return true;
            }

            ExecuteRawVoidMethod([(byte)SioOpcode.SioSkip]);

            byte[] array = new byte[16376];
            do
            {
                InputPipe.Read(array, 0, array.Length);
            }
            while (array[0] == 5);

            if (array[0] == 6)
            {
                HasCachedV2Results = false;
                GetIdV2();
                if (CachedV2Results.curPosition == 0L)
                {
                    return true;
                }
            }

            return false;
        }

        // 9
        public void FlashDataFile(string path)
        {
            /*string fileName = Path.GetFileName(path);
            this.InitFlashingStream();*/
            ExecuteRawVoidMethod([(byte)SioOpcode.SioFile]);
            /*this.packets.DataStream = this.GetStringStream(fileName);
            this.TransferPackets(false);
            this.WaitForEndResponse(false);
            this.packets.DataStream = this.GetBufferedFileStream(path);
            this.TransferPackets(false);
            this.WaitForEndResponse(false);*/
        }

        // 10
        public void Reboot()
        {
            ExecuteRawVoidMethod([(byte)SioOpcode.SioReboot]);
        }

        // 11
        public bool EnterMassStorage()
        {
            byte[] buffer = ExecuteRawMethod([(byte)SioOpcode.SioMassStorage])!;
            return buffer[0] == 3;
        }

        // 12
        public void ReadDiskInfo(out int transferSize, out uint blockSize, out ulong lastBlock)
        {
            ExecuteRawVoidMethod([(byte)SioOpcode.SioGetDiskInfo]);

            byte[] array = new byte[16];
            lock (UsbLock)
            {
                InputPipe.Read(array, 0, array.Length);
            }

            int num = 0;
            transferSize = BitConverter.ToInt32(array, num);
            num += 4;
            blockSize = BitConverter.ToUInt32(array, num);
            num += 4;
            lastBlock = BitConverter.ToUInt64(array, num);
            num += 8;
        }

        // 13
        public void ReadDataToBuffer(ulong diskOffset, byte[] buffer, int offset, int count, int diskTransferSize)
        {
            ExecuteRawVoidMethod([(byte)SioOpcode.SioReadDisk]);
            byte[] buffer1 = new byte[16];
            BitConverter.GetBytes(diskOffset).CopyTo(buffer1, 0);
            BitConverter.GetBytes((ulong)count).CopyTo(buffer1, 8);
            ExecuteRawVoidMethod(buffer1);

            int offset1 = offset;
            int count1;
            for (int index = offset + count; offset1 < index; offset1 += count1)
            {
                count1 = diskTransferSize;
                if (count1 > index - offset1)
                {
                    count1 = index - offset1;
                }

                lock (UsbLock)
                {
                    InputPipe.Read(buffer, offset1, count1);
                }

                byte[] singlebyte = new byte[1];
                if (count1 % 512 == 0)
                {
                    lock (UsbLock)
                    {
                        //InputPipe.Read(singlebyte, 0, singlebyte.Length);
                    }
                }
            }
        }

        // 14
        public bool WriteDataFromBuffer(ulong diskOffset, byte[] buffer, int offset, int count, int diskTransferSize)
        {
            ExecuteRawVoidMethod([(byte)SioOpcode.SioWriteDisk]);

            byte[] array = new byte[16];
            BitConverter.GetBytes(diskOffset).CopyTo(array, 0);
            BitConverter.GetBytes((ulong)((long)count)).CopyTo(array, 8);
            ExecuteRawVoidMethod(array);

            int i = offset;
            int num2 = offset + count;

            while (i < num2)
            {
                int num3 = diskTransferSize;
                if (num3 > num2 - i)
                {
                    num3 = num2 - i;
                }

                lock (UsbLock)
                {
                    OutputPipe.Write(buffer, i, num3);
                }

                if (num3 % 512 == 0)
                {
                    byte[] array2 = [];

                    lock (UsbLock)
                    {
                        OutputPipe.Write(array2, 0, array2.Length);
                    }
                }
                i += num3;
            }
            byte[] array3 = new byte[8];
            lock (UsbLock)
            {
                InputPipe.Read(array3, 0, array3.Length);
            }

            if (count != (long)BitConverter.ToUInt64(array3, 0))
            {
                return false;
            }

            return true;
        }

        // 15
        public bool ClearIdOverride()
        {
            byte[] buffer = ExecuteRawMethod([(byte)SioOpcode.SioClearIdOverride])!;
            bool result = buffer[0] == 3;

            // Refresh the ID
            if (result)
            {
                HasCachedV2Results = false;
                GetIdV2();
            }

            return result;
        }

        // 17
        public Guid? GetSerialNumber()
        {
            byte[] buffer = ExecuteRawMethod([(byte)SioOpcode.SioSerialNumber])!;
            return new Guid(buffer);
        }

        // 19
        public uint SetBootMode(uint bootMode, string profileName)
        {
            uint num = 0x80000015U;

            if (Encoding.Unicode.GetByteCount(profileName) >= 128)
            {
                num = 0x80000002U;
                throw new Win32Exception(87);
            }

            uint num2 = 132U;
            byte[] array = new byte[num2];
            Array.Clear(array, 0, array.Length);
            byte[] array2 = BitConverter.GetBytes(bootMode);
            array2.CopyTo(array, 0);
            array2 = Encoding.Unicode.GetBytes(profileName);
            array2.CopyTo(array, 4);
            
            ExecuteRawVoidMethod([(byte)SioOpcode.SioSetBootMode]);

            OutputPipe.Write(array, 0, array.Length);
            byte[] array3 = new byte[4];
            InputPipe.Read(array3, 0, array3.Length);
            num = BitConverter.ToUInt32(array3, 0);

            return num;
        }

        // 22
        public void GetDeviceVersion()
        {
            byte[] buffer = ExecuteRawMethod([(byte)SioOpcode.SioDeviceVersion])!;
            Console.WriteLine(Convert.ToHexString(buffer));
        }

        // 23
        public bool QueryForCommandAvailable(SioOpcode Cmd)
        {
            (_, Guid _, bool supportsFastFlash, bool _, int clientVersion, Guid _, string _) = GetIdV2();

            if (clientVersion < 2)
            {
                return Cmd < SioOpcode.SioFastFlash || supportsFastFlash;
            }

            ExecuteRawVoidMethod([(byte)SioOpcode.SioQueryForCmd]);
            byte[] buffer = ExecuteRawMethod([(byte)Cmd])!;
            return buffer[0] != 0;
        }

        // 24
        public string GetServicingLogs(string logFolderPath)
        {
            string? text = null;

            if (!QueryForCommandAvailable(SioOpcode.SioGetUpdateLogs))
            {
                throw new Exception("Command not available");
            }

            if (string.IsNullOrEmpty(logFolderPath))
            {
                throw new ArgumentNullException(nameof(logFolderPath));
            }

            ExecuteRawVoidMethod([(byte)SioOpcode.SioGetUpdateLogs]);

            byte[] array = new byte[262144];
            int num = 0;
            
            byte[] array2 = new byte[4];
            int num2 = InputPipe.Read(array2, 0, array2.Length);

            int num3 = BitConverter.ToInt32(array2, 0);
            
            string text2 = Path.GetFullPath(logFolderPath);
            Directory.CreateDirectory(text2);
            text2 = Path.Combine(text2, Path.GetRandomFileName() + ".cab");
            
            using (FileStream fileStream = File.Open(text2, FileMode.Create, FileAccess.Write))
            {
                do
                {
                    Array.Clear(array, 0, array.Length);
                    num2 = InputPipe.Read(array, 0, array.Length);
                    num += num2;
                    fileStream.Write(array, 0, array.Length);
                }
                while (num != num3);
                text = text2;
            }

            return text;
        }

        // 25
        public void QueryDeviceUnlockId(out byte[] unlockId, out byte[] oemId, out byte[] platformId)
        {
            unlockId = new byte[32];
            oemId = new byte[16];
            platformId = new byte[16];

            if (!QueryForCommandAvailable(SioOpcode.SioQueryDeviceUnlockId))
            {
                throw new Exception("Command not available");
            }

            ExecuteRawVoidMethod([(byte)SioOpcode.SioQueryDeviceUnlockId]);

            byte[] numBuffer = new byte[4];
            InputPipe.Read(numBuffer, 0, 4);

            int num = BitConverter.ToInt32(numBuffer);

            byte[] tmpBuffer = new byte[4];
            InputPipe.Read(tmpBuffer, 0, 4);

            InputPipe.Read(unlockId, 0, 32);
            InputPipe.Read(oemId, 0, 16);
            InputPipe.Read(platformId, 0, 16);

            if (num != 0)
            {
                throw new Exception("Error while reading device unlock id, " + num);
            }
        }

        // 26
        public void RelockDeviceUnlockId()
        {
            if (!QueryForCommandAvailable(SioOpcode.SioRelockDeviceUnlockId))
            {
                throw new Exception("Command not available");
            }

            ExecuteRawVoidMethod([(byte)SioOpcode.SioRelockDeviceUnlockId]);

            byte[] numBuffer = new byte[4];
            InputPipe.Read(numBuffer, 0, 4);

            int num = BitConverter.ToInt32(numBuffer);

            if (num != 0)
            {
                throw new Exception("Error while relocking device unlock id, " + num);
            }
        }

        // 27
        public uint[] QueryUnlockTokenFiles()
        {
            byte[] array = new byte[16];
            List<uint> list = [];

            if (!QueryForCommandAvailable(SioOpcode.SioQueryUnlockTokenFiles))
            {
                throw new Exception("Command not available");
            }

            ExecuteRawVoidMethod([(byte)SioOpcode.SioQueryUnlockTokenFiles]);

            byte[] numBuffer = new byte[4];
            InputPipe.Read(numBuffer, 0, 4);

            int num = BitConverter.ToInt32(numBuffer);

            byte[] tmpBuffer = new byte[4];
            InputPipe.Read(tmpBuffer, 0, 4);

            InputPipe.Read(array, 0, 16);
            
            BitArray bitArray = new BitArray(array);
            uint num2 = 0U;
            while (num2 < (ulong)((long)bitArray.Count))
            {
                if (bitArray.Get(Convert.ToInt32(num2)))
                {
                    list.Add(num2);
                }
                num2 += 1U;
            }

            if (num != 0)
            {
                throw new Exception("Error while querying unlock token files, " + num);
            }

            return [.. list];
        }

        // 28
        public void WriteUnlockTokenFile(uint unlockTokenId, byte[] fileData)
        {
            uint num = 0U;
            uint num2 = (uint)fileData.Length;

            if (1048576 < fileData.Length)
            {
                throw new ArgumentException("fileData");
            }

            if (127U < unlockTokenId)
            {
                throw new ArgumentException("unlockTokenId");
            }

            if (!QueryForCommandAvailable(SioOpcode.SioWriteUnlockTokenFile))
            {
                throw new Exception("Command not available");
            }

            ExecuteRawVoidMethod([(byte)SioOpcode.SioWriteUnlockTokenFile]);

            OutputPipe.Write(BitConverter.GetBytes(num));
            OutputPipe.Write(BitConverter.GetBytes(num2));
            OutputPipe.Write(BitConverter.GetBytes(unlockTokenId));
            OutputPipe.Write(fileData, 0, fileData.Length);

            byte[] numBuffer = new byte[4];
            InputPipe.Read(numBuffer, 0, 4);

            int num3 = BitConverter.ToInt32(numBuffer);

            if (num3 != 0)
            {
                throw new Exception("Error while writing unlock token files, " + num3);
            }
        }

        // 29
        public bool QueryBitlockerState()
        {
            if (!QueryForCommandAvailable(SioOpcode.SioQueryBitlockerState))
            {
                throw new Exception("Command not available");
            }

            ExecuteRawVoidMethod([(byte)SioOpcode.SioQueryBitlockerState]);

            byte[] numBuffer = new byte[4];
            InputPipe.Read(numBuffer, 0, 4);

            int num = BitConverter.ToInt32(numBuffer);

            byte[] flagBuffer = new byte[1];
            InputPipe.Read(flagBuffer, 0, 1);

            bool flag = flagBuffer[0] != 0;

            if (num != 0)
            {
                throw new Exception("Error while reading bitlocker state, " + num);
            }

            return flag;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SimpleIOModel()
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