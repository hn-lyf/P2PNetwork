using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using P2PNetwork.Client.HostedServices;

namespace P2PNetwork.Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
#if DEBUG
                Run(args);
                return;
#endif
                DotNet.Install.ServiceInstall.Run("qcxtVPN", args, Run, "全程迅通VPN", "全程迅通VPN");
            }
            else
            {
                Run(args);
            }
        }
        static void Run(string[] args)
        {
            ThreadPool.SetMaxThreads(2000, 2000);
            var builder = new HostApplicationBuilder(args);
            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<TunDriveService>();
            builder.Services.AddHostedService<HostClientHostedService>();
          

            var app = builder.Build();
            app.Run();
        }
    }
}