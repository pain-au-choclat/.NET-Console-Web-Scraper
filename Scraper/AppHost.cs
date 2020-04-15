using FluentEmail.Core;
using Microsoft.Extensions.Options;
using Scraper.Interfaces;
using Scraper.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scraper
{
    public class AppHost : IAppHost
    {
        private readonly IScraper _scraper;
        private readonly ICompare _compare;
        private readonly ScraperConfiguration _config;
        private readonly IFluentEmailFactory _fluentEmail;

        public AppHost(IScraper scraper, ICompare compare, IOptions<ScraperConfiguration> config, IFluentEmailFactory fluentEmail)
        {
            _scraper = scraper;
            _compare = compare;
            _config = config.Value;
            _fluentEmail = fluentEmail;
        }

        /// <summary>
        /// Provides a basic console selection for either scraping or comparing
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public async Task Run(string[] args)
        {
            if (_config.RunScheduled)
            {
                await RunScheduled().ConfigureAwait(false);
                Environment.Exit(0);
            }

            // Start Ui
            Console.WriteLine("");
            Console.WriteLine("Welcome to the one and only truly amazing .NET Core Web scraping monitoring tool...");
            Console.WriteLine("Please return to proceed...");
            Console.ReadLine();

            var option = "";
            while (option != "1" && option != "2")
            {
                Console.Clear();
                Console.WriteLine("");
                Console.WriteLine("Please select your option...");
                Console.WriteLine("1. Start new web scrape");
                Console.WriteLine("2. Start web comparison");
                Console.WriteLine("");
                Console.WriteLine("Q. To exit this beautiful tool");
                option = Console.ReadLine();

                if (option == "q")
                {
                    break;
                }
            }

            if (option == "1")
            {
                var (result, count) = await _scraper.StartScrape().ConfigureAwait(false);
                if (result)
                {
                    Console.Clear();
                    Console.WriteLine("");
                    Console.WriteLine("Web scrape completed successfully!");
                    Console.WriteLine($"{count} Url parsed successfully!");
                    Console.WriteLine("");
                    Console.WriteLine("Please return to proceed...");
                    Console.ReadLine();

                    if (_config.SendOutputEmails)
                    {   
                        if (_config.ConsoleLogging)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("Sending successful web scrape email...");
                        }

                        var subject = $"Web scrape of {_config.RootUrl} completed successfully!";
                        await SendEmail(subject, new ComparisonModel(), count).ConfigureAwait(false);
                    }
                }
                else
                {
                    Console.WriteLine("");
                    Console.WriteLine("Unable to complete web scrape successfully!");
                    Console.WriteLine("Please ensure appsettings.json is configured correctly!");
                    Console.WriteLine("Please return to exit...");
                    Console.ReadLine();
                }
            }

            if (option == "2")
            {
                Console.Clear();
                Console.WriteLine("");
                Console.WriteLine("Please type the first of folder names you wish to compare...");
                var FolderOne = Console.ReadLine();

                while (!Directory.Exists($"{_config.FilePath}\\{FolderOne}"))
                {
                    Console.Clear();
                    Console.WriteLine("");
                    Console.WriteLine("Please type the name again...");
                    FolderOne = Console.ReadLine();
                }

                Console.Clear();
                Console.WriteLine("");
                Console.WriteLine("Please type the second of folder names you wish to compare...");
                var FolderTwo = Console.ReadLine();

                while (!Directory.Exists($"{_config.FilePath}\\{FolderTwo}"))
                {
                    Console.Clear();
                    Console.WriteLine("");
                    Console.WriteLine("Please type the name again...");
                    FolderTwo = Console.ReadLine();
                }

                Console.Clear();
                Console.WriteLine("");
                Console.WriteLine("Two folders have been selected...");
                Console.WriteLine("Please press return to begin comparison...");
                Console.ReadLine();

                Console.Clear();
                Console.WriteLine("");
                Console.WriteLine("Beginning folder comparison...");
                Console.WriteLine("");

                var comparison = _compare.StartCompare($"{_config.FilePath}\\{FolderOne}",$"{_config.FilePath}\\{FolderTwo}");
                if (comparison.IsComparisonComplete)
                {
                    if (_config.SendOutputEmails)
                    {
                        if (_config.ConsoleLogging)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("Sending successful comparison email...");
                        }

                        var subject = "Web scraper has completed comparison";
                        await SendEmail(subject, comparison, 0).ConfigureAwait(false);
                    }
                }
                else
                {
                    Console.WriteLine("");
                    Console.WriteLine("Unable to complete comparison successfully!");
                    Console.WriteLine("Please ensure appsettings.json is configured correctly!");
                    Console.WriteLine("Please return to exit...");
                    Console.ReadLine();
                }

                Environment.Exit(0);
            }      
        }

        /// <summary>
        /// Runs scrape and compare as scheduled using win services
        /// </summary>
        /// <returns></returns>
        public async Task RunScheduled()
        {
            // Scheduled scrape
            var (result, count) = await _scraper.StartScrape().ConfigureAwait(false);
            if (_config.ConsoleLogging && result)
            {
                Console.WriteLine("Web scrape completed successfully!");
            }

            // Send email marking scrape completion
            if (_config.SendOutputEmails && result)
            {
                if (_config.ConsoleLogging)
                {
                    Console.WriteLine("");
                    Console.WriteLine("Sending web scrape completetion email...");
                }

                var subject = $"Web scrape of {_config.RootUrl} completed successfully!";
                await SendEmail(subject, new ComparisonModel(), count).ConfigureAwait(false);
            }

            // If two scraper files exist auto compare
            if (Directory.Exists($"{_config.FilePath}\\originalFolder") && Directory.Exists($"{_config.FilePath}\\latestFolder"))
            {
                if (_config.ConsoleLogging)
                {
                    Console.Clear();
                    Console.WriteLine("Beginning folder comparison...");
                }

                var comparison = _compare.StartCompare($"{_config.FilePath}\\originalFolder", $"{_config.FilePath}\\latestFolder");

                if (_config.ConsoleLogging && comparison.IsComparisonComplete)
                {
                    Console.WriteLine("Web comparison completed successfully");
                }

                // Send email marking comparison complete and differences
                if (_config.SendOutputEmails && comparison.IsComparisonComplete)
                {
                    if (_config.ConsoleLogging)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("Sending web scrape completetion email...");
                    }

                    var subject = "Web scraper has completed comparison";
                    await SendEmail(subject, comparison, 0).ConfigureAwait(false);
                }

                // Delete original scrape, rename latestFolder
                if (_config.ConsoleLogging)
                {
                    Console.WriteLine("");
                    Console.WriteLine("Deleting original folder and renaming latest folder...");
                }
                Directory.Delete($"{_config.FilePath}\\orignalFolder");
                Directory.Move($"{_config.FilePath}\\latestFolder", $"{_config.FilePath}\\orignalFolder");
                if (_config.ConsoleLogging)
                {
                    Console.WriteLine("Complete...");
                }
            }
        }

        /// <summary>
        /// Generic send email, using fluentemail and razor templates
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="comparison"></param>
        /// <param name="urlCount"></param>
        public async Task SendEmail(string subject, ComparisonModel comparison, int urlCount)
        {
            var emailTemplate = urlCount > 0 ? "Scrape.cshtml" : "Comparison.cshtml";

            var currentPath = Directory.GetCurrentDirectory();
            if (!File.Exists(Path.Combine(currentPath, "EmailTemplates", emailTemplate)))
            {
                var relativePath = Path.Combine(currentPath, @"..\..\..", "EmailTemplates");
                currentPath = Path.GetFullPath(relativePath);
            }
            else
            {
                currentPath = Path.Combine(currentPath, "EmailTemplates");
            }
            var templatePath = Path.Combine(currentPath, emailTemplate);

            var model = new EmailModel()
            {
                FilesAdded = comparison.FilesAdded?.ToList() ?? new List<string>(),
                FilesRemoved = comparison.FilesRemoved?.ToList() ?? new List<string>(),
                FileNamesList = comparison.FileNamesList?.ToList() ?? new List<string>(),
                LinesAddedToLatest = comparison.LinesAddedToLatest?.ToList() ?? new List<string>(),
                LinesRemovedFromOriginal = comparison.LinesRemovedFromOriginal?.ToList() ?? new List<string>(),
                DateTime = DateTime.Now,
                RootUrl = _config.RootUrl,
                Urls = urlCount
            };

            try
            {
                var email = await _fluentEmail.Create()
                    .SetFrom(_config.FromEmail)
                    .To(_config.ToEmail)
                    .Subject(subject)
                    .UsingTemplateFromFile(templatePath, model, true)
                    .SendAsync().ConfigureAwait(false);

                if (email.Successful)
                {
                    if (_config.ConsoleLogging)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("Web scraper successfully sent email!");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("");
                Console.WriteLine("Sending email failed, please ensure appsettings.json is configured correctly.");
                Console.WriteLine("");
                Console.WriteLine($"Error message: {e}");
                Console.WriteLine("");
                Console.WriteLine("Please return to exit...");
                Console.ReadLine();
            }
        }
    }
}
