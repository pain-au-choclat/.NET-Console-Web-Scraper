using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scraper.Interfaces;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Scraper
{
    static class Program
    {
        public static async Task Main(string[] args)
        {
            const string jsonFileName = "appsettings.json";

            var currentPath = Directory.GetCurrentDirectory();

            var host = new HostBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile(jsonFileName, false);
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<ScraperConfiguration>(hostContext.Configuration.GetSection("scraperSettings:settings"));

                services.AddSingleton<IAppHost, AppHost>();

                services.AddSingleton<List<string>>();

                services.AddSingleton<IScraper, Scraper>();

                services.AddHttpClient<IScraper, Scraper>();

                services.AddSingleton<ICompare, Compare>();

                services.AddSingleton<IHtmlToText, HtmlToText>();

                services.AddFluentEmail("")
                .AddRazorRenderer(Path.Join(currentPath, "EmailTemplates"))
                .AddSmtpSender(
                    host: hostContext.Configuration.GetValue<string>("scraperSettings:settings:smtpHost"),
                    port: int.Parse(hostContext.Configuration.GetValue<string>("scraperSettings:settings:port")),
                    username: hostContext.Configuration.GetValue<string>("scraperSettings:settings:username"),
                    password: hostContext.Configuration.GetValue<string>("scraperSettings:settings:password")
                    );

                services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
            })
            .UseConsoleLifetime()
            .Build();

            await host.Services.GetService<IAppHost>().Run(args);

            //await host.RunAsync().ConfigureAwait(false);
        }    
    }
}
