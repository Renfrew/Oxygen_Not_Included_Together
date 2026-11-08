using System.Linq;
using ONI_United.Networking;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transports.LiteNetLib;
using ONI_United.Networking.Transports.Steamworks;
using ONI_United.DebugTools;

namespace ONI_United.DebugTools.UnitTests
{
	public static class TransportTests
	{
		[UnitTest(name: "Transport server/client types match NetworkConfig", category: "Transport")]
		public static UnitTestResult TransportMatchesConfig()
		{
			var transport = NetworkConfig.transport;
			var server = NetworkConfig.TransportServer;
			var client = NetworkConfig.TransportClient;

			if (server == null)
				return UnitTestResult.Fail("TransportServer is null");
			if (client == null)
				return UnitTestResult.Fail("TransportClient is null");

			switch (transport)
			{
				case NetworkConfig.NetworkTransport.STEAMWORKS:
					if (server is not SteamworksServerLegacy)
						return UnitTestResult.Fail($"transport=STEAMWORKS but server is {server.GetType().Name}");
					if (client is not SteamworksClientLegacy)
						return UnitTestResult.Fail($"transport=STEAMWORKS but client is {client.GetType().Name}");
					return UnitTestResult.Pass("STEAMWORKS config matches SteamworksServerLegacy/SteamworksClientLegacy");

				case NetworkConfig.NetworkTransport.LITENETLIB:
					if (server is not LiteNetLibServerLegacy)
						return UnitTestResult.Fail($"transport={transport} but server is {server.GetType().Name}");
					if (client is not LiteNetLibClientLegacy)
						return UnitTestResult.Fail($"transport={transport} but client is {client.GetType().Name}");
					return UnitTestResult.Pass($"{transport} config matches LiteNetLibServerLegacy/LiteNetLibClientLegacy");

				default:
					return UnitTestResult.Fail($"Unknown transport: {transport}");
			}
		}

		[UnitTest(name: "Connection stable (LAN)", category: "Transport")]
		public static UnitTestResult ConnectionStable()
		{
			if (!MultiplayerSession.InActiveSession)
				return UnitTestResult.Fail("Not in a multiplayer session");

			if (!NetworkConfig.IsLanConfig())
				return UnitTestResult.Fail("Stability check only implemented for LAN/LiteNetLib transport");

			if (MultiplayerSession.IsHost)
			{
				var server = NetworkConfig.TransportServer as LiteNetLibServerLegacy;
				if (server == null || !server.IsRunning)
					return UnitTestResult.Fail("LiteNetLib Server is not running");

				int connected = server.ConnectedClientCount;
				if (connected == 0)
					return UnitTestResult.Fail("No active connections on server");

				return UnitTestResult.Pass($"Server running with {connected} active connection(s)");
			}

			var client = NetworkConfig.TransportClient as LiteNetLibClientLegacy;
			if (client == null)
				return UnitTestResult.Fail("LiteNetLib Client instance is null");
			if (!client.IsConnected)
				return UnitTestResult.Fail("LiteNetLib Client is not connected");

			int ping = client.GetPing();
			return UnitTestResult.Pass($"Client connected, ping = {ping} ms");
		}
	}
}
