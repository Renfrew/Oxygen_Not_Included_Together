using ONI_United.Networking.Replication.Components;
using ONI_United.UI.Screens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ONI_United.Networking.Packets.Architecture;
using Shared.Profiling;
using Shared.Interfaces.Networking;

namespace ONI_United.Networking.Packets.Social
{
	public class ChatHistorySyncPacket : IPacket, IAllowedWithoutWorldPacket
	{
		public List<OxySyncChat.PendingMessage> Messages = new List<OxySyncChat.PendingMessage>();

		public ChatHistorySyncPacket()
		{
		}

		public ChatHistorySyncPacket(List<OxySyncChat.PendingMessage> messages)
		{
			using var _ = Profiler.Scope();

			Messages = messages;
		}

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(Messages.Count);
			foreach (var msg in Messages)
			{
				writer.Write(msg.timestamp);
				writer.Write(msg.message);
			}
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			int count = reader.ReadInt32();
			Messages = new List<OxySyncChat.PendingMessage>(count);
			for (int i = 0; i < count; i++)
			{
				Messages.Add(new OxySyncChat.PendingMessage
				{
					timestamp = reader.ReadInt64(),
					message = reader.ReadString()
				});
			}
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (UnityChatBoxUI.Instance != null)
			{
				var ts = DateTimeOffset.FromUnixTimeMilliseconds(0).DateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
				UnityChatBoxUI.Instance.SendNewChatMessage("System", ts, ONI_Together.STRINGS.UI.MP_CHATWINDOW.CHAT_INITIALIZED);
			}

			foreach (var msg in Messages)
			{
				string ts = DateTimeOffset.FromUnixTimeMilliseconds(msg.timestamp).DateTime.ToString("HH:mm", CultureInfo.InvariantCulture);
				UnityChatBoxUI.Instance?.SendNewChatMessage("System", ts, msg.message);
			}
		}
	}
}
