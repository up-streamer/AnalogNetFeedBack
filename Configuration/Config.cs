using Microsoft.SPOT.Hardware;
using Oberon.Net;
using Oberon.Net.SocketStreams;
using Oberon.Yaler;
using SecretLabs.NETMF.Hardware.NetduinoPlus;

namespace Configuration
{
    public static class Parameters
    {
        // parameters for I/O ports specific to the used hardware
        public static Cpu.Pin LedPin;
        public static Cpu.Pin ButtonPin;
        public static Cpu.Pin LowPin;
        public static Cpu.Pin HighPin;
        public static Cpu.AnalogChannel AnalogPin;
        // connect the potentiometer as described in the book on pages 22 to 25

        // parameters for Xively samples
        // check at https://xively.com/feeds/<insert_your_Xively_feed_ID_here>
        public static string ApiKey;
        public static string FeedId;

        // parameters for server samples
        public static string RelayDomain;
        public static string RelaySecretKey;
        public static IServerStreamProvider StreamProvider;

        public static void Setup()
        {
            LedPin = Pins.ONBOARD_LED;
            ButtonPin = Pins.ONBOARD_SW1;
            LowPin = Pins.GPIO_PIN_A0;
            HighPin = Pins.GPIO_PIN_A2;
            AnalogPin = AnalogChannels.ANALOG_PIN_A1;

            //ApiKey = "<insert your API key here>";
            //FeedId = "<insert your feed ID here>";
            // check at https://xively.com/feeds/<FeedId>

            //RelayDomain = "<insert your relay domain here>";
            //RelaySecretKey = "<insert your secret key here>";

            if (RelayDomain == null)
            {
               StreamProvider = new SocketServerStreamProvider(80);
            }
            else
            {
                IClientStreamProvider clientStreamFactory = new SocketClientStreamProvider();
                StreamProvider = new YalerServerStreamProvider(clientStreamFactory, RelayDomain, RelaySecretKey);
            }
        }
    }
}
