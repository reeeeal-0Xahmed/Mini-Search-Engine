// File: UrlUtils.cs
using System;

namespace SearchEngine.Crawler
{
    internal static class UrlUtils
    {
        public static string? NormalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            url = url.Trim();

            // Ensure absolute URI (try add https:// if missing)
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                if (!Uri.TryCreate("https://" + url, UriKind.Absolute, out uri))
                    return null;
            }

            try
            {
                var builder = new UriBuilder(uri)
                {
                    Fragment = "" // remove fragment
                };

                // remove default ports to normalize
                if ((builder.Scheme == "http" && builder.Port == 80) ||
                    (builder.Scheme == "https" && builder.Port == 443))
                {
                    builder.Port = -1;
                }

                // host lowercase
                builder.Host = builder.Host.ToLowerInvariant();

                var normalized = builder.Uri.AbsoluteUri;

                // remove trailing slash for non-root paths
                if (builder.Path != "/" && normalized.EndsWith("/"))
                {
                    normalized = normalized.TrimEnd('/');
                }

                return normalized;
            }
            catch
            {
                return uri.AbsoluteUri;
            }
        }
    }
}
