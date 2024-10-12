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

namespace WPinternals.HelperClasses
{
    internal class ProgressUpdater
    {
        private readonly DateTime InitTime;
        private DateTime LastUpdateTime;
        private readonly ulong MaxValue;
        private readonly Action<int, TimeSpan?> ProgressUpdateCallback;
        internal int ProgressPercentage;

        internal ProgressUpdater(ulong MaxValue, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            InitTime = DateTime.Now;
            LastUpdateTime = DateTime.Now;
            this.MaxValue = MaxValue;
            this.ProgressUpdateCallback = ProgressUpdateCallback;
            SetProgress(0);
        }

        private ulong _Progress;
        internal ulong Progress
        {
            get
            {
                return _Progress;
            }
        }

        internal void SetProgress(ulong NewValue)
        {
            if (_Progress != NewValue)
            {
                int PreviousProgressPercentage = (int)((double)_Progress / MaxValue * 100);
                ProgressPercentage = (int)((double)NewValue / MaxValue * 100);

                _Progress = NewValue;

                if (DateTime.Now - LastUpdateTime > TimeSpan.FromSeconds(0.5) || ProgressPercentage == 100)
                {
#if DEBUG
                    Console.WriteLine("Init time: " + InitTime.ToShortTimeString() + " / Now: " + DateTime.Now.ToString() + " / NewValue: " + NewValue.ToString() + " / MaxValue: " + MaxValue.ToString() + " ->> Percentage: " + ProgressPercentage.ToString() + " / Remaining: " + TimeSpan.FromTicks((long)((DateTime.Now - InitTime).Ticks / ((double)NewValue / MaxValue) * (1 - (double)NewValue / MaxValue))).ToString());
#endif

                    if (DateTime.Now - InitTime < TimeSpan.FromSeconds(30) && ProgressPercentage < 15)
                    {
                        ProgressUpdateCallback(ProgressPercentage, null);
                    }
                    else
                    {
                        ProgressUpdateCallback(ProgressPercentage, TimeSpan.FromTicks((long)((DateTime.Now - InitTime).Ticks / ((double)NewValue / MaxValue) * (1 - (double)NewValue / MaxValue))));
                    }

                    LastUpdateTime = DateTime.Now;
                }
            }
        }

        internal void IncreaseProgress(ulong Progress)
        {
            SetProgress(_Progress + Progress);
        }
    }
}
