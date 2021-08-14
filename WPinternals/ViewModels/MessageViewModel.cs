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

namespace WPinternals
{
    internal class MessageViewModel : ContextViewModel
    {
        internal MessageViewModel(string Message, Action OkAction = null, Action CancelAction = null)
            : base()
        {
            LogFile.Log(Message);

            if (OkAction != null)
            {
                this.OkCommand = new DelegateCommand(OkAction);
            }

            if (CancelAction != null)
            {
                this.CancelCommand = new DelegateCommand(CancelAction);
            }

            this.Message = Message;
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
        public DelegateCommand OkCommand { get; } = null;
        public DelegateCommand CancelCommand { get; } = null;
    }
}
