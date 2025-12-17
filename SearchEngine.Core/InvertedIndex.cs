// File: InvertedIndex.cs
using System.Collections.Generic;

namespace SearchEngine.Core
{
    public class InvertedIndex
    {
        private readonly Dictionary<string, List<TextToken>> _index
            = new Dictionary<string, List<TextToken>>();

        public void AddTokens(IEnumerable<TextToken> tokens)
        {
            // TODO:
            // for each token:
            //   if word not exists -> create list
            //   add token to list

            foreach (var token in tokens)
            {
                if (!_index.TryGetValue(token.Word, out var list))
                {
                    list = new List<TextToken>();
                    _index[token.Word] = list;
                }

                list.Add(token);
            }
        }

        public List<TextToken> Get(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return new List<TextToken>();

            word = word.ToLowerInvariant();

            if (_index.TryGetValue(word, out var list))
                return list;

            return new List<TextToken>();
        }
        public IEnumerable<TextToken> GetByContains(string term)
        {
            term = term.ToLowerInvariant();

            foreach (var kv in _index)
            {
                // kv.Key = token term (مثال: geeksforgeeks)
                if (kv.Key.Contains(term))
                {
                    foreach (var token in kv.Value)
                        yield return token;
                }
            }
        }

        public IEnumerable<TextToken> GetByPrefix(string prefix)
        {
            prefix = prefix.ToLowerInvariant();

            foreach (var kv in _index)
            {
                if (kv.Key.StartsWith(prefix))
                {
                    foreach (var token in kv.Value)
                        yield return token;
                }
            }
        }

    }
}
