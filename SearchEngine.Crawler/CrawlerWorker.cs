// File: CrawlerWorker.cs
using AngleSharp.Html.Parser.Tokens;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SearchEngine.Crawler
{
    internal class CrawlerWorker
    {
        private readonly UrlQueue _queue;
        private readonly UrlFilter _filter;
        private readonly HtmlFetcher _fetcher;
        private readonly HtmlParser _parser;
        private readonly Indexer _indexer;
        private readonly CrawlerConfig _config;

        public CrawlerWorker(
            UrlQueue queue,
            UrlFilter filter,
            HtmlFetcher fetcher,
            HtmlParser parser,
            Indexer indexer,
            CrawlerConfig config)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// ينفّذ خطوات 1-5 من عملية المعالجة:
        /// 1) تحقق من الvisited (filter.Contains)
        /// 2) اذا مش موجود -> pre-mark (filter.Add)
        /// 3) استدعاء fetcher.FetchAsync
        /// 4) طباعة حالة الـ fetch
        /// 5) إذا لم تنجح عملية الفetch نرجع فوراً
        /// لاحقاً سنضيف Parse -> Index -> Enqueue.
        /// </summary>
        public async Task ProcessUrlAsync(UrlEntry entry, CancellationToken ct)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var rawUrl = entry.Url?.Trim();
            if (string.IsNullOrEmpty(rawUrl))
            {
                Console.WriteLine("Empty URL in entry — skipping.");
                return;
            }

            // Normalize URL first
            var url = UrlUtils.NormalizeUrl(rawUrl);
            if (string.IsNullOrEmpty(url))
            {
                Console.WriteLine($"Unable to normalize URL '{rawUrl}' — skipping.");
                return;
            }

            // 1) هل زُرنا هذا الرابط قبلًا؟
            bool visited;
            try
            {
                visited = _filter.Contains(url); // UrlFilter يجب أن يعمل على normalized urls
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking filter for {url}: {ex.Message}. Skipping.");
                return;
            }

            if (visited)
            {
                Console.WriteLine($"Skipped (visited): {url}");
                return;
            }

            // 2) pre-mark حتى لا يزوره عامل آخر
            try
            {
                _filter.Add(url); // add normalized URL
                Console.WriteLine($"Marked and fetching: {url}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking URL in filter {url}: {ex.Message}. Skipping.");
                return;
            }

            // 3) نفّذ الفetch
            FetchResult fetchResult;
            try
            {
                fetchResult = await _fetcher.FetchAsync(url, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Fetch cancelled for {url}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fetch exception for {url}: {ex.Message}");
                return;
            }

            // 4) طباعة نتيجة الفetch
            Console.WriteLine($"Fetch {url} => {fetchResult.ReasonMessage}");

            // 5) لو الفetch فشل (غير "Success") نرجع
            if (!string.Equals(fetchResult.ReasonMessage, "Success", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // ====== 6) Parse ======
            ParseResult parseResult;
            try
            {
                parseResult = await _parser.ParseAsync(fetchResult.Html ?? string.Empty, fetchResult.FinalUrl ?? url).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Parse error for {url}: {ex.Message}");
                return;
            }

            // ====== 7) Index (NDJSON) ======
            try
            {
                var item = new IndexItem
                {
                    Url = UrlUtils.NormalizeUrl(fetchResult.FinalUrl ?? url) ?? url,
                    Title = parseResult.Title ?? string.Empty,
                    Favicon = parseResult.Favicon,
                    Snippet = parseResult.Snippet ?? string.Empty,
                    FetchedAt = DateTimeOffset.UtcNow
                };

                await _indexer.AppendAsync(item, ct).ConfigureAwait(false);
                Console.WriteLine($"Indexed: {item.Url}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Indexing error for {url}: {ex.Message}");
            }

            // ====== 8) Enqueue child links (respect MaxDepth + filter) ======
            try
            {
                int nextDepth = entry.Depth + 1;
                if (_config.MaxDepth > 0 && nextDepth > _config.MaxDepth)
                {
                    Console.WriteLine($"Not enqueuing children of {url}: reached max depth ({_config.MaxDepth}).");
                    return;
                }

                foreach (var link in parseResult.Links ?? Enumerable.Empty<string>())
                {
                    ct.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(link)) continue;

                    // Normalize child link
                    var childNorm = UrlUtils.NormalizeUrl(link);
                    if (string.IsNullOrEmpty(childNorm)) continue;

                    bool childVisited;
                    try
                    {
                        childVisited = _filter.Contains(childNorm);
                    }
                    catch
                    {
                        continue;
                    }

                    if (childVisited) continue;

                    try
                    {
                        _filter.Add(childNorm);
                    }
                    catch { }

                    try
                    {
                        var childEntry = new UrlEntry { Url = childNorm, Depth = nextDepth };
                        _queue.Enqueue(childEntry);
                        Console.WriteLine($"Enqueued: {childNorm} (depth={nextDepth})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Queue enqueue failed for {childNorm}: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Enqueue cancelled.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during enqueue of children for {url}: {ex.Message}");
            }
        }


    }
}
