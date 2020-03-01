using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhoneRemote.Core;
using PhoneRemote.Protobuf;
using PhoneRemote.Protobuf.ProtoModels;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneRemote.ClientConsole
{
	class Program
	{
		static async Task Main(string[] args)
		{
			var services = new ServiceCollection()
				.AddLogging(x =>
				{
					x.ClearProviders();
					x.AddConsole();
				})
				.BuildServiceProvider();

			var serviceDescriptor = new ServiceDiscoveryMessage()
			{
				IpAddress = ByteString.CopyFrom(IPUtils.GetLocalIpAddress().GetAddressBytes()),
				Port = 8765,
				ServerName = Environment.MachineName
			};

			var s = new Socket(new SocketInformation());

			var logger = services.GetRequiredService<ILogger<PhoneRemoteServiceBroadcaster<IMessage>>>();
			var clientLogger = services.GetRequiredService<ILogger<PhoneRemoteClient<IMessage>>>();

			var rnd = new Random();

			var tcpClient = new PhoneRemoteClient<IMessage>(new ProtobufMessageSerializer(), true, clientLogger);
			var endpoint = await tcpClient.DiscoverServerAsync<ServiceDiscoveryMessage>(CancellationToken.None);

			await tcpClient.ConnectAsync(new IPEndPoint(endpoint.Address, endpoint.Port), CancellationToken.None);

			while (true)
			{
				await tcpClient.SendAsync(new CursorAction { DX = rnd.Next(-100, 100), DY = rnd.Next(-100, 100) }, CancellationToken.None);
			}
		}
	}
}
