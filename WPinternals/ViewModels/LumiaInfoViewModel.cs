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
    internal class LumiaInfoViewModel : ContextViewModel
    {
        internal PhoneInterfaces? CurrentInterface;
        internal IDisposable CurrentModel;
        internal PhoneNotifierViewModel PhoneNotifier;
        private readonly Action<PhoneInterfaces> ModeSwitchRequestCallback;
        private readonly Action SwitchToGettingStarted;

        internal LumiaInfoViewModel(PhoneNotifierViewModel PhoneNotifier, Action<PhoneInterfaces> ModeSwitchRequestCallback, Action SwitchToGettingStarted)
            : base()
        {
            this.PhoneNotifier = PhoneNotifier;
            this.ModeSwitchRequestCallback = ModeSwitchRequestCallback;
            this.SwitchToGettingStarted = SwitchToGettingStarted;

            CurrentInterface = PhoneNotifier.CurrentInterface;
            CurrentModel = PhoneNotifier.CurrentModel;

            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
            PhoneNotifier.DeviceRemoved += DeviceRemoved;
        }

        ~LumiaInfoViewModel()
        {
            PhoneNotifier.NewDeviceArrived -= NewDeviceArrived;
            PhoneNotifier.DeviceRemoved -= DeviceRemoved;
        }

        private void DeviceRemoved()
        {
            CurrentInterface = null;
            CurrentModel = null;
            ActivateSubContext(null);
        }

        private void NewDeviceArrived(ArrivalEventArgs Args)
        {
            CurrentInterface = Args.NewInterface;
            CurrentModel = Args.NewModel;

            // Determine SubcontextViewModel
            switch (CurrentInterface)
            {
                case null:
                    ActivateSubContext(null);
                    break;
                case PhoneInterfaces.Lumia_Bootloader:
                    ActivateSubContext(new NokiaBootloaderViewModel((LumiaBootManagerAppModel)CurrentModel, ModeSwitchRequestCallback, SwitchToGettingStarted));
                    break;
                case PhoneInterfaces.Lumia_PhoneInfo:
                    ActivateSubContext(new NokiaPhoneInfoViewModel((LumiaPhoneInfoAppModel)CurrentModel, ModeSwitchRequestCallback, SwitchToGettingStarted));
                    break;
                case PhoneInterfaces.Lumia_Normal:
                    ActivateSubContext(new NokiaNormalViewModel((NokiaPhoneModel)CurrentModel, ModeSwitchRequestCallback));
                    break;
                case PhoneInterfaces.Lumia_Flash:
                    ActivateSubContext(new NokiaFlashViewModel((LumiaFlashAppModel)CurrentModel, ModeSwitchRequestCallback, SwitchToGettingStarted));
                    break;
                case PhoneInterfaces.Lumia_Label:
                    ActivateSubContext(new NokiaLabelViewModel((NokiaPhoneModel)CurrentModel, ModeSwitchRequestCallback));
                    break;
                case PhoneInterfaces.Lumia_MassStorage:
                    ActivateSubContext(new NokiaMassStorageViewModel((MassStorage)CurrentModel));
                    break;
            }
        }
    }
}
