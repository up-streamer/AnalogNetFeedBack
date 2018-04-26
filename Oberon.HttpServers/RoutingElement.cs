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
// Requests are bundled in collections that define how incoming
// requests are routed to suitable request handlers.

using System.Collections;
using System.Diagnostics.Contracts;

namespace Oberon.HttpServers
{
    // RequestRouting

    /// <summary>
    /// One element of a request routing specification.
    /// </summary>
    public sealed class RoutingElement
    {
        internal RoutingElement next;
        internal string Method;
        internal string Path;
        internal bool Wildcard;
        internal RequestHandler Handler;

        internal RoutingElement(string method, string path, bool wildcard,
            RequestHandler handler)
        {
            Contract.Requires(method != null);
            Contract.Requires(method.Length >= 3);
            Contract.Requires(path != null);
            Contract.Requires(path.Length > 0);
            Contract.Requires(path[0] == '/');
            Contract.Requires(handler != null);
            Method = method;
            Path = path;
            Wildcard = wildcard;
            Handler = handler;
        }
    }

    /// <summary>
    /// An instance of class RequestRouting is automatically created as a
    /// property when a new HttpServer object is created. Because it
    /// implements the IEnumerable interface and provides an Add method,
    /// it supports C# collection initializers. This means that instead
    /// of explicitly calling the Add method with the parameters pattern
    /// and handler, an initializer with pattern and handler as elements
    /// can be used.
    /// </summary>
    public class RequestRouting : IEnumerable
    {
        RoutingElement first;

        class Enumerator : IEnumerator
        {
            readonly RoutingElement first;
            RoutingElement current;

            internal Enumerator(RoutingElement first)
            {
                this.first = first;
            }

            object IEnumerator.Current
            {
                get
                {
                    return current;
                }
            }

            bool IEnumerator.MoveNext()
            {
                if (current == null)
                {
                    current = first;
                }
                else
                {
                    current = current.next;
                }
                return (current != null);
            }

            void IEnumerator.Reset()
            {
                current = null;
            }
        }

        public IEnumerator GetEnumerator()
        {
            return new Enumerator(first);
        }

        /// <summary>
        /// This method adds a new request routing element to the
        /// collection, consisting of a request pattern and a request
        /// handler.
        /// 
        /// Preconditions
        ///     pattern != null
        ///     pattern.Length >= 3
        ///     handler != null
        /// </summary>
        /// <param name="pattern">Request pattern (method, path).</param>
        /// <param name="handler">Request handler.</param>
        public void Add(string pattern, RequestHandler handler)
        {
            Contract.Requires(pattern != null);
            Contract.Requires(pattern.Length >= 3);
            Contract.Requires(handler != null);
            var e = Parse(pattern, handler);
            Contract.Requires(e != null);
            if (first == null)
            {
                first = e;
            }
            else
            {
                RoutingElement h = first;
                while (h.next != null) { h = h.next; }
                h.next = e;
            }
        }

        /// <summary>
        /// This method adds a new request routing element to the
        /// collection, consisting of a request pattern and a request
        /// handler.
        /// 
        /// Preconditions
        ///     method != null
        ///     method.Length >= 3
        ///     path != null
        ///     path.Length > 0
        ///     path[0] == '/'
        ///     handler != null
        /// </summary>
        /// <param name="method">Method of the request pattern.</param>
        /// <param name="path">Path of the request pattern.</param>
        /// <param name="wildcard">If true, the pattern accepts all URIs starting with the given pattern.</param>
        /// <param name="handler">Request handler.</param>
        /// <returns></returns>
        public RoutingElement Add(string method, string path, bool wildcard, RequestHandler handler)
        {
            Contract.Requires(method != null);
            Contract.Requires(method.Length >= 3);
            Contract.Requires(path != null);
            Contract.Requires(path.Length > 0);
            Contract.Requires(path[0] == '/');
            Contract.Requires(handler != null);
            var e = new RoutingElement(method, path, wildcard, handler);
            if (first == null)
            {
                first = e;
            }
            else
            {
                RoutingElement h = first;
                while (h.next != null) { h = h.next; }
                h.next = e;
            }
            return e;
        }

        /// <summary>
        /// Removes a routing element.
        /// 
        /// Preconditions
        ///   e != null
        ///   "e is in list of routing elements"
        /// </summary>
        /// <param name="e">Routing element to be removed.</param>
        public void Remove(RoutingElement e)
        {
            Contract.Requires(e != null);
            Contract.Requires(first != null);
            if (e == first)
            {
                first = e.next;
            }
            else
            {
                RoutingElement h = first;
                while (h.next != e) { h = h.next; Contract.Requires(h != null); }
                h.next = e.next;
            }
        }

        RoutingElement Parse(string p, RequestHandler handler)
        {
            string method;
            string path;
            bool wildcard;
            string[] s = p.Split(' ');
            if ((s.Length != 2) ||
                (s[0].Length <= 0) ||
                (s[1].Length <= 0))
            {
                return null;
            }
            method = s[0];
            int index = s[1].IndexOf('*');
            if (index == -1)
            {
                wildcard = false;
                path = s[1];
            }
            else if (index != s[1].Length - 1)
            {
                return null;
            }
            else
            {
                wildcard = true;
                path = s[1].Substring(0, index);
            }
            return new RoutingElement(method, path, wildcard, handler);
        }
    }
}
