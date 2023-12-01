using Microsoft.Extensions.Logging;
using P2PNetwork.HostedServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace P2PNetwork
{
    public class P2PTcpSocket : P2PSocket
    {
        private static short routeLevel;
       
        static P2PTcpSocket()
        {
            Task.Run(() =>
            {
                routeLevel = GetRouteLevel(null, out _);
                if (routeLevel == 0)
                {
                    Console.WriteLine($"获取ttl失败，强制改为2");
                    routeLevel = 2;
                }
                Console.WriteLine($"获取到TTL：{routeLevel}");
            });
        }
        public static short RouteLevel => routeLevel;
        public static IEnumerable<IPAddress> GetTraceRoute(string hostNameOrAddress)
        {
            return GetTraceRoute(hostNameOrAddress, 1);
        }
        private static IEnumerable<IPAddress> GetTraceRoute(string hostNameOrAddress, int ttl)
        {
            Ping pinger = new();
            // 创建PingOptions对象
            PingOptions pingerOptions = new(ttl, true);
            int timeout = 200;
            byte[] buffer = Encoding.ASCII.GetBytes("11");
            // 创建PingReply对象
            // 发送ping命令
            PingReply reply = pinger.Send(hostNameOrAddress, timeout, buffer, pingerOptions);

            // 处理返回结果
            List<IPAddress> result = new();
            if (reply.Status == IPStatus.Success)
            {
                result.Add(reply.Address.MapToIPv4());
            }
            else if (reply.Status == IPStatus.TtlExpired || reply.Status == IPStatus.TimedOut)
            {
                //增加当前这个访问地址
                if (reply.Status == IPStatus.TtlExpired)
                {
                    result.Add(reply.Address);
                }

                if (ttl <= 20)
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
        }
        /// <summary>
        /// 获取路由层数，自己与外网距离几个网关，用于发送一个对方网络收不到没有回应的数据包
        /// </summary>
        /// <returns></returns>
        public static short GetRouteLevel(IPEndPoint iPEndPoint, out List<IPAddress> ips)
        {
            ips = new List<IPAddress>();
            try
            {
                List<string> starts = new() { "10.", "100.", "192.168.", "172." };
                var list = GetTraceRoute("www.baidu.com").ToList();
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

        private Socket client;
        public P2PTcpSocket(ILogger<P2PTcpSocket> logger) : base(logger)
        {
            
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
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var bytes = new byte[2];
                    var length = await client.ReceiveAsync(bytes, SocketFlags.None, stoppingToken);
                    if (length >= 2)
                    {
                        bytes = new byte[bytes[0] << 8 | bytes[1]];
                        length = 0;
                        while (length != bytes.Length)
                        {
                            var temp = new byte[bytes.Length - length];
                            var len = await client.ReceiveAsync(temp, SocketFlags.None, stoppingToken);
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
                        await OnReceiveDataAsync(bytes);
                    }
                }catch (Exception ex)
                {
                    if (!client.Connected)
                    {
                        Logger.LogInformation($"通道被中断");
                        this.Dispose();
                        break;
                    }
                }
            }

        }

        private  async Task<Socket> TestConnectAsync(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, CancellationTokenSource cancellationTokenSource)
        {
            Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(localEndPoint);
            try
            {
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
        public  Task SendTtl(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Ttl = routeLevel;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(localEndPoint);
            return socket.ConnectAsync(remoteEndPoint);

        }
        private static async Task<Socket> TestListenerAsync(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(localEndPoint);
            try
            {
                socket.Listen();
                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(10000))
                {
                    Socket client = await socket.AcceptAsync(cancellationTokenSource.Token);
                    Console.WriteLine($" TCP 监听收到了{client.RemoteEndPoint}");
                    var bytes = new byte[1] { 1 };
                    await client.SendAsync(new byte[] { 1 }, SocketFlags.None);
                    var length = await client.ReceiveAsync(bytes, SocketFlags.None);
                    if (length == 1)
                    {
                        return client;
                    }
                    return client;
                }
            }
            catch (Exception)
            {
                socket.Close();
                return null;
            }
        }
        public async Task<bool> TestP2P(int desIP, byte[] idBytes,bool listener=false, CancellationToken cancellationToken = default)
        {
            DesIP = desIP;
            var ips = Dns.GetHostAddresses(P2PHost);
            using Socket targetSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            targetSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            IPEndPoint remoteEndPoint = null;
            IPEndPoint localEndPoint = null;
            try
            {
                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(3000))
                {
                    await targetSocket.ConnectAsync(ips[0], P2PPort, cancellationTokenSource.Token);
                    localEndPoint = targetSocket.LocalEndPoint as IPEndPoint;
                    await targetSocket.SendAsync(idBytes, SocketFlags.None);
                    using (CancellationTokenSource cancellationTokenSource1 = new CancellationTokenSource(5000))
                    {
                        var bytes = new byte[100];
                        var length = await targetSocket.ReceiveAsync(bytes, SocketFlags.None, cancellationTokenSource1.Token);
                        var text = Encoding.UTF8.GetString(bytes, 0, length);
                        remoteEndPoint = IPEndPoint.Parse(text);//远程端口
                        Console.WriteLine($"{localEndPoint}获取需要打洞方：{text}");
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
                if (listener)
                {
                    Logger.LogInformation($"准备监听（{string.Join(".", desIP.ToBytes())}）打洞 {remoteEndPoint}->{localEndPoint}");
                    _ = SendTtl(localEndPoint, remoteEndPoint);
                    var socket = await TestListenerAsync(localEndPoint, remoteEndPoint);
                    if (socket != null)
                    {
                        client = socket;
                        Console.WriteLine($" 打洞成功");
                        P2PSocketHostedService.Instance.AddP2P(desIP, this);
                        return true;
                    }

                }
                else
                {
                    await Task.Delay(200);
                    Logger.LogInformation($"准备向（{string.Join(".", desIP.ToBytes())}）打洞 {localEndPoint}->{remoteEndPoint}");
                    var tasks = new Task<Socket>[10];
                    CancellationTokenSource cancellationTokenSource1 = new CancellationTokenSource(5000);
                    for (var i = 0; i < tasks.Length; i++)
                    {
                        
                        tasks[i] = TestConnectAsync(localEndPoint, new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port + i), cancellationTokenSource1);
                    }
                    Task.WaitAny(tasks);
                    cancellationTokenSource1.Cancel();
                    for (var i = 0; i < tasks.Length; i++)
                    {
                        if (tasks[i].Result != null)
                        {
                            client = tasks[i].Result;
                            Console.WriteLine($"{tasks[i].Result} 打洞成功");
                            P2PSocketHostedService.Instance.AddP2P(desIP, this);
                            return true;
                        }
                    }
                }
                Console.WriteLine("TCP 打洞失败");
            }
            this.Dispose();
            return false;
        }
    }
}
