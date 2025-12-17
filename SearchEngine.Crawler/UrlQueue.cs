using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SearchEngine.Crawler
{
    internal class UrlQueue
    {
       private ConcurrentQueue<UrlEntry> _queue = new ConcurrentQueue<UrlEntry>();
        public bool IsEmpty { get { return _queue.IsEmpty; } }



        public void Enqueue(UrlEntry entry) {
        
            _queue.Enqueue(entry);
        
        }
        public bool TryDequeue(out UrlEntry entry)
        {
            try
            {
                if (_queue.TryDequeue(out var item))
                {
                    entry = item;
                    return true;
                }

                entry = null;
                return false;
            }
            catch (Exception)
            {
         
                entry = null;
                return false;
            }
        }


    }
}

