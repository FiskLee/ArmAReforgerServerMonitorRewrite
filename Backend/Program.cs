using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ArmaReforgerServerMonitor.Backend
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/backend_log.json", rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Setup.ConfigurationSetup.RunSetup();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Setup failed.");
                return;
            }

            var host = CreateHostBuilder(args).Build();

            var logProcessor = host.Services.GetRequiredService<LogProcessor>();
            logProcessor.Start();

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<LogProcessor>();
                    services.AddSingleton<OSDataCollector>();
                    services.AddDbContext<DatabaseContext>();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                              .UseUrls("http://0.0.0.0:5000");
                });
    }
}
