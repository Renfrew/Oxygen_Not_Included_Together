using ONI_United.DebugTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Runtime
{
	internal class BulkPacketMonitor : MonoBehaviour
	{
		// Poll often enough for 100ms bulk packets while leaving pacing to PacketSender.
		private float updateIntervalSeconds = 0.1f;
		private float updateTimer;

		public void LateUpdate()
		{
			if (!MultiplayerSession.InActiveSession)
				return;

			updateTimer += Time.unscaledDeltaTime;

			if (updateTimer < updateIntervalSeconds)
			{
				return;
			}
			updateTimer -= updateIntervalSeconds;
			PacketSender.DispatchPendingBulkPackets();
		}
	}
}
