using Microsoft.Extensions.Options;
using Scraper.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scraper
{
    public class Scraper : IScraper
    {
        private readonly HttpClient _httpClient;
        private readonly ScraperConfiguration _config;

        public static List<string> Urls;

        public Scraper(HttpClient httpClient, IOptions<ScraperConfiguration> config, List<string> urls)
        {
            _httpClient = httpClient;
            _config = config.Value;
            Urls = urls;
        }

        /// <summary>
        /// Kicks it all off, creates a date time stamped folder for the content
        /// </summary>
        /// <returns></returns>
        public async Task<(bool, int)> StartScrape()
        {
            var urlList = new List<string>
            {
                _config.RootUrl
            };

            // Create a new folder
            string newDirectoryPath;
            if (_config.RunScheduled)
            {
                if (Directory.Exists($"{_config.FilePath}\\originalFolder"))
                {
                    newDirectoryPath = $"{_config.FilePath}\\latestFolder";
                }
                else
                {
                    newDirectoryPath = $"{_config.FilePath}\\originalFolder";
                }
            }
            else
            {
                // TimeStamped folder if not scheduled
                newDirectoryPath = $"{_config.FilePath}\\{GetPlainUrl(_config.RootUrl)}" +
                $"-{DateTime.Now:dd-MM-yyyy HH-mm-ss}";
            }
            var newDirectory = Directory.CreateDirectory(newDirectoryPath);

            if (_config.ConsoleLogging && !_config.RunScheduled)
            {
                Console.Clear();
                Console.WriteLine("");
                Console.WriteLine($"New folder has been created: {newDirectory}");
                Console.WriteLine("Let us proceed...");
                Console.ReadLine();
            }

            var result = await GetAll(urlList, newDirectory.FullName).ConfigureAwait(false);
            if (result.Count > 0)
            {
                return (true, result.Count());
            }

            return (false, 0);
        }

        /// <summary>
        /// Returns a string that is usable for a filename using the url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string GetPlainUrl(string url)
        {
            var newUrl = "";
            var sb = new StringBuilder();

            if (url.StartsWith("https://"))
            {
                newUrl = url.Substring("https://".Length);
            }

            foreach (var c in newUrl)
            {
                if (c == '/')
                {
                    sb.Append("{FS}");
                }
                else if (c == '?')
                {
                    sb.Append('$');
                }
                else if (c == ':')
                {
                    // Do nothing with these
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets all content, starting at the root url iterating through all urls found via the html content
        /// This method recurses on itself
        /// </summary>
        /// <param name="urls"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<List<string>> GetAll(List<string> urls, string path)
        {
            var newUrls = new List<string>();

            var _stopwatch = new Stopwatch();
            _stopwatch.Start();

            foreach (var url in urls)
            {
                Urls.Add(url);
                
                // Check if limit has been hit
                if (!string.IsNullOrWhiteSpace(_config.UrlLimit))
                {
                    if (int.TryParse(_config.UrlLimit, out int urlLimit))
                    {
                        if (Urls.Count > urlLimit)
                        {
                            if (_config.ConsoleLogging && !_config.RunScheduled)
                            {
                                Console.Clear();
                                Console.WriteLine($"Url limit: {_config.UrlLimit} has been reached. {Urls.Count} Urls have been parsed");
                                Console.WriteLine("Please return to proceed...");
                                Console.ReadLine();
                            }

                            return Urls;
                        }
                    }
                }

                // Check if time limit has been hit
                if (!string.IsNullOrWhiteSpace(_config.TimeLimit) && !_config.IterationBreak)
                {
                    var elapsedString = _stopwatch.Elapsed.ToString("hh\\:mm\\:ss");
                    var elapsedDateTime = DateTime.Parse(elapsedString);

                    var limit = new DateTime();
                    if (DateTime.TryParse(_config.TimeLimit, out limit))
                    {
                        if (elapsedDateTime > limit)
                        {
                            if (_config.ConsoleLogging && !_config.RunScheduled)
                            {
                                Console.Clear();
                                Console.WriteLine($"Time limit: {_config.TimeLimit} has been reached. {Urls.Count} Urls have been parsed");
                                Console.WriteLine("Please return to proceed...");
                                Console.ReadLine();
                            }

                            return Urls;
                        }
                    }
                }

                var httpResponse = new HttpResponseMessage();
                var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
                
                // Add a header to the request if specified in appsettings.json
                if (!string.IsNullOrWhiteSpace(_config.HeaderName) && !string.IsNullOrWhiteSpace(_config.HeaderValue))
                {
                    httpRequest.Headers.Add(_config.HeaderName, _config.HeaderValue);
                    httpRequest.Headers.ConnectionClose = false;
                }
                
                try
                {
                    httpResponse = await _httpClient.SendAsync(httpRequest).ConfigureAwait(false);

                    if (httpResponse.StatusCode == HttpStatusCode.OK)
                    {
                        if (_config.ConsoleLogging)
                        {
                            Console.WriteLine($"Html Response successfull: {url}");
                        }

                        // Deal with urls that point to images
                        if (httpResponse.Content.Headers.ContentType.MediaType.Contains("image"))
                        {
                            if (_config.ConsoleLogging)
                            {
                                Console.WriteLine($"Html Response is an image");
                            }

                            var contentType = httpResponse.Content.Headers.ContentType.MediaType;
                            var streamImage = await _httpClient.GetStreamAsync(url).ConfigureAwait(false);
                            var (imageSaved, imageTitle) = await SaveStreamToDisk(streamImage, url, path, contentType).ConfigureAwait(false);
                            if (_config.ConsoleLogging && imageSaved)
                            {
                                Console.WriteLine($"Image successfully saved: {imageTitle}");
                            }
                        }

                        // Deal with urls that point to application/ i.e javascript
                        if (httpResponse.Content.Headers.ContentType.MediaType.Contains("application"))
                        {
                            if (_config.ConsoleLogging)
                            {
                                Console.WriteLine($"Html response is an {httpResponse.Content.Headers.ContentType.MediaType}");
                            }

                            var contentType = httpResponse.Content.Headers.ContentType.MediaType;
                            var streamApp = await _httpClient.GetStreamAsync(url).ConfigureAwait(false);
                            var (appSaved, appTitle) = await SaveStreamToDisk(streamApp, url, path, contentType).ConfigureAwait(false);
                            if (_config.ConsoleLogging && appSaved)
                            {
                                Console.WriteLine($"App successfully saved: {appTitle}");
                            }
                        }

                        // Deal with urls that result in html or text content
                        var responseBody = "";
                        if (httpResponse.Content.Headers.ContentType.MediaType.Contains("text"))
                        {
                            // Deal with encoded html if need be
                            if (httpResponse.Content.Headers.ContentEncoding.ToString() == "gzip")
                            {
                                using Stream stream = await httpResponse.Content.ReadAsStreamAsync();
                                using Stream decompressed = new GZipStream(stream, CompressionMode.Decompress);
                                using StreamReader reader = new StreamReader(decompressed);
                                responseBody = reader.ReadToEnd();
                            }
                            else
                            {
                                responseBody = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(responseBody))
                        {
                            // Try and get correct file name extensions from url
                            var filePath = $"{path}\\";
                            var urlExtension = url.Split('.');
                            if (urlExtension[^1] != "xml" && urlExtension[^1] != "csv" && urlExtension[^1] != "css" && urlExtension[^1] != "plain"
                                && urlExtension[^1] != "js" && urlExtension[^1] != "html")
                            {
                                filePath += $"{GetPlainUrl(url)}.txt";
                            }
                            else
                            {
                                var urlSplit = url.Split('/');
                                filePath += $"{urlSplit[^1]}";
                            }

                            // Write file to disk
                            using (StreamWriter sw = new StreamWriter(@$"{filePath}"))
                            {
                                await sw.WriteLineAsync(responseBody);
                            }

                            if (_config.ConsoleLogging)
                            {
                                Console.WriteLine($"Html Response is text/html and has been successfully saved");
                            }

                            // Get all urls from current html content
                            var urlsFromHtml = GetPlainUrlsForPage(responseBody);

                            // Check href for "/someurl" urls
                            var urlsFromHref = await CheckHrefsForUrls(filePath).ConfigureAwait(false);

                            if (urlsFromHref.Count > 0)
                            {
                                urlsFromHtml.AddRange(urlsFromHref.ToList().Distinct());
                            }

                            if (urlsFromHtml.Count > 0)
                            {
                                newUrls.AddRange(urlsFromHtml);

                                if (_config.ConsoleLogging)
                                {
                                    Console.WriteLine($"{urlsFromHtml.Count} Urls obtained from current url. {newUrls.Count} Urls obtained in current iteration");
                                }
                            }
                        }
                    }
                    else
                    {
                        if (_config.ConsoleLogging)
                        {
                            Console.WriteLine($"Following status code : {httpResponse.StatusCode} for {url}");
                        }
                    }
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("Http request exception caught!");
                    Console.WriteLine($"Message {e.Message}");
                }
            }

            if (newUrls.Count > 0)
            {
                // Check we are not going over the same urls twice using Urls source of truth
                var clonedList = new List<string>();
                clonedList.AddRange(newUrls);

                foreach (var url in clonedList)
                {
                    if (Urls.Contains(url))
                    {
                        newUrls.Remove(url);
                    }
                }

                _stopwatch.Stop();
                if (_config.ConsoleLogging)
                {
                    Console.WriteLine($"About to begin next iteration...");
                    Console.WriteLine($"Currently parsed: {Urls.Count} urls in {_stopwatch.Elapsed:mm\\:ss\\.ff}. {newUrls.Count} Ready to be parsed");
                }
                if (_config.IterationBreak)
                {
                    var proceed = "";
                    while (proceed != "y")
                    {
                        Console.WriteLine("Press 'y' to continue 'n' to cancel...");
                        proceed = Console.ReadLine();
                        if (proceed == "n")
                        {
                            return Urls;
                        }
                        Console.Clear();
                    }
                }

                // Recursively call GetAll until no more new urls are found
                await GetAll(newUrls, path);
            }

            return Urls;
        }

        /// <summary>
        /// Checks every href "/someurl" urls
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public async Task<ConcurrentBag<string>> CheckHrefsForUrls(string filePath)
        {
            var fileLines = await File.ReadAllLinesAsync(filePath);
            var regexPattern = @"href=\""(.*?)\""";
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            var urls = new ConcurrentBag<string>();

            Parallel.ForEach(fileLines, (line) =>
            {
                if (line.Contains("href=\"/"))
                {
                    var match = regex.Match(line);

                    while (match.Success)
                    {
                        urls.Add(_config.RootUrl + match.Groups[1].Value);

                        match = match.NextMatch();
                    }
                }
            });

            return urls;
        }

        /// <summary>
        /// Saves streams to disk with appropriate file extension. 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="url"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<(bool, string)> SaveStreamToDisk(Stream stream, string url, string path, string contentType)
        {
            var fileName = url.Split('/');
            string imageName;

            if (contentType.Contains("image") && !fileName[^1].Contains("."))
            {
                imageName = fileName[^1] + ".jfif";
            }
            else
            {
                imageName = fileName[^1];
            }

            try
            {
                using var fileStream = File.Create($"{path}\\{imageName}");
                await stream.CopyToAsync(fileStream).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine("File stream exception caught!");
                Console.WriteLine($"Message {e.Message}");
                return (false, string.Empty);
            }

            return (true, imageName);
        }

        /// <summary>
        /// Grabs all urls from raw html content
        /// </summary>
        /// <param name="htmlContent"></param>
        /// <returns></returns>
        public List<string> GetPlainUrlsForPage(string htmlContent)
        {
            // Get all the urls that contain the root url from the root html
            var regexPattern = @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)";
            var urls = new List<string>();
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

            var match = regex.Match(htmlContent);

            while (match.Success)
            {
                if (match.Value.Contains(GetPlainUrl(_config.RootUrl)))
                {
                    // Split urls at ?
                    var newMatch = new string[] { };
                    if (match.Value.Contains('?'))
                    {
                        newMatch = match.Value.Split('?');
                    }
                    urls.Add(newMatch.Length == 0 ? match.Value : newMatch[0]);
                }

                match = match.NextMatch();
            }

            // Ensure list has no duplicates
            return urls.Distinct().ToList();
        }
    }
}
