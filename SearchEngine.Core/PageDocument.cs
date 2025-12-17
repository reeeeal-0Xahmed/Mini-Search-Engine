

namespace SearchEngine.Core
{
    public class PageDocument
    {
        public string Url { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Snippet { get; set; } = string.Empty;

        public string? Favicon { get; set; }

        public DateTime FetchedAt { get; set; }
    }
}
