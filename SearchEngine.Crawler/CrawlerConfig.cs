using System;
using System.Collections.Generic;
using System.Text;

namespace SearchEngine.Crawler
{
    internal class CrawlerConfig
    {
        public int MaxPages { get; set; }
        public int MaxDepth {  get; set; }
        public List<string> AllowedDomains { get; set; } = new List<string>();
        public int PageSizeLimit {  get; set; }
        public int MaxParallelTasks {  get; set; }
        public string UserAgent { get; set; }
        public TimeSpan Timeout { get; set; }
    }
}
