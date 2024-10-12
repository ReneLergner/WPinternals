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
using System.Windows;
using System.Windows.Data;

namespace WPinternals.HelperClasses
{
    public class BooleanConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty OnTrueProperty =
            DependencyProperty.Register("OnTrue", typeof(object), typeof(BooleanConverter),
                new PropertyMetadata(default(object)));

        public static readonly DependencyProperty OnFalseProperty =
            DependencyProperty.Register("OnFalse", typeof(object), typeof(BooleanConverter),
                new PropertyMetadata(default(object)));

        public static readonly DependencyProperty OnNullProperty =
            DependencyProperty.Register("OnNull", typeof(object), typeof(BooleanConverter),
                new PropertyMetadata(default(object)));

        public object OnTrue
        {
            get
            {
                return GetValue(OnTrueProperty);
            }
            set
            {
                SetValue(OnTrueProperty, value);
            }
        }

        public object OnFalse
        {
            get
            {
                return GetValue(OnFalseProperty);
            }
            set
            {
                SetValue(OnFalseProperty, value);
            }
        }

        public object OnNull
        {
            get
            {
                return GetValue(OnNullProperty);
            }
            set
            {
                SetValue(OnNullProperty, value);
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null
                ? OnNull ?? Default(targetType)
                : string.Equals(value.ToString(), false.ToString(), StringComparison.CurrentCultureIgnoreCase)
                    ? OnFalse
                    : OnTrue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == OnNull)
            {
                return Default(targetType);
            }

            if (value == OnFalse)
            {
                return false;
            }

            if (value == OnTrue)
            {
                return true;
            }

            if (value == null)
            {
                return null;
            }

            if (OnNull != null &&
                string.Equals(value.ToString(), OnNull.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                return Default(targetType);
            }

            if (OnFalse != null &&
                string.Equals(value.ToString(), OnFalse.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }

            if (OnTrue != null &&
                string.Equals(value.ToString(), OnTrue.ToString(), StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            return null;
        }

        public static object Default(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
