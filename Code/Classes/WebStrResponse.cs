using System;

namespace PoolAutomation.Classes
{
    public class WebStrResponse
    {
        public string LineOne { get; set; }

        public string LineTwo { get; set; }

        public string LineThree { get; set; }

        public bool IsConfigMenuLocked { get; set; }

        public bool IsServiceMode { get; set; }
        
        public bool? IsHeaterInAutoControl { get; internal set; }

        public int? AirTemp
        {
            get
            {
                if (LineOne.Contains("Air Temp", StringComparison.OrdinalIgnoreCase))
                {
                    //e.g.	Air Temp 80&#176F 
                    var splits = LineOne.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var tempLabel = splits[2];

                    //split on the degree symbol
                    var temp = tempLabel.Split("&#176")[0];

                    return Convert.ToInt32(temp);
                }

                return null;
            }
        }

        public int? PoolTemp
        {
            get
            {
                if (LineOne.Contains("Pool Temp", StringComparison.OrdinalIgnoreCase))
                {
                    //e.g.	Pool Temp 80&#176F 
                    var splits = LineOne.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var tempLabel = splits[2];

                    //split on the degree symbol
                    var temp = tempLabel.Split("&#176")[0];

                    return Convert.ToInt32(temp);
                }

                return null;
            }
        }

        public int? SpaTemp
        {
            get
            {
                if (LineOne.Contains("Spa Temp", StringComparison.OrdinalIgnoreCase))
                {
                    //e.g.	Spa Temp 80&#176F 
                    var splits = LineOne.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var tempLabel = splits[2];

                    //split on the degree symbol
                    var temp = tempLabel.Split("&#176")[0];

                    return Convert.ToInt32(temp);
                }

                return null;
            }
        }
    }
}
