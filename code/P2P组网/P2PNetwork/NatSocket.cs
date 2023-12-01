using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using P2PNetwork.HostedServices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace P2PNetwork
{
    public abstract class NatSocket : BackgroundService
    {
        //NatSocketHostedService
        private readonly ILogger _logger;
        private readonly System.Threading.Timer offlineTimer;
        private readonly int dueTime = 60000;
        protected NatSocket(ILogger logger, int dueTime = 60000)
        {
            _logger = logger;
            Logger.LogInformation($"新 udp NAT 启动");

            this.dueTime = dueTime;
            offlineTimer = new System.Threading.Timer(OfflineTimerCallback, this, dueTime, dueTime);
        }

        public ILogger Logger => _logger;
        public virtual int SourceIP { get; protected set; }
        public virtual int SourcePort { get; protected set; }
        public virtual int DesIP { get; protected set; }
        public virtual int DesPort { get; protected set; }
        public virtual string Key { get; private set; }
        public virtual string DestIPText { get; private set; }
        public virtual void SetDesIP<P>(string id, P packet)
            where P : IPv4Packet
        {
            Key = id;
            DestIPText = packet.DestIPText;
            this.SourceIP = packet.SourceIP;
            DesIP = packet.DestIP;
            Logger.LogInformation($" {Key} NAT 设置完成{DestIPText}");
        }
        protected virtual void ChangeOffline()
        {
            offlineTimer.Change(dueTime, dueTime);
        }
        private void OfflineTimerCallback(object state)
        {
            Logger.LogWarning($"{DateTime.Now} NAT {Key} OfflineTimer到期");
            this.StopAsync(default).Wait();
            this.Dispose();
        }
        public abstract Task WriteBytesAsync(IPv4Packet packet, CancellationToken cancellationToken = default);
        public override void Dispose()
        {
            NatSocketHostedService.Instance.RemoveNAT(Key);
            offlineTimer.Dispose();
            base.Dispose();
            Logger.LogInformation($"{DateTime.Now} {Key} 退出");
        }
    }
    /// <summary>
    /// ICMP NAT
    /// </summary>
    public class ICMPNatSocket : NatSocket
    {
        private readonly Socket icmpSocket;
        private readonly ConcurrentDictionary<int, int[]> pingIds = new ConcurrentDictionary<int, int[]>();
        public ICMPNatSocket(ILogger<ICMPNatSocket> logger) : base(logger)
        {
            icmpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
            icmpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, 1);
        }
        private ushort PingId;
        public virtual int LocalInt { get; private set; }
        public override void SetDesIP<P>(string id, P packet)
        {
            base.SetDesIP(id, packet);
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp))
            {
                socket.Connect(IPAddress.Parse(DestIPText), 0);
                LocalInt = (socket.LocalEndPoint as IPEndPoint).Address.GetAddressBytes().ToInt32();
            }
            icmpSocket.Connect(IPAddress.Parse(DestIPText), 0);
        }
        public override async Task WriteBytesAsync(IPv4Packet packet, CancellationToken cancellationToken = default)
        {
            if (packet is ICMPPacket icmpPacket)
            {
                ChangeOffline();
                var pingId = 0;
                lock (this)
                {
                    pingId = ++PingId;
                }
                pingIds.GetOrAdd(pingId, new[] { icmpPacket.SourceIP, icmpPacket.PingSeq });//记录新的pingid对应的原始目标ip
                icmpPacket.SourceIP = LocalInt;
                icmpPacket.PingSeq = pingId;
                await icmpSocket.SendAsync(icmpPacket.ToBytes(), SocketFlags.None, cancellationToken);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var bytes = new byte[1500];
                var length = await icmpSocket.ReceiveAsync(bytes, SocketFlags.None, stoppingToken);
                if (length > 0)
                {
                    ChangeOffline();
                    IPv4Packet pv4Packet = new IPv4Packet(bytes.Take(length).ToArray());
                    var icmpPacket = new ICMPPacket(pv4Packet);
                    if (pingIds.TryGetValue(icmpPacket.PingSeq, out var sid))
                    {
                        icmpPacket.DestIP = sid[0];
                        icmpPacket.PingSeq = sid[1];
                        _ = P2PSocketHostedService.Instance.SendBytesAsync(icmpPacket.DestIP, icmpPacket.ToBytes(), default);
                    }
                    else
                    {
                        Logger.LogInformation($"{DateTime.Now} 收到ping 回复 {icmpPacket.SourceIPText}->{icmpPacket.DestIPText}  PingId:{icmpPacket.PingId}->PingSeq:{icmpPacket.PingSeq}");
                    }
                }
            }
        }
        public override void Dispose()
        {
            pingIds.Clear();
            icmpSocket.Dispose();
            base.Dispose();
        }
    }
    public class UDPNatSocket : NatSocket
    {
        private readonly Socket client;
        public IPEndPoint RemoteEndPoint { get; private set; }
        public UDPNatSocket(ILogger<UDPNatSocket> logger) : base(logger)
        {

            client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }
        public override void SetDesIP<P>(string id, P packet)
        {
            base.SetDesIP(id, packet);
            if (packet is UDPPacket udpPacket)
            {
                this.SourcePort = udpPacket.SourcePort;
                DesPort = udpPacket.DestPort;
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(DestIPText), DesPort);
                client.SendTo(new byte[] { }, RemoteEndPoint);
            }

        }
        public override async Task WriteBytesAsync(IPv4Packet packet, CancellationToken cancellationToken = default)
        {
            if (packet is UDPPacket icmpPacket)
            {
                ChangeOffline();
                Logger.LogInformation($"发送udp 数据到{DestIPText}：{DesPort}");
                await client.SendToAsync(icmpPacket.Data, SocketFlags.None, RemoteEndPoint, cancellationToken);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation($"准备 接收 udp 数据到{DesIP}");
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            while (!stoppingToken.IsCancellationRequested)
            {
                var bytes = new byte[65530];
                var receiveResult = await client.ReceiveMessageFromAsync(bytes, SocketFlags.None, remoteEndPoint, stoppingToken);
                if (receiveResult.ReceivedBytes > 0)
                {
                    ChangeOffline();
                    Logger.LogInformation($" 接收{DesIP} udp 数据到 {receiveResult.ReceivedBytes}");
                    UDPPacket packet = new UDPPacket(bytes.Take(receiveResult.ReceivedBytes).ToArray());
                    packet.SourceIP = DesIP;
                    packet.SourcePort = DesPort;
                    packet.DestIP = this.SourceIP;
                    packet.DestPort = this.SourcePort;
                    Logger.LogInformation($"接收 udp 数据到{packet.SourceIPText}:{packet.SourcePort}->{packet.DestIPText}:{packet.DestPort}");
                    _ = P2PSocketHostedService.Instance.SendBytesAsync(packet.DestIP, packet.ToBytes(), default);
                }
            }
        }
        public override void Dispose()
        {
            client.Dispose();
            base.Dispose();
        }
    }
    public class TCPNatSocket : NatSocket
    {
        private readonly TcpListener tcpListener;
        private readonly IPEndPoint localEndPoint;
        private readonly TunDriveSDK _tunDriveSDK;
        public static readonly ConcurrentDictionary<string, IPEndPoint> TcpNats = new ConcurrentDictionary<string, IPEndPoint>();
        public TCPNatSocket(ILogger<TCPNatSocket> logger, TunDriveSDK tunDriveSDK) : base(logger)
        {
            tcpListener = new TcpListener(IPAddress.Any, 0);
            tcpListener.Start();
            localEndPoint = tcpListener.LocalEndpoint as IPEndPoint;
            _tunDriveSDK = tunDriveSDK;
            logger.LogInformation($"TCP NAT 启动  监听本地端口{localEndPoint}");
        }

        public override async Task WriteBytesAsync(IPv4Packet packet, CancellationToken cancellationToken = default)
        {
            if (packet is TCPPacket tcpPacket)
            {
                tcpPacket.DestIP = _tunDriveSDK.LocalIPInt;
                tcpPacket.DestPort = localEndPoint.Port;
                Logger.LogInformation($"TCP 目标和端口改成本机 {tcpPacket.DestIPText}:{tcpPacket.DestPort}");
                await _tunDriveSDK.WriteAsync(tcpPacket.ToBytes(), cancellationToken);
            }
        }
        public override void SetDesIP<P>(string id, P packet)
        {
            base.SetDesIP(id, packet);
            if (packet is TCPPacket tcpPacket)
            {
                this.SourcePort = tcpPacket.SourcePort;
                DesPort = tcpPacket.DestPort;
                var remoteEndPoint = new IPEndPoint(IPAddress.Parse(tcpPacket.DestIPText), tcpPacket.DestPort);
                TcpNats.AddOrUpdate($"{_tunDriveSDK.LocalIPInt}:{localEndPoint.Port}", remoteEndPoint, (key, o) => remoteEndPoint);
            }
        }
        public override void Dispose()
        {
            base.Dispose();
            Task.Run(async () =>
            {
                await Task.Delay(600);
                TcpNats.TryRemove($"{_tunDriveSDK.LocalIPInt}:{localEndPoint.Port}", out _);
            });

            try
            {
                tcpListener.Stop();
            }
            catch (Exception ex) { }

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation($"TCP NAT {Key} 启动监听");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var socket = await tcpListener.AcceptSocketAsync(stoppingToken);
                    _ = ExchangeSocket(socket, stoppingToken);
                }
                catch (Exception ex) { }
            }
            this.Dispose();
        }
        public virtual async Task ExchangeSocket(Socket socket, CancellationToken stoppingToken)
        {
            using (socket)
            {
                Socket socketa = null;
                var r = socket.RemoteEndPoint as IPEndPoint;
                var l = socket.LocalEndPoint as IPEndPoint;
                if (r.Address != l.Address)
                {
                    Logger.LogInformation($"TCP NAT 链接来了{r}");
                    socketa = socket;
                    using (Socket socket1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        try
                        {
                            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(2000))
                            {
                                await socket1.ConnectAsync(DestIPText, DesPort, cancellationTokenSource.Token);
                            }
                            await ExchangeSocket(socketa, socket1);
                            await Task.Delay(1000);
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
            }
            _ = this.StopAsync(default);

        }
        public async Task ExchangeSocket(Socket socket1, Socket socket2)
        {
            try
            {

                var networkStream1 = new NetworkStream(socket1);
                var networkStream2 = new NetworkStream(socket2);
                await Task.WhenAny(networkStream1.CopyToAsync(networkStream2), networkStream2.CopyToAsync(networkStream1));
                networkStream1.Close(3000);
                networkStream2.Close(3000);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
