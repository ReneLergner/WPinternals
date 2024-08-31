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
using System.Threading;

namespace WPinternals
{
    // Create this class on the UI thread, after the main-window of the application is initialized.
    // It is necessary to create the object on the UI thread, because notification events to the View need to be fired on that thread.
    // The Model for this ViewModel communicates over USB and for that it uses the hWnd of the main window.
    // Therefore the main window must be created before the ViewModel is created.

    internal class NokiaBootloaderViewModel : ContextViewModel
    {
        private readonly LumiaBootManagerAppModel CurrentModel;
        private readonly Action<PhoneInterfaces> RequestModeSwitch;
        internal Action SwitchToGettingStarted;
        private readonly object LockDeviceInfo = new();

        internal NokiaBootloaderViewModel(NokiaPhoneModel CurrentModel, Action<PhoneInterfaces> RequestModeSwitch, Action SwitchToGettingStarted)
            : base()
        {
            this.CurrentModel = (LumiaBootManagerAppModel)CurrentModel;
            this.RequestModeSwitch = RequestModeSwitch;
            this.SwitchToGettingStarted = SwitchToGettingStarted;
        }

        // Device info should be loaded only one time and only when the ViewModel is active
        internal override void EvaluateViewState()
        {
            if (IsActive)
            {
                new Thread(() => StartLoadDeviceInfo()).Start();
            }
        }

        private void StartLoadDeviceInfo()
        {
        }

        internal void RebootTo(string Mode)
        {
            switch (Mode)
            {
                case "Normal":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Normal);
                    break;
                case "PhoneInfo":
                    RequestModeSwitch(PhoneInterfaces.Lumia_PhoneInfo);
                    break;
                case "Label":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Label);
                    break;
                case "MassStorage":
                    RequestModeSwitch(PhoneInterfaces.Lumia_MassStorage);
                    break;
                default:
                    return;
            }
        }
    }
}
