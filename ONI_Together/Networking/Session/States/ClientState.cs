using System;

namespace ONI_United.Networking.Session.States
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
