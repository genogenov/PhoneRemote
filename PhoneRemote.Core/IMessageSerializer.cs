using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneRemote.Core
{
	public interface IMessageSerializer<T>
	{
		byte[] Serialize(T payload);

		Task<TMessage> DeserializeAsync<TMessage>(Socket socket, CancellationToken cancellationToken) where TMessage : T, new();

		TMessage Deserialize<TMessage>(byte[] buffer, CancellationToken cancellationToken) where TMessage : T, new();
	}
}
