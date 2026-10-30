// Minimal domain/native doubles for the source-linked production dispatcher and
// transport base. These tests do not exercise Unity or native network resources.
#nullable disable
using ONI_United.Networking.Sessions;

namespace Shared.Tests.Networking
{
    internal static class LifecycleTestEnvironment
    {
        internal static readonly List<string> Trace = new();
        internal static Action Registered;
        internal static Action Clearing;
        internal static bool RegistrationSucceeds = true;
        internal static void Reset()
        {
            Trace.Clear(); Registered = null; Clearing = null;
            RegistrationSucceeds = true; Session.Reset();
            LoadScreen.Loaded.Clear();
            ONI_United.Networking.Runtime.GameClient.State = ONI_United.Networking.Sessions.States.ClientState.Disconnected;
        }
    }
}
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public bool enabled;
        protected static void Destroy(object value) { }
    }
}
namespace ONI_United.DebugTools
{
    public static class DebugConsole
    {
        public static void Log(string text) => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add(text);
        public static void LogError(string text) => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add(text);
    }
}
namespace ONI_United.Misc { public static class Utils { public static bool IsInGame() => true; } }
namespace ONI_United.Networking.Identity
{
    public static class NetworkIdentityRegistry
    {
        public static void RebuildFromScene() => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add("identity:start");
        public static void Clear() => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add("identity:stop");
    }
}
namespace ONI_United.Networking.Presentation
{
    public sealed class CursorManager
    {
        public static readonly CursorManager Instance = new();
        public void AssignColor() => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add("color:start");
    }
}
namespace ONI_United.Patches.ToolPatches
{
    public static class SelectToolPatch
    {
        public static void UpdateColor() => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add("color:stop");
    }
}
namespace ONI_United.Networking.Runtime
{
    public static class GameClient { public static Sessions.States.ClientState State; }
    public static class NetworkingComponent
    {
        public static void InitializeHostTick() => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add("tick:start");
    }
}
namespace ONI_United.Networking.Sessions
{
    public static class MultiplayerSession
    {
        public static bool ShouldHostAfterLoad;
        public static bool RegisterAsHost()
        {
            var env = Shared.Tests.Networking.LifecycleTestEnvironment.Trace;
            env.Add("session:start");
            if (!Shared.Tests.Networking.LifecycleTestEnvironment.RegistrationSucceeds) return false;
            bool result = Session.BeginHosting(new Player(10, "Host"));
            Shared.Tests.Networking.LifecycleTestEnvironment.Registered?.Invoke();
            return result;
        }
        public static void Clear()
        {
            Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add("session:stop");
            Shared.Tests.Networking.LifecycleTestEnvironment.Clearing?.Invoke();
            Session.Reset();
        }
    }
}
namespace ONI_United.Networking
{
    public static class NetworkConfig { public static Transport.TransportServer TransportServer; }
}
public static class LoadScreen
{
    internal static readonly List<(string Filename, ONI_United.StateMachines.Lifecycle.Phase Phase)> Loaded = new();
    public static void DoLoad(string filename) => Loaded.Add((filename, ONI_United.StateMachines.Lifecycle.Current));
}
