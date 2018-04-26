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

// Version 4.3, for the .NET Micro Framework release 4.3.

// These classes provide objects for sensors and actuators that implement
// a common interface pattern: Properties are used to configure an object,
// and Open method checks the properties and created internal objects as
// needed, and HandleGet and HandleSet methods provide a standard way for
// getting new samples from a sensor, or for setting and getting setpoints
// for an actuator.

using System.Diagnostics.Contracts;
using Microsoft.SPOT.Hardware;

namespace Oberon.HttpResources.Netmf
{
    /// <summary>
    /// Represents a digital input (GPIO pin).
    /// </summary>
    public class DigitalSensor
    {
        Cpu.Pin pin = Cpu.Pin.GPIO_NONE;
        InputPort port;

        /// <summary>
        /// Mandatory property that denotes the pin to be used as input.
        /// Must be set before sensor is opened.
        /// </summary>
        public Cpu.Pin InputPin
        {
            get { return pin; }

            set
            {
                Contract.Requires(port == null);    // sensor is not open
                pin = value;
            }
        }

        /// <summary>
        /// To be called after setting the properties of the object.
        /// If not called explicitly, it is automatically called when
        /// the sensor is used for the first time.
        /// Preconditions
        ///     Sensor is not open
        ///     InputPin is set
        /// Postconditions
        ///     Sensor is open
        /// </summary>
        public void Open()
        {
            Contract.Requires(port == null);        // sensor is not open
            Contract.Requires(InputPin != Cpu.Pin.GPIO_NONE);
            port = new InputPort(InputPin, false,
                Port.ResistorMode.Disabled);
        }

        /// <summary>
        /// Returns a new sample.
        /// If the sensor is not open, Open is called first.
        /// Postconditions
        ///     (Result == null) || (Result is bool)
        /// </summary>
        public object HandleGet()
        {
            if (port == null) { Open(); }
            return port.Read();     // bool converted to object
        }
    }

    /// <summary>
    /// Represents a digital output (GPIO pin).
    /// </summary>
    public class DigitalActuator
    {
        Cpu.Pin pin = Cpu.Pin.GPIO_NONE;
        bool actualState;
        OutputPort port;

        /// <summary>
        /// Mandatory property that denotes the pin to be used as output.
        /// Must be set before actuator is opened.
        /// </summary>
        public Cpu.Pin OutputPin
        {
            get { return pin; }

            set
            {
                Contract.Requires(port == null);    // actuator is not open
                pin = value;
            }
        }

        /// <summary>
        /// Optional property that denotes the initial state of the output.
        /// Default: false
        /// Preconditions
        ///      Actuator is not open
        /// </summary>
        public bool InitialState
        {
            get { return actualState; }

            set
            {
                Contract.Requires(port == null);    // actuator is not open
                actualState = value;
            }
        }

        /// <summary>
        /// To be called after setting the properties of the object.
        /// If not called explicitly, it is automatically called when
        /// the actuator is used for the first time.
        /// Preconditions
        ///     Actuator is not open
        ///     OutputPin is set
        /// Postconditions
        ///     Actuator is open
        /// </summary>
        public void Open()
        {
            Contract.Requires(port == null);        // actuator is not open
            Contract.Requires(OutputPin != Cpu.Pin.GPIO_NONE);
            port = new OutputPort(OutputPin, actualState);
        }

        /// <summary>
        /// Sets a new setpoint.
        /// If the actuator is not open, Open is called first.
        /// Preconditions
        ///     setpoint != null
        ///     setpoint is bool
        /// </summary>
        public void HandlePut(object setpoint)
        {
            Contract.Requires(setpoint != null);
            Contract.Requires(setpoint is bool);
            if (port == null) { Open(); }
            actualState = (bool)setpoint;
            port.Write(actualState);
        }

        /// <summary>
        /// Returns most recent setpoint.
        /// If none has been set yet, the initial state is returned.
        /// If the actuator is not open, Open is called first.
        /// Postconditions
        ///     (Result == null) || (Result is bool)
        /// </summary>
        public object HandleGet()
        {
            if (port == null) { Open(); }
            return actualState;
        }
    }

    /// <summary>
    /// Represents an analog input.
    /// The input value is linearly scaled to the range [MinValue..MaxValue]
    /// and returned as a double value.
    /// </summary>
    public class AnalogSensor
    {
        /// <summary>
        /// Mandatory property that denotes the channel to be used as input.
        /// Must be set before sensor is opened.
        /// </summary>
        public Cpu.AnalogChannel InputPin { get; set; }

        /// <summary>
        /// Optional property with the minimal value of the input range.
        /// Default: 0.0
        /// </summary>
        public double MinValue { get; set; }

        /// <summary>
        /// Optional property with the maximum value of the input range.
        /// Default: 3.3
        /// </summary>
        public double MaxValue { get; set; }

        public AnalogSensor()
        {
            MinValue = 0.0;
            MaxValue = 3.3;
        }

        AnalogInput port;

        /// <summary>
        /// To be called after setting the properties of the object.
        /// If not called explicitly, it is automatically called when
        /// the sensor is used for the first time.
        /// Preconditions
        ///     Sensor is not open
        ///     InputPin is set
        ///     MinValue lessThan MaxValue
        /// Postconditions
        ///     Sensor is open
        /// </summary>
        public void Open()
        {
            Contract.Requires(port == null);        // sensor is not open
            Contract.Requires(InputPin >= 0);
            Contract.Requires(MinValue < MaxValue);
            port = new AnalogInput(InputPin);
            port.Offset = MinValue;
            port.Scale = MaxValue - MinValue;
        }

        /// <summary>
        /// Returns a new sample.
        /// If the sensor is not open, Open is called first.
        /// Postconditions
        ///     Result is double
        ///     (Result greaterOrEqual MinValue) && (Result lessOrEqual MaxValue)
        /// </summary>
        public object HandleGet()
        {
            if (port == null) { Open(); }
            double value = port.Read();
            // NETMF has already done the scaling
            return value;
        }
    }

    /// <summary>
    /// Represents a PWM output, which can often be used instead of a
    /// true analog output.
    /// The output value is linearly scaled to the range [MinValue..MaxValue].
    /// </summary>
    public class PwmActuator
    {
        /// <summary>
        /// Mandatory property that denotes the channel to be used as output.
        /// Must be set before actuator is opened.
        /// </summary>
        public int OutputPin { get; set; }

        /// <summary>
        /// Mandatory property that denotes the period of the PWM in
        /// microseconds.
        /// Must be set before actuator is opened.
        /// </summary>
        public int Period { get; set; }

        /// <summary>
        /// Optional property with the minimal value of the output range.
        /// Default: 0.0
        /// </summary>
        public double MinValue { get; set; }

        /// <summary>
        /// Optional property with the maximum value of the output range.
        /// Default: 3.3
        /// </summary>
        public double MaxValue { get; set; }

        public PwmActuator()
        {
            MinValue = 0.0;
            MaxValue = 3.3;
        }

        double minValue;
        double maxValue;
        PWM port;
        object lastSetpoint = null;

        /// <summary>
        /// To be called after setting the properties of the object.
        /// If not called explicitly, it is automatically called when
        /// the actuator is used for the first time.
        /// Preconditions
        ///     Actuator is not open
        ///     OutputPin is set
        ///     Period is set
        ///     MinValue lessThan MaxValue
        /// Postconditions
        ///     Actuator is open
        /// </summary>
        public void Open()
        {
            Contract.Requires(port == null);        // actuator is not open
            Contract.Requires(OutputPin >= 0);
            Contract.Requires(Period > 0);
            Contract.Requires(MinValue < MaxValue);
            minValue = MinValue;
            maxValue = MaxValue;
            port = new PWM((Cpu.PWMChannel)OutputPin, (uint)Period, 0, PWM.ScaleFactor.Microseconds, false);
            port.Start();
        }

        /// <summary>
        /// Sets a new setpoint.
        /// If the actuator is not open, Open is called first.
        /// If the setpoint is below MinValue, it is clamped to MinValue.
        /// If the setpoint is above MaxValue, it is clamped to MaxValue.
        /// Preconditions
        ///     setpoint != null
        ///     setpoint is double
        /// </summary>
        public void HandlePut(object setpoint)
        {
            Contract.Requires(setpoint != null);
            Contract.Requires(setpoint is double);
            double x = (double)setpoint;
            if (x < MinValue) { x = MinValue; }
            else if (x > MaxValue) { x = MaxValue; }
            if (port == null) { Open(); }
            int duration = (int)(((x - MinValue) * Period) / (MaxValue - MinValue));
            port.Duration = (uint)duration;
            lastSetpoint = setpoint;
        }

        /// <summary>
        /// Returns most recent setpoint.
        /// If none has been set yet, null is returned.
        /// If the actuator is not open, Open is called first.
        /// Postconditions
        ///     (Result == null) || (Result is double)
        /// </summary>
        public object HandleGet()
        {
            if (port == null) { Open(); }
            return lastSetpoint;
        }
    }
}
