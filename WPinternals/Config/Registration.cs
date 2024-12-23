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
using System.Linq;
using System.Security.Cryptography;
using System.Windows;

namespace WPinternals.Config
{
    internal static class Registration
    {
#if PREVIEW
        internal const bool IsPrerelease = true;
#else
        internal const bool IsPrerelease = false;
#endif

        internal static readonly DateTime ExpirationDate = new(2025, 06, 21);

        internal static void CheckExpiration()
        {
#if PREVIEW
            //if (IsPrerelease && (DateTime.Now >= ExpirationDate))
            if (DateTime.Now >= ExpirationDate)
            {
                if (Environment.GetCommandLineArgs().Count() > 1)
                {
                    Console.WriteLine("This prerelease version is expired!");
                    CommandLine.CloseConsole();
                }
                else
                    MessageBox.Show("This prerelease version is expired!", "Windows Phone Internals", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }
#endif
        }

        internal static bool IsRegistered()
        {
            bool Result = false;
            if (App.Config.RegistrationName != null)
            {
                Result = CalcRegKey() == App.Config.RegistrationKey;
            }
            return Result;
        }

        internal static string CalcRegKey()
        {
            string KeyBase = App.Config.RegistrationName;
            if (Environment.MachineName == null)
            {
                KeyBase += "-Unknown";
            }
            else
            {
                KeyBase += "-" + Environment.MachineName;
            }

            byte[] KeyBytes = System.Text.Encoding.UTF8.GetBytes(KeyBase);
            SHA1Managed sha = new();
            byte[] Key = sha.ComputeHash(KeyBytes);
            return Convert.ToBase64String(Key);
        }
    }
}
