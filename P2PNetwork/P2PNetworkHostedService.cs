using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;

namespace P2PNetwork
{
    public class P2PNetworkHostedService : BackgroundService
    {
        private readonly string _host = "vpn.qcxt.com";
        private readonly int _port = 62000;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IMqttClient _mqttClient;
        private readonly TunDriveService tunDriveService;
        public P2PNetworkHostedService(ILogger logger, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            tunDriveService = serviceProvider.GetService<TunDriveService>();
            _logger = logger;
            _configuration = configuration;
            _mqttClient = new MQTTnet.MqttFactory().CreateMqttClient();
            Client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;
            Client.DisconnectedAsync += Client_DisconnectedAsync; ;
            tunDriveService.P2PTest += TestP2P;
        }

        public virtual ValueTask TestP2P(int ip)
        {
            var socket = P2PSocket.GetP2PPSocket(ip, tunDriveService.LocalIPInt);
            if (socket != null)
            {
                return ValueTask.CompletedTask;
            }
            _ = System.Threading.Tasks.Task.Factory.StartNew(async () =>
            {

                var rip = ip.ToIP();
                var p2pId = tunDriveService.LocalIPBytes.Concat(ip.ToBytes()).ToArray().ToUInt64();
                Logger.LogInformation($"{DateTime.Now:F} {Client.Options.ClientId} 尝试打洞{ip} ");
                ulong id = 0l;
                lock (this)
                {
                    id = (ulong)DateTime.Now.Ticks;
                }
                _ = Client.PublishBinaryAsync($"/sys/{rip}/p2p", tunDriveService.LocalIPBytes.Concat(id.ToBytes()).ToArray());

                var p2pSocket = await P2PUDPSocket.TestP2P(p2pId, id);
                if (p2pSocket != null)
                {
                    //打洞成功
                    Logger.LogInformation($"{DateTime.Now:F} {Client.Options.ClientId} {p2pSocket.P2PTypeName} 打洞{rip}成功 ");
                    p2pSocket.ReceiveDataAsync += P2pSocket_ReceiveDataAsync;
                }
                socket = P2PSocket.GetP2PPSocket(ip, tunDriveService.LocalIPInt);
                if (socket != null)
                {
                    return;
                }
                p2pSocket = await P2PTcpSocket.TestP2P(p2pId, id);
                if (p2pSocket != null)
                {
                    //打洞成功
                    Logger.LogInformation($"{DateTime.Now:F} {Client.Options.ClientId} {p2pSocket.P2PTypeName} 打洞{rip}成功 ");
                    p2pSocket.ReceiveDataAsync += P2pSocket_ReceiveDataAsync;
                }

            }, TaskCreationOptions.LongRunning);
            return ValueTask.CompletedTask;
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            tunDriveService.StartAsync(cancellationToken);
            return base.StartAsync(cancellationToken);
        }
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            tunDriveService.StopAsync(cancellationToken);
            return base.StopAsync(cancellationToken);
        }

        private async Task Client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            Logger.LogWarning($"{DateTime.Now:F}{Client.Options.ClientId}  离线 {arg.ReasonString}");
            await Task.Delay(1000);
            await ClientConnectAsync();
        }

        protected virtual MqttClientOptions InitMQTTClient()
        {
            var options = new MQTTnet.Client.MqttClientOptionsBuilder();
            options.WithClientId(_configuration.GetValue<string>("hostCode"));
            options.WithTcpServer(_host, _port);
            options.WithKeepAlivePeriod(TimeSpan.FromSeconds(10));
            options.WithCleanSession(true);
            options.WithSessionExpiryInterval(60);
            options.WithCredentials("qcxt", "qcxt.comA1+");
            options.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);//使用mqtt5.0版本
            StringBuilder builder = new StringBuilder();
            foreach (var item in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && (item.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet))
                {
                    foreach (var ipItem in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ipItem.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            builder.Append(ipItem.Address);
                            builder.Append(',');
                        }
                    }
                }
            }
            options.WithUserProperty("ips", builder.Remove(builder.Length - 1, 1).ToString());
            options.WithUserProperty("systemName", Environment.OSVersion.ToString());
            return options.Build();
        }
        /// <summary>
        /// 父级mqtt客户端链接
        /// </summary>
        /// <returns></returns>
        public virtual async Task ClientConnectAsync()
        {
            try
            {
                var result = await Client.ConnectAsync(InitMQTTClient());
                tunDriveService.LocalIP = result.AuthenticationData.GetString();
                Logger.LogInformation($"{DateTime.Now:F} 上级节点 {Client.Options.ChannelOptions} 【{Client.Options.ClientId}】  上线成功 {result} IP地址{tunDriveService.LocalIP}");
                await Client.SubscribeAsync($"/sys/{tunDriveService.LocalIP}/+");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"{DateTime.Now:F} {Client.Options.ClientId} 上线异常  {ex.Message} ");
                return;
            }
        }
        public IMqttClient Client => _mqttClient;

        public ILogger Logger => _logger;

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {

            return ClientConnectAsync();
        }
        protected virtual Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic.EndsWith("/p2p"))
            {
                //执行打洞
                _ = System.Threading.Tasks.Task.Factory.StartNew(() => OnP2PAsync(arg.ApplicationMessage), TaskCreationOptions.LongRunning);
            }
            return Task.CompletedTask;
        }
        protected virtual async Task OnP2PAsync(MqttApplicationMessage message)
        {
            var ip = message.PayloadSegment[0] << 24 | message.PayloadSegment[1] << 16 | message.PayloadSegment[2] << 8 | message.PayloadSegment[3];// string.Join(".", message.PayloadSegment.Take(4));
            var id = message.PayloadSegment.Slice(0, 4).Concat(tunDriveService.LocalIPBytes).ToArray().ToUInt64();
            Logger.LogInformation($"{DateTime.Now:F} {Client.Options.ClientId} 准备 打洞{id.ToIP()} ");
            if (P2PSocket.GetP2PPSocket(ip, tunDriveService.LocalIPInt) == null)
            {
                var p2pSocket = await P2PUDPSocket.TestP2P(id, message.PayloadSegment.Skip(4).ToArray().ToUInt64());
                if (p2pSocket != null)
                {
                    //打洞成功
                    Logger.LogInformation($"{DateTime.Now:F} {Client.Options.ClientId} 打洞{p2pSocket.P2PTypeName} {id.ToIP()}成功 ");
                    p2pSocket.ReceiveDataAsync += P2pSocket_ReceiveDataAsync;
                    return;
                }
            }
            if (P2PSocket.GetP2PPSocket(ip, tunDriveService.LocalIPInt) == null)
            {
                var p2pSocket = await P2PTcpSocket.TestP2P(id, message.PayloadSegment.Skip(4).ToArray().ToUInt64());
                if (p2pSocket != null)
                {
                    //打洞成功
                    Logger.LogInformation($"{DateTime.Now:F} {Client.Options.ClientId} 打洞{p2pSocket.P2PTypeName} {id.ToIP()}成功 ");
                    p2pSocket.ReceiveDataAsync += P2pSocket_ReceiveDataAsync;
                    return;
                }
            }
        }

        private ValueTask P2pSocket_ReceiveDataAsync(Memory<byte> arg)
        {
            return tunDriveService.WriteAsync(arg);
        }
    }
}
