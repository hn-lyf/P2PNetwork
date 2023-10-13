using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace P2PNetwork
{
    public abstract class TunDriveSDK : IDisposable
    {
        private readonly string driveName;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TunDriveSDK> _logger;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private ushort index = 0;
        private bool disposedValue;

        public string DriveName => driveName;

        public IConfiguration Configuration => _configuration;

        public ILogger<TunDriveSDK> Logger => _logger;

        public TunDriveSDK(IConfiguration configuration, ILogger<TunDriveSDK> logger)
        {
            _configuration = configuration;
            driveName = configuration.GetValue<string>("driveName", "qcxt");
            _logger = logger;
        }
        protected virtual FileStream FileStream { get; set; }
        public abstract bool OpenDrive();

        public abstract bool ConnectionState(bool connection);
        public abstract bool SetIP(IPAddress localIPAddress, IPAddress mask);
        public virtual ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return FileStream.ReadAsync(buffer, cancellationToken);
        }

        public virtual async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var v4Packet = new IPv4Packet(buffer.ToArray());
            await semaphore.WaitAsync(cancellationToken);
            if (v4Packet.Protocol == EProtocolType.TCP)
            {
                var tcpPacket = new TCPPacket(v4Packet);
                tcpPacket.Id = index++;
                FileStream.Write(tcpPacket.ToBytes());
            }
            else if (v4Packet.Protocol == EProtocolType.UDP)
            {
                var udpPacket = new UDPPacket(v4Packet);
                udpPacket.Id = index++;
                FileStream.Write(udpPacket.ToBytes());
            }
            else if (v4Packet.Protocol == EProtocolType.ICMP)
            {
                var icmpPacket = new ICMPPacket(v4Packet);
                icmpPacket.Id = index++;
                FileStream.Write(icmpPacket.ToBytes());
            }
            FileStream.Flush();
            semaphore.Release();
        }
        public virtual string StartProcess(string fileName, string arguments, string verb = null, string workingDirectory = null)
        {
            string empty = string.Empty;
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                    FileName = fileName,
                    Verb = verb
                };
                process.Start();
                process.WaitForExit();
                empty = process.StandardOutput.ReadToEnd();
                process.Close();
            }
            return empty;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)
                }
                FileStream.Close();
                FileStream.Dispose();
                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~TunDriveSDK()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
