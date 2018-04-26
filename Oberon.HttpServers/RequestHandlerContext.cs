/* Copyright (c) 2014 Oberon microsystems, Inc. (Switzerland)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License. */

// Originally developed for the book
//   "Getting Started with the Internet of Things", by Cuno Pfister.
//   Copyright 2011 Cuno Pfister, Inc., 978-1-4493-9357-1.
//
// Version 4.3, for the .NET Micro Framework release 4.3.
//
// Server-specific parts of Gsiot.Server namespace.
// See the book "Getting Started with the Internet of Things", Appendix C.
//
// A RequestHandlerContext is the interface for a request handler,
// allowing it to read request information and to write response information.

using Oberon.HttpStreams;
using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

namespace Oberon.HttpServers
{
    /// <summary>
    /// An instance of class RequestHandlerContext provides information
    /// about the received HTTP request to a request handler. The request
    /// handler uses it to set up the HTTP response to this request, and
    /// if necessary, to construct URIs to the same service.
    /// </summary>
    public class RequestHandlerContext
    {
        // To keep this class simple, the following holds:
        //   - It only supports the most important HTTP headers.

        // server interface
        string serviceRoot;     // where ((serviceRoot != null) &&
        //                         ("serviceRoot is an absolute URI")
        string relayDomain;     // null iff no relay is used

        /// <summary>
        /// Constructor of RequestHandlerContext.
        /// </summary>
        /// <param name="serviceRoot">The URI relative to which the
        /// request URIs are processed, e.g., http://192.168.5.100:8080.
        /// </param>
        /// <param name="relayDomain">Indicates whether a relay is used;
        /// otherwise, it is null.</param>
        public RequestHandlerContext(string serviceRoot,
            string relayDomain,
            HttpReader reader, HttpWriter writer)
        {
            Contract.Requires(serviceRoot != null);
            Contract.Requires(serviceRoot.Substring(0, 7) == "http://");
            Contract.Requires(serviceRoot[serviceRoot.Length - 1] != '/');
            Contract.Requires(reader != null);
            Contract.Requires(writer != null);
            this.serviceRoot = serviceRoot;
            this.relayDomain = relayDomain;
            RequestStream = reader;
            ResponseStream = writer;
        }

        // TODO docu
        public Stream RequestStream { get; private set; }

        // TODO docu
        public Stream ResponseStream { get; private set; }

        /// <summary>
        /// Before a request handler is called, this property is set to
        /// true if (and only if) the received request contained a
        /// Connection: close header or the server has its
        /// KeepConnectionOpenAfterResponse set to false (the default).
        /// If the request handler wants to indicate that it wants to
        /// close the connection, it can set the property to true,
        /// which will add the Connection: close header to its response.
        /// </summary>
        public bool ConnectionClose { get; set; }

        // request interface

        string requestUri;

        /// <summary>
        /// This property tells you which kind of request has been
        /// received (an HTTP method such as GET or PUT). You only need
        /// to check this property if you want to support several HTTP
        /// methods in the same request handler, i.e., request patterns
        /// with a * wildcard at the beginning.
        /// </summary>
        public string RequestMethod { get; internal set; }

        /// <summary>
        /// This property contains the URI of the incoming request. You
        /// only need this property if you want to support several
        /// resources in the same request handler, i.e., request patterns
        /// with a * wildcard at the end.
        /// </summary>
        public string RequestUri
        {
            get { return requestUri; }

            internal set
            {
                Contract.Requires(value != null);
                Contract.Requires(value.Length > 0);
                Contract.Requires(value[0] == '/');
                if ((relayDomain != null) && (value == '/' + relayDomain))
                {   // After stripping away the relay prefix, this
                    // would be an illegal request URI (empty string).
                    value = value + '/';
                }
                requestUri = value;
            }
        }

        /// <summary>
        /// This property contains the content of the request’s
        /// Content-Length header if one was present; otherwise, it is
        /// null.
        /// </summary>
        public string RequestContentType { get; internal set; }

        /// <summary>
        /// This property contains the content of the request message body.
        /// </summary>
        public byte[] RequestByteArray { get; internal set; }

        string requestContentString = null;

        /// <summary>
        /// This property contains the request message body converted into
        /// a string of text, with a UTF8 encoding. You only need this
        /// property for PUT and POST requests, since GET and DELETE have
        /// no message bodies.
        /// </summary>
        public string RequestString
        {
            get
            {
                if (requestContentString == null)
                {
                    try
                    {
                        char[] chars = Encoding.UTF8.GetChars(RequestByteArray);
                        requestContentString = new string(chars);
                    }
                    catch (Exception)
                    {
                        requestContentString = null;
                    }
                }
                return requestContentString;
            }
        }

        internal bool RequestMatch(RoutingElement e)
        {
            Contract.Requires(e != null);
            Contract.Requires(e.Path != null);
            Contract.Requires(e.Path.Length > 0);
            Contract.Requires(e.Path[0] == '/');
            // Pattern = ( Method | '*') path [ '*' ]
            string uri = RequestUri;
            Contract.Requires(uri != null);
            int uriLength = uri.Length;
            Contract.Requires(uriLength >= 1);
            if (uri[0] != '/')      // some proxies return absolute URIs
            {
                return false;
            }
            string method = RequestMethod;
            Contract.Requires(method != null);
            int methodLength = method.Length;
            Contract.Requires(methodLength >= 3);
            if ((method != e.Method) && (e.Method != "*")) { return false; }

            var pos = 1;
            if (relayDomain != null)    // try to match relay domain
            {
                int relayPrefixLength = relayDomain.Length + 1;
                if (uriLength <= relayPrefixLength) { return false; }
                while (pos != relayPrefixLength)
                {
                    if (uri[pos] != relayDomain[pos - 1]) { return false; }
                    pos = pos + 1;
                }
                if (uri[pos] != '/') { return false; }
                pos = pos + 1;
            }
            // try to match request pattern
            int patternLength = e.Path.Length;
            if (uriLength < (pos - 1 + patternLength)) { return false; }
            var i = 1;
            while (i != patternLength)
            {
                if (uri[pos] != e.Path[i]) { return false; }
                pos = pos + 1;
                i = i + 1;
            }
            return ((pos == uriLength) || (e.Wildcard));
        }

        // server interface

        /// <summary>
        /// This method takes a path and constructs a relative URI out of
        /// it. If the request was relayed, this is taken into account.
        /// For example, BuildRequestUri("hello.html") may return
        /// /gsiot-FFMQ-TTD5/hello.html if the request pattern was
        /// "GET /hello*".
        /// You should use this method if your response contains relative
        /// hyperlinks to your server.
        /// 
        /// Preconditions
        ///     path != null
        ///     path.Length > 0
        ///     path[0] == '/'
        /// </summary>
        /// <param name="path">Relative path starting with a /.</param>
        /// <returns>The same string if no relay is used, or the
        /// same string prefixed with the relay domain otherwise.</returns>
        public string BuildRequestUri(string path)
        {
            Contract.Requires(path != null);
            Contract.Requires(path.Length > 0);
            Contract.Requires(path[0] == '/');
            return (relayDomain == null) ? path : "/" + relayDomain + path;
        }

        /// <summary>
        /// This method takes a path and constructs an absolute URI out of
        /// it. If the request was relayed, this is taken into account.
        /// For example, BuildAbsoluteRequestUri("hello.html") may return
        /// http://try.yaler.net/gsiot-FFMQ-TTD5/hello.html if the request
        /// pattern was "GET /hello*".
        /// You should use this method if your response contains absolute
        /// hyperlinks to your server.
        /// 
        /// Preconditions
        ///     path != null
        ///     path.Length > 0
        ///     path[0] == '/'
        /// </summary>
        /// <param name="path">Relative path starting with a /.</param>
        /// <returns>Absolute URI, containing relay domain if a relay
        /// is used.</returns>
        public string BuildAbsoluteRequestUri(string path)
        {
            Contract.Requires(path != null);
            Contract.Requires(path.Length > 0);
            Contract.Requires(path[0] == '/');
            return serviceRoot + BuildRequestUri(path);
        }

        // response interface

        int statusCode = 200;       // OK
        string responseContentType = "text/plain";
        int responseMaxAge = -1;    // no cache-control header

        /// <summary>
        /// This property can be set to indicate the status code of the
        /// response. The most important status codes for our purposes are:
        ///     200 (OK)
        ///     400 (Bad Request)
        ///     404 (Not Found)
        ///     405 (Method Not Allowed)
        /// </summary>
        public int ResponseStatusCode
        {
            get { return statusCode; }

            // -1 means "undefined value"
            set
            {
                Contract.Requires((value >= 100) || (value == -1));
                Contract.Requires(value < 600);
                statusCode = value;
            }
        }

        /// <summary>
        /// This property can be set to indicate the content type of the
        /// response. This so-called MIME type will become the value of
        /// the HTTP Content-Type header. The most important content
        /// types for our purposes are:
        /// • text/plain
        /// Used for a plain-text response such as a single numeric or
        /// text value.
        /// • text/csv
        /// Used to send a series of values.
        /// • text/html
        /// Used to send a response with formatted HTML.
        /// </summary>
        public string ResponseContentType
        {
            get { return responseContentType; }

            set
            {
                Contract.Requires(value != null);
                Contract.Requires(value.Length > 0);
                responseContentType = value;
            }
        }

        /// <summary>
        /// This property can be set to indicate the time that a
        /// resource remains valid, in seconds.
        /// </summary>
        public int ResponseMaxAge
        {
            get { return responseMaxAge; }

            set
            {
                Contract.Requires(value >= -1);
                responseMaxAge = value;
            }
        }

        /// <summary>
        /// This property can be set with the content of the response
        /// message (message body). It will be encoded in UTF8.
        /// </summary>
        public string ResponseString { get; set; }

        /// <summary>
        /// This property can be set with the content of the response
        /// message (message body).
        /// </summary>
        public byte[] ResponseByteArray { get; set; }

        internal int ResponseContentLength = -1;

        /// <summary>
        /// This method takes a string and sets up the response message
        /// body accordingly. Parameter textType indicates the content
        /// type, e.g., text/plain, text/html, etc. This method sets the
        /// response status code to 200 (OK).
        /// This method is provided for convenience so that status code,
        /// content, and content type need not be set separately.
        /// 
        /// Preconditions
        ///     content != null
        ///     textType != null
        ///     textType.Length > 0
        /// </summary>
        /// <param name="content">HTTP message body as a string, which
        /// will be encoded as UTF-8.</param>
        /// <param name="textType">A MIME type that consists of text.
        /// </param>
        public void SetResponse(string content, string textType)
        {
            Contract.Requires(content != null);
            Contract.Requires(textType != null);
            Contract.Requires(textType.Length > 0);
            ResponseStatusCode = 200;    // OK
            ResponseContentType = textType;
            ResponseString = content;
        }

        /// <summary>
        /// This method takes a byte array and sets up the response message
        /// body accordingly. Parameter contentType indicates the content
        /// type, e.g., application/octet-stream. This method sets the
        /// response status code to 200 (OK).
        /// This method is provided for convenience so that status code,
        /// content, and content type need not be set separately.
        /// 
        /// Preconditions
        ///     content != null
        ///     length >= 0
        ///     length lessOrEqual content.Length
        ///     contentType != null
        ///     contentType.Length > 0
        /// </summary>
        /// <param name="content"></param>
        /// <param name="length"></param>
        /// <param name="contentType"></param>
        public void SetResponse(byte[] content, int length, string contentType)
        {
            Contract.Requires(content != null);
            Contract.Requires(length >= 0);
            Contract.Requires(length <= content.Length);
            Contract.Requires(contentType != null);
            Contract.Requires(contentType.Length > 0);
            ResponseStatusCode = 200;    // OK
            ResponseContentType = contentType;
            ResponseByteArray = content;
            ResponseContentLength = length;
        }
    }
}
