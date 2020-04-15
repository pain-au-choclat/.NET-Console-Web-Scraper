using System.Threading.Tasks;

namespace Scraper.Interfaces
{
    public interface IAppHost
    {
        Task Run(string[] args);
    }
}
