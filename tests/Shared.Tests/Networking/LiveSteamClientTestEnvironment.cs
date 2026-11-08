// Doubles for the source-linked production Steam client. No native runtime is loaded.
#nullable disable
namespace Shared.Profiling
{
    internal static class Profiler
    {
        internal static Sample Scope() => new();
        internal sealed class Sample : System.IDisposable
        { public void Dispose() { } internal void End(int messages, int bytes) { } }
    }
}
namespace UnityEngine { internal static class Mathf { internal static float Sqrt(float value) => MathF.Sqrt(value); } }
namespace ONI_United.UI.Screens
{
    public sealed class NetworkIndicatorsScreen { public enum NetworkState { GOOD, DEGRADED, BAD } }
}
namespace ONI_United.Networking.Packets.Architecture
{
    internal static class PacketHandler
    {
        internal static ulong? CurrentSenderPlayerId { get; private set; }
        internal static object CurrentPhysicalConnection { get; private set; }
        internal static Action OnIncoming;
        internal static void HandleIncoming(byte[] bytes, ulong? sender, object connection)
        {
            CurrentSenderPlayerId = sender;
            CurrentPhysicalConnection = connection;
            try { OnIncoming?.Invoke(); }
            finally
            {
                CurrentSenderPlayerId = null;
                CurrentPhysicalConnection = null;
            }
        }
    }
}
namespace ONI_United
{
    internal static class STRINGS
    {
        internal static class UI
        {
            internal static class MP_OVERLAY
            {
                internal static class CLIENT
                {
                    internal static class STEAMWORKS
                    {
                        internal const string HOST_DISCONNECTED = "host disconnected", HOST_DISCONNECTED_DESC = "host ended connection";
                        internal const string LOCAL_PROBLEM = "local problem", LOCAL_PROBLEM_DESC = "native failure";
                    }
                }
            }
        }
    }
}
