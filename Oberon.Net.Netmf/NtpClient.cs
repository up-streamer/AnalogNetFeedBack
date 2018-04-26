// TODO NTP is based on UDP protocol, not TCP. Add UDP support to Oberon.Net.

//using Microsoft.SPOT.Time;
//using System;
//using System.Diagnostics.Contracts;
//using System.IO;

//// Explicit implementation of the NTP protocol, to retrieve the current
//// time from a time server. It is used to set the on-board clocke of a
//// NETMF board that has no battery backup for its real-time clock.
//// This implementation is relevant for programs that need to obtain the
//// current time via network streams that are not (only) based on the
//// Socket API. If you only need sockets, then you can use the native
//// NTP implementation of NETMF. For an example, see here:
//// http://www.mountaineer.org/resources/tidbits/using-the-real-time-clock/

//namespace Oberon.Net.Netmf.Ntp
//{
//    public static class NtpClient
//    {
//        public static void UpdateClock(string timeServer, IStreamFactory streamFactory)
//        {
//            // Thanks to Michael Schwarz for the original version
//            // of this method (http://weblogs.asp.net/mschwarz/about.aspx).

//            if (timeServer == null)
//            {
//                timeServer = "pool.ntp.org";
//                // you can find more time servers on
//                // http://support.ntp.org/bin/view/Servers/WebHome
//                // e.g. "time-a.nist.gov"
//            }
//            else
//            {
//                Contract.Requires(timeServer.Length > 0);
//            }
//            Contract.Requires(streamFactory != null);

//            // prepare send/receive buffer 
//            byte[] ntpData = new byte[48];
//            ntpData[0] = 0x1B;              // set protocol version 

//            using (Stream s = streamFactory.Connect(timeServer, 123))
//            {
//                s.Write(ntpData, 0, 48);
//                s.Read(ntpData, 0, 48);
//            }

//            byte otTime = 40;

//            ulong intpart = 0;
//            for (int i = 0; i != 4; i = i + 1)
//            {
//                intpart = (intpart << 8) | ntpData[otTime + i];
//            }

//            ulong fractpart = 0;
//            for (int i = 4; i != 8; i = i + 1)
//            {
//                fractpart = (fractpart << 8) | ntpData[otTime + i];
//            }

//            ulong ms = intpart * 1000 + (fractpart * 1000) / 0x100000000L;
//            long ticks = (long)ms * TimeSpan.TicksPerMillisecond;

//            var origin = new DateTime(1900, 1, 1);
//            long utcTicks = origin.Ticks + ticks;
//            TimeService.SetUtcTime(utcTicks);
//        }
//    }
//}
