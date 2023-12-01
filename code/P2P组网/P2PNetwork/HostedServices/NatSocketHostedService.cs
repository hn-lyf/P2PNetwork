using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2PNetwork.HostedServices
{
    public class NatSocketHostedService : BackgroundService
    {
        public static NatSocketHostedService Instance { get; private set; }
        private readonly IServiceProvider _serviceProvider;

        public NatSocketHostedService(IServiceProvider serviceProvider, ILogger<NatSocketHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            Instance = this;
        }

        private readonly ILogger<NatSocketHostedService> _logger;
        private readonly ConcurrentDictionary<string, NatSocket> natSokcets = new ConcurrentDictionary<string, NatSocket>();
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(1000);
        }
        public virtual void RemoveNAT(string key)
        {
            natSokcets.TryRemove(key, out _);
        }
        public virtual async Task WriteBytesAsync(ICMPPacket packet, CancellationToken cancellationToken = default)
        {
            var natSocket = natSokcets.GetOrAdd($"icmp-{packet.DestIPText}", (key) =>
            {
                var natSocket = _serviceProvider.CreateScope().ServiceProvider.GetService<ICMPNatSocket>();
                natSocket.SetDesIP(key, packet);
                _ = natSocket.StartAsync(cancellationToken);
                return natSocket;
            });
            await natSocket.WriteBytesAsync(packet, cancellationToken);
        }
        public virtual async Task WriteBytesAsync(UDPPacket packet, CancellationToken cancellationToken = default)
        {
            var natSocket = natSokcets.GetOrAdd($"udp-{packet.SourceIPText}:{packet.SourcePort}->{packet.DestIPText}:{packet.DestPort}", (key) =>
            {
                var natSocket = _serviceProvider.CreateScope().ServiceProvider.GetService<UDPNatSocket>();
                natSocket.SetDesIP(key, packet);
                _ = natSocket.StartAsync(cancellationToken);
                return natSocket;
            });
            await natSocket.WriteBytesAsync(packet, cancellationToken);
        }
        public virtual async Task WriteBytesAsync(TCPPacket packet, CancellationToken cancellationToken = default)
        {
            var key = $"tcp-{packet.SourceIPText}:{packet.SourcePort}->{packet.DestIPText}:{packet.DestPort}";
            NatSocket natSocket = null;
            if (packet.SYN)
            {
                natSocket = natSokcets.GetOrAdd(key, (key) =>
                {
                    var natSocket = _serviceProvider.CreateScope().ServiceProvider.GetService<TCPNatSocket>();
                    natSocket.SetDesIP(key, packet);
                    _ = natSocket.StartAsync(cancellationToken);
                    return natSocket;
                });
                await natSocket.WriteBytesAsync(packet, cancellationToken);
            }
            else
            {
                if (natSokcets.TryGetValue(key, out natSocket))
                {
                    await natSocket.WriteBytesAsync(packet, cancellationToken);
                    if (packet.FIN || packet.RST)
                    {
                        _logger.LogInformation($"{DateTime.Now} {key} 主动结束？");
                        await natSocket.StopAsync(default);
                    }
                }
            }
        }
        public virtual async Task WriteBytesAsync(byte[] bytes, CancellationToken cancellationToken = default)
        {
            IPv4Packet pv4Packet = new IPv4Packet(bytes);
            switch (pv4Packet.Protocol)
            {
                case EProtocolType.ICMP://ping
                    var icmpPacket = new ICMPPacket(pv4Packet);
                    await WriteBytesAsync(icmpPacket, cancellationToken);
                    break;
                case EProtocolType.TCP://tcp
                    var tcpPacket = new TCPPacket(pv4Packet);
                    await WriteBytesAsync(tcpPacket, cancellationToken);
                    break;
                case EProtocolType.UDP://udp
                    var udpPacket = new UDPPacket(pv4Packet);
                    await WriteBytesAsync(udpPacket, cancellationToken);
                    break;
            }
        }
    }
}
