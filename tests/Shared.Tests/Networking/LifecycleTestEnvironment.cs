// Minimal domain/native doubles for the source-linked production dispatcher and
// transport base. These tests do not exercise Unity or native network resources.
#nullable disable
using ONI_United.Networking.Sessions;

namespace Shared.Tests.Networking
{
    internal static class TransportTestAccess
    {
        private static readonly System.Reflection.MethodInfo TryGetConnection =
            typeof(ONI_United.Networking.Transports.Transport).GetMethod(
                "TryGetConnection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        internal static bool TryGetTestConnection(this ONI_United.Networking.Transports.Transport transport,
            ulong playerId, out object connection)
        {
            object[] arguments = [playerId, null];
            bool found = (bool)TryGetConnection.Invoke(transport, arguments);
            connection = arguments[1];
            return found;
        }
    }

    internal static class LifecycleTestEnvironment
    {
        internal static readonly List<string> Trace = new();
        internal static Action Registered;
        internal static Action Clearing;
        internal static Action PreparingClient;
        internal static bool RegistrationSucceeds = true;
        internal static void Reset()
        {
            Trace.Clear(); Registered = null; Clearing = null; PreparingClient = null;
            RegistrationSucceeds = true; Session.Reset();
            ONI_United.Core.NetworkRuntime.UseTestServer(new ONI_United.Core.NetworkRuntime.TestServer());
            ONI_United.Core.NetworkRuntime.UseTestClient(new ONI_United.Core.NetworkRuntime.TestClient());
            LoadedWorld = true;
            ONI_United.Networking.Runtime.GameClient.ResetSynchronizationState();
            Coroutines.Clear();
            ScheduledLoads.Clear();
            ONI_United.Configuration.Instance.Host.MaxLobbySize = 16;

        }
        internal static bool LoadedWorld = true;
        internal static readonly List<System.Collections.IEnumerator> Coroutines = new();
        internal static readonly List<Func<bool>> ScheduledLoads = new();
    }
}
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public bool enabled;
        protected static void Destroy(object value) { }
    }
    public sealed class WaitForSecondsRealtime(float seconds) { }
}
namespace ONI_United.DebugTools
{
    public static class ModLog
    {
        public static void Trace(string message) => DebugConsole.Log(message);
        public static void Debug(string message) => DebugConsole.Log(message);
        public static void Info(string message) => DebugConsole.Log(message);
        public static void Warning(string message) => DebugConsole.LogWarning(message);
        public static void Error(string message, bool triggerErrorScreen = false) => DebugConsole.LogError(message);
        public static void Exception(Exception exception) => Error(exception?.ToString() ?? "Unknown exception");
    }

    public static class DebugConsole
    {
        public static void Log(string text) => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add(text);
        public static void LogWarning(string text) => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add(text);
        public static void LogException(Exception exception) => LogError(exception.ToString());
        public static void LogError(string text, bool triggerErrorScreen = false) => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add(text);
    }
}
namespace ONI_United.Misc
{
    public static class Utils
    {
        public static bool IsInGame() => Shared.Tests.Networking.LifecycleTestEnvironment.LoadedWorld;
        public static bool IsValid(this ulong id) => id != 0;
    }
    public static class CoroutineRunner
    {
        public static void RunOne(System.Collections.IEnumerator coroutine)
        {
            if (coroutine.MoveNext()) Shared.Tests.Networking.LifecycleTestEnvironment.Coroutines.Add(coroutine);
        }
    }
}
public static class Grid { public static int CellCount => Shared.Tests.Networking.LifecycleTestEnvironment.LoadedWorld ? 100 : 0; }
namespace ONI_United.Misc.World
{
    public sealed class WorldSave { }
    public static class SaveHelper
    {
        public static void RequestWorldLoad(WorldSave save, Func<bool> current) =>
            Shared.Tests.Networking.LifecycleTestEnvironment.ScheduledLoads.Add(current);
    }
}
namespace ONI_United.Networking.Transfer
{
    public static class SaveChunkAssembler { public static void Clear() { } }
}
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
        public static void UpdateColor() => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add("color:" + ONI_United.Core.Lifecycle.Current);
    }
}
namespace ONI_United.Networking.Runtime
{
    public static partial class GameClient
    {
        public static void BeginConnection(bool showLoadingScreen, string ip, int port)
        {
            var client = Core.NetworkRuntime.TransportClient;
            client.Prepare();
            client.StartConnection(ip, port);
        }

        public static void PrepareSession()
        {
            Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add("client:prepare:" + Core.Lifecycle.Current);
            Shared.Tests.Networking.LifecycleTestEnvironment.PreparingClient?.Invoke();
        }
        public static class ReadyManager
        {
            public static void SendReadyStatusPacket(ONI_United.Networking.Sessions.States.ClientReadyState state) =>
                Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add("ready:" + state);
        }
    }
}
namespace ONI_United.Networking.Sessions
{
    public static class MultiplayerSession
    {
        public static ulong HostUserID { get; set; }
        public static void CreateConnectedPlayerCursors() { }
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
            Session.Reset();
            Shared.Tests.Networking.LifecycleTestEnvironment.Clearing?.Invoke();
        }
    }
}
namespace ONI_United.Core
{
    public partial class NetworkRuntime
    {
        internal static void UseTestServer(Networking.Transports.TransportServer server) => TransportServer = server;
        internal static void UseTestClient(Networking.Transports.TransportClient client) => TransportClient = client;

        public sealed class TestClient : Networking.Transports.TransportClient
        {
            public bool CleanupSucceeds = true;
            public int CleanupAttempts;
            public override bool TryStop(out string error)
            {
                CleanupAttempts++;
                error = CleanupSucceeds ? null : "test cleanup failure";
                return CleanupSucceeds;
            }
            public override void Prepare() { }
            protected override void ConnectProtocol(string ip, int port) { }
            public override object CurrentConnection => null;
            public override bool IsCurrentConnection(object connection) => false;
            public override void Update() { }
            public override void OnMessageRecieved() { }
            public override int GetPing() => 0;
            public override UI.Screens.NetworkIndicatorsScreen.NetworkState GetJitterState() => default;
            public override UI.Screens.NetworkIndicatorsScreen.NetworkState GetLatencyState() => default;
            public override UI.Screens.NetworkIndicatorsScreen.NetworkState GetPacketlossState() => default;
            public override UI.Screens.NetworkIndicatorsScreen.NetworkState GetServerPerformanceState() => default;
        }
        public sealed class TestServer : Networking.Transports.TransportServer
        {
            protected override void StartProtocol() { }
            protected override void CancelProtocol(Action<bool, string> completed) => completed(true, null);
            protected override void DisposeProtocol() { }
            public override void CloseConnections() { }
            public override bool TryGetRemotePlayerId(object connection, out ulong id) { id = 0; return false; }
            public override void DisconnectConnection(object connection, string reason) { }
            public override void Update() { }
            public override void OnMessageRecieved() { }
            public override void KickClient(ulong id) { }
        }

    }
}
public sealed class SpeedControlScreen
{
    public static readonly SpeedControlScreen Instance = new();
    public void Pause(bool pause) => Shared.Tests.Networking.LifecycleTestEnvironment.Trace.Add(
        $"pause:{ONI_United.Core.Lifecycle.Current}:{pause}");
}


namespace ONI_United.Networking.Replication.Components
{
    internal static class OxySyncChat
    {
        internal static void AddSystemMessage(string message) { }
    }
}
namespace ONI_United.Networking.Transfer
{
    public sealed class TcpFileTransferServer
    {
        public bool IsRunning { get; private set; }
        public bool IsStopped => !IsRunning;
        public string CleanupFailure => null;
        public void Start(int lanPort) => IsRunning = true;
        public void Stop() => IsRunning = false;
    }
}
