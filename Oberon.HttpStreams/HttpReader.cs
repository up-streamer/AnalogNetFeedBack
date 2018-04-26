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
// HttpReader instances support the reading of HTTP messages,
// using the .NET Stream API.
//
// For reading a message an HttpReader instance is attached to a stream,
// the request/status line is read using the provided Read* methods,
// and then the message body (content) is read, if any. For reading
// the content, one or more calls to Read are performed. At the
// end, the reader is detached and can later be reused again.
//
// HttpReaders are designed to have a mostly static memory
// consumption, so that if there is sufficient memory to create them, it
// is likely that they will not cause an out-of-memory condition
// (heap full) later on.
//
// HttpReaders do not perform any exception catching; stream exceptions
// are thus passed on to the clients.

using System;
using System.Diagnostics.Contracts;
using System.IO;

namespace Oberon.HttpStreams
{
    /// <summary>
    /// Light-weight object for reading HTTP messages from streams.
    /// An HttpWriter instance does not allocate any memory after
    /// it has been created, unless an exception occurs.
    /// </summary>
    public sealed class HttpReader : Stream
    {
        Stream connection;  // not null between Attach and Detach

        // Data is received byte by byte, up to the message content which
        // is received in larger chunk(s).
        readonly byte[] buffer = new byte[1];               // where (buffer != null)

        // Symbols are copied from byte buffer to a symbol buffer.
        // Received characters that don't fit into a symbol due to the
        // symbol buffer's size limit do not go through the symbol buffer,
        // i.e., they are truncated.
        const int symbolBufferCapacity = 128;
        readonly char[] symbolBuffer = new char[symbolBufferCapacity];
        // where (symbolBuffer != null)
        int symbolLength;                       // where (symbolLength >= 0)
        // where (symbolLength <= symbolBufferCapacity)

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// Optional property with the Receive timeout in milliseconds.
        /// Default: 30000 milliseconds (30 seconds)
        /// If the value -1 is set, no timeout occurs (wait forever).
        /// The value 0 indicates the default.
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Current status of the reader.
        /// </summary>
        public HttpStreamStatus Status { get; internal set; }

        /// <summary>
        /// Attaches an unattached reader to a stream.
        /// If the reader was previously used and an exception occurred,
        /// it can be used again anyway, because it will be completely re-
        /// initialized.
        /// Preconditions
        ///     connection != null
        ///     Timeout >= -1
        /// Postconditions
        ///     "reader is attached"
        /// </summary>
        /// <param name="connection">Open stream</param>
        public void Attach(Stream connection)
        {
            Contract.Requires(connection != null);
            Contract.Requires(Timeout >= 0);
            if (Timeout == 0) { Timeout = 30000; }    // 30 seconds default
            this.connection = connection;
            if (connection.CanTimeout)
            {
                connection.ReadTimeout = Timeout;
            }
            symbolLength = 0;
            Status = HttpStreamStatus.BeforeContent;
        }

        // Receive one character from stream.
        // Postconditions
        //     (Status != HttpStatus.BeforeContent) => (Result == (char)0)
        //     (symbolLength > symbolBufferCapacity) => "overflow"
        char ReadChar()
        {
            Contract.Requires(Status == HttpStreamStatus.BeforeContent);
            // blocking receive operation
            int read =
                connection.Read(buffer, 0, 1);
            if (read == 0)  // connection closed by other endpoint
            {
                Status = HttpStreamStatus.ConnectionError;
                return (char)0;
            }
            var c = (char)buffer[0];

            if (symbolLength < symbolBufferCapacity)
            {
                symbolBuffer[symbolLength] = c;
            }
            symbolLength = symbolLength + 1;

            return c;
        }

        /// <summary>
        /// Read a blank-terminated string from the stream.
        /// Postconditions
        ///     (s == null) <=> (Status > HttpReadStatus.InContent)
        /// If the reader is in an error state, this method does nothing.
        /// </summary>
        /// <param name="s">String that has been read, without the
        /// terminating blank character</param>
        public void ReadStringToBlank(out string s)
        {
            s = null;
            if (Status <= HttpStreamStatus.InContent)    // no error
            {
                Contract.Requires(Status == HttpStreamStatus.BeforeContent);
                symbolLength = 0;
                char c = ReadChar();
                while ((Status == HttpStreamStatus.BeforeContent) &&
                       (c != ' ') && (c != '\r'))
                {
                    c = ReadChar();
                }
                if (Status == HttpStreamStatus.BeforeContent)
                {
                    if (c == ' ')           // expected string terminator
                    {
                        if (symbolLength == 1)  // empty string not allowed
                        {
                            Status = HttpStreamStatus.SyntaxError;
                        }
                        else if (symbolLength > symbolBufferCapacity)
                        // symbol too long
                        {
                            Status = HttpStreamStatus.RequestUriTooLong;
                        }
                        else
                        {
                            Contract.Assert(symbolLength > 1);
                            s = new string(symbolBuffer, 0,
                                            symbolLength - 1);
                        }
                    }
                    else
                    {
                        Status = HttpStreamStatus.SyntaxError;
                    }
                }
            }
        }

        /// <summary>
        /// Read a colon-terminated string from the stream.
        /// The transition to reading the message content is signaled by
        /// returning the null string and by
        ///     (Status == HttpReadStatus.InContent).
        /// Postconditions
        ///     (s == null) => (Status > HttpReadStatus.InContent)
        /// If the reader is in an error state, this method does nothing.
        /// </summary>
        /// <param name="s">String that has been read, without the
        /// terminating colon</param>
        public void ReadFieldName(out string s)
        {
            s = null;
            if (Status <= HttpStreamStatus.InContent)    // no error
            {
                Contract.Requires(Status == HttpStreamStatus.BeforeContent);
                symbolLength = 0;
                char c = ReadChar();
                while ((Status == HttpStreamStatus.BeforeContent) &&
                       (c != ':') && (c != '\r'))
                {
                    c = ReadChar();
                }
                if (Status == HttpStreamStatus.BeforeContent)
                {
                    if (c == ':')           // expected string terminator
                    {
                        if (symbolLength == 1)  // empty symbol not allowed
                        {
                            Status = HttpStreamStatus.SyntaxError;
                        }
                        else if (symbolLength > symbolBufferCapacity)
                        {   // symbol too long
                            Status = HttpStreamStatus.SyntaxError;
                        }
                        else
                        {
                            Contract.Assert(symbolLength > 1);
                            s = new string(symbolBuffer, 0,
                                symbolLength - 1);
                        }
                    }
                    else    // not a string string, but end of headers
                    {
                        c = ReadChar();
                        if (c == '\n')
                        {
                            Status = HttpStreamStatus.InContent;
                        }
                        else
                        {
                            Status = HttpStreamStatus.SyntaxError;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Read a CR-LF-terminated string from the stream.
        /// Postconditions
        ///     (s == null) <=> (Status > HttpReadStatus.InContent)
        /// If the reader is in an error state, this method does nothing.
        /// </summary>
        /// <param name="s">String that has been read, without the
        /// terminating carriage return and newline characters</param>
        public void ReadFieldValue(out string s)
        {
            s = null;
            if (Status <= HttpStreamStatus.InContent)    // no error
            {
                Contract.Requires(Status == HttpStreamStatus.BeforeContent);
                symbolLength = 0;
                char c = ReadChar();
                // skip optional white space
                while ((Status == HttpStreamStatus.BeforeContent) && (c == ' '))
                {
                    symbolLength = 0;
                    c = ReadChar();
                }
                while ((Status == HttpStreamStatus.BeforeContent) && (c != '\r'))
                {
                    c = ReadChar();
                }
                if (Status == HttpStreamStatus.BeforeContent)
                {
                    c = ReadChar();
                    if (c == '\n')
                    {
                        if (symbolLength > symbolBufferCapacity)
                        {
                            // symbol too long is NOT considered as an
                            // error in a field value, the header will
                            // simply be skipped when the value is null
                        }
                        else
                        {
                            Contract.Assert(symbolLength >= 2);
                            s = new string(symbolBuffer, 0,
                                            symbolLength - 2);
                        }
                    }
                    else
                    {
                        Status = HttpStreamStatus.SyntaxError;
                    }
                }
            }
        }

        /// <summary>
        /// Reads a number of bytes from the stream, as part of the message
        /// content. It cannot be used for reading request lines, status
        /// lines, or headers.
        /// If the reader is in an error state, this method does nothing.
        /// This method may be called multiple times, to read
        /// content incrementally.
        /// If the caller has received the Content-Length header, it is
        /// responsible to read exactly this number of bytes in total.
        /// Preconditions
        ///     buffer != null
        ///     offset >= 0
        ///     count >= 0
        ///     offset + count lessEqual buffer.Length
        ///     "reader is attached"
        ///     (Status == HttpReadStatus.InContent) || "error"
        /// Postconditions
        ///     if "reader was in error state when method was called"
        ///         Result == -1
        ///         Status > HttpReadStatus.InContent
        ///     if "timeout occurred"
        ///         Result == -1
        ///         Status == HttpReadStatus.TimeoutError
        ///         IOException with message == "Timeout" was thrown
        ///     if "orderly shutdown of remote endpoint"
        ///         Result == -1
        ///         Status == HttpReadStatus.ConnectionError
        ///     if "no error occurred"
        ///         Result >= 0
        /// </summary>
        /// <param name="buffer">Some bytes from this buffer will be
        /// read</param>
        /// <param name="offset">Index of first byte to be read</param>
        /// <param name="count">Number of bytes to be read</param>
        /// <returns>Number of bytes actually read,
        /// or -1 for error</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            Contract.Requires(buffer != null);
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(offset + count <= buffer.Length);
            Contract.Requires(connection != null);
            if (Status <= HttpStreamStatus.InContent)    // no error
            {
                Contract.Requires(Status == HttpStreamStatus.InContent);
                if (count == 0)
                {
                    return 0;
                }
                else
                {
                    // count > 0
                    int received = connection.Read(buffer, offset, count);
                    if (received == 0)  // shutdown of remote endpoint
                    {
                        Status = HttpStreamStatus.ConnectionError;
                        return -1;
                    }
                    else
                    {
                        Contract.Assert(received > 0);
                        return received;
                    }
                }
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Detaches an attached reader from a stream.
        /// It is NOT checked whether the correct number of content
        /// bytes were read! Even if the bytes are not processed,
        /// they MUST be read (consumed), otherwise they will be
        /// misinterpreted as part of the next request!
        /// Preconditions
        ///     "reader is attached"
        ///     Status == HttpReadStatus.InContent
        /// Postconditions
        ///     "reader is not attached"
        /// </summary>
        public void Detach()
        {
            Contract.Requires(connection != null);
            connection = null;
            // Don't change the Status property, it may still be
            // needed by the caller for error analysis.
        }

        // not implemented

        public override void Flush() { }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
