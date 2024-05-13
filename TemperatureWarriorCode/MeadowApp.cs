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
using Meadow.Gateways.Bluetooth;
using System.Collections.Generic;

using PidAlgo;

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
        // NOTE: the fan relay works with a control signal of 5V, but the digital output only outputs 3.3V
        // We fix it using a PWM signal:
        // A duty cycle of 0 appears to output the maximum voltage to the pin (4.4V) which is enough to control the relay
        // A duty cycle of 1 appears to output 3.3V to the pin
        // A duty cycle of 0.1 appears to output 0.35V to the pin
    }

    public class HeatGun
    {
        // Heat Gun pin
        public IDigitalOutputPort pin;

        // Constructor
        public HeatGun(F7FeatherV2 device, IPin pin)
        {
            this.pin = device.CreateDigitalOutputPort(pin: pin, initialState: false);
            Console.WriteLine("Heat Gun initialized");
        }

        // Methods
        public void TurnOn()
        {
            pin.State = true;
        }

        public void TurnOff()
        {
            pin.State = false;
        }
        // NOTE: the heat gun relay works with a control signal of at least 3V,
        // so the digital output (3.3V) is enough
    }

    public class Peltier
    {
        // Heat Gun pin
        public IDigitalOutputPort pin;

        // Constructor
        public Peltier(F7FeatherV2 device, IPin pin)
        {
            this.pin = device.CreateDigitalOutputPort(pin: pin, initialState: false);
            Console.WriteLine("Peltier initialized");
        }

        // Methods
        public void TurnOn()
        {
            pin.State = true;
        }

        public void TurnOff()
        {
            pin.State = false;
        }
        // NOTE: the peltier relay works with a control signal of at least 3V,
        // so the digital output (3.3V) is enough
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
        //Heat Gun
        static HeatGun heatGun;
        //Peltier
        static Peltier peltier;
        // PID Controller
        static PidAlgo.PIDController pid;
        //Config variables
        public bool okay = false;

        public override async Task Run()
        {
            if (count == 0)
            {
                Console.WriteLine("Initialization...");

                // TODO uncomment when needed

                // Create the fan passing the Device and the pin
                fan = new Fan(Device, Device.Pins.D02);

                // Create the heat gun passing the Device and the pin
                heatGun = new HeatGun(Device, Device.Pins.D15);

                // Create the peltier passing the Device and the pin
                peltier = new Peltier(Device, Device.Pins.D14);

                // Create the PID controller
                pid = new PidAlgo.PIDController(1, 1, 1);

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
            ////////////////////////////////////////// DEVICE TESTING //////////////////////////////////////////

            //////////////////// TESTING PELTIER ////////////////////
            while (true)
            {
                // Turn on the peltier
                Console.WriteLine("Turning on the peltier");
                peltier.TurnOn();
                Thread.Sleep(2000);
                // Turn off the peltier
                Console.WriteLine("Turning off the peltier");
                peltier.TurnOff();
                Thread.Sleep(2000);
            }
            //////////////// END OF TESTING PELTIER /////////////////

            //////////////////// TESTING HEAT GUN ///////////////////
            while (true)
            {
                // Turn on the heat gun
                Console.WriteLine("Turning on the heat gun");
                heatGun.TurnOn();
                Thread.Sleep(5000);
                // Turn off the heat gun
                Console.WriteLine("Turning off the heat gun");
                heatGun.TurnOff();
                Thread.Sleep(5000);
            }
            //////////////// END OF TESTING HEAT GUN ////////////////

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

            ////////////////////////////////////// END OF DEVICE TESTING ///////////////////////////////////////
            
        }

        //TW Combat Round
        public static void StartRound()
        {   
            Console.WriteLine("Starting Round");
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
                // Save all ranges to temperature Ranges
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
                for (int i = 0; i < temperatureRanges.Length; i++){
                    // Setpoint calculation
                    double setPoint = temperatureRanges[i].MaxTemp + temperatureRanges[i].MinTemp / 2;
                    Stopwatch roundTimer = Stopwatch.StartNew(); // Temporizador para la ronda actual
                    while (roundTimer.ElapsedMilliseconds < temperatureRanges[i].RangeTimeInMilliseconds)
                    {
                        // LEER DEL SENSOR
                        double currentTemp = 0; 

                        // Calculate pid output
                        double pidOutput = pid.Update(currentTemp, setPoint);
                        if (pidOutput > 0.5) {
                            // Turn off peltier and turn on heat gun
                            peltier.TurnOff();
                            heatGun.TurnOn();
                            Console.WriteLine("Turning on heat gun");
                        } else if (pidOutput < -0.5) {
                            // Turn off heat gun and turn on peltier
                            peltier.TurnOn();
                            heatGun.TurnOff();
                            Console.WriteLine("Turning on peltier");
                        } else {
                            // Turn off peltier and heat gun
                            peltier.TurnOff();
                            heatGun.TurnOff();
                            Console.WriteLine("Turning off peltier and heat gun");
                        }
                        // PUNTO X
                        Thread.Sleep(Data.refresh - sleep_time);
                    }
                }

                // SOSPECHO PONER TODO LO DE ABAJO EN PUNTO X
                // PARA QUE SIRVE ESTO
                //This is the time refresh we did not do before
                //Thread.Sleep(Data.refresh - sleep_time);

                //Temperature registration
                Console.WriteLine($"RegTempTimer={regTempTimer.Elapsed.ToString()}, enviando Temp={Data.temp_act}");
                timeController.RegisterTemperature(double.Parse(Data.temp_act));
                regTempTimer.Restart();

            }
            // END OF THE ROUND
            // Turn off the fan
            peltier.TurnOff();
            heatGun.TurnOff();
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
