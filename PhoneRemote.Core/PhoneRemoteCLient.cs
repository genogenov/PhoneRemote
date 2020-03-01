using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneRemote.Core
{
	public class PhoneRemoteClient<TMessage>
	{
		private UdpClient udpClient;
		private TcpClient tcpClient;

		private readonly IMessageSerializer<TMessage> messageSerializer;

		public PhoneRemoteClient(IMessageSerializer<TMessage> messageSerializer)
		{
			this.messageSerializer = messageSerializer;
		}

		public async Task<IServiceDiscoveryMessage> DiscoverServerAsync<TDiscoveryMessage>(CancellationToken cancellationToken) where TDiscoveryMessage : IServiceDiscoveryMessage, TMessage, new()
		{
			var payload = Encoding.UTF8.GetBytes("ping");

			while (true)
			{
				try
				{
					this.udpClient = new UdpClient();
					this.udpClient.EnableBroadcast = true;


					var broadcastAddress = IPUtils.GetBroadcastAddress(IPUtils.GetLocalIpAddress());

					await this.udpClient.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, 8766)).ConfigureAwait(false);

					var receiveTask = this.udpClient.ReceiveAsync();
					await Task.WhenAny(receiveTask, Task.Delay(5000)).ConfigureAwait(false);
					if (receiveTask.Status != TaskStatus.RanToCompletion)
					{
						continue;
					}

					var res = receiveTask.Result;

					return this.messageSerializer.Deserialize<TDiscoveryMessage>(res.Buffer, cancellationToken);
				}
				catch
				{
				}
				finally
				{
					this.udpClient.Close();
				}
			}
		}

		public async Task ConnectAsync(IPEndPoint iPEndPoint)
		{
			this.tcpClient = new TcpClient();

			this.tcpClient.Client.NoDelay = true;
			await this.tcpClient.ConnectAsync(iPEndPoint.Address, iPEndPoint.Port);
			this.tcpClient.Client.NoDelay = true;
		}

		public Task SendAsync(TMessage payload)
		{
			var stream = this.tcpClient.GetStream();

			var data = this.messageSerializer.Serialize(payload);

			return stream.WriteAsync(data, 0, data.Length);
		}

		public async Task SendCursorAsync(int x, int y)
		{
			var stream = this.tcpClient.GetStream();

			var payload = BitConverter.GetBytes(x).Concat(BitConverter.GetBytes(y)).ToArray();
			await stream.WriteAsync(payload, 0, payload.Length);
		}
	}
}
