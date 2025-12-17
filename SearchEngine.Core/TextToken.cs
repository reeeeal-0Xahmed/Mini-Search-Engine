using System;
using System.Collections.Generic;
using System.Text;

namespace SearchEngine.Core
{
   
        public class TextToken
        {
            public string Word { get; set; }
            public TextSource Source { get; set; }
            public int Count { get; set; }
            public string Url { get; set; }
        }
    
}
