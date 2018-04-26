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
// The GetHandler and PutHandler delegate types describe the interface
// between sensors/actuators on the one hand, and resource objects on
// the other hand. Any object that provides a method compatible with
// GetHandler can be used as a sensor, and object that provides a
// method compatible with PutHandler can be used as an actuator.

using Oberon.HttpServers;

namespace Oberon.HttpResources.Netmf
{
    /// <summary>
    /// Converts an object to an HTTP response message body.
    /// content may be null.
    /// Preconditions
    ///     context != null
    /// 
    /// Postconditions
    ///     "context.ResponseContent contains serialized representation of
    ///      content object"
    ///     "context.ResponseContentType contains the MIME type of the
    ///      representation"
    /// </summary>
    public delegate void Serializer(RequestHandlerContext context,
        object content);

    /// <summary>
    /// Converts an HTTP request message body to an object.
    /// content may be null.
    /// The return value indicates whether conversion was successful.
    /// Preconditions
    ///     context != null
    /// Postconditions
    ///     Result =>
    ///         content != null
    ///         "content contains deserialized representation of
    ///          context.RequestContent"
    ///     !Result =>
    ///         content == null
    ///         "context.RequestContent could not be converted to a
    ///          C# object"
    /// </summary>
    public delegate bool Deserializer(RequestHandlerContext context,
        out object content);

    /// <summary>
    /// Returns a new sample.
    /// The result may be null.
    /// The result type(s) depend on the object.
    /// </summary>
    public delegate object GetHandler();

    /// <summary>
    /// Accepts a new setpoint.
    /// o may be null.
    /// The acceptable type(s) of o depend on the object.
    /// </summary>
    public delegate void PutHandler(object o);
}
