namespace Scraper
{
    public class ScraperConfiguration
    {
        public bool ConsoleLogging { get; set; }

        public string FilePath { get; set; }

        public string RootUrl { get; set; }

        public bool IterationBreak { get; set; }

        public string HeaderName { get; set; }

        public string HeaderValue { get; set; }

        public string TimeLimit { get; set; }

        public string UrlLimit { get; set; }

        public bool RunScheduled { get; set; }

        public bool SendOutputEmails { get; set; }

        public string FromEmail { get; set; }

        public string ToEmail { get; set; }

        public string SmtpHost { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string Port { get; set; }
    }
}
