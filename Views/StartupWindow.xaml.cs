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
using System.Linq;
using System.Windows;

namespace WPinternals
{
    /// <summary>
    /// Interaction logic for StartupWindow.xaml
    /// </summary>
    public partial class StartupWindow : Window
    {
        public StartupWindow()
        {
            InitializeComponent();
        }

        protected override async void OnSourceInitialized(EventArgs e)
        {
            Visibility = Visibility.Hidden;

            bool NeedLicenseAgrement = false;
            if (Registry.CurrentUser.OpenSubKey("Software\\WPInternals") == null)
            {
                Registry.CurrentUser.OpenSubKey("Software", true).CreateSubKey("WPInternals");
            }

            if (Registration.IsPrerelease && (Registry.CurrentUser.OpenSubKey("Software\\WPInternals").GetValue("NdaAccepted") == null))
            {
                NeedLicenseAgrement = true;
            }
            else if (Registry.CurrentUser.OpenSubKey("Software\\WPInternals").GetValue("DisclaimerAccepted") == null)
            {
                NeedLicenseAgrement = true;
            }

            if ((!Registration.IsPrerelease || Registration.IsRegistered()) && !NeedLicenseAgrement)
            {
                // USB communication uses Windows Messages and therefore the MainViewModel
                // can only be created after the Main Window was initialized.
                System.Threading.SynchronizationContext UIContext = System.Threading.SynchronizationContext.Current;
                await CommandLine.ParseCommandLine(UIContext);
            }
            else if (Environment.GetCommandLineArgs().Length > 1)
            {
                Console.WriteLine("First time use");
                Console.WriteLine("Switching to graphic user interface for license and registration");
                CommandLine.CloseConsole(false);
            }

            new MainWindow().Show();
        }
    }
}
