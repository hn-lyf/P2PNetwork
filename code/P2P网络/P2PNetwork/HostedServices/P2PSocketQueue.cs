using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace P2PNetwork.HostedServices
{
    public class P2PSocketQueue
    {
        private readonly ConcurrentQueue<byte[]> writeDataQueue;
        private readonly ConcurrentQueue<byte[]> readDataQueue;//接收队列
        private readonly Logger<P2PSocketQueue> _logger;

        public ConcurrentQueue<byte[]> WriteDataQueue => writeDataQueue;

        public ConcurrentQueue<byte[]> ReadDataQueue => readDataQueue;

        public P2PSocketQueue(Logger<P2PSocketQueue> logger)
        {
            _logger = logger;
            writeDataQueue = new ConcurrentQueue<byte[]>();
            readDataQueue = new ConcurrentQueue<byte[]>();
        }
        public virtual void WriteWriteDataQueue(byte[] bytes)
        {
            WriteDataQueue.Enqueue(bytes);
        }
        public virtual void WriteReadDataQueue(byte[] bytes)
        {
            ReadDataQueue.Enqueue(bytes);
        }
    }
}
