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
    internal class NokiaModePhoneInfoViewModel : ContextViewModel
    {
        private readonly LumiaPhoneInfoAppModel CurrentModel;
        private readonly Action<PhoneInterfaces?> RequestModeSwitch;
        private readonly object LockDeviceInfo = new();
        private bool DeviceInfoLoaded = false;

        internal NokiaModePhoneInfoViewModel(NokiaPhoneModel CurrentModel, Action<PhoneInterfaces?> RequestModeSwitch)
            : base()
        {
            this.CurrentModel = (LumiaPhoneInfoAppModel)CurrentModel;
            this.RequestModeSwitch = RequestModeSwitch;
        }

        internal override void EvaluateViewState()
        {
            if (IsActive)
            {
                new Thread(() => StartLoadDeviceInfo()).Start();
            }
        }

        private bool? _EffectivePhoneInfoSecurityStatus = null;
        public bool? EffectivePhoneInfoSecurityStatus
        {
            get
            {
                return _EffectivePhoneInfoSecurityStatus;
            }
            set
            {
                _EffectivePhoneInfoSecurityStatus = value;
                OnPropertyChanged(nameof(EffectivePhoneInfoSecurityStatus));
            }
        }

        internal void StartLoadDeviceInfo()
        {
            lock (LockDeviceInfo)
            {
                if (!DeviceInfoLoaded)
                {
                    try
                    {
                        LumiaPhoneInfoAppPhoneInfo Info = CurrentModel.ReadPhoneInfo();

                        //EffectivePhoneInfoSecurityStatus = Info.UefiSecureBootEnabled; // FIXME

                        LogFile.Log("Effective Bootloader Security Status: " + EffectivePhoneInfoSecurityStatus.ToString());
                    }
                    catch
                    {
                        LogFile.Log("Reading status from Flash interface was aborted.");
                    }
                    DeviceInfoLoaded = true;
                }
            }
        }

        internal void RebootTo(string Mode)
        {
            switch (Mode)
            {
                case "Normal":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Normal);
                    break;
                case "Flash":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Flash);
                    break;
                case "BootMgr":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Bootloader);
                    break;
                case "Label":
                    RequestModeSwitch(PhoneInterfaces.Lumia_Label);
                    break;
                case "MassStorage":
                    RequestModeSwitch(PhoneInterfaces.Lumia_MassStorage);
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
