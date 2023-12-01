using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using P2PNetwork.HostedServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2PNetwork
{
    /// <summary>
    /// p2p socket
    /// </summary>
    public abstract class P2PSocket:BackgroundService
    {
        private readonly string _P2PHost = "p2p.vpn.qcxt.com";
        private readonly int _P2PPort = 62001;
        private readonly ILogger _logger;
        private  System.Threading.Timer timer;
        private  System.Threading.Timer offlineTimer;
        public virtual int DesIP {  get; set; }
        protected P2PSocket(ILogger logger)
        {
            _logger = logger;
            
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            timer = new Timer(TimerCallback, this, 3000, 3000);
            offlineTimer = new System.Threading.Timer(OfflineTimerCallback, this, 300000, 300000);//5分钟没有数据来往就关闭通道
            return base.StartAsync(cancellationToken);
        }

        public string P2PHost => _P2PHost;

        public int P2PPort => _P2PPort;

        public ILogger Logger => _logger;
        private int Index = 1;
        protected virtual void TimerCallback(object state)
        {
            if (Index > 3)
            {
                Logger.LogWarning($"{DateTime.Now} 触发连续2次周期没收到心跳或任何数据，关闭通道");
                this.StopAsync(default).Wait();
                this.Dispose();
            }
            if (this.ExecuteTask != null)
            {
                Index++;
                _ = SendAsync(new byte[] { 2 });
            }
        }
        protected virtual void OfflineTimerCallback(object state)
        {
            Logger.LogWarning($"{DateTime.Now}  OfflineTimer到期");
             this.StopAsync(default).Wait();
            this.Dispose();
        }
        public abstract ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
        public virtual async ValueTask OnReceiveDataAsync(byte[] bytes)
        {
            Index = 0;
            timer.Change(3000, 3000);//如果收到数据 则认为链接健康
            if (bytes.Length > 1)
            {
                offlineTimer.Change(300000, 300000);//5分钟结束延后
                await  NetworkDriveHostedService.Instance.SendBytesAsync(bytes, default);
            }
            else if (bytes[0]==2)
            {
               await SendAsync(new byte[] { 1 });
            }
        }
        public override void Dispose()
        {
            P2PSocketHostedService.Instance.RemoveP2P(DesIP);
            timer?.Dispose();
            offlineTimer?.Dispose();
            base.Dispose();

        }
    }
}
