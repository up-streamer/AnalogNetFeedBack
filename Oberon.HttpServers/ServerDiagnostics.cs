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
// Internal facility. Not further documented.

using System;

namespace Oberon.HttpServers
{
    public class ServerDiagnostics
    {
        public DateTime StartTime { get; set; }
        public int AcceptErrors { get; set; }               // accept errors (e.g., timeouts)
        public int AcceptFailures { get; set; }             // accept failures (unknown exceptions)
        public int RequestsTotal { get; set; }              // accepted requests total
        public int RequestHandlerErrors { get; set; }       // request handler errors (e.g., timeouts)
        public int RequestHandlerFailures { get; set; }     // request handler failures (unknown exceptions)
    }
}
