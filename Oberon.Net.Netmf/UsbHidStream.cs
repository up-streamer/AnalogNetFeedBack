//Copyright 2014 Oberon microsystems, Inc.
// NOT OPEN SOURCE!

using Microsoft.SPOT.Hardware;
using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;

namespace Oberon.Net.Netmf.UsbHidStreams
{
    internal class UsbHidStream : Stream
    {
        readonly byte[] readBuffer = new byte[UsbHidConstants.REPORT_LENGTH];
        readonly byte[] writeBuffer = new byte[UsbHidConstants.REPORT_LENGTH];
        // [0] is length, [1..63] is content
        int readOffset;
        int readCount;
        bool endOfReading;
        bool endOfWriting;
        Stream link;                // non-blocking (!) USB stream

        internal UsbHidStream(Stream s)
        {
            Contract.Requires(s != null);
            readOffset = 0;
            readCount = 0;
            endOfReading = false;
            endOfWriting = false;
            link = s;
        }

        public override bool CanRead { get { return !endOfReading; } }      // has stream been closed by client?
        public override bool CanWrite { get { return !endOfWriting; } }     // has stream been closed by device?

        internal void ReadIntoBuffer()
        {
            Contract.Requires(!endOfReading);
            int read = link.Read(readBuffer, 0, UsbHidConstants.REPORT_LENGTH);
            if (read <= 0)      // no data available, try again once after sleeping for a while
            {
                PowerState.Sleep(SleepLevel.Sleep, HardwareEvent.USBIn);
                // PowerState.Sleep wakes up about once every minute even if there
                // is no USB input. We use this behavior to implement a crude timeout.
                read = link.Read(readBuffer, 0, UsbHidConstants.REPORT_LENGTH);
            }
            if (read > 0)
            {
                Contract.Assert(read == UsbHidConstants.REPORT_LENGTH);
                // all data from the host comes in reports of 64 bytes length each
                readOffset = 1;
                readCount = readBuffer[0];
                Contract.Assert(readCount >= 0);
                Contract.Assert(readCount < UsbHidConstants.REPORT_LENGTH);
            }
            else
            {
                throw new IOException("Timeout");   // System.TimeoutException is missing in NETMF
            }
            Contract.Ensures(readCount >= 0);
            // readCount == 0:  last-report has been read (close-connection signal from host)
            // readCount > 0:   report with genuine data has been read
            // IOException: read timeout has expired
        }

        internal bool DataAvailable
        {
            get { return readCount > 0; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Contract.Requires(buffer != null);
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(offset + count <= buffer.Length);
            Contract.Requires(!endOfReading);

            var read = 0;
            if (count > 0)
            {
                if (readCount == 0)         // readBuffer is empty, read from USB stream
                {
                    ReadIntoBuffer();
                }
                Contract.Assert(readCount >= 0);
                Contract.Assert(readCount < UsbHidConstants.REPORT_LENGTH);
                if (readCount == UsbHidConstants.LAST_REPORT)   // == 0
                {
                    endOfReading = true;
                }
                else if (readCount > 0)          // data available
                {
                    read = (count <= readCount) ? count : readCount;      // MIN(count, readCount)
                    int n = read;
                    while (n != 0)
                    {
                        // Buffer.BlockCopy unfortunately not available in NETMF
                        buffer[offset] = readBuffer[readOffset];
                        offset = offset + 1;
                        readOffset = readOffset + 1;
                        n = n - 1;
                    }
                    readCount = readCount - read;
                }
            }
            Contract.Ensures(read >= 0);
            Contract.Ensures(read <= count);
            // read < Constants.REPORT_LENGTH is true but not part of the contract
            // read > 0 means "read bytes are available"
            // read == 0 means (count == 0) || "no more data will arrive from this stream"
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Contract.Requires(buffer != null);
            Contract.Requires(offset >= 0);
            Contract.Requires(count >= 0);
            Contract.Requires(offset + count <= buffer.Length);
            Contract.Requires(!endOfWriting);

            if (count > 0)
            {
                do      // send as many USB HID reports as necessary
                {
                    // send at most 63 content bytes per USB HID report:
                    int len = (count < UsbHidConstants.REPORT_LENGTH) ?
                                count : UsbHidConstants.REPORT_LENGTH - 1;
                    writeBuffer[0] = (byte)len;
                    var i = 1;
                    int n = len;
                    while (n != 0)
                    {
                        // Buffer.BlockCopy unfortunately not available in NETMF
                        writeBuffer[i] = buffer[offset];
                        i = i + 1;
                        offset = offset + 1;
                        n = n - 1;
                    }
                    count = count - len;
                    link.Write(writeBuffer, 0, UsbHidConstants.REPORT_LENGTH);
                    // wait at least one millisecond until next message
                    Thread.Sleep(UsbHidConstants.REQUEST_PERIOD);
                } while (count != 0);
                // all bytes have been written
            }
        }

        public override void Flush()
        {
            if (link != null)
            {
                link.Flush();
            }
        }

        public override void Close()
        {
            endOfReading = true;
            if (!endOfWriting)
            {
                endOfWriting = true;
                writeBuffer[0] = 0;
                try
                {
                    link.Write(writeBuffer, 0, UsbHidConstants.REPORT_LENGTH);
                    Flush();
                    // link is NOT closed, it will be reused by later HidStream instances
                }
                catch (Exception)
                { }
                link = null;
                Thread.Sleep(UsbHidConstants.REQUEST_PERIOD);     // wait at least one millisecond until next report
            }
            Dispose();
        }


        // unavailable features

        public override bool CanSeek { get { return false; } }
        public override bool CanTimeout { get { return false; } }
        // actually read *does* time out, but the timeout is fixed and cannot be changed

        public override int ReadTimeout
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public override int WriteTimeout
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public override long Length
        {
            get { throw new System.NotImplementedException(); }
        }

        public override long Position
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }
    }
}
