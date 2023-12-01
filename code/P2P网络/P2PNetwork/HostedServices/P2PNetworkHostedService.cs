using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;

namespace P2PNetwork.HostedServices
{
    /// <summary>
    /// p2p 网络服务
    /// </summary>
    public class P2PNetworkHostedService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IMqttClient _mqttClient;
        public IConfiguration Configuration => _configuration;
        public IMqttClient Client => _mqttClient;
        public P2PNetworkHostedService(IConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;

            //图省事，直接用mqtt作为主消息体了
            _mqttClient = new MQTTnet.MqttFactory().CreateMqttClient();
        }
        private byte[] _localIPBytes = null;
        public virtual byte[] LocalIPBytes
        {
            get { return _localIPBytes; }
            private set
            {
                _localIPBytes = value;
                LocalIPInt = value.ToInt32();
                LocalIP = string.Join(".", _localIPBytes);
            }
        }
        public virtual int LocalIPInt { get; private set; }
        public virtual string LocalIP { get; private set; }
        public ILogger Logger => _logger;
        protected virtual MqttClientOptions InitMQTTClient()
        {
            var options = new MQTTnet.Client.MqttClientOptionsBuilder();
            options.WithClientId(Configuration.GetValue<string>("p2p:mqtt:id", Guid.NewGuid().ToString("N")));//mqtt唯一id
            options.WithTcpServer(Configuration.GetValue<string>("p2p:mqtt:host", "p2p.hnlyf.com"), Configuration.GetValue<int>("p2p:mqtt:port", 62001));
            options.WithKeepAlivePeriod(TimeSpan.FromSeconds(10));
            options.WithCleanSession(true);
            options.WithSessionExpiryInterval(60);
            options.WithCredentials(Configuration.GetValue<string>("p2p:mqtt:userName", "hnlyf"), Configuration.GetValue<string>("p2p:mqtt:password", "p2p.hnlyf.com"));
            options.WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500);//使用mqtt5.0版本
            List<string> addressList = new List<string>();
            //把本机网络报告给服务器
            foreach (var item in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && (item.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet || item.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211))
                {
                    foreach (var ipItem in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ipItem.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            addressList.Add(ipItem.Address.ToString());
                        }
                    }
                }
            }
            options.WithUserProperty("ips", string.Join(",", addressList));
            options.WithUserProperty("systemName", Environment.OSVersion.ToString());
            options.WithUserProperty("name", Configuration.GetValue<string>("name", Environment.MachineName));
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
                LocalIPBytes = result.AuthenticationData;//登录后返回分配的IP地址，4个字节
                Logger.LogInformation($"{DateTime.Now:F} 上级节点 {Client.Options.ChannelOptions} 【{Client.Options.ClientId}】  上线成功 分配IP地址：{LocalIP}");
                await Client.SubscribeAsync($"sys/{LocalIP}/+");//订阅所有发送给本机的消息
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"{DateTime.Now:F} {Client.Options.ClientId} 上线异常  {ex.Message} ");
                return;
            }
        }
        /// <summary>
        /// mqtt离线，离线后网卡不受影响，但是会影响打洞，只能udp转发
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private async Task Client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            Logger.LogWarning($"{DateTime.Now:F}{Client.Options.ClientId}  离线 {arg.ReasonString}");
            await Task.Delay(1000);
            await ClientConnectAsync();
        }
        protected virtual Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic.EndsWith("/p2p"))
            {
                //执行打洞
                _ = System.Threading.Tasks.Task.Factory.StartNew(() => OnP2PAsync(arg.ApplicationMessage), TaskCreationOptions.LongRunning);
            }
            //else if (arg.ApplicationMessage.Topic.EndsWith("/tcp"))
            //{
            //    //执行打洞
            //    _ = System.Threading.Tasks.Task.Factory.StartNew(() => OnTcpAsync(arg.ApplicationMessage), TaskCreationOptions.LongRunning);
            //}
            return Task.CompletedTask;
        }
        /// <summary>
        /// 需要p2p打洞
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected virtual async Task OnP2PAsync(MqttApplicationMessage message)
        {
            var ip = message.PayloadSegment.ToInt32();//需要链接的ip
            var p2pType = message.PayloadSegment[4];//打洞类型，1 udp 、2 tcp 服务端、3 tcp客户端
            var p2pId = message.PayloadSegment.ToUInt64(6);//本次打洞id
        }
        /// <summary>
        /// 发送p2p请求
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public virtual ValueTask OnSendP2P(int ip)
        {

        }
        protected virtual async ValueTask OnSendUdpP2P(string ip, long p2pId)
        {
            Logger.LogInformation($"{DateTime.Now:F} {Client.Options.ClientId} 尝试 UDP 打洞{LocalIP} -> {ip}");
            await Client.PublishBinaryAsync($"sys/{ip}/p2p", LocalIPBytes.Concat(new byte[] { 1 }).Concat(p2pId.ToBytes()).ToArray());
        }
        protected virtual async ValueTask OnSendTcpServerP2P(string ip, long p2pId)
        {
            Logger.LogInformation($"{DateTime.Now:F} {Client.Options.ClientId} 尝试 TCP 打洞 {LocalIP}(服务端) -> {ip}(客户端)");
            await Client.PublishBinaryAsync($"sys/{ip}/p2p", LocalIPBytes.Concat(new byte[] { 2 }).Concat(p2pId.ToBytes()).ToArray());
        }
        protected virtual async ValueTask OnSendTcpClientP2P(string ip, long p2pId)
        {
            Logger.LogInformation($"{DateTime.Now:F} {Client.Options.ClientId} 尝试 TCP 打洞 {LocalIP}(客户端) -> {ip}(服务端)");
            await Client.PublishBinaryAsync($"sys/{ip}/p2p", LocalIPBytes.Concat(new byte[] { 3 }).Concat(p2pId.ToBytes()).ToArray());
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}
