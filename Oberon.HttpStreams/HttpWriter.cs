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
// HttpWriter instances support the writing of HTTP messages,
// using the .NET Stream API.
//
// For writing a message an HttpWriter instance is attached to a stream,
// the request/status line is written using the provided Write* methods,
// and then the message body (content) is written, if any. For writing
// the content, one or more calls to Write are performed. At the
// end, the writer is detached and can later be reused again.
//
// HttpWriters are designed to have a mostly static memory
// consumption, so that if there is sufficient memory to create them, it
// is likely that they will not cause an out-of-memory condition
// (heap full) later on.
//
// HttpWriters do not perform any exception catching; stream exceptions
// are thus passed on to the clients.

using System;
using System.Diagnostics.Contracts;
using System.IO;

namespace Oberon.HttpStreams
{
    /// <summary>
    /// Light-weight object for writing HTTP messages to streams.
    /// An HttpWriter instance does not allocate any memory after
    /// it has been created, unless an exception occurs.
    /// </summary>
    public sealed class HttpWriter : Stream
    {
        Stream connection;  // not null between Attach and Detach

        // Data to be written is buffered first. When the buffer is full,
        // it is flushed and cleared.
        const int bufferLength = 100;
        const int maxWriteBufferSize = 1024;
        readonly byte[] buffer = new byte[bufferLength]; // where (buffer != null)
        int bufferPos;                          // where (bufferPos >= 0)
        // where (bufferPos <= bufferLength)

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        /// <summary>
        /// Optional property with the Send timeout in milliseconds.
        /// Default: 5000 milliseconds (5 seconds)
        /// If the value -1 is set, no timeout occurs (wait forever).
        /// The value 0 indicates the default.
        /// If a value between 1 and 499 is set, the stream API will
        /// use 500 milliseconds as its minimum timeout value.
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Current status of the writer.
        /// </summary>
        public HttpStreamStatus Status { get; internal set; }

        /// <summary>
        /// Attaches a writer to a stream.
        /// If the writer was previously used and an exception occurred,
        /// it can be used again anyway, because it will be completely re-
        /// initialized.
        /// Preconditions
        ///     connection != null
        ///     Timeout >= -1
        /// Postconditions
        ///     "writer is attached"
        /// </summary>
        /// <param name="connection">Open stream</param>
        public void Attach(Stream connection)
        {
            // Attach even works after an exception, even if the object
            // state is inconsistent.
            Contract.Requires(connection != null);
            Contract.Requires(Timeout >= -1);
            this.connection = connection;
            if (Timeout == 0) { Timeout = 5000; }    // 5 seconds default
            if (connection.CanTimeout)
            {
                connection.WriteTimeout = Timeout;
            }
            Status = HttpStreamStatus.BeforeContent;
            bufferPos = 0;
        }

        public override void Flush()
        {
            Contract.Requires(connection != null);
            if (Status <= HttpStreamStatus.InContent)    // no error
            {
                Contract.Requires(Status == HttpStreamStatus.BeforeContent);
                var pos = 0;
                int toWrite = bufferPos;
                while (toWrite > maxWriteBufferSize)
                {
                    connection.Write(buffer, pos, maxWriteBufferSize);
                    pos = pos + maxWriteBufferSize;
                    toWrite = toWrite - maxWriteBufferSize;
                }
                if (toWrite > 0)
                {
                    connection.Write(buffer, pos, toWrite);
                    connection.Flush();
                }
            }
            bufferPos = 0;
        }

        /// <summary>
        /// Writes a single ASCII character to the stream, as part of a
        /// HTTP request line, HTTP status line, or HTTP header.
        /// It *cannot* be used to write content, i.e., the HTTP message
        /// body. Use WriteContent for that purpose.
        /// If the writer is in an error state, this method does nothing.
        /// Preconditions
        ///     c >= (char)0
        ///     c lessEqual (char)127
        ///     "writer is attached"
        ///     (Status == HttpWriteStatus.BeforeContent) || "error"
        /// </summary>
        /// <param name="c">ASCII character to be written</param>
        public void WriteChar(char c)
        {
            var b = (int)c;
            Contract.Requires((b >= 0) && (b <= 127));
            Contract.Requires(connection != null);
            if (Status <= HttpStreamStatus.InContent)    // no error
            {
                Contract.Requires(Status == HttpStreamStatus.BeforeContent);
                // bufferPos >= 0
                // bufferPos <= bufferLength
                if (bufferPos == bufferLength)
                {
                    Flush();
                    // bufferPos == 0
                }
                // bufferPos >= 0
                // bufferPos < bufferLength
                buffer[bufferPos] = (byte)b;
                bufferPos = bufferPos + 1;
                // bufferPos > 0
                // bufferPos <= bufferLength
            }
        }

        /// <summary>
        /// Writes an ASCII string to the stream, as part of a
        /// HTTP request line, HTTP status line, or HTTP header.
        /// It *cannot* be used to write content, i.e., the HTTP message
        /// body. Use WriteContent for that purpose.
        /// If the writer is in an error state, this method does nothing.
        /// Preconditions
        ///     s != null
        ///     "writer is attached"
        ///     (Status == HttpWriteStatus.BeforeContent) || "error"
        /// </summary>
        /// <param name="s">ASCII string to be written</param>
        public void WriteString(string s)
        {
            Contract.Requires(s != null);
            Contract.Requires(connection != null);
            if (Status <= HttpStreamStatus.InContent)    // no error
            {
                Contract.Requires(Status == HttpStreamStatus.BeforeContent);
                var i = 0;
                int length = s.Length;
                while (i != length)
                {
                    WriteChar(s[i]);
                    i = i + 1;
                }
            }
        }

        /// <summary>
        /// Writes an ASCII string to the stream, as part of a
        /// HTTP request line, HTTP status line, or HTTP header.
        /// After the string, a carriage return and a newline character
        /// are written to the stream as well.
        /// It *cannot* be used to write content, i.e., the HTTP message
        /// body. Use WriteContent for that purpose.
        /// If the writer is in an error state, this method does nothing.
        /// Preconditions
        ///     s != null
        ///     "writer is attached"
        ///     (Status == HttpWriteStatus.BeforeContent) || "error"
        /// </summary>
        /// <param name="s">ASCII string to be written</param>
        public void WriteLine(string s)
        {
            Contract.Requires(s != null);
            Contract.Requires(connection != null);
            if (Status <= HttpStreamStatus.InContent)    // no error
            {
                Contract.Requires(Status == HttpStreamStatus.BeforeContent);
                var i = 0;
                int length = s.Length;
                while (i != length)
                {
                    WriteChar(s[i]);
                    i = i + 1;
                }
                WriteChar('\r');
                WriteChar('\n');
            }
        }

        /// <summary>
        /// Writes a carriage return and a newline character to the
        /// stream. The two most recently written bytes must also
        /// have been a carriage return and a newline character.
        /// In the HTTP protocol, the sequence CR NL CR NL indicates
        /// the transition to the message body, i.e., the bytes
        /// immediately following these four bytes - if any - form the
        /// message content.
        /// This method must be cause after the last header is written
        /// and before the content is written!
        /// Preconditions
        ///     "writer is attached"
        ///     Status == HttpWriteStatus.BeforeContent
        /// Postconditions
        ///     Status == HttpWriteStatus.InContent
        /// </summary>
        public void WriteBeginOfContent()
        {
            Contract.Requires(connection != null);
            Contract.Requires(Status == HttpStreamStatus.BeforeContent);
            WriteChar('\r');
            WriteChar('\n');
            Flush();
            if (Status == HttpStreamStatus.BeforeContent)
            {
                Status = HttpStreamStatus.InContent;
            }
        }

        /// <summary>
        /// Writes a number of bytes to the stream, as part of the message
        /// content. It cannot be used for writing request lines, status
        /// lines, or headers.
        /// If the writer is in an error state, this method does nothing.
        /// This method may be called multiple times, to write
        /// content incrementally.
        /// If the caller has sent the Content-Length header, it is
        /// responsible to write exactly this number of bytes in total.
        /// Preconditions
        ///     buffer != null
        ///     offset >= 0
        ///     count >= 0
        ///     offset + count lessEqual buffer.Length
        ///     "writer is attached"
        ///     (Status == HttpWriteStatus.InContent) || "error"
        /// </summary>
        /// <param name="buffer">Some bytes from this buffer will be
        /// written</param>
        /// <param name="offset">Index of first byte to be written</param>
        /// <param name="count">Number of bytes to be written</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            Contract.Requires(buffer != null);
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(offset + count <= buffer.Length);
            Contract.Requires(connection != null);
            if (Status <= HttpStreamStatus.InContent)    // no error
            {
                Contract.Requires(Status == HttpStreamStatus.InContent);
                var pos = offset;
                int toWrite = count;
                while (toWrite > maxWriteBufferSize)
                {
                    connection.Write(buffer, pos, maxWriteBufferSize);
                    pos = pos + maxWriteBufferSize;
                    toWrite = toWrite - maxWriteBufferSize;
                }
                if (toWrite > 0)
                {
                    connection.Write(buffer, pos, toWrite);
                }
            }
        }

        /// <summary>
        /// Detaches an attached writer from a stream.
        /// Preconditions
        ///     "writer is attached"
        /// Postconditions
        ///     "writer is not attached"
        /// </summary>
        public void Detach()
        {
            Contract.Requires(connection != null);
            connection = null;
            // Don't change the Status property, it may still be
            // needed by the caller for error analysis.
        }

        // not implemented

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

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
