using Microsoft.Extensions.Options;
using Scraper.Interfaces;
using Scraper.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            // Check files count and output if any are missing or file name has changed
            var fileDifferences = new List<string>();
            var filesHaveBeenRemoved = false;

            if (firstFolderFileNames.Count() > secondFolderFileNames.Count())
            {
                // Find the files that are missing
                IEnumerable<string> differences = firstFolderFileNames.Except(secondFolderFileNames);

                if (_config.ConsoleLogging && differences.Count() > 0)
                {
                    FolderDifferencesConsoleLogging(differences, firstFolderFileNames.Count(), secondFolderFileNames.Count(),
                        originalFolder.Name, latestFolder.Name);
                }

                if (differences.Count() > 0)
                {
                    fileDifferences = differences.ToList();
                    filesHaveBeenRemoved = true;
                }
            }
            else if (firstFolderFileNames.Count() == secondFolderFileNames.Count())
            {
                if (_config.ConsoleLogging)
                {
                    Console.WriteLine("Both folders have the same file count");
                }
            }
            else
            {
                // Find the files that are missing
                IEnumerable<string> differences = secondFolderFileNames.Except(firstFolderFileNames);

                if (_config.ConsoleLogging && differences.Count() > 0)
                {
                    FolderDifferencesConsoleLogging(differences, firstFolderFileNames.Count(), secondFolderFileNames.Count,
                        originalFolder.Name, latestFolder.Name);
                }

                if (differences.Count() > 0)
                {
                    fileDifferences = differences.ToList();
                    filesHaveBeenRemoved = false;
                }
            }

            // Check .txt files for changes
            var (addedToLatest, removedFromLatest, fileNamesList) = GetTextFileChanges(originalFolder, latestFolder);
            if (addedToLatest.Count > 0 && removedFromLatest.Count > 0)
            {
                if (_config.ConsoleLogging)
                {
                    TextDifferencesConsoleLogging(addedToLatest, removedFromLatest);
                }

            }

            var comparisonModel = new ComparisonModel()
            {
                AddedToLatest = addedToLatest,
                RemovedFromLatest = removedFromLatest,
                FileNamesList = fileNamesList,
                FileDifferences = fileDifferences,
                IsComparisonComplete = true,
                IsFilesRemoved = filesHaveBeenRemoved
            };

            return comparisonModel;
        }

        /// <summary>
        /// Deals with the console logging from the text file differences
        /// </summary>
        /// <param name="textChanges"></param>
        /// <param name="textOriginal"></param>
        public void TextDifferencesConsoleLogging(ConcurrentBag<string> textChanges, ConcurrentBag<string> textOriginal)
        {
            var changesList = textChanges.ToList();
            var originalList = textOriginal.ToList();
            var lines = changesList.Count();
            var linesLeast = originalList.Count();
            var linesSwitched = false;

            if (originalList.Count() > changesList.Count())
            {
                lines = originalList.Count();
                linesLeast = changesList.Count();
                linesSwitched = true;
            }

            for (var i = 0; i < lines; i++)
            {
                if (i < linesLeast)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"[ORIGINAL]: {originalList[i]}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[CHANGE]: {changesList[i]}");
                }
                else
                {
                    if (linesSwitched)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"[ORIGINAL]: {originalList[i]}");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CHANGE]: [LINE HAS BEEN REMOVED]");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CHANGE]: {changesList[i]}");
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.White;

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
        /// <param name="diff"></param>
        /// <param name="firstFolderCount"></param>
        /// <param name="secondFolderCount"></param>
        /// <param name="originalFolderName"></param>
        /// <param name="latestFolderName"></param>
        public void FolderDifferencesConsoleLogging(IEnumerable<string> diff, int firstFolderCount, int secondFolderCount,
            string originalFolderName, string latestFolderName)
        {
            Console.Clear();
            Console.WriteLine($"[{latestFolderName}]: Has a greater file count of {secondFolderCount}");
            Console.WriteLine($"[{originalFolderName}]: Has a count of {firstFolderCount}");
            Console.WriteLine("");

            Console.WriteLine($"The following file/files are in [{latestFolderName}] but not [{originalFolderName}]...");
            Console.WriteLine("");

            foreach (var fileName in diff)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(fileName);
                Console.ForegroundColor = ConsoleColor.White;
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
                        foreach (var line in inFirstNotInSecond)
                        {
                            var convertedLine = _htmlToText.Convert(line);
                            if (!string.IsNullOrEmpty(convertedLine))
                            {
                                originalListRemovedFromLatest.Add($"[{latestFile.Name}]: " + convertedLine);
                            }
                        }

                        // In the latest file but was not present in the original (have been added)
                        foreach (var line in inSecondNotInFirst)
                        {
                            var convertedLine = _htmlToText.Convert(line);
                            if (!string.IsNullOrEmpty(convertedLine))
                            {
                                changesListAddedToLatest.Add($"[{latestFile.Name}]: " + convertedLine);
                            }
                        }
                    }
                }
            });

            return (changesListAddedToLatest, originalListRemovedFromLatest, fileNameList);
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
