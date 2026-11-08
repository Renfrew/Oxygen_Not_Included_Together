// Small deterministic API doubles for the source-linked inactive Steam draft.
// These tests exercise callback ordering and ownership, not the Steam runtime.
#nullable disable
using System;
using System.Collections.Generic;

internal static class SteamManager { internal static bool Initialized = true; }

namespace Steamworks
{
    public readonly record struct HSteamListenSocket(uint m_HSteamListenSocket)
    { internal static HSteamListenSocket Invalid => default; }
    public readonly record struct HSteamNetPollGroup(uint m_HSteamNetPollGroup)
    { internal static HSteamNetPollGroup Invalid => default; }
    public readonly record struct HSteamNetConnection(uint m_HSteamNetConnection)
    { internal static HSteamNetConnection Invalid => default; }
    internal struct GameLobbyJoinRequested_t { internal CSteamID m_steamIDFriend, m_steamIDLobby; }
    internal struct LobbyChatUpdate_t { internal ulong m_ulSteamIDUserChanged; internal uint m_rgfChatMemberStateChange; }
    internal struct LobbyCreated_t { internal ulong m_ulSteamIDLobby; internal EResult m_eResult; }
    internal struct LobbyEnter_t { internal ulong m_ulSteamIDLobby; internal uint m_EChatRoomEnterResponse; }
    internal struct LobbyMatchList_t { internal uint m_nLobbiesMatching; }
    internal struct PersonaStateChange_t { internal ulong m_ulSteamID; internal int m_nChangeFlags; }
    internal readonly record struct SteamAPICall_t(ulong Value)
    {
        internal static SteamAPICall_t Invalid => default;
    }

    internal sealed class CallResult<T> : IDisposable
    {
        internal static CallResult<T> Create(Action<T, bool> completed) => new();
        internal void Set(SteamAPICall_t call) =>
            throw new NotSupportedException("Steam lobby call results require a native callback fixture.");
        public void Dispose() { }
    }
    public readonly record struct CSteamID(ulong m_SteamID)
    {
        internal static CSteamID Nil => default;
        internal bool IsValid() => m_SteamID != 0;
    }
    internal enum ESteamNetworkingConfigValue { k_ESteamNetworkingConfig_TimeoutInitial, k_ESteamNetworkingConfig_TimeoutConnected }
    internal enum ESteamNetworkingConfigDataType { k_ESteamNetworkingConfig_Int32 }
    internal struct SteamNetworkingConfigValue_t
    {
        internal ESteamNetworkingConfigValue m_eValue;
        internal ESteamNetworkingConfigDataType m_eDataType;
        internal ConfigValue m_val;
        internal struct ConfigValue { internal int m_int32; }
    }
    public enum EResult { k_EResultOK, k_EResultFail }
    internal enum ESteamNetworkingConnectionState
    {
        k_ESteamNetworkingConnectionState_Connecting,
        k_ESteamNetworkingConnectionState_Connected,
        k_ESteamNetworkingConnectionState_ClosedByPeer,
        k_ESteamNetworkingConnectionState_ProblemDetectedLocally
    }
    internal struct SteamNetworkingIdentity
    {
        internal CSteamID SteamId;
        internal CSteamID GetSteamID() => SteamId;
        internal void SetSteamID64(ulong id) => SteamId = new(id);
    }
    internal struct SteamNetConnectionInfo_t
    {
        internal HSteamListenSocket m_hListenSocket;
        internal ESteamNetworkingConnectionState m_eState;
        internal SteamNetworkingIdentity m_identityRemote;
    }
    internal struct SteamNetConnectionStatusChangedCallback_t
    {
        internal HSteamNetConnection m_hConn;
        internal SteamNetConnectionInfo_t m_info;
    }
    internal struct SteamNetworkingMessage_t
    {
        internal HSteamNetConnection m_conn;
        internal IntPtr m_pData;
        internal int m_cbSize;
        internal static readonly List<IntPtr> Released = new();
        internal static readonly HashSet<IntPtr> ReleaseFailures = new();
        internal static void Release(IntPtr pointer)
        {
            Released.Add(pointer);
            if (ReleaseFailures.Contains(pointer)) throw new InvalidOperationException("Release failure");
        }
    }
    internal sealed class Callback<T> : IDisposable
    {
        private static readonly List<Callback<T>> callbacks = new();
        private readonly Action<T> handler;
        private Callback(Action<T> handler) { this.handler = handler; callbacks.Add(this); }
        internal static Callback<T> Create(Action<T> handler) => new(handler);
        internal static void Dispatch(T data)
        { foreach (var callback in callbacks.ToArray()) callback.handler(data); }
        internal static Action<T> CaptureDispatch()
        {
            var captured = callbacks.ToArray();
            return data => { foreach (var callback in captured) callback.handler(data); };
        }
        internal static int Count => callbacks.Count;
        public void Dispose() => callbacks.Remove(this);
    }
    internal static class SteamFriends
    {
        internal static readonly Dictionary<CSteamID, string> Names = new();
        internal static CSteamID LastRequestedIdentity;
        internal static bool RequestUserInformation(CSteamID identity, bool nameOnly)
        {
            if (!nameOnly) throw new InvalidOperationException("Only persona names should be requested.");
            LastRequestedIdentity = identity;
            return true;
        }
        internal static string GetFriendPersonaName(CSteamID identity) =>
            Names.TryGetValue(identity, out var name) ? name : "[unknown]";
    }
    public struct SteamNetConnectionRealTimeStatus_t
    {
        internal int m_nPing, m_cbSentUnackedReliable, m_cbPendingUnreliable;
        internal long m_usecQueueTime;
        internal float m_flConnectionQualityLocal, m_flConnectionQualityRemote;
        internal float m_flInBytesPerSec, m_flOutBytesPerSec, m_flInPacketsPerSec, m_flOutPacketsPerSec;
        internal ESteamNetworkingConnectionState m_eState;
    }
    public struct SteamNetConnectionRealTimeLaneStatus_t { }
    internal static class SteamNetworkingSockets
    {
        internal static readonly Dictionary<HSteamNetConnection, SteamNetConnectionInfo_t> Connections = new();
        internal static readonly HashSet<HSteamNetConnection> CloseFailures = new();
        internal static readonly HashSet<HSteamNetConnection> Closed = new();
        internal static uint NextClientHandle = 10;
        internal static int ConnectCalls, RunCallbackCalls;
        internal static bool ConnectFails, ConnectThrows;
        internal static SteamNetworkingConfigValue_t[] LastOptions;
        internal static int LastVirtualPort;
        internal static readonly Queue<IntPtr[]> Received = new();
        internal static readonly Queue<Action> PendingCallbacks = new();
        internal static HSteamNetConnection ConnectP2P(ref SteamNetworkingIdentity identity, int port, int count, SteamNetworkingConfigValue_t[] options)
        {
            ConnectCalls++; LastVirtualPort = port; LastOptions = options;
            if (ConnectThrows) throw new InvalidOperationException("Connect failure");
            if (ConnectFails) return HSteamNetConnection.Invalid;
            var handle = new HSteamNetConnection(NextClientHandle++);
            Connections.Add(handle, new() { m_identityRemote = identity,
                m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting });
            return handle;
        }
        internal static EResult GetConnectionRealTimeStatus(HSteamNetConnection connection,
            ref SteamNetConnectionRealTimeStatus_t status, int count, ref SteamNetConnectionRealTimeLaneStatus_t lane)
        {
            if (!Connections.TryGetValue(connection, out var info)) return EResult.k_EResultFail;
            status.m_eState = info.m_eState;
            return EResult.k_EResultOK;
        }
        internal static void RunCallbacks()
        {
            RunCallbackCalls++;
            while (PendingCallbacks.TryDequeue(out var next)) next();
        }
        internal static int ReceiveMessagesOnConnection(HSteamNetConnection connection, IntPtr[] messages, int max)
        {
            if (!Connections.ContainsKey(connection)) return -1;
            if (!Received.TryDequeue(out var batch)) return 0;
            Array.Copy(batch, messages, batch.Length);
            return batch.Length;
        }
        internal static HSteamListenSocket CreateListenSocketP2P(int port, int count, SteamNetworkingConfigValue_t[] options) => new(1);
        internal static HSteamNetPollGroup CreatePollGroup() => new(1);
        internal static bool CloseListenSocket(HSteamListenSocket socket) { Connections.Clear(); return true; }
        internal static bool DestroyPollGroup(HSteamNetPollGroup group) => true;
        internal static bool GetConnectionInfo(HSteamNetConnection connection, out SteamNetConnectionInfo_t info)
        {
            return Connections.TryGetValue(connection, out info);
        }
        internal static EResult AcceptConnection(HSteamNetConnection connection)
        {
            var info = Connections[connection];
            info.m_eState = ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected;
            Connections[connection] = info;
            return EResult.k_EResultOK;
        }
        internal static bool SetConnectionPollGroup(HSteamNetConnection connection, HSteamNetPollGroup group) => true;
        internal static bool CloseConnection(HSteamNetConnection connection, int reason, string detail, bool linger)
        {
            if (CloseFailures.Contains(connection)) return false;
            Closed.Add(connection);
            return Connections.Remove(connection);
        }
        internal static int ReceiveMessagesOnPollGroup(HSteamNetPollGroup group, IntPtr[] messages, int count) => 0;
    }
}
namespace Shared.Tests.Networking
{
    internal static class SteamLobbyFixture
    {
        // The native lobby-entry callback is outside the scope of these tests.
        internal static void SetCurrentLobby(ulong id) =>
            typeof(ONI_United.Networking.Transports.Steamworks.SteamLobby)
                .GetProperty("CurrentLobby")!
                .SetValue(null, new Steamworks.CSteamID(id));
    }
}
