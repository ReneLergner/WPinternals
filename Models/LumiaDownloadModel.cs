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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Xml;

namespace WPinternals
{
    internal static class LumiaDownloadModel
    {
        internal static string SearchFFU(string ProductType, string ProductCode, string OperatorCode)
        {
            string FoundProductType;
            return SearchFFU(ProductType, ProductCode, OperatorCode, out FoundProductType);
        }

        internal static string SearchFFU(string ProductType, string ProductCode, string OperatorCode, out string FoundProductType)
        {
            if (ProductType == "")
                ProductType = null;
            if (ProductCode == "")
                ProductCode = null;
            if (OperatorCode == "")
                OperatorCode = null;

            if (ProductCode != null)
            {
                ProductCode = ProductCode.ToUpper();
                ProductType = null;
                OperatorCode = null;
            }
            if (ProductType != null)
            {
                ProductType = ProductType.ToUpper();
                if (ProductType.StartsWith("RM") && !ProductType.StartsWith("RM-"))
                    ProductType = "RM-" + ProductType.Substring(2);
            }
            if (OperatorCode != null)
                OperatorCode = OperatorCode.ToUpper();

            DiscoveryQueryParameters DiscoveryQueryParams = new DiscoveryQueryParameters
            {
                manufacturerName = "Microsoft",
                manufacturerProductLine = "Lumia",
                packageType = "Firmware",
                packageClass = "Public",
                manufacturerHardwareModel = ProductType,
                manufacturerHardwareVariant = ProductCode,
                operatorName = OperatorCode
            };
            DiscoveryParameters DiscoveryParams = new DiscoveryParameters
            {
                query = DiscoveryQueryParams
            };

            DataContractJsonSerializer Serializer1 = new DataContractJsonSerializer(typeof(DiscoveryParameters));
            MemoryStream JsonStream1 = new MemoryStream();
            Serializer1.WriteObject(JsonStream1, DiscoveryParams);
            JsonStream1.Seek(0L, SeekOrigin.Begin);
            string JsonContent = new StreamReader(JsonStream1).ReadToEnd();

            Uri RequestUri = new Uri("https://api.swrepository.com/rest-api/discovery/1/package");

            HttpClient HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("SoftwareRepository");
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            Task<HttpResponseMessage> HttpPostTask = HttpClient.PostAsync(RequestUri, new StringContent(JsonContent, Encoding.UTF8, "application/json"));
            HttpPostTask.Wait();
            HttpResponseMessage Response = HttpPostTask.Result;

            string JsonResultString = "";
            if (Response.StatusCode == HttpStatusCode.OK)
            {
                Task<string> ReadResponseTask = Response.Content.ReadAsStringAsync();
                ReadResponseTask.Wait();
                JsonResultString = ReadResponseTask.Result;
            }

            SoftwarePackage Package = null;
            using (MemoryStream JsonStream2 = new MemoryStream(Encoding.UTF8.GetBytes(JsonResultString)))
            {
                DataContractJsonSerializer Serializer2 = new DataContractJsonSerializer(typeof(SoftwarePackages));
                SoftwarePackages SoftwarePackages = (SoftwarePackages)Serializer2.ReadObject(JsonStream2);
                if (SoftwarePackages != null)
                {
                    Package = SoftwarePackages.softwarePackages.FirstOrDefault<SoftwarePackage>();
                }
            }

            if (Package == null)
                throw new WPinternalsException("FFU not found");

            FoundProductType = Package.manufacturerHardwareModel[0];

            SoftwareFile FileInfo = Package.files.Where(f => f.fileName.EndsWith(".ffu", StringComparison.OrdinalIgnoreCase)).First();

            Uri FileInfoUri = new Uri("https://api.swrepository.com/rest-api/discovery/fileurl/1/" + Package.id + "/" + FileInfo.fileName);
            Task<string> GetFileInfoTask = HttpClient.GetStringAsync(FileInfoUri);
            GetFileInfoTask.Wait();
            string FileInfoString = GetFileInfoTask.Result;

            string FfuUrl = "";
            FileUrlResult FileUrl = null;
            using (MemoryStream JsonStream3 = new MemoryStream(Encoding.UTF8.GetBytes(FileInfoString)))
            {
                DataContractJsonSerializer Serializer3 = new DataContractJsonSerializer(typeof(FileUrlResult));
                FileUrl = (FileUrlResult)Serializer3.ReadObject(JsonStream3);
                if (FileUrl != null)
                {
                    FfuUrl = FileUrl.url;
                }
            }

            HttpClient.Dispose();

            return FfuUrl;
        }

        internal static string[] SearchEmergencyFiles(string ProductType)
        {
            ProductType = ProductType.ToUpper();
            if (ProductType.StartsWith("RM") && !ProductType.StartsWith("RM-"))
                ProductType = "RM-" + ProductType.Substring(2);

            LogFile.Log("Getting Emergency files for: " + ProductType, LogType.FileAndConsole);

            if ((ProductType == "RM-1072") || (ProductType == "RM-1073"))
            {
                LogFile.Log("Due to mix-up in online-repository, redirecting to emergency files of RM-1113", LogType.FileAndConsole);
                ProductType = "RM-1113";
            }

            List<string> Result = new List<string>();

            WebClient Client = new WebClient();
            string Src;
            string FileName;
            string Config = null;
            try
            {
                Config = Client.DownloadString(@"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/emergency_flash_config.xml");
            }
            catch
            {
                LogFile.Log("Emergency files for " + ProductType + " not found", LogType.FileAndConsole);
                return null;
            }
            Client.Dispose();

            XmlDocument Doc = new XmlDocument();
            Doc.LoadXml(Config);

            // Hex
            XmlNode Node = Doc.SelectSingleNode("//emergency_flash_config/hex_flasher");
            if (Node != null)
            {
                FileName = Node.Attributes["image_path"].InnerText;
                Src = @"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/" + FileName;
                LogFile.Log("Hex-file: " + Src);
                Result.Add(Src);
            }

            // Mbn
            Node = Doc.SelectSingleNode("//emergency_flash_config/mbn_image");
            if (Node != null)
            {
                FileName = Node.Attributes["image_path"].InnerText;
                Src = @"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/" + FileName;
                LogFile.Log("Mbn-file: " + Src);
                Result.Add(Src);
            }

            // Ede
            foreach (XmlNode SubNode in Doc.SelectNodes("//emergency_flash_config/first_boot_images/first_boot_image"))
            {
                FileName = SubNode.Attributes["image_path"].InnerText;
                Src = @"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/" + FileName;
                LogFile.Log("Firehose-programmer-file: " + Src);
                Result.Add(Src);
            }

            // Edp
            foreach (XmlNode SubNode in Doc.SelectNodes("//emergency_flash_config/second_boot_firehose_single_image/firehose_image"))
            {
                FileName = SubNode.Attributes["image_path"].InnerText;
                Src = @"https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/" + FileName;
                LogFile.Log("Firehose-payload-file: " + Src);
                Result.Add(Src);
            }

            return Result.ToArray();
        }
    }

    #pragma warning disable 0649
    [DataContract]
    internal class FileUrlResult
    {
        [DataMember]
        internal string url;

        [DataMember]
        internal List<string> alternateUrl;

        [DataMember]
        internal long fileSize;

        [DataMember]
        internal List<SoftwareFileChecksum> checksum;
    }
    #pragma warning restore 0649

    [DataContract]
    public class DiscoveryQueryParameters
    {
        [DataMember(EmitDefaultValue = false)]
        public string customerName;

        [DataMember(EmitDefaultValue = false)]
        public ExtendedAttributes extendedAttributes;

        [DataMember(EmitDefaultValue = false)]
        public string manufacturerHardwareModel;

        [DataMember(EmitDefaultValue = false)]
        public string manufacturerHardwareVariant;

        [DataMember(EmitDefaultValue = false)]
        public string manufacturerModelName;

        [DataMember]
        public string manufacturerName;

        [DataMember(EmitDefaultValue = false)]
        public string manufacturerPackageId;

        [DataMember(EmitDefaultValue = false)]
        public string manufacturerPlatformId;

        [DataMember]
        public string manufacturerProductLine;

        [DataMember(EmitDefaultValue = false)]
        public string manufacturerVariantName;

        [DataMember(EmitDefaultValue = false)]
        public string operatorName;

        [DataMember]
        public string packageClass;

        [DataMember(EmitDefaultValue = false)]
        public string packageRevision;

        [DataMember(EmitDefaultValue = false)]
        public string packageState;

        [DataMember(EmitDefaultValue = false)]
        public string packageSubRevision;

        [DataMember(EmitDefaultValue = false)]
        public string packageSubtitle;

        [DataMember(EmitDefaultValue = false)]
        public string packageTitle;

        [DataMember]
        public string packageType;
    }

    [DataContract]
    public class DiscoveryParameters
    {
        [DataMember(Name = "api-version")]
        public string apiVersion;

        [DataMember]
        public DiscoveryQueryParameters query;

        [DataMember]
        public List<string> condition;

        [DataMember]
        public List<string> response;

        public DiscoveryParameters(): this(DiscoveryCondition.Default)
        {
        }

        public DiscoveryParameters(DiscoveryCondition Condition)
        {
            this.apiVersion = "1";
            this.query = new DiscoveryQueryParameters();
            this.condition = new List<string>();
            if (Condition == DiscoveryCondition.All)
            {
                this.condition.Add("all");
                return;
            }
            if (Condition == DiscoveryCondition.Latest)
            {
                this.condition.Add("latest");
                return;
            }
            this.condition.Add("default");
        }
    }

    public enum DiscoveryCondition
    {
        Default,
        All,
        Latest
    }

    [Serializable]
    public class ExtendedAttributes : ISerializable
    {
        public Dictionary<string, string> Dictionary;

        public ExtendedAttributes()
        {
            this.Dictionary = new Dictionary<string, string>();
        }

        protected ExtendedAttributes(SerializationInfo info, StreamingContext context)
        {
            if (info != null)
            {
                this.Dictionary = new Dictionary<string, string>();
                SerializationInfoEnumerator Enumerator = info.GetEnumerator();
                while (Enumerator.MoveNext())
                {
                    this.Dictionary.Add(Enumerator.Current.Name, (string)Enumerator.Current.Value);
                }
            }
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info != null)
            {
                foreach (string Current in this.Dictionary.Keys)
                {
                    info.AddValue(Current, this.Dictionary[Current]);
                }
            }
        }
    }

    [DataContract]
    public class SoftwareFileChecksum
    {
        [DataMember]
        public string type;

        [DataMember]
        public string value;
    }

    [DataContract]
    public class SoftwarePackages
    {
        [DataMember]
        public List<SoftwarePackage> softwarePackages;
    }

    [DataContract]
    public class SoftwarePackage
    {
        [DataMember]
        public List<string> customerName;

        [DataMember]
        public ExtendedAttributes extendedAttributes;

        [DataMember]
        public List<SoftwareFile> files;

        [DataMember]
        public string id;

        [DataMember]
        public List<string> manufacturerHardwareModel;

        [DataMember]
        public List<string> manufacturerHardwareVariant;

        [DataMember]
        public List<string> manufacturerModelName;

        [DataMember]
        public string manufacturerName;

        [DataMember]
        public string manufacturerPackageId;

        [DataMember]
        public List<string> manufacturerPlatformId;

        [DataMember]
        public string manufacturerProductLine;

        [DataMember]
        public List<string> manufacturerVariantName;

        [DataMember]
        public List<string> operatorName;

        [DataMember]
        public List<string> packageClass;

        [DataMember]
        public string packageDescription;

        [DataMember]
        public string packageRevision;

        [DataMember]
        public string packageState;

        [DataMember]
        public string packageSubRevision;

        [DataMember]
        public string packageSubtitle;

        [DataMember]
        public string packageTitle;

        [DataMember]
        public string packageType;
    }

    [DataContract]
    public class SoftwareFile
    {
        [DataMember]
        public List<SoftwareFileChecksum> checksum;

        [DataMember]
        public string fileName;

        [DataMember]
        public long fileSize;

        [DataMember]
        public string fileType;
    }
}
