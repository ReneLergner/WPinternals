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
using System.Threading;

namespace WPinternals
{
    internal class BusyViewModel : ContextViewModel
    {
        private readonly ulong MaxProgressValue = 0;
        internal ProgressUpdater ProgressUpdater = null;

        // UIContext can be passed to BusyViewModel, when it needs to update progress-controls and it is created on a worker-thread.
        internal BusyViewModel(string Message, string SubMessage = null, ulong? MaxProgressValue = null, SynchronizationContext UIContext = null, bool ShowAnimation = true, bool ShowRebootHelp = false)
        {
            LogFile.Log(Message);

            this.UIContext = UIContext ?? SynchronizationContext.Current;

            this.Message = Message;
            this.SubMessage = SubMessage;
            this.ShowAnimation = ShowAnimation;
            this.ShowRebootHelp = ShowRebootHelp;
            if (MaxProgressValue != null)
            {
                ProgressPercentage = 0;
                this.MaxProgressValue = (ulong)MaxProgressValue;
                ProgressUpdater = new ProgressUpdater((ulong)MaxProgressValue, (p, t) =>
                {
                    if ((this.UIContext == null) || (this.UIContext == SynchronizationContext.Current))
                    {
                        ProgressPercentage = p;
                        TimeRemaining = t;
                    }
                    else
                    {
                        this.UIContext.Post((s) =>
                            {
                                ProgressPercentage = p;
                                TimeRemaining = t;
                            }, null);
                    }
                });
            }
        }

        internal void SetShowRebootHelp(bool Value)
        {
            ShowRebootHelp = Value;
        }

        internal void SetProgress(ulong Value)
        {
            if (ProgressUpdater != null)
            {
                UIContext.Post((s) => ProgressUpdater.SetProgress(Value), null);
            }
        }

        private string _Message = null;
        public string Message
        {
            get
            {
                return _Message;
            }
            set
            {
                _Message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        private string _SubMessage = null;
        public string SubMessage
        {
            get
            {
                return _SubMessage;
            }
            set
            {
                _SubMessage = value;
                OnPropertyChanged(nameof(SubMessage));
            }
        }

        private int? _ProgressPercentage = null;
        public int? ProgressPercentage
        {
            get
            {
                return _ProgressPercentage;
            }
            set
            {
                if (_ProgressPercentage != value)
                {
                    _ProgressPercentage = value;
                    OnPropertyChanged(nameof(ProgressPercentage));
                    OnPropertyChanged(nameof(ShowAnimation));
                    UpdateProgressText();
                }
            }
        }

        private TimeSpan? _TimeRemaining = null;
        public TimeSpan? TimeRemaining
        {
            get
            {
                return _TimeRemaining;
            }
            set
            {
                if (_TimeRemaining != value)
                {
                    _TimeRemaining = value;
                    OnPropertyChanged(nameof(TimeRemaining));
                    UpdateProgressText();
                }
            }
        }

        private void UpdateProgressText()
        {
            string NewText = null;
            if (ProgressPercentage != null)
            {
                NewText = "Progress: " + ((int)ProgressPercentage).ToString() + "%";
            }
            if (TimeRemaining != null)
            {
                if (NewText == null)
                {
                    NewText = "";
                }
                else
                {
                    NewText += " - ";
                }

                NewText += "Estimated time remaining: " + ((TimeSpan)TimeRemaining).ToString(@"h\:mm\:ss");
            }
            ProgressText = NewText;
        }

        private string _ProgressText = null;
        public string ProgressText
        {
            get
            {
                return _ProgressText;
            }
            set
            {
                if (_ProgressText != value)
                {
                    _ProgressText = value;
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        private bool _ShowAnimation = true;
        public bool ShowAnimation
        {
            get
            {
                return (_ProgressPercentage == null) && _ShowAnimation;
            }
            set
            {
                if (_ShowAnimation != value)
                {
                    _ShowAnimation = value;
                    OnPropertyChanged(nameof(ShowAnimation));
                }
            }
        }

        private bool _ShowRebootHelp = false;
        public bool ShowRebootHelp
        {
            get
            {
                return _ShowRebootHelp;
            }
            set
            {
                if (_ShowRebootHelp != value)
                {
                    _ShowRebootHelp = value;
                    OnPropertyChanged(nameof(ShowRebootHelp));
                }
            }
        }
    }
}
