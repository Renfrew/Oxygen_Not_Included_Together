using Shared.Profiling;

namespace ONI_United.Networking.Packets.Tools.Disinfect
{
    public class DisinfectPacket : DragToolPacket
    {
        public DisinfectPacket()
        {
            using var _ = Profiler.Scope();

            ToolInstance = DisinfectTool.Instance;
            ToolMode     = DragToolMode.OnDragTool;
        }
    }
}