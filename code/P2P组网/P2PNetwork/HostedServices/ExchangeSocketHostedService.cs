using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace P2PNetwork.HostedServices
{
    public class ExchangeSocketHostedService : BackgroundService
    {
        public static ExchangeSocketHostedService Instance { get; private set; }
        private readonly UdpClient udpClient;
        private readonly string P2PHost = "ex.vpn.qcxt.com";
        private readonly int P2PPort = 62002;
        private readonly ILogger<ExchangeSocketHostedService> _logger;
        private CancellationTokenSource _stoppingCts;
        private readonly IServiceProvider _serviceProvider;
        public ExchangeSocketHostedService(ILogger<ExchangeSocketHostedService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            udpClient = new UdpClient();
            Instance = this;
            _serviceProvider = serviceProvider;
        }
        protected virtual async Task TimerSendAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (NetworkDriveHostedService.Instance.LocalIPBytes != null)
                {

                    await SendLocalIPAsync();
                    await Task.Delay(10000);
                }
                else
                {
                    await Task.Delay(500);
                }
            }
        }
        public virtual async Task SendLocalIPAsync()
        {
            await udpClient.SendAsync(NetworkDriveHostedService.Instance.LocalIPBytes, P2PHost, P2PPort);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = TimerSendAsync(stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var receiveResult = await udpClient.ReceiveAsync(stoppingToken);
                    _ = OnReceiveDataAsync(receiveResult.Buffer, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"服务器转发接收异常{ex}");
                }
            }
        }
        protected virtual async Task OnReceiveDataAsync(byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (bytes.Length > 5)
            {
                await NetworkDriveHostedService.Instance.SendBytesAsync(bytes, cancellationToken);
            }
            else if (bytes.Length == 5)
            {
                //对方提示 我需要打洞
                var desIP = bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3];
                //需要打动
                var p2pId = bytes.SkipLast(1).Concat(NetworkDriveHostedService.Instance.LocalIPBytes).ToArray();//打动id?
                switch (bytes[4])
                {
                    case 0://udp
                        var p2pUDPSocket = _serviceProvider.CreateScope().ServiceProvider.GetService<P2PUDPSocket>();
                        await p2pUDPSocket.TestP2P(desIP, p2pId, cancellationToken);
                        break;
                    case 1: //tcp listener
                        var p2pTcpSocket = _serviceProvider.CreateScope().ServiceProvider.GetService<P2PTcpSocket>();
                        await p2pTcpSocket.TestP2P(desIP, p2pId, true, cancellationToken);
                        break;
                    case 2: //tcp cli
                        var p2pTcpSocket2 = _serviceProvider.CreateScope().ServiceProvider.GetService<P2PTcpSocket>();
                        await p2pTcpSocket2.TestP2P(desIP, p2pId, false, cancellationToken);
                        break;
                }

            }
            //如果等于4，需要打洞？然后是对方打洞的IP?
        }
        public virtual async ValueTask SendBytesAsync(int desIP, byte[] bytes, CancellationToken cancellationToken = default)
        {
            using (var memoryStream = new System.IO.MemoryStream())
            {
                memoryStream.WriteByte((byte)(desIP >> 24));
                memoryStream.WriteByte((byte)(desIP >> 16 & 255));
                memoryStream.WriteByte((byte)(desIP >> 8 & 255));
                memoryStream.WriteByte((byte)(desIP & 255));
                memoryStream.Write(bytes);
                await udpClient.SendAsync(memoryStream.ToArray(), P2PHost, P2PPort);
            }
        }
    }
}
