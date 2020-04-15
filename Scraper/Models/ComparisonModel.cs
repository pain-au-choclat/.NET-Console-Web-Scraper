using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Scraper.Models
{
    public class ComparisonModel
    {
        public ConcurrentBag<string> AddedToLatest { get; set; }

        public ConcurrentBag<string> RemovedFromLatest { get; set; }

        public ConcurrentBag<string> FileNamesList { get; set; }

        public List<string> FileDifferences { get; set; }

        public bool IsComparisonComplete { get; set; }

        public bool IsFilesRemoved { get; set; }
    }
}
