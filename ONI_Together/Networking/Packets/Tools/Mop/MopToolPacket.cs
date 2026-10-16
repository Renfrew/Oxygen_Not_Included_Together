using Shared.Profiling;

namespace ONI_United.Networking.Packets.Tools.Mop
{
	public class MopToolPacket : DragToolPacket
	{
		public MopToolPacket()
		{
			using var _ = Profiler.Scope();

			ToolInstance = MopTool.Instance;
			ToolMode     = DragToolMode.OnDragTool;
		}
	}
}
