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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Windows;

namespace WPinternals
{
    internal static class Registration
    {
#if PREVIEW
        internal const bool IsPrerelease = true;
#else
        internal const bool IsPrerelease = false;
#endif

        internal static readonly DateTime ExpirationDate = new DateTime(2019, 01, 06);

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
                Result = (CalcRegKey() == App.Config.RegistrationKey);
            }
            return Result;
        }

        internal static string CalcRegKey()
        {
            string KeyBase = App.Config.RegistrationName;
            if (Environment.MachineName == null)
                KeyBase += "-Unknown";
            else
                KeyBase += "-" + Environment.MachineName;
            byte[] KeyBytes = System.Text.Encoding.UTF8.GetBytes(KeyBase);
            SHA1Managed sha = new SHA1Managed();
            byte[] Key = sha.ComputeHash(KeyBytes);
            return System.Convert.ToBase64String(Key);
        }
    }

    public class WPinternalsConfig
    {
        internal static WPinternalsConfig ReadConfig()
        {
            WPinternalsConfig Result;

            string FilePath = Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\WPInternals.config");
            if (File.Exists(FilePath))
            {
                string XmlString = File.ReadAllText(FilePath);
                XmlSerializer x = new XmlSerializer(typeof(WPinternalsConfig), "");
                MemoryStream s = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(XmlString));
                Result = (WPinternalsConfig)x.Deserialize(s);
                return Result;
            }
            else
                return new WPinternalsConfig();
        }

        internal void WriteConfig()
        {
            string DirPath = Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals");
            if (!Directory.Exists(DirPath))
                Directory.CreateDirectory(DirPath);
            string FilePath = Environment.ExpandEnvironmentVariables("%ALLUSERSPROFILE%\\WPInternals\\WPInternals.config");

            XmlSerializer x = new XmlSerializer(typeof(WPinternalsConfig), "");

            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            System.IO.StreamWriter FileWriter = new System.IO.StreamWriter(FilePath);
            x.Serialize(FileWriter, this, ns);
            FileWriter.Close();
        }

        internal void SetProfile(string Type, string PlatformID, string ProductCode, string PhoneFirmware, string FfuFirmware, UInt32 FillSize, UInt32 HeaderSize, bool AssumeImageHeaderFallsInGap, bool AllocateAsyncBuffersOnPhone)
        {
            FlashProfile Profile = GetProfile(PlatformID, PhoneFirmware, FfuFirmware);
            if (Profile == null)
            {
                Profile = new FlashProfile();
                Profile.Type = Type;
                Profile.PlatformID = PlatformID;
                Profile.ProductCode = ProductCode;
                Profile.PhoneFirmware = PhoneFirmware;
                Profile.FfuFirmware = FfuFirmware;
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
            return FlashProfiles.Where(p => ((string.Compare(p.PlatformID, PlatformID, true) == 0) && (string.Compare(p.PhoneFirmware, PhoneFirmware, true) == 0) && ((FfuFirmware == null) || (string.Compare(p.FfuFirmware, FfuFirmware, true) == 0)))).FirstOrDefault();
        }

        public List<FlashProfile> FlashProfiles = new List<FlashProfile>();

        internal void AddFfuToRepository(string FFUPath)
        {
            try
            {
                FFU NewFFU = new FFU(FFUPath);
                AddFfuToRepository(FFUPath, NewFFU.PlatformID, NewFFU.GetFirmwareVersion(), NewFFU.GetOSVersion());
            }
            catch (Exception Ex)
            {
                LogFile.LogException(Ex, LogType.FileAndConsole);
            }
        }

        internal void AddFfuToRepository(string FFUPath, string PlatformID, string FirmwareVersion, string OSVersion)
        {
            FFUEntry Entry = FFURepository.Where(e => ((e.PlatformID == PlatformID) && (e.FirmwareVersion == FirmwareVersion) && (string.Compare(e.Path, FFUPath, true) == 0))).FirstOrDefault();
            if (Entry == null)
            {
                LogFile.Log("Adding FFU to repository: " + FFUPath, LogType.FileAndConsole);
                LogFile.Log("Platform ID: " + PlatformID, LogType.FileAndConsole);
                if (FirmwareVersion != null)
                    LogFile.Log("Firmware version: " + FirmwareVersion, LogType.FileAndConsole);
                if (OSVersion != null)
                    LogFile.Log("OS version: " + OSVersion, LogType.FileAndConsole);

                Entry = new FFUEntry();
                Entry.Path = FFUPath;
                Entry.PlatformID = PlatformID;
                Entry.FirmwareVersion = FirmwareVersion;
                Entry.OSVersion = OSVersion;
                FFURepository.Add(Entry);
                WriteConfig();
            }
            else
                LogFile.Log("FFU not added, because it was already present in the repository.", LogType.FileAndConsole);
        }

        internal void RemoveFfuFromRepository(string FFUPath)
        {
            int Count = 0;
            FFURepository.Where(e => (string.Compare(e.Path, FFUPath, true) == 0)).ToList().ForEach(e => 
                {
                    Count++;
                    FFURepository.Remove(e);
                });
            if (Count == 0)
                LogFile.Log("FFU was not removed from repository because it was not present.", LogType.FileAndConsole);
            else
            {
                LogFile.Log("Removed FFU from repository: " + FFUPath, LogType.FileAndConsole);
                WriteConfig();
            }
        }

        public List<FFUEntry> FFURepository = new List<FFUEntry>();

        public List<EmergencyFileEntry> EmergencyRepository = new List<EmergencyFileEntry>();

        internal void AddEmergencyToRepository(string Type, string ProgrammerPath, string PayloadPath)
        {
            EmergencyFileEntry Entry = EmergencyRepository.Where(e => ((e.Type == Type) && (string.Compare(e.ProgrammerPath, ProgrammerPath, true) == 0))).FirstOrDefault();
            if ((Entry != null) && (PayloadPath != null) && (string.Compare(Entry.PayloadPath, PayloadPath, true) != 0))
            {
                LogFile.Log("Updating emergency payload path in repository: " + PayloadPath, LogType.FileAndConsole);
                Entry.PayloadPath = PayloadPath;
                WriteConfig();
            }
            else if (Entry == null)
            {
                LogFile.Log("Adding emergency files to repository: " + ProgrammerPath, LogType.FileAndConsole);
                LogFile.Log("Type: " + Type, LogType.FileAndConsole);
                
                Entry = new EmergencyFileEntry();
                Entry.Type = Type;
                Entry.ProgrammerPath = ProgrammerPath;
                Entry.PayloadPath = PayloadPath;

                QualcommPartition Programmer = new QualcommPartition(ProgrammerPath);
                Entry.RKH = Programmer.RootKeyHash;

                EmergencyRepository.Add(Entry);
                WriteConfig();
            }
            else
                LogFile.Log("Emergency files not added, because they were already present in the repository.", LogType.FileAndConsole);
        }

        internal void RemoveEmergencyFromRepository(string ProgrammerPath)
        {
            int Count = 0;
            EmergencyRepository.Where(e => (string.Compare(e.ProgrammerPath, ProgrammerPath, true) == 0)).ToList().ForEach(e =>
            {
                Count++;
                EmergencyRepository.Remove(e);
            });
            if (Count == 0)
                LogFile.Log("Emergency file was not removed from repository because it was not present.", LogType.FileAndConsole);
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

    public class FlashProfile
    {
        public string Type;
        public string PlatformID;
        public string ProductCode;
        public string PhoneFirmware;
        public string FfuFirmware;

        [XmlIgnore]
        internal UInt32 FillSize;
        [XmlIgnore]
        internal UInt32 HeaderSize;
        [XmlIgnore]
        internal bool AssumeImageHeaderFallsInGap;
        [XmlIgnore]
        internal bool AllocateAsyncBuffersOnPhone;

        [XmlElement(ElementName = "Profile")]
        public string ProfileAsString
        {
            get
            {
                byte[] ValueBuffer = new byte[10];
                ValueBuffer[0] = 1; // Profile version
                ByteOperations.WriteUInt32(ValueBuffer, 1, FillSize);
                ByteOperations.WriteUInt32(ValueBuffer, 5, HeaderSize);
                if (AssumeImageHeaderFallsInGap) ValueBuffer[9] |= 1;
                if (AllocateAsyncBuffersOnPhone) ValueBuffer[9] |= 2;
                return System.Convert.ToBase64String(ValueBuffer);
            }
            set
            {
                byte[] ValueBuffer = System.Convert.FromBase64String(value);
                byte Version = ValueBuffer[0];
                FillSize = ByteOperations.ReadUInt32(ValueBuffer, 1);
                HeaderSize = ByteOperations.ReadUInt32(ValueBuffer, 5);
                AssumeImageHeaderFallsInGap = (bool)((ValueBuffer[9] & 1) != 0);
                AllocateAsyncBuffersOnPhone = (bool)((ValueBuffer[9] & 2) != 0);
            }
        }
    }

    public class FFUEntry
    {
        public string PlatformID;
        public string FirmwareVersion;
        public string OSVersion;
        public string Path;

        internal bool Exists()
        {
            return File.Exists(Path);
        }
    }

    public class EmergencyFileEntry
    {
        public string Type;

        [XmlIgnore]
        public byte[] RKH;
        [XmlElement(ElementName = "RKH")]
        public string RKHAsString
        {
            get
            {
                return RKH == null ? null : Converter.ConvertHexToString(RKH, "");
            }
            set
            {
                RKH = value == null ? null : Converter.ConvertStringToHex(value);
            }
        }

        public string ProgrammerPath;
        public string PayloadPath;

        internal bool ProgrammerExists()
        {
            return File.Exists(ProgrammerPath);
        }

        internal bool PayloadExists()
        {
            return File.Exists(PayloadPath);
        }
    }
}
