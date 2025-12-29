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


        Console.WriteLine("=== Mini Search Engine Crawler ===");


        // ========== CONFIG ==========
        var config = new CrawlerConfig
        {
            MaxPages = 50,              // الحد الأقصى للصفحات
            MaxDepth = 2,               // عمق روابط الانتشار
            PageSizeLimit = 500,        // KB
            MaxParallelTasks = 5,
            UserAgent = "MiniCrawlerBot/1.0",
            Timeout = TimeSpan.FromSeconds(10)
        };

        // ========== COMPONENTS ==========
        var queue = new UrlQueue();
        var filter = new UrlFilter();
        var fetcher = new HtmlFetcher(config);
        var parser = new HtmlParser(50);
        var outputPath = Path.Combine(
            Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.FullName,
             "data",
               "index.ndjson"
                   );

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        Console.WriteLine("Crawler writing to:");
        Console.WriteLine(outputPath);




        var indexer = new CrawlerIndexer(outputPath);

        // ========== SEED URLs ==========
        // ========== SEED URLs ==========
        var seeds = new[]
  {

    "https://cs50.harvard.edu",
    "https://developer.mozilla.org",
    "https://www.geeksforgeeks.org",
    "https://www.tutorialspoint.com",
    "https://www.javatpoint.com",
    "https://www.programiz.com",
    "https://cplusplus.com",
    "https://learn.microsoft.com/en-us/dotnet/",
    "https://www.w3schools.com",
    "https://www.digitalocean.com/community/tutorials",
    "https://docs.python.org/3/",
    "https://www.freecodecamp.org/news/",
    "https://kotlinlang.org/docs/home.html",
    "https://react.dev/learn",
    "https://spring.io/guides",
    "https://leetcode.com/" ,
    "https://www.entityframeworktutorial.net/efcore/entity-framework-core.aspx",
    "https://learngitbranching.js.org/",
    "https://osintfr.com/",
    "https://elzero.org/",
"https://adelnasim.com/ar/",
"https://nouvil.net/",
"https://codeforces.com/",
"https://www.geeksforgeeks.org/",
"https://www.w3schools.com/",
"https://devdocs.io/",
"https://www.programiz.com/cpp-programming/online-compiler/",
"https://harmash.com/",
"https://www.coursera.org/?skipBrowseRedirect=true/",
"https://www.simplilearn.com/skillup-free-online-courses#exploreCoursesSkillup",
"https://www.classcentral.com/",
"https://www.freecodecamp.org/learn/",
"https://www.codecademy.com/learn?page=learning",
"https://www.udemy.com/",
"https://learn.udacity.com/",
"https://visualgo.net/en",
"https://stackoverflow.com/",
"https://css-tricks.com/",
"https://www.khanacademy.org/profile/me/courses",
"https://webcode.tools/",
"https://developer.mozilla.org/en-US/",
"https://overapi.com/",
"https://roadmap.sh/",
"https://www.sololearn.com/en/profile/31760585",
"https://harmash.com/",
"https://www.edraak.org/",
"https://app.almentor.net/home",
"https://www.codewars.com/users/sign_in",
"https://academy.hsoub.com/",
"https://yanfaa.com/eg/home",
"https://www.hackerrank.com/",
"https://leetcode.com/",
"https://manual.cs50.io/",
"https://cursa.app/en",
"https://www.edx.org/",
"https://www.sanfoundry.com/",
"https://books.goalkicker.com/",
"https://stackoverflow.com/",
"https://roadmap.sh/backend",
"https://www.freecodecamp.org/learn",
"https://ajitpal.github.io/BookBank/#"

    };

        foreach (var url in seeds)
            queue.Enqueue(new UrlEntry { Url = url, Depth = 0 });

        Console.WriteLine("Seed URLs added.");


        // ========== WORKER SETUP ==========
        var worker = new CrawlerWorker(queue, filter, fetcher, parser, indexer, config);

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        int processed = 0;

        // ========== WORKER POOL ==========
        var tasks = Enumerable.Range(0, config.MaxParallelTasks)
            .Select(i => Task.Run(async () =>
            {
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        // حاول تفريغ واحد من الطابور
                        if (!queue.TryDequeue(out var entry))
                        {
                            await Task.Delay(200, ct).ConfigureAwait(false);
                            continue;
                        }

                        // تحقق إننا لم نتجاوز الحد
                        if (Interlocked.CompareExchange(ref processed, 0, 0) >= config.MaxPages)
                        {
                            // reached the limit -> request cancel and break
                            try { cts.Cancel(); } catch { }
                            break;
                        }

                        try
                        {
                            await worker.ProcessUrlAsync(entry, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // تم الإلغاء أثناء معالجة هذا الإدخال
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Worker exception processing {entry?.Url}: {ex.Message}");
                            // استمر بعد تسجيل الخطأ
                        }
                        finally
                        {
                            Interlocked.Increment(ref processed);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // خروج نظيف عند الإلغاء
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Worker outer exception: {ex.Message}");
                }
            }, ct)).ToArray();

        // ========== WAIT FOR ALL WORKERS SAFELY ==========
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // طبيعي عند الإلغاء؛ تجاهل
        }
        catch (AggregateException agg)
        {
            // لو ظهرت AggregateException (نادر الآن) نطبع التفاصيل
            foreach (var inner in agg.InnerExceptions)
                Console.WriteLine($"Worker aggregate exception: {inner.GetType().Name}: {inner.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception waiting workers: {ex.Message}");
        }

        Console.WriteLine("Crawling finished.");
        Console.WriteLine("Output saved in data/index.ndjson");



    }
}

