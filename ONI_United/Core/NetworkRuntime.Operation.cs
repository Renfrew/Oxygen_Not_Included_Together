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

        internal static bool ConnectClient(ClientTransport owner, System.Action connect) =>
            ReferenceEquals(owner, TransportClient)
            && Lifecycle.Handle(Lifecycle.Event.Join, connect);

        internal static bool DisconnectClient(ClientTransport owner, System.Action disconnect)
        {
            var operation = ActiveOperation;
            if (!IsCurrentOperation(owner, operation)) return false;
            return Lifecycle.Handle(Lifecycle.Event.ClearClientSession, () =>
            {
                if (IsCurrentOperation(owner, operation)) disconnect();
            });
        }

        internal static bool PollClient(ClientTransport owner, System.Action poll)
        {
            var operation = ActiveOperation;
            if (!IsCurrentOperation(owner, operation)) return false;
            return Lifecycle.Handle(Lifecycle.Event.PollClient, () =>
            {
                if (IsCurrentOperation(owner, operation)) poll();
            });
        }

        internal static bool ClientTransportConnected(ClientTransport owner, System.Action notify)
        {
            var operation = ActiveOperation;
            if (!IsCurrentOperation(owner, operation)) return false;
            return Lifecycle.Handle(Lifecycle.Event.PrepareClientSession, () =>
            {
                if (IsCurrentOperation(owner, operation)) notify();
            });
        }

        internal static bool ClientTransportDisconnected(ClientTransport owner, object? operation, System.Action notify) =>
            IsCurrentOperation(owner, operation)
            && Lifecycle.Handle(Lifecycle.Event.SessionCleared, notify);

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
            if (!IsCurrentOperation(owner, operation)) return;
            if (Lifecycle.Current == Lifecycle.Phase.Joining)
                Lifecycle.Handle(Lifecycle.Event.PrepareClientSession);
        }

        internal static bool ClientAdmitted(TransportClient owner, object? operation) =>
            IsCurrentOperation(owner, operation)
            && Lifecycle.Handle(Lifecycle.Event.ClientAdmitted, () =>
            {
                if (IsCurrentOperation(owner, operation)) owner.OnRequestStateOrReturn?.Invoke();
            });

        public static void DisconnectClient() => TransportClient.Disconnect();

        internal static void CleanupClientTransport(TransportClient owner, object? operation, System.Action completed)
        {
            if (!IsCurrentOperation(owner, operation))
                throw new InvalidOperationException("Client cleanup was requested without owning the active operation.");

            if (!owner.TryStop(out string error))
            {
                DebugConsole.LogError("[NetworkRuntime] Client cleanup remains unresolved: " + error);
                return;
            }
            if (!IsCurrentOperation(owner, operation)) return;
            if (IsSteam && !SteamLobby.TryReleaseClientLobby(out error, (success, failure) =>
            {
                if (!success)
                    DebugConsole.LogError("[NetworkRuntime] Client lobby cleanup remains unresolved: " + failure);
                else if (IsCurrentOperation(owner, operation))
                    completed();
            }))
            {
                if (error != null) DebugConsole.LogError("[NetworkRuntime] " + error);
                return;
            }
            if (IsCurrentOperation(owner, operation)) completed();
        }
    }
}
