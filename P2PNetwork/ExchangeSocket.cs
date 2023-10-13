using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace P2PNetwork
{
    public class ExchangeSocket : P2PSocket
    {
        private readonly UdpClient udpClient;
        private byte[] localIPBytes;
        private int localIPInt;

        public ExchangeSocket() : base(0)
        {
            udpClient = new UdpClient(0);
        }
        private string localIP;
        public virtual string LocalIP
        {
            get
            {
                return localIP;
            }
            set
            {
                localIP = value;
                localIPInt = value.IPToInt();
                localIPBytes = localIP.Split('.').Select(byte.Parse).ToArray();
                _ = SendAsync(LocalIPBytes);
            }
        }
        protected override void TimerCallback(object state)
        {

        }
        protected override void OfflineTimerCallback(object state)
        {
            if (LocalIPBytes != null)
            {
                _ = SendAsync(LocalIPBytes);
            }
        }
        public override string P2PTypeName => "No P2P";

        public byte[] LocalIPBytes { get => localIPBytes; }
        public int LocalIPInt { get => localIPInt; }


        public virtual async ValueTask SendAsync(int ip, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using (var memoryStream = new System.IO.MemoryStream())
            {
                memoryStream.WriteByte((byte)(ip >> 24));
                memoryStream.WriteByte((byte)(ip >> 16 & 255));
                memoryStream.WriteByte((byte)(ip >> 8 & 255));
                memoryStream.WriteByte((byte)(ip & 255));
                memoryStream.Write(buffer.ToArray());
                await udpClient.SendAsync(memoryStream.ToArray(), P2PSocket.P2PHost, P2PSocket.P2PPort);
            }
        }

        public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using (var memoryStream = new System.IO.MemoryStream())
            {
                await udpClient.SendAsync(buffer, P2PSocket.P2PHost, P2PSocket.P2PPort);
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var receiveResult = await udpClient.ReceiveAsync(stoppingToken);
                _ = OnReceiveDataAsync(receiveResult.Buffer);
            }
        }

    }
}
