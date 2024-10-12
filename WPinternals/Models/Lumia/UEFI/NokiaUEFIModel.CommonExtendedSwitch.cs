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

namespace WPinternals.Models.Lumia.UEFI
{
    internal partial class NokiaUEFIModel : NokiaPhoneModel
    {
        internal void SwitchToFlashAppContext()
        {
            CommonPhoneInfo info = ReadPhoneInfoCommon();

            bool ModernFlashApp = info.VersionMajor >= 2;

            SendCommonExtendedSwitchToCommand('F');

            if (ModernFlashApp)
            {
                DisableRebootTimeOut();

                InterfaceChanged(PhoneInterfaces.Lumia_Flash, _devicePath);
            }
        }

        internal void SwitchToPhoneInfoAppContext()
        {
            CommonPhoneInfo info = ReadPhoneInfoCommon();

            bool ModernFlashApp = info.VersionMajor >= 2;

            SendCommonExtendedSwitchToCommand('P');

            if (ModernFlashApp)
            {
                DisableRebootTimeOut();

                CommonInfo.App = FlashAppType.PhoneInfoApp;

                InterfaceChanged(PhoneInterfaces.Lumia_PhoneInfo, _devicePath);
            }
        }

        internal void SwitchToBootManagerContext(bool DisableTimeOut = true)
        {
            CommonPhoneInfo info = ReadPhoneInfoCommon();

            bool ModernFlashApp = info.VersionMajor >= 2;

            SendCommonExtendedSwitchToCommand('B');

            if (ModernFlashApp)
            {
                DisableRebootTimeOut();

                InterfaceChanged(PhoneInterfaces.Lumia_Bootloader, _devicePath);
            }
        }

        internal void SwitchToMmosContext()
        {
            SendCommonExtendedSwitchToCommand('A');
        }

        internal void SwitchToNormalModeContext()
        {
            SendCommonExtendedSwitchToCommand('W');
        }

        internal void SwitchToMassStorageModeContext()
        {
            SendCommonExtendedSwitchToCommand('M');
        }

        internal void SwitchToResetContext()
        {
            SendCommonExtendedSwitchToCommand('R');
        }

        internal void SwitchToEmergencyMode()
        {
            SendCommonExtendedSwitchToCommand('E');
        }

        internal void SendCommonExtendedSwitchToCommand(char Mode)
        {
            byte[] Request = new byte[7];
            ByteOperations.WriteAsciiString(Request, 0, $"{SwitchModeSignature}{Mode}");

            byte[] Response = ExecuteRawMethod(Request);

            if (ByteOperations.ReadAsciiString(Response, 0, 4) == "NOKU")
            {
                throw new NotSupportedException();
            }

            UInt16 Error = (UInt16)((Response[6] << 8) + Response[7]);

            if (Error > 0)
            {
                throw new NotSupportedException("SendCommonExtendedSwitchToCommand: Error 0x" + Error.ToString("X4"));
            }
        }
    }
}
