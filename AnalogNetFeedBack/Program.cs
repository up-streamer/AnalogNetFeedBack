using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;
using Configuration;
using Oberon.HttpResources.Netmf;
using Oberon.HttpServers;
using System.Diagnostics;
using Oberon.Net;

namespace AnalogNetFeedBack
{
    public class Program
    {
        public static void Main()
        {
            // Yaler parameters
            Parameters.RelayDomain = null;
            Parameters.RelaySecretKey = null;
            // Xively parameters
            Parameters.ApiKey = null;
            Parameters.FeedId = null;
            
            Parameters.Setup();
           
            var buffer = new Buffer { };

            var buffer2 = new AnalogSensor
            {
                InputPin = Cpu.AnalogChannel.ANALOG_5,        //Declare to Oberon.HTTPResources
                MinValue = 0.0,
                MaxValue = 3.3
            };

            var blinker = new Blinker { SourceBuffer = buffer };
            var pwmread = new PWMRead { SourceBuffer2 = buffer2 };

            var webServer = new HttpServer
            {
                StreamProvider = Parameters.StreamProvider,
                RequestRouting =
            {
                {
                    "PUT /PWMDuty/target",
                    new ManipulatedVariable
                    {
                        FromHttpRequest =
                            CSharpRepresentation.TryDeserializeInt,
                        ToActuator = buffer.HandlePut
                    }.HandleRequest
                },
                {
                    "GET /PWMDuty/target.html",
                    HandleBlinkTargetHtml
                },
                {
                    "GET /PWMDuty/target/actual",
                    new MeasuredVariable
                    {
                        FromSensor = buffer2.HandleGet
                    }.HandleRequest
                }
            }
            };

            var blinkerThread = new Thread(blinker.Run);
            var PWMreadThread = new Thread(pwmread.Run);
            PWMreadThread.Start();
            blinkerThread.Start();
            webServer.Run();
        }

        static void HandleBlinkTargetHtml(RequestHandlerContext context)
        {
            // var RefreshIt = DateTime.Now;
            string requestUri =
                context.BuildRequestUri("/PWMDuty/target");
            var script =
                @"<html>
                <head>
                  <script type=""text/javascript"">
                    var r;
                    var resp;
                    try {
                      r = new XMLHttpRequest();
                    } catch (e) {
                      r = new ActiveXObject('Microsoft.XMLHTTP');
                    }
                    function put (content) {
                      r.open('PUT', '" + requestUri + @"');
                      r.setRequestHeader(""Content-Type"", ""text/plain"");
                      r.send(document.getElementById(""duty"").value);
                    }
                    
                    function get () {
                        r.open('GET', '" + requestUri + "/actual" + @"');
                        r.onreadystatechange = XMLHttpRequestCompleted;
                        r.setRequestHeader(""Content-Type"", ""text/plain"");
                        r.send(null);   
                     }

                    var Timer = setInterval(function(){ get() }, 3000);

                     function XMLHttpRequestCompleted()
                     {   
                         if (r.readyState == 4)
                           {
                            try
                               {
                                resp = parseFloat(r.responseText);
                                resp = resp.toFixed(3);
                                resp = resp.toString(); 
                                resp = resp + "" Volts"";
                                (document.getElementById(""atual"").value)=resp;
                               }
                                catch (e)
                                 {
                                 }   
                           }
                     }
                   

                  </script>
                </head>
                <body onload='get()'>
                  <p>
                    <input type=""text"" value=""50"" id=""duty"">
                    <input type=""button"" value=""Set Duty %"" onclick=""put()""/>
                    <input type=""text"" value=""0"" id=""atual"">
                    <input type=""button"" value=""Actual"" onclick=""get()""/>
                  </p>
                </body>
              </html>";
            context.SetResponse(script, "text/html");
        }
    }

    public class Blinker
    {
        public Buffer SourceBuffer { get; set; }
        public AnalogSensor SourceBuffer2 { get; set; }

        public void Run()
        {
            var ledPort = new OutputPort(Pins.ONBOARD_LED, false);

            // Output PWM pulse for RC filter
            var PWMPort = new OutputPort(Pins.GPIO_PIN_D0, false);  // //Cpu.Pin.GPIO_Pin0

            // Voltage read from RC filter.
            var duty = 50;
            var on = true;
            while (true)
            {
                object setpoint = SourceBuffer.HandleGet();
                if (setpoint != null)
                {
                    duty = (int)setpoint;

                    /* Adjust max. duty cycle to 100% equals to 1000 ms */
                    //duty = duty * 10;

                    /* Limits duty cyle betweel 1% e 100%, em 100 ms */
                    duty = duty > 100 ? 100 : duty;
                    duty = duty < 1 ? 1 : duty;
                }

                on = true;
                ledPort.Write(on);
                PWMPort.Write(on);
                Thread.Sleep(duty);
                on = false;
                ledPort.Write(on);
                PWMPort.Write(on);
                Thread.Sleep(100 - duty);
            }
        }
    }

    public class PWMRead
    {
        public AnalogSensor SourceBuffer2 { get; set; }

        public void Run()
        {
            while (true)
            {
                object voltHiRes = SourceBuffer2.HandleGet();
                Double b = (Double)voltHiRes;
                var a = Rounding(b);
                string Prtvalue = voltHiRes.ToString();
                Debug.Print("Volts = " + Prtvalue + " " + "Rounded = " + a);
                Thread.Sleep(3000);                 // 3 secondos between samples
            }

        }

        // 'Trick' to round to 3 decimal places.
        // Note: Due size constrains, .netmf doesn't support full rounding class,
        //or the range of Math functions the full .net has.
        private string Rounding(Double Value)
        {
            return (Value.ToString("f3"));
        }

    }


    /// <summary>
    /// The Buffer class provides a means for parallel actors to exchange
    /// data in a thread-safe way. One actor puts objects into a buffer
    /// instance, the other fetches them from the buffer.
    /// This buffer class is able to buffer at most one object at a time:
    /// if the producer puts a new object into the buffer, its old contents
    /// is replaced by the new one. When the consumer gets the current
    /// buffer contents, the buffer is not modified in any way.
    /// This buffer class is intended for actors that are synchronized to
    /// real-time ("time-triggered"); no mechanism for direct
    /// synchronization between the actors is provided.
    /// </summary>
    public sealed class Buffer
    {
        readonly static object Lock = new object();

        object buffer = null;

        /// <summary>
        /// Puts object o, which may be null, into the buffer.
        /// The current buffer contents is overwritten with the new one.
        /// It is safe to call this method from different threads.
        /// </summary>
        /// <param name="o">New value to be buffered.</param>
        public void HandlePut(object o)
        {
            lock (Lock)
            {
                buffer = o;
            }
        }

        /// <summary>
        /// Returns the object that is currently contained in the buffer,
        /// or null.
        /// It is safe to call this method from different threads.
        /// </summary>
        /// <returns>Currently buffered value. May be null.</returns>
        public object HandleGet()
        {
            object o;
            lock (Lock)
            {
                o = buffer;
            }
            return o;
        }
    }
}
