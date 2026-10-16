using Database;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Packets.Architecture;
using System.IO;
using Shared.Profiling;
using UnityEngine;
using Shared.Interfaces.Networking;
using ONI_United.Networking.Session;

namespace ONI_United.Networking.Packets.Social
{
	public class DreamBubblePacket : IPacket
	{
		public int NetId;
		public bool IsVisible;
		public string DreamId;
		public string BackgroundAnim;
		public string[] IconSpriteNames;
		public float SecondPerImage;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(NetId);
			writer.Write(IsVisible);
			writer.Write(DreamId ?? string.Empty);
			writer.Write(BackgroundAnim ?? string.Empty);

			if (IconSpriteNames != null)
			{
				writer.Write(IconSpriteNames.Length);
				foreach (var name in IconSpriteNames)
					writer.Write(name ?? string.Empty);
			}
			else
			{
				writer.Write(0);
			}

			writer.Write(SecondPerImage);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			NetId = reader.ReadInt32();
			IsVisible = reader.ReadBoolean();
			DreamId = reader.ReadString();
			BackgroundAnim = reader.ReadString();

			int iconCount = reader.ReadInt32();
			IconSpriteNames = new string[iconCount];
			for (int i = 0; i < iconCount; i++)
				IconSpriteNames[i] = reader.ReadString();

			SecondPerImage = reader.ReadSingle();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (MultiplayerSession.IsHost)
				return;

			if (!NetworkIdentityRegistry.TryGet(NetId, out var identity))
				return;

			var go = identity.gameObject;
			if (go.IsNullOrDestroyed())
				return;

			if (!IsVisible)
			{
				NameDisplayScreen.Instance.StopDreaming(go);
				return;
			}

			Dream dream = null;
			if (!string.IsNullOrEmpty(DreamId))
				dream = Db.Get().Dreams.TryGet(DreamId);

			if (dream == null && !string.IsNullOrEmpty(BackgroundAnim) && IconSpriteNames != null && IconSpriteNames.Length > 0)
			{
				dream = new Dream(DreamId ?? "Dream", Db.Get().Dreams, BackgroundAnim, IconSpriteNames, SecondPerImage);
			}

			if (dream != null)
				NameDisplayScreen.Instance.SetDream(go, dream);
		}
	}
}
