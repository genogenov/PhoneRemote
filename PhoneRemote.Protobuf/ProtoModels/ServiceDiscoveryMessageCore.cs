using PhoneRemote.Core;
using System.Net;

namespace PhoneRemote.Protobuf.ProtoModels
{
	public partial class ServiceDiscoveryMessage : IServiceDiscoveryMessage
	{
		public IPAddress Address => new IPAddress(this.IpAddress.Span);
	}
}
