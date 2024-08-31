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

namespace WPinternals
{
    internal class NokiaModeMassStorageViewModel : ContextViewModel
    {
        private readonly MassStorage CurrentModel;
        private readonly Action<PhoneInterfaces?> RequestModeSwitch;

        internal NokiaModeMassStorageViewModel(NokiaPhoneModel CurrentModel, Action<PhoneInterfaces?> RequestModeSwitch)
            : base()
        {
            this.CurrentModel = (MassStorage)CurrentModel;
            this.RequestModeSwitch = RequestModeSwitch;
        }

        private bool _SupportsReboot = false;
        public bool SupportsReboot
        {
            get
            {
                return _SupportsReboot;
            }
            set
            {
                _SupportsReboot = value;
                OnPropertyChanged(nameof(SupportsReboot));
            }
        }

        internal override void EvaluateViewState()
        {
            if (IsActive)
            {
                SupportsReboot = CurrentModel.DoesDeviceSupportReboot();
            }
        }

        public void RebootTo(string Mode)
        {
            switch (Mode)
            {
                case "Normal":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Normal);
                    break;
                case "PhoneInfo":
                    RequestModeSwitch(PhoneInterfaces.Lumia_PhoneInfo);
                    break;
                case "BootMgr":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Bootloader);
                    break;
                case "Label":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Label);
                    break;
                case "Flash":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Flash);
                    break;
                case "Shutdown":
                    RequestModeSwitch(null);
                    break;
                default:
                    return;
            }
        }
    }
}
