using System;
using System.Collections.Generic;
using System.Linq;

namespace SearchEngine.Core
{
    public class QueryEngine
    {
        private readonly InvertedIndex _index;
        private readonly Dictionary<string, PageDocument> _pagesByUrl;

        public QueryEngine(InvertedIndex index, IEnumerable<PageDocument> pages)
        {
            _index = index;
            // خريطة للوصول السريع للـ Title/Snippet
            _pagesByUrl = pages
                .GroupBy(p => p.Url)
                .Select(g => g.First())
                .ToDictionary(p => p.Url, p => p);
        }
        private string Highlight(string text, IEnumerable<string> words)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            foreach (var word in words)
            {
                if (word.Length < 3)
                    continue;

                text = System.Text.RegularExpressions.Regex.Replace(
                    text,
                    $"({System.Text.RegularExpressions.Regex.Escape(word)})",
                    "<b>$1</b>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }

            return text;
        }

        public List<SearchResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SearchResult>();

            // 1) Normalize + split
            var words = query
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();

            var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var word in words)
            {
                // ===== 2-A) Prefix match (أقوى وأسرع) =====
                var prefixTokens = _index.GetByPrefix(word);
                foreach (var t in prefixTokens)
                {
                    if (!scores.ContainsKey(t.Url))
                        scores[t.Url] = 0;

                    scores[t.Url] += (t.Source == TextSource.Title ? 3 : 1)
                                     * Math.Max(1, t.Count);
                }

                // ===== 2-B) Contains match (أضعف – للمرونة) =====
                var containsTokens = _index.GetByContains(word);
                foreach (var t in containsTokens)
                {
                    if (!scores.ContainsKey(t.Url))
                        scores[t.Url] = 0;

                    // وزن أقل عشان الرسمي يفضل فوق
                    scores[t.Url] += (t.Source == TextSource.Title ? 1.5 : 0.5)
                                     * Math.Max(1, t.Count);
                }
            }

            // ===== 3) Domain bonus (للمواقع الرسمية) =====
            foreach (var url in scores.Keys.ToList())
            {
                try
                {
                    var uri = new Uri(url);
                    var host = uri.Host.ToLowerInvariant();

                    // لو الدومين يحتوي كلمة البحث
                    if (words.Any(w => host.Contains(w.Replace(" ", ""))))
                        scores[url] += 5;

                    // الصفحة الرئيسية غالبًا الرسمية
                    if (uri.AbsolutePath == "/" || uri.AbsolutePath.Length <= 1)
                        scores[url] += 3;
                }
                catch { /* تجاهل */ }
            }

            // ===== 4) Build results + sort =====
            var results = scores
                .Select(kv =>
                {
                    _pagesByUrl.TryGetValue(kv.Key, out var page);
                    return new SearchResult
                    {
                        Url = kv.Key,
                        Title = page?.Title ?? kv.Key,
                        Snippet = Highlight(page?.Snippet ?? string.Empty, words),
                        Score = kv.Value
                    };
                })
                .OrderByDescending(r => r.Score)
                .Take(20)
                .ToList();

            return results;
        }

    }
}
