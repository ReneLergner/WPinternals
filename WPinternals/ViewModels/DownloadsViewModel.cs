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

using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WPinternals
{
    internal class DownloadsViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel Notifier;
        private readonly Timer SpeedTimer;
        private bool IsSearching = false;

        internal DownloadsViewModel(PhoneNotifierViewModel Notifier)
        {
            IsSwitchingInterface = false;
            IsFlashModeOperation = false;
            this.Notifier = Notifier;
            Notifier.NewDeviceArrived += Notifier_NewDeviceArrived;

            RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"Software\WPInternals", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\WPInternals");

            DownloadFolder = (string)Key.GetValue("DownloadFolder", @"C:\ProgramData\WPinternals\Repository");
            Key.Close();

            SpeedTimer = new Timer(TimerCallback, this, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            AddFFUCommand = new DelegateCommand(() =>
            {
                string FFUPath = null;

                OpenFileDialog dlg = new();
                dlg.DefaultExt = ".ffu"; // Default file extension
                dlg.Filter = "ROM images (.ffu)|*.ffu"; // Filter files by extension 

                bool? result = dlg.ShowDialog();

                if (result == true)
                {
                    FFUPath = dlg.FileName;
                    string FFUFile = Path.GetFileName(FFUPath);

                    try
                    {
                        App.Config.AddFfuToRepository(FFUPath);
                        App.Config.WriteConfig();
                        LastStatusText = $"File \"{FFUFile}\" was added to the repository.";
                    }
                    catch (WPinternalsException Ex)
                    {
                        LastStatusText = $"Error: {Ex.Message}. File \"{FFUFile}\" was not added.";
                    }
                    catch
                    {
                        LastStatusText = $"Error: File \"{FFUFile}\" was not added.";
                    }
                }
                else
                {
                    LastStatusText = null;
                }
            });
        }

        private string _LastStatusText = null;
        public string LastStatusText
        {
            get
            {
                return _LastStatusText;
            }
            set
            {
                _LastStatusText = value;
                OnPropertyChanged(nameof(LastStatusText));
            }
        }

        internal static void TimerCallback(object State)
        {
            foreach (DownloadEntry Entry in App.DownloadManager.DownloadList)
            {
                if (Entry.SpeedIndex >= 0)
                {
                    int ArrayIndex = (int)(Entry.SpeedIndex % 10);
                    Entry.Speeds[ArrayIndex] = Entry.BytesReceived - Entry.LastBytesReceived;
                    int Count = (int)((Entry.SpeedIndex + 1) > 10 ? 10 : (Entry.SpeedIndex + 1));
                    long Sum = 0;
                    for (int i = 0; i < Count; i++)
                    {
                        Sum += Entry.Speeds[i];
                    }

                    Entry.Speed = Sum / Count;
                    Entry.TimeLeft = Entry.Speed < 1000 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds((Entry.Size - Entry.BytesReceived) / Entry.Speed);
                }
                Entry.LastBytesReceived = Entry.BytesReceived;
                Entry.SpeedIndex++;
            }
        }

        private void Notifier_NewDeviceArrived(ArrivalEventArgs Args)
        {
            EvaluateViewState();
        }

        internal static long GetFileLengthFromURL(string URL)
        {
            long Length = 0;

            WebRequest webReq = WebRequest.Create(URL);

            if (webReq is HttpWebRequest req)
            {
                req.Method = "HEAD";
                req.ServicePoint.ConnectionLimit = 10;
                using (WebResponse resp = req.GetResponse())
                {
                    long.TryParse(resp.Headers.Get("Content-Length"), out Length);
                }
                return Length;
            }
            else if (webReq is FileWebRequest filereq)
            {
                webReq.Method = "HEAD";
                using (WebResponse resp = webReq.GetResponse())
                {
                    long.TryParse(resp.Headers.Get("Content-Length"), out Length);
                }
                return Length;
            }

            return 0;
        }

        internal static string GetFileNameFromURL(string URL)
        {
            string FileName = Path.GetFileName(URL);
            int End = FileName.IndexOf('?');
            if (End >= 0)
            {
                FileName = FileName.Substring(0, End);
            }

            return FileName;
        }

        private void Search()
        {
            if (IsSearching)
            {
                return;
            }

            IsSearching = true;

            SynchronizationContext UIContext = SynchronizationContext.Current;
            SearchResultList.Clear();

            new Thread(() =>
            {
                string FFUURL = null;
                string[] EmergencyURLs = null;
                string SecureWIMURL = null;

                try
                {
                    string TempProductType = ProductType.ToUpper();
                    if ((TempProductType?.StartsWith("RM") == true) && !TempProductType.StartsWith("RM-"))
                    {
                        TempProductType = "RM-" + TempProductType[2..];
                    }

                    ProductType = TempProductType;

                    try
                    {
                        FFUURL = LumiaDownloadModel.SearchFFU(ProductType, ProductCode, OperatorCode, out TempProductType);
                    }
                    catch (WPinternalsException ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                        FFUURL = LumiaDownloadModel.SearchFFU(ProductType, null, OperatorCode, out TempProductType);
                    }

                    if (TempProductType != null)
                    {
                        ProductType = TempProductType;
                    }

                    if (ProductType != null)
                    {
                        EmergencyURLs = LumiaDownloadModel.SearchEmergencyFiles(ProductType);
                    }

                    if (ProductType != null && FirmwareVersion != null)
                    {
                        (SecureWIMURL, string _) = LumiaDownloadModel.SearchENOSW(ProductType, FirmwareVersion);
                    }
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }

                UIContext.Post(s =>
                {
                    if (FFUURL != null)
                    {
                        SearchResultList.Add(new SearchResult(FFUURL, ProductType, FFUDownloaded, null));
                    }

                    if (EmergencyURLs != null)
                    {
                        SearchResultList.Add(new SearchResult($"{ProductType} emergency-files", EmergencyURLs, ProductType, EmergencyDownloaded, ProductType));
                    }

                    if (SecureWIMURL != null)
                    {
                        SearchResultList.Add(new SearchResult($"{ProductType} ENOSW-files", SecureWIMURL, ProductType, ENOSWDownloaded, FirmwareVersion));
                    }
                }, null);

                IsSearching = false;
            }).Start();
        }

        internal void Download(string URL, string Category, Action<string[], object> Callback, object State = null)
        {
            string Folder = Category == null ? DownloadFolder : Path.Combine(DownloadFolder, Category);
            DownloadList.Add(new DownloadEntry(URL, Folder, null, Callback, State));
        }

        internal void Download(string[] URLs, string Category, Action<string[], object> Callback, object State = null)
        {
            string Folder = Category == null ? DownloadFolder : Path.Combine(DownloadFolder, Category);
            foreach (string URL in URLs)
            {
                DownloadList.Add(new DownloadEntry(URL, Folder, URLs, Callback, State));
            }
        }

        private void DownloadAll()
        {
            SynchronizationContext UIContext = SynchronizationContext.Current;

            new Thread(() =>
            {
                string FFUURL = null;
                string[] EmergencyURLs = null;
                string SecureWIMURL = null;
                try
                {
                    string TempProductType = ProductType.ToUpper();
                    if ((TempProductType?.StartsWith("RM") == true) && !TempProductType.StartsWith("RM-"))
                    {
                        TempProductType = "RM-" + TempProductType[2..];
                    }

                    ProductType = TempProductType;
                    FFUURL = LumiaDownloadModel.SearchFFU(ProductType, ProductCode, OperatorCode, out TempProductType);
                    if (TempProductType != null)
                    {
                        ProductType = TempProductType;
                    }

                    if (ProductType != null)
                    {
                        EmergencyURLs = LumiaDownloadModel.SearchEmergencyFiles(ProductType);
                    }

                    if (ProductType != null && FirmwareVersion != null)
                    {
                        (SecureWIMURL, string _) = LumiaDownloadModel.SearchENOSW(ProductType, FirmwareVersion);
                    }
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex, LogType.FileOnly);
                }

                UIContext.Post(s =>
                {
                    if (FFUURL != null)
                    {
                        Download(FFUURL, ProductType, FFUDownloadedAndCheckSupported, null);
                    }

                    if (EmergencyURLs != null)
                    {
                        Download(EmergencyURLs, ProductType, EmergencyDownloaded, ProductType);
                    }

                    if (SecureWIMURL != null)
                    {
                        Download(SecureWIMURL, ProductType, ENOSWDownloaded, FirmwareVersion);
                    }
                }, null);
            }).Start();
        }

        private void DownloadSelected()
        {
            foreach (SearchResult Result in SearchResultList.Where(r => r.IsSelected))
            {
                App.DownloadManager.Download(Result.URLs, Result.Category, Result.Callback, Result.State);
            }
        }

        private void FFUDownloaded(string[] Files, object State)
        {
            App.Config.AddFfuToRepository(Files[0]);
        }

        private void FFUDownloadedAndCheckSupported(string[] Files, object State)
        {
            App.Config.AddFfuToRepository(Files[0]);

            if (!App.Config.FFURepository.Any(e => App.PatchEngine.PatchDefinitions.First(p => p.Name == "SecureBootHack-V2-EFIESP").TargetVersions.Any(v => v.Description == e.OSVersion)))
            {
                const string ProductType2 = "RM-1085";
                string URL = LumiaDownloadModel.SearchFFU(ProductType2, null, null);
                Download(URL, ProductType2, FFUDownloaded, null);
            }
        }

        private void EmergencyDownloaded(string[] Files, object State)
        {
            string Type = (string)State;
            string ProgrammerPath = null;
            string PayloadPath = null;

            for (int i = 0; i < Files.Length; i++)
            {
                if (Files[i].EndsWith(".ede", StringComparison.OrdinalIgnoreCase))
                {
                    ProgrammerPath = Files[i];
                }

                if (Files[i].EndsWith(".edp", StringComparison.OrdinalIgnoreCase))
                {
                    PayloadPath = Files[i];
                }
            }

            if ((Type != null) && (ProgrammerPath != null) && (PayloadPath != null))
            {
                App.Config.AddEmergencyToRepository(Type, ProgrammerPath, PayloadPath);
            }
        }

        private void ENOSWDownloaded(string[] Files, object State)
        {
            App.Config.AddSecWimToRepository(Files[0], (string)State);
        }

        public ObservableCollection<DownloadEntry> DownloadList { get; } = new();
        public ObservableCollection<SearchResult> SearchResultList { get; } = new();

        private DelegateCommand _DownloadSelectedCommand = null;
        public DelegateCommand DownloadSelectedCommand
        {
            get
            {
                return _DownloadSelectedCommand ??= new DelegateCommand(() => DownloadSelected());
            }
        }

        private DelegateCommand _SearchCommand = null;
        public DelegateCommand SearchCommand
        {
            get
            {
                return _SearchCommand ??= new DelegateCommand(() => Search());
            }
        }

        private DelegateCommand _DownloadAllCommand = null;
        public DelegateCommand DownloadAllCommand
        {
            get
            {
                return _DownloadAllCommand ??= new DelegateCommand(() => DownloadAll());
            }
        }

        private string _DownloadFolder = null;
        public string DownloadFolder
        {
            get
            {
                return _DownloadFolder;
            }
            set
            {
                if (_DownloadFolder != value)
                {
                    _DownloadFolder = value;

                    try
                    {
                        Directory.CreateDirectory(_DownloadFolder);
                    }
                    catch (Exception ex)
                    {
                        LogFile.LogException(ex, LogType.FileOnly);
                    }
                    if (!Directory.Exists(_DownloadFolder))
                    {
                        _DownloadFolder = @"C:\ProgramData\WPinternals\Repository";
                        Directory.CreateDirectory(_DownloadFolder);
                    }

                    RegistryKey Key = Registry.CurrentUser.OpenSubKey(@"Software\WPInternals", true);

                    if (_DownloadFolder == null)
                    {
                        if (Key.GetValue("DownloadFolder") != null)
                        {
                            Key.DeleteValue("DownloadFolder");
                        }
                    }
                    else
                    {
                        Key.SetValue("DownloadFolder", _DownloadFolder);
                    }

                    Key.Close();

                    OnPropertyChanged(nameof(DownloadFolder));
                }
            }
        }

        private string _ProductCode = null;
        public string ProductCode
        {
            get
            {
                return _ProductCode;
            }
            set
            {
                if (_ProductCode != value)
                {
                    _ProductCode = value;

                    OnPropertyChanged(nameof(ProductCode));
                }
            }
        }

        private string _ProductType = null;
        public string ProductType
        {
            get
            {
                return _ProductType;
            }
            set
            {
                if (_ProductType != value)
                {
                    _ProductType = value;

                    OnPropertyChanged(nameof(ProductType));
                }
            }
        }

        private string _FirmwareVersion = null;
        public string FirmwareVersion
        {
            get
            {
                return _FirmwareVersion;
            }
            set
            {
                if (_FirmwareVersion != value)
                {
                    _FirmwareVersion = value;

                    OnPropertyChanged(nameof(FirmwareVersion));
                }
            }
        }

        private string _OperatorCode = null;
        public string OperatorCode
        {
            get
            {
                return _OperatorCode;
            }
            set
            {
                if (_OperatorCode != value)
                {
                    _OperatorCode = value;

                    OnPropertyChanged(nameof(OperatorCode));
                }
            }
        }

        internal override async void EvaluateViewState()
        {
            if (IsSwitchingInterface)
            {
                return;
            }

            if (!IsActive)
            {
                return;
            }

            if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Flash)
            {
                LumiaFlashAppPhoneInfo FlashAppInfo = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadPhoneInfo(ExtendedInfo: true);
                FirmwareVersion = FlashAppInfo.Firmware;

                IsSwitchingInterface = true;

                try
                {
                    bool ModernFlashApp = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadPhoneInfo().FlashAppProtocolVersionMajor >= 2;
                    if (ModernFlashApp)
                    {
                        ((LumiaFlashAppModel)Notifier.CurrentModel).SwitchToPhoneInfoAppContext();
                    }
                    else
                    {
                        ((LumiaFlashAppModel)Notifier.CurrentModel).SwitchToPhoneInfoAppContextLegacy();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                    {
                        throw new WPinternalsException("Unexpected Mode");
                    }

                    LumiaPhoneInfoAppModel LumiaPhoneInfoModel = (LumiaPhoneInfoAppModel)Notifier.CurrentModel;
                    LumiaPhoneInfoAppPhoneInfo Info = LumiaPhoneInfoModel.ReadPhoneInfo();
                    ProductType = Info.Type;
                    OperatorCode = "";
                    ProductCode = Info.ProductCode;

                    ModernFlashApp = Info.PhoneInfoAppVersionMajor >= 2;
                    if (ModernFlashApp)
                    {
                        LumiaPhoneInfoModel.SwitchToFlashAppContext();
                    }
                    else
                    {
                        LumiaPhoneInfoModel.ContinueBoot();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        await Notifier.WaitForArrival();
                    }

                    if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                    {
                        throw new WPinternalsException("Unexpected Mode");
                    }
                }
                catch (Exception ex)
                {
                    LogFile.LogException(ex);
                }

                IsSwitchingInterface = false;
            }
            else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_PhoneInfo)
            {
                LumiaPhoneInfoAppModel LumiaPhoneInfoModel = (LumiaPhoneInfoAppModel)Notifier.CurrentModel;
                LumiaPhoneInfoAppPhoneInfo Info = LumiaPhoneInfoModel.ReadPhoneInfo();
                ProductType = Info.Type;
                OperatorCode = "";
                ProductCode = Info.ProductCode;

                IsSwitchingInterface = true;

                bool ModernFlashApp = Info.PhoneInfoAppVersionMajor >= 2;
                if (ModernFlashApp)
                {
                    LumiaPhoneInfoModel.SwitchToFlashAppContext();
                }
                else
                {
                    LumiaPhoneInfoModel.ContinueBoot();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                {
                    await Notifier.WaitForArrival();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_Flash)
                {
                    throw new WPinternalsException("Unexpected Mode");
                }

                LumiaFlashAppPhoneInfo FlashAppInfo = ((LumiaFlashAppModel)Notifier.CurrentModel).ReadPhoneInfo(ExtendedInfo: true);
                FirmwareVersion = FlashAppInfo.Firmware;

                ModernFlashApp = FlashAppInfo.FlashAppProtocolVersionMajor >= 2;
                if (ModernFlashApp)
                {
                    ((LumiaFlashAppModel)Notifier.CurrentModel).SwitchToPhoneInfoAppContext();
                }
                else
                {
                    ((LumiaFlashAppModel)Notifier.CurrentModel).SwitchToPhoneInfoAppContextLegacy();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                {
                    await Notifier.WaitForArrival();
                }

                if (Notifier.CurrentInterface != PhoneInterfaces.Lumia_PhoneInfo)
                {
                    throw new WPinternalsException("Unexpected Mode");
                }

                IsSwitchingInterface = false;
            }
            else if (Notifier.CurrentInterface == PhoneInterfaces.Lumia_Normal)
            {
                NokiaPhoneModel LumiaNormalModel = (NokiaPhoneModel)Notifier.CurrentModel;
                OperatorCode = LumiaNormalModel.ExecuteJsonMethodAsString("ReadOperatorName", "OperatorName"); // Example: 000-NL
                string TempProductType = LumiaNormalModel.ExecuteJsonMethodAsString("ReadManufacturerModelName", "ManufacturerModelName"); // RM-821_eu_denmark_251
                if (TempProductType.Contains('_'))
                {
                    TempProductType = TempProductType.Substring(0, TempProductType.IndexOf('_'));
                }

                ProductType = TempProductType;
                ProductCode = LumiaNormalModel.ExecuteJsonMethodAsString("ReadProductCode", "ProductCode"); // 059Q9D7

                FirmwareVersion = LumiaNormalModel.ExecuteJsonMethodAsString("ReadSwVersion", "SwVersion");
            }
        }
        public DelegateCommand AddFFUCommand { get; } = null;
    }

    internal enum DownloadStatus
    {
        Downloading,
        Ready,
        Failed
    };

    internal class DownloadEntry : INotifyPropertyChanged, IProgress<GeneralDownloadProgress>
    {
        private readonly SynchronizationContext UIContext;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        internal Action<string[], object> Callback;
        internal object State;
        internal string URL;
        internal string[] URLCollection;
        internal string Folder;
        //internal HttpClient Client;
        internal HttpDownloader Client;
        internal long SpeedIndex = -1;
        internal long[] Speeds = new long[10];
        internal long LastBytesReceived;
        internal long BytesReceived;

        internal DownloadEntry(string URL, string Folder, string[] URLCollection, Action<string[], object> Callback, object State)
        {
            UIContext = SynchronizationContext.Current;
            this.URL = URL;
            this.Callback = Callback;
            this.State = State;
            this.URLCollection = URLCollection;
            this.Folder = Folder;
            Directory.CreateDirectory(Folder);
            Name = DownloadsViewModel.GetFileNameFromURL(URL);
            Uri Uri = new(URL);
            Status = DownloadStatus.Downloading;
            new Thread(() =>
            {
                Size = DownloadsViewModel.GetFileLengthFromURL(URL);

                //Client = new HttpClient();

                //_ = Client.DownloadFileAsync(Uri, Path.Combine(Folder, DownloadsViewModel.GetFileNameFromURL(Uri.LocalPath)), Client_DownloadProgressChanged, Client_DownloadFileCompleted);

                Client = new(Folder, 4, false);

                _ = Client.DownloadAsync([new FileDownloadInformation(URL, DownloadsViewModel.GetFileNameFromURL(Uri.LocalPath), Size, null, null)], this);
            }).Start();
        }

        public void Report(GeneralDownloadProgress e)
        {
            foreach (FileDownloadStatus status in e.DownloadedStatus)
            {
                if (status == null)
                {
                    continue;
                }

                if (status.FileStatus == FileStatus.Failed || status.FileStatus == FileStatus.Failed)
                {
                    Client_DownloadFileCompleted(true);
                }

                if (status.FileStatus == FileStatus.Completed)
                {
                    Client_DownloadFileCompleted(false);
                }

                if (status.FileStatus == FileStatus.Downloading)
                {
                    Client_DownloadProgressChanged(new HttpClientDownloadProgress(status.DownloadedBytes, status.File.FileSize));
                }
            }
        }

        private void Client_DownloadFileCompleted(bool Error)
        {
            void Finish()
            {
                Status = Error ? DownloadStatus.Failed : DownloadStatus.Ready;
                App.DownloadManager.DownloadList.Remove(this);
                if (Status == DownloadStatus.Ready)
                {
                    if (URLCollection?.Any(c => App.DownloadManager.DownloadList.Any(d => d.URL == c)) != true) // if there are no files left to download from this collection, then call the callback-function.
                    {
                        Client.Dispose();
                        Client = null;

                        string[] Files;
                        if (URLCollection == null)
                        {
                            Files = new string[1];
                            Files[0] = Path.Combine(Folder, DownloadsViewModel.GetFileNameFromURL(URL));
                        }
                        else
                        {
                            Files = new string[URLCollection.Length];
                            for (int i = 0; i < URLCollection.Length; i++)
                            {
                                Files[i] = Path.Combine(Folder, DownloadsViewModel.GetFileNameFromURL(URLCollection[i]));
                            }
                        }

                        Callback(Files, State);
                    }
                }
            }

            if (UIContext == null)
            {
                Finish();
            }
            else
            {
                UIContext?.Post(d => Finish(), null);
            }
        }

        private void Client_DownloadProgressChanged(HttpClientDownloadProgress e)
        {
            BytesReceived = e.BytesReceived;
            Progress = e.ProgressPercentage;
        }

        protected void OnPropertyChanged(string propertyName)
        {
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

        private DownloadStatus _Status;
        public DownloadStatus Status
        {
            get
            {
                return _Status;
            }
            set
            {
                if (_Status != value)
                {
                    _Status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        private string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (_Name != value)
                {
                    _Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private long _Size;
        public long Size
        {
            get
            {
                return _Size;
            }
            set
            {
                if (_Size != value)
                {
                    _Size = value;
                    OnPropertyChanged(nameof(Size));
                }
            }
        }

        private TimeSpan _TimeLeft;
        public TimeSpan TimeLeft
        {
            get
            {
                return _TimeLeft;
            }
            set
            {
                if (_TimeLeft != value)
                {
                    _TimeLeft = value;
                    OnPropertyChanged(nameof(TimeLeft));
                }
            }
        }

        private double _Speed;
        public double Speed
        {
            get
            {
                return _Speed;
            }
            set
            {
                if (_Speed != value)
                {
                    _Speed = value;
                    OnPropertyChanged(nameof(Speed));
                }
            }
        }

        private int _Progress;
        public int Progress
        {
            get
            {
                return _Progress;
            }
            set
            {
                if (_Progress != value)
                {
                    _Progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }
    }

    internal class SearchResult : INotifyPropertyChanged
    {
        private readonly SynchronizationContext UIContext;
        public event PropertyChangedEventHandler PropertyChanged;
        internal string[] URLs;
        internal Action<string[], object> Callback;
        internal object State;
        internal string Category;

        internal SearchResult(string URL, string Category, Action<string[], object> Callback, object State)
        {
            UIContext = SynchronizationContext.Current;
            URLs = new string[1];
            URLs[0] = URL;
            Name = DownloadsViewModel.GetFileNameFromURL(URL);
            this.Callback = Callback;
            this.State = State;
            this.Category = Category;
            GetSize();
        }

        internal SearchResult(string Name, string[] URLs, string Category, Action<string[], object> Callback, object State)
        {
            UIContext = SynchronizationContext.Current;
            this.URLs = URLs;
            this.Name = Name;
            this.Callback = Callback;
            this.State = State;
            this.Category = Category;
            GetSize();
        }

        internal SearchResult(string Name, string URL, string Category, Action<string[], object> Callback, object State)
        {
            UIContext = SynchronizationContext.Current;
            URLs = new string[1];
            URLs[0] = URL;
            this.Name = Name;
            this.Callback = Callback;
            this.State = State;
            this.Category = Category;
            GetSize();
        }

        private void GetSize()
        {
            new Thread(() =>
            {
                long CalcSize = 0;
                foreach (string URL in URLs)
                {
                    CalcSize += DownloadsViewModel.GetFileLengthFromURL(URL);
                }
                Size = CalcSize;
            }).Start();
        }

        protected void OnPropertyChanged(string propertyName)
        {
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

        private string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (_Name != value)
                {
                    _Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private long _Size;
        public long Size
        {
            get
            {
                return _Size;
            }
            set
            {
                if (_Size != value)
                {
                    _Size = value;
                    OnPropertyChanged(nameof(Size));
                }
            }
        }

        private bool _IsSelected;
        public bool IsSelected
        {
            get
            {
                return _IsSelected;
            }
            set
            {
                if (_IsSelected != value)
                {
                    _IsSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
    }

    public class HttpClientDownloadProgress
    {
        //
        // Summary:
        //     Gets the asynchronous task progress percentage.
        //
        // Returns:
        //     A percentage value indicating the asynchronous task progress.
        public int ProgressPercentage { get; }

        //
        // Summary:
        //     Gets the number of bytes received.
        //
        // Returns:
        //     An System.Int64 value that indicates the number of bytes received.
        public long BytesReceived { get; }

        //
        // Summary:
        //     Gets the total number of bytes in a System.Net.WebClient data download operation.
        //
        // Returns:
        //     An System.Int64 value that indicates the number of bytes that will be received.
        public long TotalBytesToReceive { get; }

        internal HttpClientDownloadProgress(long BytesReceived, long TotalBytesToReceive)
        {
            this.TotalBytesToReceive = TotalBytesToReceive;
            this.BytesReceived = BytesReceived;
            ProgressPercentage = (int)Math.Round((float)BytesReceived / TotalBytesToReceive * 100f);
        }
    }

    public static class HttpClientProgressExtensions
    {
        public static async Task DownloadFileAsync(this HttpClient client, Uri address, string fileName, Action<HttpClientDownloadProgress> progress = null, Action<bool> completed = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                using FileStream destination = File.Create(fileName);
                using HttpResponseMessage response = await client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead);
                long? contentLength = response.Content.Headers.ContentLength;
                using Stream download = await response.Content.ReadAsStreamAsync();

                if (progress is null || !contentLength.HasValue)
                {
                    await download.CopyToAsync(destination);
                    if (completed != null)
                        completed(true);
                    return;
                }

                Progress<long> progressWrapper = new Progress<long>(totalBytes => progress(new HttpClientDownloadProgress(totalBytes, contentLength.Value)));
                await download.CopyToAsync(destination, 81920, progressWrapper, cancellationToken);

                if (completed != null)
                    completed(true);
            }
            catch
            {
                if (completed != null)
                    completed(false);
            }
        }

        private static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, IProgress<long> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (!source.CanRead)
                throw new InvalidOperationException($"'{nameof(source)}' is not readable.");
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite)
                throw new InvalidOperationException($"'{nameof(destination)}' is not writable.");

            byte[] buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }
        }
    }

    public class DownloaderNameConvertor : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            return Path.GetFileNameWithoutExtension((string)value);
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class DownloaderSizeConvertor : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            long? Size = value as long?;
            if (Size < 1024)
            {
                return Size + " B";
            }

            if (Size < (1024 * 1024))
            {
                return Math.Round((double)Size / 1024, 0) + " KB";
            }

            return Math.Round((double)Size / 1024 / 1024, 0) + " MB";
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class DownloaderSpeedConvertor : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            return ((int)((double)value / 1024)).ToString() + " KB/s";
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class DownloaderTimeRemainingConvertor : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            TimeSpan TimeLeft = (TimeSpan)value;
            if (TimeLeft == Timeout.InfiniteTimeSpan)
            {
                return "";
            }

            return TimeLeft.ToString(@"h\:mm\:ss");
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
