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

using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System;

namespace WPinternals
{
    /// <summary>
    /// Interaction logic for NokiaBootloaderView.xaml
    /// </summary>
    public partial class NokiaBootloaderView : UserControl
    {
        private static PhoneNotifierViewModel PhoneNotifier;
        private static SynchronizationContext UIContext;

        public NokiaBootloaderView()
        {
            InitializeComponent();

            InterruptBoot = App.InterruptBoot;
            UIContext = SynchronizationContext.Current;

            // Setting these properties in XAML results in an error. Why?
            GifImage.GifSource = "/aerobusy.gif";
            GifImage.AutoStart = true;

            Loaded += NokiaBootloaderView_Loaded;
            Unloaded += NokiaBootloaderView_Unloaded;
        }

        private void NokiaBootloaderView_Unloaded(object sender, RoutedEventArgs e)
        {
            PhoneNotifier.NewDeviceArrived -= PhoneNotifier_NewDeviceArrived;
        }

        private void NokiaBootloaderView_Loaded(object sender, RoutedEventArgs e)
        {
            // Find the phone notifier
            DependencyObject obj = (DependencyObject)sender;
            while (!(obj is MainWindow))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }

            PhoneNotifier = ((MainViewModel)((MainWindow)obj).DataContext).PhoneNotifier;

            PhoneNotifier.NewDeviceArrived += PhoneNotifier_NewDeviceArrived;
        }

        private void HandleHyperlinkClick(object sender, RoutedEventArgs args)
        {
            Hyperlink link = args.Source as Hyperlink;
            if (link?.NavigateUri != null)
            {
                if (link.NavigateUri.ToString() == "Getting started")
                {
                    App.NavigateToGettingStarted();
                }
                else if (link.NavigateUri.ToString() == "Unlock boot")
                {
                    App.NavigateToUnlockBoot();
                }
                else if (link.NavigateUri.ToString() == "Interrupt boot")
                {
                    InterruptBoot = true;
                }
                else if (link.NavigateUri.ToString() == "Normal boot")
                {
                    InterruptBoot = false;
                }
                
                (this.DataContext as NokiaBootloaderViewModel)?.RebootTo(link.NavigateUri.ToString());
            }
        }

        private void Document_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as FlowDocument)?.AddHandler(Hyperlink.ClickEvent, new RoutedEventHandler(HandleHyperlinkClick));
        }

        public static readonly DependencyProperty InterruptBootProperty =
            DependencyProperty.Register("InterruptBoot", typeof(Boolean), typeof(NokiaBootloaderView), new FrameworkPropertyMetadata(InterruptBootChanged));
        public bool InterruptBoot
        {
            get
            {
                return (bool)GetValue(InterruptBootProperty);
            }
            set
            {
                SetValue(InterruptBootProperty, value);
            }
        }

        internal static void InterruptBootChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            App.InterruptBoot = (bool)e.NewValue;

            if ((bool)e.NewValue && PhoneNotifier.CurrentInterface == PhoneInterfaces.Lumia_Bootloader)
            {
                App.InterruptBoot = false;
                LogFile.Log("Found Lumia BootMgr and user forced to interrupt the boot process. Force to Flash-mode.");
                Task.Run(() => SwitchModeViewModel.SwitchTo(PhoneNotifier, PhoneInterfaces.Lumia_Flash));
            }
        }

        internal void PhoneNotifier_NewDeviceArrived(ArrivalEventArgs Args)
        {
            if (App.InterruptBoot && Args.NewInterface == PhoneInterfaces.Lumia_Bootloader)
            {
                App.InterruptBoot = false;
                LogFile.Log("Found Lumia BootMgr and user forced to interrupt the boot process. Force to Flash-mode.");
                Task.Run(() => SwitchModeViewModel.SwitchTo(PhoneNotifier, PhoneInterfaces.Lumia_Flash));
            }
        }
    }
}
