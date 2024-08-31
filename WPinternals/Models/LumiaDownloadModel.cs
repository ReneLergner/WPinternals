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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace WPinternals
{
    internal static class LumiaDownloadModel
    {
        internal static string SearchFFU(string ProductType, string ProductCode, string OperatorCode)
        {
            return SearchFFU(ProductType, ProductCode, OperatorCode, out string FoundProductType);
        }

        internal static string SearchFFU(string ProductType, string ProductCode, string OperatorCode, out string FoundProductType)
        {
            if (ProductType?.Length == 0)
            {
                ProductType = null;
            }

            if (ProductCode?.Length == 0)
            {
                ProductCode = null;
            }

            if (OperatorCode?.Length == 0)
            {
                OperatorCode = null;
            }

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
                {
                    ProductType = "RM-" + ProductType[2..];
                }
            }
            if (OperatorCode != null)
            {
                OperatorCode = OperatorCode.ToUpper();
            }

            DiscoveryQueryParameters DiscoveryQueryParams = new()
            {
                manufacturerName = "Microsoft",
                manufacturerProductLine = "Lumia",
                packageType = "Firmware",
                packageClass = "Public",
                manufacturerHardwareModel = ProductType,
                manufacturerHardwareVariant = ProductCode,
                operatorName = OperatorCode
            };
            DiscoveryParameters DiscoveryParams = new()
            {
                query = DiscoveryQueryParams
            };

            DataContractJsonSerializer Serializer1 = new(typeof(DiscoveryParameters));
            MemoryStream JsonStream1 = new();
            Serializer1.WriteObject(JsonStream1, DiscoveryParams);
            JsonStream1.Seek(0L, SeekOrigin.Begin);
            string JsonContent = new StreamReader(JsonStream1).ReadToEnd();

            Uri RequestUri = new("https://api.swrepository.com/rest-api/discovery/1/package");

            HttpClient HttpClient = new();
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
            using (MemoryStream JsonStream2 = new(Encoding.UTF8.GetBytes(JsonResultString)))
            {
                DataContractJsonSerializer Serializer2 = new(typeof(SoftwarePackages));
                SoftwarePackages SoftwarePackages = (SoftwarePackages)Serializer2.ReadObject(JsonStream2);
                if (SoftwarePackages != null)
                {
                    Package = SoftwarePackages.softwarePackages.FirstOrDefault();
                }
            }

            if (Package == null)
            {
                throw new WPinternalsException("FFU not found", "No FFU has been found in the remote software repository for the requested model.");
            }

            FoundProductType = Package.manufacturerHardwareModel[0];

            SoftwareFile FileInfo = Package.files.First(f => f.fileName.EndsWith(".ffu", StringComparison.OrdinalIgnoreCase));

            Uri FileInfoUri = new("https://api.swrepository.com/rest-api/discovery/fileurl/1/" + Package.id + "/" + FileInfo.fileName);
            Task<string> GetFileInfoTask = HttpClient.GetStringAsync(FileInfoUri);
            GetFileInfoTask.Wait();
            string FileInfoString = GetFileInfoTask.Result;

            string FfuUrl = "";
            FileUrlResult FileUrl = null;
            using (MemoryStream JsonStream3 = new(Encoding.UTF8.GetBytes(FileInfoString)))
            {
                DataContractJsonSerializer Serializer3 = new(typeof(FileUrlResult));
                FileUrl = (FileUrlResult)Serializer3.ReadObject(JsonStream3);
                if (FileUrl != null)
                {
                    FfuUrl = FileUrl.url.Replace("sr.azureedge.net", "softwarerepo.blob.core.windows.net");
                }
            }

            HttpClient.Dispose();

            return FfuUrl;
        }

        internal static (string SecureWIMUrl, string DPLUrl) SearchENOSW(string ProductType, string PhoneFirmwareRevision)
        {
            if (ProductType?.Length == 0)
            {
                ProductType = null;
            }

            if (ProductType != null)
            {
                ProductType = ProductType.ToUpper();
                if (ProductType.StartsWith("RM") && !ProductType.StartsWith("RM-"))
                {
                    ProductType = $"RM-{ProductType[2..]}";
                }
            }

            DiscoveryQueryParameters DiscoveryQueryParams = new()
            {
                manufacturerName = "Microsoft",
                manufacturerProductLine = "Lumia",
                packageType = "Test Mode",
                packageClass = "Public",
                manufacturerHardwareModel = ProductType
            };

            DiscoveryParameters DiscoveryParams = new()
            {
                query = DiscoveryQueryParams
            };

            DataContractJsonSerializer Serializer1 = new(typeof(DiscoveryParameters));
            MemoryStream JsonStream1 = new();
            Serializer1.WriteObject(JsonStream1, DiscoveryParams);
            JsonStream1.Seek(0L, SeekOrigin.Begin);
            string JsonContent = new StreamReader(JsonStream1).ReadToEnd();

            Uri RequestUri = new("https://api.swrepository.com/rest-api/discovery/1/package");

            HttpClient HttpClient = new();
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
            using MemoryStream JsonResultStream = new(Encoding.UTF8.GetBytes(JsonResultString));
            DataContractJsonSerializer SoftwarePackagesJsonSerializer = new(typeof(SoftwarePackages));
            SoftwarePackages SoftwarePackages = (SoftwarePackages)SoftwarePackagesJsonSerializer.ReadObject(JsonResultStream);

            if (SoftwarePackages != null)
            {
                foreach (SoftwarePackage pkg in SoftwarePackages.softwarePackages)
                {
                    Package = SoftwarePackages.softwarePackages.FirstOrDefault();
                }
            }

            if (Package == null)
            {
                throw new WPinternalsException("ENOSW package not found", "No ENOSW package has been found in the remote software repository for the requested model.");
            }

            SoftwareFile SecureWimSoftwareFile = Package.files.First(f => f.fileName.EndsWith(".secwim", StringComparison.OrdinalIgnoreCase));
            SoftwareFile DPLSoftwareFile = Package.files.First(f => f.fileName.EndsWith(".dpl", StringComparison.OrdinalIgnoreCase));

            Uri DPLFileUrlUri = new($"https://api.swrepository.com/rest-api/discovery/fileurl/1/{Package.id}/{DPLSoftwareFile.fileName}");

            Task<string> GetDPLTask = HttpClient.GetStringAsync(DPLFileUrlUri);
            GetDPLTask.Wait();

            string DPLFileUrlResultContent = GetDPLTask.Result;
            FileUrlResult DPLFileUrlResult = null;
            using MemoryStream DPLFileUrlResultStream = new(Encoding.UTF8.GetBytes(DPLFileUrlResultContent));
            DataContractJsonSerializer DPLFileUrlResultSerializer = new(typeof(FileUrlResult));
            DPLFileUrlResult = (FileUrlResult)DPLFileUrlResultSerializer.ReadObject(DPLFileUrlResultStream);

            string DPLFileUrl = "";

            if (DPLFileUrlResult != null)
            {
                DPLFileUrl = DPLFileUrlResult.url.Replace("sr.azureedge.net", "softwarerepo.blob.core.windows.net");
            }

            if (DPLFileUrl?.Length == 0)
            {
                throw new WPinternalsException("DPL not found", "No DPL has been found in the remote software repository for the requested model.");
            }

            Task<string> GetDPLStrTask = HttpClient.GetStringAsync(DPLFileUrl);
            GetDPLStrTask.Wait();
            string DPLStrString = GetDPLStrTask.Result;

            DPL.Package dpl;
            XmlSerializer serializer = new(typeof(DPL.Package));
            using StringReader reader = new(DPLStrString.Replace("ft:", "").Replace("dpl:", "").Replace("typedes:", ""));
            dpl = (DPL.Package)serializer.Deserialize(reader);

            foreach (DPL.File file in dpl.Content.Files.File)
            {
                string name = file.Name;

                DPL.Range range = file.Extensions.MmosWimFile.UseCaseCompatibilities.Compatibility.FirstOrDefault().Range;

                if (IsFirmwareBetween(PhoneFirmwareRevision, range.From, range.To))
                {
                    SecureWimSoftwareFile = Package.files.First(f => f.fileName.EndsWith(name, StringComparison.OrdinalIgnoreCase));
                }
            }

            Uri FileInfoUri = new("https://api.swrepository.com/rest-api/discovery/fileurl/1/" + Package.id + "/" + SecureWimSoftwareFile.fileName);
            Task<string> GetFileInfoTask = HttpClient.GetStringAsync(FileInfoUri);
            GetFileInfoTask.Wait();
            string FileInfoString = GetFileInfoTask.Result;

            string ENOSWFileUrl = "";

            FileUrlResult FileUrl = null;

            using MemoryStream JsonStream4 = new(Encoding.UTF8.GetBytes(FileInfoString));
            DataContractJsonSerializer Serializer4 = new(typeof(FileUrlResult));
            FileUrl = (FileUrlResult)Serializer4.ReadObject(JsonStream4);
            if (FileUrl != null)
            {
                ENOSWFileUrl = FileUrl.url.Replace("sr.azureedge.net", "softwarerepo.blob.core.windows.net");
            }

            HttpClient.Dispose();

            return (ENOSWFileUrl, DPLFileUrl);
        }

        private static bool IsFirmwareBetween(string PhoneFirmwareRevision, string Limit1, string Limit2)
        {
            var version = new Version(PhoneFirmwareRevision);
            var version1 = new Version(Limit1);
            var version2 = new Version(Limit2);

            var result = version.CompareTo(version1);
            var result2 = version.CompareTo(version2);

            return result >= 0 && result2 <= 0;
        }

        internal static string[] SearchEmergencyFiles(string ProductType)
        {
            ProductType = ProductType.ToUpper();
            if (ProductType.StartsWith("RM") && !ProductType.StartsWith("RM-"))
            {
                ProductType = "RM-" + ProductType[2..];
            }

            LogFile.Log("Getting Emergency files for: " + ProductType, LogType.FileAndConsole);

            if ((ProductType == "RM-1072") || (ProductType == "RM-1073"))
            {
                LogFile.Log("Due to mix-up in online-repository, redirecting to emergency files of RM-1113", LogType.FileAndConsole);
                ProductType = "RM-1113";
            }

            List<string> Result = new();

            WebClient Client = new();
            string Src;
            string FileName;
            string Config = null;
            try
            {
                Config = Client.DownloadString("https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/emergency_flash_config.xml");
            }
            catch
            {
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
                Src = "https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/" + FileName;
                LogFile.Log("Hex-file: " + Src);
                Result.Add(Src);
            }

            // Mbn
            Node = Doc.SelectSingleNode("//emergency_flash_config/mbn_image");
            if (Node != null)
            {
                FileName = Node.Attributes["image_path"].InnerText;
                Src = "https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/" + FileName;
                LogFile.Log("Mbn-file: " + Src);
                Result.Add(Src);
            }

            // Ede
            foreach (XmlNode SubNode in Doc.SelectNodes("//emergency_flash_config/first_boot_images/first_boot_image"))
            {
                FileName = SubNode.Attributes["image_path"].InnerText;
                Src = "https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/" + FileName;
                LogFile.Log("Firehose-programmer-file: " + Src);
                Result.Add(Src);
            }

            // Edp
            foreach (XmlNode SubNode in Doc.SelectNodes("//emergency_flash_config/second_boot_firehose_single_image/firehose_image"))
            {
                FileName = SubNode.Attributes["image_path"].InnerText;
                Src = "https://repairavoidance.blob.core.windows.net/packages/EmergencyFlash/" + ProductType + "/" + FileName;
                LogFile.Log("Firehose-payload-file: " + Src);
                Result.Add(Src);
            }

            return [.. Result];
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

        public DiscoveryParameters() : this(DiscoveryCondition.Default)
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

    public static class DPL
    {
        [XmlRoot(ElementName = "BasicProductCodes")]
        public class BasicProductCodes
        {
            [XmlElement(ElementName = "BasicProductCode")]
            public List<string> BasicProductCode { get; set; }
        }

        [XmlRoot(ElementName = "Identification")]
        public class Identification
        {
            [XmlElement(ElementName = "TypeDesignator")]
            public string TypeDesignator { get; set; }
            [XmlElement(ElementName = "BasicProductCodes")]
            public BasicProductCodes BasicProductCodes { get; set; }
            [XmlElement(ElementName = "Purpose")]
            public string Purpose { get; set; }
        }

        [XmlRoot(ElementName = "Extensions")]
        public class Extensions
        {
            [XmlElement(ElementName = "PackageType")]
            public string PackageType { get; set; }
            [XmlElement(ElementName = "Identification")]
            public Identification Identification { get; set; }
            [XmlElement(ElementName = "FileType")]
            public string FileType { get; set; }
            [XmlElement(ElementName = "MmosWimFile")]
            public MmosWimFile MmosWimFile { get; set; }
        }

        [XmlRoot(ElementName = "PackageDescription")]
        public class PackageDescription
        {
            [XmlElement(ElementName = "Identifier")]
            public string Identifier { get; set; }
            [XmlElement(ElementName = "Revision")]
            public string Revision { get; set; }
            [XmlElement(ElementName = "Extensions")]
            public Extensions Extensions { get; set; }
        }

        [XmlRoot(ElementName = "Digest")]
        public class Digest
        {
            [XmlAttribute(AttributeName = "method")]
            public string Method { get; set; }
            [XmlAttribute(AttributeName = "encoding")]
            public string Encoding { get; set; }
            [XmlText]
            public string Text { get; set; }
        }

        [XmlRoot(ElementName = "Digests")]
        public class Digests
        {
            [XmlElement(ElementName = "Digest")]
            public List<Digest> Digest { get; set; }
        }

        [XmlRoot(ElementName = "Range")]
        public class Range
        {
            [XmlAttribute(AttributeName = "from")]
            public string From { get; set; }
            [XmlAttribute(AttributeName = "to")]
            public string To { get; set; }
        }

        [XmlRoot(ElementName = "Compatibility")]
        public class Compatibility
        {
            [XmlElement(ElementName = "Range")]
            public Range Range { get; set; }
            [XmlAttribute(AttributeName = "useCase")]
            public string UseCase { get; set; }
        }

        [XmlRoot(ElementName = "UseCaseCompatibilities")]
        public class UseCaseCompatibilities
        {
            [XmlElement(ElementName = "Compatibility")]
            public List<Compatibility> Compatibility { get; set; }
        }

        [XmlRoot(ElementName = "MmosWimFile")]
        public class MmosWimFile
        {
            [XmlElement(ElementName = "UseCaseCompatibilities")]
            public UseCaseCompatibilities UseCaseCompatibilities { get; set; }
        }

        [XmlRoot(ElementName = "File")]
        public class File
        {
            [XmlElement(ElementName = "Name")]
            public string Name { get; set; }
            [XmlElement(ElementName = "Digests")]
            public Digests Digests { get; set; }
            [XmlElement(ElementName = "Revision")]
            public string Revision { get; set; }
            [XmlElement(ElementName = "Extensions")]
            public Extensions Extensions { get; set; }
        }

        [XmlRoot(ElementName = "Files")]
        public class Files
        {
            [XmlElement(ElementName = "File")]
            public List<File> File { get; set; }
        }

        [XmlRoot(ElementName = "Content")]
        public class Content
        {
            [XmlElement(ElementName = "PackageDescription")]
            public PackageDescription PackageDescription { get; set; }
            [XmlElement(ElementName = "Files")]
            public Files Files { get; set; }
        }

        [XmlRoot(ElementName = "CanonicalizationMethod", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public class CanonicalizationMethod
        {
            [XmlAttribute(AttributeName = "Algorithm")]
            public string Algorithm { get; set; }
        }

        [XmlRoot(ElementName = "SignatureMethod", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public class SignatureMethod
        {
            [XmlAttribute(AttributeName = "Algorithm")]
            public string Algorithm { get; set; }
        }

        [XmlRoot(ElementName = "Transform", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public class Transform
        {
            [XmlAttribute(AttributeName = "Algorithm")]
            public string Algorithm { get; set; }
        }

        [XmlRoot(ElementName = "Transforms", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public class Transforms
        {
            [XmlElement(ElementName = "Transform", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public Transform Transform { get; set; }
        }

        [XmlRoot(ElementName = "DigestMethod", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public class DigestMethod
        {
            [XmlAttribute(AttributeName = "Algorithm")]
            public string Algorithm { get; set; }
        }

        [XmlRoot(ElementName = "Reference", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public class Reference
        {
            [XmlElement(ElementName = "Transforms", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public Transforms Transforms { get; set; }
            [XmlElement(ElementName = "DigestMethod", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public DigestMethod DigestMethod { get; set; }
            [XmlElement(ElementName = "DigestValue", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public string DigestValue { get; set; }
            [XmlAttribute(AttributeName = "URI")]
            public string URI { get; set; }
        }

        [XmlRoot(ElementName = "SignedInfo", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public class SignedInfo
        {
            [XmlElement(ElementName = "CanonicalizationMethod", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public CanonicalizationMethod CanonicalizationMethod { get; set; }
            [XmlElement(ElementName = "SignatureMethod", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public SignatureMethod SignatureMethod { get; set; }
            [XmlElement(ElementName = "Reference", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public Reference Reference { get; set; }
        }

        [XmlRoot(ElementName = "KeyInfo", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public class KeyInfo
        {
            [XmlElement(ElementName = "KeyName", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public string KeyName { get; set; }
        }

        [XmlRoot(ElementName = "Signature", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
        public class Signature
        {
            [XmlElement(ElementName = "SignedInfo", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public SignedInfo SignedInfo { get; set; }
            [XmlElement(ElementName = "SignatureValue", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public string SignatureValue { get; set; }
            [XmlElement(ElementName = "KeyInfo", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public KeyInfo KeyInfo { get; set; }
            [XmlAttribute(AttributeName = "xmlns")]
            public string Xmlns { get; set; }
        }

        [XmlRoot(ElementName = "Package")]
        public class Package
        {
            [XmlElement(ElementName = "Content")]
            public Content Content { get; set; }
            [XmlElement(ElementName = "Signature", Namespace = "http://www.w3.org/2000/09/xmldsig#")]
            public Signature Signature { get; set; }
        }
    }
}
