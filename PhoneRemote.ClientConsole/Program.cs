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

			var logger = services.GetRequiredService<ILogger<PhoneRemoteServiceBroadcaster<IMessage>>>();

			var rnd = new Random();

			var tcpClient = new PhoneRemoteClient<IMessage>(new ProtobufMessageSerializer());
			var endpoint = await tcpClient.DiscoverServerAsync<ServiceDiscoveryMessage>(CancellationToken.None);

			await tcpClient.ConnectAsync(new IPEndPoint(endpoint.IpAddress, endpoint.Port));

			while (true)
			{
				await tcpClient.SendAsync(new CursorPosition { DX = rnd.Next(), DY = rnd.Next() });
			}
		}
	}
}
