using Scraper.Models;

namespace Scraper.Interfaces
{
    public interface ICompare
    {
        ComparisonModel StartCompare(string folderOne, string folderTwo);
    }
}
