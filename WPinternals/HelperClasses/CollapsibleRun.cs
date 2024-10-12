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
//
// Some of the classes and functions in this file were found online.
// Where possible the original authors are referenced.

using System;
using System.Windows;
using System.Windows.Documents;

namespace WPinternals.HelperClasses
{
    internal class CollapsibleRun : Run
    {
        private string CollapsibleText;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            CollapsibleText = Text;
        }

        public bool IsVisible
        {
            get
            {
                return (bool)GetValue(IsVisibleProperty);
            }
            set
            {
                SetValue(IsVisibleProperty, value);
            }
        }
        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
          "IsVisible", typeof(bool), typeof(CollapsibleRun), new PropertyMetadata(true, IsVisibleChanged));
        public static void IsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CollapsibleRun)d).Text = (bool)e.NewValue ? ((CollapsibleRun)d).CollapsibleText : string.Empty;
        }
    }
}
