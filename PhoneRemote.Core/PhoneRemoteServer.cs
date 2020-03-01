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
		private readonly IMessageSerializer<TMessage> messageSerializer;
		private readonly IPEndPoint endpoint;
		private readonly ILogger<PhoneRemoteServer<TMessage>> logger;

		private Socket server;
		private Socket remoteSocket;

		public PhoneRemoteServer(IMessageSerializer<TMessage> messageSerializer, IPEndPoint localEndpoint, ILogger<PhoneRemoteServer<TMessage>> logger)
		{
			this.messageSerializer = messageSerializer;
			this.endpoint = localEndpoint;
			this.logger = logger;
		}

		public async Task WaitForConnectionAsync(CancellationToken cancellationToken)
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
						this.server.Listen(1);
						var remoteSocketTask = this.server.AcceptAsync();
						await Task.WhenAny(remoteSocketTask, tcs.Task);

						if (remoteSocketTask.Status != TaskStatus.RanToCompletion)
						{
							this.logger.LogError(remoteSocketTask.Exception, "Failed to accept incoming connection. Retrying");
							continue;
						}

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

				this.CloseExistingRemoteSocket();
				this.CloseServerSocket();
			}
			finally
			{
				registration.Dispose();
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
					if (!cancellationToken.IsCancellationRequested)
					{
						this.logger.LogWarning($"Trying to reset connection..");
						this.CloseExistingRemoteSocket();
						await WaitForConnectionAsync(cancellationToken);
						continue;
					}
				}

				yield return message;
			}
		}

		private void CloseExistingRemoteSocket()
		{
			try
			{
				this.CloseSocket(this.remoteSocket);
			}
			finally
			{
				this.remoteSocket = null;
			}
		}

		private void CloseServerSocket()
		{
			try
			{
				this.CloseSocket(this.server);
			}
			finally
			{
				this.server = null;
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
