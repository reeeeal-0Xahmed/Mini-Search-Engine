// File: HtmlParser.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace SearchEngine.Crawler
{
    internal class ParseResult
    {
        public string Title { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public List<string> Links { get; set; } = new List<string>();
        public int OutlinksCount => Links?.Count ?? 0;
        public string? Favicon { get; set; } = null;
        public string Snippet { get; set; } = string.Empty;
    }

    internal class HtmlParser
    {
        private readonly AngleSharp.Html.Parser.HtmlParser _parser;
        private readonly int _maxLinks;
        private readonly HashSet<string>? _allowedDomainsLower;
        private const int SnippetMaxLength = 250;

        public HtmlParser(int maxLinks = 0, IEnumerable<string>? allowedDomains = null)
        {
            _parser = new AngleSharp.Html.Parser.HtmlParser();
            _maxLinks = Math.Max(0, maxLinks);
            if (allowedDomains != null)
            {
                _allowedDomainsLower = new HashSet<string>(allowedDomains
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim().ToLowerInvariant()));
            }
        }

        private static string? ResolveHrefToAbsolute(string? href, string? resolvedBase)
        {
            if (string.IsNullOrWhiteSpace(href)) return null;

            href = href.Trim();

            var low = href.ToLowerInvariant();
            if (low.StartsWith("javascript:") || low.StartsWith("mailto:") || low.StartsWith("tel:") || low.StartsWith("#"))
                return null;

            // protocol-relative: //example.com/path
            if (href.StartsWith("//"))
            {
                var scheme = "https:";
                if (!string.IsNullOrWhiteSpace(resolvedBase))
                {
                    try
                    {
                        var b = new Uri(resolvedBase);
                        scheme = b.Scheme + ":";
                    }
                    catch { /* keep https: */ }
                }

                return scheme + href;
            }

            // absolute
            if (Uri.IsWellFormedUriString(href, UriKind.Absolute))
                return href;

            // relative + base
            if (!string.IsNullOrWhiteSpace(resolvedBase))
            {
                try
                {
                    if (Uri.TryCreate(new Uri(resolvedBase), href, out var resolved))
                    {
                        var ub = new UriBuilder(resolved) { Fragment = "" };
                        return ub.Uri.AbsoluteUri;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public async Task<ParseResult> ParseAsync(string html, string? baseUrl)
        {
            var result = new ParseResult();
            if (string.IsNullOrWhiteSpace(html)) return result;

            IDocument document;
            try
            {
                document = await _parser.ParseDocumentAsync(html);
            }
            catch
            {
                return result;
            }

            // Determine reliable base to resolve relative URLs
            string? resolvedBase = null;
            if (!string.IsNullOrWhiteSpace(document.BaseUri)
                && Uri.IsWellFormedUriString(document.BaseUri, UriKind.Absolute)
                && !document.BaseUri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                resolvedBase = document.BaseUri;
            }
            else if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.IsWellFormedUriString(baseUrl, UriKind.Absolute))
            {
                resolvedBase = baseUrl;
            }

            // Title
            try { result.Title = document.Title?.Trim() ?? string.Empty; } catch { result.Title = string.Empty; }

            // Meta description
            try
            {
                var meta = document.QuerySelector("meta[name=description]") as IElement;
                result.MetaDescription = meta?.GetAttribute("content")?.Trim() ?? string.Empty;
            }
            catch { result.MetaDescription = string.Empty; }

            // Snippet: first non-empty <p> or meta description
            try
            {
                var firstP = document.QuerySelectorAll("p")
                                     .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.TextContent));
                if (firstP != null)
                    result.Snippet = firstP.TextContent.Trim();
                else
                    result.Snippet = result.MetaDescription ?? string.Empty;

                if (!string.IsNullOrEmpty(result.Snippet) && result.Snippet.Length > SnippetMaxLength)
                    result.Snippet = result.Snippet.Substring(0, SnippetMaxLength).Trim() + "...";
            }
            catch
            {
                result.Snippet = string.Empty;
            }

            // Favicon: try once using link rel and resolve + normalize
            try
            {
                var linkEl = document.QuerySelector("link[rel~='icon'], link[rel='shortcut icon']") as IElement;
                string? faviconHref = linkEl?.GetAttribute("href")?.Trim();

                var resolvedFav = ResolveHrefToAbsolute(faviconHref, resolvedBase);

                if (string.IsNullOrWhiteSpace(resolvedFav) && !string.IsNullOrWhiteSpace(resolvedBase))
                {
                    try
                    {
                        if (Uri.TryCreate(resolvedBase, UriKind.Absolute, out var b))
                            resolvedFav = new UriBuilder(b) { Path = "/favicon.ico", Query = "", Fragment = "" }.Uri.AbsoluteUri;
                    }
                    catch { /* ignore */ }
                }

                if (!string.IsNullOrWhiteSpace(resolvedFav))
                    resolvedFav = UrlUtils.NormalizeUrl(resolvedFav);

                if (!string.IsNullOrWhiteSpace(resolvedFav) && Uri.IsWellFormedUriString(resolvedFav, UriKind.Absolute))
                    result.Favicon = resolvedFav;
                else
                    result.Favicon = null;
            }
            catch
            {
                result.Favicon = null;
            }

            // Links: collect anchors, resolve, normalize, filter early, stop at _maxLinks
            var linksSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var anchors = document.QuerySelectorAll("a[href]");

                foreach (var el in anchors)
                {
                    if (_maxLinks > 0 && linksSet.Count >= _maxLinks) break;

                    var hrefRaw = el.GetAttribute("href")?.Trim();
                    var abs = ResolveHrefToAbsolute(hrefRaw, resolvedBase);
                    if (string.IsNullOrWhiteSpace(abs)) continue;

                    // normalize url
                    abs = UrlUtils.NormalizeUrl(abs);
                    if (string.IsNullOrWhiteSpace(abs)) continue;

                    // remove fragment (should be removed by NormalizeUrl, but keep safe)
                    var hashIdx = abs.IndexOf('#');
                    if (hashIdx >= 0) abs = abs.Substring(0, hashIdx);

                    // domain filtering if provided
                    if (_allowedDomainsLower != null)
                    {
                        try
                        {
                            var host = new Uri(abs).Host.ToLowerInvariant();
                            bool hostOk = false;
                            foreach (var allowed in _allowedDomainsLower)
                            {
                                if (host == allowed || host.EndsWith("." + allowed))
                                {
                                    hostOk = true;
                                    break;
                                }
                            }
                            if (!hostOk) continue;
                        }
                        catch { continue; }
                    }

                    linksSet.Add(abs);
                }

                result.Links = linksSet.ToList();
            }
            catch
            {
                result.Links = new List<string>();
            }

            return result;
        }
    }
}
