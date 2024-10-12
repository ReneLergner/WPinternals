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
using System.Collections.Generic;
using System.Windows.Input;

namespace WPinternals.HelperClasses
{
    // This class is taken from the Prism library by Microsoft Patterns & Practices
    // License: http://compositewpf.codeplex.com/license
    public abstract class DelegateCommandBase : ICommand, IActiveAware
    {
        private List<WeakReference> _canExecuteChangedHandlers;
        private bool _isActive;
        private readonly Func<object, bool> canExecuteMethod;
        private readonly Action<object> executeMethod;

        public event EventHandler CanExecuteChanged
        {
            add
            {
                WeakEventHandlerManager.AddWeakReferenceHandler(ref _canExecuteChangedHandlers, value, 2);
            }
            remove
            {
                WeakEventHandlerManager.RemoveWeakReferenceHandler(_canExecuteChangedHandlers, value);
            }
        }

        public event EventHandler IsActiveChanged;

        protected DelegateCommandBase(Action<object> executeMethod, Func<object, bool> canExecuteMethod)
        {
            if (executeMethod == null || canExecuteMethod == null)
            {
                throw new ArgumentNullException(nameof(executeMethod), "Delegate Command Delegates Cannot Be Null");
            }
            this.executeMethod = executeMethod;
            this.canExecuteMethod = canExecuteMethod;
        }

        protected bool CanExecute(object parameter)
        {
            if (canExecuteMethod != null)
            {
                return canExecuteMethod(parameter);
            }
            return true;
        }

        protected void Execute(object parameter)
        {
            executeMethod(parameter);
        }

        protected virtual void OnCanExecuteChanged()
        {
            WeakEventHandlerManager.CallWeakReferenceHandlers(this, _canExecuteChangedHandlers);
        }

        protected virtual void OnIsActiveChanged()
        {
            IsActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseCanExecuteChanged()
        {
            OnCanExecuteChanged();
        }

        bool ICommand.CanExecute(object parameter)
        {
            return CanExecute(parameter);
        }

        void ICommand.Execute(object parameter)
        {
            Execute(parameter);
        }

        public bool IsActive
        {
            get
            {
                return _isActive;
            }
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnIsActiveChanged();
                }
            }
        }
    }
}
