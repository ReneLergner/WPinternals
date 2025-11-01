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
using System.IO;
using WPinternals.HelperClasses;
using WPinternals.Models.Lumia.NCSd;
using WPinternals.Models.UEFIApps.Flash;
using WPinternals.Models.UEFIApps.PhoneInfo;

namespace WPinternals
{
    internal class DownloadsViewModel : ContextViewModel
    {
        private readonly PhoneNotifierViewModel Notifier;

        internal DownloadsViewModel(PhoneNotifierViewModel Notifier)
        {
            IsSwitchingInterface = false;
            IsFlashModeOperation = false;
            this.Notifier = Notifier;
            Notifier.NewDeviceArrived += Notifier_NewDeviceArrived;

            AddFFUCommand = new DelegateCommand(() =>
            {
                string FFUPath = null;

                OpenFileDialog dlg = new()
                {
                    DefaultExt = ".ffu", // Default file extension
                    Filter = "ROM images (.ffu)|*.ffu" // Filter files by extension 
                };

                bool? result = dlg.ShowDialog();

                if (result == true)
                {
                    FFUPath = dlg.FileName;
                    string FFUFile = Path.GetFileName(FFUPath);

                    try
                    {
                        App.Config.AddFfuToRepository(FFUPath);
                        App.Config.WriteConfig();
                        LastFFUStatusText = $"File \"{FFUFile}\" was added to the repository.";
                    }
                    catch (WPinternalsException Ex)
                    {
                        LastFFUStatusText = $"Error: {Ex.Message}. File \"{FFUFile}\" was not added.";
                    }
                    catch (Exception ex)
                    {
                        LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                        LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                        LogFile.Log(ex.Message, LogType.FileAndConsole);
                        LogFile.Log(ex.StackTrace, LogType.FileAndConsole);

                        LastFFUStatusText = $"Error: File \"{FFUFile}\" was not added.";
                    }
                }
                else
                {
                    LastFFUStatusText = null;
                }
            });

            AddSecWIMCommand = new DelegateCommand(() =>
            {
                string SecWIMPath = null;

                OpenFileDialog dlg = new()
                {
                    DefaultExt = ".secwim", // Default file extension
                    Filter = "Secure WIM images (.secwim)|*.secwim" // Filter files by extension 
                };

                bool? result = dlg.ShowDialog();

                if (result == true)
                {
                    SecWIMPath = dlg.FileName;
                    string SecWIMFile = Path.GetFileName(SecWIMPath);

                    try
                    {
                        App.Config.AddSecWimToRepository(SecWIMPath, FirmwareVersion);
                        App.Config.WriteConfig();
                        LastSecWIMStatusText = $"File \"{SecWIMFile}\" was added to the repository.";
                    }
                    catch (WPinternalsException Ex)
                    {
                        LastSecWIMStatusText = $"Error: {Ex.Message}. File \"{SecWIMFile}\" was not added.";
                    }
                    catch (Exception ex)
                    {
                        LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                        LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                        LogFile.Log(ex.Message, LogType.FileAndConsole);
                        LogFile.Log(ex.StackTrace, LogType.FileAndConsole);

                        LastSecWIMStatusText = $"Error: File \"{SecWIMFile}\" was not added.";
                    }
                }
                else
                {
                    LastSecWIMStatusText = null;
                }
            });
        }

        private string _LastFFUStatusText = null;
        public string LastFFUStatusText
        {
            get
            {
                return _LastFFUStatusText;
            }
            set
            {
                _LastFFUStatusText = value;
                OnPropertyChanged(nameof(LastFFUStatusText));
            }
        }

        private string _LastSecWIMStatusText = null;
        public string LastSecWIMStatusText
        {
            get
            {
                return _LastSecWIMStatusText;
            }
            set
            {
                _LastSecWIMStatusText = value;
                OnPropertyChanged(nameof(LastSecWIMStatusText));
            }
        }

        private void Notifier_NewDeviceArrived(ArrivalEventArgs Args)
        {
            EvaluateViewState();
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
                NokiaCareSuiteModel LumiaNormalModel = (NokiaCareSuiteModel)Notifier.CurrentModel;
                FirmwareVersion = LumiaNormalModel.ExecuteJsonMethodAsString("ReadSwVersion", "SwVersion");
            }
        }
        public DelegateCommand AddFFUCommand { get; } = null;
        public DelegateCommand AddSecWIMCommand { get; } = null;
    }
}