using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace P2PNetwork
{
    public class P2PUDPSocket : P2PSocket
    {
        private readonly UdpClient client;
        private readonly IPEndPoint _remoteEndPoint;
        public virtual IPEndPoint RemoteEndPoint => _remoteEndPoint;
        public P2PUDPSocket(ulong ip, UdpClient client, IPEndPoint remoteEndPoint) : base(ip)
        {
            this.client = client;
            _remoteEndPoint = remoteEndPoint;
        }
        public override string P2PTypeName => "P2P UDP";
        public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await client.SendAsync(buffer, RemoteEndPoint, cancellationToken);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested && client.Client != null)
            {
                var receiveResult = await client.ReceiveAsync(stoppingToken);
                _ = OnReceiveDataAsync(receiveResult.Buffer);
            }
        }
        public static async Task<P2PSocket> TestP2P(ulong ip, ulong id)
        {
            UdpClient client = new UdpClient(new System.Net.IPEndPoint(IPAddress.Any, 0));
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                client.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { 0 }, null);
            }
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            await Task.Delay(100);
            await client.SendAsync(BitConverter.GetBytes(id), 8, new System.Net.IPEndPoint(Dns.GetHostAddresses(P2PSocket.P2PHost)[0], P2PSocket.P2PPort));//发送此次打洞的id
            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(5000))
            {
                try
                {
                    var receiveResult = await client.ReceiveAsync(cancellationTokenSource.Token);
                    var text = Encoding.UTF8.GetString(receiveResult.Buffer);
                    var remoteEndPoint = IPEndPoint.Parse(text);//远程端口
                    Console.WriteLine(text);
                    await client.SendAsync(new byte[] { 1 }, 1, remoteEndPoint);
                    using (CancellationTokenSource cancellationTokenSource1 = new CancellationTokenSource(3000))
                    {
                        receiveResult = await client.ReceiveAsync(cancellationTokenSource1.Token);
                        //打洞成功
                        await client.SendAsync(new byte[] { 2 }, 1, receiveResult.RemoteEndPoint);
                        P2PUDPSocket p2PUDPSocket = new P2PUDPSocket(ip, client, receiveResult.RemoteEndPoint);
                        _ = System.Threading.Tasks.Task.Factory.StartNew(() => p2PUDPSocket.StartAsync(default), TaskCreationOptions.LongRunning);
                        return p2PUDPSocket;
                    }
                }
                catch (Exception ex)
                {

                }
            }
            client.Dispose();
            return null;
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            client.Dispose();
            return base.StopAsync(cancellationToken);
        }
    }
}
