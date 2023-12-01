using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace P2PNetwork.HostedServices
{
    /// <summary>
    /// P2P打洞服务
    /// </summary>
    public class P2PSocketHostedService : BackgroundService
    {

        public static P2PSocketHostedService Instance { get; private set; }
        private readonly ConcurrentQueue<int> _p2pSocketQueue;
        private readonly ConcurrentDictionary<int, P2PSocket> _p2pSockets;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<P2PSocketHostedService> _logger;
        private CancellationToken cancellation;
        private readonly IServiceProvider _serviceProvider;

        public P2PSocketHostedService(IMemoryCache memoryCache, ILogger<P2PSocketHostedService> logger, IServiceProvider serviceProvider)
        {
            _memoryCache = memoryCache;
            Instance = this;
            _logger = logger;
            _p2pSockets = new ConcurrentDictionary<int, P2PSocket>();
            _p2pSocketQueue = new ConcurrentQueue<int>();
            _serviceProvider = serviceProvider;
            if (P2PTcpSocket.RouteLevel > 0)
            {
                logger.LogInformation($"距离外网距离{P2PTcpSocket.RouteLevel}");
            }
        }
        public virtual void AddP2P(int desIP,P2PSocket p2PSocket)
        {
            _logger.LogInformation($"{DateTime.Now} {string.Join(",",desIP.ToBytes())} 打洞成功{p2PSocket.GetType().Name}");
           _p2pSockets.AddOrUpdate(desIP, p2PSocket, (id, old) => {
                _logger.LogInformation($"更新{id}");
                return p2PSocket; });
           _= p2PSocket.StartAsync(cancellation);
        }
        public virtual void RemoveP2P(int desIP)
        {
           if( _p2pSockets.TryRemove(desIP, out _))
            {
                _memoryCache.Remove(desIP);//如果存在，则删除不打洞缓存
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.cancellation = stoppingToken;
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_p2pSocketQueue.TryDequeue(out var desIP))
                {
                    //需要打洞的目标IP
                    _logger.LogInformation($"需要打洞到{desIP}");
                   _= TestP2P(desIP, stoppingToken);
                }
                else
                {
                    await Task.Delay(10);
                }
            }
        }
        protected virtual async Task TestP2P(int desIP,CancellationToken cancellationToken)
        {
            var p2pId = NetworkDriveHostedService.Instance.LocalIPBytes.Concat(desIP.ToBytes()).ToArray();//打动id?
            await ExchangeSocketHostedService.Instance.SendBytesAsync(desIP, NetworkDriveHostedService.Instance.LocalIPBytes.Concat(new byte[] { 0 }).ToArray());
            //对方提示 我需要打洞
            var p2pUDPSocket = _serviceProvider.CreateScope().ServiceProvider.GetService<P2PUDPSocket>();
            if(!await p2pUDPSocket.TestP2P(desIP, p2pId, cancellationToken))
            {
                await ExchangeSocketHostedService.Instance.SendBytesAsync(desIP, NetworkDriveHostedService.Instance.LocalIPBytes.Concat(new byte[] { 1 }).ToArray());
                var p2pTcpPSocket = _serviceProvider.CreateScope().ServiceProvider.GetService<P2PTcpSocket>();
               if( !await p2pTcpPSocket.TestP2P(desIP, p2pId, false, cancellationToken))
                {
                    await ExchangeSocketHostedService.Instance.SendBytesAsync(desIP, NetworkDriveHostedService.Instance.LocalIPBytes.Concat(new byte[] { 2 }).ToArray());
                    p2pTcpPSocket = _serviceProvider.CreateScope().ServiceProvider.GetService<P2PTcpSocket>();
                    await p2pTcpPSocket.TestP2P(desIP, p2pId, true, cancellationToken);
                }
                //需要打动
                return;
            }
        }
        /// <summary>
        /// 判断是否存在P2P,如果不存在，则加入到打洞队列
        /// </summary>
        /// <param name="desIP"></param>
        /// <returns></returns>
        public virtual bool IgnoreP2PSocket(int desIP)
        {
           
            _memoryCache.GetOrCreate(desIP, (ic) =>
            {
                ic.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);//每5分钟只打一次洞
                return true;
            });
            return false;
        }
        public virtual async ValueTask SendBytesAsync(int desIP, byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (_p2pSockets.TryGetValue(desIP, out var p2PSocket))
            {
                await p2PSocket.SendAsync(bytes, cancellationToken);
                return;
            }
            await ExchangeSocketHostedService.Instance.SendBytesAsync(desIP, bytes, cancellationToken);
            _memoryCache.GetOrCreate(desIP, (ic) =>
            {
                _p2pSocketQueue.Enqueue(desIP);
                ic.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);//每5分钟只打一次洞
                return true;
            });
        }
    }
}
