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
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace WPinternals.HelperClasses
{
    // This class was found online.
    // Original author is probably: mdm20
    // https://stackoverflow.com/questions/5566330/get-gif-to-play-in-wpf-with-gifimage-class/5568703#5568703
    internal class GifImage : Image
    {
        private bool _isInitialized;
        private GifBitmapDecoder _gifDecoder;
        private Int32Animation _animation;

        public int FrameIndex
        {
            get
            {
                return (int)GetValue(FrameIndexProperty);
            }
            set
            {
                SetValue(FrameIndexProperty, value);
            }
        }

        private void Initialize()
        {
            _gifDecoder = new GifBitmapDecoder(new Uri("pack://application:,,," + GifSource), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            _animation = new Int32Animation(0, _gifDecoder.Frames.Count - 1, new Duration(new TimeSpan(0, 0, 0, _gifDecoder.Frames.Count / 10, (int)((_gifDecoder.Frames.Count / 10.0 - _gifDecoder.Frames.Count / 10) * 1000))))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            Source = _gifDecoder.Frames[0];

            _isInitialized = true;
        }

        static GifImage()
        {
            VisibilityProperty.OverrideMetadata(typeof(GifImage),
                new FrameworkPropertyMetadata(VisibilityPropertyChanged));
        }

        private static void VisibilityPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if ((Visibility)e.NewValue == Visibility.Visible)
            {
                ((GifImage)sender).StartAnimation();
            }
            else
            {
                ((GifImage)sender).StopAnimation();
            }
        }

        public static readonly DependencyProperty FrameIndexProperty =
            DependencyProperty.Register("FrameIndex", typeof(int), typeof(GifImage), new UIPropertyMetadata(0, new PropertyChangedCallback(ChangingFrameIndex)));

        private static void ChangingFrameIndex(DependencyObject obj, DependencyPropertyChangedEventArgs ev)
        {
            GifImage gifImage = obj as GifImage;
            gifImage.Source = gifImage._gifDecoder.Frames[(int)ev.NewValue];
        }

        public bool AutoStart
        {
            get
            {
                return (bool)GetValue(AutoStartProperty);
            }
            set
            {
                SetValue(AutoStartProperty, value);
            }
        }

        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.Register("AutoStart", typeof(bool), typeof(GifImage), new UIPropertyMetadata(false, AutoStartPropertyChanged));

        private static void AutoStartPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                (sender as GifImage)?.StartAnimation();
            }
        }

        public string GifSource
        {
            get
            {
                return (string)GetValue(GifSourceProperty);
            }
            set
            {
                SetValue(GifSourceProperty, value);
            }
        }

        public static readonly DependencyProperty GifSourceProperty =
            DependencyProperty.Register("GifSource", typeof(string), typeof(GifImage), new UIPropertyMetadata(string.Empty, GifSourcePropertyChanged));

        private static void GifSourcePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            (sender as GifImage)?.Initialize();
        }

        public void StartAnimation()
        {
            if (!_isInitialized)
            {
                Initialize();
            }

            BeginAnimation(FrameIndexProperty, _animation);
        }

        public void StopAnimation()
        {
            BeginAnimation(FrameIndexProperty, null);
        }
    }
}
