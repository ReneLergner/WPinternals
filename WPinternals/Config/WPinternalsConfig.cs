﻿// Copyright (c) 2018, Rene Lergner - @Heathcliff74xda
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using WPinternals.HelperClasses;

namespace WPinternals.Config
{

    public class WPinternalsConfig
    {
        internal static WPinternalsConfig ReadConfig()
        {
            string FilePath = Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\WPInternals.config");
            if (File.Exists(FilePath))
            {
                string XmlString = File.ReadAllText(FilePath);
                XmlSerializer x = new(typeof(WPinternalsConfig), "");
                MemoryStream s = new(System.Text.Encoding.UTF8.GetBytes(XmlString));
                return (WPinternalsConfig)x.Deserialize(s);
            }
            else
            {
                return new WPinternalsConfig();
            }
        }

        internal void WriteConfig()
        {
            string DirPath = Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals");
            if (!Directory.Exists(DirPath))
            {
                Directory.CreateDirectory(DirPath);
            }

            string FilePath = Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\WPInternals.config");

            XmlSerializer x = new(typeof(WPinternalsConfig), "");

            XmlSerializerNamespaces ns = new();
            ns.Add("", "");
            StreamWriter FileWriter = new(FilePath);
            x.Serialize(FileWriter, this, ns);
            FileWriter.Close();
        }

        internal void SetProfile(string Type, string PlatformID, string ProductCode, string PhoneFirmware, string FfuFirmware, uint FillSize, uint HeaderSize, bool AssumeImageHeaderFallsInGap, bool AllocateAsyncBuffersOnPhone)
        {
            FlashProfile Profile = GetProfile(PlatformID, PhoneFirmware, FfuFirmware);
            if (Profile == null)
            {
                Profile = new FlashProfile
                {
                    Type = Type,
                    PlatformID = PlatformID,
                    ProductCode = ProductCode,
                    PhoneFirmware = PhoneFirmware,
                    FfuFirmware = FfuFirmware
                };
                FlashProfiles.Add(Profile);
            }

            Profile.FillSize = FillSize;
            Profile.HeaderSize = HeaderSize;
            Profile.AssumeImageHeaderFallsInGap = AssumeImageHeaderFallsInGap;
            Profile.AllocateAsyncBuffersOnPhone = AllocateAsyncBuffersOnPhone;

            WriteConfig();
        }

        internal FlashProfile GetProfile(string PlatformID, string PhoneFirmware, string FfuFirmware = null)
        {
            return FlashProfiles.Find(p => string.Equals(p.PlatformID, PlatformID, StringComparison.CurrentCultureIgnoreCase) && string.Equals(p.PhoneFirmware, PhoneFirmware, StringComparison.CurrentCultureIgnoreCase) && (FfuFirmware == null || string.Equals(p.FfuFirmware, FfuFirmware, StringComparison.CurrentCultureIgnoreCase)));
        }

        public List<FlashProfile> FlashProfiles = [];

        internal void AddFfuToRepository(string FFUPath)
        {
            try
            {
                FFU NewFFU = new(FFUPath);
                AddFfuToRepository(FFUPath, NewFFU.PlatformID, NewFFU.GetFirmwareVersion(), NewFFU.GetOSVersion());
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex, LogType.FileAndConsole);
            }
        }

        internal void AddFfuToRepository(string FFUPath, string PlatformID, string FirmwareVersion, string OSVersion)
        {
            FFUEntry Entry = FFURepository.Find(e => e.PlatformID == PlatformID && e.FirmwareVersion == FirmwareVersion && string.Equals(e.Path, FFUPath, StringComparison.CurrentCultureIgnoreCase));
            if (Entry == null)
            {
                LogFile.Log("Adding FFU to repository: " + FFUPath, LogType.FileAndConsole);
                LogFile.Log("Platform ID: " + PlatformID, LogType.FileAndConsole);
                if (FirmwareVersion != null)
                {
                    LogFile.Log("Firmware version: " + FirmwareVersion, LogType.FileAndConsole);
                }

                if (OSVersion != null)
                {
                    LogFile.Log("OS version: " + OSVersion, LogType.FileAndConsole);
                }

                Entry = new FFUEntry
                {
                    Path = FFUPath,
                    PlatformID = PlatformID,
                    FirmwareVersion = FirmwareVersion,
                    OSVersion = OSVersion
                };
                FFURepository.Add(Entry);
                WriteConfig();
            }
            else
            {
                LogFile.Log("FFU not added, because it was already present in the repository.", LogType.FileAndConsole);
            }
        }

        internal void RemoveFfuFromRepository(string FFUPath)
        {
            int Count = 0;
            FFURepository.Where(e => string.Equals(e.Path, FFUPath, StringComparison.CurrentCultureIgnoreCase)).ToList().ForEach(e =>
                {
                    Count++;
                    FFURepository.Remove(e);
                });
            if (Count == 0)
            {
                LogFile.Log("FFU was not removed from repository because it was not present.", LogType.FileAndConsole);
            }
            else
            {
                LogFile.Log("Removed FFU from repository: " + FFUPath, LogType.FileAndConsole);
                WriteConfig();
            }
        }

        public List<FFUEntry> FFURepository = [];

        internal void AddSecWimToRepository(string SecWimPath, string FirmwareVersion)
        {
            SecWimEntry Entry = SecWimRepository.Find(e => e.FirmwareVersion == FirmwareVersion && string.Equals(e.Path, SecWimPath, StringComparison.CurrentCultureIgnoreCase));
            if (Entry == null)
            {
                LogFile.Log("Adding Secure WIM to repository: " + SecWimPath, LogType.FileAndConsole);
                if (FirmwareVersion != null)
                {
                    LogFile.Log("Firmware version: " + FirmwareVersion, LogType.FileAndConsole);
                }

                Entry = new SecWimEntry
                {
                    Path = SecWimPath,
                    FirmwareVersion = FirmwareVersion
                };
                SecWimRepository.Add(Entry);
                WriteConfig();
            }
            else
            {
                LogFile.Log("Secure WIM not added, because it was already present in the repository.", LogType.FileAndConsole);
            }
        }

        internal void RemoveSecWimFromRepository(string SecWimPath)
        {
            int Count = 0;
            SecWimRepository.Where(e => string.Equals(e.Path, SecWimPath, StringComparison.CurrentCultureIgnoreCase)).ToList().ForEach(e =>
            {
                Count++;
                SecWimRepository.Remove(e);
            });
            if (Count == 0)
            {
                LogFile.Log("Secure WIM was not removed from repository because it was not present.", LogType.FileAndConsole);
            }
            else
            {
                LogFile.Log("Removed Secure WIM from repository: " + SecWimPath, LogType.FileAndConsole);
                WriteConfig();
            }
        }

        public List<SecWimEntry> SecWimRepository = [];

        public List<EmergencyFileEntry> EmergencyRepository = [];

        internal void AddEmergencyToRepository(string Type, string ProgrammerPath, string PayloadPath)
        {
            EmergencyFileEntry Entry = EmergencyRepository.Find(e => e.Type == Type && string.Equals(e.ProgrammerPath, ProgrammerPath, StringComparison.CurrentCultureIgnoreCase));
            if (Entry != null && PayloadPath != null && !string.Equals(Entry.PayloadPath, PayloadPath, StringComparison.CurrentCultureIgnoreCase))
            {
                LogFile.Log("Updating emergency payload path in repository: " + PayloadPath, LogType.FileAndConsole);
                Entry.PayloadPath = PayloadPath;
                WriteConfig();
            }
            else if (Entry == null)
            {
                LogFile.Log("Adding emergency files to repository: " + ProgrammerPath, LogType.FileAndConsole);
                LogFile.Log("Type: " + Type, LogType.FileAndConsole);

                Entry = new EmergencyFileEntry
                {
                    Type = Type,
                    ProgrammerPath = ProgrammerPath,
                    PayloadPath = PayloadPath
                };

                QualcommPartition Programmer = new(ProgrammerPath);
                Entry.RKH = Programmer.RootKeyHash;

                EmergencyRepository.Add(Entry);
                WriteConfig();
            }
            else
            {
                LogFile.Log("Emergency files not added, because they were already present in the repository.", LogType.FileAndConsole);
            }
        }

        internal void RemoveEmergencyFromRepository(string ProgrammerPath)
        {
            int Count = 0;
            EmergencyRepository.Where(e => string.Equals(e.ProgrammerPath, ProgrammerPath, StringComparison.CurrentCultureIgnoreCase)).ToList().ForEach(e =>
            {
                Count++;
                EmergencyRepository.Remove(e);
            });
            if (Count == 0)
            {
                LogFile.Log("Emergency file was not removed from repository because it was not present.", LogType.FileAndConsole);
            }
            else
            {
                LogFile.Log("Removed Emergency file from repository: " + ProgrammerPath, LogType.FileAndConsole);
                WriteConfig();
            }
        }

        public string RegistrationName;
        public string RegistrationEmail;
        public string RegistrationSkypeID;
        public string RegistrationTelegramID;
        public string RegistrationKey;
    }
}
