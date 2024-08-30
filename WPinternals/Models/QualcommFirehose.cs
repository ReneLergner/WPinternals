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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WPinternals
{
    internal class QualcommFirehose
    {
        private readonly QualcommSerial Serial;

        public QualcommFirehose(QualcommSerial Serial)
        {
            this.Serial = Serial;
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
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
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

        public async Task<bool> Reset()
        {
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

        public bool SendEdPayload(string ProgrammerPath, string PayloadPath)
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