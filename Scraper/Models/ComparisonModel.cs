using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Scraper.Models
{
    public class ComparisonModel
    {
        public ConcurrentBag<string> LinesAddedToLatest { get; set; }

        public ConcurrentBag<string> LinesRemovedFromOriginal { get; set; }

        public ConcurrentBag<string> FileNamesList { get; set; }

        public IEnumerable<string> FilesRemoved { get; set; }

        public IEnumerable<string> FilesAdded { get; set; }

        public bool IsComparisonComplete { get; set; }
    }
}
