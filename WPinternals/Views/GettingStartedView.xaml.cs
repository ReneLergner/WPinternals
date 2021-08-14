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

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace WPinternals
{
    /// <summary>
    /// Interaction logic for GettingStartedView.xaml
    /// </summary>
    public partial class GettingStartedView : UserControl
    {
        public GettingStartedView()
        {
            InitializeComponent();
        }

        private void HandleHyperlinkClick(object sender, RoutedEventArgs args)
        {
            if (args.Source is Hyperlink link)
            {
                switch (link.NavigateUri.ToString())
                {
                    case "Disclaimer":
                        (this.DataContext as GettingStartedViewModel)?.ShowDisclaimer();
                        break;
                    case "UnlockBoot":
                        (this.DataContext as GettingStartedViewModel)?.SwitchToUnlockBoot();
                        break;
                    case "UnlockRoot":
                        (this.DataContext as GettingStartedViewModel)?.SwitchToUnlockRoot();
                        break;
                    case "Backup":
                        (this.DataContext as GettingStartedViewModel)?.SwitchToBackup();
                        break;
                    case "Flash":
                        (this.DataContext as GettingStartedViewModel)?.SwitchToFlashRom();
                        break;
                    case "Dump":
                        (this.DataContext as GettingStartedViewModel)?.SwitchToDumpRom();
                        break;
                    case "Download":
                        (this.DataContext as GettingStartedViewModel)?.SwitchToDownload();
                        break;
                    default:
                        Process process = new();
                        process.StartInfo.UseShellExecute = true;
                        process.StartInfo.FileName = link.NavigateUri.AbsoluteUri;
                        process.Start();
                        break;
                }
            }
        }

        private void Document_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as FlowDocument)?.AddHandler(Hyperlink.ClickEvent, new RoutedEventHandler(HandleHyperlinkClick));
        }

        private void TextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as TextBlock)?.AddHandler(Hyperlink.ClickEvent, new RoutedEventHandler(HandleHyperlinkClick));
        }

        private void BulletDecorator_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as System.Windows.Controls.Primitives.BulletDecorator)?.AddHandler(Hyperlink.ClickEvent, new RoutedEventHandler(HandleHyperlinkClick));
        }
    }
}
