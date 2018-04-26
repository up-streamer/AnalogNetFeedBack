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
// This server implementation intentionally uses a single thread only, to
// make it simple, small, and with minimal memory overhead.
// If such a single-threaded server kept a connection to a client open,
// no requests from other clients could be handled. For this reason, the
// server explicitly closes the connection after handling a request, or
// after a timeout when no new data has arrived for a while.
// To explicitly close the connection after a request, the server sends
// the "Connection: close" header in its response.

using Oberon.HttpStreams;
using Oberon.Net;
using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading;

namespace Oberon.HttpServers
{
    /// <summary>
    /// The delegate type RequestHandler determines the parameter
    /// (context) and result (void) that a method must have so that it
    /// can be added to a request routing collection.
    /// 
    /// Preconditions
    ///     context != null
    /// </summary>
    /// <param name="context">Context with its request and server state
    /// set up, but without response state yet.</param>
    public delegate void RequestHandler(RequestHandlerContext context);

    // HTTP server

    /// <summary>
    /// An instance of class HttpServer represents a web service that
    /// handles HTTP requests at a particular port, or uses a relay server
    /// to make it accessible even without a public Internet address.
    /// </summary>
    public class HttpServer
    {
        /// <summary>
        /// Indicates whether server is already initialized.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Determines whether or not connection is always closed after
        /// sending the response to a request. Default is false.
        /// </summary>
        public bool KeepConnectionOpenAfterResponse { get; set; }

        /// <summary>
        /// Factory for producing streams to the remote host,
        /// by opening a server endpoint.
        /// </summary>
        public IServerStreamProvider StreamProvider { get; set; }

        /// <summary>
        /// Mandatory property. At least one request routing element
        /// should be added to this property to support at least one
        /// request URI.
        /// NEW
        /// When the server instance is created, an empty RequestRouting
        /// instance is created, so this property usually need not be
        /// set by the client.
        /// </summary>
        public RequestRouting RequestRouting { get; set; }

        /// <summary>
        /// Diagnostic information.
        /// </summary>
        public ServerDiagnostics Diagnostics { get; private set; }

        /// <summary>
        /// Determines whether exceptions in a RequestHandler or in
        /// a PostResponseHandler are caught or not. During debugging,
        /// you usually don't want exceptions to be caught.
        /// At run-time, you may want a different behavior.
        /// </summary>
        public bool CatchHandlerFailures { get; set; }

        string serviceRootPath; // e.g. "http://example.com"
                                // or "http://try.yaler.net/gsiot-FFMQ-TTD5"
        string relayDomain;     // null if no relay is used, otherwise not null, e.g.:
                                // "gsiot-FFMQ-TTD5".

        static void DebugPrint(string s)
        {
            //Microsoft.SPOT.Debug.Print(s);
        }
       
        /// <summary>
        /// A server that receives and interprets HTTP requests, calls
        /// your request handlers, and returns HTTP responses.
        /// Unless its RelayDomain and RelaySecretKey are set up before
        /// opening it, it does not use a relay service. This means it
        /// only works on your local network, or you need to use port
        /// forwarding or a similar mechanism to make your device visible
        /// on the Internet at large.
        /// </summary>
        public HttpServer()
        {
            IsOpen = false;
            KeepConnectionOpenAfterResponse = false;
            StreamProvider = null;
            RequestRouting = new RequestRouting();
            Diagnostics = null;
            serviceRootPath = null;
            relayDomain = null;
            CatchHandlerFailures = false;
            DebugPrint("HttpServer: server created");
        }

        /// <summary>
        /// This method completes the initialization of the server. If a
        /// relay is used, it performs the first registration of the
        /// device at the relay. Before it is called, the server
        /// properties must have been set up. Normally, you don’t need to
        /// call this method, since it is called by Run if necessary.
        /// </summary>
        public void Open()
        {
            Contract.Requires(!IsOpen);
            Contract.Requires(StreamProvider != null);
            Contract.Requires(RequestRouting != null);
            serviceRootPath = StreamProvider.LocalUrl;
            string[] elems = serviceRootPath.Split('/');
            int n = elems.Length;
            Contract.Requires((n == 3) || (n == 4));
            if (n == 3)
            {
                relayDomain = null;     // no relay server is used
            }
            else
            {
                relayDomain = elems[3];
            }

            Diagnostics = new ServerDiagnostics();
            Diagnostics.StartTime = DateTime.Now;

            IsOpen = true;
            DebugPrint("HttpServer: server opened");
            Trace.TraceInformation("Base Uri: " + serviceRootPath + "/");
        }

        public void Check()
        {
            Open();
        }

        public void Close()
        {
            IsOpen = false;
            if (StreamProvider != null)
            {
                StreamProvider.Dispose();
                StreamProvider = null;
            }
            DebugPrint("HttpServer: server closed");
        }

        /// <summary>
        /// Add a new request routing element while server is already running.
        /// 
        /// Precondition
        ///   pattern != null
        ///   pattern.Length >= 3
        ///   handler != null
        /// </summary>
        /// <param name="pattern">Request pattern.</param>
        /// <param name="handler">Request handler.</param>
        public void Add(string pattern, RequestHandler handler)
        {
            Contract.Requires(pattern != null);
            Contract.Requires(pattern.Length >= 3);
            Contract.Requires(handler != null);
            RequestRouting.Add(pattern, handler);
        }

        /// <summary>
        /// This method calls Open if it was not called already by the
        /// application, and then enters an endless loop where it
        /// repeatedly waits for incoming requests, accepts them, and
        /// performs the necessary processing for handling the request.
        /// </summary>
        public void Run()
        {
            if (!IsOpen) { Open(); }
            DebugPrint("HttpServer: running");
            while (IsOpen)      // IsOpen is never set to false, except in Close
            {
                Stream connection = null;
                do      // open connection, then handle one or more requests, until connection is closed by client or by request handler
                {
                    // wait for next request until this succeeds
                    while (connection == null)
                    {
                        try
                        {
                            connection = StreamProvider.Accept();
                            DebugPrint("connection opened");
                        }
                        catch (IOException e)
                        {
                            // e.g. DNS lookup or connect to relay server may fail sporadically
                            Trace.TraceError("HttpServer: stream error in Gsiot.Server.HttpServer.Run.Accept:\r\n" + e.Message);
                            Contract.Assert(connection == null);
                            Diagnostics.AcceptErrors = Diagnostics.AcceptErrors + 1;
                            Trace.TraceInformation("HttpServer: recovering from DNS lookup failure...");
                            Thread.Sleep(1000);
                        }
                        catch (Exception e)
                        {
                            Trace.TraceError("HttpServer: exception in Run.Accept (possibly network cable not plugged in, or problem with Yaler refresh):\r\n" + e);
                            Diagnostics.AcceptFailures = Diagnostics.AcceptFailures + 1;
                        }
                    }
                    // connection != null

                    // handle request
                    Diagnostics.RequestsTotal = Diagnostics.RequestsTotal + 1;
                    bool connectionClose = !KeepConnectionOpenAfterResponse;
                    try
                    {
                        ConsumeRequest(connection, serviceRootPath,
                            relayDomain, RequestRouting,
                            CatchHandlerFailures,
                            ref connectionClose);
                    }
                    catch (IOException e)       // only I/O exceptions are caught, no handler failures
                    {
                        // possibly device was disconnected, or host has sent no data (read timeout)
                        DebugPrint("HttpServer: stream error in Run.ConsumeRequest:\r\n" + e.Message);
                        Diagnostics.RequestHandlerErrors = Diagnostics.RequestHandlerErrors + 1;
                    }
                    if (connectionClose)
                    {
                        connection.Close();
                        connection = null;
                        DebugPrint("HttpServer: connection closed");
                    }
                } while (connection != null);
            }
            DebugPrint("HttpServer: server connection has been closed");
        }

        /// <summary>
        /// Private method that handles an incoming request.
        /// It sets up a RequestHandlerContext instance with the data from
        /// the incoming HTTP request, finds a suitable request handler to
        /// produce the response, and then sends the response as an HTTP
        /// response back to the client.
        /// Preconditions
        ///     "connection is open"
        ///     serviceRoot != null
        ///     serviceRoot.Length > 8
        ///     "serviceRoot starts with 'http://' and ends with '/'"
        ///     requestRouting != null
        /// </summary>
        /// <param name="connection">Open TCP/IP connection</param>
        /// <param name="serviceRoot">The absolute URI that is a prefix of
        /// all request URIs that this web service supports. It must start
        /// with "http://" and must end with "/".</param>
        /// <param name="relayDomain">Host name or Internet address of the
        /// relay to be used, or null if no relay is used</param>
        /// <param name="requestRouting">Collection of
        ///   { request pattern, request handler}
        /// pairs</param>
        /// <param name="connectionClose">Return parameter that indicates
        /// that the connection should be closed after this call. This may
        /// be because the incoming request has a "Connection: close"
        /// header, because the request handler has set the
        /// ConnectionClose property, or because some error occurred.
        /// </param>
        internal static void ConsumeRequest(Stream connection,
            string serviceRoot, string relayDomain,
            RequestRouting requestRouting,
            bool catchRequestFailures,
            ref bool connectionClose)
        {
            Contract.Requires(connection != null);
            Contract.Requires(serviceRoot != null);
            Contract.Requires(serviceRoot.Length > 8);
            Contract.Requires(serviceRoot.Substring(0, 7) == "http://");
            Contract.Requires(serviceRoot[serviceRoot.Length - 1] != '/');
            Contract.Requires(requestRouting != null);

            long startTime = DateTime.Now.Ticks;

            // initialization --------------------------------------------
            DebugPrint("HttpServer: ConsumeRequest - initialize");
            var reader = new HttpReader();
            var writer = new HttpWriter();
            var context = new RequestHandlerContext(serviceRoot,
                relayDomain, reader, writer);

            // receive request -------------------------------------------
            reader.Attach(connection);

            // read request line
            DebugPrint("HttpServer: ConsumeRequest - read request line");
            string httpMethod;
            string requestUri;
            string httpVersion;

            reader.ReadStringToBlank(out httpMethod);
            reader.ReadStringToBlank(out requestUri);
            if (reader.Status == HttpStreamStatus.RequestUriTooLong)
            {
                DebugPrint("HttpServer: ConsumeRequest - request URI too long!");
                context.ResponseStatusCode = 414;    // Request-URI Too Long
                context.ResponseContentType = "text/plain";
                context.ResponseString = "request URI too long";
                reader.Detach();
                connectionClose = true;
                return;
            }
            reader.ReadFieldValue(out httpVersion);
            if (reader.Status != HttpStreamStatus.BeforeContent)  // error
            {
                DebugPrint("HttpServer: ConsumeRequest - could not read HTTP version!");
                reader.Detach();
                connectionClose = true;
                return;
            }

            context.RequestMethod = httpMethod;
            context.RequestUri = requestUri;
            // ignore version

            // headers
            DebugPrint("HttpServer: ConsumeRequest - read headers");
            string fieldName;
            string fieldValue;
            int requestContentLength = -1;
            reader.ReadFieldName(out fieldName);
            while (reader.Status == HttpStreamStatus.BeforeContent)
            {
                reader.ReadFieldValue(out fieldValue);
                if (fieldValue != null)
                {
                    Contract.Assert(reader.Status ==
                                    HttpStreamStatus.BeforeContent);
                    if (fieldName == "Connection")
                    {
                        connectionClose =
                            (connectionClose ||
                            (fieldValue == "close") ||
                            (fieldValue == "Close"));
                    }
                    else if (fieldName == "Content-Type")
                    {
                        context.RequestContentType = fieldValue;
                        DebugPrint("HttpServer: ConsumeRequest - request Content-Type: " + fieldValue);
                    }
                    else if (fieldName == "Content-Length")
                    {
                        if (ServerUtilities.TryParseUInt32(fieldValue,
                            out requestContentLength))
                        {
                            // content length is now known
                            DebugPrint("HttpServer: ConsumeRequest - request Content-Length: " + requestContentLength);
                        }
                        else
                        {
                            DebugPrint("HttpServer: ConsumeRequest - request syntax error");
                            //reader.Status = HttpStatus.SyntaxError;
                            reader.Detach();
                            connectionClose = true;
                            return;
                        }
                    }
                }
                else
                {
                    // it's ok to skip header whose value is too long
                }
                Contract.Assert(reader.Status == HttpStreamStatus.BeforeContent);
                reader.ReadFieldName(out fieldName);
            }
            if (reader.Status != HttpStreamStatus.InContent)
            {
                reader.Detach();
                connectionClose = true;
                return;
            }

            // content
            DebugPrint("HttpServer: ConsumeRequest - read content");
            context.RequestByteArray = null;
            if (requestContentLength > 0)
            {
                // receive content
                context.RequestByteArray = new byte[requestContentLength];
                int toRead = requestContentLength;
                var read = 0;
                while ((toRead > 0) && (read >= 0))
                {
                    // already read: requestContentLength - toRead
                    read = reader.Read(context.RequestByteArray,
                        requestContentLength - toRead, toRead);
                    if (read < 0) { break; }    // timeout or shutdown
                    toRead = toRead - read;
                }
            }

            reader.Detach();
            if (reader.Status != HttpStreamStatus.InContent)
            {
                connectionClose = true;
                return;
            }

            // delegate request processing to a request handler ----------
            DebugPrint("HttpServer: ConsumeRequest - call request handler");
            var match = false;
            foreach (RoutingElement e in requestRouting)
            {
                if (context.RequestMatch(e))
                {
                    context.ConnectionClose = false;
                    context.ResponseStatusCode = -1;                                    // undefined
                    Contract.Requires(context.ResponseContentType != null);
                    Contract.Requires(context.ResponseContentType == "text/plain");     // default

                    if (catchRequestFailures)
                    {
                        try
                        {
                            DebugPrint("HttpServer: ConsumeRequest -   before protected call");
                            e.Handler(context);
                            DebugPrint("HttpServer: ConsumeRequest -   after protected call");
                        }
                        catch (Exception h)
                        {
                            DebugPrint("HttpServer: exception in request handler: " + h);
                        }
                    }
                    else
                    {
                        DebugPrint("HttpServer: ConsumeRequest -   before unprotected call");
                        e.Handler(context);
                        DebugPrint("HttpServer: ConsumeRequest -   after unprotected call");
                    }

                    Contract.Ensures(context.ResponseStatusCode >= 100);
                    Contract.Ensures(context.ResponseStatusCode < 600);
                    Contract.Ensures(context.ResponseContentType != null);
                    Contract.Ensures(context.ResponseContentType.Length > 0);
                    connectionClose = connectionClose || context.ConnectionClose;
                    match = true;
                    break;
                }
            }
            if (!match)
            {
                context.ResponseStatusCode = 404;    // Not Found
                context.ResponseContentType = "text/plain";
                context.ResponseString = "404 error - resource not found";
                DebugPrint("HttpServer: no matching request handler found");
            }
            Contract.Assert(context.ResponseContentType != null);

            // send response ---------------------------------------------
            DebugPrint("HttpServer: ConsumeRequest - send response");
            writer.Attach(connection);

            // status line
            DebugPrint("HttpServer: ConsumeRequest - send status line");
            writer.WriteString("HTTP/1.1 ");
            writer.WriteString(context.ResponseStatusCode.ToString());
            writer.WriteLine(" ");  // omit optional reason phrase

            // headers
            DebugPrint("HttpServer: ConsumeRequest - send headers");
            if (connectionClose)
            {
                writer.WriteLine("Connection: close");
            }
            if (context.ResponseMaxAge > 0)
            {
                writer.WriteLine("Cache-Control: max-age=" + context.ResponseMaxAge);
            }
            else if (context.ResponseMaxAge == 0)
            {
                writer.WriteLine("Cache-Control: no-cache");
            }
            writer.WriteString("Content-Type: ");
            writer.WriteLine(context.ResponseContentType);
            DebugPrint("HttpServer: ConsumeRequest - response Content-Type: " + context.ResponseContentType);

            if (context.ResponseString != null)
            {
                DebugPrint("HttpServer: ConsumeRequest - response content is text");
                context.ResponseByteArray =
                    Encoding.UTF8.GetBytes(context.ResponseString);
                Contract.Assert(context.ResponseByteArray != null);
            }
            if (context.ResponseByteArray != null)
            {
                DebugPrint("HttpServer: ConsumeRequest - content available as binary");
                if (context.ResponseContentLength == -1)
                {
                    context.ResponseContentLength = context.ResponseByteArray.Length;    // default is to take the entire buffer
                }
                writer.WriteString("Content-Length: ");
                writer.WriteLine(context.ResponseContentLength.ToString());
                DebugPrint("HttpServer: ConsumeRequest - response Content-Length: " + context.ResponseContentLength);
            }
            else
            {
                writer.WriteLine("Content-Length: 0");
                DebugPrint("HttpServer: ConsumeRequest - response Content-Length: 0");
            }

            // content
            DebugPrint("HttpServer: ConsumeRequest - send content");
            writer.WriteBeginOfContent();                                                   // this writes the CR LF separation between headers and content
            if (context.ResponseByteArray != null)    // send content
            {
                writer.Write(context.ResponseByteArray, 0, context.ResponseContentLength);
            }
            DebugPrint("HttpServer: response sent");

            writer.Detach();

            long duration = (DateTime.Now.Ticks - startTime) / 10000;
            Trace.TraceInformation(context.RequestMethod + " " +
                                   context.RequestUri + " -> " +
                                   context.ResponseStatusCode + " [" + duration + " ms]");
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////////

        void ConsumeRequestItem(object state)
        {
            Contract.Requires(state != null);
            Contract.Requires(state is Stream);
            var connection = (Stream)state;

            var i = 0;
            while (connection != null)
            {
                i = i + 1;

                // handle request
                Diagnostics.RequestsTotal = Diagnostics.RequestsTotal + 1;
                try
                {
                    bool connectionClose = !KeepConnectionOpenAfterResponse;
                    // ignore connectionClose, i.e., always
                    // close connection after a request
                    ConsumeRequest(connection, serviceRootPath,
                        relayDomain, RequestRouting,
                        CatchHandlerFailures,
                        ref connectionClose);
                    if (connectionClose)
                    {
                        connection.Close();
                        connection = null;
                    }
                }
                catch (IOException e)       // only I/O exceptions are caught, no handler failures
                {
                    // possibly device was disconnected, or host has sent no data (read timeout)
                    Trace.TraceError("HttpServer: stream error in ConsumeRequestItem.ConsumeRequest:\r\n" + e.Message);
                    Diagnostics.RequestHandlerErrors = Diagnostics.RequestHandlerErrors + 1;
                    connection.Close();
                    connection = null;
                }
            }
            DebugPrint("HttpServer: exit ConsumeRequestItem");
        }

        void ProduceRequestItems(object state)
        {
            if (!IsOpen) { Open(); }
            while (IsOpen)
            {
                // wait for next request
                Stream connection = null;
                try
                {
                    connection = StreamProvider.Accept();
                    Contract.Ensures(connection != null);
                }
                catch (IOException e)
                {
                    Contract.Assert(connection == null);
                    // possibly device was disconnected
                    Trace.Fail("stream error in ProduceRequestItems.Accept:\r\n" + e.Message);
                    Diagnostics.AcceptErrors = Diagnostics.AcceptErrors + 1;
                    Thread.Sleep(500);
                }
                catch (Exception e)
                {
                    Contract.Assert(connection == null);
                    Trace.Fail("exception in ProduceRequestItems.Accept:\r\n" + e);
                    Diagnostics.AcceptFailures = Diagnostics.AcceptFailures + 1;
                    Thread.Sleep(500);
                }
                if (connection != null)
                {
                    ThreadPool.QueueUserWorkItem(ConsumeRequestItem, connection);
                }
            }
            DebugPrint("ProduceRequestItems: closed");
        }

        public void Start(int nofThreads)
        {
            Trace.TraceInformation("HttpServer: start with " + nofThreads + " thread(s)");
            Contract.Requires(nofThreads > 0);
            if (nofThreads > 1)
            {
                ThreadPool.QueueUserWorkItem(ProduceRequestItems, null);
            }
            else
            {
                Start();
            }
        }

        Thread oneThread;

        public void Start()
        {
            oneThread = new Thread(Run);
            oneThread.Start();
        }
    }


    class ServerUtilities
    {
        internal static bool TryParseUInt32(string s, out int result)
        {
            Contract.Requires(s != null);
            result = 0;
            if (s.Length > 0)
            {
                var r = 0;
                foreach (char c in s)
                {
                    if ((c < '0') || (c > '9')) { return false; }
                    var n = (int)(c - '0');
                    r = (r * 10) + n;
                }
                result = r;
                return true;
            }
            return false;
        }
    }
}
