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
// We use the TRACE constant, which is set to true in Debug mode
// and to false in RELEASE mode in this project.
//
// See Microsoft's API documentation for details:
// http://msdn.microsoft.com/en-us/library/system.diagnostics.trace(v=vs.110).aspx

using Microsoft.SPOT;

namespace System.Diagnostics
{
    /// <summary>
    /// Provides a set of methods that help you trace the execution
    /// of your code. This class cannot be inherited.
    /// </summary>
    public sealed class Trace
    {
        static int count = 0;

        /// <summary>
        /// Writes an informational message to the debug output channel using the specified message.
        /// </summary>
        /// <param name="message">The informative message to write.</param>
        [System.Diagnostics.ConditionalAttribute("TRACE")]
        public static void TraceInformation(string message)
        {
            Debug.Print(count.ToString("d5") + " INFO\t" + message);
            count++;
        }

        /// <summary>
        /// Writes a warning message to the debug output channel using the specified message.
        /// </summary>
        /// <param name="message">The informative message to write.</param>
        [System.Diagnostics.ConditionalAttribute("TRACE")]
        public static void TraceWarning(string message)
        {
            Debug.Print(count.ToString("d5") + " WARN\t" + message);
            count++;
        }

        /// <summary>
        /// Writes an error message to the debug output channel using the specified message.
        /// </summary>
        /// <param name="message">The informative message to write.</param>
        [System.Diagnostics.ConditionalAttribute("TRACE")]
        public static void TraceError(string message)
        {
            Debug.Print(count.ToString("d5") + " ERROR\t" + message);
            count++;
        }

        /// <summary>
        /// Emits the specified error message to the debug output channel.
        /// </summary>
        /// <param name="message">A message to emit.</param>
        public static void Fail(string message)
        {
            // This is not compiled conditionally, a failure should always be reported.
            Debug.Print(count.ToString("d5") + " FAIL\t" + message);
            count++;
        }
    }
}
