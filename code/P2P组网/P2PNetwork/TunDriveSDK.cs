using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace P2PNetwork
{
    public abstract class TunDriveSDK : IDisposable
    {
        private readonly string driveName;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private ushort index = 0;
        private bool disposedValue;


        private byte[] localIPBytes;
        private int localIPInt;
        private string localIP;

        public TunDriveSDK(IConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            driveName = configuration.GetValue<string>("driveName", "P2P");
            LocalIP = configuration.GetValue<string>("LocalIP", "43.168.8.8");
            _logger = logger;
        }
        public byte[] LocalIPBytes { get => localIPBytes; }
        public int LocalIPInt { get => localIPInt; }
        public IConfiguration Configuration => _configuration;
        public virtual string LocalIP
        {
            get
            {
                return localIP;
            }
            set
            {
                localIP = value;
                localIPBytes = localIP.Split('.').Select(byte.Parse).ToArray();
                localIPInt = localIPBytes[0] << 24 | localIPBytes[1] << 16 | localIPBytes[2] << 8 | localIPBytes[3];
            }
        }
        public ILogger Logger => _logger;
        public string DriveName => driveName;

        protected virtual FileStream FileStream { get; set; }
        public abstract bool OpenDrive();

        public abstract bool ConnectionState(bool connection);
        public virtual ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return FileStream.ReadAsync(buffer, cancellationToken);
        }
        public virtual async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            FileStream.Write(buffer.ToArray());//这里异步写反而会很慢，不知道为什么，所以被迫改成了同步
            FileStream.Flush();
            semaphore.Release();
        }
        public abstract bool SetIP(IPAddress localIPAddress, IPAddress mask);
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
                FileStream?.Close();
                FileStream?.Dispose();
                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
