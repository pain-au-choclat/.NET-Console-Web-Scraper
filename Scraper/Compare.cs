using Microsoft.Extensions.Options;
using Scraper.Interfaces;
using Scraper.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scraper
{
    public class Compare : ICompare
    {
        private readonly ScraperConfiguration _config;

        public Compare(IOptions<ScraperConfiguration> config)
        {
            _config = config.Value;
        }

        public ComparisonModel StartCompare(string folderOne,
            string folderTwo)
        {
            var originalFolder = new DirectoryInfo(folderOne);
            var latestFolder = new DirectoryInfo(folderTwo);

            // Ensure that folderOne is the latest folder
            if (originalFolder.CreationTime > latestFolder.CreationTime)
            {
                var tempFolder = originalFolder;
                originalFolder = latestFolder;
                latestFolder = tempFolder;
            }

            var firstFolderFileNames = GetFilesNames(originalFolder);
            var secondFolderFileNames = GetFilesNames(latestFolder);

            IEnumerable<string> removedFromOriginal = firstFolderFileNames.Except(secondFolderFileNames);
            IEnumerable<string> addedToOriginal = secondFolderFileNames.Except(firstFolderFileNames);

            if (_config.ConsoleLogging)
            {
                FolderDifferencesConsoleLogging(removedFromOriginal, addedToOriginal, originalFolder.Name, latestFolder.Name);
            }

            // Check .txt files for changes
            var (linesAddedToLatest, linesRemovedFromOriginal) = GetTextFileChanges(originalFolder, latestFolder);

            if (_config.ConsoleLogging)
            {
                TextDifferencesConsoleLogging(linesAddedToLatest, linesRemovedFromOriginal);
            }

            var comparisonModel = new ComparisonModel()
            {
                LinesAddedToLatest = linesAddedToLatest,
                LinesRemovedFromOriginal = linesRemovedFromOriginal,
                FilesAdded = addedToOriginal,
                FilesRemoved = removedFromOriginal,
                IsComparisonComplete = true,
            };

            return comparisonModel;
        }

        /// <summary>
        /// Deals with the console logging from the text file differences
        /// </summary>
        /// <param name="linesAdded"></param>
        /// <param name="linesRemovedFromOriginal"></param>
        public void TextDifferencesConsoleLogging(ConcurrentBag<string> linesAdded, ConcurrentBag<string> linesRemovedFromOriginal)
        {
            if (linesAdded.Count() > 0)
            {
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"The following lines have been added to {_config.RootUrl}:");
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Gray;

                foreach (var line in linesAdded)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("");
                    Console.WriteLine(line);
                    Console.ForegroundColor = ConsoleColor.Gray;

                }
            }
            if (linesRemovedFromOriginal.Count() > 0)
            {
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"The following lines have been removed from {_config.RootUrl}:");
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.White;

                foreach (var line in linesRemovedFromOriginal)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("");
                    Console.WriteLine(line);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }
            if (linesRemovedFromOriginal.Count() == 0 && linesAdded.Count() == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"The files in both folder are the same!");
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            if (!_config.RunScheduled)
            {
                Console.WriteLine("");
                Console.WriteLine("Please return to proceed...");
                Console.WriteLine("");
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Deal with all the console logging from the file differences
        /// </summary>
        /// /// <param name="removedFromOriginal"></param>
        /// /// <param name="addedToOriginal"></param>
        /// <param name="originalFolderName"></param>
        /// <param name="latestFolderName"></param>
        public void FolderDifferencesConsoleLogging(IEnumerable<string> removedFromOriginal, IEnumerable<string> addedToOriginal,
            string originalFolderName, string latestFolderName)
        {
            if (removedFromOriginal.Count() > 0)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"The following files have been removed from [{originalFolderName}]:");
                Console.WriteLine("");
                foreach (var fileName in removedFromOriginal)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(fileName);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }
            if (addedToOriginal.Count() > 0)
            {
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"The following files have been added to [{latestFolderName}]:");
                Console.WriteLine("");
                foreach (var fileName in addedToOriginal)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(fileName);
                    Console.ForegroundColor = ConsoleColor.Gray;
                }
            }
            if (addedToOriginal.Count() == 0 && removedFromOriginal.Count() == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Both folders have the same files!");
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            if (!_config.RunScheduled)
            {
                Console.WriteLine("");
                Console.WriteLine("Please return to proceed to txt/html file differences...");
                Console.WriteLine("");
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Compare every file in both folders in parallel, return original strings and new strings
        /// </summary>
        /// <param name="originalFolder"></param>
        /// <param name="latestFolder"></param>
        /// <returns></returns>
        public (ConcurrentBag<string>, ConcurrentBag<string>) GetTextFileChanges(DirectoryInfo originalFolder, DirectoryInfo latestFolder)
        {
            var changesListAddedToLatest = new ConcurrentBag<string>();
            var originalListRemovedFromLatest = new ConcurrentBag<string>();

            Parallel.ForEach (originalFolder.GetFiles(), (originalFile) =>
            {
                if (originalFile.Name.Split('.')[^1] == "txt" || originalFile.Name.Split('.')[^1] == "html")
                {
                    if (File.Exists($"{latestFolder.FullName}\\{originalFile.Name}"))
                    {
                        var htmlToText = new HtmlToText();
                        var latestFile = latestFolder.GetFiles(originalFile.Name).FirstOrDefault();

                        var latestFileLines = File.ReadAllLines(latestFile.FullName);
                        var originalFileLines = File.ReadAllLines(originalFile.FullName);

                        IEnumerable<string> inFirstNotInSecond = originalFileLines.Except(latestFileLines);
                        IEnumerable<string> inSecondNotInFirst = latestFileLines.Except(originalFileLines);

                        // In the original file but have been removed from the latest file (have been removed)
                        if (inFirstNotInSecond.Count() > 0)
                        {
                            //Parallel.ForEach(inFirstNotInSecond, (line) =>
                            //{
                            //    if (IsStringHtml(line))
                            //    {
                            //        var convertedLine = _htmlToText.Convert(line);
                            //        if (!string.IsNullOrEmpty(convertedLine))
                            //        {
                            //            originalListRemovedFromLatest.Add($"[{latestFile.Name}]: " + convertedLine);
                            //        }
                            //    }
                            //    else
                            //    {
                            //        originalListRemovedFromLatest.Add($"[{latestFile.Name}]: " + line);
                            //    }
                            //});
                            foreach (var line in inFirstNotInSecond)
                            {
                                if (IsStringHtml(line))
                                {
                                    var convertedLine = htmlToText.Convert(line);
                                    if (!string.IsNullOrEmpty(convertedLine))
                                    {
                                        originalListRemovedFromLatest.Add($"[{latestFile.Name}]: " + convertedLine);
                                    }
                                }
                                else
                                {
                                    originalListRemovedFromLatest.Add($"[{latestFile.Name}]: " + line);
                                }
                            }
                        }

                        // In the latest file but was not present in the original (have been added)
                        if (inSecondNotInFirst.Count() > 0)
                        {
                            //Parallel.ForEach(inSecondNotInFirst, (line) =>
                            //{
                            //    if (IsStringHtml(line))
                            //    {
                            //        var convertedLine = _htmlToText.Convert(line);
                            //        if (!string.IsNullOrEmpty(convertedLine))
                            //        {
                            //            changesListAddedToLatest.Add($"[{latestFile.Name}]: " + convertedLine);
                            //        }
                            //    }
                            //    else
                            //    {
                            //        changesListAddedToLatest.Add($"[{latestFile.Name}]: " + line);
                            //    }
                            //});
                            foreach (var line in inSecondNotInFirst)
                            {
                                if (IsStringHtml(line))
                                {
                                    var convertedLine = htmlToText.Convert(line);
                                    if (!string.IsNullOrEmpty(convertedLine))
                                    {
                                        changesListAddedToLatest.Add($"[{GetUrlFromFileName(latestFile.Name)}]: " + convertedLine);
                                    }
                                }
                                else
                                {
                                    changesListAddedToLatest.Add($"[{latestFile.Name}]: " + line);
                                }
                            }
                        }
                    }
                }
            });

            return (changesListAddedToLatest, originalListRemovedFromLatest);
        }

        /// <summary>
        /// Get the original url back from the filename
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public string GetUrlFromFileName(string fileName)
        {
            var urlBuilder = new StringBuilder();
            urlBuilder.Append("https://");

            var nameSplit = fileName.Split('.');
            if (nameSplit[^1] == "txt" || nameSplit[^1] == "html")
            {
                urlBuilder.Append(fileName.Replace("{FS}", "/").Replace($".{nameSplit[^1]}", ""));
            }
            else
            {
                urlBuilder.Append(fileName.Replace("{FS}", "/"));
            }

            return urlBuilder.ToString();
        }

        /// <summary>
        /// Check to see if a line is html
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public bool IsStringHtml(string line)
        {
            var htmlRegex = new Regex(@"<\s*([^ >]+)[^>]*>.*?<\s*/\s*\1\s*>");
            return htmlRegex.Match(line).Success;
        }

        /// <summary>
        /// Get the file names for a specific folder in parallel
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public ConcurrentBag<string> GetFilesNames(DirectoryInfo folder)
        {
            var fileNames = new ConcurrentBag<string>();

            Parallel.ForEach(folder.GetFiles(), (currentFile) =>
            {
                var fileName = currentFile.Name;
                fileNames.Add(fileName);
            });

            return fileNames;
        }
    }
}
