#nullable enable
using System;
using ONI_United.Networking.Transport;
using ONI_United.Networking.Transport.Steamworks;
using ONI_United.DebugTools;

namespace ONI_United.Core
{
    public partial class NetworkRuntime
    {
        internal static object? ActiveOperation { get; private set; }
        internal static object? OperationTransport { get; private set; }
        internal static bool HasActiveOperation => ActiveOperation != null;
        internal static bool HasHostOperation => HasActiveOperation && OperationTransport is TransportServer;
        internal static bool HasClientOperation => HasActiveOperation && OperationTransport is ClientTransport;

        internal static bool IsCurrentOperation(object? owner, object? operation) =>
            operation != null && ReferenceEquals(operation, ActiveOperation)
            && ReferenceEquals(owner, OperationTransport)
            && (ReferenceEquals(owner, TransportServer) || ReferenceEquals(owner, TransportClient));

        internal static void BeginHostOperation()
        {
            if (HasActiveOperation) throw new InvalidOperationException("A multiplayer operation is still owned.");
            OperationTransport = TransportServer;
            ActiveOperation = new object();
        }

        internal static void BeginClientOperation()
        {
            if (HasActiveOperation) throw new InvalidOperationException("A multiplayer operation is still owned.");
            TransportClient.BeginOperation();
            OperationTransport = TransportClient;
            ActiveOperation = TransportClient.Operation;
        }

        internal static void ClearOperation()
        {
            ActiveOperation = null;
            OperationTransport = null;
        }

        internal static void InvalidateOperation(object owner)
        {
            if (ReferenceEquals(owner, OperationTransport)) ClearOperation();
        }

        internal static void StartHostTransport(object? operation)
        {
            var owner = TransportServer;
            if (IsCurrentOperation(owner, operation) && Lifecycle.Current == Lifecycle.Phase.StartingHost)
                owner.Start();
        }

        internal static bool HostSessionReady(TransportServer owner, object? operation) =>
            IsCurrentOperation(owner, operation) && Lifecycle.Handle(Lifecycle.Event.HostSessionReady);

        public static void StartServer()
        {
            if (!Lifecycle.Handle(Lifecycle.Event.StartHost))
                DebugConsole.LogError("[NetworkRuntime] Host start is not permitted in the current lifecycle state.");
        }

        public static void Stop()
        {
            if (HasHostOperation) StopHost();
            else if (HasClientOperation) DisconnectClient();
        }

        public static void StopHost()
        {
            var owner = TransportServer;
            var operation = ActiveOperation;
            Lifecycle.Handle(Lifecycle.Event.ClearHostSession, () =>
            {
                if (IsCurrentOperation(owner, operation)) owner.Stop();
            });
        }

        internal static void TransportEstablished(TransportClient owner, object? operation)
        {
            if (IsCurrentOperation(owner, operation))
                Lifecycle.Handle(Lifecycle.Event.PrepareClientSession);
        }

        internal static bool ClientAdmitted(TransportClient owner, object? operation) =>
            IsCurrentOperation(owner, operation)
            && Lifecycle.Handle(Lifecycle.Event.ClientAdmitted, () =>
            {
                if (IsCurrentOperation(owner, operation)) owner.OnRequestStateOrReturn?.Invoke();
            });

        public static void DisconnectClient()
        {
            var owner = TransportClient;
            var operation = ActiveOperation;
            Lifecycle.Handle(Lifecycle.Event.ClearClientSession, () =>
            {
                if (!IsCurrentOperation(owner, operation)) return;
                if (!owner.TryStop(out string error))
                {
                    DebugConsole.LogError("[NetworkRuntime] Client cleanup remains unresolved: " + error);
                    return;
                }
                if (IsSteam && !SteamLobby.TryReleaseClientLobby(out error, (success, failure) =>
                {
                    if (success) Lifecycle.Handle(Lifecycle.Event.SessionCleared);
                    else DebugConsole.LogError("[NetworkRuntime] Client lobby cleanup remains unresolved: " + failure);
                }))
                {
                    if (error != null) DebugConsole.LogError("[NetworkRuntime] " + error);
                    return;
                }
                Lifecycle.Handle(Lifecycle.Event.SessionCleared);
            });
        }
    }
}
