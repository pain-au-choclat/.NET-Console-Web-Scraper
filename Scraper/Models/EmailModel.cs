using System;
using System.Collections.Generic;

namespace Scraper.Models
{
    public class EmailModel
    {
        public List<string> AddedToLatest { get; set; }

        public List<string> RemovedFromLatest { get; set; }

        public List<string> FilesDifferences { get; set; }

        public List<string> FileNamesList { get; set; }

        public DateTime DateTime { get; set; }

        public string RootUrl { get; set; }

        public bool FilesRemoved { get; set; }

        public int Urls { get; set; }
    }
}
