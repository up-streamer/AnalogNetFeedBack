/* Copyright (c) 2014 Oberon microsystems, Inc. (Switzerland)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License. */

// Originally developed for the book
//   "Getting Started with the Internet of Things", by Cuno Pfister.
//   Copyright 2011 Cuno Pfister, Inc., 978-1-4493-9357-1.
//
// Version 4.3, for the .NET Micro Framework release 4.3.
//
// See Microsoft's API documentation for details:
// http://msdn.microsoft.com/en-us/library/system.threading.threadpool(v=vs.110).aspx

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace System.Threading
{
    /// <summary>
    /// Represents a callback method to be executed by a thread pool thread.
    /// </summary>
    /// <param name="state">An object containing information to be used by the callback method.</param>
    public delegate void WaitCallback(object state);

    /// <summary>
    /// Provides a pool of threads that can be used to execute tasks, post work items,
    /// and wait on behalf of other threads.
    /// </summary>
    public static class ThreadPool
    {
        static int threadCount = 8;         // default, at least enough for four open sockets plus one server thread (i.e., threadCount >= 5)
        static Thread[] threads;
        static BoundedQueue workItemQueue;

        /// <summary>
        /// Sets the number of requests to the thread pool than can be active concurrently.
        /// All requests above that number remain queued until thread pool threads become available.
        /// </summary>
        /// <param name="workerThreads">The maximum number of worker threads in the thread pool.</param>
        /// <param name="completionPortThreads">The maximum number of asynchronous I/O threads in the thread pool. For NETMF, it must be 0.</param>
        /// <returns>true if the change is successful; otherwise, false.</returns>
        public static bool SetMinThreads(int workerThreads, int completionPortThreads)
        {
            Contract.Requires(workerThreads > 0);
            Contract.Requires(completionPortThreads == 0);      // NETMF has basically synchronous APIs
            Contract.Requires(threads == null);                 // cannot change thread count when pool is already in use
            threadCount = workerThreads;
            Open(threadCount);
            return true;
        }

        static void Open(int nofThreads)
        {
            // threads == null
            // nofThreads > 0
            threads = new Thread[nofThreads];
            var i = 0;
            while (i != threads.Length)
            {
                var t = new Thread(Run);
                t.Priority = ThreadPriority.Lowest;
                threads[i] = t;
                i = i + 1;
            }
            workItemQueue = new BoundedQueue(nofThreads);
        }

        /// <summary>
        /// Queues a method for execution, and specifies an object containing data to
        /// be used by the method. The method executes when a thread pool thread becomes
        /// available.
        /// </summary>
        /// <param name="callback">A System.Threading.WaitCallback representing the method to execute.</param>
        /// <param name="state">An object containing data to be used by the method.</param>
        /// <returns>true if the method is successfully queued; System.NotSupportedException is
        /// thrown if the work item could not be queued.</returns>
        public static bool QueueUserWorkItem(WaitCallback callback,
                                                object state)
        {
            Contract.Requires(callback != null);
            if (threads == null)
            {
                Open(threadCount);
                var i = 0;
                while (i != threads.Length)
                {
                    threads[i].Start();
                    i = i + 1;
                }
            }
            var item = new WorkItem(callback, state);
            workItemQueue.Enqueue(item);                // blocks if queue is full
            return true;
        }

        static void Run()
        {
            while (true)
            {
                var obj = workItemQueue.Dequeue();      // blocks if queue is empty
                var item = (WorkItem)obj;
                try
                {
                    item.callback(item.state);
                }
                catch (Exception e)
                {
                    Trace.Fail("ThreadPool exception in callback:\r\n" + e);
                }
            }
        }
    }

    // private types

    class WorkItem
    {
        internal readonly WaitCallback callback;
        internal readonly object state;

        internal WorkItem(WaitCallback c, object s)
        {
            Contract.Requires(c != null);
            callback = c;
            state = s;
        }
    }

    class BoundedQueue
    {
        readonly ManualResetEvent nonEmptySignal;       // where (nonEmptySignal != null)
        readonly ManualResetEvent nonFullSignal;        // where (nonFullSignal != null)
        readonly int capacity;                          // where (capacity > 0)
        readonly Queue queue;                           // where (queue != null)
        
        readonly static object TheLock = new object();

        internal BoundedQueue(int c)
        {
            // c > 0
            nonEmptySignal = new ManualResetEvent(false);
            nonFullSignal = new ManualResetEvent(false);
            capacity = c;
            queue = new Queue();
        }

        internal void Enqueue(object o)
        {
            // this method may block
            // o != null
            var done = false;
            do
            {
                nonFullSignal.WaitOne();
                // queue may have space for another work item
                lock (TheLock)
                {
                    // make sure that queue still has space for another work item
                    if (queue.Count != capacity)
                    {
                        queue.Enqueue(o);
                        // queue may now be full
                        if (queue.Count == capacity)
                        {
                            nonFullSignal.Reset();
                        }
                        nonEmptySignal.Set();
                        done = true;
                    }
                }
            }
            while (!done);
        }

        internal object Dequeue()
        {
            // this method may block
            object o = null;
            do
            {
                nonEmptySignal.WaitOne();
                // queue may contain a work item
                lock (TheLock)
                {
                    // make sure that queue still contains a work item
                    if (queue.Count != 0)
                    {
                        o = queue.Dequeue();
                        // o != null
                        // queue may now be emtpy
                        if (queue.Count == 0)
                        {
                            nonEmptySignal.Reset();
                        }
                        nonFullSignal.Set();
                    }
                }
            }
            while (o == null);
            // o != null
            return o;
        }
    }
}
