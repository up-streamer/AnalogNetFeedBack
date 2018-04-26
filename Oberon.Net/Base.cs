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

using System;
using System.IO;

namespace Oberon.Net
{
    /// <summary>
    /// Interface of a client-side stream factory.
    /// </summary>
    public interface IClientStreamProvider : IDisposable
    {
        /// <summary>
        /// Open connection to a server (remote host) at a given port.
        /// 
        /// Pre
        ///   remoteHostName != null
        ///   remoteHostName.Length > 0
        ///   remotePort >= 0
        ///   remotePort lessEqual 65535
        /// 
        /// Exceptions
        ///   TODO
        /// </summary>
        /// <param name="remoteHostName">Name of remote host to which connection is attempted.</param>
        /// <param name="remotePort">Port of remote host to which connection is attempted.</param>
        /// <returns>Open stream to remote host if successful, null otherwise.</returns>
        Stream Connect(string remoteHostName, int remotePort);
    }

    /// <summary>
    /// Interface of a server-side stream factory.
    /// </summary>
    public interface IServerStreamProvider : IDisposable
    {
        /// <summary>
        /// URL with protocol, host name, optionally port, optionally path prefix, e.g.:
        ///   http://oberon.ch
        ///   http://192.168.100.1:80
        ///   http://try.yaler.net/myRelayDomain
        ///   
        /// Post
        ///   LocalUrl != null
        ///   LocalUrl.Length >= 5
        /// </summary>
        string LocalUrl { get; }

        /// <summary>
        /// Accept incoming connection from a client.
        /// 
        /// Exceptions
        ///   TODO
        /// </summary>
        /// <returns>Open stream from client.</returns>
        Stream Accept();
    }
}
