using System.Threading.Tasks;

namespace Scraper.Interfaces
{
    public interface IScraper
    {
        Task<(bool, int)> StartScrape();
    }
}
