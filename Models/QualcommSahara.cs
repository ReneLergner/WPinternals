// Copyright (c) 2018, Rene Lergner - wpinternals.net - @Heathcliff74xda
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
using System.Threading.Tasks;

namespace WPinternals
{
    internal enum SaharaMode: uint
    {
        ImageTransferPending = 0x00,
        ImagetransferComplete = 0x01,
        MemoryDebug = 0x02,
        Command = 0x03
    }

    internal delegate void ReadyHandler();

    internal class QualcommSahara
    {
        private QualcommSerial Serial;

        public QualcommSahara(QualcommSerial Serial)
        {
            Serial.EncodeCommands = false;
            Serial.DecodeResponses = false;
            this.Serial = Serial;
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
                byte[] Hello = null;
                Hello = Serial.GetResponse(new byte[] { 0x01, 0x00, 0x00, 0x00 });

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

                // Packet:
                // 00000002 = Hello response command id
                // 00000030 = Length
                // 00000002 = Protocol version
                // 00000001 = Supported version
                // 00000000 = Status OK
                // 00000000 = Mode
                // rest is reserved space
                Step = 2;
                byte[] HelloResponse = new byte[] {
                    0x02, 0x00, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                };
                Serial.SendData(HelloResponse);

                Step = 3;
                using (System.IO.FileStream FileStream = new System.IO.FileStream(Path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    while (true)
                    {
                        Step = 4;
                        byte[] ReadDataRequest = Serial.GetResponse(null);
                        UInt32 ResponseID = ByteOperations.ReadUInt32(ReadDataRequest, 0);
                        if (ResponseID == 4)
                            break;
                        if (ResponseID != 3)
                        {
                            Step = 5;
                            throw new BadConnectionException();
                        }

                        Offset = ByteOperations.ReadUInt32(ReadDataRequest, 0x0C);
                        Length = ByteOperations.ReadUInt32(ReadDataRequest, 0x10);
                        if ((ImageBuffer == null) || (ImageBuffer.Length != Length))
                            ImageBuffer = new byte[Length];
                        if (FileStream.Position != Offset)
                            FileStream.Seek(Offset, System.IO.SeekOrigin.Begin);

                        Step = 6;
                        FileStream.Read(ImageBuffer, 0, (int)Length);

                        Step = 7;
                        Serial.SendData(ImageBuffer);
                    }
                }
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex, LogType.FileAndConsole, Step.ToString() + " 0x" + Offset.ToString("X8") + " 0x" + Length.ToString("X8"));
                Result = false;
            }

            if (Result)
                LogFile.Log("Programmer loaded into phone memory", LogType.FileOnly);

            return Result;
        }

        public bool Handshake()
        {
            bool Result = true;

            try
            {
                byte[] Hello = Serial.GetResponse(new byte[] { 0x01, 0x00, 0x00, 0x00 });

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

                byte[] HelloResponse = new byte[] { 
                    0x02, 0x00, 0x00, 0x00, 0x30, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                };

                byte[] Ready = Serial.SendCommand(HelloResponse, new byte[] { 0x03, 0x00, 0x00, 0x00 });
            }
            catch
            {
                Result = false;
            }

            return Result;
        }

        public void ResetSahara()
        {
            Serial.SendCommand(new byte[] { 0x07, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00 }, new byte[] { 0x08, 0x00, 0x00, 0x00 });
        }

        public bool ConnectToProgrammerInTestMode()
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

            byte[] HelloPacketFromPcToProgrammer = new byte[0x20C];
            ByteOperations.WriteUInt32(HelloPacketFromPcToProgrammer, 0, 0x57503730);
            ByteOperations.WriteUInt32(HelloPacketFromPcToProgrammer, 0x28, 0x57503730);
            ByteOperations.WriteUInt32(HelloPacketFromPcToProgrammer, 0x208, 0x57503730);
            ByteOperations.WriteUInt16(HelloPacketFromPcToProgrammer, 0x48, 0x4445);

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
                    Serial.SendData(HelloPacketFromPcToProgrammer);
                    LogFile.Log("Hello packet accepted", LogType.FileOnly);
                }
                catch
                {
                    LogFile.Log("Hello packet not accepted", LogType.FileOnly);
                }

                try
                {
                    Serial.SetTimeOut(500);
                    Incoming = System.Text.Encoding.ASCII.GetString(Serial.GetResponse(null));
                    LogFile.Log("In: " + Incoming, LogType.FileOnly);
                    Serial.SetTimeOut(200);
                    if (Incoming.Contains("Chip serial num"))
                    {
                        Incoming = System.Text.Encoding.ASCII.GetString(Serial.GetResponse(null));
                        LogFile.Log("In: " + Incoming, LogType.FileOnly);
                        LogFile.Log("Incoming Hello-packets received", LogType.FileOnly);
                    }

                    while (Incoming.IndexOf("response value") < 0)
                    {
                        Incoming = System.Text.Encoding.ASCII.GetString(Serial.GetResponse(null));
                        LogFile.Log("In: " + Incoming, LogType.FileOnly);
                    };
                    
                    LogFile.Log("Incoming Hello-response received", LogType.FileOnly);
                    HandshakeCompleted = true;
                }
                catch { }
            }
            while (!HandshakeCompleted && (HelloSendCount < 6));

            if (HandshakeCompleted)
                LogFile.Log("Handshake completed with programmer in testmode", LogType.FileOnly);
            else
                LogFile.Log("Handshake with programmer failed", LogType.FileOnly);

            return HandshakeCompleted;
        }

        public async Task<bool> Reset(string ProgrammerPath)
        {
            bool SendImageResult = await Task.Run(() => SendImage(ProgrammerPath));
            if (!SendImageResult)
                return false;

            await Task.Run(() => StartProgrammer());

            bool Connected = await Task.Run(() => ConnectToProgrammerInTestMode());
            if (!Connected)
                return false;

            LogFile.Log("Rebooting phone", LogType.FileAndConsole);
            string Command03 = "<?xml version=\"1.0\" ?><data><power value=\"reset\"/></data>";
            LogFile.Log("Out: " + Command03, LogType.FileOnly);
            Serial.SendData(System.Text.Encoding.ASCII.GetBytes(Command03));

            string Incoming;
            do
            {
                Incoming = System.Text.Encoding.ASCII.GetString(Serial.GetResponse(null));
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
            byte[] SwitchModeCommand = new byte[] { 0x0C, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            ByteOperations.WriteUInt32(SwitchModeCommand, 8, (UInt32)Mode);
            byte[] ResponsePattern = null;
            switch (Mode)
            {
                case SaharaMode.ImageTransferPending:
                    ResponsePattern = new byte[] { 0x04, 0x00, 0x00, 0x00 };
                    break;
                case SaharaMode.MemoryDebug:
                    ResponsePattern = new byte[] { 0x09, 0x00, 0x00, 0x00 };
                    break;
                case SaharaMode.Command:
                    ResponsePattern = new byte[] { 0x0B, 0x00, 0x00, 0x00 };
                    break;
            }
            Serial.SendCommand(SwitchModeCommand, ResponsePattern);
        }

        public void StartProgrammer()
        {
            LogFile.Log("Starting programmer", LogType.FileAndConsole);
            byte[] DoneCommand = new byte[] { 0x05, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00 };
            byte[] DoneResponse = Serial.SendCommand(DoneCommand, new byte[] { 0x06, 0x00, 0x00, 0x00 });
            LogFile.Log("Programmer being launched on phone", LogType.FileOnly);
        }
    }
}
