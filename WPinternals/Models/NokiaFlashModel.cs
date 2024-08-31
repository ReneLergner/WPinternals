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
    internal delegate void InterfaceChangedHandler(PhoneInterfaces NewInterface, string DevicePath);

    internal class NokiaFlashModel : NokiaPhoneModel
    {
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

        public NokiaFlashModel(string DevicePath) : base(DevicePath) { }

        internal void SwitchToBootManagerContext(bool DisableTimeOut = true)
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, SwitchModeSignature + "B");
            byte[] Response = ExecuteRawMethod(Request);
            if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
            {
                throw new NotSupportedException();
            }

            UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);
            if (Error > 0)
            {
                throw new NotSupportedException("SwitchToBootManagerContext: Error 0x" + Error.ToString("X4"));
            }
        }

        internal void SwitchToPhoneInfoAppContext()
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, SwitchModeSignature + "P");
            byte[] Response = ExecuteRawMethod(Request);
            if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
            {
                throw new NotSupportedException();
            }

            UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);
            if (Error > 0)
            {
                throw new NotSupportedException("SwitchToPhoneInfoAppContext: Error 0x" + Error.ToString("X4"));
            }
        }

        internal void SwitchToMmosContext()
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, SwitchModeSignature + "A");
            ExecuteRawVoidMethod(Request);

            ResetDevice();

            Dispose(true);
        }

        internal void SwitchToFlashAppContext()
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, SwitchModeSignature + "F"); // This will stop charging the phone
            byte[] Response = ExecuteRawMethod(Request);
            if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
            {
                throw new NotSupportedException();
            }

            UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);
            if (Error > 0)
            {
                throw new NotSupportedException("SwitchToFlashAppContext: Error 0x" + Error.ToString("X4"));
            }
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
    }
}
