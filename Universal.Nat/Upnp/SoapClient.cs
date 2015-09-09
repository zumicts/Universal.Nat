﻿//
// Authors:
//   Lucas Ontivero lucasontivero@gmail.com 
//
// Copyright (C) 2014 Lucas Ontivero
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Universal.Nat.Exceptions;
using Universal.Nat.Utils;

namespace Universal.Nat.Upnp
{
    internal class SoapClient
    {
        private readonly string _serviceType;
        private readonly Uri _url;

        public SoapClient(Uri url, string serviceType)
        {
            _url = url;
            _serviceType = serviceType;
        }

        public async Task<XDocument> InvokeAsync(string operationName, IDictionary<string, object> args)
        {
            NatDiscoverer.TraceSource.TraceEvent(TraceEventType.Verbose, 0, "SOAPACTION: **{0}** url:{1}", operationName,
                                                 _url);
            byte[] messageBody = BuildMessageBody(operationName, args);
            HttpWebRequest request = BuildHttpWebRequest(operationName, messageBody);

            if (messageBody.Length > 0)
            {
                using (var stream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                {
                    await stream.WriteAsync(messageBody, 0, messageBody.Length).ConfigureAwait(false);
                }
            }

            using(var response = await GetWebResponse(request).ConfigureAwait(false))
            {
                var stream = response.GetResponseStream();
                var contentLength = response.ContentLength;

                var reader = new StreamReader(stream, Encoding.UTF8);

                var responseBody = contentLength != -1
                                    ? reader.ReadAsMany((int) contentLength)
                                    : reader.ReadToEnd();

                var responseXml = GetXmlDocument(responseBody);

                response.Dispose();
                return responseXml;
            }
        }

        private static async Task<WebResponse> GetWebResponse(WebRequest request)
        {
            WebResponse response;
            try
            {
                response = await request.GetResponseAsync().ConfigureAwait(false);
            }
            catch (WebException ex)
            {
                NatDiscoverer.TraceSource.TraceEvent(TraceEventType.Verbose, 0, "WebException status: {0}", ex.Status);

                // Even if the request "failed" we need to continue reading the response from the router
                response = ex.Response as HttpWebResponse;

                if (response == null)
                    throw;
            }
            return response;
        }

        private HttpWebRequest BuildHttpWebRequest(string operationName, byte[] messageBody)
        {
            var request = WebRequest.CreateHttp(_url);
            //request.KeepAlive = false;
            request.Method = "POST";
            request.ContentType = "text/xml; charset=\"utf-8\"";
            request.Headers["SOAPACTION"] = "\"" + _serviceType + "#" + operationName + "\"";
            request.Headers["Content-Length"] = messageBody.Length.ToString();
            return request;
        }

        private byte[] BuildMessageBody(string operationName, IEnumerable<KeyValuePair<string, object>> args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<s:Envelope ");
            sb.AppendLine("   xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" ");
            sb.AppendLine("   s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">");
            sb.AppendLine("   <s:Body>");
            sb.AppendLine("      <u:" + operationName + " xmlns:u=\"" + _serviceType + "\">");
            foreach (var a in args)
            {
                sb.AppendLine("         <" + a.Key + ">" + Convert.ToString(a.Value, CultureInfo.InvariantCulture) +
                              "</" + a.Key + ">");
            }
            sb.AppendLine("      </u:" + operationName + ">");
            sb.AppendLine("   </s:Body>");
            sb.Append("</s:Envelope>\r\n\r\n");
            string requestBody = sb.ToString();

            byte[] messageBody = Encoding.UTF8.GetBytes(requestBody);
            return messageBody;
        }

        private XDocument GetXmlDocument(string response)
        {
            XElement node;
            var doc = XDocument.Parse(response);

            // Error messages should be found under this namespace
            var ns = (XNamespace) "urn:schemas-upnp-org:control-1-0";

            // Check to see if we have a fault code message.
            if ((node = doc.Descendants(ns + "UPnPError").FirstOrDefault()) != null)
            {
                int code = Convert.ToInt32(node.Element(ns + "errorCode").Value, CultureInfo.InvariantCulture);
                string errorMessage = node.Element(ns + "errorDescription").Value;
                NatDiscoverer.TraceSource.LogWarn("Server failed with error: {0} - {1}", code, errorMessage);
                throw new MappingException(code, errorMessage);
            }

            return doc;
        }
    }
}