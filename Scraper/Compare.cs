using Microsoft.Extensions.Options;
using Scraper.Interfaces;
using Scraper.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scraper
{
    public class Compare : ICompare
    {
        private readonly ScraperConfiguration _config;
        private readonly IHtmlToText _htmlToText;

        public Compare(IOptions<ScraperConfiguration> config, IHtmlToText htmlToText)
        {
            _config = config.Value;
            _htmlToText = htmlToText;
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
            var (linesAddedToLatest, linesRemovedFromOriginal, fileNamesList) = GetTextFileChanges(originalFolder, latestFolder);

            if (_config.ConsoleLogging)
            {
                TextDifferencesConsoleLogging(linesAddedToLatest, linesRemovedFromOriginal);
            }

            var comparisonModel = new ComparisonModel()
            {
                LinesAddedToLatest = linesAddedToLatest,
                LinesRemovedFromOriginal = linesRemovedFromOriginal,
                FileNamesList = fileNamesList,
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
            var linesAddedList = linesAdded.ToList();
            var removedFromOriginalList = linesRemovedFromOriginal.ToList();

            if (linesAddedList.Count() > 0)
            {
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"The following lines have been added to {_config.RootUrl}:");
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Gray;

                foreach (var line in linesAddedList)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("");
                    Console.WriteLine(line);
                    Console.ForegroundColor = ConsoleColor.Gray;

                }
            }
            if (removedFromOriginalList.Count() > 0)
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
        public (ConcurrentBag<string>, ConcurrentBag<string>, ConcurrentBag<string>) GetTextFileChanges(DirectoryInfo originalFolder,
            DirectoryInfo latestFolder)
        {
            var changesListAddedToLatest = new ConcurrentBag<string>();
            var originalListRemovedFromLatest = new ConcurrentBag<string>();
            var fileNameList = new ConcurrentBag<string>();

            Parallel.ForEach(originalFolder.GetFiles(), (originalFile) =>
            {
                if (originalFile.Name.Split('.')[^1] == "txt" || originalFile.Name.Split('.')[^1] == "html")
                {
                    if (File.Exists($"{latestFolder.FullName}\\{originalFile.Name}"))
                    {
                        var latestFile = latestFolder.GetFiles(originalFile.Name).FirstOrDefault();

                        var latestFileLines = File.ReadAllLines(latestFile.FullName);
                        var originalFileLines = File.ReadAllLines(originalFile.FullName);

                        IEnumerable<string> inFirstNotInSecond = originalFileLines.Except(latestFileLines);
                        IEnumerable<string> inSecondNotInFirst = latestFileLines.Except(originalFileLines);

                        // If file (either original or latest) is changed add file name to list
                        if (inFirstNotInSecond.Count() > 0 && inSecondNotInFirst.Count() > 0)
                        {
                            fileNameList.Add(latestFile.Name);
                        }

                        // In the original file but have been removed from the latest file (have been removed)
                        if (inFirstNotInSecond.Count() > 0)
                        {
                            foreach (var line in inFirstNotInSecond)
                            {
                                if (IsStringHtml(line))
                                {
                                    var convertedLine = _htmlToText.Convert(line);
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
                            foreach (var line in inSecondNotInFirst)
                            {
                                if (IsStringHtml(line))
                                {
                                    var convertedLine = _htmlToText.Convert(line);
                                    if (!string.IsNullOrEmpty(convertedLine))
                                    {
                                        changesListAddedToLatest.Add($"[{latestFile.Name}]: " + convertedLine);
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

            return (changesListAddedToLatest, originalListRemovedFromLatest, fileNameList);
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
