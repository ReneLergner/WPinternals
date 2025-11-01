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
using System.Threading;
using System.Threading.Tasks;

namespace WPinternals.HelperClasses
{
    internal class AsyncAutoResetEvent
    {
        private readonly LinkedList<TaskCompletionSource<bool>> waiters =
            new();

        private bool isSignaled;

        public AsyncAutoResetEvent(bool signaled)
        {
            isSignaled = signaled;
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitAsync(timeout, CancellationToken.None);
        }

        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> tcs;

            lock (waiters)
            {
                if (isSignaled)
                {
                    isSignaled = false;
                    return true;
                }
                else if (timeout == TimeSpan.Zero)
                {
                    return isSignaled;
                }
                else
                {
                    tcs = new TaskCompletionSource<bool>();
                    waiters.AddLast(tcs);
                }
            }

            Task winner = await Task.WhenAny(tcs.Task, Task.Delay(timeout, cancellationToken));
            if (winner == tcs.Task)
            {
                // The task was signaled.
                return true;
            }
            else
            {
                // We timed-out; remove our reference to the task.
                // This is an O(n) operation since waiters is a LinkedList<T>.
                lock (waiters)
                {
                    bool removed = waiters.Remove(tcs);
                    System.Diagnostics.Debug.Assert(removed);
                    return false;
                }
            }
        }

        public void Set()
        {
            TaskCompletionSource<bool> toRelease = null;

            lock (waiters)
            {
                if (waiters.Count > 0)
                {
                    // Signal the first task in the waiters list.
                    toRelease = waiters.First.Value;
                    waiters.RemoveFirst();
                }
                else if (!isSignaled)
                {
                    // No tasks are pending
                    isSignaled = true;
                }
            }

            toRelease?.SetResult(true);
        }
    }
}
