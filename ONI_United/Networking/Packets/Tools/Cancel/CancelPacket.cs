using Shared.Profiling;

namespace ONI_United.Networking.Packets.Tools.Cancel
{
	public class CancelPacket : DragToolPacket
	{
		public CancelPacket()
		{
			using var _ = Profiler.Scope();

			ToolInstance = CancelTool.Instance;
			ToolMode = DragToolMode.OnDragTool;
		}
	}
}
