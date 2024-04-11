﻿using Meadow;
using Meadow.Foundation;
using Meadow.Foundation.Sensors.Temperature;
using Meadow.Foundation.Graphics;
using Meadow.Foundation.Displays;
using Meadow.Devices;
using Meadow.Hardware;
using Meadow.Gateway.WiFi;
using Meadow.Units;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using TemperatureWarriorCode.Web;
using NETDuinoWar;
using Meadow.Gateways.Bluetooth;


namespace TemperatureWarriorCode
{
    public class Fan
    {
        // Fan pin
        public IDigitalOutputPort pin;
        public IPwmPort pwm;

        // Constructor
        public Fan(F7FeatherV2 device, IPin pin)
        {
            this.pwm = device.CreatePwmPort(pin, new Frequency(25000, Frequency.UnitType.Hertz), 0f);
            pwm.Start();
            Console.WriteLine("Fan initialized");
        }

        // Methods
        public void TurnOn()
        {
            pwm.DutyCycle = 0.1f;
        }

        public void TurnOff()
        {
            pwm.DutyCycle = 0f;
        }
        // NOTE: a duty cycle of 0 appears to output the maximum voltage to the pin (4.4V)
        // A duty cycle of 1 appears to output 3.3V to the pin
        // A duty cycle of 0.1 appears to output 0.35V to the pin
    }


    public class MeadowApp : App<F7FeatherV2>
    {

        //Temperature Sensor
        AnalogTemperature sensor; // outdated

        //Display
        St7789 display; //not used
        MicroGraphics graphics;


        //Time Controller Values
        public static int total_time = 0;
        public static int total_time_in_range = 0;
        public static int total_time_out_of_range = 0;

        public int count = 0;

        //Fan
        static Fan fan;


        public override async Task Run()
        {
            if (count == 0)
            {
                Console.WriteLine("Initialization...");

                // TODO uncomment when needed

                // Create the fan passing the Device and the pin
                fan = new Fan(Device, Device.Pins.D02);

                // Temperature Sensor Configuration
                sensor = new AnalogTemperature(analogPin: Device.Pins.A01, sensorType: AnalogTemperature.KnownSensorType.TMP36);
                sensor.TemperatureUpdated += AnalogTemperatureUpdated;
                sensor.StartUpdating(TimeSpan.FromSeconds(2));


                // TODO Local Network configuration (uncomment when needed)
                var wifi = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();
                wifi.NetworkConnected += WiFiAdapter_ConnectionCompleted;

                Console.WriteLine($"ssid: {Secrets.WIFI_NAME}\npswd: {Secrets.WIFI_PASSWORD}");
                //WiFi Channel
                WifiNetwork wifiNetwork = ScanForAccessPoints(Secrets.WIFI_NAME);
                Console.WriteLine($"wifinetwork{wifiNetwork}");

                wifi.NetworkConnected += WiFiAdapter_WiFiConnected;
                await wifi.Connect(Secrets.WIFI_NAME, Secrets.WIFI_PASSWORD);

                string IPAddress = wifi.IpAddress.ToString();

                //Connnect to the WiFi network.
                Console.WriteLine($"IP Address: {IPAddress}");
                Data.IP = IPAddress;
                if (!string.IsNullOrWhiteSpace(IPAddress))
                {
                    Data.IP = IPAddress;
                    WebServer webServer = new WebServer(wifi.IpAddress, Data.Port);
                    if (webServer != null)
                    {
                        webServer.Start();
                    }
                }

                // TODO Display initialization (uncomment when needed)
                //Display();

                count++;
            }

            ////////////////////// TESTING FAN //////////////////////
            while (true)
            {
                // Turn on the fan
                Console.WriteLine("Turning on the fan");
                fan.TurnOn();
                Thread.Sleep(5000);
                // Turn off the fan
                Console.WriteLine("Turning off the fan");
                fan.TurnOff();
                Thread.Sleep(5000);
            }
            /////////////////// END OF TESTING FAN //////////////////
        }

        //TW Combat Round
        public static void StartRound()
        {

            Stopwatch timer = Stopwatch.StartNew();
            timer.Start();

            //Value to control the time for heating and cooling
            //First iteration is 100 for the time spend creating timecontroller and thread
            int sleep_time = 20;

            //Initialization of time controller
            TimeController timeController = new TimeController();

            //Configuration of differents ranges
            TemperatureRange[] temperatureRanges = new TemperatureRange[Data.round_time.Length];

            //Range configurations
            bool success;
            string error_message = null;
            Data.is_working = true;

            //define ranges
            for (int i = 0; i < Data.temp_min.Length; i++)
            {
                Console.WriteLine(Data.temp_max[i]);
                temperatureRanges[i] = new TemperatureRange(double.Parse(Data.temp_min[i]), double.Parse(Data.temp_max[i]), int.Parse(Data.round_time[i]) * 1000);
                total_time += int.Parse(Data.round_time[i]);
            }

            //Initialization of timecontroller with the ranges
            timeController.DEBUG_MODE = false;
            success = timeController.Configure(temperatureRanges, total_time * 1000, Data.refresh, out error_message);
            Console.WriteLine(success);

            //Initialization of timer
            Thread t = new Thread(Timer);
            t.Start();

            Stopwatch regTempTimer = new Stopwatch();
            timeController.StartOperation(); // aquí se inicia el conteo en la librería de control
            regTempTimer.Start();

            Console.WriteLine("STARTING");

            //THE TW (Temperature Warrior) START WORKING
            while (Data.is_working)
            {
                // Turn on the fan
                fan.TurnOn();

                //This is the time refresh we did not do before
                Thread.Sleep(Data.refresh - sleep_time);

                //Temperature registration
                Console.WriteLine($"RegTempTimer={regTempTimer.Elapsed.ToString()}, enviando Temp={Data.temp_act}");
                timeController.RegisterTemperature(double.Parse(Data.temp_act));
                regTempTimer.Restart();

            }
            // END OF THE ROUND
            // Turn off the fan
            fan.TurnOff();
            Console.WriteLine("Round Finish");
            t.Abort();

            total_time_in_range += timeController.TimeInRangeInMilliseconds;
            total_time_out_of_range += timeController.TimeOutOfRangeInMilliseconds;
            Data.time_in_range_temp = (timeController.TimeInRangeInMilliseconds / 1000);

            Console.WriteLine("Tiempo dentro del rango " + (((double)timeController.TimeInRangeInMilliseconds / 1000)) + " s de " + total_time + " s");
            Console.WriteLine("Tiempo fuera del rango " + ((double)total_time_out_of_range / 1000) + " s de " + total_time + " s");
        }

        //Round Timer
        private static void Timer()
        {
            Data.is_working = true;
            for (int i = 0; i < Data.round_time.Length; i++)
            {
                Data.time_left = int.Parse(Data.round_time[i]);

                while (Data.time_left > 0)
                {
                    Data.time_left--;
                    Thread.Sleep(1000);
                }
            }
            Data.is_working = false;
        }

        //Temperature and Display Updated
        void AnalogTemperatureUpdated(object sender, IChangeResult<Meadow.Units.Temperature> e)
        {


            Data.temp_act = Math.Round((Double)e.New.Celsius, 2).ToString();

            Console.WriteLine($"Temperature={Data.temp_act}");
        }

        void WiFiAdapter_WiFiConnected(object sender, EventArgs e)
        {
            if (sender != null)
            {
                Console.WriteLine($"Connecting to WiFi Network {Secrets.WIFI_NAME}");
            }
        }

        void WiFiAdapter_ConnectionCompleted(object sender, EventArgs e)
        {
            Console.WriteLine("Connection request completed.");
        }

        protected WifiNetwork ScanForAccessPoints(string SSID)
        {
            WifiNetwork wifiNetwork = null;
            ObservableCollection<WifiNetwork> networks = new ObservableCollection<WifiNetwork>(Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>().Scan()?.Result?.ToList()); //REVISAR SI ESTO ESTA BIEN
            wifiNetwork = networks?.FirstOrDefault(x => string.Compare(x.Ssid, SSID, true) == 0);
            return wifiNetwork;
        }
    }
}