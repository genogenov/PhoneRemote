using System.Net;

namespace PhoneRemote.Core
{
	public interface IServiceDiscoveryMessage
	{
		IPAddress IpAddress { get; }

		int Port { get;}

		string ServerName { get; }
	}
}
