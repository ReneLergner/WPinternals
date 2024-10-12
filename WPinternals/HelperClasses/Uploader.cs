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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WPinternals.HelperClasses
{
#if PREVIEW
    internal static class Uploader
    {
        internal static List<Task> Uploads = [];

        internal static void Upload(string FileName, string Text)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(Text);
            MemoryStream FileStream = new(byteArray);
            Upload(FileName, FileStream);
        }

        internal static void Upload(string FileName, byte[] Data)
        {
            Upload(FileName, new MemoryStream(Data));
        }

        internal static void Upload(string FileName, Stream FileStream)
        {
            //TODO: Fix
            //Upload(new Uri(@"https://www.wpinternals.net/upload.php", UriKind.Absolute), "uploadedfile", FileName, FileStream);
        }

        private static void Upload(Uri Address, string InputName, string FileName, Stream FileStream)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            System.Net.Http.HttpClient httpClient = new();
            System.Net.Http.MultipartFormDataContent form = [];
            System.Net.Http.StreamContent Content = new(FileStream);
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
            Task.WaitAll([.. Uploads]);
        }
    }
#endif
}
