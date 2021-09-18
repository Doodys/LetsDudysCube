using System;
using System.IO.Ports;

namespace TimeCube
{
    public class PortConfig
    {
        public string SerialMessage { get; set; }

        private readonly string portName;
        private const int BAUD_RATE = 115200;

        public SerialPort serialPort;
        public bool continueReading;

        public PortConfig(string portName)
        {
            this.portName = portName;

            serialPort = new SerialPort();
            SetSerialPortProperties();

            continueReading = true;
        }

        private void SetSerialPortProperties()
        {
            // Allow the user to set the appropriate properties.
            serialPort.PortName = portName;
            serialPort.BaudRate = BAUD_RATE;

            // Set the read/write timeouts
            serialPort.ReadTimeout = 500;
            serialPort.WriteTimeout = 500;
        }
    }
}
