using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PoolAutomation.Classes;
using PoolAutomation.Helpers;

namespace PoolAutomation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.ConfigureEndpointDefaults(listenOptions => listenOptions.UseConnectionLogging());
                    });

                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var dir = hostContext.Configuration.GetValue<string>("CacheDir");
                    if (!string.IsNullOrEmpty(dir))
                    {
                        Global.CACHE_DIR = dir;
                    }

                    var aquaConnectIp = hostContext.Configuration.GetValue<string>("AquaConnectIp");
                    if (!string.IsNullOrEmpty(aquaConnectIp))
                    {
                        Constants.WEBSTR_IP = aquaConnectIp;
                    }

                    services
                        .AddSingleton<WebStrService>()
                        .AddSingleton<AquaConnect>((sp) =>
                        {
                            if (File.Exists(Global.AquaConnectCache))
                            {
                                var pool = JsonConvert.DeserializeObject<AquaConnect>(File.ReadAllText(Global.AquaConnectCache));

                                return pool;
                            }
                            else
                            {
                                return new AquaConnect();
                            }
                        })
                        .AddHostedService<WebStrBackgroundService>();
                })
                .ConfigureLogging(logging =>
                {
                    // clear default logging providers
                    logging.ClearProviders();

                    // add built-in providers manually, as needed 
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.AddEventLog();
                });
    }
}
