using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace P2PNetwork.P2PListener.HostedServices
{
    public class P2PListenerHostedService : BackgroundService
    {
        private readonly UdpClient udpClient;
        private readonly ILogger _logger;
        private readonly TcpListener tcpListener;
        private readonly ConcurrentDictionary<ulong, IPEndPoint> p2pUdpEndPoints = new ConcurrentDictionary<ulong, IPEndPoint>();
        private readonly ConcurrentDictionary<ulong, Socket> p2pTcpSockets = new ConcurrentDictionary<ulong, Socket>();
        private readonly ConcurrentDictionary<int, IPEndPoint> remoteEndPoints = new ConcurrentDictionary<int, IPEndPoint>();
        public P2PListenerHostedService(ILogger<P2PListenerHostedService> logger)
        {
            _logger = logger;
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 62001));
            tcpListener = new TcpListener(IPAddress.Any, 62001);
        }

        public ILogger Logger => _logger;
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"tcp 打洞服务启动");
            tcpListener.Start();
            tcpListener.BeginAcceptSocket(AcceptSocketCallback, tcpListener);
            return base.StartAsync(cancellationToken);
        }
        protected virtual async void AcceptSocketCallback(IAsyncResult asyncResult)
        {
            Socket socket = null;
            try
            {
                socket = tcpListener.EndAcceptSocket(asyncResult);
                _logger.LogWarning($"{socket.RemoteEndPoint} 请求打洞");
            }
            catch (Exception ex)
            {

            }
            finally
            {
                tcpListener.BeginAcceptSocket(AcceptSocketCallback, tcpListener);
            }
            if (socket != null)
            {
                using (socket)
                {
                    var bytes = new byte[8];
                    var length = await socket.ReceiveAsync(bytes, SocketFlags.None);
                    var id = BitConverter.ToUInt64(bytes);
                    _logger.LogWarning($"{DateTime.Now}{socket.RemoteEndPoint} {id} tcp 请求打洞了");
                    var oleRemoteEndPoint = socket.RemoteEndPoint;
                    var t = p2pTcpSockets.AddOrUpdate(id, socket, (id, old) =>
                    {
                        _logger.LogWarning($"{socket.RemoteEndPoint} {id} tcp 准发发送对方ip");
                        old.Send(System.Text.Encoding.UTF8.GetBytes(socket.RemoteEndPoint.ToString()));
                        socket.Send(System.Text.Encoding.UTF8.GetBytes(old.RemoteEndPoint.ToString()));
                        oleRemoteEndPoint = old.RemoteEndPoint;
                        return socket;
                    });
                    if (oleRemoteEndPoint.ToString() == t.RemoteEndPoint.ToString())
                    {
                        await Task.Delay(5000);
                        if (p2pTcpSockets.ContainsKey(id))//间隔5s还没被接受，就认为是死链
                        {
                            _logger.LogWarning($"{oleRemoteEndPoint} {id} 无接受端 tcp 被关闭");
                            p2pTcpSockets.TryRemove(id, out _);
                        }
                    }
                    else
                    {
                        p2pTcpSockets.TryRemove(id, out _);
                    }
                    _logger.LogWarning($"{DateTime.Now} {socket.RemoteEndPoint} {id} tcp 关闭");
                    await Task.Delay(100);
                    socket.Close(100);
                }
            }

        }
        protected virtual async Task OnReceiveData(UdpReceiveResult receiveResult)
        {
            if (receiveResult.Buffer.Length == 8)//打洞
            {
                var id = BitConverter.ToUInt64(receiveResult.Buffer);

                _logger.LogWarning($"{receiveResult.RemoteEndPoint}   {id}  请求udp打洞");
                var oleRemoteEndPoint = receiveResult.RemoteEndPoint;
                var t = p2pUdpEndPoints.AddOrUpdate(id, receiveResult.RemoteEndPoint, (id, old) =>
                {
                    udpClient.Send(System.Text.Encoding.UTF8.GetBytes(receiveResult.RemoteEndPoint.ToString()), old);
                    udpClient.Send(System.Text.Encoding.UTF8.GetBytes(old.ToString()), receiveResult.RemoteEndPoint);
                    oleRemoteEndPoint = old;
                    return receiveResult.RemoteEndPoint;
                });
                if (oleRemoteEndPoint.ToString() == t.ToString())
                {
                    await Task.Delay(5000);
                    if (p2pUdpEndPoints.ContainsKey(id))//间隔5s还没被接受，就认为是死链
                    {
                        _logger.LogWarning($"{receiveResult.RemoteEndPoint} {id} udp 无接受端 被关闭");
                    }
                }
                else
                {
                    p2pUdpEndPoints.TryRemove(id, out _);
                }
            }
            else
            {
                var ip = receiveResult.Buffer[0] << 24 | receiveResult.Buffer[1] << 16 | receiveResult.Buffer[2] << 8 | receiveResult.Buffer[3];
                if (receiveResult.Buffer.Length == 4)
                {
                    remoteEndPoints.AddOrUpdate(ip, receiveResult.RemoteEndPoint, (i, o) => receiveResult.RemoteEndPoint);
                    _logger.LogInformation($"{ip.ToIP()} ：{receiveResult.RemoteEndPoint}");
                }
                else
                {
                    if (remoteEndPoints.TryGetValue(ip, out var remoteEndPoint))
                    {

                        _ = udpClient.SendAsync(receiveResult.Buffer.AsSpan(4).ToArray(), remoteEndPoint);
                    }
                    else
                    {
                        _logger.LogInformation($"{receiveResult.RemoteEndPoint} 发送到 {ip.ToIP()} 没上线，被抛弃");
                    }
                }
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"udp & tcp 打洞服务启动");
            while (!stoppingToken.IsCancellationRequested)
            {
                var receiveResult = await udpClient.ReceiveAsync();
                _ = OnReceiveData(receiveResult);
            }
            tcpListener.Stop();
            udpClient.Close();
        }
    }
    public static class ByteExtension
    {
        public static int ToInt32(this byte[] bytes, int index = 0)
        {
            return (bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3];
        }
        public static uint ToUInt32(this byte[] bytes, int index = 0)
        {
            return (uint)((bytes[index] << 24) | (bytes[index + 1] << 16) | (bytes[index + 2] << 8) | bytes[index + 3]);
        }

        public static byte[] ToBytes(this ulong value)
        {
            byte[] array = new byte[8];
            array[7] = (byte)(value & 0xFF);
            array[6] = (byte)((value >> 8) & 0xFF);
            array[5] = (byte)((value >> 16) & 0xFF);
            array[4] = (byte)((value >> 24) & 0xFF);
            array[3] = (byte)((value >> 32) & 0xFF);
            array[2] = (byte)((value >> 40) & 0xFF);
            array[1] = (byte)((value >> 48) & 0xFF);
            array[0] = (byte)((value >> 56) & 0xFF);
            return array;
        }
        public static string ToIP(this int ip)
        {
            return string.Join(".", ip >> 24 & 255, ip >> 16 & 255, ip >> 8 & 255, ip & 255);
        }
        public static int IPToInt(this string ip)
        {
            return ip.Split('.').Select(byte.Parse).ToArray().ToInt32(); ;
        }
        public static string GetString(this byte[] bytes, Encoding encoding = null)
        {
            return (encoding ?? Encoding.UTF8).GetString(bytes);
        }
    }
}
