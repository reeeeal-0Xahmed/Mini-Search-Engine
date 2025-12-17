using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;



namespace SearchEngine.Crawler
{
    internal class HtmlFetcher
    {
        private readonly HttpClient _HttpClient;
        private readonly CrawlerConfig _Config;

        public HtmlFetcher(CrawlerConfig Config)
        {
            _Config = Config;
            if (Config == null) throw new ArgumentNullException(nameof(Config));

            _HttpClient = new HttpClient();
            _HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_Config.UserAgent);
            _HttpClient.Timeout = Timeout.InfiniteTimeSpan;

        }
        public async Task<FetchResult> FetchAsync(string url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url) ||
                !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                  url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                return new FetchResult
                {
                    FinalUrl = url,
                    StatusCode = 0,
                    Html = null,
                    ReasonMessage = "InvalidScheme"
                };
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(_Config.Timeout);
            var token = linkedCts.Token;

            try
            {
                using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                using var response = await _HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

                var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return new FetchResult
                    {
                        FinalUrl = finalUrl,
                        StatusCode = (int)response.StatusCode,
                        Html = null,
                        ReasonMessage = "HttpError"
                    };
                }

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (string.IsNullOrEmpty(contentType) || !contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    return new FetchResult
                    {
                        FinalUrl = finalUrl,
                        StatusCode = (int)response.StatusCode,
                        Html = null,
                        ReasonMessage = "NotHtml"
                    };
                }

                long limitBytes = _Config.PageSizeLimit * 1024L;
                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > limitBytes)
                {
                    return new FetchResult
                    {
                        FinalUrl = finalUrl,
                        StatusCode = (int)response.StatusCode,
                        Html = null,
                        ReasonMessage = "TooLarge"
                    };
                }

                // اقرأ الجسم كستريم وبشكل مجزأ
                using var stream = await response.Content.ReadAsStreamAsync(token);

                // مخزن مؤقت لبايتس الصفحة
                using var ms = new System.IO.MemoryStream();
                byte[] buffer = new byte[8192]; // 8KB
                long totalRead = 0;

                // اقرأ حتى نهاية الستريم أو حتى نتجاوز الحد
                while (true)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0) break; // نهاية الستريم

                    ms.Write(buffer, 0, read);
                    totalRead += read;

                    if (totalRead > limitBytes)
                    {
                        return new FetchResult
                        {
                            FinalUrl = finalUrl,
                            StatusCode = (int)response.StatusCode,
                            Html = null,
                            ReasonMessage = "TooLarge"
                        };
                    }
                }

                // ارجع المؤشر للبداية عشان نقرا الـ bytes لاحقًا كنص
                ms.Position = 0;

                // تحويل البايتس إلى نص مع اكتشاف الترميز (charset/BOM)
                string htmlText;
                var charset = response.Content.Headers.ContentType?.CharSet;
                if (!string.IsNullOrEmpty(charset))
                {
                    try
                    {
                        var encoding = Encoding.GetEncoding(charset);
                        using var reader = new StreamReader(ms, encoding, detectEncodingFromByteOrderMarks: true);
                        htmlText = await reader.ReadToEndAsync();
                    }
                    catch
                    {
                        ms.Position = 0;
                        using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                        htmlText = await reader.ReadToEndAsync();
                    }
                }
                else
                {
                    using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    htmlText = await reader.ReadToEndAsync();
                }

                return new FetchResult
                {
                    FinalUrl = finalUrl,
                    StatusCode = (int)response.StatusCode,
                    Html = htmlText,
                    ReasonMessage = "Success"
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // الإلغاء من قبل المُستدعي
                return new FetchResult
                {
                    FinalUrl = url,
                    StatusCode = 0,
                    Html = null,
                    ReasonMessage = "Cancelled"
                };
            }
            catch (OperationCanceledException)
            {
                // عادةً timeout من linkedCts
                return new FetchResult
                {
                    FinalUrl = url,
                    StatusCode = 0,
                    Html = null,
                    ReasonMessage = "Timeout"
                };
            }
            catch (HttpRequestException ex)
            {
                return new FetchResult
                {
                    FinalUrl = url,
                    StatusCode = 0,
                    Html = null,
                    ReasonMessage = "NetworkError: " + ex.Message
                };
            }
            catch (Exception ex)
            {
                return new FetchResult
                {
                    FinalUrl = url,
                    StatusCode = 0,
                    Html = null,
                    ReasonMessage = "Error: " + ex.Message
                };
            }
        }


    }
}