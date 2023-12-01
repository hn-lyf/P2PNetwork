using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace P2PNetwork.HostedServices
{
    /// <summary>
    /// Tun 驱动服务
    /// </summary>
    public class TunDriveService : BackgroundService
    {
        private readonly IConfiguration _configuration; 
        private readonly ILogger<TunDriveService> _logger;
        private readonly IMemoryCache _memoryCache;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}
