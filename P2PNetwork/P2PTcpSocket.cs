using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;

namespace P2PNetwork
{
    public class P2PTcpSocket : P2PSocket
    {
        private static readonly short routeLevel;

        static P2PTcpSocket()
        {
            routeLevel = GetRouteLevel(null, out _);
        }
        public static IEnumerable<IPAddress> GetTraceRoute(string hostNameOrAddress)
        {
            return GetTraceRoute(hostNameOrAddress, 1);
        }
        private static IEnumerable<IPAddress> GetTraceRoute(string hostNameOrAddress, int ttl)
        {
            Ping pinger = new();
            // 创建PingOptions对象
            PingOptions pingerOptions = new(ttl, true);
            int timeout = 100;
            byte[] buffer = Encoding.ASCII.GetBytes("11");
            // 创建PingReply对象
            // 发送ping命令
            PingReply reply = pinger.Send(hostNameOrAddress, timeout, buffer, pingerOptions);

            // 处理返回结果
            List<IPAddress> result = new();
            if (reply.Status == IPStatus.Success)
            {
                result.Add(reply.Address);
            }
            else if (reply.Status == IPStatus.TtlExpired || reply.Status == IPStatus.TimedOut)
            {
                //增加当前这个访问地址
                if (reply.Status == IPStatus.TtlExpired)
                {
                    result.Add(reply.Address);
                }

                if (ttl <= 10)
                {
                    //递归访问下一个地址
                    IEnumerable<IPAddress> tempResult = GetTraceRoute(hostNameOrAddress, ttl + 1);
                    result.AddRange(tempResult);
                }
            }
            else
            {
                //失败
            }
            return result;
        }/// <summary>
         /// 获取路由层数，自己与外网距离几个网关，用于发送一个对方网络收不到没有回应的数据包
         /// </summary>
         /// <returns></returns>
        public static short GetRouteLevel(IPEndPoint iPEndPoint, out List<IPAddress> ips)
        {
            ips = new List<IPAddress>();
            try
            {
                List<string> starts = new() { "10.", "100.", "192.168.", "172." };
                var list = GetTraceRoute(P2PSocket.P2PHost).ToList();
                for (short i = 0; i < list.Count(); i++)
                {
                    string ip = list.ElementAt(i).ToString();
                    if (ip.StartsWith(starts[0], StringComparison.Ordinal) || ip.StartsWith(starts[1], StringComparison.Ordinal) || ip.StartsWith(starts[2], StringComparison.Ordinal) || ip.StartsWith(starts[3], StringComparison.Ordinal))
                    {
                        if (ip.StartsWith(starts[2], StringComparison.Ordinal) == false)
                            ips.Add(list.ElementAt(i));
                    }
                    else
                    {
                        return ++i;
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("我后面异常了？");
            }
            return 0;
        }
        public static Task SendTtl(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Ttl = routeLevel;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(localEndPoint);
            return socket.ConnectAsync(remoteEndPoint);

        }
        private static async Task<Socket> TestConnectAsync(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(localEndPoint);
            try
            {
                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(10000))
                {
                    await socket.ConnectAsync(remoteEndPoint, cancellationTokenSource.Token);
                    var bytes = new byte[1] { 1 };
                    await socket.SendAsync(new byte[] { 1 }, SocketFlags.None);
                    using (CancellationTokenSource cancellationTokenSource2 = new CancellationTokenSource(2000))
                    {
                        try
                        {
                            var length = await socket.ReceiveAsync(bytes, SocketFlags.None, cancellationTokenSource2.Token);
                            if (length == 1)
                            {
                                return socket;
                            }
                        }
                        catch (Exception)
                        {

                        }
                    }
                    Console.WriteLine("我被判定为假链接，关闭");

                }
            }
            catch (Exception ex)
            {
                return null;
            }
            socket.Close();
            return null;
        }
        public override async ValueTask SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteByte((byte)(buffer.Length >> 8));
                memoryStream.WriteByte((byte)(buffer.Length & 255));
                memoryStream.Write(buffer.ToArray());
                await client.SendAsync(memoryStream.ToArray(), SocketFlags.None);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var index = 0;
            while (!stoppingToken.IsCancellationRequested && Client != null)
            {
                var bytes = new byte[2];
                var length = await Client.ReceiveAsync(bytes, SocketFlags.None, stoppingToken);
                if (length >= 2)
                {
                    bytes = new byte[bytes[0] << 8 | bytes[1]];
                    length = 0;
                    while (length != bytes.Length)
                    {
                        var temp = new byte[bytes.Length];
                        var len = await Client.ReceiveAsync(temp, SocketFlags.None, stoppingToken);
                        if (len > 0)
                        {
                            Array.Copy(temp, 0, bytes, length, len);
                            length += len;
                        }
                        else
                        {
                            index++;
                            if (index > 3)
                            {
                                await StopAsync(default);
                                return;
                            }
                            continue;
                        }
                    }
                    _ = OnReceiveDataAsync(bytes);
                }
            }
        }
        private readonly Socket client;
        public P2PTcpSocket(ulong ip, Socket client) : base(ip)
        {
            this.client = client;
        }
        public virtual Socket Client => client;
        public override string P2PTypeName => "P2P TCP";
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Client.Dispose();
            return base.StopAsync(cancellationToken);
        }
        public static async Task<P2PSocket> TestP2P(ulong ip, ulong id)
        {
            var ips = Dns.GetHostAddresses(P2PSocket.P2PHost);
            using Socket targetSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            targetSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            IPEndPoint remoteEndPoint = null;
            IPEndPoint localEndPoint = null;

            try
            {
                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(3000))
                {
                    await targetSocket.ConnectAsync(ips[0], P2PSocket.P2PPort, cancellationTokenSource.Token);
                    localEndPoint = targetSocket.LocalEndPoint as IPEndPoint;
                    await targetSocket.SendAsync(BitConverter.GetBytes(id), SocketFlags.None);
                    using (CancellationTokenSource cancellationTokenSource1 = new CancellationTokenSource(5000))
                    {
                        var bytes = new byte[100];
                        var length = await targetSocket.ReceiveAsync(bytes, SocketFlags.None, cancellationTokenSource1.Token);
                        var text = Encoding.UTF8.GetString(bytes, 0, length);
                        remoteEndPoint = IPEndPoint.Parse(text);//远程端口
                        Console.WriteLine(text);
                        targetSocket.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}" + ex);
            }
            if (remoteEndPoint != null)
            {
                var task2 = Task.FromResult((Socket)null);// TestListenerAsync(localEndPoint, remoteEndPoint);
                try
                {
                    _ = SendTtl(localEndPoint, remoteEndPoint);
                }
                catch (Exception ex) { }
                var task1 = TestConnectAsync(localEndPoint, remoteEndPoint);//Task.FromResult((Socket)null);//

                Task.WaitAll(task1, task2);
                P2PTcpSocket pTcpSocket = null;
                if (task1.Result != null)
                {
                    pTcpSocket = new P2PTcpSocket(ip, task1.Result);
                    if (task2.Result != null)
                    {
                        Console.WriteLine("我也成功了？");
                    }
                }
                else if (task2.Result != null)
                {
                    pTcpSocket = new P2PTcpSocket(ip, task1.Result);
                }
                if (pTcpSocket != null)
                {
                    _ = System.Threading.Tasks.Task.Factory.StartNew(() => pTcpSocket.StartAsync(default), TaskCreationOptions.LongRunning);
                }
                return pTcpSocket;
            }
            return null;
        }
    }
}
