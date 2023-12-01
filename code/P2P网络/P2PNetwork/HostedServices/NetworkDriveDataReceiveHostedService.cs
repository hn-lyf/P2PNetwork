using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace P2PNetwork.HostedServices
{
    /// <summary>
    /// 网卡驱动接收
    /// </summary>
    public class NetworkDriveDataReceiveHostedService : BackgroundService
    {
        private readonly TunNetworkDrive _tunNetworkDrive;
        private readonly Logger<NetworkDriveDataWriteHostedService> _logger;

        public NetworkDriveDataReceiveHostedService(TunNetworkDrive tunNetworkDrive, Logger<NetworkDriveDataWriteHostedService> logger)
        {
            _tunNetworkDrive = tunNetworkDrive;
            _logger = logger;
        }

        public TunNetworkDrive TunNetworkDrive => _tunNetworkDrive;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (stoppingToken.IsCancellationRequested)
            {
                if (TunNetworkDrive.ReadDataQueue.TryDequeue(out var bytes))
                {
                    //网卡读到的数据，需要转发出去的
                    //提取到目标ip地址,然后判定是否有p2p网络，如果有就发到p2p的队列，如果没有就发送到数据交换区
                    var desIPInt = bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19];
                    var desIP = string.Join(".", bytes[16], bytes[17], bytes[18], bytes[19]);
                    await SendToBytes(bytes, desIPInt, desIP);
                }
            }
        }
        protected virtual async Task SendToBytes(byte[] bytes, int desIPInt, string desIP)
        {
            if (TunNetworkDrive.TryGetP2PSocketQueue(desIPInt, out var p2pSocketQueue))
            {
                p2pSocketQueue.WriteWriteDataQueue(bytes);//写入到p2p队列数据
                return;
            }
            await DataExchangeHostedService.Instance.SendBytes(bytes, desIPInt);//如果没有打洞就通过数据转发发送出去
        }
    }
}
