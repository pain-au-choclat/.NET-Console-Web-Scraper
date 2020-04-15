using System;
using System.Collections.Generic;

namespace Scraper.Models
{
    public class EmailModel
    {
        public List<string> LinesAddedToLatest { get; set; }

        public List<string> LinesRemovedFromOriginal { get; set; }

        public List<string> FilesAdded { get; set; }

        public List<string> FilesRemoved { get; set; }

        public List<string> FileNamesList { get; set; }

        public DateTime DateTime { get; set; }

        public string RootUrl { get; set; }

        public int Urls { get; set; }
    }
}
