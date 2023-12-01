using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace P2PNetwork.HostedServices
{
    public class TunNetworkDrive
    {
        private readonly ConcurrentQueue<byte[]> writeDataQueue;
        private readonly ConcurrentQueue<byte[]> readDataQueue;//接收队列
        private readonly ConcurrentDictionary<int, P2PSocketQueue> p2pNetwork;
        private readonly Logger<TunNetworkDrive> _logger;

        public ConcurrentQueue<byte[]> WriteDataQueue => writeDataQueue;

        public ConcurrentQueue<byte[]> ReadDataQueue => readDataQueue;

        public ConcurrentDictionary<int, P2PSocketQueue> P2pNetwork => p2pNetwork;

        public TunNetworkDrive(Logger<TunNetworkDrive> logger)
        {
            _logger = logger;
            writeDataQueue = new ConcurrentQueue<byte[]>();
            readDataQueue = new ConcurrentQueue<byte[]>();
        }
        /// <summary>
        /// 获取指定IP的P2P队列
        /// </summary>
        /// <param name="desIP"></param>
        /// <param name="p2PSocket"></param>
        /// <returns></returns>
        public virtual bool TryGetP2PSocketQueue(int desIP, out P2PSocketQueue p2PSocket)
        {
            if (P2pNetwork.TryGetValue(desIP, out p2PSocket))
            {
                return true;
            }
            //如果没有p2p 是否要触发打洞，打洞方编号：desIP:scrIP
            return false;
        }
        /// <summary>
        /// 写入到本地网卡队列
        /// </summary>
        /// <param name="bytes"></param>
        public virtual void WriteWriteDataQueue(byte[] bytes)
        {
            WriteDataQueue.Enqueue(bytes);
        }
        /// <summary>
        /// 本地网卡读取到的队列数据
        /// </summary>
        /// <param name="bytes"></param>
        public virtual void WriteReadDataQueue(byte[] bytes)
        {
            ReadDataQueue.Enqueue(bytes);
        }
    }
}
