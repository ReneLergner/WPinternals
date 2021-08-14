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
using System.Threading;
using System.Windows;

namespace WPinternals
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal static Action NavigateToGettingStarted;
        internal static Action NavigateToUnlockBoot;
        internal static PatchEngine PatchEngine;
        internal static WPinternalsConfig Config;
        internal static Mutex mutex = new(false, "Global\\WPinternalsRunning");
        internal static DownloadsViewModel DownloadManager;
        internal static bool InterruptBoot = false;
        internal static bool IsPnPEventLogMissing = true;

        public App()
            : base()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

#if NETCORE
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif

            if (Environment.GetCommandLineArgs().Length > 1)
            {
                CommandLine.OpenConsole();
            }

            try
            {
                if (!mutex.WaitOne(0, false))
                {
                    if (Environment.GetCommandLineArgs().Length > 1)
                    {
                        Console.WriteLine("Windows Phone Internals is already running");
                        CommandLine.CloseConsole();
                    }
                    else
                    {
                        MessageBox.Show("Windows Phone Internals is already running.", "Windows Phone Internals", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }

                    Environment.Exit(0);
                }
            }
            catch (AbandonedMutexException) { }

            Registration.CheckExpiration();

            string PatchDefintionsXml;
            string PatchDefintionsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PatchDefintions.xml");
            if (File.Exists(PatchDefintionsPath))
            {
                PatchDefintionsXml = File.ReadAllText(PatchDefintionsPath);
            }
            else
            {
                using Stream stream = System.Reflection.Assembly.GetEntryAssembly().GetManifestResourceStream("WPinternals.PatchDefinitions.xml");
                using StreamReader sr = new(stream);
                PatchDefintionsXml = sr.ReadToEnd();
            }
            PatchEngine = new PatchEngine(PatchDefintionsXml);

            Config = WPinternalsConfig.ReadConfig();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception)
            {
                LogFile.LogException(e.ExceptionObject as Exception);
            }
        }
    }
}
