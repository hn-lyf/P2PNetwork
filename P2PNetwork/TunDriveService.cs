using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace P2PNetwork
{
    public class TunDriveService : BackgroundService
    {
        private readonly TunDriveSDK tunDriveSDK;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TunDriveService> _logger;
        private readonly ExchangeSocket exchangeSocket;
        private readonly IMemoryCache _memoryCache;
        public TunDriveService(IConfiguration configuration, ILogger<TunDriveService> logger, ILogger<TunDriveSDK> loggerTunDriveSDK, IMemoryCache memoryCache)
        {
            _configuration = configuration;
            _logger = logger;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                tunDriveSDK = new TunDriveWinSDK(configuration, loggerTunDriveSDK);
            }
            else
            {
                tunDriveSDK = new TunDriveLinuxSDK(configuration, loggerTunDriveSDK);
            }
            exchangeSocket = new ExchangeSocket();
            exchangeSocket.ReceiveDataAsync += P2pSocket_ReceiveDataAsync;
            _memoryCache = memoryCache;
        }

        public TunDriveSDK TunDriveSDK => tunDriveSDK;
        public virtual string LocalIP { get { return exchangeSocket.LocalIP; } set { exchangeSocket.LocalIP = value; } }
        public virtual byte[] LocalIPBytes { get { return exchangeSocket.LocalIPBytes; } }
        public virtual int LocalIPInt { get { return exchangeSocket.LocalIPInt; } }


        public ExchangeSocket ExchangeSocket => exchangeSocket;

        public IMemoryCache MemoryCache => _memoryCache;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (string.IsNullOrEmpty(LocalIP))
                {
                    await Task.Delay(200);
                    continue;
                }
                break;
            }
            while ((!stoppingToken.IsCancellationRequested))
            {
                TunDriveSDK.OpenDrive();
                TunDriveSDK.SetIP(System.Net.IPAddress.Parse(LocalIP), System.Net.IPAddress.Parse("255.255.255.0"));

                TunDriveSDK.ConnectionState(true);
                ExchangeSocket.LocalIP = LocalIP;
                _ = ExchangeSocket.StartAsync(stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var buffer = new Memory<byte>(new byte[1500]);
                        int length = await TunDriveSDK.ReadAsync(buffer, stoppingToken);
                        _ = OnunReceiveAsync(buffer.Slice(0, length));
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            _ = ExchangeSocket.StopAsync(stoppingToken);


        }
        public event Func<Memory<byte>, ValueTask> ReceiveDataAsync;
        public event Func<int, ValueTask> P2PTest;
        protected virtual ValueTask OnunReceiveAsync(Memory<byte> buffer)
        {
            try
            {
                var span = buffer.Span;
                if (buffer.Span[0] == 0x45)
                {
                    if (ReceiveDataAsync != null)
                    {
                        return ReceiveDataAsync(buffer);
                    }
                    var destIPMask = span[16] << 8 | span[17];
                    var destIP = span[16] << 24 | span[17] << 16 | span[18] << 8 | span[19];
                    var socket = P2PSocket.GetP2PPSocket(destIP, LocalIPInt);
                    if (socket != null)
                    {
                        return socket.SendAsync(buffer);
                    }
                    if (P2PTest != null)
                    {
                        _ = MemoryCache.GetOrCreate(destIP, (ic) =>
                        {
                            _ = P2PTest(destIP);
                            ic.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                            return true;
                        });
                    }

                    return ExchangeSocket.SendAsync(destIP, buffer);
                }
            }
            catch
            {

            }
            return ValueTask.CompletedTask;
        }

        private ValueTask P2pSocket_ReceiveDataAsync(Memory<byte> buffer)
        {
            return WriteAsync(buffer);
        }

        public virtual ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return TunDriveSDK.WriteAsync(buffer, cancellationToken);
        }
    }
}
