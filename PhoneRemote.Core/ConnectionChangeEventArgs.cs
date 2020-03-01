using System;

namespace PhoneRemote.Core
{
	public class ConnectionChangeEventArgs : EventArgs
	{
		public bool IsConnected { get; set; }

		public bool IsConnecting { get; set; }
	}
}
