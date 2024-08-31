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
    internal class LumiaModeViewModel : ContextViewModel
    {
        internal PhoneInterfaces? CurrentInterface;
        internal IDisposable CurrentModel;
        internal PhoneNotifierViewModel PhoneNotifier;
        private readonly Action Callback;

        internal LumiaModeViewModel(PhoneNotifierViewModel PhoneNotifier, Action Callback)
            : base()
        {
            this.PhoneNotifier = PhoneNotifier;
            this.Callback = Callback;

            CurrentInterface = PhoneNotifier.CurrentInterface;
            CurrentModel = PhoneNotifier.CurrentModel;

            PhoneNotifier.NewDeviceArrived += NewDeviceArrived;
            PhoneNotifier.DeviceRemoved += DeviceRemoved;
        }

        ~LumiaModeViewModel()
        {
            PhoneNotifier.NewDeviceArrived -= NewDeviceArrived;
            PhoneNotifier.DeviceRemoved -= DeviceRemoved;
        }

        private void DeviceRemoved()
        {
            CurrentInterface = null;
            CurrentModel = null;

            if (!IsSwitchingInterface)
            {
                ActivateSubContext(null);
            }
        }

        private void NewDeviceArrived(ArrivalEventArgs Args)
        {
            CurrentInterface = Args.NewInterface;
            CurrentModel = Args.NewModel;

            if (!IsSwitchingInterface && IsActive)
            {
                Refresh();
            }
        }

        internal override void EvaluateViewState()
        {
            if (!IsSwitchingInterface && IsActive)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            // Determine SubcontextViewModel
            switch (CurrentInterface)
            {
                case null:
                    ActivateSubContext(null);
                    break;
                case PhoneInterfaces.Lumia_Bootloader:
                    ActivateSubContext(new NokiaModeBootloaderViewModel((LumiaBootManagerAppModel)CurrentModel, OnModeSwitchRequested));
                    break;
                case PhoneInterfaces.Lumia_PhoneInfo:
                    ActivateSubContext(new NokiaModePhoneInfoViewModel((LumiaPhoneInfoAppModel)CurrentModel, OnModeSwitchRequested));
                    break;
                case PhoneInterfaces.Lumia_Normal:
                    ActivateSubContext(new NokiaModeNormalViewModel((NokiaPhoneModel)CurrentModel, OnModeSwitchRequested));
                    break;
                case PhoneInterfaces.Lumia_Flash:
                    ActivateSubContext(new NokiaModeFlashViewModel((LumiaFlashAppModel)CurrentModel, OnModeSwitchRequested));
                    break;
                case PhoneInterfaces.Lumia_Label:
                    ActivateSubContext(new NokiaModeLabelViewModel((NokiaPhoneModel)CurrentModel, OnModeSwitchRequested));
                    break;
                case PhoneInterfaces.Lumia_MassStorage:
                    ActivateSubContext(new NokiaModeMassStorageViewModel((MassStorage)CurrentModel, OnModeSwitchRequested));
                    break;
            }
        }

        // Called from eventhandler, so "async void" is valid here.
        internal async void OnModeSwitchRequested(PhoneInterfaces? TargetInterface)
        {
            IsSwitchingInterface = true;

            try
            {
                await SwitchModeViewModel.SwitchToWithStatus(PhoneNotifier, TargetInterface, SetWorkingStatus, UpdateWorkingStatus, null); // This is a manual switch. We don't care about which volume arrives.

                IsSwitchingInterface = false;
                Callback();
                Refresh();
            }
            catch (Exception Ex)
            {
                IsSwitchingInterface = false;
                ActivateSubContext(new MessageViewModel(Ex.Message, () =>
                {
                    Callback();
                    Refresh();
                }));
            }
        }
    }
}
