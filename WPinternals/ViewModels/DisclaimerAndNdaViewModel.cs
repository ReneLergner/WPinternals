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

using Microsoft.Win32;
using System;
using System.Windows;

namespace WPinternals
{
    internal class DisclaimerAndNdaViewModel : ContextViewModel
    {
        private readonly Action Accepted;

        internal DisclaimerAndNdaViewModel(Action Accepted)
            : base()
        {
            this.Accepted = Accepted;
        }

        private DelegateCommand _ExitCommand = null;
        public DelegateCommand ExitCommand
        {
            get
            {
                return _ExitCommand ??= new DelegateCommand(() => Application.Current.Shutdown());
            }
        }

        private DelegateCommand _ContinueCommand = null;
        public DelegateCommand ContinueCommand
        {
            get
            {
                return _ContinueCommand ??= new DelegateCommand(() =>
                    {
                        Registry.CurrentUser.OpenSubKey("Software\\WPInternals", true).SetValue("DisclaimerAccepted", 1, RegistryValueKind.DWord);
                        Registry.CurrentUser.OpenSubKey("Software\\WPInternals", true).SetValue("NdaAccepted", 1, RegistryValueKind.DWord);
                        Accepted();
                    });
            }
        }
    }
}
