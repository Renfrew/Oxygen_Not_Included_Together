using ONI_United.Networking.Sessions.States;
using Shared.Profiling;
using UnityEngine;
using ONI_United.Networking;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Sessions;
using ONI_United.StateMachines;
using ONI_United.Networking.Transfer;
using Shared;

namespace ONI_United.Networking.Runtime
{
	public class NetworkingComponent : MonoBehaviour
	{
		public const float MaxMissedTicks = 3f;

		private static float _tickAccumulator;
		private static float _tickInterval = 1f / 60f;
		public static float TickInterval => _tickInterval;

		public static void RefreshTickRate()
		{
			if (Configuration.Instance?.Host?.Server != null)
				_tickInterval = 1f / TickRateToTps(Configuration.Instance.Host.Server.TickRate);
		}

		private static int TickRateToTps(ServerTickRate rate) => rate switch
		{
			ServerTickRate.TPS_20 => 20,
			ServerTickRate.TPS_30 => 30,
			ServerTickRate.TPS_60 => 60,
			ServerTickRate.TPS_90 => 90,
			ServerTickRate.TPS_120 => 120,
			ServerTickRate.TPS_128 => 128,
			_ => 60,
		};

        internal static void InitializeHostTick()
        {
            RefreshTickRate();
            _tickAccumulator = 0f;
        }


		public static UnityTaskScheduler scheduler = new UnityTaskScheduler();

		/*
		 * TODO:
		 * Update this class now that we can have different relay types. This is not steam specific anymore
		 *
		 * **/

		private void Update()
		{
			using var _ = Profiler.Scope();

            NetworkConfig.TransportServer.PollCancellation();

			scheduler.Tick();

			if (NetworkConfig.transport.Equals(NetworkConfig.NetworkTransport.STEAMWORKS))
			{
				if (!SteamManager.Initialized)
					return;
			}

            if (Lifecycle.Current == Lifecycle.Phase.Hosting && MultiplayerSession.IsHost)
			{
				_tickAccumulator += Time.unscaledDeltaTime;
                _tickAccumulator = Mathf.Min(_tickAccumulator, _tickInterval * MaxMissedTicks);
                if (_tickAccumulator >= _tickInterval)
                {
                    _tickAccumulator -= _tickInterval;
                    NetworkConfig.TransportServer.Update();
                    NetworkConfig.TransportServer.OnMessageRecieved();
                    SaveFileTransferManager.CheckForLostChunks();
                }
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

            // Lifecycle owns host disposal for both protocols; retain client shutdown.
            if (!MultiplayerSession.InActiveSession
                || Lifecycle.HasHostOperation)
                return;
            NetworkConfig.Stop();
		}
	}
}
