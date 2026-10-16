using ONI_United.DebugTools;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Packets.Architecture;
using ONI_United.Networking.Packets.Tools.Clear;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shared.Profiling;
using UnityEngine;

namespace ONI_United.Networking.Packets.Tools
{
	internal class DisconnectPacket : DragToolPacket
	{
		public DisconnectPacket() : base()
		{
			using var _ = Profiler.Scope();

			ToolInstance = DisconnectTool.Instance;
			ToolMode = DragToolMode.OnDragComplete;
		}
	}
}
