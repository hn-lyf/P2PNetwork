using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using P2PNetwork.HostedServices;

namespace P2PNetwork
{
    public class P2PUDPSocket : P2PSocket
    {
        private readonly UdpClient client = new UdpClient(new System.Net.IPEndPoint(IPAddress.Any, 0));
        private IPEndPoint remoteEndPoint;
        public P2PUDPSocket(ILogger<P2PUDPSocket> logger) : base(logger)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                client.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await client.SendAsync(buffer,remoteEndPoint, cancellationToken);
        }
        public virtual async Task<bool> TestP2P(int desIP, byte[] idBytes, CancellationToken cancellationToken = default)
        {
            DesIP=desIP;
            try
            {
                Logger.LogInformation($"准备向（{string.Join(".", desIP.ToBytes())}）打洞");
                await client.SendAsync(idBytes, P2PHost, P2PPort, cancellationToken);//发送此次打洞的id
                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(5000))
                {
                    var receiveResult = await client.ReceiveAsync(cancellationTokenSource.Token);
                    var text = Encoding.UTF8.GetString(receiveResult.Buffer);
                    remoteEndPoint = IPEndPoint.Parse(text);//远程端口
                    Logger.LogInformation($"收到对方（{string.Join(".", desIP.ToBytes())}）地址端口：{remoteEndPoint}");
                    await client.SendAsync(new byte[] { 1 }, 1, remoteEndPoint);
                    using (CancellationTokenSource cancellationTokenSource1 = new CancellationTokenSource(3000))
                    {
                        receiveResult = await client.ReceiveAsync(cancellationTokenSource1.Token);
                        //打洞成功
                        remoteEndPoint = receiveResult.RemoteEndPoint;
                        Logger.LogInformation($"最后收到对方（{string.Join(".", desIP.ToBytes())}）地址端口：{remoteEndPoint}");
                        await client.SendAsync(new byte[] { 2 }, 1, receiveResult.RemoteEndPoint);
                        P2PSocketHostedService.Instance.AddP2P(desIP, this);
                        return true;
                    }
                }
            }catch (Exception ex)
            {
                Logger.LogInformation($"（{string.Join(".", desIP.ToBytes())}）打洞 失败");
            }
            this.Dispose();
            return false;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("P2P UDP 准备接收数据");
            while (!stoppingToken.IsCancellationRequested)
            {
               var receiveResult=await client.ReceiveAsync(stoppingToken);
               _= OnReceiveDataAsync(receiveResult.Buffer);
            }
            Logger.LogInformation("P2P UDP 接收完成");
        }
        public override void Dispose()
        {
            client.Dispose();
            base.Dispose();
        }
    }
}
