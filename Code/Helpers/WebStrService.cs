using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PoolAutomation.Classes;

namespace PoolAutomation.Helpers
{
    /*All of the following code is from the WebFuncs.js.  This file is accessible
    using the developer menu (F12) when navigating to the "server" via a browser.*/
    public class WebStrService
    {
        readonly string[] MENU_CONFIG_LOCKED = new string[2] { "Configuration", "Menu-Locked" };

        readonly string[] SERVICE_MODE_LOCKED = new string[2] { "Service Mode", "System Locked" };

        readonly string[] HEATER_AUTO_CONTROL = new string[2] { "Heater1", "Auto Control" };

        readonly string[] HEATER_MANUAL_OFF = new string[2] { "Heater1", "Manual Off" };

        private readonly ILogger<WebStrService> logger;

        public bool ProcessingStateChange { get; private set; }

        public WebStrService(ILogger<WebStrService> logger)
        {
            this.logger = logger;
        }

        public async Task<WebStrResponse> GetWebStrReponseAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                var content = new StringContent("Update Local Server&");
                var response = await client.PostAsync($"http://{Constants.WEBSTR_IP}/WNewSt.htm", content);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var resp = await response.Content.ReadAsStringAsync();

                    var webStrResponse = ProcessWebRequest(resp);

                    return webStrResponse;
                }
            }

            return new WebStrResponse();
        }

        private WebStrResponse ProcessWebRequest(string response)
        {
            //only needs the contents of the body.
            var respBody = response.Split("<body>")[1];

            //There is some code in the WebStr service that says the trailing
            //  body might now show up, not sure if that will happen or not
            if (respBody.Contains("</body>"))
            {
                respBody = respBody.Split("</body>")[0];
            }

            //
            // Separate the body into Line One, Line Two, and the raw 
            // set of characters which encode the LED states. 
            // 
            var bodySplit = respBody.Split("xxx");
            var lineOne = bodySplit[0].Trim();
            var lineTwo = bodySplit[1].Trim();

            bool isConfigLocked = false;
            bool isServiceMode = false;

            bool? isHeaterInAutoControl = null;

            //
            // Evaluate the display strings against the configuration locked strings. 
            // If the strings match, set the variable that is used to signal that the
            // server should accept the unlock input. 
            // 
            if (LineEquals(lineOne, lineTwo, MENU_CONFIG_LOCKED))
            {
                isConfigLocked = true;
            }
            else if (LineEquals(lineOne, lineTwo, SERVICE_MODE_LOCKED))
            {
                isConfigLocked = true;
            }

            if (LineEquals(lineOne, lineTwo, HEATER_AUTO_CONTROL))
            {
                isHeaterInAutoControl = true;
            }
            else if (LineEquals(lineOne, lineTwo, HEATER_MANUAL_OFF))
            {
                isHeaterInAutoControl = false;
            }

            var respRawLeds = bodySplit[2].Trim();

            return new WebStrResponse
            {
                IsConfigMenuLocked = isConfigLocked,
                IsServiceMode = isServiceMode,
                IsHeaterInAutoControl = isHeaterInAutoControl,
                LineOne = lineOne,
                LineTwo = lineTwo,
                LineThree = respRawLeds
            };
        }

        private bool LineEquals(string lineOne, string lineTwo, string[] lineText)
        {
            return String.Equals(lineOne, lineText[0], StringComparison.InvariantCultureIgnoreCase) &&
                   String.Equals(lineTwo, lineText[1], StringComparison.InvariantCultureIgnoreCase);
        }

        public async Task ProcessStateChangeAsync(AquaConnect currentState, string attribute, string newState)
        {
            int buttonPresses = 1;
            string key = string.Empty;

            try
            {
                ProcessingStateChange = true;

                var startTime = DateTime.Now;

                logger.Log(LogLevel.Information, $"Processing state change STARTED(StartTime: {startTime}).");

                // also changes currentState to match the intended final state
                GetKeyToChangeState(currentState, attribute, newState, ref buttonPresses, ref key, out var message);

                currentState.Message = message;

                logger.Log(LogLevel.Information, $"Processing state change: {key} X {buttonPresses}.");

                //don't let anything change if in service mode, or menu locked
                if (!currentState.IsDisabled && key.Length > 0)
                {
                    //press the pool button
                    var success = await PostWebStrKeyAsync(key);

                    //press the pool button again to change to the 
                    //next state since we want to skip the previous state
                    if (buttonPresses == 2 && success)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Constants.PROCESS_CHANGE_DELAY_SEC));

                        await PostWebStrKeyAsync(key);
                    }
                }
                logger.Log(LogLevel.Information, $"Processing state change FINISHED(StartTime: {startTime}).");
            }
            finally
            {
                currentState.Message = string.Empty;

                ProcessingStateChange = false;
            }
        }

        private void GetKeyToChangeState(AquaConnect currentState, string attribute, string newState, ref int buttonPresses, ref string key, out string message)
        {
            message = "";

            switch (attribute.ToLower())
            {
                case "lights":
                    //if the lights are off, turn them on
                    //if the lights are on, turn them off
                    //there is also colors, but not sure on the sequence yet
                    if ((currentState.IsLightsOn == false && newState.Equals("on", StringComparison.InvariantCultureIgnoreCase)) ||
                        (currentState.IsLightsOn == true && newState.Equals("off", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        message = $"Ligths: {(currentState.IsLightsOn ? "on" : "off")} -> {newState}";

                        currentState.IsLightsOn = newState.Equals("on", StringComparison.InvariantCultureIgnoreCase);

                        key = "09";
                    }
                    logger.Log(LogLevel.Information, "Processing state change: Lights.");
                    break;
                case "poolmode":
                    var newPoolMode = Enum.Parse<PoolMode>(newState, true);

                    //don't change anything if we are already in "new" state
                    if (currentState.Mode == newPoolMode)
                    {
                        break;
                    }

                    key = "07";

                    message = $"Mode: {currentState.Mode}";

                    switch (currentState.Mode)
                    {
                        case PoolMode.Pool:
                            message = $"{message} -> {PoolMode.Spa}";
                            if (newPoolMode == PoolMode.Spillover)
                            {
                                message = $"{message} -> {newPoolMode}";
                                buttonPresses = 2;
                            }
                            break;
                        case PoolMode.Spa:
                            message = $"{message} -> {PoolMode.Spillover}";
                            if (newPoolMode == PoolMode.Pool)
                            {
                                message = $"{message} -> {newPoolMode}";
                                buttonPresses = 2;
                            }
                            break;
                        case PoolMode.Spillover:
                            message = $"{message} -> {PoolMode.Pool}";
                            if (newPoolMode == PoolMode.Spillover)
                            {
                                message = $"{message} -> {newPoolMode}";
                                buttonPresses = 2;
                            }
                            break;
                    }

                    logger.Log(LogLevel.Information, $"Processing state change: Mode - {currentState.Mode} -> {newPoolMode}.");

                    //change cached state
                    currentState.Mode = newPoolMode;

                    break;
                case "filtermode":
                    var newFilterMode = Enum.Parse<FilterMode>(newState, true);

                    //don't change anything if we are already in "new" state
                    if (currentState.FilterMode == newFilterMode)
                    {
                        break;
                    }

                    key = "08";

                    message = $"Speed: {currentState.FilterMode}";

                    switch (currentState.FilterMode)
                    {
                        case FilterMode.High:
                            message = $"{message} -> {FilterMode.Low}";
                            if (newFilterMode == FilterMode.Off)
                            {
                                message = $"{message} -> {newFilterMode}";
                                buttonPresses = 2;
                            }
                            break;
                        case FilterMode.Low:
                            message = $"{message} -> {FilterMode.Off}";
                            if (newFilterMode == FilterMode.High)
                            {
                                message = $"{message} -> {newFilterMode}";
                                buttonPresses = 2;
                            }
                            break;
                        case FilterMode.Off:
                            message = $"{message} -> {FilterMode.High}";
                            if (newFilterMode == FilterMode.Low)
                            {
                                message = $"{message} -> {newFilterMode}";
                                buttonPresses = 2;
                            }
                            break;
                    }

                    logger.Log(LogLevel.Information, $"Processing state change: Filter - {currentState.FilterMode} -> {newFilterMode}.");

                    //change cached state
                    currentState.FilterMode = newFilterMode;
                    break;
                case "heater-auto":
                    if ((currentState.IsHeaterInAutoControl == false && newState.Equals("on", StringComparison.InvariantCultureIgnoreCase)) ||
                        (currentState.IsHeaterInAutoControl == true && newState.Equals("off", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        message = $"Heater-Auto: {(currentState.IsHeaterInAutoControl ? "on" : "off")} -> {newState}";

                        currentState.IsHeaterInAutoControl = newState.Equals("on", StringComparison.InvariantCultureIgnoreCase);

                        key = "13";
                    }
                    logger.Log(LogLevel.Information, "Processing state change: Heater-Auto.");
                    break;
            }
        }

        private async Task<bool> PostWebStrKeyAsync(string keyNum)
        {
            using (HttpClient client = new HttpClient())
            {
                var keyString = $"KeyId={keyNum}&";

                var content = new StringContent(keyString);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                content.Headers.ContentLength = keyString.Length;
                client.DefaultRequestHeaders.Connection.Add("close");

                var response = await client.PostAsync($"http://{Constants.WEBSTR_IP}/WNewSt.htm", content);

                return response.StatusCode == HttpStatusCode.OK;
            }
        }

        public Dictionary<string, LedState> ProcessRawLedData(string respRawLeds)
        {
            var keyIdx = 0;

            var result = new Dictionary<string, LedState>(24);

            //
            // Extract the nibbles from the raw data and convert them into the corresponding
            // class which will be used to control the display mode for the LED.   
            // 
            for (int i = 0; i < ((respRawLeds.Length)); i++)
            {
                var oneCharData = respRawLeds[i];

                //
                // Convert the ASCII byte into the format which has each 
                // nibble represented as an ASCII byte.   For instance, the 
                // ASCII byte '3' which is made up of the binary data 0x34 
                // will be converted into a two byte ACSII array that has 
                // '3' and '4' as its contents.   
                //
                var twoCharData = ExtractNibbles(oneCharData);

                var highKey = $"Key_{keyIdx++:00}";
                var lowKey = $"Key_{keyIdx++:00}";

                var highNib = twoCharData[0];
                var lowNib = twoCharData[1];

                result.Add(highKey, DecodeRawLedData(highNib));

                if (i == (respRawLeds.Length - 1))
                {
                    // ProcessControlNibble(LowNib);

                    // Make sure that the key does not get displayed.  
                    result.Add(lowKey, LedState.WEBS_NOKEY);
                }
                else
                {
                    result.Add(lowKey, DecodeRawLedData(lowNib));
                }
            }

            return result;
        }

        //
        // This function will convert an ASCII byte into a string which contains the 
        // two nibbles used for the input byte.  For instance, the byte '3' will be 
        // converted to "33" which contains string representation of its binary 
        // data.  The function is used to process the data used to encode key presses
        // and only convert a limited range of data. 
        //
        public string ExtractNibbles(char nibble)
        {
            switch (nibble)
            {
                case '3':
                    return "33";
                case '4':
                    return "34";
                case '5':
                    return "35";
                case '6':
                    return "36";

                case 'C':
                    return "43";
                case 'D':
                    return "44";
                case 'E':
                    return "45";
                case 'F':
                    return "46";

                case 'S':
                    return "53";
                case 'T':
                    return "54";
                case 'U':
                    return "55";
                case 'V':
                    return "56";

                case 'c':
                    return "63";
                case 'd':
                    return "64";
                case 'e':
                    return "65";
                case 'f':
                    return "66";

                default:
                    return "00";
            }
        }

        public LedState DecodeRawLedData(char nibData)
        {
            switch (nibData)
            {
                case '3':
                    return LedState.WEBS_NOKEY;
                case '4':
                    return LedState.WEBS_OFF;
                case '5':
                    return LedState.WEBS_ON;
                case '6':
                    return LedState.WEBS_BLINK;
                default:
                    return LedState.WEBS_NOKEY;
            }
        }

    }
}