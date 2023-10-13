using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using P2PNetwork.P2PListener.HostedServices;

namespace P2PNetwork.P2PListener
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ThreadPool.SetMaxThreads(2000, 2000);
            var builder = new HostApplicationBuilder(args);
            builder.Services.AddMemoryCache();
            builder.Services.AddHostedService<P2PListenerHostedService>();
            var app = builder.Build();
            app.Run();
        }
    }
}