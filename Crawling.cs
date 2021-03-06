﻿using ConsoleApp.Data;
using ConsoleApp.Task;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


namespace ConsoleApp.Task
{
    class Crawling
    {
        static BlockingCollection<PageUrls> listOfCrawledUrls = new BlockingCollection<PageUrls>();
        static BlockingCollection<string> urls = new BlockingCollection<string>();
        static BlockingCollection<Queue<PageUrls>> listOfQueue = new BlockingCollection<Queue<PageUrls>>();
        Random rand = new Random();
        private IRepository _repo;
        public Crawling(IRepository repo)
        {
            _repo = repo;
        }
        public void StartCrawl(string url, int threadsCount)
        {
            System.Threading.Tasks.Task.Run(() => ConsumerDbContext());
            Thread[] threads = new Thread[threadsCount];
            int halfOfThreads = threads.Length / 2;
            for (int i = 0; i < halfOfThreads; i++)
            {
                listOfQueue.Add(new Queue<PageUrls>());
            }
            for (int i = 0; i < halfOfThreads; i++)
            {
                var iteratorQueue = listOfQueue.ElementAt(i);
                threads[i]= new Thread(() => StartCrawlOneThread(url, iteratorQueue));
                threads[i+halfOfThreads]= new Thread(() => Consumer(iteratorQueue));
                threads[i].Start();
                threads[i+halfOfThreads].Start();
            }
            if (threads.Length % 2 != 0)
            {
                var iteratorQueue = listOfQueue.ElementAt(0);
                threads[threads.Length - 1] = new Thread(() => StartCrawlOneThread(url, iteratorQueue));
                threads[threads.Length - 1].Start();
            }
                
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }
        }
        PageUrls CrawlOneUrlProducer(string fullUrl, Queue<PageUrls> queue)
        {
            Measures m = new Measures();
            var res = new PageUrls();
            lock(queue) 
            {
                if (!urls.Any(r => r == fullUrl)&& !queue.Any(r => r.Url == fullUrl))
                {
                    res = m.TakeMesuares(fullUrl);
                    queue.Enqueue(res);
                    Monitor.Pulse(queue);             
                    Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " Put in queue" + fullUrl);
                }
            }
            return res;
        }
        void CrawlInternalUrls(PageUrls r, Queue<PageUrls> queue)
        {
            if (r.InternalUrls != null)
            {
                foreach (var url in r.InternalUrls)
                {
                    CrawlOneUrlProducer(url.Url, queue);
                }
            }
        }
        void CrawlExternalUrls(PageUrls r, Queue<PageUrls> queue)
        {
            if (r.ExternalUrls != null)
            {
                foreach (var url in r.ExternalUrls)
                {
                    CrawlOneUrlProducer(url.Url, queue);
                }
            }
        }
        void StartCrawlOneThread(string url, Queue<PageUrls> queue)
        {
            var r = CrawlOneUrlProducer(url,queue);
            CrawlInternalUrls(r,queue);
            CrawlExternalUrls(r, queue);
            for (int i = 0; i < listOfCrawledUrls.Count; i++)
            {
                var temp = listOfCrawledUrls.ElementAt(i);
                var random = rand.Next(2, 4);
                if (random % 2 == 0)
                {
                    CrawlInternalUrls(temp,queue);
                    CrawlExternalUrls(temp, queue);
                }
                else
                {
                    CrawlExternalUrls(temp, queue);
                    CrawlInternalUrls(temp, queue);
                }
                if (i == 1)
                {
                    int x = i;
                }
            }
        }
        void Consumer(Queue<PageUrls> queue)
        {
            while (true)
            {
                lock(queue) //(sharedQueue)
                {
                    while (queue.Count == 0)//(sharedQueue.Count == 0)
                        Monitor.Wait(queue);
                    var res = queue.Dequeue();
                    if(!urls.Any(r => r == res.Url) && !listOfCrawledUrls.Any(r=>r.Url== res.Url))
                    {
                        listOfCrawledUrls.Add(res);
                        urls.Add(res.Url);
                    }                  
                    Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " Processing to list url: " + res.Url);
                }
            }
        }
        void ConsumerDbContext()
        {
            if (listOfCrawledUrls.Count == 0)
                Thread.Sleep(3000);
            while (listOfCrawledUrls.Count != 0)
            {
                int count = listOfCrawledUrls.Count;
                IEnumerable<PageUrls> items = listOfCrawledUrls.Take(count).ToArray();
                _repo.Add(items);
                for (int i = 0; i < count; i++)
                {
                    listOfCrawledUrls.Take();
                }
                Console.WriteLine("Saving to DB");
                Thread.Sleep(20000);
            }
        }
    }
}
