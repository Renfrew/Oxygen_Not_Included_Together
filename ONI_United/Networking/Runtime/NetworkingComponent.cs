using ONI_United.Networking.Sessions.States;
using Shared.Profiling;
using UnityEngine;
using ONI_United.Networking;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Sessions;
using ONI_United.Networking.Transport.LiteNetLib;

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

            // Shutdown observation must continue outside Started, including frontend/pause.
            if (NetworkConfig.TransportServer is LiteNetLibServer lanServer)
                lanServer.PollShutdown();

			scheduler.Tick();

			if (NetworkConfig.transport.Equals(NetworkConfig.NetworkTransport.STEAMWORKS))
			{
				if (!SteamManager.Initialized)
					return;
			}

            if (GameServer.State == ServerState.Started && MultiplayerSession.IsHost)
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

            // Lifecycle owns LAN host disposal, regardless of which quit callback runs first.
            // Retain the existing client and Steam shutdown path here.
            if (!MultiplayerSession.InActiveSession
                || (NetworkConfig.IsLanConfig() && MultiplayerSession.IsHost))
                return;
            NetworkConfig.Stop();
		}
	}
}
