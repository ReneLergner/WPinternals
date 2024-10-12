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
using WPinternals.HelperClasses;

namespace WPinternals.Models.Lumia.UEFI
{
    internal delegate void InterfaceChangedHandler(PhoneInterfaces NewInterface, string DevicePath);

    internal partial class NokiaUEFIModel : NokiaPhoneModel
    {
        private string _devicePath;

        private readonly CommonPhoneInfo CommonInfo = new();

        internal event InterfaceChangedHandler InterfaceChanged = delegate { };

        //
        // Not valid commands
        //
        /* NOK    */
        private const string Signature = "NOK";
        /* NOKX   */
        private const string ExtendedMessageSignature = $"{Signature}X";
        /* NOKXC  */
        private const string CommonExtendedMessageSignature = $"{ExtendedMessageSignature}C";

        //
        // Common extended commands
        //
        /* NOKXCB */
        private const string SwitchModeSignature = $"{CommonExtendedMessageSignature}B";
        /* NOKXCE */
        private const string EchoSignature = $"{CommonExtendedMessageSignature}E";

        public NokiaUEFIModel(string DevicePath) : base(DevicePath)
        {
            _devicePath = DevicePath;
        }

        //
        // Normal commands
        //
        /* NOKD   */
        private const string DisableTimeoutsSignature = $"{Signature}D";
        /* NOKI   */
        private const string HelloSignature = $"{Signature}I";
        /* NOKV   */
        private const string InfoQuerySignature = $"{Signature}V";

        internal FlashAppType GetFlashAppType()
        {
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, InfoQuerySignature);
            byte[] Response = ExecuteRawMethod(Request);
            if ((Response == null) || (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU"))
            {
                throw new NotSupportedException();
            }

            return (FlashAppType)Response[5];
        }

        internal CommonPhoneInfo ReadPhoneInfoCommon()
        {
            // NOKH = Get Phone Info (IMEI and info from Product.dat) - Not available on some phones, like Lumia 640.
            // NOKV = Info Query

            CommonPhoneInfo Result = CommonInfo;

            if (Result.State == PhoneInfoState.Empty)
            {
                byte[] Request = new byte[4];
                ByteOperations.WriteAsciiString(Request, 0, InfoQuerySignature);
                byte[] Response = ExecuteRawMethod(Request);
                if ((Response != null) && (ByteOperations.ReadAsciiString(Response, 0, 4) != "NOKU"))
                {
                    Result.App = (FlashAppType)Response[5];
                    Result.ProtocolVersionMajor = Response[6];
                    Result.ProtocolVersionMinor = Response[7];
                    Result.VersionMajor = Response[8];
                    Result.VersionMinor = Response[9];
                }

                Result.State = PhoneInfoState.Basic;
            }

            return Result;
        }

        public void DisableRebootTimeOut()
        {
            byte[] Request = new byte[4];
            const string Header = DisableTimeoutsSignature;
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Header), 0, Request, 0, Header.Length);
            ExecuteRawMethod(Request);
        }

        internal void Hello()
        {
            byte[] Request = new byte[4];
            ByteOperations.WriteAsciiString(Request, 0, HelloSignature);
            byte[] Response = ExecuteRawMethod(Request);
            if (Response == null)
            {
                throw new BadConnectionException();
            }

            if (ByteOperations.ReadAsciiString(Response, 0, 4) != HelloSignature)
            {
                throw new WPinternalsException("Bad response from phone!", "The phone did not answer properly to the Hello message sent.");
            }
        }

        internal void RaiseInterfaceChanged(PhoneInterfaces NewInterface)
        {
            InterfaceChanged(NewInterface, _devicePath);
        }

        // Raw USB Interfaces

        protected byte[] ExecuteRawMethod(byte[] RawMethod)
        {
            return ExecuteRawMethod(RawMethod, RawMethod.Length);
        }

        protected byte[] ExecuteRawMethod(byte[] RawMethod, int Length)
        {
            byte[] Buffer = new byte[0x8000]; // Should be at least 0x4408 for receiving the GPT packet.
            byte[] Result = null;
            lock (UsbLock)
            {
                Device.OutputPipe.Write(RawMethod, 0, Length);
                try
                {
                    int OutputLength = Device.InputPipe.Read(Buffer);
                    Result = new byte[OutputLength];
                    System.Buffer.BlockCopy(Buffer, 0, Result, 0, OutputLength);
                }
                catch (Exception ex) // Reboot command looses connection
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }
            }
            return Result;
        }

        protected void ExecuteRawVoidMethod(byte[] RawMethod)
        {
            ExecuteRawVoidMethod(RawMethod, RawMethod.Length);
        }

        protected void ExecuteRawVoidMethod(byte[] RawMethod, int Length)
        {
            lock (UsbLock)
            {
                Device.OutputPipe.Write(RawMethod, 0, Length);
            }
        }
    }
}
