using Google.Protobuf;
using PhoneRemote.Core;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneRemote.Protobuf
{
	public class ProtobufMessageSerializer : IMessageSerializer<IMessage>
	{
		private const int MaxMessageBytesLength = 10000;
		private const int MessageLengthHeaderBytes = 4;

		public TMessage Deserialize<TMessage>(byte[] buffer, CancellationToken cancellationToken) where TMessage : IMessage, new()
		{
			var message = new TMessage();

			var data = new byte[buffer.Length - MessageLengthHeaderBytes];
			Buffer.BlockCopy(buffer, 4, data, 0, data.Length);
			message.MergeFrom(data);

			return message;
		}

		public async Task<TMessage> DeserializeAsync<TMessage>(Socket socket, CancellationToken cancellationToken) where TMessage : IMessage, new()
		{
			var buff = new byte[MessageLengthHeaderBytes];

			int length = 0;

			while (length == 0)
			{
				int bytesReceived = await socket.ReceiveAsync(buff, SocketFlags.None);

				if (bytesReceived == 0)
				{
					throw new InvalidOperationException("Socket disconnected. 0 bytes received");
				}

				length = BitConverter.ToInt32(buff);
			}

			if (length > MaxMessageBytesLength)
			{
				throw new InvalidOperationException("Max message length exceeded");
			}

			buff = new byte[length];

			int read = 0;

			while (read < buff.Length)
			{
				read += await socket.ReceiveAsync(new Memory<byte>(buff, read, buff.Length - read), SocketFlags.None);
			}

			var message = new TMessage();

			message.MergeFrom(buff);

			return message;
		}

		public byte[] Serialize(IMessage payload)
		{
			var buffer = new byte[MessageLengthHeaderBytes + payload.CalculateSize()];
			var bytes = payload.ToByteArray();

			Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length), 0, buffer, 0, 4);
			Buffer.BlockCopy(bytes, 0, buffer, 4, bytes.Length);

			return buffer;
		}
	}
}
