using ONI_United.DebugTools;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Packets.Social;
using ONI_United.Scripts.Duplicants;
using System.IO;
using Shared.Profiling;
using UnityEngine;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Packets.World
{
	/// <summary>
	/// Packet to spawn entities (duplicants or items) on clients with matching NetIds.
	/// Sent from host when an entity is spawned (e.g., from Telepad or Sandbox).
	/// </summary>
	public class TelepadEntitySpawnPacket : IPacket
	{
		public ImmigrantOptionEntry EntityData;
		public int NetId;

		// Position
		public Vector3 Pos;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(NetId);
			EntityData.Serialize(writer);
			writer.Write(Pos);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			NetId = reader.ReadInt32();
			EntityData = ImmigrantOptionEntry.Deserialize(reader);
			Pos = reader.ReadVector3();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			// Only clients should process this
			if (MultiplayerSession.IsHost) return;

			DebugConsole.Log($"[EntitySpawnPacket] Client: Received spawn for NetId {NetId}, IsDuplicant={EntityData.IsDuplicant}, ItemID: {EntityData.GetId()} at {Pos}");

			try
			{
				var deliverable = EntityData.ToGameDeliverable();
				if (deliverable == null)
				{
					DebugConsole.LogWarning($"[EntitySpawnPacket] Deliverable could not be constructed for NetId {NetId}");
					return;
				}

				if (deliverable is not MinionStartingStats)
				{
					// move care packages a bit to the left to be centered
					Pos.x -= 0.5f;
				}

				GameObject entity = deliverable.Deliver(Pos);
				if (entity != null)
				{
					// duplicants from the printer are assigned an extra skill point, this is skipped over with a direct delivery
					if (entity.TryGetComponent<MinionResume>(out var res))
						res.ForceAddSkillPoint();

					NetworkIdentity identity = entity.AddOrGet<NetworkIdentity>();
					identity.OverrideNetId(NetId);

					if (entity.GetComponent<MinionIdentity>() != null || entity.HasTag(GameTags.BaseMinion))
					{
						entity.AddOrGet<MinionMultiplayerInitializer>();
					}

					DebugConsole.Log($"[EntitySpawnPacket] Successfully spawned duplicant '{entity.name}' (NetId: {NetId}) on client");
				}
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogError($"[EntitySpawnPacket] Failed to spawn: {ex}");
			}
		}
	}
}
