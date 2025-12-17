using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SearchEngine.Crawler
{
    /// <summary>
    /// Hybrid UrlFilter: Bloom Filter (bit array) + HashSet for exact membership.
    /// Provides: Normalize, Contains, Add, Clear, periodic persistence (save/load).
    /// </summary>
    internal class UrlFilter : IDisposable
    {
        // ---- Configurable defaults ----
        private readonly int _expectedItems;
        private readonly double _falsePositiveRate;

        // Bloom internals
        private readonly long _m; // number of bits
        private readonly int _k; // number of hash functions
        private readonly byte[] _bits; // backing bytes for bit array (length = (_m+7)/8)

        // HashSet to guarantee exactness
        private readonly HashSet<string> _set;

        // locks & concurrency
        private readonly object _lock = new();

        // Persistence
        private readonly string _bloomPath;
        private readonly string _metaPath;
        private readonly string _tempPath;
        private readonly string _bakPath;

        private CancellationTokenSource? _saveCts;
        private Task? _saveTask;
        private TimeSpan _saveInterval;

        // SHA256 instance reused for hashing
        private readonly SHA256 _sha256 = SHA256.Create();

        // Constructor
        public UrlFilter(
            int expectedItems = 10_000,
            double falsePositiveRate = 0.01,
            TimeSpan? saveInterval = null,
            string? bloomPath = null)
        {
            if (expectedItems <= 0) throw new ArgumentOutOfRangeException(nameof(expectedItems));
            if (falsePositiveRate <= 0 || falsePositiveRate >= 1) throw new ArgumentOutOfRangeException(nameof(falsePositiveRate));

            _expectedItems = expectedItems;
            _falsePositiveRate = falsePositiveRate;
            _saveInterval = saveInterval ?? TimeSpan.FromMinutes(5);

            // default file paths (in current directory)
            _bloomPath = bloomPath ?? Path.Combine(AppContext.BaseDirectory, "bloom.dat");
            _metaPath = _bloomPath + ".meta.json";
            _tempPath = _bloomPath + ".tmp";
            _bakPath = _bloomPath + ".bak";

            // compute m and k
            // m = - (n * ln p) / (ln2)^2
            // k = (m/n) * ln2
            double ln2 = Math.Log(2.0);
            double mDouble = -(_expectedItems * Math.Log(_falsePositiveRate)) / (ln2 * ln2);
            _m = (long)Math.Ceiling(mDouble);
            double kDouble = (mDouble / _expectedItems) * ln2;
            _k = Math.Max(1, (int)Math.Round(kDouble));

            // allocate backing bytes
            long byteLen = (_m + 7) / 8;
            _bits = new byte[byteLen];

            _set = new HashSet<string>(StringComparer.Ordinal);

            // attempt to load existing bloom if present
            try
            {
                LoadFromDiskIfExists();
            }
            catch
            {
                // ignore load errors; continue with fresh bloom
            }
        }

        // -----------------------------
        // Public API
        // -----------------------------

        /// <summary>
        /// Normalize a URL according to rules (lowercase host/scheme, remove trailing slash, remove fragment).
        /// Keeps query string.
        /// If url is relative or invalid, returns trimmed-lower string.
        /// </summary>
        public string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;

            url = url.Trim();

            // quick lower-casing for simple inputs
            try
            {
                Uri uri = new Uri(url, UriKind.RelativeOrAbsolute);
                if (!uri.IsAbsoluteUri)
                {
                    // relative -> lower + trim
                    return url.ToLowerInvariant();
                }

                // canonicalize components
                string scheme = uri.Scheme.ToLowerInvariant();
                string host = uri.Host.ToLowerInvariant();
                string path = uri.AbsolutePath;
                if (!string.IsNullOrEmpty(path))
                {
                    path = path.TrimEnd('/');
                }
                string query = uri.Query; // keep as-is (starts with ? or empty)
                // ignore fragment
                string normalized = $"{scheme}://{host}{path}{query}";
                if (normalized.EndsWith("://")) normalized = normalized.TrimEnd(':', '/');
                return normalized;
            }
            catch
            {
                // fallback: simple normalization
                return url.ToLowerInvariant();
            }
        }

        /// <summary>
        /// Check if URL is considered visited.
        /// Uses Bloom first, then HashSet to confirm.
        /// Thread-safe.
        /// </summary>
        public bool Contains(string url)
        {
            var norm = NormalizeUrl(url);
            if (string.IsNullOrEmpty(norm)) return false;

            // fast bloom check (may be false positive)
            bool bloomHas;
            lock (_lock)
            {
                bloomHas = BloomContains(norm);
                if (!bloomHas) return false; // definitely not present
                // bloom says maybe, confirm with hashset
                return _set.Contains(norm);
            }
        }

        /// <summary>
        /// Add URL to set: if not present, add to HashSet and Bloom. Thread-safe.
        /// </summary>
        public void Add(string url)
        {
            var norm = NormalizeUrl(url);
            if (string.IsNullOrEmpty(norm)) return;

            lock (_lock)
            {
                if (_set.Contains(norm)) return;
                _set.Add(norm);
                BloomAdd(norm);
            }
        }

        /// <summary>
        /// Clear both HashSet and Bloom (reinitialize bits).
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _set.Clear();
                Array.Clear(_bits, 0, _bits.Length);
            }
        }

        // -----------------------------
        // Bloom internal helpers
        // -----------------------------

        private void BloomAdd(string value)
        {
            var hashes = ComputeHashes(value);
            foreach (var h in hashes)
            {
                SetBit((long)(h % (ulong)_m));


            }
        }

        private bool BloomContains(string value)
        {
            var hashes = ComputeHashes(value);
            foreach (var h in hashes)
            {
                if (GetBit((long)(h % (ulong)_m))) return false;
            }
            return true;
        }

        // Compute k hash values using SHA256 with different salts
        private ulong[] ComputeHashes(string value)
        {
            // Using SHA256: feed value + index to produce deterministic hashes
            // We'll produce k 64-bit hashes by slicing SHA256 output.
            var bytes = Encoding.UTF8.GetBytes(value);
            ulong[] result = new ulong[_k];

            // produce as many bytes as needed
            int neededBytes = _k * 8; // 8 bytes per 64-bit hash
            byte[] output = new byte[neededBytes];
            int produced = 0;
            int round = 0;
            while (produced < neededBytes)
            {
                // create buffer: value bytes + round byte
                byte[] buf = new byte[bytes.Length + 4];
                Buffer.BlockCopy(bytes, 0, buf, 0, bytes.Length);
                // append round as 4-byte int
                var r = BitConverter.GetBytes(round);
                Buffer.BlockCopy(r, 0, buf, bytes.Length, 4);

                byte[] hash = _sha256.ComputeHash(buf);
                int toCopy = Math.Min(hash.Length, neededBytes - produced);
                Buffer.BlockCopy(hash, 0, output, produced, toCopy);
                produced += toCopy;
                round++;
            }

            // convert the output into ulong values
            for (int i = 0; i < _k; i++)
            {
                int offset = i * 8;
                result[i] = BitConverter.ToUInt64(output, offset);
            }

            return result;
        }

        private bool GetBit(long index)
        {
            long byteIndex = index / 8;
            int bitIndex = (int)(index % 8);
            return (_bits[byteIndex] & (1 << bitIndex)) != 0;
        }

        private void SetBit(long index)
        {
            long byteIndex = index / 8;
            int bitIndex = (int)(index % 8);
            _bits[byteIndex] |= (byte)(1 << bitIndex);
        }

        // -----------------------------
        // Persistence: ExportBits, SaveNow, Start/StopPeriodicSave, LoadFromDiskIfExists
        // -----------------------------

        /// <summary>
        /// Export a snapshot (copy) of the bloom backing bytes under lock.
        /// Returns a new byte[] copy.
        /// </summary>
        public byte[] ExportBits()
        {
            lock (_lock)
            {
                var copy = new byte[_bits.Length];
                Array.Copy(_bits, copy, _bits.Length);
                return copy;
            }
        }

        /// <summary>
        /// Save now: writes snapshot + metadata to disk atomically (temp -> replace).
        /// </summary>
        public void SaveNow()
        {
            byte[] bytes;
            var meta = new BloomMeta
            {
                Version = 1,
                BitSize = _m,
                HashCount = _k,
                ExpectedItems = _expectedItems,
                FalsePositiveRate = _falsePositiveRate,
                Timestamp = DateTime.UtcNow
            };

            // take snapshot quickly under lock
            bytes = ExportBits();

            // write to temp files
            try
            {
                // metadata
                var metaJson = JsonSerializer.Serialize(meta);
                File.WriteAllText(_metaPath + ".tmp", metaJson, Encoding.UTF8);

                // bloom bits
                File.WriteAllBytes(_tempPath, bytes);

                // atomic replace: backup old files then move temp to final
                if (File.Exists(_bloomPath))
                {
                    // replace bloom (create backup)
                    File.Copy(_bloomPath, _bakPath, overwrite: true);
                    File.Delete(_bloomPath);
                }
                File.Move(_tempPath, _bloomPath);

                // replace meta
                if (File.Exists(_metaPath))
                {
                    File.Copy(_metaPath, _metaPath + ".bak", overwrite: true);
                    File.Delete(_metaPath);
                }
                File.Move(_metaPath + ".tmp", _metaPath);
            }
            catch (Exception ex)
            {
                // best-effort logging (Console). In real app use proper logger.
                Console.WriteLine($"[UrlFilter.SaveNow] Failed to save bloom: {ex.Message}");
                // cleanup temp files if present
                try { if (File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
                try { if (File.Exists(_metaPath + ".tmp")) File.Delete(_metaPath + ".tmp"); } catch { }
            }
        }

        /// <summary>
        /// Start periodic save loop (if already started, this is no-op).
        /// </summary>
        public void StartPeriodicSave()
        {
            lock (_lock)
            {
                if (_saveTask != null && !_saveTask.IsCompleted) return; // already running

                _saveCts = new CancellationTokenSource();
                var token = _saveCts.Token;
                _saveTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            await Task.Delay(_saveInterval, token);
                            SaveNow();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // expected on cancellation
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UrlFilter.StartPeriodicSave] periodic error: {ex.Message}");
                    }
                }, token);
            }
        }

        /// <summary>
        /// Stop periodic save and perform a final SaveNow.
        /// </summary>
        public void StopPeriodicSave()
        {
            // cancel and wait for task
            CancellationTokenSource? cts;
            Task? task;
            lock (_lock)
            {
                cts = _saveCts;
                task = _saveTask;
                _saveCts = null;
                _saveTask = null;
            }

            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                    task?.Wait(TimeSpan.FromSeconds(10));
                }
                catch { /* swallow */ }
                finally
                {
                    cts.Dispose();
                }
            }

            // attempt final save
            SaveNow();
        }

        /// <summary>
        /// Load from disk if bloom metadata + file exist and compatible.
        /// </summary>
        private void LoadFromDiskIfExists()
        {
            if (!File.Exists(_bloomPath) || !File.Exists(_metaPath)) return;

            try
            {
                string metaJson = File.ReadAllText(_metaPath, Encoding.UTF8);
                var meta = JsonSerializer.Deserialize<BloomMeta>(metaJson);
                if (meta == null) return;

                if (meta.BitSize != _m || meta.HashCount != _k)
                {
                    // incompatible parameters -> ignore saved bloom
                    Console.WriteLine("[UrlFilter] saved bloom incompatible with current parameters => ignoring");
                    return;
                }

                byte[] bytes = File.ReadAllBytes(_bloomPath);
                if (bytes.Length != _bits.Length)
                {
                    Console.WriteLine("[UrlFilter] saved bloom bit array length mismatch => ignoring");
                    return;
                }

                lock (_lock)
                {
                    Array.Copy(bytes, _bits, bytes.Length);
                }

                Console.WriteLine("[UrlFilter] Bloom loaded from disk.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UrlFilter] Failed to load bloom: {ex.Message}");
                // try using backup
                try
                {
                    if (File.Exists(_bakPath))
                    {
                        byte[] bytes = File.ReadAllBytes(_bakPath);
                        if (bytes.Length == _bits.Length)
                        {
                            lock (_lock) Array.Copy(bytes, _bits, bytes.Length);
                            Console.WriteLine("[UrlFilter] Bloom loaded from backup.");
                        }
                    }
                }
                catch { /* ignore */ }
            }
        }

        // -----------------------------
        // Metadata class for saving
        // -----------------------------
        private class BloomMeta
        {
            public int Version { get; set; }
            public long BitSize { get; set; }
            public int HashCount { get; set; }
            public int ExpectedItems { get; set; }
            public double FalsePositiveRate { get; set; }
            public DateTime Timestamp { get; set; }
        }

        // Dispose pattern
        public void Dispose()
        {
            try
            {
                StopPeriodicSave();
            }
            catch { }
            _sha256?.Dispose();
        }
    }
}
