using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PhoneRemote.Core;
using PhoneRemote.Protobuf;
using PhoneRemote.Protobuf.ProtoModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneRemote.AndroidClient.Client
{
	public class PhoneRemoteClientEventArgs : EventArgs
	{
		public ServiceDiscoveryMessage Message { get; set; }

		public bool IsConnected { get; set; }
	}

	public class ClientFacade
	{
		private CancellationTokenSource serverDiscoveryCancel;
		private Task serverDiscoveryTask;

		private CancellationTokenSource serverConnectCancel;
		private Task serverConnectTask;

		private ServiceDiscoveryMessage discoveryMessage;

		private readonly ILogger<PhoneRemoteClient<IMessage>> logger;
		private readonly PhoneRemoteClient<IMessage> tcpClient;
		private readonly CancellationTokenSource tokenSource;

		public event EventHandler<PhoneRemoteClientEventArgs> ServerDiscovery;
		public event EventHandler<PhoneRemoteClientEventArgs> ConnectionStateChange;

		public ClientFacade()
		{
			this.tokenSource = new CancellationTokenSource();
			this.logger = LoggerFactory.Create(x => x.ClearProviders()).CreateLogger<PhoneRemoteClient<IMessage>>();
			this.tcpClient = new PhoneRemoteClient<IMessage>(new ProtobufMessageSerializer(), false, this.logger);
		}

		public bool IsConnected { get; set; }

		public bool IsConnecting { get; set; }

		public void DiscoverAndConnect()
		{
			this.ServerDiscovery += OnServerDiscovery;
			this.tcpClient.ConnectionChange += TcpClient_ConnectionChange;
			this.StartDiscover();
		}

		public void Send(IMessage message)
		{
			if (this.IsConnected)
			{
				_ = this.tcpClient.SendAsync(message, this.tokenSource.Token);
			}
		}

		private void TcpClient_ConnectionChange(object sender, ConnectionChangeEventArgs e)
		{
			this.IsConnected = e.IsConnected;
			this.IsConnecting = e.IsConnecting;

			if (this.IsConnected)
			{
				this.StopConnecting();
			}
			else if (!this.IsConnecting)
			{
				this.StartConnecting(this.discoveryMessage);
			}

			this.ConnectionStateChange?.Invoke(this, new PhoneRemoteClientEventArgs { IsConnected = this.IsConnected, Message = this.discoveryMessage });
		}

		private void OnServerDiscovery(object sender, PhoneRemoteClientEventArgs e)
		{
			this.discoveryMessage = e.Message;

			this.StopDiscover();
			this.StartConnecting(e.Message);
		}

		private Task StartConnecting(ServiceDiscoveryMessage message)
		{
			this.serverConnectCancel = CancellationTokenSource.CreateLinkedTokenSource(this.tokenSource.Token);

			var token = serverConnectCancel.Token;

			this.serverConnectTask = Task.Run(async () =>
			{
				while (!token.IsCancellationRequested)
				{
					try
					{
						await this.tcpClient.ConnectAsync(new System.Net.IPEndPoint(message.Address, message.Port), token);
						return;
					}
					catch
					{
					}

					await Task.Delay(1000, token);
				}

				throw new TaskCanceledException();
			});

			return this.serverConnectTask;
		}

		private Task StartDiscover()
		{
			this.serverDiscoveryCancel = CancellationTokenSource.CreateLinkedTokenSource(this.tokenSource.Token);

			var token = serverDiscoveryCancel.Token;

			this.serverDiscoveryTask = Task.Run(async () =>
			{
				while (!token.IsCancellationRequested)
				{
					try
					{
						var message = await this.tcpClient.DiscoverServerAsync<ServiceDiscoveryMessage>(this.tokenSource.Token);

						this.ServerDiscovery?.Invoke(this, new PhoneRemoteClientEventArgs { IsConnected = this.IsConnected, Message = message });
					}
					catch (Exception ex)
					{
					}

					await Task.Delay(5000, token);
				}

				throw new TaskCanceledException();
			});

			return this.serverDiscoveryTask;
		}

		private void StopDiscover()
		{
			this.serverDiscoveryCancel.Cancel();
			this.serverDiscoveryCancel.Dispose();

			this.serverDiscoveryCancel = null;
			this.serverDiscoveryTask = null;
		}

		private void StopConnecting()
		{
			this.serverConnectCancel.Cancel();
			this.serverConnectCancel.Dispose();

			this.serverConnectCancel = null;
			this.serverConnectTask = null;
		}
	}
}