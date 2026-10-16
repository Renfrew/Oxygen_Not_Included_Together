namespace ONI_United.Networking.Session.States
{
	public enum ClientEvent
	{
		BeginConnect = 1,
		CancelConnect = 2,
		ConnectionFailed = 3,
		TransportConnected = 4,
		WorldLoadStarted = 5,
		SynchronizationCompleted = 6,
		TransportDisconnected = 7
	}
}
