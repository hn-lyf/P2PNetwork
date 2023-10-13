using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace P2PNetwork
{
    public class TunDriveLinuxSDK : TunDriveSDK
    {
        [DllImport("libc.so.6", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int Ioctl(SafeHandle device, UInt32 request, byte[] dat);
        public TunDriveLinuxSDK(IConfiguration configuration, ILogger<TunDriveSDK> logger) : base(configuration, logger)
        {
        }

        public override bool ConnectionState(bool connection)
        {
            StartProcess("ip", $"link set {DriveName} up");
            Logger.LogInformation($"设置网卡“{DriveName}” 为启用状态 mtu：1400");
            StartProcess("ip", $"link set dev {DriveName} mtu 1400");
            return true;
        }

        public override bool OpenDrive()
        {
            if (FileStream == null || FileStream.SafeFileHandle.IsClosed)
            {
                var safeFileHandle = System.IO.File.OpenHandle("/dev/net/tun", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);
                const UInt32 TUNSETIFF = 1074025674;
                byte[] ifreqFREG0 = System.Text.Encoding.ASCII.GetBytes(DriveName);
                Array.Resize(ref ifreqFREG0, 16);
                byte[] ifreqFREG1 = { 0x01, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                //IFF_TUN | IFF_NO_PI == 4097 == 1001 == 0x10,0x01  tun0
                byte[] ifreq = BytesPlusBytes(ifreqFREG0, ifreqFREG1);
                int stat = Ioctl(safeFileHandle, TUNSETIFF, ifreq);
                FileStream = new FileStream(safeFileHandle, FileAccess.ReadWrite, 1500);
                return !safeFileHandle.IsClosed;
            }
            return true;
        }
        private byte[] BytesPlusBytes(byte[] A, byte[] B)
        {
            byte[] ret = new byte[A.Length + B.Length - 1 + 1];
            int k = 0;
            for (var i = 0; i <= A.Length - 1; i++)
                ret[i] = A[i];
            k = A.Length;
            for (var i = k; i <= ret.Length - 1; i++)
                ret[i] = B[i - k];
            return ret;
        }
        public override bool SetIP(IPAddress localIPAddress, IPAddress mask)
        {
            var maskLength = 32 - Convert.ToString(~mask.GetAddressBytes().ToUInt32(), 2).Length;
            Logger.LogInformation($"设置网卡“{DriveName}” IP地址：{localIPAddress}/{maskLength}");
            StartProcess("ip", $"addr add {localIPAddress.ToString()}/{maskLength} dev {DriveName}");
            return true;
        }
    }
}
