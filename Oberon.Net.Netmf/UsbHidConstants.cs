//Copyright 2014 Oberon microsystems, Inc.
// NOT OPEN SOURCE!

namespace Oberon.Net.Netmf.UsbHidStreams
{
    class UsbHidConstants
    {
        // these constants ensure the maximum throughput that is possible with the HID profile
        internal const int REPORT_LENGTH = 64;      // 64 bytes is maximum payload of a USB HID report message
        internal const byte REQUEST_PERIOD = 1;     // one millisecond is shortest possible time between two HID report messages
        internal const byte LAST_REPORT = 0;        // last report of this connection ("connection close") in
        // this direction ("end of request" should be signaled with
        // a LAST_REPORT if possible, "end of response" must be
        // signaled with a LAST_REPORT)
    }
}
