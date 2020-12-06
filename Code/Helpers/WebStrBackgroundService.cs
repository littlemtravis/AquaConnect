using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PoolAutomation.Classes;

namespace PoolAutomation.Helpers
{
    public class WebStrBackgroundService : BackgroundService
    {
        private readonly ILogger logger;
        private readonly WebStrService webStrService;
        private readonly AquaConnect pool;

        public WebStrBackgroundService(ILogger<WebStrBackgroundService> logger, WebStrService webStrService, AquaConnect pool)
        {
            this.logger = logger;
            this.webStrService = webStrService;
            this.pool = pool;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogDebug("Staring");

            stoppingToken.Register(() => logger.LogInformation("Token signaling service to stop."));

            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogDebug("Getting WebStr Response");

                try
                {
                    await GetWebStrResponse();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"GetWebStrResponse failedn at {DateTime.Now}");
                }

                await Task.Delay(TimeSpan.FromSeconds(Constants.BACKGROUND_WORKER_DELAY_SEC), stoppingToken);
            }

            logger.LogDebug("Stopping");
        }

        private async Task GetWebStrResponse()
        {
            var result = await webStrService.GetWebStrReponseAsync();

            if (!webStrService.ProcessingStateChange)
            {
                var keyState = webStrService.ProcessRawLedData(result.LineThree);

                pool.KeyStates = keyState;
            }

            pool.IsDisabled = result.IsConfigMenuLocked || result.IsServiceMode;
            pool.DisplayLineOne = result.LineOne;
            pool.DisplayLineTwo = result.LineTwo;

            if (result.AirTemp.HasValue)
            {
                pool.AirTemperature = result.AirTemp.Value;
            }

            if (result.PoolTemp.HasValue)
            {
                pool.PoolTemperature = result.PoolTemp.Value;
                pool.PoolTemperatureAsOf = DateTime.Now;
            }

            if (result.SpaTemp.HasValue)
            {
                pool.SpaTemperature = result.SpaTemp.Value;
                pool.SpaTemperatureAsOf = DateTime.Now;
            }

            if (result.IsHeaterInAutoControl.HasValue)
            {
                pool.IsHeaterInAutoControl = result.IsHeaterInAutoControl.Value;
            }

            AquaConnect.Searlize(pool);
        }
    }
}