using System;

namespace ONI_United.Networking.Sessions.States
{
	[Flags]
	public enum ClientReadyState
	{
		Ready,
		Unready,
		Loading
	}
}
