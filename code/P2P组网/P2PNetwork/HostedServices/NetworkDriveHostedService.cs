using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace P2PNetwork.HostedServices
{
    /// <summary>
    /// 网络驱动服务
    /// </summary>
    public class NetworkDriveHostedService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly TunDriveSDK _tunDriveSDK;
        public static NetworkDriveHostedService Instance { get; private set; }
        public NetworkDriveHostedService(IConfiguration configuration, ILogger<NetworkDriveHostedService> logger, TunDriveSDK tunDriveSDK)
        {
            _configuration = configuration;
            _logger = logger;
            _tunDriveSDK = tunDriveSDK;
            Instance = this;
        }
        public IConfiguration Configuration => _configuration;

        public TunDriveSDK TunDriveSDK => _tunDriveSDK;
        public virtual string LocalIP { get { return TunDriveSDK.LocalIP; } set { TunDriveSDK.LocalIP = value; } }
        public virtual byte[] LocalIPBytes { get { return TunDriveSDK.LocalIPBytes; } }
        public virtual int LocalIPInt { get { return TunDriveSDK.LocalIPInt; } }
        public virtual ConcurrentDictionary<string, IPEndPoint> NatIPEndPoints { get; } = new ConcurrentDictionary<string, IPEndPoint>();
        public virtual ConcurrentDictionary<string, int> NatIPEndPointPorts { get; } = new ConcurrentDictionary<string, int>();
        public int ExchangeIPInt
        {
            get;
            set;
        } = 732432390;
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TunDriveSDK.OpenDrive();
            TunDriveSDK.SetIP(System.Net.IPAddress.Parse(LocalIP), System.Net.IPAddress.Parse("255.255.0.0"));
            TunDriveSDK.ConnectionState(true);
            await ExchangeSocketHostedService.Instance.SendLocalIPAsync();
            _logger.LogInformation($"网卡启动完成 本地{LocalIP}（{LocalIPInt}）");
            while (!stoppingToken.IsCancellationRequested)
            {

                try
                {
                    var buffer = new Memory<byte>(new byte[1500]);
                    int length = await TunDriveSDK.ReadAsync(buffer, stoppingToken);
                    _ = OnunReceiveAsync(buffer.Slice(0, length).ToArray());
                }
                catch (Exception)
                {

                }
            }
            _logger.LogInformation($"我结束了");
        }
        protected virtual async ValueTask OnunReceiveAsync(byte[] bytes, CancellationToken cancellationToken = default)
        {
            //收到数据后 是不是应该抛给队列另外一个专门的线程处理？这样就不会
            if (bytes[0] == 0x45)//暂时只处理IPV4
            {
                var destIPMask = bytes[16] << 8 | bytes[17];
                var destIP = bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19];
                var localMask = ((uint)LocalIPInt >> 16);
                var sourceIP = bytes[12] << 24 | bytes[13] << 16 | bytes[14] << 8 | bytes[15];
                if (bytes[9] == 6 && sourceIP == LocalIPInt && TCPNatSocket.TcpNats.Count > 0)
                {
                    IPv4Packet pv4Packet = new IPv4Packet(bytes);
                    var tcpPacket = new TCPPacket(pv4Packet);
                    //{_tunDriveSDK.LocalIPInt}:{localEndPoint.Port}
                    _logger.LogInformation($"{tcpPacket.SourceIP}:{tcpPacket.SourcePort}");
                    if (TCPNatSocket.TcpNats.TryGetValue($"{tcpPacket.SourceIP}:{tcpPacket.SourcePort}", out var endPoint))
                    {
                        tcpPacket.SourceIP = endPoint.Address.GetAddressBytes().ToInt32();
                        tcpPacket.SourcePort = endPoint.Port;
                        await P2PSocketHostedService.Instance.SendBytesAsync(destIP, tcpPacket.ToBytes(), cancellationToken);
                        return;
                    }
                }
                if (sourceIP != LocalIPInt)
                {
                    bytes[12] = 43;
                    bytes[13] = 168;
                    IPv4Packet pv4Packet = new IPv4Packet(bytes);
                    switch (pv4Packet.Protocol)
                    {
                        case EProtocolType.ICMP:
                            var icmpPacket = new ICMPPacket(pv4Packet);
                            await P2PSocketHostedService.Instance.SendBytesAsync(destIP, icmpPacket.ToBytes(), cancellationToken);
                            break;
                        case EProtocolType.UDP:
                            var udpPacket = new UDPPacket(pv4Packet);
                            await P2PSocketHostedService.Instance.SendBytesAsync(destIP, udpPacket.ToBytes(), cancellationToken);
                            break;
                        case EProtocolType.TCP:
                            var tcpPacket = new TCPPacket(pv4Packet);

                            await P2PSocketHostedService.Instance.SendBytesAsync(destIP, tcpPacket.ToBytes(), cancellationToken);
                            break;
                    }
                }
                if (destIPMask == localMask)//是同一个网段的，不需要nat的
                {
                    await P2PSocketHostedService.Instance.SendBytesAsync(destIP, bytes, cancellationToken);
                }
                else if (ExchangeIPInt != 0) //非同一个网段的,但是有转发到目标的ip
                {
                    await P2PSocketHostedService.Instance.SendBytesAsync(ExchangeIPInt, bytes, cancellationToken);
                }
                //目标不是同一个网段且没有中转IP，则丢弃

            }
        }
        public virtual async ValueTask SendBytesAsync(byte[] bytes, CancellationToken cancellationToken = default)
        {
            var sourceIP = bytes[12] << 24 | bytes[13] << 16 | bytes[14] << 8 | bytes[15];
            P2PSocketHostedService.Instance.IgnoreP2PSocket(sourceIP);//忽列这个ip的打洞，不然会导致双方都在打洞，造成浪费 个人觉得
            var destIP = bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19];//目标IP
            var destIPMask = bytes[16] << 8 | bytes[17];
            var sourceIPMask = bytes[12] << 8 | bytes[13];
            var localMask = ((uint)LocalIPInt >> 16);
            if (destIP != LocalIPInt)
            {
                bytes[16] = 172;
                bytes[17] = 16;
                IPv4Packet pv4Packet = new IPv4Packet(bytes);
                switch (pv4Packet.Protocol)
                {
                    case EProtocolType.ICMP:
                        var icmpPacket = new ICMPPacket(pv4Packet);
                        await TunDriveSDK.WriteAsync(icmpPacket.ToBytes(), cancellationToken);
                        break;
                    case EProtocolType.UDP:
                        var udpPacket = new UDPPacket(pv4Packet);
                        await TunDriveSDK.WriteAsync(udpPacket.ToBytes(), cancellationToken);
                        break;
                    case EProtocolType.TCP:
                        var tcpPacket = new TCPPacket(pv4Packet);
                        await TunDriveSDK.WriteAsync(tcpPacket.ToBytes(), cancellationToken);
                        break;
                }
                return;
            }
            await TunDriveSDK.WriteAsync(bytes, cancellationToken);
            //if (destIPMask == localMask)//是同一个网段的，不需要nat的
            //{
            //    await TunDriveSDK.WriteAsync(bytes, cancellationToken);
            //}
            //else
            //{
            //    //需要nat的包
            //    await NatSocketHostedService.Instance.WriteBytesAsync(bytes, cancellationToken);
            //}
        }
    }
}
