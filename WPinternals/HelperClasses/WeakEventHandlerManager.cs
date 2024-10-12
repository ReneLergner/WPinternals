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
using System.Windows;
using System.Windows.Threading;

namespace WPinternals.HelperClasses
{
    // This class is taken from the Prism library by Microsoft Patterns & Practices
    // License: http://compositewpf.codeplex.com/license
    internal static class WeakEventHandlerManager
    {
        public static void AddWeakReferenceHandler(ref List<WeakReference> handlers, EventHandler handler, int defaultListSize)
        {
            (handlers ??= defaultListSize > 0 ? new List<WeakReference>(defaultListSize) : []).Add(new WeakReference(handler));
        }

        private static void CallHandler(object sender, EventHandler eventHandler)
        {
            DispatcherProxy proxy = DispatcherProxy.CreateDispatcher();
            if (eventHandler != null)
            {
                if (proxy?.CheckAccess() == false)
                {
                    proxy.BeginInvoke(new Action<object, EventHandler>(CallHandler), [sender, eventHandler]);
                }
                else
                {
                    eventHandler(sender, EventArgs.Empty);
                }
            }
        }

        public static void CallWeakReferenceHandlers(object sender, List<WeakReference> handlers)
        {
            if (handlers != null)
            {
                EventHandler[] callees = new EventHandler[handlers.Count];
                int count = 0;
                count = CleanupOldHandlers(handlers, callees, count);
                for (int i = 0; i < count; i++)
                {
                    CallHandler(sender, callees[i]);
                }
            }
        }

        private static int CleanupOldHandlers(List<WeakReference> handlers, EventHandler[] callees, int count)
        {
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                WeakReference reference = handlers[i];
                if (reference.Target is not EventHandler target)
                {
                    handlers.RemoveAt(i);
                }
                else
                {
                    callees[count] = target;
                    count++;
                }
            }
            return count;
        }

        public static void RemoveWeakReferenceHandler(List<WeakReference> handlers, EventHandler handler)
        {
            if (handlers != null)
            {
                for (int i = handlers.Count - 1; i >= 0; i--)
                {
                    WeakReference reference = handlers[i];
                    if (reference.Target is not EventHandler target || target == handler)
                    {
                        handlers.RemoveAt(i);
                    }
                }
            }
        }

        private class DispatcherProxy
        {
            private readonly Dispatcher innerDispatcher;

            private DispatcherProxy(Dispatcher dispatcher)
            {
                innerDispatcher = dispatcher;
            }

            public DispatcherOperation BeginInvoke(Delegate method, params object[] args)
            {
                return innerDispatcher.BeginInvoke(method, DispatcherPriority.Normal, args);
            }

            public bool CheckAccess()
            {
                return innerDispatcher.CheckAccess();
            }

            public static DispatcherProxy CreateDispatcher()
            {
                if (Application.Current == null)
                {
                    return null;
                }
                return new DispatcherProxy(Application.Current.Dispatcher);
            }
        }
    }
}
