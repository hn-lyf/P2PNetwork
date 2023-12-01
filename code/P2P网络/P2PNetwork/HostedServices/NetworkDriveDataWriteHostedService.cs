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
    /// 网卡驱动写入服务
    /// </summary>
    public class NetworkDriveDataWriteHostedService : BackgroundService
    {
        private readonly TunNetworkDrive _tunNetworkDrive;
        private readonly Logger<NetworkDriveDataWriteHostedService> _logger;

        public NetworkDriveDataWriteHostedService(TunNetworkDrive tunNetworkDrive, Logger<NetworkDriveDataWriteHostedService> logger)
        {
            _tunNetworkDrive = tunNetworkDrive;
            _logger = logger;
        }

        public TunNetworkDrive TunNetworkDrive => _tunNetworkDrive;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (stoppingToken.IsCancellationRequested)
            {
                if (TunNetworkDrive.WriteDataQueue.TryDequeue(out var bytes))
                {
                    //需要处理比如，需要nat的数据
                }
            }
        }
    }
}
