using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PoolAutomation.Helpers;

namespace PoolAutomation.Classes
{
    public class AquaConnect
    {
        public Dictionary<string, LedState> KeyStates { get; set; }

        public double PoolTemperature { get; set; }

        public DateTime? PoolTemperatureAsOf { get; set; }

        public double SpaTemperature { get; set; }

        public DateTime? SpaTemperatureAsOf { get; set; }

        public double AirTemperature { get; set; }

        public bool IsHeaterInAutoControl { get; set; }

        //Is disabled if the Menu-Locked or System Locked
        public bool IsDisabled { get; set; }

        [JsonIgnore]
        public PoolMode Mode
        {
            get
            {
                if (this.GetKeyState(Constants.POOL_KEY, LedState.WEBS_OFF) == LedState.WEBS_ON)
                {
                    return PoolMode.Pool;
                }
                else if (this.GetKeyState(Constants.SPA_KEY, LedState.WEBS_OFF) == LedState.WEBS_ON)
                {
                    return PoolMode.Spa;
                }
                else if (this.GetKeyState(Constants.SPILLOVER_KEY, LedState.WEBS_OFF) == LedState.WEBS_ON)
                {
                    return PoolMode.Spillover;
                }

                return PoolMode.Off;
            }
            set
            {
                switch (value)
                {
                    case PoolMode.Off:
                        SetKeyState(Constants.POOL_KEY, LedState.WEBS_OFF);
                        SetKeyState(Constants.SPA_KEY, LedState.WEBS_OFF);
                        SetKeyState(Constants.SPILLOVER_KEY, LedState.WEBS_OFF);
                        break;

                    case PoolMode.Pool:
                        SetKeyState(Constants.POOL_KEY, LedState.WEBS_ON);
                        SetKeyState(Constants.SPA_KEY, LedState.WEBS_OFF);
                        SetKeyState(Constants.SPILLOVER_KEY, LedState.WEBS_OFF);
                        break;

                    case PoolMode.Spa:
                        SetKeyState(Constants.POOL_KEY, LedState.WEBS_OFF);
                        SetKeyState(Constants.SPA_KEY, LedState.WEBS_ON);
                        SetKeyState(Constants.SPILLOVER_KEY, LedState.WEBS_OFF);
                        break;

                    case PoolMode.Spillover:
                        SetKeyState(Constants.POOL_KEY, LedState.WEBS_OFF);
                        SetKeyState(Constants.SPA_KEY, LedState.WEBS_OFF);
                        SetKeyState(Constants.SPILLOVER_KEY, LedState.WEBS_ON);
                        break;
                }
            }
        }

        [JsonIgnore]
        public FilterMode FilterMode
        {
            get
            {
                if (this.GetKeyState(Constants.FILTER_KEY, LedState.WEBS_OFF) == LedState.WEBS_ON)
                {
                    return FilterMode.High;
                }
                //BLINK is always set in Low mode
                //AUX1 is only set after automation has gone from high down to low, usually when switching from pool to spa
                else if ((this.GetKeyState(Constants.FILTER_KEY, LedState.WEBS_OFF) == LedState.WEBS_BLINK) ||
                         (this.GetKeyState(Constants.AUX1_KEY, LedState.WEBS_OFF) == LedState.WEBS_ON))
                {
                    return FilterMode.Low;
                }

                return FilterMode.Off;
            }
            set
            {
                switch (value)
                {
                    case FilterMode.Off:
                        SetKeyState(Constants.FILTER_KEY, LedState.WEBS_OFF);
                        break;

                    case FilterMode.Low: //TFCD5C333333/DVCD4C333333 (Pool+Filter Low)
                        SetKeyState(Constants.FILTER_KEY, LedState.WEBS_BLINK);
                        SetKeyState(Constants.AUX1_KEY, LedState.WEBS_ON);
                        break;

                    case FilterMode.High: //TECD4C333333 (Pool+Filter High)
                        SetKeyState(Constants.FILTER_KEY, LedState.WEBS_ON);
                        SetKeyState(Constants.AUX1_KEY, LedState.WEBS_OFF);
                        break;
                }
            }
        }

        [JsonIgnore]
        public bool IsLightsOn
        {
            get { return this.GetKeyState(Constants.LIGHTS_KEY, LedState.WEBS_OFF) == LedState.WEBS_ON; }
            set { this.SetKeyState(Constants.LIGHTS_KEY, value ? LedState.WEBS_ON : LedState.WEBS_OFF); }
        }

        [JsonIgnore]
        public bool IsHeaterOn { get { return this.GetKeyState(Constants.HEATER1_KEY, LedState.WEBS_OFF) == LedState.WEBS_ON; } }

        public string DisplayLineOne { get; set; }

        public string DisplayLineTwo { get; set; }

        public string Message { get; set; }

        public AquaConnect() => KeyStates = new Dictionary<string, LedState>();

        public AquaConnect(Dictionary<string, LedState> keyStates) => this.KeyStates = keyStates;

        private LedState GetKeyState(string key, LedState defaultState)
        {
            if (!KeyStates.TryGetValue(key, out var state))
            {
                state = defaultState;
            }

            return state;
        }

        private void SetKeyState(string key, LedState state)
        {
            if (KeyStates.ContainsKey(key))
            {
                KeyStates[key] = state;
            }
            else
            {
                KeyStates.Add(key, state);
            }
        }

        public static void Searlize(AquaConnect obj)
        {
            var serializer = new Newtonsoft.Json.JsonSerializer();

            using (StreamWriter sw = new StreamWriter(Global.AquaConnectCache))
            {
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, obj);
                }
            }
        }
    }
}