using PhoneRemote.Core;
using System.Net;

namespace PhoneRemote.Protobuf.ProtoModels
{
	public partial class ServiceDiscoveryMessage : IServiceDiscoveryMessage
	{
		IPAddress IServiceDiscoveryMessage.IpAddress => new IPAddress(this.IpAddress.Span);
	}
}
