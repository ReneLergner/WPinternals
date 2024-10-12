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
using System.Collections.Generic;
using System.Net;
using System.Xml;
using WPinternals.HelperClasses;

namespace WPinternals.Models.Lumia.MSR
{
    internal static class LumiaDownloadModel
    {
        internal static string[] SearchEmergencyFiles(string ProductType)
        {
            ProductType = ProductType.ToUpper();
            if (ProductType.StartsWith("RM") && !ProductType.StartsWith("RM-"))
            {
                ProductType = "RM-" + ProductType[2..];
            }

            LogFile.Log("Getting Emergency files for: " + ProductType, LogType.FileAndConsole);

            if (ProductType == "RM-1072" || ProductType == "RM-1073")
            {
                LogFile.Log("Due to mix-up in online-repository, redirecting to emergency files of RM-1113", LogType.FileAndConsole);
                ProductType = "RM-1113";
            }

            List<string> Result = [];

            WebClient Client = new();
            string Src;
            string FileName;
            string Config = null;
            try
            {
                Config = Client.DownloadString($"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/{ProductType}/emergency_flash_config.xml");
            }
            catch (Exception ex)
            {
                LogFile.Log("An unexpected error happened", LogType.FileAndConsole);
                LogFile.Log(ex.GetType().ToString(), LogType.FileAndConsole);
                LogFile.Log(ex.Message, LogType.FileAndConsole);
                LogFile.Log(ex.StackTrace, LogType.FileAndConsole);

                LogFile.Log("Emergency files for " + ProductType + " not found", LogType.FileAndConsole);
                return null;
            }
            Client.Dispose();

            XmlDocument Doc = new();
            Doc.LoadXml(Config);

            // Hex
            XmlNode Node = Doc.SelectSingleNode("//emergency_flash_config/hex_flasher");
            if (Node != null)
            {
                FileName = Node.Attributes["image_path"].InnerText;
                Src = $"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/{ProductType}/{FileName}";
                LogFile.Log("Hex-file: " + Src);
                Result.Add(Src);
            }

            // Mbn
            Node = Doc.SelectSingleNode("//emergency_flash_config/mbn_image");
            if (Node != null)
            {
                FileName = Node.Attributes["image_path"].InnerText;
                Src = $"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/{ProductType}/{FileName}";
                LogFile.Log("Mbn-file: " + Src);
                Result.Add(Src);
            }

            // Ede
            foreach (XmlNode SubNode in Doc.SelectNodes("//emergency_flash_config/first_boot_images/first_boot_image"))
            {
                FileName = SubNode.Attributes["image_path"].InnerText;
                Src = $"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/{ProductType}/{FileName}";
                LogFile.Log("Firehose-programmer-file: " + Src);
                Result.Add(Src);
            }

            // Edp
            foreach (XmlNode SubNode in Doc.SelectNodes("//emergency_flash_config/second_boot_firehose_single_image/firehose_image"))
            {
                FileName = SubNode.Attributes["image_path"].InnerText;
                Src = $"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/{ProductType}/{FileName}";
                LogFile.Log("Firehose-payload-file: " + Src);
                Result.Add(Src);
            }

            return [.. Result];
        }
    }
}
