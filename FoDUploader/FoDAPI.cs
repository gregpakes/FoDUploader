﻿#region copyright
// Copyright (c) 2016 -  HPE Security Fortify on Demand, Ryan Black

//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
#endregion 

using System;
using System.Text;
using RestSharp;
using RestSharp.Deserializers;
using System.Web;
using System.Collections.Specialized;
using System.IO;
using FoDUploader.API;
using System.Diagnostics;

namespace FoDUploader
{
    class FoDAPI
    {
        private string apiToken;
        private string apiSecret;
        private string userName;
        private string password;
        private string submissionZIP;

        private bool doOpensourceReport;
        private bool doExpressScan;
        private bool doAutomatedAudit;
        private bool includeThirdParty;

        private bool isTokenAuth;
        private bool isDebug;

        private string accessToken;

        private UriBuilder baseURI;
        private NameValueCollection queryParameters;


        private const long seglen = (1024 * 1024) * 10;  // 10Mb chunk size
        private const int globaltimeoutinminutes = 1 * 60000;
        private const int maxretries = 5;


        public FoDAPI(Options options, string submissionZIP)
        {
            this.baseURI = new UriBuilder(options.uploadURL);
            this.queryParameters = GetqueryParameters(this.baseURI);
            this.isTokenAuth = (string.IsNullOrWhiteSpace(options.username)) ? true : false;
            this.submissionZIP = submissionZIP;
            this.apiToken = options.apiToken;
            this.apiSecret = options.apiTokenSecret;
            this.userName = options.username;
            this.password = options.password;
            this.doAutomatedAudit = options.automatedAudit;
            this.doOpensourceReport = options.opensourceReport;
            this.doExpressScan = options.expressScan;
            this.includeThirdParty = options.includeThirdParty;
            this.isDebug = options.debug;
        }

        private IRestResponse SendData(RestClient client, byte[] data, long fragNo, long offset)
        {

            var request = new RestRequest(Method.POST);

            request.AddHeader("Authorization", "Bearer " + accessToken);
            request.AddHeader("Content-Type", "application/octet-stream");

            // add tenant/scan parameters

            request.AddQueryParameter("assessmentTypeId", queryParameters.Get("astid"));
            request.AddQueryParameter("technologyStack", queryParameters.Get("ts"));

            if (queryParameters.Get("ts").Equals("JAVA/J2EE") || queryParameters.Get("ts").Equals(".NET") || queryParameters.Get("ts").Equals("PYTHON"))
            {
                request.AddQueryParameter("languageLevel", queryParameters.Get("ll"));
            }

            // add optional assessment parameters for sonatype, automated audit, express scanning

            request = AddOptionalParameters(request);

            request.AddQueryParameter("fragNo", fragNo.ToString());
            request.AddQueryParameter("offset", offset.ToString());
            request.AddParameter("application/octet-stream", data, ParameterType.RequestBody);

            var postURI = client.BuildUri(request).AbsoluteUri;

            if (isDebug)
            {
                Trace.WriteLine(string.Format("DEBUG: POST string: {0}", postURI));
            }

            int attempts = 0;
            string httpStatus = "";
            IRestResponse response;

            do
            {
                response = client.Execute(request);
                httpStatus = response.StatusCode.ToString();
                attempts++;
                if (httpStatus == "OK")
                {
                    break;
                }
                if (isDebug)
                {
                    Trace.WriteLine("Error: POST Response: " + response.Content.ToString());
                }
                if (attempts >= maxretries)
                {
                    Trace.WriteLine("Error: Maximum POST attempts reached, please check your connection and try again later.");
                    break;
                }
            }

            while (httpStatus != "OK" || attempts < maxretries);

            return response;
        }

        public void SendScanPost()
        {
            FileInfo fi = new FileInfo(submissionZIP);

            Trace.WriteLine("Beginning upload....");
            // endpoint https://www.hpfod.com/api/v1/Release/{releaseId}/scan/
            // parameters ?assessmentTypeId=&technologyStack=&languageLevel=&fragNo=&offset=&

            StringBuilder endpoint = new StringBuilder();
            endpoint.Append(baseURI.Scheme + "://");
            endpoint.Append(baseURI.Host + "/");
            endpoint.Append("api/v1/Release/");
            endpoint.Append(queryParameters.Get("pv"));
            endpoint.Append("/scan/");

            var client = new RestClient(endpoint.ToString());
            client.Timeout = globaltimeoutinminutes * 120;

            // Read it in chunks
            string uploadStatus = "";           

            using (FileStream fs = new FileStream(submissionZIP, FileMode.Open))
            {
                byte[] readByteBuffer = new byte[seglen];
                byte[] sendByteBuffer;

                int fragmentNumber = 0;
                int offset = 0;
                int bytesRead = 0;
                double bytesSent = 0;
                double fileSize = fi.Length;
                long chunkSize = seglen;

                try
                {
                    while ((bytesRead = fs.Read(readByteBuffer, 0, (int)chunkSize)) > 0)
                    {
                        decimal uploadProgress = Math.Round(((decimal)bytesSent / (decimal)fileSize) * 100.0M);

                        sendByteBuffer = new byte[chunkSize];

                        if (bytesRead < seglen)
                        {
                            fragmentNumber = -1;
                            Array.Resize<byte>(ref sendByteBuffer, bytesRead);
                            Array.Copy(readByteBuffer, sendByteBuffer, sendByteBuffer.Length);
                            var lastPost = SendData(client, sendByteBuffer, fragmentNumber, offset);
                            uploadStatus = lastPost.StatusCode.ToString();
                            bytesSent += bytesRead;
                            break;
                        }
                        Array.Copy(readByteBuffer, sendByteBuffer, sendByteBuffer.Length);
                        var post = SendData(client, sendByteBuffer, fragmentNumber++, offset);
                        uploadStatus = post.StatusCode.ToString();
                        offset += bytesRead;
                        bytesSent += bytesRead;

                        if ((fs.Length - offset) < seglen)
                        {
                            chunkSize = (fs.Length - offset);
                        }
                        Trace.WriteLine(uploadProgress + "%");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                    throw;
                }
                finally
                {
                    if (fs != null)
                    {
                        fs.Close();
                    }
                }

                if (uploadStatus == "OK")
                {
                    Trace.WriteLine("Assessment submission successful.");
                }
                else
                {
                    Trace.WriteLine("Error submitting to Fortify on Demand.");
                }
            }
        }


        /// <summary>
        /// Obtains, or updates, the authorization token for FoD. If isAuthToken is true client_credentials will be used instead of password
        /// </summary>
        public bool Authorize()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(baseURI.Scheme);
            sb.Append("://");
            sb.Append(baseURI.Host + "/");
            sb.Append("oauth/token");

            var client = new RestClient(sb.ToString());
            var request = new RestRequest("", Method.POST);

            request.AddParameter("scope", "https://hpfod.com/tenant");

            if (isTokenAuth)
            {
                request.AddParameter("grant_type", "client_credentials");
                request.AddParameter("client_id", apiToken);
                request.AddParameter("client_secret", apiSecret);
            }
            else
            {
                request.AddParameter("grant_type", "password");
                request.AddParameter("username", queryParameters.Get("tc") + @"\" + userName);
                request.AddParameter("password", password);
            }

            int attempts = 0;

            do
            {
                try
                {
                    attempts++;
                    var response = client.Execute(request);
                    accessToken = new JsonDeserializer().Deserialize<AuthorizationResponse>(response).accessToken;

                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        if (isDebug)
                        {
                            Trace.WriteLine(string.Format("DEBUG: Authentication Token: {0}", accessToken));
                        }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    accessToken = null;
                    Trace.WriteLine(string.Format("Error count {0} retrieving API access token", attempts));
                }
            }
            while (attempts <= maxretries);

            return false;
        }

        private RestRequest AddOptionalParameters(RestRequest request)
        {
            if (doOpensourceReport)
            {
                request.AddQueryParameter("doSonatypeScan", "true");
            }
            if (doAutomatedAudit)
            {
                request.AddQueryParameter("auditPreferenceId", "2");
            }
            if (doExpressScan)
            {
                request.AddQueryParameter("scanPreferenceId", "2");
            }
            if (includeThirdParty) // I am unsure if it's safe to let the default work so I'm explicit. 
            {
                request.AddQueryParameter("excludeThirdPartyLibs", "false");
            }
            if (!includeThirdParty)
            {
                request.AddQueryParameter("excludeThirdPartyLibs", "true");
            }
            return request;
        }

        public ReleaseResponse GetReleaseInfo()
        {

            StringBuilder endpoint = new StringBuilder();
            endpoint.Append(baseURI.Scheme + "://");
            endpoint.Append(baseURI.Host + "/");
            endpoint.Append("api/v1/Release/");
            endpoint.Append(queryParameters.Get("pv"));

            var client = new RestClient(endpoint.ToString());
            client.Timeout = globaltimeoutinminutes * 120;
            var request = new RestRequest(Method.GET);

            request.AddHeader("Authorization", "Bearer " + accessToken);
            request.AddHeader("Content-Type", "application/octet-stream");

            try
            {
               var response = client.Execute(request);
               ReleaseResponse release = new JsonDeserializer().Deserialize<ReleaseResponse>(response);
                return release;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                throw;
            }
        }

        public TenantEntitlementQuery GetEntitlementInfo()
        {
            StringBuilder endpoint = new StringBuilder();
            endpoint.Append(baseURI.Scheme + "://");
            endpoint.Append(baseURI.Host + "/");
            endpoint.Append("api/v2/TenantEntitlements");

            var client = new RestClient(endpoint.ToString());
            client.Timeout = globaltimeoutinminutes * 120;
            var request = new RestRequest(Method.GET);

            request.AddHeader("Authorization", "Bearer " + accessToken);
            request.AddHeader("Content-Type", "application/octet-stream");

            try
            {
                var response = client.Execute(request);
                TenantEntitlementQuery entitlements = new JsonDeserializer().Deserialize<TenantEntitlementQuery>(response);
                return entitlements;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                throw;
            }

        }

        public bool isLoggedIn()
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return false;
            }
            return true;
        }


        public void RetireToken()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(baseURI.Scheme);
            sb.Append("://");
            sb.Append(baseURI.Host + "/");
            sb.Append("oauth/retireToken");

            var client = new RestClient(sb.ToString());
            var request = new RestRequest("", Method.GET);

            request.AddHeader("Auhorization", "Bearer " + accessToken);

            try
            {
                var response = client.Execute(request);
                accessToken = new JsonDeserializer().Deserialize<AuthorizationResponse>(response).accessToken;
            }
            catch (Exception ex)
            {
                accessToken = null;
                Trace.WriteLine(ex.Message);
                throw;
            }
        }

        private NameValueCollection GetqueryParameters(UriBuilder postURL)
        {
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(postURL.Query);
            return queryParameters;
        }
    }
}
