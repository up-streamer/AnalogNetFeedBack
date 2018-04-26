# AnalogNetFeedBack
An IOT PWM with analog feedback poof of concept

The code generates a crude PWM pulse based on turning on and off the digital output GPIO_PIN_D0, then it goes to a filter and the resultand DC voltage is feeded back to the board thru analog input ANALOG_5.
It Also has a server which handles the values in and out to a very simple web page.
Think of it as one more example using the techincs shown on the book "Getting Started with the Internet of Things by Cuno Pfister"

The AnalogNetFeedBack.zip file contains all the project files for Netduino 2 plus firmware 4.3.1 in Visual Studio 2012 environment.

More infos at: tipsforiot.wordpress.com
