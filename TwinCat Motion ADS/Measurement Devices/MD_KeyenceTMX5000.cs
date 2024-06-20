﻿using System.IO.Ports;
using System.Threading;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;

namespace TwinCat_Motion_ADS.MeasurementDevice
{    
    public class MD_KeyenceTMX5000 : BaseRs232MeasurementDevice, I_MeasurementDevice
    {
        public MD_KeyenceTMX5000(string baudRate = "115200")
        {
            BaudRate = baudRate;
            DeviceType = DeviceTypes.KeyenceTMX5000;

            ChConnected = new bool[KEYENCE_MAX_CHANNELS];
            ChName = new string[KEYENCE_MAX_CHANNELS];
        }
        private const int defaultTimeout = 1000;

        const int _KEYENCE_MAX_CHANNELS = 16;
        public int KEYENCE_MAX_CHANNELS { get { return _KEYENCE_MAX_CHANNELS; } }
        public bool[] ChConnected { get; set; }
        public string[] ChName { get; set; }

        public new bool Disconnect()
        {
            ChannelList.Clear();
            if (SerialPort.IsOpen)
            {
                try
                {
                    SerialPort.Close();
                    SerialPort.Dispose();
                }
                catch
                {
                    return false;
                }
                Connected = false;
                UpdateChannelList();
                return true;
            }
            return false;
        }

        public void UpdateChannelList()
        {
            ChannelList.Clear();
            NumberOfChannels = 0;
            if (!Connected) return;
            Tuple<string, int> tmp;// = (Name, 1).ToTuple();

            for (int i = 1; i <= KEYENCE_MAX_CHANNELS; i++)
            {
                if (ChConnected[i - 1])
                {
                    tmp = (ChName[i - 1], i).ToTuple();
                    ChannelList.Add(tmp);
                }
            }
            NumberOfChannels = ChannelList.Count;
        }

        public async Task<string> GetMeasurement()
        {
            ReadInProgress = true;
            string measurement;
            List<string> measures = await GetAllMeasures();
            measurement = String.Join(",", measures);    //Keyence returns a string list so we can convert to a single CSV string
            ReadInProgress = false;
            return measurement;
        }

        public async Task<string> GetChannelMeasurement(int channelNumber = 0)
        {
            ReadInProgress = true;
            string measurement = await GetMeasureAsync(channelNumber);
            ReadInProgress = false;
            return measurement;
        }

        public async Task<List<string>> GetAllMeasures(int timeoutMilliSeconds = defaultTimeout)
        {
            List<string> measurements = new List<string>(); //setup a list to hold measurement data
            for (int i = 1; i < 17; i++)
            {
                var str = await GetMeasureAsync(i);
                if (str[1] != 'X')  //Check the 2nd char of returned string, F is when no data present and X is unused channel
                {
                    measurements.Add(str);  //If valid data add to the list
                }
            }
            return measurements;
        }
        
        public async Task<string> GetMeasureAsync(int measurementChannel, int timeoutMilliSeconds = defaultTimeout)
        {
            CancellationTokenSource ct = new CancellationTokenSource();

            //Send GM,0,1,ID as my message. Tool channels appear to come in as 200,201,202,203 etc
            var txBuf = new byte[] { 0x47, 0x4D, 0x2C, 0x30, 0x2C, 0x31, 0x2C, 0x30, 0x30, 0x30, 0x0D };
            txBuf[7] = 0x32;
            if (measurementChannel>10)
            {
                txBuf[8] = 0x31;
                txBuf[9] = Convert.ToByte(measurementChannel + 37);
            }
            else
            {
                txBuf[8] = 0x30;
                txBuf[9] = Convert.ToByte(measurementChannel + 47);
            }
            
            //create a buffer to hold the returned value
            var rxBuf = new byte[15];
            rxBuf = await ReadAsyncBuff(txBuf, 15, ct, timeoutMilliSeconds);
            if (rxBuf.Length != 15) //Device should always return 29 bytes for a single channel read
            {
                return "Measurement failed";
            }
            byte[] measureBuf = new byte[9]; //setup a buffer for extracting just the 8 bytes of measurement data
            System.Array.Copy(rxBuf, 5, measureBuf, 0, 9);
            var str = System.Text.Encoding.Default.GetString(measureBuf); //convert measurement buffer to a string
            return str;
        }
        
        public async Task<byte[]> ReadAsyncBuff(byte[] cmd, int rb, CancellationTokenSource ct, int timeout = defaultTimeout)
        {
            byte[] measurement = new byte[rb];
            var readTask = Task<byte[]>.Run(() =>
            {

                SerialPort.DiscardInBuffer();
                //SerialPort.Write(cmd, 0, 20);   //Should be 20 bytes for a read
                SerialPort.Write(cmd, 0, 11);
                while (SerialPort.BytesToRead < rb)
                {
                    if (ct.Token.IsCancellationRequested)
                    {
                        return null;
                        //throw new TaskCanceledException();
                    }
                }
                _ = SerialPort.Read(measurement, 0, rb);
                return measurement;
            });
            if (await Task.WhenAny(readTask, Task.Delay(timeout, ct.Token)) == readTask)
            {
                ct.Cancel();
                return measurement;
            }
            else
            {
                ct.Cancel();
                return System.Array.Empty<byte>(); //if timeout return empty array
            }
        }
    }

}
