using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneRemote.Core
{
	public class PhoneRemoteServiceBroadcaster<TServiceDiscoveryMessage>
	{
		private const int ServiceDiscoveryPort = 8766;

		private readonly ILogger logger;
		private readonly object syncRoot = new object();
		private readonly IMessageSerializer<TServiceDiscoveryMessage> messageSerializer;
		private readonly TServiceDiscoveryMessage discoveryMessage;
		private Socket udpClient;
		private CancellationTokenSource cts;
		private TaskCompletionSource<int> cancellationTask;

		private bool isShutDown = false;

		public PhoneRemoteServiceBroadcaster(IMessageSerializer<TServiceDiscoveryMessage> messageSerializer, TServiceDiscoveryMessage discoveryMessage, ILogger<PhoneRemoteServiceBroadcaster<TServiceDiscoveryMessage>> logger)
		{
			this.cts = new CancellationTokenSource();
			this.messageSerializer = messageSerializer;
			this.discoveryMessage = discoveryMessage;
			this.logger = logger;
		}

		public void StartListenForClients()
		{
			lock (this.syncRoot)
			{
				this.cts = new CancellationTokenSource();
				this.cancellationTask = new TaskCompletionSource<int>();
				this.cts.Token.Register(() => this.cancellationTask.SetCanceled());
				this.InitSocket();
				_ = this.ListenForConnections();

				this.logger.LogInformation($"Start listening for connections on {this.udpClient.LocalEndPoint}");
			}
		}

		public void StopListening()
		{
			lock (this.syncRoot)
			{
				this.cts?.Cancel();

				if (this.udpClient != null)
				{
					try
					{
						this.udpClient.Close();
						this.cts.Dispose();
					}
					finally
					{
						this.udpClient = null;
						this.cts = null;

						this.logger.LogInformation($"Stopped listening for connections.");
					}
				}
			}
		}

		private async Task ListenForConnections()
		{
			while (true)
			{
				try
				{		
					var buffer = new byte[10];
					var task = this.udpClient.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, ServiceDiscoveryPort));
					var socket = await Task.WhenAny(task, this.cancellationTask.Task).ConfigureAwait(false);
					if (isShutDown)
					{
						return;
					}

					_ = OnConnectionAccepted(task.Result);
				}
				catch (Exception ex)
				{
					if (isShutDown || this.cancellationTask.Task.IsCanceled)
					{
						this.logger.LogWarning(ex, "Broadcaster shutting down.");
						return;
					}
				}
			}
		}

		private Task OnConnectionAccepted(SocketReceiveFromResult remote)
		{
			this.logger.LogInformation($"Received connection from {remote.RemoteEndPoint}");

			var endpoint = remote.RemoteEndPoint;

			var payload = this.messageSerializer.Serialize(this.discoveryMessage);

			return Task.WhenAny(this.udpClient.SendToAsync(payload, SocketFlags.None, endpoint), this.cancellationTask.Task);
		}

		private void InitSocket()
		{
			if (this.udpClient == null)
			{
				this.udpClient = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				this.udpClient.EnableBroadcast = true;
				this.udpClient.Bind(new IPEndPoint(IPAddress.Any, ServiceDiscoveryPort));
			}
		}
	}
}
