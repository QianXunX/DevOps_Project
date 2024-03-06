using Meadow;
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


namespace TemperatureWarriorCode {
    public class MeadowApp : App<F7FeatherV2> {

        //Temperature Sensor
        AnalogTemperature sensor;

        //Time Controller Values
        public static int total_time = 0;
        public static int total_time_in_range = 0;
        public static int total_time_out_of_range = 0;

        public int count = 0;

        // Entry point of the application
        public override async Task Run() {
            if (count == 0) {
                Console.WriteLine("Initialization...");

                // TODO uncomment when needed 
                // Temperature Sensor Configuration
                sensor = new AnalogTemperature(analogPin: Device.Pins.A01, sensorType: AnalogTemperature.KnownSensorType.TMP36);
                // sensor.Temperature.Value.Celsius // To do it this way we would need to create a new thread to update the temperature (we are not doing it this way)
                sensor.TemperatureUpdated += AnalogTemperatureUpdated;  // This way we are updating the temperature in the main thread by using a callback
                sensor.StartUpdating(TimeSpan.FromSeconds(2));  // We are updating the temperature every 2 seconds (Change this value if needed)
                // Note: DHT22 sensor only updates every 2 seconds, so there is no need to update it more frequently if it is used

                // TODO Display Configuration (uncomment when needed) (WE DO NOT NEED THE DISPLAY)
                //var config = new SpiClockConfiguration(new Frequency(48000, Frequency.UnitType.Kilohertz), SpiClockConfiguration.Mode.Mode3);
                //var spiBus = Device.CreateSpiBus(Device.Pins.SCK, Device.Pins.COPI, Device.Pins.CIPO, config);
                //display = new St7789(
                //spiBus: spiBus,
                //chipSelectPin: null,
                //dcPin: Device.Pins.D01,
                //resetPin: Device.Pins.D00,
                //width: 240, height: 240);
                //graphics = new MicroGraphics(display);
                //graphics.Rotation = RotationType._270Degrees;

                // TODO Local Network configuration (uncomment when needed)
                //var wifi = Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>();
                //wifi.NetworkConnected += WiFiAdapter_ConnectionCompleted;

                ////WiFi Channel
                //WifiNetwork wifiNetwork = ScanForAccessPoints(Secrets.WIFI_NAME);

                //wifi.NetworkConnected += WiFiAdapter_WiFiConnected;
                //await wifi.Connect(Secrets.WIFI_NAME, Secrets.WIFI_PASSWORD);

                //string IPAddress = wifi.IpAddress.ToString();

                // Connnect to the WiFi network.
                /*
                Console.WriteLine($"IP Address: {IPAddress}");
                Data.IP = IPAddress;
                if (!string.IsNullOrWhiteSpace(IPAddress)) {
                    Data.IP = IPAddress;
                    WebServer webServer = new WebServer(wifi.IpAddress, Data.Port);
                    if (webServer != null) {
                        webServer.Start();  // After the start, it will be running in the background
                    }
                }
                */

                // TODO Display initialization (uncomment when needed)
                //Display();

                Console.WriteLine("Meadow Initialized!");

                count = count + 1;
            }
        }

        //TW Combat Round
        public static void StartRound() {

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
            for (int i = 0; i < Data.temp_min.Length; i++) {
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

            //THE TW START WORKING
            while (Data.is_working) {
                // PUT HERE THE CODE TO CONTROL THE HEATER AND COOLER

                //This is the time refresh we did not do before
                // We are sleeping the time we need to sleep to make the refresh time be the same as the one we want
                // We need to update sleep_time to adjust it to the time that our code takes to run, so that timeController.RegisterTemperature
                // is called at EXACTLY thetime we are told to do so
                Thread.Sleep(Data.refresh - sleep_time);

                //Temperature registration
                Console.WriteLine($"RegTempTimer={regTempTimer.Elapsed.ToString()}, enviando Temp={Data.temp_act}");
                timeController.RegisterTemperature(double.Parse(Data.temp_act));
                regTempTimer.Restart();

            }
            Console.WriteLine("Round Finish");
            t.Abort();

            total_time_in_range += timeController.TimeInRangeInMilliseconds;
            total_time_out_of_range += timeController.TimeOutOfRangeInMilliseconds;
            Data.time_in_range_temp = (timeController.TimeInRangeInMilliseconds / 1000);

            Console.WriteLine("Tiempo dentro del rango " + (((double)timeController.TimeInRangeInMilliseconds / 1000)) + " s de " + total_time + " s");
            Console.WriteLine("Tiempo fuera del rango " + ((double)total_time_out_of_range / 1000) + " s de " + total_time + " s");
        }

        //Round Timer
        private static void Timer() {
            Data.is_working = true;
            for (int i = 0; i < Data.round_time.Length; i++) {
                Data.time_left = int.Parse(Data.round_time[i]);

                while (Data.time_left > 0) {
                    Data.time_left--;
                    Thread.Sleep(1000);
                }
            }
            Data.is_working = false;
        }

        //Display Theme


        //Temperature and Display Updated
        void AnalogTemperatureUpdated(object sender, IChangeResult<Meadow.Units.Temperature> e) {
            // We can filter the high frequency changes on the temperature (DO THIS SPECIALLY FOR THE BAD SENSOR)
            Data.temp_act = Math.Round((Double)e.New.Celsius, 2).ToString();
            Console.WriteLine($"Temperature={Data.temp_act}");
        }

        void WiFiAdapter_WiFiConnected(object sender, EventArgs e) {
            if (sender != null) {
                Console.WriteLine($"Connecting to WiFi Network {Secrets.WIFI_NAME}");
            }
        }

        void WiFiAdapter_ConnectionCompleted(object sender, EventArgs e) {
            Console.WriteLine("Connection request completed.");
        }

        protected WifiNetwork ScanForAccessPoints(string SSID) {
            WifiNetwork wifiNetwork = null;
            ObservableCollection<WifiNetwork> networks = new ObservableCollection<WifiNetwork>(Device.NetworkAdapters.Primary<IWiFiNetworkAdapter>().Scan()?.Result?.ToList()); //REVISAR SI ESTO ESTA BIEN
            wifiNetwork = networks?.FirstOrDefault(x => string.Compare(x.Ssid, SSID, true) == 0);
            return wifiNetwork;
        }
    }
}