using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SearchEngine.Core
{
    public class Tokenizer
    {
        public IEnumerable<TextToken> Tokenize(string text, TextSource source, string url)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            // 1) Normalize
            text = text.ToLowerInvariant();

            // 2) Split on non-letters (existing behavior)
            var roughWords = Regex.Split(text, @"[^a-z0-9]+");

            foreach (var rough in roughWords)
            {
                if (rough.Length < 2)
                    continue;

                // 3) Split camelCase / PascalCase (NEW)
                foreach (var word in SplitOnWordBoundaries(rough))
                {
                    if (word.Length < 2)
                        continue;

                    yield return new TextToken
                    {
                        Word = word,
                        Source = source,
                        Url = url,
                        Count = 1
                    };
                }
            }
        }

        private IEnumerable<string> SplitOnWordBoundaries(string word)
        {
            // geeksforgeeks → geeksforgeeks (no capitals)
            // visualstudiocode → visualstudiocode
            // BUT:
            // VisualStudioCode → visual | studio | code

            var parts = Regex.Split(word, @"(?<!^)(?=[A-Z])");

            foreach (var p in parts)
            {
                var w = p.Trim();
                if (!string.IsNullOrEmpty(w))
                    yield return w;
            }
        }
    }
}
