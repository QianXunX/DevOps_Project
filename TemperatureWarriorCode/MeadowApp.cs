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
using System.Dynamic;
using System.Security.Cryptography;

using System.IO;
using System.Text;

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
    public class MovingAverage
    {
        private readonly Queue<double> samples = new Queue<double>();
        private readonly int windowSize;
        private double sum = 0;

        public MovingAverage(int size)
        {
            windowSize = size;
        }

        public void AddSample(double sample)
        {
            samples.Enqueue(sample);
            sum += sample;

            if (samples.Count > windowSize)
            {
                sum -= samples.Dequeue();
            }
        }

        public double GetAverage()
        {
            if (samples.Count == 0)
            {
                return 0;
            }

            return sum / samples.Count;
        }

        public double GetStandardDeviation()
        {
            double average = GetAverage();
            double sumOfSquaresOfDifferences = samples.Select(val => (val - average) * (val - average)).Sum();
            double standardDeviation = Math.Sqrt(sumOfSquaresOfDifferences / samples.Count);
            return standardDeviation;
        }
    }


    public class MeadowApp : App<F7FeatherV2>
    {
        private const int MovingAverageWindowSize = 3;
        private readonly MovingAverage movingAverage = new MovingAverage(MovingAverageWindowSize);

        // Umbral para detectar valores atípicos (en número de desviaciones estándar)
        private const double OutlierThreshold = 7.0;

        //Temperature Sensor
        public static AnalogTemperature sensor; // outdated

        //Display
        //St7789 display; //not used
        //MicroGraphics graphics;


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

                // Initial Checkup HW
                // turn on all elements for 1 second and then turn off
                fan.TurnOn();
                heatGun.TurnOn();
                peltier.TurnOn();
                Thread.Sleep(1000);
                fan.TurnOff();
                heatGun.TurnOff();
                peltier.TurnOff();


                // Create the PID controller
                pid = new PidAlgo.PIDController(1.2, 0.001, 0.01);

                // Temperature Sensor Configuration
                sensor = new AnalogTemperature(analogPin: Device.Pins.A03, sensorType: AnalogTemperature.KnownSensorType.TMP36);
                sensor.TemperatureUpdated += AnalogTemperatureUpdated;
                sensor.StartUpdating(TimeSpan.FromSeconds(1));


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
        }

        static void printArray(string[] array)
        {
            string result = "[" + string.Join(", ", array) + "]";
            Console.WriteLine(result);
        }

        public static void UpdateSensorRefreshRate(int refreshRateInMilliseconds)
        {
            TimeSpan newInterval = TimeSpan.FromMilliseconds(refreshRateInMilliseconds);
            sensor.StopUpdating(); // Detiene la actualización actual
            sensor.StartUpdating(newInterval); // Inicia la actualización con el nuevo intervalo
            Console.WriteLine($"Sensor refresh rate updated to {newInterval.TotalMilliseconds} milliseconds.");
        }
        public static void SaveToCsv(string[] values, string filePath)
        {
            StringBuilder csvContent = new StringBuilder();

            foreach (var value in values)
            {
                csvContent.AppendLine(value);
            }

            File.WriteAllText(filePath, csvContent.ToString());
        }

        //TW Combat Round
        public static void StartRound()
        {
            try
            {

                if (Data.number_of_instances > 0)
                {
                    return;
                }

                Data.number_of_instances++;

                UpdateSensorRefreshRate(Data.refresh);

                Console.WriteLine("Datas ==================================================");
                printArray(Data.temp_min);
                printArray(Data.temp_max);
                printArray(Data.round_time);
                Console.WriteLine("==================================================");

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

                //define ranges
                for (int i = 0; i < Data.temp_min.Length; i++)
                {
                    // Save all ranges to temperature Ranges
                    if (double.TryParse(Data.temp_min[i], out double tempMin) &&
                    double.TryParse(Data.temp_max[i], out double tempMax) &&
                    int.TryParse(Data.round_time[i], out int roundTime))
                    {
                        temperatureRanges[i] = new TemperatureRange(tempMin, tempMax, roundTime * 1000);
                        total_time += roundTime;
                    }
                    else
                    {
                        Console.WriteLine("Error parsing temperature ranges");
                        Console.WriteLine("Array: ");
                        double intValueMin, intValueMax;
                        int intValueRound;
                        bool sMin = double.TryParse(Data.temp_min[i], out intValueMin);
                        bool sMax = double.TryParse(Data.temp_min[i], out intValueMax);
                        bool sRound = int.TryParse(Data.temp_min[i], out intValueRound);

                        Console.WriteLine(sMin + Data.temp_min[i]);
                        printArray(Data.temp_min);
                        Console.WriteLine(sMax + Data.temp_max[i]);
                        printArray(Data.temp_max);
                        Console.WriteLine(sRound + Data.round_time[i]);
                        printArray(Data.round_time);
                        return;
                    }
                }
                // Turning on the fan


                //Initialization of timecontroller with the ranges
                timeController.DEBUG_MODE = false;
                success = timeController.Configure(temperatureRanges, total_time * 1000, Data.refresh, out error_message);
                Data.total_time_s = total_time;
                Console.WriteLine(success);


                // Initial settings
                Console.WriteLine("Turning on the fan");
                fan.TurnOn();
                Console.WriteLine("Turning off the peltier and heat gun");
                peltier.TurnOff();
                heatGun.TurnOff();

                // Create arrat to store temperature values named registered_temps
                Data.temp_values = new string[0];
                Data.pid_values = new string[0];
       


                // Inicialización del cronómetro
                Stopwatch stopwatch = new Stopwatch();
                //Stopwatch regTempTimer = new Stopwatch();

                bool emergencyError = false;
                int run_once = 0;
                //THE TW (Temperature Warrior) START WORKING


                run_once++;
                //regTempTimer.Start();
                Stopwatch sleep = new Stopwatch();

                sleep.Start();
                Console.WriteLine("STARTING ROUND/S ==================================================");
        

                int timeInRangeAccumulated = 0;
                timeController.StartOperation(); // aquí se inicia el conteo en la librería de control
            
                //Initialization of timer
                Thread t = new Thread(Timer);
                Data.current_round = 1;
                t.Start();
    
                for (int i = 0; i < temperatureRanges.Length; i++)
                {
                    Data.current_round = i + 1;

                    // CONTADOR DE TIEMPO DE RANGO
                    stopwatch.Start();

                    // Initializing round
                    Console.WriteLine("Starting round " + i);
                    Console.WriteLine("Temp Range: [" + temperatureRanges[i].MinTemp + ", " + temperatureRanges[i].MaxTemp + "]");
                    Console.WriteLine("Time: " + temperatureRanges[i].RangeTimeInMilliseconds);

                    // Setpoint calculation
                    double setPointWhenCurrAbove = temperatureRanges[i].MaxTemp;
                    Data.temp_max_act = int.Parse(Data.temp_max[i]);
                    double setPointWhenCurrBelow = temperatureRanges[i].MinTemp;
                    Data.temp_min_act = int.Parse(Data.temp_min[i]);

                    // set both setpoints to the same value being the middle of the range, above + below / 2
                    double setPoint = (setPointWhenCurrAbove + setPointWhenCurrBelow) / 2;



                    Stopwatch roundTimer = Stopwatch.StartNew(); // Temporizador para la ronda actual
                    while (roundTimer.ElapsedMilliseconds < (temperatureRanges[i].RangeTimeInMilliseconds))  
                    {
                        if (Data.is_working == false)
                        {

                            break;
                        }

                        if (double.TryParse(Data.temp_act, out double currentTemp))
                        {
                            currentTemp = Math.Round(currentTemp, 1);

                            if (currentTemp > 55)
                            {   
                                Console.WriteLine("OVER 55ºC");
                                fan.TurnOff();
                                heatGun.TurnOff();
                                peltier.TurnOff();
                                emergencyError = true;
                                break;
                            }

                            timeController.RegisterTemperature(currentTemp);
                            Data.time_in_range_temp = (timeController.TimeInRangeInMilliseconds / 1000);

                            // Calculate pid output
                            // use only setpoint and current temperature to calculate pid output
                            String decision = "none";
                            double pidOutput = 0;

                            pidOutput = pid.Update(currentTemp, setPoint);
                            if (pidOutput > 1)
                            {
                                // Turn on peltier
                                decision = "H";
                                peltier.TurnOff();
                                heatGun.TurnOn();
                            } else if (pidOutput < -1) {
                                // Turn on heat gun
                                decision = "P";
                                peltier.TurnOn();
                                heatGun.TurnOff();
                            } else {
                                // Turn off peltier and heat gun
                                decision = "N";
                                peltier.TurnOff();
                                heatGun.TurnOff();

                            }
                            //Console.WriteLine(currentTemp + " :: " + Math.Round(pidOutput, 2) + " :: " + decision);
                            //Console.WriteLine(stopwatch.ElapsedMilliseconds.ToString() + " + " + sleep.ElapsedMilliseconds.ToString() + " = " + (stopwatch.ElapsedMilliseconds + sleep.ElapsedMilliseconds));
                        }
                        else
                        {
                            Console.WriteLine("Error parsing temperature" + Data.temp_act);
                        }
                            
                        stopwatch.Stop();
                        sleep_time = (int)stopwatch.ElapsedMilliseconds;
                            
                        if (sleep_time > Data.refresh)
                        {
                            sleep_time = Data.refresh;
                        }

                        sleep.Restart();


                        Thread.Sleep(Data.refresh - sleep_time);
                        sleep.Stop();

                        stopwatch.Restart();

                    }
                    if (Data.is_working == false)
                    {
                        break;
                    }

                    // FINISH ROUND

                    // MEDICION TIEMPO EN RANGO
                    int timeInRangeCurrent = timeController.TimeInRangeInMilliseconds - timeInRangeAccumulated;
                    timeInRangeAccumulated = timeController.TimeInRangeInMilliseconds;
 
                    Data.time_in_range_temp = (timeController.TimeInRangeInMilliseconds / 1000);
                    String rangeTimeString = Math.Round((double)temperatureRanges[i].RangeTimeInMilliseconds/1000, 1).ToString();
                    String timeInRangeSeconds = Math.Round((double)timeInRangeCurrent / 1000, 1).ToString();
                    String timeOutOfRangeSeconds = Math.Round((double)(temperatureRanges[i].RangeTimeInMilliseconds - timeInRangeCurrent)/1000, 1).ToString();

                    Data.current_round_time_in_range = Math.Round( (double)timeInRangeCurrent / 1000, 1);

                    Console.WriteLine("::::::::::::::::::::RESULTS OF ROUND:::::::::::::::::");
                    Console.WriteLine("Tiempo dentro del rango " + timeInRangeSeconds + " s de " + rangeTimeString + " s");
                    Console.WriteLine("Tiempo fuera del rango " + timeOutOfRangeSeconds + " s de " + rangeTimeString + " s");


                    //regTempTimer.Restart();
                    //Console.WriteLine($"RegTempTimer={regTempTimer.Elapsed.ToString()}, enviando Temp={Data.temp_act}");

                    if (emergencyError) { break; }

                }


                run_once = 0;

                // END OF THE COMBAT
                Data.is_combat_finished = true;

                Console.WriteLine("::::::::::::::::::::RESULTS:::::::::::::::::");

                Console.WriteLine("Tiempo total " + total_time + " s");
                Console.WriteLine("Tiempo en rango " + (Math.Round((double)timeController.TimeInRangeInMilliseconds / 1000, 1)) + " s de " + total_time + " s");

                Console.WriteLine("Tiempo dentro del rango " + Math.Round((double)timeController.TimeInRangeInMilliseconds / 1000, 1) + " s de " + total_time + " s");
                Console.WriteLine("Tiempo fuera del rango " + Math.Round((double)timeController.TimeOutOfRangeInMilliseconds / 1000, 1) + " s de " + total_time + " s");


                peltier.TurnOff();
                heatGun.TurnOff();
                fan.TurnOff();
                Console.WriteLine("Round Finish");

                t.Abort();
                Data.is_working = false;

                Console.WriteLine("data.is_working = " + Data.is_working);

                Data.number_of_instances --;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in StartRound");
                Console.WriteLine(e);
            }
        }

        static T[] AppendToArray<T>(T[] array, T value) {
            // Create a new array with a size one larger than the original array
            T[] newArray = new T[array.Length + 1];

            // Copy elements from the original array to the new array
            for (int i = 0; i < array.Length; i++) {
                newArray[i] = array[i];
            }

            // Add the new value to the end of the new array
            newArray[newArray.Length - 1] = value;

            return newArray;
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
                    Console.WriteLine("Time left: " + Data.time_left);
                }
                Data.time_left = 0;
                Console.WriteLine("i: " + i);
                Console.WriteLine("Data.round_time.Length: " + Data.round_time.Length);

            }
            Console.WriteLine("#################Timer Finish");
            Data.is_working = false;
        }

        //Temperature and Display Updated
        void AnalogTemperatureUpdated(object sender, IChangeResult<Meadow.Units.Temperature> e)
        {
            double currentTemperature;
            try
            {
                currentTemperature = e.New.Celsius;
            }catch(Exception ex)
            {
                Console.WriteLine("Error in AnalogTemperatureUpdated");
                Console.WriteLine(ex);
                currentTemperature = 18;
            }
            Data.temp_act = Math.Round(currentTemperature, 1).ToString();
            //Console.WriteLine($"Temperature={Data.temp_act}");
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
