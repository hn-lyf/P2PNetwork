using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace P2PNetwork.HostedServices
{
    /// <summary>
    /// 数据交互服务
    /// </summary>
    public class DataExchangeHostedService : BackgroundService
    {
        public readonly string HostName = "ex.vpn.qcxt.com";
        public readonly int Port = 62001;
        private readonly UdpClient udpClient;
        private readonly ILogger<DataExchangeHostedService> _logger;
        private readonly TunNetworkDrive _tunNetworkDrive;
        public static DataExchangeHostedService Instance { get; private set; }

        public TunNetworkDrive TunNetworkDrive => _tunNetworkDrive;

        public DataExchangeHostedService(ILogger<DataExchangeHostedService> logger, TunNetworkDrive tunNetworkDrive)
        {
            _logger = logger;
            udpClient = new UdpClient();
            Instance = this;
            _tunNetworkDrive = tunNetworkDrive;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var receiveResult = await udpClient.ReceiveAsync(); //NetworkDriveHostedService
                if (receiveResult.Buffer.Length > 1)
                {
                    TunNetworkDrive.WriteWriteDataQueue(receiveResult.Buffer);
                }
            }
        }
        /// <summary>
        /// 通过中转服务器发送出去
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="desIP"></param>
        /// <returns></returns>
        public virtual async Task SendBytes(byte[] bytes, int desIP)
        {
            using (var memoryStream = new System.IO.MemoryStream())
            {
                memoryStream.WriteByte((byte)(desIP >> 24));
                memoryStream.WriteByte((byte)(desIP >> 16 & 255));
                memoryStream.WriteByte((byte)(desIP >> 8 & 255));
                memoryStream.WriteByte((byte)(desIP & 255));
                memoryStream.Write(bytes.ToArray());
                await udpClient.SendAsync(memoryStream.ToArray(), HostName, Port);
            }
        }
        /// <summary>
        /// 是否资源
        /// </summary>
        public override void Dispose()
        {
            udpClient.Dispose();
            base.Dispose();
        }
    }
}
