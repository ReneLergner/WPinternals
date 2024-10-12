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
using System.IO;
using System.Reflection;
using System.Text;

namespace WPinternals.HelperClasses
{
    internal static class LogFile
    {
        private static readonly StreamWriter w = null;
        private static readonly object lockobject = new();
#if PREVIEW
        private static string LogAction = null;
        private static StringBuilder LogBuilder;
#endif

        static LogFile()
        {
            try
            {
                if (!Directory.Exists(Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals")))
                {
                    Directory.CreateDirectory(Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals"));
                }

                w = File.AppendText(Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\WPInternals.log"));
            }
            catch { }
        }

        public static void Log(string logMessage, LogType Type = LogType.FileOnly)
        {
            if (w == null)
            {
                return;
            }

            lock (lockobject)
            {
                if (Type == LogType.FileOnly || Type == LogType.FileAndConsole)
                {
                    DateTime Now = DateTime.Now;
                    string Text = Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ": " + logMessage;
                    w.WriteLine(Text);
                    w.Flush();

#if PREVIEW
                    if (LogAction != null)
                        LogBuilder.AppendLine(Text);
#endif
                }

                if (CommandLine.IsConsoleVisible && (Type == LogType.ConsoleOnly || Type == LogType.FileAndConsole))
                {
                    Console.WriteLine(logMessage);
                }
            }
        }

        public static void LogException(Exception Ex, LogType Type = LogType.FileAndConsole, string AdditionalInfo = null)
        {
            string Indent = "";
            Exception CurrentEx = Ex;
            while (CurrentEx != null)
            {
                Log(Indent + "Error: " + RemoveBadChars(CurrentEx.Message).Replace("of type '.' ", "") + (AdditionalInfo == null ? "" : " - " + AdditionalInfo), Type);
                AdditionalInfo = null;
                if (CurrentEx is WPinternalsException)
                {
                    Log(Indent + ((WPinternalsException)CurrentEx).SubMessage, Type);
                }
#if DEBUG
                if (CurrentEx.StackTrace != null)
                {
                    Log(Indent + CurrentEx.StackTrace, LogType.FileOnly);
                }
#endif
                Indent += "    ";
                CurrentEx = CurrentEx.InnerException;
            }
        }

        private static string RemoveBadChars(string Text)
        {
            return System.Text.RegularExpressions.Regex.Replace(Text, @"[^\u0020-\u007E]+", string.Empty);
        }

        public static void DumpLog(StreamReader r)
        {
            string line;
            while ((line = r.ReadLine()) != null)
            {
                Console.WriteLine(line);
            }
        }

        public static void LogApplicationVersion()
        {
            Log("Windows Phone Internals version " +
                Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "." +
                Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString() + "." +
                Assembly.GetExecutingAssembly().GetName().Version.Build.ToString() + "." +
                Assembly.GetExecutingAssembly().GetName().Version.Revision.ToString(), LogType.FileAndConsole);
            Log("Copyright Heathcliff74", LogType.FileAndConsole);
        }

        internal static void BeginAction(string Action)
        {
#if PREVIEW
            if (LogAction == null)
            {
                LogAction = Action;
                LogBuilder = new StringBuilder();
                LogBuilder.AppendLine("Windows Phone Internals version " +
                Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "." +
                Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString() + "." +
                Assembly.GetExecutingAssembly().GetName().Version.Build.ToString() + "." +
                Assembly.GetExecutingAssembly().GetName().Version.Revision.ToString());
                LogBuilder.AppendLine("Copyright Heathcliff74");
                LogBuilder.AppendLine("Action: " + Action);
                if (App.Config.RegistrationName != null)
                    LogBuilder.AppendLine("Name: " + App.Config.RegistrationName);
                if (App.Config.RegistrationEmail != null)
                    LogBuilder.AppendLine("Mail: " + App.Config.RegistrationEmail);
                if (App.Config.RegistrationSkypeID != null)
                    LogBuilder.AppendLine("Skype: " + App.Config.RegistrationSkypeID);
                if (App.Config.RegistrationTelegramID != null)
                    LogBuilder.AppendLine("Telegram: " + App.Config.RegistrationTelegramID);
                if (Environment.MachineName != null)
                    LogBuilder.AppendLine("Machine: " + Environment.MachineName);
            }
#endif
        }

        internal static void EndAction()
        {
            EndAction(null);
        }

        internal static void EndAction(string Action)
        {
#if PREVIEW
            if (LogAction != null && (Action == null || LogAction == Action))
            {
                Action = LogAction;
                LogAction = null;
                string FileName = "";
                if (App.Config.RegistrationName != null)
                    FileName += App.Config.RegistrationName + " - ";
                FileName += DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " - " + Action + " - " +
                    Assembly.GetExecutingAssembly().GetName().Version.Major.ToString() + "." +
                    Assembly.GetExecutingAssembly().GetName().Version.Minor.ToString() + "." +
                    Assembly.GetExecutingAssembly().GetName().Version.Build.ToString() + "." +
                    Assembly.GetExecutingAssembly().GetName().Version.Revision.ToString() + ".log";

                // Normalize filename
                try
                {
                    FileName = Encoding.ASCII.GetString(Encoding.GetEncoding("ISO-8859-8").GetBytes(FileName));
                }
                catch { }
                FileName = FileName.Replace("?", "");

                if (Action.ToLower() == "registration")
                    Uploader.Upload(FileName, LogBuilder.ToString());
                else
                {
                    try
                    {
                        Uploader.Upload(FileName, LogBuilder.ToString());
                    }
                    catch { }
                }
            }
#endif
        }
    }
}
