using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SearchEngine.Core
{
    public class NdjsonPageReader
    {
        public IEnumerable<PageDocument> Read(string filePath)
        {
            // تأكد إن الملف موجود
            if (!File.Exists(filePath))
                yield break;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                PageDocument? doc = null;
                try
                {
                    doc = JsonSerializer.Deserialize<PageDocument>(line);
                }
                catch
                {
                    // لو السطر بايظ نتجاهله ونكمّل
                    continue;
                }

                if (doc != null && !string.IsNullOrWhiteSpace(doc.Url))
                    yield return doc;
            }
        }
    }
}
