using System;

namespace SearchEngine.Core
{
    public class Indexer
    {
        private readonly Tokenizer _tokenizer;
        private readonly InvertedIndex _index;

        public Indexer(Tokenizer tokenizer, InvertedIndex index)
        {
            _tokenizer = tokenizer;
            _index = index;
        }

        public void BuildFromNdjson(string filePath)
        {
            var reader = new NdjsonPageReader();

            foreach (var page in reader.Read(filePath))
            {
                // Title tokens
                if (!string.IsNullOrWhiteSpace(page.Title))
                {
                    var titleTokens = _tokenizer.Tokenize(
                        page.Title,
                        TextSource.Title,
                        page.Url
                    );
                    _index.AddTokens(titleTokens);
                }

                // Snippet tokens
                if (!string.IsNullOrWhiteSpace(page.Snippet))
                {
                    var snippetTokens = _tokenizer.Tokenize(
                        page.Snippet,
                        TextSource.Snippet,
                        page.Url
                    );
                    _index.AddTokens(snippetTokens);
                }
            }
        }
    }
}
