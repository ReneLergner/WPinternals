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
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace WPinternals.HelperClasses
{
    public class HexConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty SeparatorProperty =
            DependencyProperty.Register("OnTrue", typeof(object), typeof(HexConverter),
                new PropertyMetadata(" "));

        public object Separator
        {
            get
            {
                return GetValue(SeparatorProperty);
            }
            set
            {
                SetValue(SeparatorProperty, value);
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte[] bytes)
            {
                StringBuilder s = new(1000);
                for (int i = bytes.GetLowerBound(0); i <= bytes.GetUpperBound(0); i++)
                {
                    if (i != bytes.GetLowerBound(0))
                    {
                        s.Append(Separator);
                    }

                    s.Append(bytes[i].ToString("X2"));
                }
                return s.ToString();
            }
            else
            {
                return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static object Default(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
