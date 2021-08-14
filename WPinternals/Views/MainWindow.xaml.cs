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
using System.Windows;

namespace WPinternals
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Visibility = Visibility.Collapsed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            DataContext = new MainViewModel();
            ((MainViewModel)DataContext).PropertyChanged += MainViewModel_PropertyChanged;
        }

        private void MainViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ContextViewModel")
            {
                MainContentScrollViewer.ScrollToTop();
            }
        }

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            const int MinChildWidth = 500;
            const int MaxChildWidth = 700;
            const int MarginTarget = 100;

            if (e.WidthChanged)
            {
                if (e.NewSize.Width >= (MaxChildWidth + (2 * MarginTarget)))
                {
                    ChildFrame.Width = MaxChildWidth;
                }
                else
                {
                    ChildFrame.Width = (e.NewSize.Width - (2 * MarginTarget)) < MinChildWidth ? MinChildWidth : e.NewSize.Width - (2 * MarginTarget);
                }
            }
            ChildFrame.Margin = new Thickness(0, 20, 0, 20);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if ((Application.Current.MainWindow != this) && (Application.Current.MainWindow != null))
            {
                Application.Current.MainWindow.Close(); // This one holds the hWnd for handling USB events and it must be closed too.
            }
        }
    }
}
