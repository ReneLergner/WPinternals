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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace WPinternals
{
    internal enum SaharaMode : uint
    {
        ImageTransferPending = 0x00,
        ImagetransferComplete = 0x01,
        MemoryDebug = 0x02,
        Command = 0x03
    }

    internal enum SaharaCommand : uint
    {
        HelloRequest = 0x01,
        HelloResponse = 0x02,
        ReadData = 0x03,
        EndTransfer = 0x04,
        DoneRequest = 0x05,
        DoneResponse = 0x06,
        ResetRequest = 0x07,
        ResetResponse = 0x08,
        MemoryDebug = 0x09,
        MemoryRead = 0x0A,
        CommandReady = 0x0B,
        SwitchMode = 0x0C,
        ExecuteRequest = 0x0D,
        ExecuteResponse = 0x0E,
        ExecuteData = 0x0F,
        MemoryDebug64 = 0x10,
        MemoryRead64 = 0x11,
        MemoryReadData64 = 0x12,
        ResetStateMachineIdentifier = 0x13
    }

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

        private static byte[] BuildCommandPacket(SaharaCommand SaharaCommand, byte[] CommandBuffer = null)
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

        private static byte[] BuildHelloResponsePacket(SaharaMode SaharaMode, UInt32 ProtocolVersion = 2, UInt32 SupportedVersion = 1, UInt32 MaxPacketLength = 0 /* 0: Status OK */)
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

            return BuildCommandPacket(SaharaCommand.HelloResponse, Hello);
        }

        private static byte[] BuildExecuteRequestPacket(UInt32 RequestID)
        {
            byte[] Execute = new byte[0x04];
            ByteOperations.WriteUInt32(Execute, 0x00, RequestID);
            return BuildCommandPacket(SaharaCommand.ExecuteRequest, Execute);
        }

        private static byte[] BuildExecuteDataPacket(UInt32 RequestID)
        {
            byte[] Execute = new byte[0x04];
            ByteOperations.WriteUInt32(Execute, 0x00, RequestID);
            return BuildCommandPacket(SaharaCommand.ExecuteData, Execute);
        }

        private byte[][] GetRootKeyHashes()
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
                byte[] HelloResponse = BuildHelloResponsePacket(SaharaMode.Command);
                Serial.SendData(HelloResponse);

                Step = 3;
                byte[] ReadDataRequest = Serial.GetResponse(null);
                UInt32 ResponseID = ByteOperations.ReadUInt32(ReadDataRequest, 0);

                if (ResponseID != 0xB)
                {
                    throw new BadConnectionException();
                }

                Step = 4;
                byte[][] RKHs = GetRootKeyHashes();
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
                byte[] HelloResponse = BuildHelloResponsePacket(SaharaMode.ImageTransferPending);
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

                byte[] HelloResponse = BuildHelloResponsePacket(SaharaMode.ImageTransferPending);

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
            Serial.SendCommand(BuildCommandPacket(SaharaCommand.ResetRequest), [0x08, 0x00, 0x00, 0x00]);
        }

        public bool ConnectToProgrammer(byte[] PacketFromPcToProgrammer)
        {
            // Behaviour of old firehose:
            // Takes about 20 ms to be started.
            // Then PC has to start talking to the phone.
            // Behaviour of new firehose:
            // After 2000 ms the firehose starts talking to the PC
            //
            // For the duration of 2.5 seconds we will send Hello packages
            // And also wait for incoming messages
            // An incoming message can be a response to our outgoing Hello packet (read incoming until "response value")
            // Or it can be an incoming Hello-packet from the programmer (always 2 packets, starting with "Chip serial num")
            // Sending the hello-packet can succeed immediately, or it can timeout.
            // When sending succeeds, an answer should be incoming immediately to complete the handshake.
            // When an incoming Hello was received, the phone still expects to receive another Hello.

            int HelloSendCount = 0;
            bool HandshakeCompleted = false;
            string Incoming;
            do
            {
                Serial.SetTimeOut(200);
                HelloSendCount++;
                try
                {
                    LogFile.Log("Send Hello to programmer (" + HelloSendCount.ToString() + ")", LogType.FileOnly);
                    Serial.SendData(PacketFromPcToProgrammer);
                    LogFile.Log("Hello packet accepted", LogType.FileOnly);
                }
                catch
                {
                    LogFile.Log("Hello packet not accepted", LogType.FileOnly);
                }

                try
                {
                    Serial.SetTimeOut(500);
                    Incoming = Encoding.ASCII.GetString(Serial.GetResponse(null));
                    LogFile.Log("In: " + Incoming, LogType.FileOnly);
                    Serial.SetTimeOut(200);
                    if (Incoming.Contains("Chip serial num"))
                    {
                        Incoming = Encoding.ASCII.GetString(Serial.GetResponse(null));
                        LogFile.Log("In: " + Incoming, LogType.FileOnly);
                        LogFile.Log("Incoming Hello-packets received", LogType.FileOnly);
                    }

                    while (Incoming.IndexOf("response value") < 0)
                    {
                        Incoming = Encoding.ASCII.GetString(Serial.GetResponse(null));
                        LogFile.Log("In: " + Incoming, LogType.FileOnly);
                    }

                    LogFile.Log("Incoming Hello-response received", LogType.FileOnly);

                    if (!Incoming.Contains("Failed to authenticate Digital Signature."))
                    {
                        HandshakeCompleted = true;
                    }
                    else
                    {
                        LogFile.Log("Programmer failed to authenticate Digital Signature", LogType.FileOnly);
                    }
                }
                catch { }
            }
            while (!HandshakeCompleted && (HelloSendCount < 6));

            return HandshakeCompleted;
        }

        public bool ConnectToProgrammerInTestMode()
        {
            byte[] HelloPacketFromPcToProgrammer = new byte[0x20C];
            ByteOperations.WriteUInt32(HelloPacketFromPcToProgrammer, 0, 0x57503730);
            ByteOperations.WriteUInt32(HelloPacketFromPcToProgrammer, 0x28, 0x57503730);
            ByteOperations.WriteUInt32(HelloPacketFromPcToProgrammer, 0x208, 0x57503730);
            ByteOperations.WriteUInt16(HelloPacketFromPcToProgrammer, 0x48, 0x4445);

            bool HandshakeCompleted = ConnectToProgrammer(HelloPacketFromPcToProgrammer);

            if (HandshakeCompleted)
            {
                LogFile.Log("Handshake completed with programmer in testmode", LogType.FileOnly);
            }
            else
            {
                LogFile.Log("Handshake with programmer failed", LogType.FileOnly);
            }

            return HandshakeCompleted;
        }

        public async Task<bool> Reset(string ProgrammerPath)
        {
            bool SendImageResult = await Task.Run(() => SendImage(ProgrammerPath));
            if (!SendImageResult)
            {
                return false;
            }

            await Task.Run(() => StartProgrammer());

            bool Connected = await Task.Run(() => ConnectToProgrammerInTestMode());
            if (!Connected)
            {
                return false;
            }

            LogFile.Log("Rebooting phone", LogType.FileAndConsole);
            const string Command03 = "<?xml version=\"1.0\" ?><data><power value=\"reset\"/></data>";
            LogFile.Log("Out: " + Command03, LogType.FileOnly);
            Serial.SendData(Encoding.ASCII.GetBytes(Command03));

            string Incoming;
            do
            {
                Incoming = Encoding.ASCII.GetString(Serial.GetResponse(null));
                LogFile.Log("In: " + Incoming, LogType.FileOnly);
            }
            while (Incoming.IndexOf("response value") < 0);

            // Workaround for problem
            // SerialPort is sometimes not disposed correctly when the device is already removed.
            // So explicitly dispose here
            Serial.Close();

            return true;
        }

        public void SwitchMode(SaharaMode Mode)
        {
            byte[] SwitchMode = new byte[0x04];
            ByteOperations.WriteUInt32(SwitchMode, 0x00, (UInt32)Mode);
            byte[] SwitchModeCommand = BuildCommandPacket(SaharaCommand.SwitchMode, SwitchMode);

            byte[] ResponsePattern = null;
            switch (Mode)
            {
                case SaharaMode.ImageTransferPending:
                    ResponsePattern = [0x04, 0x00, 0x00, 0x00];
                    break;
                case SaharaMode.MemoryDebug:
                    ResponsePattern = [0x09, 0x00, 0x00, 0x00];
                    break;
                case SaharaMode.Command:
                    ResponsePattern = [0x0B, 0x00, 0x00, 0x00];
                    break;
            }
            Serial.SendCommand(SwitchModeCommand, ResponsePattern);
        }

        public void StartProgrammer()
        {
            LogFile.Log("Starting programmer", LogType.FileAndConsole);
            byte[] DoneCommand = BuildCommandPacket(SaharaCommand.DoneRequest);
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

        public async Task<bool> SendEdPayload(string ProgrammerPath, string PayloadPath)
        {
            // First, let's read the Emergency Download payload header and verify its validity
            FileStream PayloadStream = File.OpenRead(PayloadPath);

            byte[] ValidReferencePayloadHeader = [0x45, 0x6D, 0x65, 0x72, 0x67, 0x65, 0x6E, 0x63, 0x79, 0x20, 0x50, 0x61, 0x79, 0x6C, 0x6F, 0x61, 0x64];

            byte[] PayloadHeader = new byte[17];
            PayloadStream.Read(PayloadHeader, 0, 17);

            bool IsValidEdPayloadImage = StructuralComparisons.StructuralEqualityComparer.Equals(PayloadHeader, ValidReferencePayloadHeader);
            if (!IsValidEdPayloadImage)
            {
                return false;
            }

            // Now let's read the information block
            PayloadStream.Seek(0x64, SeekOrigin.Begin);
            byte[] PayloadInformationBlock = new byte[0x64];

            PayloadStream.Read(PayloadInformationBlock, 0, 0x64);
            string buildtime = Encoding.ASCII.GetString(PayloadInformationBlock).Trim('\0');

            PayloadStream.Read(PayloadInformationBlock, 0, 0x64);
            string builddate = Encoding.ASCII.GetString(PayloadInformationBlock).Trim('\0');

            PayloadStream.Read(PayloadInformationBlock, 0, 0x64);
            string version = Encoding.ASCII.GetString(PayloadInformationBlock).Trim('\0');

            PayloadInformationBlock = new byte[0x670];
            PayloadStream.Read(PayloadInformationBlock, 0, 0x670);
            string Info = Encoding.ASCII.GetString(PayloadInformationBlock).Trim('\0');

            // Print some information about the payload
            LogFile.Log("Emerency flasher version 0.1", LogType.FileAndConsole);
            LogFile.Log("Programmer information:", LogType.FileAndConsole);
            LogFile.Log("Build time: " + buildtime, LogType.FileAndConsole);
            LogFile.Log("Build date: " + builddate, LogType.FileAndConsole);
            LogFile.Log("Version: " + version, LogType.FileAndConsole);
            LogFile.Log("Info: " + Info, LogType.FileAndConsole);

            // Send the emergency programmer to the phone
            bool SendImageResult = await Task.Run(() => SendImage(ProgrammerPath));
            if (!SendImageResult)
            {
                return false;
            }

            // Start the emergency programmer on the phone
            await Task.Run(() => StartProgrammer());

            // Wait a few seconds before sending commands
            LogFile.Log("Waiting...", LogType.FileAndConsole);
            Thread.Sleep(2000);
            LogFile.Log("Waiting...OK", LogType.FileAndConsole);

            bool Terminated = false;
            bool Connected = false;
            bool ProgrammerRawMode = false;

            string Incoming;

            while (!Terminated)
            {
                PayloadInformationBlock = new byte[0x200];
                PayloadStream.Read(PayloadInformationBlock, 0, 0x200);
                string ProgrammerCommand = Encoding.ASCII.GetString(PayloadInformationBlock.Skip(0xC).ToArray()).Trim('\0');

                LogFile.Log(ProgrammerCommand, LogType.FileAndConsole);

                byte[] PacketFromPcToProgrammer = [];
                byte[] temp = new byte[0x200];

                while (true)
                {
                    if (PayloadStream.Position == PayloadStream.Length)
                    {
                        Terminated = true;
                        break;
                    }

                    PayloadStream.Read(temp, 0, 0x200);

                    if (temp[12] == 77 && temp[13] == 83 && temp[14] == 71 && temp[15] == 95)
                    {
                        PayloadStream.Seek(-0x200, SeekOrigin.Current);
                        break;
                    }

                    PacketFromPcToProgrammer = [.. PacketFromPcToProgrammer, .. temp];
                }

                bool ExpectingReplyFromProgrammer = false;

                if (ProgrammerCommand.Contains("XML"))
                {
                    string Outgoing = Encoding.ASCII.GetString(PacketFromPcToProgrammer).Trim('\0');
                    PacketFromPcToProgrammer = Encoding.ASCII.GetBytes(Outgoing);
                    LogFile.Log("Out: " + Outgoing, LogType.FileAndConsole);
                }

                if (!ProgrammerCommand.Contains("RAW_DATA") && !ProgrammerRawMode)
                {
                    ExpectingReplyFromProgrammer = true;
                }

                if (ProgrammerCommand.Contains("LAST") && ProgrammerRawMode)
                {
                    ExpectingReplyFromProgrammer = true;
                }

                if (ProgrammerCommand.Contains("DATA_ALL") && ProgrammerRawMode)
                {
                    ExpectingReplyFromProgrammer = true;
                }

                if (ProgrammerCommand.Contains("RAW_DATA") && !ProgrammerRawMode)
                {
                    LogFile.Log("Phone is not in raw mode ON, leaving...", LogType.FileAndConsole);

                    // Workaround for problem
                    // SerialPort is sometimes not disposed correctly when the device is already removed.
                    // So explicitly dispose here
                    Serial.Close();

                    LogFile.Log("Phone has been emergency flashed unsuccessfully!", LogType.FileAndConsole);
                    PayloadStream.Dispose();

                    return false;
                }

                if (!ProgrammerCommand.Contains("RAW_DATA") && ProgrammerRawMode)
                {
                    LogFile.Log("Phone is not in raw mode ON, leaving...", LogType.FileAndConsole);

                    // Workaround for problem
                    // SerialPort is sometimes not disposed correctly when the device is already removed.
                    // So explicitly dispose here
                    Serial.Close();

                    LogFile.Log("Phone has been emergency flashed unsuccessfully!", LogType.FileAndConsole);
                    PayloadStream.Dispose();

                    return false;
                }

                if (Connected)
                {
                    Serial.SendData(PacketFromPcToProgrammer);
                }

                if (ExpectingReplyFromProgrammer)
                {
                    if (!Connected)
                    {
                        Connected = ConnectToProgrammer(PacketFromPcToProgrammer);

                        if (Connected)
                        {
                            LogFile.Log("Handshake completed with programmer in validated image programming (VIP) mode", LogType.FileAndConsole);
                        }
                        else
                        {
                            LogFile.Log("Handshake with programmer failed", LogType.FileAndConsole);
                        }

                        if (!Connected)
                        {
                            LogFile.Log("Phone programmer is now ignoring us, leaving...", LogType.FileAndConsole);

                            // Workaround for problem
                            // SerialPort is sometimes not disposed correctly when the device is already removed.
                            // So explicitly dispose here
                            Serial.Close();

                            LogFile.Log("Phone has been emergency flashed unsuccessfully!", LogType.FileAndConsole);
                            PayloadStream.Dispose();

                            return false;
                        }
                    }
                    else
                    {
                        do
                        {
                            Serial.SetTimeOut(500);
                            Incoming = Encoding.ASCII.GetString(Serial.GetResponse(null));
                            Serial.SetTimeOut(200);
                            LogFile.Log("In: " + Incoming, LogType.FileAndConsole);
                        }
                        while (Incoming.IndexOf("response value") < 0);

                        if (Incoming.Contains("rawmode=\"false\""))
                        {
                            ProgrammerRawMode = false;
                            LogFile.Log("Raw mode: OFF", LogType.FileAndConsole);
                        }

                        if (Incoming.Contains("rawmode=\"true\""))
                        {
                            ProgrammerRawMode = true;
                            LogFile.Log("Raw mode: ON", LogType.FileAndConsole);
                        }

                        if (!Incoming.Contains("ACK"))
                        {
                            LogFile.Log("Phone programmer is now ignoring us, leaving...", LogType.FileAndConsole);

                            // Workaround for problem
                            // SerialPort is sometimes not disposed correctly when the device is already removed.
                            // So explicitly dispose here
                            Serial.Close();

                            LogFile.Log("Phone has been emergency flashed unsuccessfully!", LogType.FileAndConsole);
                            PayloadStream.Dispose();

                            return false;
                        }
                    }
                }
            }

            // Workaround for problem
            // SerialPort is sometimes not disposed correctly when the device is already removed.
            // So explicitly dispose here
            Serial.Close();

            LogFile.Log("Phone has been emergency flashed successfully!", LogType.FileAndConsole);
            PayloadStream.Dispose();

            return true;
        }
    }
}
