using HarmonyLib;
using ONI_Together.Networking;
using ONI_Together.Networking.Components;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Packets.Social;
using Shared.Profiling;

namespace ONI_United.Patches.Duplicant
{
	internal class Dreamer_Patches
	{
		[HarmonyPatch(typeof(Dreamer), nameof(Dreamer.PrepareDream))]
		public static class Dreamer_PrepareDream_Patch
		{
			public static bool Prefix()
			{
				using var _ = Profiler.Scope();
				return !MultiplayerSession.IsClient;
			}

			public static void Postfix(Dreamer.Instance smi)
			{
				using var _ = Profiler.Scope();

				if (!MultiplayerSession.IsHost)
					return;

				var dream = smi.currentDream;
				if (dream == null)
					return;

				var go = smi.gameObject;
				if (go.IsNullOrDestroyed())
					return;

				if (!go.TryGetComponent<NetworkIdentity>(out var identity))
					return;

				string[] iconNames = null;
				if (dream.Icons != null)
				{
					iconNames = new string[dream.Icons.Length];
					for (int i = 0; i < dream.Icons.Length; i++)
						iconNames[i] = dream.Icons[i]?.name ?? string.Empty;
				}

				var packet = new DreamBubblePacket
				{
					NetId = identity.NetId,
					IsVisible = true,
					DreamId = dream.Id ?? string.Empty,
					BackgroundAnim = dream.BackgroundAnim ?? string.Empty,
					IconSpriteNames = iconNames,
					SecondPerImage = dream.secondPerImage
				};

				PacketSender.SendToAllClients(packet, PacketSendMode.Reliable);
			}
		}

		[HarmonyPatch(typeof(Dreamer), nameof(Dreamer.RemoveDream))]
		public static class Dreamer_RemoveDream_Patch
		{
			public static bool Prefix()
			{
				using var _ = Profiler.Scope();
				return !MultiplayerSession.IsClient;
			}

			public static void Postfix(Dreamer.Instance smi)
			{
				using var _ = Profiler.Scope();

				if (!MultiplayerSession.IsHost)
					return;

				var go = smi.gameObject;
				if (go.IsNullOrDestroyed())
					return;

				if (!go.TryGetComponent<NetworkIdentity>(out var identity))
					return;

				var packet = new DreamBubblePacket
				{
					NetId = identity.NetId,
					IsVisible = false
				};

				PacketSender.SendToAllClients(packet, PacketSendMode.Reliable);
			}
		}

		[HarmonyPatch(typeof(Dreamer), nameof(Dreamer.UpdateDream))]
		public static class Dreamer_UpdateDream_Patch
		{
			public static bool Prefix()
			{
				using var _ = Profiler.Scope();
				return !MultiplayerSession.IsClient;
			}
		}
	}
}
