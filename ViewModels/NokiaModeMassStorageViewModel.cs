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

using System.Collections.Generic;

namespace WPinternals
{
    internal class NokiaModeMassStorageViewModel : ContextViewModel
    {
        private NokiaPhoneModel CurrentModel;

        internal NokiaModeMassStorageViewModel(NokiaPhoneModel CurrentModel)
            : base()
        {
            this.CurrentModel = CurrentModel;
        }

        internal void RebootTo(string Mode)
        {
            string DeviceMode;

            switch (Mode)
            {
                case "Flash":
                    DeviceMode = "Flash";
                    LogFile.Log("Reboot to Flash");
                    break;
                case "Label":
                    DeviceMode = "Test";
                    LogFile.Log("Reboot to Label");
                    break;
                case "MassStorage":
                    DeviceMode = "Flash"; // TODO: implement folow-up
                    LogFile.Log("Reboot to Mass Storage");
                    break;
                default:
                    return;
            }

            Dictionary<string, object> Params = new Dictionary<string, object>();
            Params.Add("DeviceMode", DeviceMode);
            Params.Add("ResetMethod", "HwReset");
            CurrentModel.ExecuteJsonMethodAsync("SetDeviceMode", Params);
        }
    }
}
