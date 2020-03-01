using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneRemote.Core
{
	public class PhoneRemoteServer<TMessage>
	{
		private readonly object syncRoot = new object();
		private Socket server;
		private readonly IMessageSerializer<TMessage> messageSerializer;
		private readonly IPEndPoint endpoint;
		private readonly ILogger<PhoneRemoteServer<TMessage>> logger;
		private Socket remoteSocket;

		public PhoneRemoteServer(IMessageSerializer<TMessage> messageSerializer, IPEndPoint endpoint, ILogger<PhoneRemoteServer<TMessage>> logger)
		{
			this.messageSerializer = messageSerializer;
			this.endpoint = endpoint;
			this.logger = logger;
		}

		public async Task WaitForConnectionAsync(CancellationToken cancellationToken)
		{
			while (true)
			{
				var tcs = new TaskCompletionSource<int>();
				try
				{
					cancellationToken.Register(() => tcs.SetCanceled());

					this.InitSocket();
					this.server.Listen(1);
					var remoteSocketTask = this.server.AcceptAsync();
					await Task.WhenAny(remoteSocketTask, tcs.Task);

					this.remoteSocket = remoteSocketTask.Result;
					this.SetKeepAlive();
					this.logger.LogInformation($"Got connection from {remoteSocket.RemoteEndPoint}");

					return;
				}
				catch (Exception ex)
				{
					if (!cancellationToken.IsCancellationRequested)
					{
						this.logger.LogError(ex, "Failed to accept incoming connection. Retrying");
					}
				}
			}
		}

		public async IAsyncEnumerable<T> WaitForMessageAsync<T>([EnumeratorCancellation]CancellationToken cancellationToken) where T : TMessage, new()
		{
			T message = default;

			while (true)
			{
				try
				{
					message = await this.messageSerializer.DeserializeAsync<T>(this.remoteSocket, cancellationToken);
				}
				catch (Exception ex)
				{
					this.logger.LogError(ex, $"Failed to receive data from {this.remoteSocket?.RemoteEndPoint}");
					if (!cancellationToken.IsCancellationRequested && this.server != null)
					{
						if (!this.server.Connected)
						{
							this.logger.LogWarning($"Trying to reset connection..");
							this.CloseExistingRemoteSocket();
							await WaitForConnectionAsync(cancellationToken);
							continue;
						}
					}
				}

				yield return message;
			}
		}

		private async Task TryFixConnection(CancellationToken cancellationToken)
		{
			try
			{
				var tcs = new TaskCompletionSource<int>();

				cancellationToken.Register(() => tcs.SetCanceled());

				var socketTask = this.server.AcceptAsync(this.remoteSocket);
				await Task.WhenAny(socketTask, tcs.Task);

				this.remoteSocket = socketTask.Result;

				this.logger.LogInformation($"Fixed connection with {remoteSocket.RemoteEndPoint}");
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Failed to fix incoming connection.");
				this.remoteSocket = null;

				this.CloseExistingRemoteSocket();

				if (!cancellationToken.IsCancellationRequested)
				{
					await WaitForConnectionAsync(cancellationToken);
				}
			}
		}

		private void CloseExistingRemoteSocket()
		{
			try
			{
				this.remoteSocket?.Close();
			}
			catch (Exception ex)
			{
				this.logger.LogError(ex, "Error while trying to close existing connection");
			}
			finally
			{
				this.remoteSocket = null;
			}
		}

		//public async Task<(int x, int y)> WaitForCursorAsync()
		//{
		//	var buff = new byte[8];

		//	int read = 0;

		//	while (read < buff.Length)
		//	{
		//		read += await this.remoteSocket.ReceiveAsync(buff, read, buff.Length - read);
		//	}

		//	return (BitConverter.ToInt32(buff.AsSpan(0..4)), BitConverter.ToInt32(buff.AsSpan(4..8)));
		//}

		private void SetKeepAlive()
		{
			int size = sizeof(uint);
			uint on = 1;
			uint keepAliveInterval = 100;
			uint retryInterval = 100; //If no response, resend every second.
			byte[] inArray = new byte[size * 3];
			Array.Copy(BitConverter.GetBytes(on), 0, inArray, 0, size);
			Array.Copy(BitConverter.GetBytes(keepAliveInterval), 0, inArray, size, size);
			Array.Copy(BitConverter.GetBytes(retryInterval), 0, inArray, size * 2, size);
			this.remoteSocket.IOControl(IOControlCode.KeepAliveValues, inArray, null);

			this.server.IOControl(IOControlCode.KeepAliveValues, inArray, null);

		}

		private void InitSocket()
		{
			lock (this.syncRoot)
			{
				if (this.server == null)
				{
					this.server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					server.NoDelay = true;
					server.Bind(this.endpoint);
				}
			}
		}
	}
}
