using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace P2PNetwork.HostedServices
{
    public class P2PSocketManageHostedService : BackgroundService
    {
        private readonly ConcurrentQueue<int> desIPP2PQueue = new ConcurrentQueue<int>();
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (desIPP2PQueue.TryDequeue(out var desIP))
                {
                    //需要打洞的
                    _ = Task.Factory.StartNew(() => OnTestP2PAsync(desIP), TaskCreationOptions.LongRunning);
                }
                else
                {
                    await Task.Delay(3);
                }
            }
        }
        protected virtual async Task OnTestP2PAsync(int desIP)
        {
            await Task.Delay(100);
        }
    }
}
