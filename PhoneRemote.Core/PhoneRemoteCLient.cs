using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneRemote.Core
{
	public class PhoneRemoteClient<TMessage>
	{
		private Socket client;

		private readonly object syncRoot = new object();
		private readonly IMessageSerializer<TMessage> messageSerializer;
		private readonly ILogger<PhoneRemoteClient<TMessage>> logger;
		private readonly bool activelyRepairConnection;

		private Task establishConnectionTask;
		private IPEndPoint remoteEndpoint;

		public event EventHandler<ConnectionChangeEventArgs> ConnectionChange;

		public PhoneRemoteClient(IMessageSerializer<TMessage> messageSerializer, bool activelyRepairConnection, ILogger<PhoneRemoteClient<TMessage>> logger)
		{
			this.messageSerializer = messageSerializer;
			this.activelyRepairConnection = activelyRepairConnection;
			this.logger = logger;
		}

		public bool IsConnected { get; private set; }

		public bool IsConnecting { get; private set; }

		public async Task<TDiscoveryMessage> DiscoverServerAsync<TDiscoveryMessage>(CancellationToken cancellationToken) where TDiscoveryMessage : IServiceDiscoveryMessage, TMessage, new()
		{
			var tcs = new TaskCompletionSource<int>();
			var registration = cancellationToken.Register(() => tcs.SetCanceled());

			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						using (var udpClient = new UdpClient())
						{
							udpClient.EnableBroadcast = true;

							// for emulator
							//var broadcastAddress = IPAddress.Parse("10.0.2.2");
							var broadcastAddress = IPAddress.Broadcast;

							await udpClient.SendAsync(new byte[0], 0, new IPEndPoint(broadcastAddress, 8766)).ConfigureAwait(false);

							var receiveTask = udpClient.ReceiveAsync();
							await Task.WhenAny(receiveTask, Task.Delay(5000), tcs.Task).ConfigureAwait(false);
							if (receiveTask.Status != TaskStatus.RanToCompletion)
							{
								this.logger.LogWarning("Could not receive response from broadcast in 5s. Retrying.");
								continue;
							}

							var res = receiveTask.Result;

							return this.messageSerializer.Deserialize<TDiscoveryMessage>(res.Buffer, cancellationToken);
						}
					}
					catch (Exception ex)
					{
						this.logger.LogError(ex, "Error while resolving server.");
					}
				}

				throw new TaskCanceledException();
			}
			finally
			{
				registration.Dispose();
			}
		}

		public Task ConnectAsync(IPEndPoint iPEndPoint, CancellationToken cancellationToken)
		{
			return this.StartEstablishConnection(iPEndPoint, cancellationToken);
		}

		public async Task SendAsync(TMessage payload, CancellationToken cancellationToken)
		{
			var data = this.messageSerializer.Serialize(payload);

			if (!this.IsConnected && this.activelyRepairConnection)
			{
				_ = this.StartEstablishConnection(this.remoteEndpoint, CancellationToken.None);
				return;
			}

			try
			{
				int bytesSend = await this.client.SendAsync(data, SocketFlags.None);
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, $"Failed to send data to {this.remoteEndpoint}");
				if (!cancellationToken.IsCancellationRequested)
				{
					this.logger.LogWarning($"Trying to reset connection..");
					this.CloseSocket();
					if (this.activelyRepairConnection)
					{
						_ = this.StartEstablishConnection(this.remoteEndpoint, CancellationToken.None);
					}

					this.ConnectionChange?.Invoke(this, new ConnectionChangeEventArgs { IsConnected = this.IsConnected, IsConnecting = this.IsConnecting });
				}
			}
		}

		private void InitSocket()
		{
			lock (this.syncRoot)
			{
				if (this.client == null)
				{
					this.client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					this.client.NoDelay = true;
					this.client.SendTimeout = 5000;
					this.client.ReceiveTimeout = 5000;
				}
			}
		}

		private Task StartEstablishConnection(IPEndPoint iPEndPoint, CancellationToken cancellationToken)
		{
			if (this.establishConnectionTask == null)
			{
				lock (this.syncRoot)
				{
					if (this.establishConnectionTask == null)
					{
						this.establishConnectionTask = EstablishConnection(iPEndPoint, cancellationToken).ContinueWith(async t =>
						{
							await Task.Yield();

							this.IsConnecting = false;
							if (t.Status != TaskStatus.RanToCompletion || !this.IsConnected)
							{
								this.establishConnectionTask = null;
							}

							this.ConnectionChange?.Invoke(this, new ConnectionChangeEventArgs { IsConnected = this.IsConnected, IsConnecting = this.IsConnecting });
						});

						this.IsConnecting = true;
					}
				}
			}

			return this.establishConnectionTask;
		}

		private async Task EstablishConnection(IPEndPoint iPEndPoint, CancellationToken cancellationToken)
		{
			var tcs = new TaskCompletionSource<int>();
			var registration = cancellationToken.Register(() => tcs.SetCanceled());

			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					try
					{
						this.InitSocket();
						var remoteSocketTask = this.client.ConnectAsync(iPEndPoint);
						await Task.WhenAny(remoteSocketTask, tcs.Task).ConfigureAwait(false);

						if (remoteSocketTask.Status != TaskStatus.RanToCompletion)
						{
							this.logger.LogError(remoteSocketTask.Exception, "Failed to connect to remote server. Retrying");
							continue;
						}

						this.SetKeepAlive();
						this.logger.LogInformation($"Connected to {iPEndPoint}");
						this.remoteEndpoint = iPEndPoint;
						this.IsConnected = true;
						return;
					}
					catch (Exception ex)
					{
						if (!cancellationToken.IsCancellationRequested)
						{
							this.logger.LogError(ex, "Failed to connect to remote server. Retrying");
						}
					}
				}

				this.CloseSocket();
			}
			finally
			{
				registration.Dispose();
			}
		}

		private void CloseSocket(Socket socket)
		{
			try
			{
				socket?.Close();
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Error while trying to close existing connection");
			}

			try
			{
				socket?.Dispose();
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Error while trying to close existing connection");
			}
		}

		private void CloseSocket()
		{
			lock (this.syncRoot)
			{
				this.IsConnected = false;

				try
				{
					this.CloseSocket(this.client);
				}
				finally
				{
					this.client = null;
					this.establishConnectionTask = null;
				}
			}
		}

		private void SetKeepAlive()
		{
			int size = sizeof(uint);
			uint on = 1;
			uint keepAliveInterval = 5000; // send after 1s of inactivity
			uint retryInterval = 5000; //If no response, resend every second.
			byte[] inArray = new byte[size * 3];
			Array.Copy(BitConverter.GetBytes(on), 0, inArray, 0, size);
			Array.Copy(BitConverter.GetBytes(keepAliveInterval), 0, inArray, size, size);
			Array.Copy(BitConverter.GetBytes(retryInterval), 0, inArray, size * 2, size);
			this.client.IOControl(IOControlCode.KeepAliveValues, inArray, null);
		}
	}
}
