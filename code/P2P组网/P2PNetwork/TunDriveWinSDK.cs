using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace P2PNetwork
{
    [SupportedOSPlatform("windows")]
    public class TunDriveWinSDK: TunDriveSDK
    {
        private static string DriverPath = AppDomain.CurrentDomain.BaseDirectory + "Drivers";
        private const string AdapterKey = "SYSTEM\\CurrentControlSet\\Control\\Class\\{4D36E972-E325-11CE-BFC1-08002BE10318}";
        private const string ConnectionKey = "SYSTEM\\CurrentControlSet\\Control\\Network\\{4D36E972-E325-11CE-BFC1-08002BE10318}";
        public const int TAP_WIN_IOCTL_GET_MAC = 1;
        public const int TAP_WIN_IOCTL_GET_VERSION = 2;
        public const int TAP_WIN_IOCTL_GET_MTU = 3;
        public const int TAP_WIN_IOCTL_GET_INFO = 4;
        public const int TAP_WIN_IOCTL_CONFIG_POINT_TO_POINT = 5;
        public const int TAP_WIN_IOCTL_SET_MEDIA_STATUS = 6;
        public const int TAP_WIN_IOCTL_CONFIG_DHCP_MASQ = 7;
        public const int TAP_WIN_IOCTL_GET_LOG_LINE = 8;
        public const int TAP_WIN_IOCTL_CONFIG_DHCP_SET_OPT = 9;
        public const int TAP_WIN_IOCTL_CONFIG_TUN = 10;

        public const uint FILE_ATTRIBUTE_SYSTEM = 0x4;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        public const uint METHOD_BUFFERED = 0;
        public const uint FILE_ANY_ACCESS = 0;
        public const uint FILE_DEVICE_UNKNOWN = 0x22;
        public TunDriveWinSDK(IConfiguration configuration, ILogger<TunDriveWinSDK> logger) : base(configuration, logger)
        {

        }
        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool DeviceIoControl(SafeHandle device, uint IoControlCode, IntPtr InBuffer, uint InBufferSize, IntPtr OutBuffer, uint OutBufferSize, ref uint BytesReturned, IntPtr Overlapped);
        public static void Extract()
        {
            Directory.CreateDirectory(DriverPath);
            ZipArchive zipArchive = new ZipArchive(typeof(TunDriveWinSDK).Assembly.GetManifestResourceStream($"P2PNetwork.{(Environment.Is64BitOperatingSystem ? "amd64" : "i386")}.zip"), ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in zipArchive.Entries)
            {
                entry.ExtractToFile(Path.Combine(DriverPath, entry.FullName), overwrite: true);
            }
        }
        public string Install(string tapName)
        {
            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(ConnectionKey))
            {
                var names = registryKey.GetSubKeyNames();
                foreach (var name in names)
                {
                    using (var connectionRegistryKey = registryKey.OpenSubKey(name).OpenSubKey("Connection"))
                    {
                        if (connectionRegistryKey != null && connectionRegistryKey.GetValue("Name").ToString() == tapName)
                        {
                            return name;
                        }
                    }
                }
                Extract();
                StartProcess(Path.Combine(DriverPath, "tapinstall.exe"), $"install OemVista.inf TAP0901", "runas", DriverPath);
                foreach (var name in registryKey.GetSubKeyNames())
                {
                    if (!names.Contains(name))
                    {
                        using (var connectionRegistryKey = registryKey.OpenSubKey(name).OpenSubKey("Connection"))
                        {
                            if (connectionRegistryKey != null)
                            {
                                StartProcess("netsh", @$"interface set interface name=""{connectionRegistryKey.GetValue("Name")}"" newname=""{tapName}""");
                                return name;
                            }
                        }
                    }
                }
            }
            return string.Empty;
        }
        public override bool OpenDrive()
        {
            if (FileStream == null || FileStream.SafeFileHandle.IsClosed)
            {
                var className = Install(DriveName);
                var safeFileHandle = System.IO.File.OpenHandle($@"\\.\\Global\\{className}.tap", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);
                FileStream = new FileStream(safeFileHandle, FileAccess.ReadWrite, 1500);
            }
            return true;
        }
        public override bool ConnectionState(bool connection)
        {
            uint Length = 0;
            IntPtr cconfig = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(cconfig, connection ? 1 : 0);

            var b = DeviceIoControl(FileStream.SafeFileHandle, CTL_CODE(FILE_DEVICE_UNKNOWN, TAP_WIN_IOCTL_SET_MEDIA_STATUS, METHOD_BUFFERED, FILE_ANY_ACCESS), cconfig, 4, cconfig, 4, ref Length, IntPtr.Zero);
            StartProcess("netsh", $"netsh interface ipv4 set subinterface \"{DriveName}\" mtu=\"1400\" store=persistent");
            return b;
        }
        private static uint CTL_CODE(uint iDeviceType, uint iFunction, uint iMethod, uint iAccess)
        {
            return ((iDeviceType << 16) | (iAccess << 14) | (iFunction << 2) | iMethod);
        }
        private static int ParseIP(IPAddress address)
        {
            byte[] addressBytes = address.GetAddressBytes();
            return addressBytes[0] | (addressBytes[1] << 8) | (addressBytes[2] << 16) | (addressBytes[3] << 24);
        }
        public override bool SetIP(IPAddress localIPAddress, IPAddress mask)
        {
            StartProcess("netsh", $"interface ip set address name=\"{DriveName}\" source=static addr={localIPAddress} mask={mask} gateway=none");
            IntPtr intPtr = Marshal.AllocHGlobal(12);
            Marshal.WriteInt32(intPtr, 0, ParseIP(localIPAddress));
            Marshal.WriteInt32(intPtr, 4, ParseIP(IPAddress.Any));
            Marshal.WriteInt32(intPtr, 8, ParseIP(IPAddress.Any));
            uint lpBytesReturned = 0;
            bool result = DeviceIoControl(FileStream.SafeFileHandle, 2228264, intPtr, 12u, intPtr, 12u, ref lpBytesReturned, IntPtr.Zero);
            Marshal.FreeHGlobal(intPtr);
            return result;
        }
    }
}
