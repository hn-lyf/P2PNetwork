using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace P2PNetwork.Client.HostedServices
{
    public class HostClientHostedService : P2PNetworkHostedService
    {
        public HostClientHostedService(ILogger<HostClientHostedService> logger, IConfiguration configuration, IServiceProvider serviceProvider) : base(logger, configuration, serviceProvider)
        {

        }
    }
}
