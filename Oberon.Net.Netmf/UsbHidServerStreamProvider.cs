//Copyright 2014 Oberon microsystems, Inc.
// NOT OPEN SOURCE!

// TODO make UsbHidServerStreamProvider private?

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware.UsbClient;
using System;
using System.Diagnostics.Contracts;
using System.IO;

namespace Oberon.Net.Netmf.UsbHidStreams
{
    public sealed class UsbHidServerStreamProvider : IServerStreamProvider
    {
        // caution: singleton semantics (i.e., there should only be one instance of this class)!
        ushort vendorID;
        ushort productID;
        ushort deviceVersion;
        string manufacturerName;
        string productName;
        string displayName;
        string friendlyName;

        public bool IsOpen { get; private set; }
        public string LocalHostName { get; private set; }
        public int LocalPort { get; private set; }
        public string LocalUrl { get; private set; }

        public ushort VendorID { get; private set; }
        public ushort ProductID { get; private set; }
        public ushort DeviceVersion { get; private set; }

        public string ManufacturerName { get; set; }
        public string ProductName { get; set; }
        public string DisplayName { get; set; }
        public string FriendlyName { get; set; }

        // singleton variables
        static UsbController usbController;
        static Stream usbStream;                        // this stream is never closed!

        public UsbHidServerStreamProvider()
        {
            IsOpen = false;
            LocalHostName = "USB";
            LocalPort = -1;
            vendorID = 0x0000;
            productID = 0x0000;
            deviceVersion = 0x0000;
            manufacturerName = "";
            productName = "";
            displayName = "";
            friendlyName = "";
        }

        public void Open()
        {
            Contract.Requires(!IsOpen);
            Contract.Requires(manufacturerName != null);
            Contract.Requires(manufacturerName.Length > 0);
            Contract.Requires(productName != null);
            Contract.Requires(productName.Length > 0);
            Contract.Requires(displayName != null);
            Contract.Requires(displayName.Length > 0);
            Contract.Requires(friendlyName != null);
            Contract.Requires(friendlyName.Length > 0);

            try
            {
                usbController = UsbController.GetController(0);     // TODO generalize to deal with multiple controllers?
                if (usbController.Status != UsbController.PortState.Stopped) { usbController.Stop(); }
                Debug.GC(true);                                     // compact heap
                usbController.Configuration = HidConfiguration(this);
                usbController.Start();
                usbStream = usbController.CreateUsbStream(1, 2);
                Debug.GC(true);
                // TODO does USB controller need to be stopped/restarted
                // after connection with PC was temporarily lost?
                IsOpen = true;
            }
            catch (Exception)
            {
                usbController = null;
            }
        }

        public void Dispose()
        {
            if (IsOpen)
            {
                Contract.Assert(usbController != null);
                IsOpen = false;
                usbController.Stop();
                // TODO how to completely close the controller?
            }
        }

        public Stream Accept()
        {
            Contract.Requires(IsOpen);
            var s = new UsbHidStream(usbStream);
            do
            {
                s.ReadIntoBuffer();
                // ReadIntoBuffer waits until a report arrives, or a stray last-report, or a timeout
            } while (!s.DataAvailable);
            return s;
        }

        static Configuration HidConfiguration(UsbHidServerStreamProvider l)
        {
            Contract.Requires(l != null);
            var manufacturerName = new Configuration.StringDescriptor(1, l.manufacturerName);
            var productName = new Configuration.StringDescriptor(2, l.productName);
            var displayName = new Configuration.StringDescriptor(4, l.displayName);
            var friendlyName = new Configuration.StringDescriptor(5, l.friendlyName);

            // create the device descriptor.
            var device = new Configuration.DeviceDescriptor(l.vendorID,
                                                            l.productID,
                                                            l.deviceVersion);
            device.iManufacturer = 1;   // string #1 is the manufacturer name
            device.iProduct = 2;        // string #2 is the product name

            var inEndpoint = new Configuration.Endpoint(
                1, Configuration.Endpoint.ATTRIB_Interrupt | Configuration.Endpoint.ATTRIB_Write);
            inEndpoint.bInterval = UsbHidConstants.REQUEST_PERIOD;

            var outEndpoint = new Configuration.Endpoint(
                2, Configuration.Endpoint.ATTRIB_Interrupt | Configuration.Endpoint.ATTRIB_Read);

            var endpoints = new Configuration.Endpoint[]
            {
                inEndpoint,
                outEndpoint
            };

            // set up the mouse interface
            var usbInterface = new Configuration.UsbInterface(0, endpoints);
            usbInterface.bInterfaceClass = 3;       // HID interface
            usbInterface.bInterfaceSubClass = 0;    // no subclass
            usbInterface.bInterfaceProtocol = 0;    // no protocol

            // assemble the HID class descriptor
            var hidPayload = new byte[]
            {
                0x01, 0x01,     // bcdHID = HID version 1.01
                0x00,           // bCountryCode = none
                0x01,           // bNumDescriptors = number of descriptors available for this device
                0x22,           // bDescriptorType = Report descriptor
                25, 0           // wDescriptorLength = total size of Report descriptor
            };

            usbInterface.classDescriptors = new Configuration.ClassDescriptor[1]
            {
                new Configuration.ClassDescriptor(0x21, hidPayload)     // class HID
            };

            var usbInterfaces = new Configuration.UsbInterface[]
            {
                usbInterface
            };

            var cfgDesc = new Configuration.ConfigurationDescriptor(100, usbInterfaces);  // 100 mA

            // create the report descriptor as a Generic
            var ReportPayload = new byte[]
            {
                0x06, 0x00, 0xFF,		// Usage page (vendor defined)          (global)
                0x09, 0x01,				// Usage ID (vendor defined)            (local)
                0xA1, 0x01,				// Collection (application)             (main)

                0x15, 0x00,     		// Logical Minimum (0)                  (global)
                0x26, 0xFF, 0x00,   	// Logical Maximum (255)                (global)
                0x75, 0x08,     		// Report Size (8 bits)                 (global)
                0x95, UsbHidConstants.REPORT_LENGTH,   // Report Count (64 fields) (global)

                0x09, 0x03,     		// Usage ID - vendor defined            (local)
                0x81, 0x02,     		// Input (Data, Variable, Absolute)     (main)

                0x09, 0x04,     		// Usage ID - vendor defined            (local)
                0x91, 0x02,      	    // Output (Data, Variable, Absolute)    (main)

                0xC0					// end collection                       (main)
            };

            const byte DescriptorRequest = 0x81;
            const byte ReportDescriptor = 0x22;
            const ushort Report_wValue = (ushort)ReportDescriptor << 8;
            var reportDescriptor = new Configuration.GenericDescriptor(
                DescriptorRequest, Report_wValue, ReportPayload);

            var configuration = new Configuration();
            configuration.descriptors = new Configuration.Descriptor[]
            {
                device,
                cfgDesc,
                manufacturerName,
                productName,
                displayName,
                friendlyName,
                reportDescriptor
            };
            return configuration;
        }
    }
}
