﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.IO;
using System.Text.Json.Serialization;
using Meadow.Foundation.Graphics;
using Newtonsoft.Json;


namespace TemperatureWarriorCode.Web {
    public class WebServer
    {

        private IPAddress _ip = null;
        private int _port = -1;
        private bool _runServer = true;
        private static HttpListener listener;
        private static int pageViews = 0;
        private static int requestCount = 0;
        private static bool ready = false;
        private static readonly string pass = "pass";
        private static string message = "";
        private static bool canRefresh = false;


        /// <summary>
        /// Delegate for the CommandReceived event.
        /// </summary>
        public delegate void CommandReceivedHandler(object source, WebCommandEventArgs e);

        /// <summary>
        /// CommandReceived event is triggered when a valid command (plus parameters) is received.
        /// Valid commands are defined in the AllowedCommands property.
        /// </summary>
        public event CommandReceivedHandler CommandReceived;

        public string Url
        {
            get
            {
                if (_ip != null && _port != -1)
                {
                    return $"http://{_ip}:{_port}/";
                }
                else
                {
                    return $"http://127.0.0.1:{_port}/";
                }
            }
        }

        public WebServer(IPAddress ip, int port)
        {
            _ip = ip;
            _port = port;
        }


        public void Start()
        {
            if (listener == null)
            {
                listener = new HttpListener();
                listener.Prefixes.Add(Url);

            }

            listener.Start();

            Console.WriteLine($"The url of the webserver is {Url}");

            Task.Run(() => HandleIncomingConnections());
        }

        public void Stop()
        {
            _runServer = false;
        }

        public static string[] trimAndRemoveEmpty(string[] data)
        {
            if (data == null)
            {
                return new string[0]; // Devolver un array vacío si los datos son nulos
            }

            List<string> cleanedData = new List<string>();

            foreach (string item in data)
            {
                // Decodificar la cadena primero
                string decodedItem = Uri.UnescapeDataString(item);
                string trimmedItem = decodedItem.Trim();

                if (!string.IsNullOrWhiteSpace(trimmedItem))
                {
                    // Verificar si el elemento es un número válido
                    if (int.TryParse(trimmedItem, out _))
                    {
                        cleanedData.Add(trimmedItem);
                    }
                    else
                    {
                        Console.WriteLine($"No es un número válido: {trimmedItem}");
                    }
                }
            }

            return cleanedData.ToArray();

        }

        // Checks if the string contains only numbers
        public static bool isAllNumbers(string input)
        {
            return Regex.IsMatch(input, @"^-?\d+(\.\d+)?$");
        }

        public static string tempCheck(string[] data)
        {
            if (data == null)
            {
                Console.WriteLine("Data is null");
                return "Data is null";
            }

            foreach (string item in data)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    return "Empty value"; // empty value
                }
                else if (!isAllNumbers(item))
                {
                    return "Not a number: " + item + "; Array: " + printArray(data); // not a number
                }
                else if (int.Parse(item) < 12 || int.Parse(item) > 30)
                {
                    return "Out of range: " + item; // out of range
                }

            }
            return ""; // all correct
        }
        static string printArray(string[] array)
        {
            string result = "[" + string.Join(", ", array) + "]";
            Console.WriteLine(result);
            return result;
        }

        // check if elements in array temp_max are greater than elements in array temp_min
        public static string compareMaxMinValues(string[] temp_max, string[] temp_min)
        {
            try
            {

                if (temp_max == null || temp_min == null)
                {
                    Console.WriteLine("Data is null");
                    return "Data is null";
                }

                if (temp_max.Length != temp_min.Length)
                {
                    return "The length of the arrays is different!";
                }

                for (int i = 0; i < temp_max.Length; i++)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(temp_max[i]) || string.IsNullOrWhiteSpace(temp_min[i]))
                        {
                            return "Value is empty: " + temp_max[i] + " " + temp_min[i];
                        }
                        else if (!isAllNumbers(temp_max[i]) || !isAllNumbers(temp_min[i]))
                        {
                            return "Value is not a number: " + temp_max[i] + " " + temp_min[i];
                        }
                        if (int.Parse(temp_max[i]) <= int.Parse(temp_min[i]))
                        {
                            return "Temp max is smaller than Temp min in position: " + i + " Temp max: " + temp_max[i] + " Temp min: " + temp_min[i];
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error en compareMaxMinValues");
                        Console.WriteLine(e);
                        Console.WriteLine("Temp max: " + temp_max[i] + " Temp min: " + temp_min[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error en compareMaxMinValues");
                Console.WriteLine(e);
                return "Error en compareMaxMinValues: " + e;
            }
            return "";
        }

        public static string timeCheck(string[] data)
        {
            if (data == null)
            { return "Data is Null"; }

            foreach (string item in data)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    return "Value is empty: " + data;
                }
                else if (!isAllNumbers(item))
                {
                    return "Value is not a number: " + data;
                }
            }
            return "";

        }

        public static string refreshValueCheck(string data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data))
                {
                    return "Value is empty: " + data;
                }
                else if (!isAllNumbers(data))
                {
                    return "Value is not a number: " + data;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error en refreshValueCheck");
                Console.WriteLine(e);
                return "Error en refreshValueCheck: " + e;
            }
            return "";

        }

        private async Task UpdatePage(string message)
        {
            // Will wait here until we hear from a connection
            HttpListenerContext ctx = await listener.GetContextAsync();
            HttpListenerResponse resp = ctx.Response;

            // Write the response info
            string disableSubmit = !_runServer ? "disabled" : "";
            byte[] data = Encoding.UTF8.GetBytes(writeHTML(message));
            resp.ContentType = "text/html";
            resp.ContentEncoding = Encoding.UTF8;
            resp.ContentLength64 = data.LongLength;

            // Write out to the response stream (asynchronously), then close it
            await resp.OutputStream.WriteAsync(data, 0, data.Length);
            resp.Close();
        }

        public void CombatTerminationWatcher()
        {
            try
            {
                int prevRound = 1;
                while (Data.is_working)
                {
                    if (Data.current_round != prevRound)
                    {
                        prevRound = Data.current_round;
                        message = "Se ha terminado la ronda " + prevRound + " con " + Data.current_round_time_in_range
                            + "s en el rango " + "[ " + Data.temp_min_act + ", " + Data.temp_max_act + "]" + ".";
                    }
                    Thread.Sleep(1000);
                }

                message = "Se ha terminado todas las rondas con " + Data.time_in_range_temp + " s de "
                    + Data.total_time_s + " s en los rangos indicados.";
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in CombatTerminationWatcher:");
                Console.WriteLine(e);
            }
        }

        private async Task HandleIncomingConnections()
        {

            await Task.Run(async () =>
            {
                // While a user hasn't visited the `shutdown` url, keep on handling requests
                while (_runServer)
                {

                    // Will wait here until we hear from a connection
                    HttpListenerContext ctx = await listener.GetContextAsync();

                    // Peel out the requests and response objects
                    HttpListenerRequest req = ctx.Request;
                    HttpListenerResponse resp = ctx.Response;

                    // Print out some info about the request
                    Console.WriteLine("Request #: {0}", ++requestCount);
                    Console.WriteLine(req.Url);
                    Console.WriteLine(req.HttpMethod);
                    Console.WriteLine(req.UserHostName);
                    Console.WriteLine(req.UserAgent);
                    Console.WriteLine();


                    if (req.Url.AbsolutePath.StartsWith("/css/"))
                    {
                        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, req.Url.AbsolutePath.TrimStart('/'));
                        if (File.Exists(filePath))
                        {
                            byte[] fileData = File.ReadAllBytes(filePath);
                            resp.ContentType = "text/css";
                            resp.ContentEncoding = Encoding.UTF8;
                            resp.ContentLength64 = fileData.LongLength;
                            await resp.OutputStream.WriteAsync(fileData, 0, fileData.Length);
                            resp.Close();
                            continue;
                        }
                        else
                        {
                            resp.StatusCode = 404;
                            resp.Close();
                            continue;
                        }
                    }



                    // If `shutdown` url requested w/ POST, then shutdown the server after serving the page
                    if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/shutdown")
                    {
                        Console.WriteLine("Shutdown requested");
                        _runServer = false;
                    }
                    byte[] data;

                    if (req.Url.AbsolutePath == "/status")
                    {
                        if (ready)
                        {
                            continue;
                        }
                        if (!Data.is_working && !ready && canRefresh)
                        {
                            message = "El combate ha terminado.";
                            canRefresh = false;
                        }

                        if (Data.is_working)
                        {
                            var statusObject = new
                            {
                                TempActual = $"{Data.temp_act}ºC",
                                TiempoEnRango = $"{Math.Round((double) Data.time_in_range_temp / 1000, 1)}s",
                                Rango = new
                                {
                                    Min = $"{Math.Round((double) Data.temp_min_act, 1)}ºC",
                                    Max = $"{Math.Round((double) Data.temp_max_act, 1)}ºC"
                                },
                                TiempoFaltanteRonda = new
                                {
                                    TiempoFaltante = Math.Round((double) Data.remainRoundTime/1000, 1),
                                    TotalRonda = Data.time_left
                                },
                                TiempoFaltanteCombate = new
                                {
                                    TiempoFaltante = Math.Round((double)Data.remainTotalTime, 1),
                                    TotalCombate = Math.Round((double)Data.total_time_s/1000, 1)
                                }
                            };

                            string jsonResponse = JsonConvert.SerializeObject(statusObject);
                            byte[] statusData = Encoding.UTF8.GetBytes(jsonResponse);
                            resp.ContentType = "application/json";
                            resp.ContentEncoding = Encoding.UTF8;
                            resp.ContentLength64 = statusData.LongLength;
                            await resp.OutputStream.WriteAsync(statusData, 0, statusData.Length);
                            resp.Close();


                            continue; // Pasar a la siguiente iteración para manejar la próxima solicitud
                        }

                    }

                    if (req.Url.AbsolutePath == "/setparams")
                    {
                        try
                        {
                            //Get parameters
                            string url = req.RawUrl;
                            if (!string.IsNullOrWhiteSpace(url))
                            {

                                //Get text to the right from the interrogation mark
                                string[] urlParts = url.Split('?');
                                if (urlParts?.Length >= 1)
                                {
                                    //The parametes are in the array first position
                                    string[] parameters = urlParts[1].Split('&');
                                    if (parameters?.Length >= 2)
                                    {

                                        // Param 0 => Temp max
                                        // Param 1 => Temp min
                                        // Param 2 => to display_refresh
                                        // Param 3 => to refresh
                                        // Param 4 => to round_time
                                        // Param 5 => to pass

                                        // retrieve values from the parameters
                                        string[] temp_max_parts = parameters[0].Split('=');
                                        string[] temp_min_parts = parameters[1].Split('=');
                                        string[] display_refresh_parts = parameters[2].Split('=');
                                        string[] refresh_parts = parameters[3].Split('=');
                                        string[] round_time_parts = parameters[4].Split('=');
                                        string[] pass_parts = parameters[5].Split('=');

                                        string[] temp_max_array = trimAndRemoveEmpty(temp_max_parts[1].Split(';'));
                                        string[] temp_min_array = trimAndRemoveEmpty(temp_min_parts[1].Split(';'));
                                        string[] round_time_array = trimAndRemoveEmpty(round_time_parts[1].Split(';'));
                                        string display_refresh_str = display_refresh_parts[1];
                                        string refresh_str = refresh_parts[1];
                                        string pass_temp = pass_parts[1];

                                        /*
                                        Console.WriteLine("Temp max: " + mostarDatos(temp_max_array));
                                        Console.WriteLine("Temp min: " + mostarDatos(temp_min_array));
                                        Console.WriteLine("Round time: " + mostarDatos(round_time_array));
                                        Console.WriteLine("Display refresh: " + display_refresh_str);
                                        Console.WriteLine("Refresh: " + refresh_str);
                                        */

                                        // check if there are empty values and empty lists
                                        bool allCorrect = true;

                                        // Check for empty fields
                                        if (temp_max_array == null || temp_min_array == null || round_time_array == null ||
                                                temp_max_array.Length == 0 || temp_min_array.Length == 0 || round_time_array.Length == 0 ||
                                                string.IsNullOrWhiteSpace(display_refresh_str) || string.IsNullOrWhiteSpace(refresh_str) || string.IsNullOrWhiteSpace(pass_temp))
                                        {
                                            message = "There are empty values!";
                                            allCorrect = false;
                                        }

                                        // check if the values are correct
                                        if (allCorrect && !string.Equals(pass, pass_temp))
                                        {
                                            message = "Wrong PASSWORD!";
                                            allCorrect = false;
                                        }

                                        // check if temp_max, temp_min, round_time have the same length
                                        if (allCorrect && temp_max_array.Length != temp_min_array.Length || temp_max_array.Length != round_time_array.Length || temp_min_array.Length != round_time_array.Length)
                                        {
                                            message = "The length of the arrays is different!" + "Temp max: " + temp_max_array.Length + " Temp min: " + temp_min_array.Length + " Round time: " + round_time_array.Length;
                                            allCorrect = false;
                                        }

                                        // Check if display_refresh and refresh are numbers
                                        string output = refreshValueCheck(display_refresh_str);

                                        if (allCorrect)
                                        {
                                            output = refreshValueCheck(display_refresh_str);
                                            if (output != "")
                                            {
                                                message = "Incorrect value in DISPLAY REFRESH: " + output;
                                                allCorrect = false;
                                            }
                                        }

                                        output = tempCheck(temp_max_array);
                                        if (allCorrect && output != "")
                                        {
                                            message = "Incorrect value/s in TEMP MAX: " + output;
                                            allCorrect = false;
                                        }

                                        output = tempCheck(temp_min_array);
                                        if (allCorrect && output != "")
                                        {
                                            message = "Incorrect value/s in TEMP MIN: " + output;
                                            allCorrect = false;
                                        }
                                        output = timeCheck(round_time_array);

                                        if (allCorrect && output != "")
                                        {
                                            message = "Incorrect value/s in ROUND TIME: " + output;
                                            allCorrect = false;
                                        }

                                        output = compareMaxMinValues(temp_max_array, temp_min_array);

                                        if (allCorrect && output != "")
                                        {
                                            message = "Incorrect some value/s in TEMP MAX is smaller than TEMP MIN: " + output;
                                            allCorrect = false;
                                        }

                                        if (allCorrect && (int.TryParse(display_refresh_str, out int n) == false || n < 0))
                                        {
                                            message = "Incorrect value in DISPLAY REFRESH!";
                                            allCorrect = false;
                                        }

                                        if (allCorrect && (int.TryParse(refresh_str, out int m) == false || m < 0))
                                        {
                                            message = "Incorrect value in REFRESH!";
                                            allCorrect = false;
                                        }


                                        if (allCorrect)
                                        {
                                            Data.temp_max = temp_max_array;
                                            Data.temp_min = temp_min_array;
                                            Data.display_refresh = int.Parse(display_refresh_str);
                                            Data.refresh = int.Parse(refresh_str);
                                            Data.round_time = round_time_array;
                                            Console.WriteLine("Temp max: " + mostarDatos(Data.temp_max));
                                            Console.WriteLine("Temp min: " + mostarDatos(Data.temp_min));
                                            Console.WriteLine("Round time: " + mostarDatos(Data.round_time));
                                            Console.WriteLine("Display refresh: " + Data.display_refresh);
                                            Console.WriteLine("Refresh: " + Data.refresh);

                                            ready = true;
                                            message = "Se han guardado los valores correctamente.";
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error en setparams");
                            Console.WriteLine(e);
                        }
                    }

                    if (req.Url.AbsolutePath == "/start")
                    {
                        try
                        {
                            // Start the round
                            Thread ronda = new Thread(MeadowApp.StartRound);
                            ronda.Start();

                            while (!Data.is_working)
                            {
                                Thread.Sleep(100);
                            }

                            ready = false;
                            canRefresh = true;

                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error en start");
                            Console.WriteLine(e);
                        }
                    }
                    if (req.Url.AbsolutePath == "/temp")
                    {

                        message = $"Temp Actual: {Data.temp_act}ºC; Rango ºC: [{Data.temp_min_act} ºC, {Data.temp_max_act}ºC];\n" +
                        $"Tiempo Total en Rango / Total Combate: {Math.Round((double) Data.time_in_range_temp, 1)} / {Data.total_time_s};\n" +
                        $"Tiempo Faltante Ronda / Total Ronda: {Math.Round((double) Data.remainRoundTime / 1000, 1)} / {Math.Round((double) Data.currentRoundTime / 1000, 1)};\n" +
                        $"Tiempo Faltante Combate / Total Combate: {Math.Round((double) Data.remainTotalTime / 1000, 1)} / {Data.total_time_s}";
;
                    }

                    if (req.Url.AbsolutePath == "/stop")
                    {
                        try
                        {
              
                            Data.is_working = false;
                            message = "La ronda ha sido detenida.";
                            Console.WriteLine(message);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error en stop");
                            Console.WriteLine(e);
                        }
                    }


                    // Write the response info
                    string disableSubmit = !_runServer ? "disabled" : "";
                    data = Encoding.UTF8.GetBytes(string.Format(writeHTML(message), pageViews, disableSubmit));
                    resp.ContentType = "text/html";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.LongLength;

                    // Write out to the response stream (asynchronously), then close it
                    await resp.OutputStream.WriteAsync(data, 0, data.Length);
                    resp.Close();
                }
            });
        }


        public static string mostarDatos(string[] data)
        {
            string datos = string.Empty;
            if (data != null)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    datos = datos + data[i] + ";";
                }

                return datos;
            }
            else
            {
                return "";
            }
        }


        public static string writeHTML(string message)
        {
            // Si ya estamos listos, deshabilita todos los inputs
            string disabled = "";

            // Solo muestra guardar y comenzar ronda cuando estamos listos
            string save = "<button type=\"button\" onclick='save()'>Guardar</button>";
            string temp = "<a href='#' class='btn btn-primary tm-btn-search' onclick='temp()'>Consultar Temperatura</a>";
            string start = "";
            string stop = "";

            if (ready)
            {
                disabled = "disabled";
                save = "";
                start = "<button type=\"button\" onclick='start()'>Comenzar Ronda</button>";
            }

            if (Data.is_working)
            {
                disabled = "disabled";
                stop = "<button type=\"button\" onclick='stop()'>Parar</button>";
                start = "";
                save = "";
            }

            // Escribir la página HTML
            string html = "<!DOCTYPE html>" +
            "<html>" +
            "<head>" +
                            "<meta charset='utf-8'>" +
                            "<meta http - equiv = 'X-UA-Compatible' content = 'IE=edge'>" +
                            "<meta name = 'viewport' content = 'width=device-width, initial-scale=1' > " +
                            "<title>Meadow Controller</title>" +
                            "<link rel='stylesheet' href='https://fonts.googleapis.com/css?family=Open+Sans:300,400,600,700'>" +
                            "<link rel = 'stylesheet' href = 'https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css'>" +
                            "<style>" +
                            "#status {{" +
                                    "top:    %;" +
                                    "left: 50 %;" +
                                    "transform: translate(-50 %, -50 %);" +
                                    "font-size: 24px;" +
                                    "font-weight: bold;}}" +
                                    
                            "</style>" +

                            "<script src='https://cdnjs.cloudflare.com/ajax/libs/Chart.js/3.8.0/chart.js'> </script>" +
                            "<script>" +
                            "function fetchTemp() {{location.href = '/temp';}}" +
                            "setInterval(fetchTemp, 10000);" + // 10 segundos para no sobrecargar la meadow
                            /*
                            "async function fetchStatus() {{try{{" +
                            "const response = await fetch('/temp');" +
                            "const data = await response.json();" +
                            "console.log(data);" +  
                            //"console.log(document.getElementById('status'));" +
                            //"document.getElementById('status').innerText = 'TempActual: ${{ data.TempActual}};'" +
                            "}}catch (error){{console.error('Error fetching status', error);}}}} setInterval(fetchStatus, 5000)" + // Polling cada 5 segundos*/
                            "</script>" +
            "</head>" +
            "<body>" +
                            "<script> function save(){{" +
                            "console.log(\"Calling Save in JS!!\");" +
                            "var tempMax = document.forms['params']['tempMax'].value;" +
                            "var tempMin = document.forms['params']['tempMin'].value;" +
                            "var time = document.forms['params']['time'].value;" +
                            "var displayRefresh = document.forms['params']['displayRefresh'].value;" +
                            "var refresh = document.forms['params']['refresh'].value;" +
                            "var pass = document.forms['params']['pass'].value;" +
                            "location.href = 'setparams?tempMax=' + tempMax + '&tempMin=' + tempMin + '&displayRefresh=' + displayRefresh + '&refresh=' + refresh + '&time=' + time + '&pass=' + pass;" +
                            "}} " +
                            "function temp(){{" +
                            "console.log(\"Calling temp in JS!!\");" +
                            "location.href = 'temp'" +
                            "}} " +
                            "function start(){{location.href = 'start'}}" +
                            "function stop(){{location.href = 'stop'}}" +
                            "</script>" +

                            "<div class='tm-main-content' id='top'>" +
                            "<div class='tm-top-bar-bg'></div>" +
                            "<div class='container'>" +
                            "<div class='row'>" +
                            "<nav class='navbar navbar-expand-lg narbar-light'>" +
                            "</nav>" +
                            "</div>" +
                            "</div>" +
                            "</div>" +
                            "<div class='tm-section tm-bg-img' id='tm-section-1'>" +
                            "<div class='tm-bg-white ie-container-width-fix-2'>" +
                            "<div class='container ie-h-align-center-fix'>" +
                            "<div class='row'>" +
                            "<div class='col-xs-12 ml-auto mr-auto ie-container-width-fix'>" +
                            "<form name='params' method = 'get' class='tm-search-form tm-section-pad-2'>" +
                            "<div class='form-row tm-search-form-row'>" +
                            "<div class='form-group tm-form-element tm-form-element-100'>" +
                            "<p>Temperatura Max <b>(&deg;C)</b> <input name='tempMax' type='text' class='form-control' value='" + mostarDatos(Data.temp_max) + "' " + disabled + "></input></p>" +
                            "</div>" +
                            "<div class='form-group tm-form-element tm-form-element-50'>" +
                            "<p>Temperatura Min <b>(&deg;C)</b> <input name='tempMin' type='text' class='form-control' value='" + mostarDatos(Data.temp_min) + "' " + disabled + "></input></p>" +
                            "</div>" +
                            "<div class='form-group tm-form-element tm-form-element-50'>" +
                            "<p>Duraci&oacute;n Ronda <b>(s)</b> <input name='time' type='text' class='form-control' value='" + mostarDatos(Data.round_time) + "' " + disabled + "></input></p>" +
                            "</div>" +
                            "</div>" +
                            "<div class='form-row tm-search-form-row'>" +
                            "<div class='form-group tm-form-element tm-form-element-100'>" +
                            "<p>Cadencia Refresco <b>(ms)</b> <input name='displayRefresh' type='number' class='form-control' value='" + Data.display_refresh + "' " + disabled + "></input></p>" +
                            "</div>" +
                            "<div class='form-group tm-form-element tm-form-element-50'>" +
                            "<p>Cadencia Interna <b>(ms)</b> <input name='refresh' type='number' class='form-control' value='" + Data.refresh + "' " + disabled + "></input></p>" +
                            "</div>" +
                            "<div class='form-group tm-form-element tm-form-element-50'>" +
                            "<p>Contrase&ntilde;a <input name='pass' type='password' class='form-control'> </input></p>" +
                            "</div>" +

                            "</form>" +
                            "<div class='form-group tm-form-element tm-form-element-50'>" +
                            save + start + stop +
                            "</div>" +
                            "<div class='form-group tm-form-element tm-form-element-50'>" +
                            temp +
                            "</div>" +
                            "<p style = \"font-weight:bold;\" id = \"status\"> Estado actual...</p>" +

                            "</div>" +
                            "<p style='text-align:center; display:flex; justify-content:center; align-items:center;' id='status'>" + message + "</p>" +
                            "</div>" +
                            "</div>" +
                            "</div>" +
                            "</div>" +
                            "</div>" +

                            "<div class='container ie-h-align-center-fix'>" +
                            //graph +
                            "</div>" +
            "</body>" +
            "</html>";
            return html;
        }
    }
}

