// Copyright (c) 2018, Rene Lergner - wpinternals.net - @Heathcliff74xda
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
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using System.IO.Compression;
using System.Collections;
using System.Reflection;
using System.Net;

namespace WPinternals
{
    internal delegate void SetWorkingStatus(string Message, string SubMessage = null, ulong? MaxProgressValue = null, bool ShowAnimation = true, WPinternalsStatus Status = WPinternalsStatus.Undefined);
    internal delegate void UpdateWorkingStatus(string Message, string SubMessage = null, ulong? CurrentProgressValue = null, WPinternalsStatus Status = WPinternalsStatus.Undefined);
    internal delegate void ExitSuccess(string Message, string SubMessage = null);
    internal delegate void ExitFailure(string Message, string SubMessage = null);

    internal enum WPinternalsStatus
    {
        Undefined,
        Scanning,
        Flashing,
        Patching,
        WaitingForManualReset,
        SwitchingMode,
        Initializing
    };

    public class BooleanConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty OnTrueProperty =
            DependencyProperty.Register("OnTrue", typeof(object), typeof(BooleanConverter),
                new PropertyMetadata(default(object)));

        public static readonly DependencyProperty OnFalseProperty =
            DependencyProperty.Register("OnFalse", typeof(object), typeof(BooleanConverter),
                new PropertyMetadata(default(object)));

        public static readonly DependencyProperty OnNullProperty =
            DependencyProperty.Register("OnNull", typeof(object), typeof(BooleanConverter),
                new PropertyMetadata(default(object)));

        public object OnTrue
        {
            get { return GetValue(OnTrueProperty); }
            set { SetValue(OnTrueProperty, value); }
        }

        public object OnFalse
        {
            get { return GetValue(OnFalseProperty); }
            set { SetValue(OnFalseProperty, value); }
        }

        public object OnNull
        {
            get { return GetValue(OnNullProperty); }
            set { SetValue(OnNullProperty, value); }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            value = value == null
                ? OnNull ?? Default(targetType)
                : (string.Equals(value.ToString(), false.ToString(), StringComparison.CurrentCultureIgnoreCase)
                    ? OnFalse
                    : OnTrue);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == OnNull) return Default(targetType);
            if (value == OnFalse) return false;
            if (value == OnTrue) return true;
            if (value == null) return null;

            if (OnNull != null &&
                string.Equals(value.ToString(), OnNull.ToString(), StringComparison.CurrentCultureIgnoreCase))
                return Default(targetType);

            if (OnFalse != null &&
                string.Equals(value.ToString(), OnFalse.ToString(), StringComparison.CurrentCultureIgnoreCase))
                return false;

            if (OnTrue != null &&
                string.Equals(value.ToString(), OnTrue.ToString(), StringComparison.CurrentCultureIgnoreCase))
                return true;

            return null;
        }

        public static object Default(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    public class HexConverter : DependencyObject, IValueConverter
    {
        public static readonly DependencyProperty SeparatorProperty =
            DependencyProperty.Register("OnTrue", typeof(object), typeof(HexConverter),
                new PropertyMetadata(" "));

        public object Separator
        {
            get { return GetValue(SeparatorProperty); }
            set { SetValue(SeparatorProperty, value); }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is byte[])
            {
                byte[] bytes = (byte[])value;
                StringBuilder s = new StringBuilder(1000);
                for (int i = bytes.GetLowerBound(0); i <= bytes.GetUpperBound(0); i++)
                {
                    if (i != bytes.GetLowerBound(0))
                        s.Append(Separator);
                    s.Append(bytes[i].ToString("X2"));
                }
                return s.ToString();
            }
            else
                return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static object Default(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    public class ObjectToVisibilityConverter : DependencyObject, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "Collapsed";
            else
                return "Visible";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static object Default(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    public class InverseObjectToVisibilityConverter : DependencyObject, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "Visible";
            else
                return "Collapsed";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static object Default(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    internal class CollapsibleRun : Run
    {
        private string CollapsibleText;

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            CollapsibleText = Text;
        }

        public Boolean IsVisible
        {
            get 
            {
                return (Boolean)this.GetValue(IsVisibleProperty);
            }
            set 
            {
                this.SetValue(IsVisibleProperty, value);
            }
        }
        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
          "IsVisible", typeof(Boolean), typeof(CollapsibleRun), new PropertyMetadata(true, IsVisibleChanged));
        public static void IsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                ((CollapsibleRun)d).Text = ((CollapsibleRun)d).CollapsibleText;
            else
                ((CollapsibleRun)d).Text = String.Empty;
        }
    }

    internal class CollapsibleSection : Section
    {
        public CollapsibleSection()
        {
            CollapsibleBlocks = new List<Block>();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            foreach (Block Block in Blocks)
                CollapsibleBlocks.Add(Block);

            Blocks.Clear();
        }

        public List<Block> CollapsibleBlocks { get; private set; }

        public Boolean IsCollapsed
        {
            get
            {
                return (Boolean)this.GetValue(IsCollapsedProperty);
            }
            set
            {
                this.SetValue(IsCollapsedProperty, value);

                Blocks.Clear();

                if (IsInitialized)
                {
                    if (!value)
                    {
                        foreach (Block Block in CollapsibleBlocks)
                            Blocks.Add(Block);
                    }
                }
            }
        }
        public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(
          "IsCollapsed", typeof(Boolean), typeof(CollapsibleSection), new PropertyMetadata(false));
    }

    // Overloaded Paragraph class to remove empty Run-elements, caused by auto-formatting new-lines in the XAML
    // Use local:Paragraph in a FlowDocument
    // This correction only works at run-time
    public class Paragraph : System.Windows.Documents.Paragraph
    {
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            int inlinesCount = this.Inlines.Count;
            for (int i = 0; i < inlinesCount; i++)
            {
                System.Windows.Documents.Inline inline = this.Inlines.ElementAt(i);
                if (inline is System.Windows.Documents.Run)
                {
                    if ((inline as System.Windows.Documents.Run).Text == Convert.ToChar(32).ToString()) //ACSII 32 is the white space
                    {
                        (inline as System.Windows.Documents.Run).Text = string.Empty;
                    }
                }
            }
        }
    }

    internal class ArrivalEventArgs : EventArgs
    {
        public PhoneInterfaces NewInterface;
        public IDisposable NewModel;

        public ArrivalEventArgs(PhoneInterfaces NewInterface, IDisposable NewModel)
            : base()
        {
            this.NewInterface = NewInterface;
            this.NewModel = NewModel;
        }
    }

    internal static class BigEndian
    {
        public static byte[] GetBytes(object Value)
        {
            byte[] Bytes;
            if (Value is short)
                Bytes = BitConverter.GetBytes((short)Value);
            else if (Value is ushort)
                Bytes = BitConverter.GetBytes((ushort)Value);
            else if (Value is int)
                Bytes = BitConverter.GetBytes((int)Value);
            else if (Value is uint)
                Bytes = BitConverter.GetBytes((uint)Value);
            else
                throw new NotSupportedException();

            byte[] Result = new byte[Bytes.Length];
            for (int i = 0; i < Bytes.Length; i++)
                Result[i] = Bytes[Bytes.Length - 1 - i];

            return Result;
        }

        public static byte[] GetBytes(object Value, int Width)
        {
            byte[] Result;
            byte[] BigEndianBytes = GetBytes(Value);
            if (BigEndianBytes.Length == Width)
                return BigEndianBytes;
            else if (BigEndianBytes.Length > Width)
            {
                Result = new byte[Width];
                System.Buffer.BlockCopy(BigEndianBytes, BigEndianBytes.Length - Width, Result, 0, Width);
                return Result;
            }
            else
            {
                Result = new byte[Width];
                System.Buffer.BlockCopy(BigEndianBytes, 0, Result, Width - BigEndianBytes.Length, BigEndianBytes.Length);
                return Result;
            }
        }

        public static UInt16 ToUInt16(byte[] Buffer, int Offset)
        {
            byte[] Bytes = new byte[2];
            for (int i = 0; i < 2; i++)
                Bytes[i] = Buffer[Offset + 1 - i];
            return BitConverter.ToUInt16(Bytes, 0);
        }

        public static Int16 ToInt16(byte[] Buffer, int Offset)
        {
            byte[] Bytes = new byte[2];
            for (int i = 0; i < 2; i++)
                Bytes[i] = Buffer[Offset + 1 - i];
            return BitConverter.ToInt16(Bytes, 0);
        }

        public static UInt32 ToUInt32(byte[] Buffer, int Offset)
        {
            byte[] Bytes = new byte[4];
            for (int i = 0; i < 4; i++)
                Bytes[i] = Buffer[Offset + 3 - i];
            return BitConverter.ToUInt32(Bytes, 0);
        }

        public static Int32 ToInt32(byte[] Buffer, int Offset)
        {
            byte[] Bytes = new byte[4];
            for (int i = 0; i < 4; i++)
                Bytes[i] = Buffer[Offset + 3 - i];
            return BitConverter.ToInt32(Bytes, 0);
        }
    }

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
            get { return (int)GetValue(FrameIndexProperty); }
            set { SetValue(FrameIndexProperty, value); }
        }

        private void Initialize()
        {
            _gifDecoder = new GifBitmapDecoder(new Uri("pack://application:,,," + this.GifSource), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            _animation = new Int32Animation(0, _gifDecoder.Frames.Count - 1, new Duration(new TimeSpan(0, 0, 0, _gifDecoder.Frames.Count / 10, (int)((_gifDecoder.Frames.Count / 10.0 - _gifDecoder.Frames.Count / 10) * 1000))));
            _animation.RepeatBehavior = RepeatBehavior.Forever;
            this.Source = _gifDecoder.Frames[0];

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

        static void ChangingFrameIndex(DependencyObject obj, DependencyPropertyChangedEventArgs ev)
        {
            var gifImage = obj as GifImage;
            gifImage.Source = gifImage._gifDecoder.Frames[(int)ev.NewValue];
        }

        public bool AutoStart
        {
            get { return (bool)GetValue(AutoStartProperty); }
            set { SetValue(AutoStartProperty, value); }
        }

        public static readonly DependencyProperty AutoStartProperty =
            DependencyProperty.Register("AutoStart", typeof(bool), typeof(GifImage), new UIPropertyMetadata(false, AutoStartPropertyChanged));

        private static void AutoStartPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                (sender as GifImage).StartAnimation();
        }

        public string GifSource
        {
            get { return (string)GetValue(GifSourceProperty); }
            set { SetValue(GifSourceProperty, value); }
        }

        public static readonly DependencyProperty GifSourceProperty =
            DependencyProperty.Register("GifSource", typeof(string), typeof(GifImage), new UIPropertyMetadata(string.Empty, GifSourcePropertyChanged));

        private static void GifSourcePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            (sender as GifImage).Initialize();
        }

        public void StartAnimation()
        {
            if (!_isInitialized)
                this.Initialize();

            BeginAnimation(FrameIndexProperty, _animation);
        }

        public void StopAnimation()
        {
            BeginAnimation(FrameIndexProperty, null);
        }
    }

    internal enum LogType
    {
        FileOnly,
        FileAndConsole,
        ConsoleOnly
    };

    internal static class LogFile
    {
        private static StreamWriter w = null;
        private static object lockobject = new object();
#if PREVIEW
        private static string LogAction = null;
        private static StringBuilder LogBuilder;
#endif

        static LogFile()
        {
            try
            {
                if (!Directory.Exists(Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals")))
                    Directory.CreateDirectory(Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals"));
                w = File.AppendText(Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\WPInternals.log"));
            }
            catch { }
        }

        public static void Log(string logMessage, LogType Type = LogType.FileOnly)
        {
            if (w == null) return;

            lock (lockobject)
            {
                if ((Type == LogType.FileOnly) || (Type == LogType.FileAndConsole))
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

                if ((CommandLine.IsConsoleVisible) && ((Type == LogType.ConsoleOnly) || (Type == LogType.FileAndConsole)))
                    Console.WriteLine(logMessage);
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
                    Log(Indent + ((WPinternalsException)CurrentEx).SubMessage, Type);
#if DEBUG
                if (CurrentEx.StackTrace != null)
                    Log(Indent + CurrentEx.StackTrace, LogType.FileOnly);
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
            Log("Copyright Heathcliff74 / wpinternals.net", LogType.FileAndConsole);
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
                LogBuilder.AppendLine("Copyright Heathcliff74 / wpinternals.net");
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
            if ((LogAction != null) && ((Action == null) || (LogAction == Action)))
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
                    FileName = System.Text.Encoding.ASCII.GetString(System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(FileName));
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

    public static class Converter
    {
        public static string ConvertHexToString(byte[] Bytes, string Separator)
        {
            StringBuilder s = new StringBuilder(1000);
            for (int i = Bytes.GetLowerBound(0); i <= Bytes.GetUpperBound(0); i++)
            {
                if (i != Bytes.GetLowerBound(0))
                    s.Append(Separator);
                s.Append(Bytes[i].ToString("X2"));
            }
            return s.ToString();
        }

        public static byte[] ConvertStringToHex(string HexString)
        {
            if (HexString.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[HexString.Length >> 1];

            for (int i = 0; i < (HexString.Length >> 1); ++i)
            {
                arr[i] = (byte)((GetHexVal(HexString[i << 1]) << 4) + (GetHexVal(HexString[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

    }

    // This class was found online.
    // Original author is probably: John Melville
    // https://social.msdn.microsoft.com/Forums/en-US/163ef755-ff7b-4ea5-b226-bbe8ef5f4796/is-there-a-pattern-for-calling-an-async-method-synchronously?forum=async
    public static class AsyncHelpers
    {
        /// <summary>
        /// Execute's an async Task<T> method which has a void return value synchronously
        /// </summary>
        /// <param name="task">Task<T> method to execute</param>
        public static void RunSync(Func<Task> task)
        {
            var oldContext = SynchronizationContext.Current;
            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synch);
            synch.Post(async _ =>
            {
                try
                {
                    await task();
                }
                catch (Exception e)
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null);
            synch.BeginMessageLoop();

            SynchronizationContext.SetSynchronizationContext(oldContext);
        }

        /// <summary>
        /// Execute's an async Task<T> method which has a T return type synchronously
        /// </summary>
        /// <typeparam name="T">Return Type</typeparam>
        /// <param name="task">Task<T> method to execute</param>
        /// <returns></returns>
        public static T RunSync<T>(Func<Task<T>> task)
        {
            var oldContext = SynchronizationContext.Current;
            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synch);
            T ret = default(T);
            synch.Post(async _ =>
            {
                try
                {
                    ret = await task();
                }
                catch (Exception e)
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null);
            synch.BeginMessageLoop();
            SynchronizationContext.SetSynchronizationContext(oldContext);
            return ret;
        }

        private class ExclusiveSynchronizationContext : SynchronizationContext
        {
            private bool done;
            public Exception InnerException { get; set; }
            readonly AutoResetEvent workItemsWaiting = new AutoResetEvent(false);
            readonly Queue<Tuple<SendOrPostCallback, object>> items =
                new Queue<Tuple<SendOrPostCallback, object>>();

            public override void Send(SendOrPostCallback d, object state)
            {
                throw new NotSupportedException("We cannot send to our same thread");
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (items)
                {
                    items.Enqueue(Tuple.Create(d, state));
                }
                workItemsWaiting.Set();
            }

            public void EndMessageLoop()
            {
                Post(_ => done = true, null);
            }

            public void BeginMessageLoop()
            {
                while (!done)
                {
                    Tuple<SendOrPostCallback, object> task = null;
                    lock (items)
                    {
                        if (items.Count > 0)
                        {
                            task = items.Dequeue();
                        }
                    }
                    if (task != null)
                    {
                        task.Item1(task.Item2);
                        if (InnerException != null) // the method threw an exeption
                        {
                            throw new AggregateException("AsyncHelpers.Run method threw an exception.", InnerException);
                        }
                    }
                    else
                    {
                        workItemsWaiting.WaitOne();
                    }
                }
            }

            public override SynchronizationContext CreateCopy()
            {
                return this;
            }
        }
    }

    // This class is taken from the Prism library by Microsoft Patterns & Practices
    // License: http://compositewpf.codeplex.com/license
    internal static class WeakEventHandlerManager
    {
        public static void AddWeakReferenceHandler(ref List<WeakReference> handlers, EventHandler handler, int defaultListSize)
        {
            if (handlers == null)
            {
                handlers = (defaultListSize > 0) ? new List<WeakReference>(defaultListSize) : new List<WeakReference>();
            }
            handlers.Add(new WeakReference(handler));
        }

        private static void CallHandler(object sender, EventHandler eventHandler)
        {
            DispatcherProxy proxy = DispatcherProxy.CreateDispatcher();
            if (eventHandler != null)
            {
                if ((proxy != null) && !proxy.CheckAccess())
                {
                    proxy.BeginInvoke(new Action<object, EventHandler>(WeakEventHandlerManager.CallHandler), new object[] { sender, eventHandler });
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
                EventHandler target = reference.Target as EventHandler;
                if (target == null)
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
                    EventHandler target = reference.Target as EventHandler;
                    if ((target == null) || (target == handler))
                    {
                        handlers.RemoveAt(i);
                    }
                }
            }
        }

        private class DispatcherProxy
        {
            private Dispatcher innerDispatcher;

            private DispatcherProxy(Dispatcher dispatcher)
            {
                this.innerDispatcher = dispatcher;
            }

            public DispatcherOperation BeginInvoke(Delegate method, params object[] args)
            {
                return this.innerDispatcher.BeginInvoke(method, DispatcherPriority.Normal, args);
            }

            public bool CheckAccess()
            {
                return this.innerDispatcher.CheckAccess();
            }

            public static WeakEventHandlerManager.DispatcherProxy CreateDispatcher()
            {
                if (Application.Current == null)
                {
                    return null;
                }
                return new WeakEventHandlerManager.DispatcherProxy(Application.Current.Dispatcher);
            }
        }
    }

    // This interface is taken from the Prism library by Microsoft Patterns & Practices
    // License: http://compositewpf.codeplex.com/license
    public interface IActiveAware
    {
        /// <summary>
        /// Gets or sets a value indicating whether the object is active.
        /// </summary>
        /// <value><see langword="true" /> if the object is active; otherwise <see langword="false" />.</value>
        bool IsActive { get; set; }

        /// <summary>
        /// Notifies that the value for <see cref="IsActive"/> property has changed.
        /// </summary>
        event EventHandler IsActiveChanged;
    }

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
                WeakEventHandlerManager.AddWeakReferenceHandler(ref this._canExecuteChangedHandlers, value, 2);
            }
            remove
            {
                WeakEventHandlerManager.RemoveWeakReferenceHandler(this._canExecuteChangedHandlers, value);
            }
        }

        public event EventHandler IsActiveChanged;

        protected DelegateCommandBase(Action<object> executeMethod, Func<object, bool> canExecuteMethod)
        {
            if ((executeMethod == null) || (canExecuteMethod == null))
            {
                throw new ArgumentNullException("executeMethod", "Delegate Command Delegates Cannot Be Null");
            }
            this.executeMethod = executeMethod;
            this.canExecuteMethod = canExecuteMethod;
        }

        protected bool CanExecute(object parameter)
        {
            if (this.canExecuteMethod != null)
            {
                return this.canExecuteMethod(parameter);
            }
            return true;
        }

        protected void Execute(object parameter)
        {
            this.executeMethod(parameter);
        }

        protected virtual void OnCanExecuteChanged()
        {
            WeakEventHandlerManager.CallWeakReferenceHandlers(this, this._canExecuteChangedHandlers);
        }

        protected virtual void OnIsActiveChanged()
        {
            EventHandler isActiveChanged = this.IsActiveChanged;
            if (isActiveChanged != null)
            {
                isActiveChanged(this, EventArgs.Empty);
            }
        }

        public void RaiseCanExecuteChanged()
        {
            this.OnCanExecuteChanged();
        }

        bool ICommand.CanExecute(object parameter)
        {
            return this.CanExecute(parameter);
        }

        void ICommand.Execute(object parameter)
        {
            this.Execute(parameter);
        }

        public bool IsActive
        {
            get
            {
                return this._isActive;
            }
            set
            {
                if (this._isActive != value)
                {
                    this._isActive = value;
                    this.OnIsActiveChanged();
                }
            }
        }
    }

    // This class is taken from the Prism library by Microsoft Patterns & Practices
    // License: http://compositewpf.codeplex.com/license
    public class DelegateCommand : DelegateCommandBase
    {
        public DelegateCommand(Action executeMethod)
            : this(executeMethod, () => true)
        {
        }

        public DelegateCommand(Action executeMethod, Func<bool> canExecuteMethod): base(o => executeMethod(), f => canExecuteMethod())
        {
        }

        public bool CanExecute()
        {
            return base.CanExecute(null);
        }

        public void Execute()
        {
            base.Execute(null);
        }
    }

    internal class FlowDocumentScrollViewerNoMouseWheel: FlowDocumentScrollViewer
    {
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
        }
    }

    internal class ProgressUpdater
    {
        private DateTime InitTime;
        private DateTime LastUpdateTime;
        private UInt64 MaxValue;
        private Action<int, TimeSpan?> ProgressUpdateCallback;
        internal int ProgressPercentage;

        internal ProgressUpdater(UInt64 MaxValue, Action<int, TimeSpan?> ProgressUpdateCallback)
        {
            InitTime = DateTime.Now;
            LastUpdateTime = DateTime.Now;
            this.MaxValue = MaxValue;
            this.ProgressUpdateCallback = ProgressUpdateCallback;
            SetProgress(0);
        }

        private UInt64 _Progress;
        internal UInt64 Progress
        {
            get
            {
                return _Progress;
            }
        }

        internal void SetProgress(UInt64 NewValue)
        {
            if (_Progress != NewValue)
            {
                int PreviousProgressPercentage = (int)((double)_Progress / MaxValue * 100);
                ProgressPercentage = (int)((double)NewValue / MaxValue * 100);

                _Progress = NewValue;

                if (((DateTime.Now - LastUpdateTime) > TimeSpan.FromSeconds(0.5)) || (ProgressPercentage == 100))
                {
#if DEBUG
                    Console.WriteLine("Init time: " + InitTime.ToShortTimeString() + " / Now: " + DateTime.Now.ToString() + " / NewValue: " + NewValue.ToString() + " / MaxValue: " + MaxValue.ToString() + " ->> Percentage: " + ProgressPercentage.ToString() + " / Remaining: " + TimeSpan.FromTicks((long)((DateTime.Now - InitTime).Ticks / ((double)NewValue / MaxValue) * ((double)1 - ((double)NewValue / MaxValue)))).ToString());
#endif

                    if (((DateTime.Now - InitTime) < TimeSpan.FromSeconds(30)) && (ProgressPercentage < 15))
                        ProgressUpdateCallback(ProgressPercentage, null);
                    else
                        ProgressUpdateCallback(ProgressPercentage, TimeSpan.FromTicks((long)((DateTime.Now - InitTime).Ticks / ((double)NewValue / MaxValue) * ((double)1 - ((double)NewValue / MaxValue)))));

                    LastUpdateTime = DateTime.Now;
                }
            }
        }

        internal void IncreaseProgress(UInt64 Progress)
        {
            SetProgress(_Progress + Progress);
        }
    }

    internal static class Compression
    {
        internal static Stream GetDecompressedStreamWithSeek(Stream InputStream)
        {
            long P = InputStream.Position;
            byte[] GZipHeader = new byte[3];
            InputStream.Read(GZipHeader, 0, 3);
            InputStream.Position = P;
            if (StructuralComparisons.StructuralEqualityComparer.Equals(GZipHeader, new byte[] { 0x1F, 0x8B, 0x08 }))
                return new GZipStream(InputStream, CompressionMode.Decompress, false);
            else
                return InputStream;
        }

        internal static bool IsCompressedStream(Stream InputStream)
        {
            byte[] GZipHeader = new byte[3];
            InputStream.Read(GZipHeader, 0, 3);
            return StructuralComparisons.StructuralEqualityComparer.Equals(GZipHeader, new byte[] { 0x1F, 0x8B, 0x08 });
        }

        internal static GZipStream GetDecompressedStream(Stream InputStream)
        {
            return new GZipStream(InputStream, CompressionMode.Decompress, false);
        }
    }

    internal class WPinternalsException: Exception
    {
        // Message and SubMessaage are always printable
        internal string SubMessage = null;

        internal WPinternalsException() : base() { }

        internal WPinternalsException(string Message) : base(Message) { }

        internal WPinternalsException(string Message, Exception InnerException) : base(Message, InnerException) { }

        internal WPinternalsException(string Message, string SubMessage) : base(Message) { this.SubMessage = SubMessage; }

        internal WPinternalsException(string Message, string SubMessage, Exception InnerException) : base(Message, InnerException) { this.SubMessage = SubMessage; }
    }

    // This class is written by: Eugene Beresovsky
    // https://stackoverflow.com/questions/13035925/stream-wrapper-to-make-stream-seekable/28036366#28036366
    internal class ReadSeekableStream : Stream
    {
        private long _underlyingPosition;
        private readonly byte[] _seekBackBuffer;
        private int _seekBackBufferCount;
        private int _seekBackBufferIndex;
        private readonly Stream _underlyingStream;

        public ReadSeekableStream(Stream underlyingStream, int seekBackBufferSize)
        {
            if (!underlyingStream.CanRead)
                throw new Exception("Provided stream " + underlyingStream + " is not readable");
            _underlyingStream = underlyingStream;
            _seekBackBuffer = new byte[seekBackBufferSize];
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int copiedFromBackBufferCount = 0;
            if (_seekBackBufferIndex < _seekBackBufferCount)
            {
                copiedFromBackBufferCount = Math.Min(count, _seekBackBufferCount - _seekBackBufferIndex);
                Buffer.BlockCopy(_seekBackBuffer, _seekBackBufferIndex, buffer, offset, copiedFromBackBufferCount);
                offset += copiedFromBackBufferCount;
                count -= copiedFromBackBufferCount;
                _seekBackBufferIndex += copiedFromBackBufferCount;
            }
            int bytesReadFromUnderlying = 0;
            if (count > 0)
            {
                bytesReadFromUnderlying = _underlyingStream.Read(buffer, offset, count);
                if (bytesReadFromUnderlying > 0)
                {
                    _underlyingPosition += bytesReadFromUnderlying;

                    var copyToBufferCount = Math.Min(bytesReadFromUnderlying, _seekBackBuffer.Length);
                    var copyToBufferOffset = Math.Min(_seekBackBufferCount, _seekBackBuffer.Length - copyToBufferCount);
                    var bufferBytesToMove = Math.Min(_seekBackBufferCount - 1, copyToBufferOffset);

                    if (bufferBytesToMove > 0)
                        Buffer.BlockCopy(_seekBackBuffer, _seekBackBufferCount - bufferBytesToMove, _seekBackBuffer, 0, bufferBytesToMove);
                    Buffer.BlockCopy(buffer, offset, _seekBackBuffer, copyToBufferOffset, copyToBufferCount);
                    _seekBackBufferCount = Math.Min(_seekBackBuffer.Length, _seekBackBufferCount + copyToBufferCount);
                    _seekBackBufferIndex = _seekBackBufferCount;
                }
            }
            return copiedFromBackBufferCount + bytesReadFromUnderlying;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.End)
                return SeekFromEnd((int)Math.Max(0, -offset));

            var relativeOffset = origin == SeekOrigin.Current
                ? offset
                : offset - Position;

            if (relativeOffset == 0)
                return Position;
            else if (relativeOffset > 0)
                return SeekForward(relativeOffset);
            else
                return SeekBackwards(-relativeOffset);
        }

        private long SeekForward(long origOffset)
        {
            long offset = origOffset;
            var seekBackBufferLength = _seekBackBuffer.Length;

            int backwardSoughtBytes = _seekBackBufferCount - _seekBackBufferIndex;
            int seekForwardInBackBuffer = (int)Math.Min(offset, backwardSoughtBytes);
            offset -= seekForwardInBackBuffer;
            _seekBackBufferIndex += seekForwardInBackBuffer;

            if (offset > 0)
            {
                // first completely fill seekBackBuffer to remove special cases from while loop below
                if (_seekBackBufferCount < seekBackBufferLength)
                {
                    var maxRead = seekBackBufferLength - _seekBackBufferCount;
                    if (offset < maxRead)
                        maxRead = (int)offset;
                    var bytesRead = _underlyingStream.Read(_seekBackBuffer, _seekBackBufferCount, maxRead);
                    _underlyingPosition += bytesRead;
                    _seekBackBufferCount += bytesRead;
                    _seekBackBufferIndex = _seekBackBufferCount;
                    if (bytesRead < maxRead)
                    {
                        if (_seekBackBufferCount < offset)
                            throw new NotSupportedException("Reached end of stream seeking forward " + origOffset + " bytes");
                        return Position;
                    }
                    offset -= bytesRead;
                }

                // now alternate between filling tempBuffer and seekBackBuffer
                bool fillTempBuffer = true;
                var tempBuffer = new byte[seekBackBufferLength];
                while (offset > 0)
                {
                    var maxRead = offset < seekBackBufferLength ? (int)offset : seekBackBufferLength;
                    var bytesRead = _underlyingStream.Read(fillTempBuffer ? tempBuffer : _seekBackBuffer, 0, maxRead);
                    _underlyingPosition += bytesRead;
                    var bytesReadDiff = maxRead - bytesRead;
                    offset -= bytesRead;
                    if (bytesReadDiff > 0 /* reached end-of-stream */ || offset == 0)
                    {
                        if (fillTempBuffer)
                        {
                            if (bytesRead > 0)
                            {
                                Buffer.BlockCopy(_seekBackBuffer, bytesRead, _seekBackBuffer, 0, bytesReadDiff);
                                Buffer.BlockCopy(tempBuffer, 0, _seekBackBuffer, bytesReadDiff, bytesRead);
                            }
                        }
                        else
                        {
                            if (bytesRead > 0)
                                Buffer.BlockCopy(_seekBackBuffer, 0, _seekBackBuffer, bytesReadDiff, bytesRead);
                            Buffer.BlockCopy(tempBuffer, bytesRead, _seekBackBuffer, 0, bytesReadDiff);
                        }
                        if (offset > 0)
                            throw new NotSupportedException("Reached end of stream seeking forward " + origOffset + " bytes");
                    }
                    fillTempBuffer = !fillTempBuffer;
                }
            }
            return Position;
        }

        private long SeekBackwards(long offset)
        {
            var intOffset = (int)offset;
            if (offset > int.MaxValue || intOffset > _seekBackBufferIndex)
                throw new NotSupportedException("Cannot currently seek backwards more than " + _seekBackBufferIndex + " bytes");
            _seekBackBufferIndex -= intOffset;
            return Position;
        }

        private long SeekFromEnd(long offset)
        {
            var intOffset = (int)offset;
            var seekBackBufferLength = _seekBackBuffer.Length;
            if (offset > int.MaxValue || intOffset > seekBackBufferLength)
                throw new NotSupportedException("Cannot seek backwards from end more than " + seekBackBufferLength + " bytes");

            // first completely fill seekBackBuffer to remove special cases from while loop below
            if (_seekBackBufferCount < seekBackBufferLength)
            {
                var maxRead = seekBackBufferLength - _seekBackBufferCount;
                var bytesRead = _underlyingStream.Read(_seekBackBuffer, _seekBackBufferCount, maxRead);
                _underlyingPosition += bytesRead;
                _seekBackBufferCount += bytesRead;
                _seekBackBufferIndex = Math.Max(0, _seekBackBufferCount - intOffset);
                if (bytesRead < maxRead)
                {
                    if (_seekBackBufferCount < intOffset)
                        throw new NotSupportedException("Could not seek backwards from end " + intOffset + " bytes");
                    return Position;
                }
            }
            else
            {
                _seekBackBufferIndex = _seekBackBufferCount;
            }

            // now alternate between filling tempBuffer and seekBackBuffer
            bool fillTempBuffer = true;
            var tempBuffer = new byte[seekBackBufferLength];
            while (true)
            {
                var bytesRead = _underlyingStream.Read(fillTempBuffer ? tempBuffer : _seekBackBuffer, 0, seekBackBufferLength);
                _underlyingPosition += bytesRead;
                var bytesReadDiff = seekBackBufferLength - bytesRead;
                if (bytesReadDiff > 0) // reached end-of-stream
                {
                    if (fillTempBuffer)
                    {
                        if (bytesRead > 0)
                        {
                            Buffer.BlockCopy(_seekBackBuffer, bytesRead, _seekBackBuffer, 0, bytesReadDiff);
                            Buffer.BlockCopy(tempBuffer, 0, _seekBackBuffer, bytesReadDiff, bytesRead);
                        }
                    }
                    else
                    {
                        if (bytesRead > 0)
                            Buffer.BlockCopy(_seekBackBuffer, 0, _seekBackBuffer, bytesReadDiff, bytesRead);
                        Buffer.BlockCopy(tempBuffer, bytesRead, _seekBackBuffer, 0, bytesReadDiff);
                    }
                    _seekBackBufferIndex -= intOffset;
                    return Position;
                }
                fillTempBuffer = !fillTempBuffer;
            }
        }

        public override long Position
        {
            get { return _underlyingPosition - (_seekBackBufferCount - _seekBackBufferIndex); }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public override bool CanTimeout { get { return _underlyingStream.CanTimeout; } }
        public override bool CanWrite { get { return _underlyingStream.CanWrite; } }
        public override long Length { get { return _underlyingStream.Length; } }
        public override void SetLength(long value) { _underlyingStream.SetLength(value); }
        public override void Write(byte[] buffer, int offset, int count) { _underlyingStream.Write(buffer, offset, count); }
        public override void Flush() { _underlyingStream.Flush(); }
        public override void Close() { _underlyingStream.Close(); }
        public new void Dispose() { _underlyingStream.Dispose(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _underlyingStream.Dispose();
            }
        }

    }

    // For reading a compressed stream or normal stream
    internal class DecompressedStream : Stream
    {
        private Stream UnderlyingStream;
        private bool IsSourceCompressed;
        private UInt64 DecompressedLength;
        private Int64 ReadPosition = 0;

        // For reading a compressed stream
        internal DecompressedStream(Stream InputStream)
        {
            UnderlyingStream = new ReadSeekableStream(InputStream, 0x100);
            
            byte[] Signature = new byte["CompressedPartition".Length + 2];
            Signature[0x00] = 0xFF;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes("CompressedPartition"), 0, Signature, 0x01, "CompressedPartition".Length);
            Signature["CompressedPartition".Length + 1] = 0x00;

            int PrimaryHeaderSize = 0x0A + "CompressedPartition".Length;
            byte[] SignatureRead = new byte[Signature.Length];
            UnderlyingStream.Read(SignatureRead, 0, Signature.Length);

            IsSourceCompressed = StructuralComparisons.StructuralEqualityComparer.Equals(Signature, SignatureRead);
            if (IsSourceCompressed)
            {
                byte[] FormatVersionBytes = new byte[4];
                UnderlyingStream.Read(FormatVersionBytes, 0, 4);
                if (BitConverter.ToUInt32(FormatVersionBytes, 0) > 1) // Max supported format version = 1
                    throw new InvalidDataException();

                byte[] HeaderSizeBytes = new byte[4];
                UnderlyingStream.Read(HeaderSizeBytes, 0, 4);
                UInt32 HeaderSize = BitConverter.ToUInt32(HeaderSizeBytes, 0);

                if (HeaderSize >= (Signature.Length + 0x10))
                {
                    byte[] DecompressedLengthBytes = new byte[8];
                    UnderlyingStream.Read(DecompressedLengthBytes, 0, 8);
                    DecompressedLength = BitConverter.ToUInt64(DecompressedLengthBytes, 0);
                }
                else
                    throw new InvalidDataException();

                UInt32 HeaderBytesRemaining = (UInt32)(HeaderSize - Signature.Length - 0x10);
                if (HeaderBytesRemaining > 0)
                {
                    byte[] HeaderBytes = new byte[HeaderBytesRemaining];
                    UnderlyingStream.Read(HeaderBytes, 0, (int)HeaderBytesRemaining);
                }

                UnderlyingStream = new GZipStream(UnderlyingStream, CompressionMode.Decompress, false);
            }
            else
                UnderlyingStream.Position = 0;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override int Read(byte[] buffer, int offset, int count) 
        {
            int RealCount = UnderlyingStream.Read(buffer, offset, count);
            ReadPosition += RealCount;
            return RealCount;
        }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override long Position
        {
            get { return ReadPosition; }
            set { throw new NotSupportedException(); }
        }
        public override bool CanTimeout { get { return UnderlyingStream.CanTimeout; } }
        public override bool CanWrite { get { return true; } }
        public override long Length 
        { 
            get 
            {
                if (IsSourceCompressed)
                    return (long)DecompressedLength;
                else
                    return UnderlyingStream.Length;
            } 
        }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) {throw new NotSupportedException(); }
        public override void Flush() { UnderlyingStream.Flush(); }
        public override void Close() { UnderlyingStream.Close(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.UnderlyingStream.Dispose();
            }
        }
    }

    // For writing a compressed stream
    internal class CompressedStream: Stream
    {
        private UInt32 HeaderSize;
        private UInt64 WritePosition;
        private GZipStream UnderlyingStream;

        internal CompressedStream(Stream OutputStream, UInt64 TotalDecompressedStreamLength)
        {
            // Write header
            HeaderSize = (UInt32)(0x12 + "CompressedPartition".Length);
            OutputStream.WriteByte(0xFF);
            OutputStream.Write(Encoding.ASCII.GetBytes("CompressedPartition"), 0, "CompressedPartition".Length);
            OutputStream.WriteByte(0x00);
            OutputStream.Write(BitConverter.GetBytes((UInt32)1), 0, 4); // Format version = 1
            OutputStream.Write(BitConverter.GetBytes(HeaderSize), 0, 4); // Headersize
            OutputStream.Write(BitConverter.GetBytes(TotalDecompressedStreamLength), 0, 8);

            this.UnderlyingStream = new GZipStream(OutputStream, CompressionLevel.Optimal, false);
            WritePosition = 0;
        }

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override long Position
        {
            get { return (long)WritePosition; }
            set { throw new NotSupportedException(); }
        }
        public override bool CanTimeout { get { return UnderlyingStream.CanTimeout; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return (long)WritePosition; } }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) 
        {
            WritePosition += (UInt64)count;
            UnderlyingStream.Write(buffer, offset, count);
        }
        public override void Flush() { UnderlyingStream.Flush(); }
        public override void Close() { UnderlyingStream.Close(); }
        public new void Dispose() { UnderlyingStream.Dispose(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.UnderlyingStream.Dispose();
            }
        }
    }

    internal class SeekableStream: Stream        
    {
        private Stream UnderlyingStream;
        private Int64 ReadPosition = 0;
        private Func<Stream> StreamInitializer;
        private Int64 UnderlyingStreamLength;

        // For reading a compressed stream
        internal SeekableStream(Func<Stream> StreamInitializer, Int64? Length = null)
        {
            this.StreamInitializer = StreamInitializer;
            UnderlyingStream = StreamInitializer();
            if (Length != null)
                UnderlyingStreamLength = (Int64)Length;
            else
            {
                try
                {
                    UnderlyingStreamLength = UnderlyingStream.Length;
                }
                catch
                { 
                    throw new ArgumentException("Unknown stream length");
                }
            }
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override int Read(byte[] buffer, int offset, int count) 
        {
            int RealCount = UnderlyingStream.Read(buffer, offset, count);
            ReadPosition += RealCount;
            return RealCount;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (UnderlyingStream.CanSeek)
            {
                ReadPosition = UnderlyingStream.Seek(offset, origin);
                return ReadPosition;
            }
            else
            {
                Int64 NewPosition = 0;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        NewPosition = offset;
                        break;
                    case SeekOrigin.Current:
                        NewPosition = ReadPosition + offset;
                        break;
                    case SeekOrigin.End:
                        NewPosition = UnderlyingStreamLength - offset;
                        break;
                }
                if ((NewPosition < 0) || (NewPosition > UnderlyingStreamLength))
                    throw new ArgumentOutOfRangeException();
                if (NewPosition < ReadPosition)
                {
                    UnderlyingStream.Close();
                    UnderlyingStream = StreamInitializer();
                    ReadPosition = 0;
                }
                UInt64 Remaining;
                byte[] Buffer = new byte[16384];
                while (ReadPosition < NewPosition)
                {
                    Remaining = (UInt64)(NewPosition - ReadPosition);
                    if (Remaining > (UInt64)Buffer.Length)
                        Remaining = (UInt64)Buffer.Length;
                    UnderlyingStream.Read(Buffer, 0, (int)Remaining);
                    ReadPosition += (long)Remaining;
                }
                return ReadPosition;
            }
        }
        public override long Position
        {
            get
            {
                return ReadPosition;
            }
            set 
            {
                Seek(value, SeekOrigin.Begin);
            }
        }
        public override bool CanTimeout { get { return UnderlyingStream.CanTimeout; } }
        public override bool CanWrite { get { return false; } }
        public override long Length 
        { 
            get 
            {
                return UnderlyingStreamLength;
            } 
        }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override void Flush() { throw new NotSupportedException(); }
        public override void Close() { UnderlyingStream.Close(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.UnderlyingStream.Dispose();
            }
        }
    }

    internal enum ResourceType
    {
        RT_ACCELERATOR = 9, //Accelerator table.
        RT_ANICURSOR = 21, //Animated cursor.
        RT_ANIICON = 22, //Animated icon.
        RT_BITMAP = 2, //Bitmap resource.
        RT_CURSOR = 1, //Hardware-dependent cursor resource.
        RT_DIALOG = 5, //Dialog box.
        RT_DLGINCLUDE = 17, //Allows
        RT_FONT = 8, //Font resource.
        RT_FONTDIR = 7, //Font directory resource.
        RT_GROUP_CURSOR = ((RT_CURSOR) + 11), //Hardware-independent cursor resource.
        RT_GROUP_ICON = ((RT_ICON) + 11), //Hardware-independent icon resource.
        RT_HTML = 23, //HTML resource.
        RT_ICON = 3, //Hardware-dependent icon resource.
        RT_MANIFEST = 24, //Side-by-Side Assembly Manifest.
        RT_MENU = 4, //Menu resource.
        RT_MESSAGETABLE = 11, //Message-table entry.
        RT_PLUGPLAY = 19, //Plug and Play resource.
        RT_RCDATA = 10, //Application-defined resource (raw data).
        RT_STRING = 6, //String-table entry.
        RT_VERSION = 16, //Version resource.
        RT_VXD = 20, //
        RT_DLGINIT = 240,
        RT_TOOLBAR = 241
    };

    internal class PE
    {
        internal static byte[] GetResource(byte[] PEfile, int[] Index)
        {
            // Explanation of PE header here:
            // https://msdn.microsoft.com/en-us/library/ms809762.aspx?f=255&MSPPError=-2147217396

            UInt32 PEPointer = ByteOperations.ReadUInt32(PEfile, 0x3C);
            UInt16 OptionalHeaderSize = ByteOperations.ReadUInt16(PEfile, PEPointer + 0x14);
            UInt32 SectionTablePointer = PEPointer + 0x18 + OptionalHeaderSize;
            UInt16 SectionCount = ByteOperations.ReadUInt16(PEfile, PEPointer + 0x06);
            UInt32? ResourceSectionEntryPointer = null;
            for (int i = 0; i < SectionCount; i++)
            {
                string SectionName = ByteOperations.ReadAsciiString(PEfile, (UInt32)(SectionTablePointer + (i * 0x28)), 8);
                int e = SectionName.IndexOf('\0');
                if (e >= 0)
                    SectionName = SectionName.Substring(0, e);
                if (SectionName == ".rsrc")
                {
                    ResourceSectionEntryPointer = (UInt32)(SectionTablePointer + (i * 0x28));
                    break;
                }
            }
            if (ResourceSectionEntryPointer == null)
                throw new WPinternalsException("Resource-section not found");
            UInt32 ResourceRawSize = ByteOperations.ReadUInt32(PEfile, (UInt32)ResourceSectionEntryPointer + 0x10);
            UInt32 ResourceRawPointer = ByteOperations.ReadUInt32(PEfile, (UInt32)ResourceSectionEntryPointer + 0x14);
            UInt32 ResourceVirtualPointer = ByteOperations.ReadUInt32(PEfile, (UInt32)ResourceSectionEntryPointer + 0x0C);

            UInt32 p = ResourceRawPointer;
            for (int i = 0; i < Index.Length; i++)
            {
                UInt16 ResourceNamedEntryCount = ByteOperations.ReadUInt16(PEfile, p + 0x0c);
                UInt16 ResourceIdEntryCount = ByteOperations.ReadUInt16(PEfile, p + 0x0e);
                for (int j = ResourceNamedEntryCount; j < ResourceNamedEntryCount + ResourceIdEntryCount; j++)
                {
                    UInt32 ResourceID = ByteOperations.ReadUInt32(PEfile, (UInt32)(p + 0x10 + (j * 8)));
                    UInt32 NextPointer = ByteOperations.ReadUInt32(PEfile, (UInt32)(p + 0x10 + (j * 8) + 4));
                    if (ResourceID == (UInt32)Index[i])
                    {
                        // Check high bit
                        if (((NextPointer & 0x80000000) == 0) != (i == (Index.Length - 1)))
                            throw new WPinternalsException("Bad resource path");

                        p = ResourceRawPointer + (NextPointer & 0x7fffffff);
                        break;
                    }
                }
            }

            UInt32 ResourceValuePointer = ByteOperations.ReadUInt32(PEfile, p) - ResourceVirtualPointer + ResourceRawPointer;
            UInt32 ResourceValueSize = ByteOperations.ReadUInt32(PEfile, p + 4);

            byte[] ResourceValue = new byte[ResourceValueSize];
            Array.Copy(PEfile, ResourceValuePointer, ResourceValue, 0, ResourceValueSize);

            return ResourceValue;
        }

        internal static Version GetFileVersion(byte[] PEfile)
        {
            byte[] version = PE.GetResource(PEfile, new int[] { (int)ResourceType.RT_VERSION, 1, 1033 });

            // RT_VERSION format:
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms647001(v=vs.85).aspx
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms646997(v=vs.85).aspx
            UInt32 FixedFileInfoPointer = 0x28;
            UInt16 Major = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x0A);
            UInt16 Minor = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x08);
            UInt16 Build = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x0E);
            UInt16 Revision = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x0C);

            return new Version(Major, Minor, Build, Revision);
        }

        internal static Version GetProductVersion(byte[] PEfile)
        {
            byte[] version = PE.GetResource(PEfile, new int[] { (int)ResourceType.RT_VERSION, 1, 1033 });

            // RT_VERSION format:
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms647001(v=vs.85).aspx
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ms646997(v=vs.85).aspx
            UInt32 FixedFileInfoPointer = 0x28;
            UInt16 Major = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x12);
            UInt16 Minor = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x10);
            UInt16 Build = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x16);
            UInt16 Revision = ByteOperations.ReadUInt16(version, FixedFileInfoPointer + 0x14);

            return new Version(Major, Minor, Build, Revision);
        }
    }

#if PREVIEW
    internal static class Uploader
    {
        internal static List<Task> Uploads = new List<Task>();

        internal static void Upload(string FileName, string Text)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(Text);
            MemoryStream FileStream = new MemoryStream(byteArray);
            Upload(FileName, FileStream);
        }

        internal static void Upload(string FileName, byte[] Data)
        {
            Upload(FileName, new MemoryStream(Data));
        }

        internal static void Upload(string FileName, Stream FileStream)
        {
            Upload(new Uri(@"https://www.wpinternals.net/upload.php", UriKind.Absolute), "uploadedfile", FileName, FileStream);
        }

        private static void Upload(Uri Address, string InputName, string FileName, Stream FileStream)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            System.Net.Http.HttpClient httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.MultipartFormDataContent form = new System.Net.Http.MultipartFormDataContent();
            System.Net.Http.StreamContent Content = new System.Net.Http.StreamContent(FileStream);
            Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            form.Add(Content, InputName, FileName);
            Task<System.Net.Http.HttpResponseMessage> UploadTask = httpClient.PostAsync(Address, form);

            Uploads.Add(
                UploadTask.ContinueWith((t) =>
                {
                    Uploads.Remove(t);
                    httpClient.Dispose();
                })
            );
        }

        internal static void WaitForUploads()
        {
            Task.WaitAll(Uploads.ToArray());
        }
    }
#endif

    internal class AsyncAutoResetEvent
    {
        readonly LinkedList<TaskCompletionSource<bool>> waiters =
            new LinkedList<TaskCompletionSource<bool>>();

        bool isSignaled;

        public AsyncAutoResetEvent(bool signaled)
        {
            this.isSignaled = signaled;
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return this.WaitAsync(timeout, CancellationToken.None);
        }

        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> tcs;

            lock (this.waiters)
            {
                if (this.isSignaled)
                {
                    this.isSignaled = false;
                    return true;
                }
                else if (timeout == TimeSpan.Zero)
                {
                    return this.isSignaled;
                }
                else
                {
                    tcs = new TaskCompletionSource<bool>();
                    this.waiters.AddLast(tcs);
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
                lock (this.waiters)
                {
                    bool removed = this.waiters.Remove(tcs);
                    System.Diagnostics.Debug.Assert(removed);
                    return false;
                }
            }
        }

        public void Set()
        {
            TaskCompletionSource<bool> toRelease = null;

            lock (this.waiters)
            {
                if (this.waiters.Count > 0)
                {
                    // Signal the first task in the waiters list.
                    toRelease = this.waiters.First.Value;
                    this.waiters.RemoveFirst();
                }
                else if (!this.isSignaled)
                {
                    // No tasks are pending
                    this.isSignaled = true;
                }
            }

            if (toRelease != null)
            {
                toRelease.SetResult(true);
            }
        }
    }

    // This class was written by: Rolf Wessels
    // https://github.com/rolfwessels/lazycowprojects/tree/master/Wpf

    /// <summary>
    /// Static class used to attach to wpf control
    /// </summary>
    public static class GridViewColumnResize
    {
#region DependencyProperties

        public static readonly DependencyProperty WidthProperty =
            DependencyProperty.RegisterAttached("Width", typeof(string), typeof(GridViewColumnResize),
                                                new PropertyMetadata(OnSetWidthCallback));

        public static readonly DependencyProperty GridViewColumnResizeBehaviorProperty =
            DependencyProperty.RegisterAttached("GridViewColumnResizeBehavior",
                                                typeof(GridViewColumnResizeBehavior), typeof(GridViewColumnResize),
                                                null);

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(GridViewColumnResize),
                                                new PropertyMetadata(OnSetEnabledCallback));

        public static readonly DependencyProperty ListViewResizeBehaviorProperty =
            DependencyProperty.RegisterAttached("ListViewResizeBehaviorProperty",
                                                typeof(ListViewResizeBehavior), typeof(GridViewColumnResize), null);

#endregion

        public static string GetWidth(DependencyObject obj)
        {
            return (string)obj.GetValue(WidthProperty);
        }

        public static void SetWidth(DependencyObject obj, string value)
        {
            obj.SetValue(WidthProperty, value);
        }

        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

#region CallBack

        private static void OnSetWidthCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as GridViewColumn;
            if (element != null)
            {
                GridViewColumnResizeBehavior behavior = GetOrCreateBehavior(element);
                behavior.Width = e.NewValue as string;
            }
            else
            {
                Console.Error.WriteLine("Error: Expected type GridViewColumn but found " +
                                        dependencyObject.GetType().Name);
            }
        }

        private static void OnSetEnabledCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as ListView;
            if (element != null)
            {
                ListViewResizeBehavior behavior = GetOrCreateBehavior(element);
                behavior.Enabled = (bool)e.NewValue;
            }
            else
            {
                Console.Error.WriteLine("Error: Expected type ListView but found " + dependencyObject.GetType().Name);
            }
        }

        private static ListViewResizeBehavior GetOrCreateBehavior(ListView element)
        {
            var behavior = element.GetValue(GridViewColumnResizeBehaviorProperty) as ListViewResizeBehavior;
            if (behavior == null)
            {
                behavior = new ListViewResizeBehavior(element);
                element.SetValue(ListViewResizeBehaviorProperty, behavior);
            }

            return behavior;
        }

        private static GridViewColumnResizeBehavior GetOrCreateBehavior(GridViewColumn element)
        {
            var behavior = element.GetValue(GridViewColumnResizeBehaviorProperty) as GridViewColumnResizeBehavior;
            if (behavior == null)
            {
                behavior = new GridViewColumnResizeBehavior(element);
                element.SetValue(GridViewColumnResizeBehaviorProperty, behavior);
            }

            return behavior;
        }

#endregion

#region Nested type: GridViewColumnResizeBehavior
        
        // This class was written by: Rolf Wessels
        // https://github.com/rolfwessels/lazycowprojects/tree/master/Wpf

        /// <summary>
        /// GridViewColumn class that gets attached to the GridViewColumn control
        /// </summary>
        public class GridViewColumnResizeBehavior
        {
            private readonly GridViewColumn _element;

            public GridViewColumnResizeBehavior(GridViewColumn element)
            {
                _element = element;
            }

            public string Width { get; set; }

            public bool IsStatic
            {
                get { return StaticWidth >= 0; }
            }

            public double StaticWidth
            {
                get
                {
                    double result;
                    return double.TryParse(Width, out result) ? result : -1;
                }
            }

            public double Percentage
            {
                get
                {
                    if (!IsStatic)
                    {
                        return Mulitplier * 100;
                    }
                    return 0;
                }
            }

            public double Mulitplier
            {
                get
                {
                    if (Width == "*" || Width == "1*") return 1;
                    if (Width.EndsWith("*"))
                    {
                        double perc;
                        if (double.TryParse(Width.Substring(0, Width.Length - 1), out perc))
                        {
                            return perc;
                        }
                    }
                    return 1;
                }
            }

            public void SetWidth(double allowedSpace, double totalPercentage)
            {
                if (IsStatic)
                {
                    _element.Width = StaticWidth;
                }
                else
                {
                    double width = Math.Max(allowedSpace * (Percentage / totalPercentage), 0);
                    _element.Width = width;
                }
            }
        }

#endregion

#region Nested type: ListViewResizeBehavior

        // This class was written by: Rolf Wessels
        // https://github.com/rolfwessels/lazycowprojects/tree/master/Wpf
        
        /// <summary>
        /// ListViewResizeBehavior class that gets attached to the ListView control
        /// </summary>
        public class ListViewResizeBehavior
        {
            private const int Margin = 25;
            private const long RefreshTime = Timeout.Infinite;
            private const long Delay = 500;

            private readonly ListView _element;
            private readonly Timer _timer;

            public ListViewResizeBehavior(ListView element)
            {
                if (element == null) throw new ArgumentNullException("element");
                _element = element;
                element.Loaded += OnLoaded;

                // Action for resizing and re-enable the size lookup
                // This stops the columns from constantly resizing to improve performance
                Action resizeAndEnableSize = () =>
                {
                    Resize();
                    _element.SizeChanged += OnSizeChanged;
                };
                _timer = new Timer(x => Application.Current.Dispatcher.BeginInvoke(resizeAndEnableSize), null, Delay,
                                   RefreshTime);
            }

            public bool Enabled { get; set; }


            private void OnLoaded(object sender, RoutedEventArgs e)
            {
                _element.SizeChanged += OnSizeChanged;
            }

            private void OnSizeChanged(object sender, SizeChangedEventArgs e)
            {
                if (e.WidthChanged)
                {
                    _element.SizeChanged -= OnSizeChanged;
                    _timer.Change(Delay, RefreshTime);
                }
            }

            private void Resize()
            {
                if (Enabled)
                {
                    double totalWidth = _element.ActualWidth;
                    var gv = _element.View as GridView;
                    if (gv != null)
                    {
                        double allowedSpace = totalWidth - GetAllocatedSpace(gv);
                        allowedSpace = allowedSpace - Margin;
                        double totalPercentage = GridViewColumnResizeBehaviors(gv).Sum(x => x.Percentage);
                        foreach (GridViewColumnResizeBehavior behavior in GridViewColumnResizeBehaviors(gv))
                        {
                            behavior.SetWidth(allowedSpace, totalPercentage);
                        }
                    }
                }
            }

            private static IEnumerable<GridViewColumnResizeBehavior> GridViewColumnResizeBehaviors(GridView gv)
            {
                foreach (GridViewColumn t in gv.Columns)
                {
                    var gridViewColumnResizeBehavior =
                        t.GetValue(GridViewColumnResizeBehaviorProperty) as GridViewColumnResizeBehavior;
                    if (gridViewColumnResizeBehavior != null)
                    {
                        yield return gridViewColumnResizeBehavior;
                    }
                }
            }

            private static double GetAllocatedSpace(GridView gv)
            {
                double totalWidth = 0;
                foreach (GridViewColumn t in gv.Columns)
                {
                    var gridViewColumnResizeBehavior =
                        t.GetValue(GridViewColumnResizeBehaviorProperty) as GridViewColumnResizeBehavior;
                    if (gridViewColumnResizeBehavior != null)
                    {
                        if (gridViewColumnResizeBehavior.IsStatic)
                        {
                            totalWidth += gridViewColumnResizeBehavior.StaticWidth;
                        }
                    }
                    else
                    {
                        totalWidth += t.ActualWidth;
                    }
                }
                return totalWidth;
            }
        }

#endregion
    }

    internal static class ExtensionMethods
    {
        // This method was written by: Lawrence Johnston
        // https://stackoverflow.com/a/22078975
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {

            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {

                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    throw new TimeoutException("The operation has timed out.");
                }
            }
        }
    }
}
