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
using System.ComponentModel;
using System.Threading;

namespace WPinternals
{
    internal class ContextViewModel : INotifyPropertyChanged
    {
        protected SynchronizationContext UIContext;

        public bool IsSwitchingInterface = false;
        public bool IsFlashModeOperation = false;

        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        protected void OnPropertyChanged(string propertyName)
        {
            if ((UIContext == null) && (SynchronizationContext.Current != null))
            {
                UIContext = SynchronizationContext.Current;
            }

            if (this.PropertyChanged != null)
            {
                if (SynchronizationContext.Current == UIContext)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
                else
                {
                    UIContext.Post((s) => PropertyChanged(this, new PropertyChangedEventArgs(propertyName)), null);
                }
            }
        }

        private ContextViewModel _SubContextViewModel;
        public ContextViewModel SubContextViewModel
        {
            get
            {
                return _SubContextViewModel;
            }
            private set
            {
                if (_SubContextViewModel != null)
                {
                    _SubContextViewModel.IsActive = false;
                }

                _SubContextViewModel = value;
                if (_SubContextViewModel != null)
                {
                    _SubContextViewModel.IsActive = IsActive;
                }

                OnPropertyChanged(nameof(SubContextViewModel));
            }
        }

        internal ContextViewModel()
        {
            UIContext = SynchronizationContext.Current;
        }

        internal ContextViewModel(MainViewModel Main) : this()
        {
        }

        internal ContextViewModel(MainViewModel Main, ContextViewModel SubContext) : this(Main)
        {
            SubContextViewModel = SubContext;
        }

        internal bool IsActive { get; set; } = false;

        internal virtual void EvaluateViewState()
        {
        }

        internal void Activate()
        {
            IsActive = true;
            EvaluateViewState();
            SubContextViewModel?.Activate();
        }

        internal void ActivateSubContext(ContextViewModel NewSubContext)
        {
            if (_SubContextViewModel != null)
            {
                _SubContextViewModel.IsActive = false;
            }

            if (NewSubContext != null)
            {
                if (IsActive)
                {
                    NewSubContext.Activate();
                }
                else
                {
                    NewSubContext.IsActive = false;
                }
            }
            SubContextViewModel = NewSubContext;
        }

        internal void SetWorkingStatus(string Message, string SubMessage, ulong? MaxProgressValue, bool ShowAnimation = true, WPinternalsStatus Status = WPinternalsStatus.Undefined)
        {
            ActivateSubContext(new BusyViewModel(Message, SubMessage, MaxProgressValue, UIContext: UIContext, ShowAnimation: ShowAnimation, ShowRebootHelp: Status == WPinternalsStatus.WaitingForManualReset));
        }

        internal void UpdateWorkingStatus(string Message, string SubMessage, ulong? CurrentProgressValue, WPinternalsStatus Status = WPinternalsStatus.Undefined)
        {
            if (SubContextViewModel is BusyViewModel Busy)
            {
                if (Message != null)
                {
                    Busy.Message = Message;
                    Busy.SubMessage = SubMessage;
                }
                if ((CurrentProgressValue != null) && (Busy.ProgressUpdater != null))
                {
                    try
                    {
                        Busy.ProgressUpdater.SetProgress((ulong)CurrentProgressValue);
                    }
                    catch (Exception Ex)
                    {
                        LogFile.LogException(Ex);
                    }
                }
                Busy.SetShowRebootHelp(Status == WPinternalsStatus.WaitingForManualReset);
            }
        }
    }
}
