using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using P2PNetwork.HostedServices;

namespace P2PNetwork.HostClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ThreadPool.SetMaxThreads(5000, 5000);
            var builder = new HostApplicationBuilder(args);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                builder.Services.AddSingleton<TunDriveSDK, TunDriveWinSDK>();
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                builder.Services.AddSingleton<TunDriveSDK, TunDriveLinuxSDK>();
            }
            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<P2PUDPSocket>();
            builder.Services.AddScoped<P2PTcpSocket>();

            builder.Services.AddScoped<ICMPNatSocket>();
            builder.Services.AddScoped<UDPNatSocket>();
            builder.Services.AddScoped<TCPNatSocket>();


            builder.Services.AddHostedService<NetworkDriveHostedService>();
            builder.Services.AddHostedService<P2PSocketHostedService>();
            builder.Services.AddHostedService<ExchangeSocketHostedService>();
            builder.Services.AddHostedService<NatSocketHostedService>();

            var app = builder.Build();
            app.Run();
        }
    }
}