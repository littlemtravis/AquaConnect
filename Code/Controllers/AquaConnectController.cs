using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PoolAutomation.Classes;
using PoolAutomation.Helpers;
using System.Net;
using Microsoft.Extensions.Logging;

namespace PoolAutomation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AquaConnectController : ControllerBase
    {
        private readonly ILogger<AquaConnectController> logger;
        private readonly WebStrService webStrService;
        private readonly AquaConnect pool;

        public AquaConnectController(ILogger<AquaConnectController> logger, WebStrService webStrService, AquaConnect pool)
        {
            this.logger = logger;
            this.webStrService = webStrService;
            this.pool = pool;
        }

        [HttpGet]
        public dynamic Get()
        {
            logger.LogDebug("Get Started");

            var result = new
            {
                PoolMode = pool.Mode.ToString().ToLower(),
                AirTemperature = pool.AirTemperature,
                PoolTemperature = pool.PoolTemperature,
                PoolTemperatureAsOf = pool.PoolTemperatureAsOf?.ToString("HH:mmtt MM/dd/yyyy"),
                SpaTemperature = pool.SpaTemperature,
                SpaTemperatureAsOf = pool.SpaTemperatureAsOf?.ToString("HH:mmtt MM/dd/yyyy"),
                IsDisabled = pool.IsDisabled,
                FilterMode = pool.FilterMode.ToString().ToLower(),
                Lights = pool.IsLightsOn ? "on" : "off",
                Heater = pool.IsHeaterOn ? "on" : "off",
                HeaterAuto = pool.IsHeaterInAutoControl ? "auto" : "off",
                DisplayLineOne = WebUtility.HtmlDecode(pool.DisplayLineOne),
                DisplayLineTwo = WebUtility.HtmlDecode(pool.DisplayLineTwo),
                Message = WebUtility.HtmlDecode(pool.Message ?? string.Empty)
            };

            logger.LogDebug("Get Finished");

            return result;
        }

        [HttpPost]
        public async Task Post([FromBody] EntityState entity)
        {
            await webStrService.ProcessStateChangeAsync(pool, entity.Entity, entity.State);
        }

        [HttpGet("{rawLedData}")]
        public dynamic GetKeyStates(string rawLedData)
        {
            var keyStates = webStrService.ProcessRawLedData(rawLedData);

            var keys = new Dictionary<string, string>();
            keys.Add(Constants.POOL_KEY, "POOL");
            keys.Add(Constants.SPA_KEY, "SPA");
            keys.Add(Constants.SPILLOVER_KEY, "SPILLOVER");
            keys.Add(Constants.FILTER_KEY, "FILTER");
            keys.Add(Constants.LIGHTS_KEY, "LIGHTS");

            keys.Add(Constants.HEATER1_KEY, "HEATER1");
            keys.Add(Constants.VALVE3_KEY, "VALVE3");
            keys.Add(Constants.AUX1_KEY, "AUX1");
            keys.Add(Constants.AUX2_KEY, "AUX2");

            var results = keyStates.ToDictionary(k => k.Key, k => k.Value.ToString());

            return new
            {
                results,
                keys
            };
        }
    }
}