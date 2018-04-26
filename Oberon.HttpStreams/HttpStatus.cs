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

namespace Oberon.HttpStreams
{
    /// <summary>
    /// Possible states of a reader or writer instance.
    /// BeforeContent
    ///     Currently writing/reading request line, status line, or header.
    /// InContent
    ///     Currently writing/reading the message content.
    /// TimeoutError
    ///     Nothing was sent/received until the Timeout has passed.
    /// ConnectionError
    ///     The connection was lost during writing/reading.
    /// SyntaxError
    ///     Unexpected read received as part of a request line, status
    ///     line, or header.
    /// </summary>
    public enum HttpStreamStatus
    {
        BeforeContent, InContent,       // reader & writer "no error" codes
        ConnectionError,                // reader & writer error codes
        SyntaxError,                    // reader-specific error code
        RequestUriTooLong               // reader-specific error code
    }
}
