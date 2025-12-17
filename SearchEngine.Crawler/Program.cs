using SearchEngine.Core;
using SearchEngine.Crawler;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using CoreIndexer = SearchEngine.Core.Indexer;
using CrawlerIndexer = SearchEngine.Crawler.Indexer;


internal class Program
{
    static async Task Main(string[] args)
    {


        //        Console.WriteLine("=== Mini Search Engine Crawler ===");


        //        // ========== CONFIG ==========
        //        var config = new CrawlerConfig
        //        {
        //            MaxPages = 50,              // الحد الأقصى للصفحات
        //            MaxDepth = 2,               // عمق روابط الانتشار
        //            PageSizeLimit = 500,        // KB
        //            MaxParallelTasks = 5,
        //            UserAgent = "MiniCrawlerBot/1.0",
        //            Timeout = TimeSpan.FromSeconds(10)
        //        };

        //        // ========== COMPONENTS ==========
        //        var queue = new UrlQueue();
        //        var filter = new UrlFilter();
        //        var fetcher = new HtmlFetcher(config);
        //        var parser = new HtmlParser(50);
        //        var outputPath = Path.Combine(
        //            Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.FullName,
        //             "data",
        //               "index.ndjson"
        //                   );

        //        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        //        Console.WriteLine("Crawler writing to:");
        //        Console.WriteLine(outputPath);




        //        var indexer = new CrawlerIndexer(outputPath);

        //        // ========== SEED URLs ==========
        //        // ========== SEED URLs ==========
        //        var seeds = new[]
        //  {
        //    // Educational & Open Knowledge (usually crawl-friendly)
        //    "https://en.wikipedia.org",
        //    "https://simple.wikipedia.org",
        //    "https://developer.mozilla.org",
        //    "https://www.w3schools.com",
        //    "https://www.geeksforgeeks.org",
        //    "https://ocw.mit.edu",

        //    // Government & Public Domain (crawl-friendly)
        //    "https://www.nasa.gov",
        //    "https://data.gov",
        //    "https://www.loc.gov",

        //    // Open Source & Developer (usually crawl-friendly)
        //    "https://github.com",
        //    "https://stackoverflow.com",
        //    "https://learn.microsoft.com",

        //    // News & Media (check robots.txt carefully)
        //    "https://www.bbc.com/news",
        //    "https://www.reuters.com",
        //    "https://www.apnews.com",

        //    // Academic & Research
        //    "https://arxiv.org",
        //    "https://scholar.google.com",
        //    "https://www.ncbi.nlm.nih.gov",

        //    // Public APIs & Data Sources
        //    "https://api.data.gov",
        //    "https://jsonplaceholder.typicode.com",
        //    "https://swapi.dev"
        //};

        //        foreach (var url in seeds)
        //            queue.Enqueue(new UrlEntry { Url = url, Depth = 0 });

        //        Console.WriteLine("Seed URLs added.");


        //        // ========== WORKER SETUP ==========
        //        var worker = new CrawlerWorker(queue, filter, fetcher, parser, indexer, config);

        //        using var cts = new CancellationTokenSource();
        //        var ct = cts.Token;

        //        int processed = 0;

        //        // ========== WORKER POOL ==========
        //        var tasks = Enumerable.Range(0, config.MaxParallelTasks)
        //            .Select(i => Task.Run(async () =>
        //            {
        //                try
        //                {
        //                    while (!ct.IsCancellationRequested)
        //                    {
        //                        // حاول تفريغ واحد من الطابور
        //                        if (!queue.TryDequeue(out var entry))
        //                        {
        //                            await Task.Delay(200, ct).ConfigureAwait(false);
        //                            continue;
        //                        }

        //                        // تحقق إننا لم نتجاوز الحد
        //                        if (Interlocked.CompareExchange(ref processed, 0, 0) >= config.MaxPages)
        //                        {
        //                            // reached the limit -> request cancel and break
        //                            try { cts.Cancel(); } catch { }
        //                            break;
        //                        }

        //                        try
        //                        {
        //                            await worker.ProcessUrlAsync(entry, ct).ConfigureAwait(false);
        //                        }
        //                        catch (OperationCanceledException)
        //                        {
        //                            // تم الإلغاء أثناء معالجة هذا الإدخال
        //                            break;
        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            Console.WriteLine($"Worker exception processing {entry?.Url}: {ex.Message}");
        //                            // استمر بعد تسجيل الخطأ
        //                        }
        //                        finally
        //                        {
        //                            Interlocked.Increment(ref processed);
        //                        }
        //                    }
        //                }
        //                catch (OperationCanceledException)
        //                {
        //                    // خروج نظيف عند الإلغاء
        //                }
        //                catch (Exception ex)
        //                {
        //                    Console.WriteLine($"Worker outer exception: {ex.Message}");
        //                }
        //            }, ct)).ToArray();

        //        // ========== WAIT FOR ALL WORKERS SAFELY ==========
        //        try
        //        {
        //            await Task.WhenAll(tasks).ConfigureAwait(false);
        //        }
        //        catch (OperationCanceledException)
        //        {
        //            // طبيعي عند الإلغاء؛ تجاهل
        //        }
        //        catch (AggregateException agg)
        //        {
        //            // لو ظهرت AggregateException (نادر الآن) نطبع التفاصيل
        //            foreach (var inner in agg.InnerExceptions)
        //                Console.WriteLine($"Worker aggregate exception: {inner.GetType().Name}: {inner.Message}");
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"Unhandled exception waiting workers: {ex.Message}");
        //        }

        //        Console.WriteLine("Crawling finished.");
        //        Console.WriteLine("Output saved in data/index.ndjson");
        var tokenizer = new Tokenizer();
        var index = new InvertedIndex();
        var indexer = new CoreIndexer(tokenizer, index);

        // 2) ملف NDJSON (غيّر المسار حسب جهازك)
        string ndjsonPath = @"C:\Users\LAPTOP\source\repos\SearchEngine.Crawler\SearchEngine.Crawler\data\index.ndjson";

        // 3) ابني الـ Index
        indexer.BuildFromNdjson(ndjsonPath);

        // 4) اقرأ الصفحات (علشان QueryEngine)
        var reader = new NdjsonPageReader();
        var pages = new List<PageDocument>(reader.Read(ndjsonPath));

        // 5) Query Engine
        var queryEngine = new QueryEngine(index, pages);

        // 6) جرّب بحث
        Console.WriteLine("type what you want to search ");
        string s = Console.ReadLine();
        Console.WriteLine($"Search: {s}");

        var results = queryEngine.Search(s);

        foreach (var r in results)
        {
            Console.WriteLine($"{r.Score:F2} | {r.Title}");
            Console.WriteLine($"{r.Url}");
            Console.WriteLine($"{r.Snippet}");
            Console.WriteLine(new string('-', 40));
        }


    }
}

