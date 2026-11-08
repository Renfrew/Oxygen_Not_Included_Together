using ONI_United.DebugTools;
using ONI_United.Networking.Identity;
using Shared.Interfaces.Networking;
using System.IO;
using System.Linq;
using Shared.Profiling;

namespace ONI_United.Networking.Packets.Animation;

public class ToggleMinionKanimEffectPacket : IPacket
{
	public int NetId;
	public bool Enable;
	public int ContextHash; // A hash of the context string, e.g. "dig", "sleep", "build"
	public string Event;   // e.g. "LaserOn", "LaserOff", "DreamsOn"

	public void Serialize(BinaryWriter writer)
	{
		using var _ = Profiler.Scope();

		writer.Write(NetId);
		writer.Write(Enable);
		writer.Write(ContextHash);
		writer.Write(Event);
	}

	public void Deserialize(BinaryReader reader)
	{
		using var _ = Profiler.Scope();

		NetId = reader.ReadInt32();
		Enable = reader.ReadBoolean();
		ContextHash = reader.ReadInt32();
		Event = reader.ReadString();
	}

	public void OnDispatched()
	{
		using var _ = Profiler.Scope();

		if (!NetworkIdentityRegistry.TryGet(NetId, out var go)) return;

		var toggler = go.GetComponentsInChildren<KBatchedAnimEventToggler>()
				.FirstOrDefault(t => t.enableEvent == Event || t.disableEvent == Event);
		if (toggler == null)
		{
			DebugConsole.LogWarning($"[ToggleMinionEffectPacket] Toggler with event '{Event}' not found");
			return;
		}

		toggler.GetComponentInParent<AnimEventHandler>()?.SetContext(new HashedString(ContextHash));

		if (Enable)
			toggler.Enable(null);
		else
			toggler.Disable(null);
	}
}
