using ONI_United.Networking.Session;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Packets.World.Buildings;
using ONI_United.Networking.Session.States;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Profiling;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Networking.Synchronization
{
	internal class ClientReceiver_Operational : KMonoBehaviour
	{
		[MyCmpGet] NetworkIdentity o;

		public override void OnSpawn()
		{
			using var _ = Profiler.Scope();

			base.OnSpawn();
			if (MultiplayerSession.IsClient)
				PacketSender.SendToHost(new RequestOperationalStatePacket(this));
		}

		public bool IsFunctional { get; set; }

		public bool IsOperational { get; set; } = true;

		public bool IsActive { get; set; }

		public bool IsPowered { get; set; } = true;

		/// <summary>
		/// False until the host's state arrives. Until then the Operational getters return the
		/// vanilla values, so a building initialising on a client doesn't see a fake state change.
		/// </summary>
		public bool HasHostState { get; set; }
	}
}
