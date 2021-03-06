﻿using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhoneRemote.Core;
using PhoneRemote.Interop.Windows;
using PhoneRemote.Protobuf;
using PhoneRemote.Protobuf.ProtoModels;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneRemote.ServerConsole
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
			var serverLogger = services.GetRequiredService<ILogger<PhoneRemoteServer<IMessage>>>();

			var serializer = new ProtobufMessageSerializer();
			var broadcaster = new PhoneRemoteServiceBroadcaster<IMessage>(serializer, serviceDescriptor, logger);
			var server = new PhoneRemoteServer<IMessage>(serializer, new IPEndPoint(new IPAddress(serviceDescriptor.IpAddress.Span), serviceDescriptor.Port), serverLogger);

			broadcaster.StartListenForClients();

			Console.WriteLine("Listening on local..");
			await server.WaitForConnectionAsync(CancellationToken.None);
			Console.WriteLine("Got connection. Listening for messages..");

			var cursor = CursorUtils.GetCursorInfo();

			await foreach (var action in server.WaitForMessageAsync<CursorAction>(CancellationToken.None))
			{
				//Console.SetCursorPosition(0, 2);

				//cursor.ptScreenPos.x += pos.DX;
				//cursor.ptScreenPos.y += pos.DY;

				//Console.WriteLine($"dx,dy={pos.DX},{pos.DY}");

				CursorUtils.DispatchMouseEvent(action);
			}
		}
	}
}
