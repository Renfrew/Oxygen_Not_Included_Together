using System;

namespace ONI_United.Networking.Sessions.States
{
	public enum ClientState
	{
		Disconnected,
		Connecting,
		Connected,
		LoadingWorld,
		InGame
	}

	[Flags]
	public enum ClientReadyState
	{
		Ready,
		Unready,
		Loading
	}
}
