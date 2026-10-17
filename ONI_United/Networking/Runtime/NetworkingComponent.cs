using ONI_United.Networking.Session.States;
using Shared.Profiling;
using UnityEngine;
using ONI_United.Networking;
using ONI_United.Networking.Lifecycle;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Runtime
{
	public class NetworkingComponent : MonoBehaviour
	{
		public static UnityTaskScheduler scheduler = new UnityTaskScheduler();

		/*
		 * TODO:
		 * Update this class now that we can have different relay types. This is not steam specific anymore
		 *
		 * **/

		private void Update()
		{
			using var _ = Profiler.Scope();

			scheduler.Tick();

			if (NetworkConfig.transport.Equals(NetworkConfig.NetworkTransport.STEAMWORKS))
			{
				if (!SteamManager.Initialized)
					return;
			}

            if (GameServer.State == ServerState.Started &&
                (MultiplayerSession.IsHost ||
                 NetworkConfig.transport == NetworkConfig.NetworkTransport.RIPTIDE))
			{
				GameServer.Update();
			}
			else
			{
				GameClient.Poll();

				if (MultiplayerSession.IsClient && MultiplayerSession.HostUserID.IsValid())
				{
					// Check for inactive transfers and request missing chunks
					ONI_United.Networking.Transfer.SaveChunkAssembler.CheckInactiveTransfers();
				}
			}
            NetworkConfig.TransportPacketSender.Flush();
        }

        private void OnApplicationQuit()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.InActiveSession)
				return;

			NetworkConfig.Stop();
		}
	}
}
