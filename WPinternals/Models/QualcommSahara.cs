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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WPinternals
{
    internal delegate void ReadyHandler();

    internal class QualcommSahara
    {
        private readonly QualcommSerial Serial;

        public QualcommSahara(QualcommSerial Serial)
        {
            Serial.EncodeCommands = false;
            Serial.DecodeResponses = false;
            this.Serial = Serial;
        }

        private static byte[] BuildCommandPacket(QualcommSaharaCommand SaharaCommand, byte[] CommandBuffer = null)
        {
            UInt32 CommandID = (uint)SaharaCommand;
            UInt32 CommandBufferLength = 0;
            if (CommandBuffer != null)
            {
                CommandBufferLength = (UInt32)CommandBuffer.Length;
            }
            UInt32 Length = 0x8u + CommandBufferLength;

            byte[] Packet = new byte[Length];
            ByteOperations.WriteUInt32(Packet, 0x00, CommandID);
            ByteOperations.WriteUInt32(Packet, 0x04, Length);

            if (CommandBuffer != null && CommandBufferLength != 0)
            {
                Buffer.BlockCopy(CommandBuffer, 0, Packet, 0x08, CommandBuffer.Length);
            }

            return Packet;
        }

        private static byte[] BuildHelloResponsePacket(QualcommSaharaMode SaharaMode, UInt32 ProtocolVersion = 2, UInt32 SupportedVersion = 1, UInt32 MaxPacketLength = 0 /* 0: Status OK */)
        {
            UInt32 Mode = (uint)SaharaMode;

            // Hello packet:
            // xxxxxxxx = Protocol version
            // xxxxxxxx = Supported version
            // xxxxxxxx = Max packet length
            // xxxxxxxx = Expected mode
            // 6 dwords reserved space
            byte[] Hello = new byte[0x28];
            ByteOperations.WriteUInt32(Hello, 0x00, ProtocolVersion);
            ByteOperations.WriteUInt32(Hello, 0x04, SupportedVersion);
            ByteOperations.WriteUInt32(Hello, 0x08, MaxPacketLength);
            ByteOperations.WriteUInt32(Hello, 0x0C, Mode);
            ByteOperations.WriteUInt32(Hello, 0x10, 0);
            ByteOperations.WriteUInt32(Hello, 0x14, 0);
            ByteOperations.WriteUInt32(Hello, 0x18, 0);
            ByteOperations.WriteUInt32(Hello, 0x1C, 0);
            ByteOperations.WriteUInt32(Hello, 0x20, 0);
            ByteOperations.WriteUInt32(Hello, 0x24, 0);

            return BuildCommandPacket(QualcommSaharaCommand.HelloResponse, Hello);
        }

        private static byte[] BuildExecuteRequestPacket(UInt32 RequestID)
        {
            byte[] Execute = new byte[0x04];
            ByteOperations.WriteUInt32(Execute, 0x00, RequestID);
            return BuildCommandPacket(QualcommSaharaCommand.ExecuteRequest, Execute);
        }

        private static byte[] BuildExecuteDataPacket(UInt32 RequestID)
        {
            byte[] Execute = new byte[0x04];
            ByteOperations.WriteUInt32(Execute, 0x00, RequestID);
            return BuildCommandPacket(QualcommSaharaCommand.ExecuteData, Execute);
        }

        public byte[][] GetRKHs()
        {
            Serial.SendData(BuildExecuteRequestPacket(0x3));

            byte[] ReadDataRequest = Serial.GetResponse(null);
            UInt32 ResponseID = ByteOperations.ReadUInt32(ReadDataRequest, 0);

            if (ResponseID != 0xE)
            {
                throw new BadConnectionException();
            }

            uint RKHLength = ByteOperations.ReadUInt32(ReadDataRequest, 0x0C);

            Serial.SendData(BuildExecuteDataPacket(0x3));

            byte[] Response = Serial.GetResponse(null, Length: (int)RKHLength);
            
            List<byte[]> RootKeyHashes = new();
            for (int i = 0; i < RKHLength / 0x20; i++)
            {
                RootKeyHashes.Add(Response[(i * 0x20)..((i + 1) * 0x20)]);
            }

            return [.. RootKeyHashes];
        }

        public byte[] GetRKH()
        {
            int Step = 0;
            UInt32 Offset = 0;
            UInt32 Length = 0;

            try
            {
                Step = 1;
                byte[] Hello = Serial.GetResponse([0x01, 0x00, 0x00, 0x00]);

                // Incoming Hello packet:
                // 00000001 = Hello command id
                // xxxxxxxx = Length
                // xxxxxxxx = Protocol version
                // xxxxxxxx = Supported version
                // xxxxxxxx = Max packet length
                // xxxxxxxx = Expected mode
                // 6 dwords reserved space
                LogFile.Log("Protocol: 0x" + ByteOperations.ReadUInt32(Hello, 0x08).ToString("X8"), LogType.FileOnly);
                LogFile.Log("Supported: 0x" + ByteOperations.ReadUInt32(Hello, 0x0C).ToString("X8"), LogType.FileOnly);
                LogFile.Log("MaxLength: 0x" + ByteOperations.ReadUInt32(Hello, 0x10).ToString("X8"), LogType.FileOnly);
                LogFile.Log("Mode: 0x" + ByteOperations.ReadUInt32(Hello, 0x14).ToString("X8"), LogType.FileOnly);

                Step = 2;
                byte[] HelloResponse = BuildHelloResponsePacket(QualcommSaharaMode.Command);
                Serial.SendData(HelloResponse);

                Step = 3;
                byte[] ReadDataRequest = Serial.GetResponse(null);
                UInt32 ResponseID = ByteOperations.ReadUInt32(ReadDataRequest, 0);

                if (ResponseID != 0xB)
                {
                    throw new BadConnectionException();
                }

                Step = 4;
                byte[][] RKHs = GetRKHs();
                return RKHs[0];
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex, LogType.FileAndConsole, Step.ToString() + " 0x" + Offset.ToString("X8") + " 0x" + Length.ToString("X8"));
            }

            return null;
        }

        public bool SendImage(string Path)
        {
            bool Result = true;

            LogFile.Log("Sending programmer: " + Path, LogType.FileOnly);

            int Step = 0;
            UInt32 Offset = 0;
            UInt32 Length = 0;

            byte[] ImageBuffer = null;
            try
            {
                Step = 1;
                byte[] Hello = Serial.GetResponse([0x01, 0x00, 0x00, 0x00]);

                // Incoming Hello packet:
                // 00000001 = Hello command id
                // xxxxxxxx = Length
                // xxxxxxxx = Protocol version
                // xxxxxxxx = Supported version
                // xxxxxxxx = Max packet length
                // xxxxxxxx = Expected mode
                // 6 dwords reserved space
                LogFile.Log("Protocol: 0x" + ByteOperations.ReadUInt32(Hello, 0x08).ToString("X8"), LogType.FileOnly);
                LogFile.Log("Supported: 0x" + ByteOperations.ReadUInt32(Hello, 0x0C).ToString("X8"), LogType.FileOnly);
                LogFile.Log("MaxLength: 0x" + ByteOperations.ReadUInt32(Hello, 0x10).ToString("X8"), LogType.FileOnly);
                LogFile.Log("Mode: 0x" + ByteOperations.ReadUInt32(Hello, 0x14).ToString("X8"), LogType.FileOnly);

                Step = 2;
                byte[] HelloResponse = BuildHelloResponsePacket(QualcommSaharaMode.ImageTransferPending);
                Serial.SendData(HelloResponse);

                Step = 3;
                using FileStream FileStream = new(Path, FileMode.Open, FileAccess.Read);
                while (true)
                {
                    Step = 4;
                    byte[] ReadDataRequest = Serial.GetResponse(null);
                    UInt32 ResponseID = ByteOperations.ReadUInt32(ReadDataRequest, 0);
                    if (ResponseID == 4)
                    {
                        break;
                    }

                    if (ResponseID != 3)
                    {
                        Step = 5;
                        throw new BadConnectionException();
                    }

                    Offset = ByteOperations.ReadUInt32(ReadDataRequest, 0x0C);
                    Length = ByteOperations.ReadUInt32(ReadDataRequest, 0x10);
                    if ((ImageBuffer == null) || (ImageBuffer.Length != Length))
                    {
                        ImageBuffer = new byte[Length];
                    }

                    if (FileStream.Position != Offset)
                    {
                        FileStream.Seek(Offset, SeekOrigin.Begin);
                    }

                    Step = 6;
                    FileStream.Read(ImageBuffer, 0, (int)Length);

                    Step = 7;
                    Serial.SendData(ImageBuffer);
                }
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex, LogType.FileAndConsole, Step.ToString() + " 0x" + Offset.ToString("X8") + " 0x" + Length.ToString("X8"));
                Result = false;
            }

            if (Result)
            {
                LogFile.Log("Programmer loaded into phone memory", LogType.FileOnly);
            }

            return Result;
        }

        public bool Handshake()
        {
            bool Result = true;

            try
            {
                byte[] Hello = Serial.GetResponse([0x01, 0x00, 0x00, 0x00]);

                // Incoming Hello packet:
                // 00000001 = Hello command id
                // xxxxxxxx = Length
                // xxxxxxxx = Protocol version
                // xxxxxxxx = Supported version
                // xxxxxxxx = Max packet length
                // xxxxxxxx = Expected mode
                // 6 dwords reserved space
                LogFile.Log("Protocol: 0x" + ByteOperations.ReadUInt32(Hello, 0x08).ToString("X8"), LogType.FileOnly);
                LogFile.Log("Supported: 0x" + ByteOperations.ReadUInt32(Hello, 0x0C).ToString("X8"), LogType.FileOnly);
                LogFile.Log("MaxLength: 0x" + ByteOperations.ReadUInt32(Hello, 0x10).ToString("X8"), LogType.FileOnly);
                LogFile.Log("Mode: 0x" + ByteOperations.ReadUInt32(Hello, 0x14).ToString("X8"), LogType.FileOnly);

                byte[] HelloResponse = BuildHelloResponsePacket(QualcommSaharaMode.ImageTransferPending);

                byte[] Ready = Serial.SendCommand(HelloResponse, [0x03, 0x00, 0x00, 0x00]);
            }
            catch
            {
                Result = false;
            }

            return Result;
        }

        public void ResetSahara()
        {
            Serial.SendCommand(BuildCommandPacket(QualcommSaharaCommand.ResetRequest), [0x08, 0x00, 0x00, 0x00]);
        }

        public void SwitchMode(QualcommSaharaMode Mode)
        {
            byte[] SwitchMode = new byte[0x04];
            ByteOperations.WriteUInt32(SwitchMode, 0x00, (UInt32)Mode);
            byte[] SwitchModeCommand = BuildCommandPacket(QualcommSaharaCommand.SwitchMode, SwitchMode);

            byte[] ResponsePattern = null;
            switch (Mode)
            {
                case QualcommSaharaMode.ImageTransferPending:
                    ResponsePattern = [0x04, 0x00, 0x00, 0x00];
                    break;
                case QualcommSaharaMode.MemoryDebug:
                    ResponsePattern = [0x09, 0x00, 0x00, 0x00];
                    break;
                case QualcommSaharaMode.Command:
                    ResponsePattern = [0x0B, 0x00, 0x00, 0x00];
                    break;
            }
            Serial.SendCommand(SwitchModeCommand, ResponsePattern);
        }

        public void StartProgrammer()
        {
            LogFile.Log("Starting programmer", LogType.FileAndConsole);
            byte[] DoneCommand = BuildCommandPacket(QualcommSaharaCommand.DoneRequest);
            bool Started = false;
            int count = 0;
            do
            {
                count++;
                try
                {
                    byte[] DoneResponse = Serial.SendCommand(DoneCommand, [0x06, 0x00, 0x00, 0x00]);
                    Started = true;
                }
                catch (BadConnectionException)
                {
                    LogFile.Log("Problem while starting programmer. Attempting again.", LogType.FileAndConsole);
                }
            } while (!Started && count < 3);
            if (count >= 3 && !Started)
            {
                LogFile.Log("Maximum number of attempts to start the programmer exceeded.", LogType.FileAndConsole);
                throw new BadConnectionException();
            }
            LogFile.Log("Programmer being launched on phone", LogType.FileOnly);
        }

        public async Task<bool> LoadProgrammer(string ProgrammerPath)
        {
            bool SendImageResult = await Task.Run(() => SendImage(ProgrammerPath));
            if (!SendImageResult)
            {
                return false;
            }

            await Task.Run(() => StartProgrammer());
            return true;
        }
    }
}
