namespace SearchEngine.Core
{
    public class SearchResult
    {
        public string Url { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Snippet { get; set; } = string.Empty;

        public double Score { get; set; }
    }
}
