using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace P2PNetwork
{
    public abstract class P2PSocket : BackgroundService
    {
        public const string P2PHost = "p2p.vpn.qcxt.com";
        public const int P2PPort = 62001;
        private static readonly ConcurrentDictionary<ulong, P2PSocket> p2pSockets = new ConcurrentDictionary<ulong, P2PSocket>();
        public abstract ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
        public event Func<Memory<byte>, ValueTask> ReceiveDataAsync;
        private readonly System.Threading.Timer timer;
        private readonly System.Threading.Timer offlineTimer;
        private readonly ulong ip;
        private readonly ulong ipb;
        protected P2PSocket(ulong ip)
        {
            this.ip = ip;
            ipb = (ulong)(ip & 0xffffffff)<<32 | (ip >> 32);
            Console.WriteLine($"{ip.ToIP()} {this.P2PTypeName} 进来了？ {ip}->{ipb}");
            p2pSockets.AddOrUpdate(ip, this, (i, o) =>
            {
                try
                {
                    Console.WriteLine($"{ip.ToIP()} 居然又来一个{this.P2PTypeName}");
                    _ = o.StopAsync(default);
                }
                catch (Exception) { }
                return this;
            });
            timer = new System.Threading.Timer(TimerCallback, this, 3000, 3000);
            offlineTimer = new System.Threading.Timer(OfflineTimerCallback, this, 15000, 15000);
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            p2pSockets.TryRemove(ip, out _);
            timer.Dispose();
            offlineTimer.Dispose();
            return base.StopAsync(cancellationToken);
        }
        protected virtual void TimerCallback(object state)
        {
            _ = SendAsync(new byte[] { 2 });
        }
        protected virtual void OfflineTimerCallback(object state)
        {
            Console.WriteLine($"{DateTime.Now} {ip} OfflineTimer到期");
            _ = this.StopAsync(default);
        }
        protected virtual Task OnReceiveDataAsync(Memory<byte> buffer)
        {

            timer.Change(3000, 3000);
            offlineTimer.Change(15000, 15000);//3分钟没有有效数据则关闭通道
            return Task.Factory.StartNew(() =>
              {
                  if (buffer.Length > 1)
                  {
                      if (ReceiveDataAsync != null)
                      {
                          _ = ReceiveDataAsync(buffer);
                      }
                  }
              }, TaskCreationOptions.LongRunning);
        }
        public abstract string P2PTypeName { get; }
        public static P2PSocket GetP2PPSocket(int ip, int localIP)
        {
            var ipB = ip.ToBytes();
            var localIPB = localIP.ToBytes();

            var id = ipB.Concat(localIPB).ToArray().ToUInt64();
            if (p2pSockets.TryGetValue(id, out var socket))
            {
                return socket;
            }
            id = localIPB.Concat(ipB).ToArray().ToUInt64();
            if (p2pSockets.TryGetValue(id, out var socket2))
            {
                return socket2;
            }
            Console.WriteLine($"{ip.ToIP()} 没找到p2p?");
            return null;
        }
        public static async Task<P2PSocket> TestP2P(ulong ip, ulong id)
        {
            Console.WriteLine($"{ip.ToIP()} 尝试打洞{id}");
            var p2pSocket = await P2PUDPSocket.TestP2P(ip, id);
            if (p2pSocket == null)
            {
                p2pSocket = await P2PTcpSocket.TestP2P(ip, id);
            }
            if (p2pSocket == null)
            {
                p2pSocket = await P2PUDPSocket.TestP2P(ip, id);
                if (p2pSocket == null)
                {
                    p2pSocket = await P2PTcpSocket.TestP2P(ip, id);
                }
            }
            return p2pSocket;
        }
    }
}
