namespace SearchEngine.Crawler
{
    internal class FetchResult
    {
        public string FinalUrl { get; set; }
        public int StatusCode { get; set; }
        public string? Html { get; set; }
        public string ReasonMessage { get; set; }
    }
}
