using System.Net;

namespace PhoneRemote.Core
{
	public interface IServiceDiscoveryMessage
	{
		IPAddress Address { get; }

		int Port { get;}

		string ServerName { get; }
	}
}
