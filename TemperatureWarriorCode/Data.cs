using System;
using Meadow.Foundation;

namespace TemperatureWarriorCode {
    class Data {

        //WEB VARIABLES
        public static string IP = null;
        public static int Port = 2550;

        //ROUND VARIABLES
        public static string[] temp_max = { "14; 30; 20" }; // In ºC
        public static String[] temp_min = { "12; 28; 19" }; // In ºC
        public static int display_refresh = 100; // In ms
        public static int refresh = 100; // In ms
        public static String[] round_time = { "30; 30; 30" }; // in s

        public static int temp_max_act = 0; // In ºC
        public static int temp_min_act = 0; // In ºC
        public static int current_round = 0; // In s
        public static int number_of_instances = 0; // In s

        // MODE VARIABLES
        public static bool not_set_sensor_refresh = true;

        //START ROUND VARIABLES
        public static bool is_working = false;
        public static string temp_act = "0"; // In ºC
        public static int time_left; // in s
        public static int time_in_range_temp = 0; //In ms.

        //SENSOR VARIABLES
        public static string[] csv_count = Array.Empty<string>();

        public static string[] temp_values = Array.Empty<string>();
        public static string[] pid_values = Array.Empty<string>();
                                           
        //COLORS FOR DISPLAY
        public static Color[] colors = new Color[4]
        {
            Color.FromHex("#67E667"),
            Color.FromHex("#00CC00"),
            Color.FromHex("#269926"),
            Color.FromHex("#008500")
        };
    }
}
