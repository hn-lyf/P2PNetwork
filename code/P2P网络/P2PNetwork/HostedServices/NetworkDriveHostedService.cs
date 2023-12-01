using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace P2PNetwork.HostedServices
{
    /// <summary>
    /// 网卡驱动服务
    /// </summary>
    public class NetworkDriveHostedService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (stoppingToken.IsCancellationRequested)
            {
                //读取网卡数据
                //NetworkDriveDataReceiveHostedService
            }
        }
    }
}
