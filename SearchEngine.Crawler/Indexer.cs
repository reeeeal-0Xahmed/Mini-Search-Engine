// File: Indexer.cs
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SearchEngine.Crawler
{
    /// <summary>
    /// العنصر الذي نكتب كل سطر NDJSON بواسطته.
    /// نخزن الحقول المطلوبة فقط: Url, Title, Favicon, Snippet, FetchedAt
    /// </summary>
    internal class IndexItem
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Favicon { get; set; } = null;
        public string Snippet { get; set; } = string.Empty;
        public DateTimeOffset FetchedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Indexer بسيط يكتب كل IndexItem كسطر NDJSON في ملف.
    /// آمن للاستخدام من عدة مهام via SemaphoreSlim.
    /// </summary>
    internal class Indexer : IDisposable
    {
        private readonly string _filePath;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;

        public Indexer(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };

            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// Appends one item as a newline-delimited JSON (NDJSON).
        /// </summary>
        public async Task AppendAsync(IndexItem item, CancellationToken ct = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            string line = JsonSerializer.Serialize(item, _jsonOptions);

            await _writeLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // Append (text + newline) in UTF-8
                await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, ct).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Dispose()
        {
            _writeLock?.Dispose();
        }
    }
}
