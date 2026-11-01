using System.Collections.Generic;
using ONI_United.Core;
using ONI_United.DebugTools;
using ONI_United.Networking.Sessions;

namespace ONI_United.Networking.Transport
{
    // INACTIVE MIGRATION CONTRACT: no production protocol derives from this class.
    // NetworkRuntime admits Start/Stop through Lifecycle before these entry points.
    // Protocol owners must retain/cancel their native work and suppress obsolete
    // completion/failure notifications after Stop. Lifecycle rejects
    // invalid phases; it cannot identify an old result during a newer compatible phase.
    // Repeated Stop is admitted in Stopping: native coalescing/retry belongs to the
    // protocol. This base keeps no operation identity, pending flag or callback state.
    public abstract class ServerTransport
    {
        private readonly Dictionary<ulong, object> connections = new();

        public void Start()
        {
            InternalStart();
        }

        public void Stop()
        {
            InternalStop();
        }

        // The runtime caller services the selected server; polling does not change Lifecycle.
        public void Poll()
        {
            InternalPoll();
        }

        protected abstract void InternalPoll();
        protected abstract void InternalStart();
        protected abstract void InternalStop();

        protected void OnStarted()
        {
            Lifecycle.Handle(Lifecycle.Event.TransportStarted, null);
        }

        protected bool OnConnected(ulong clientId, string playerName, object connection)
        {
            if (connection == null || connections.ContainsKey(clientId)
                || Lifecycle.Current != Lifecycle.Phase.Hosting
                || !Session.TryAdmitPlayer(clientId, playerName))
                return false;

            connections.Add(clientId, connection);
            return true;
        }

        protected void OnStartFailed(string reason)
        {
            DebugConsole.LogError("[ServerTransport] " +
                (string.IsNullOrEmpty(reason) ? "Host startup failed." : reason));
            Lifecycle.Handle(Lifecycle.Event.Stop, Stop);
        }

        protected void OnStopped()
        {
            Lifecycle.Handle(Lifecycle.Event.Stopped, null);
        }

        protected void OnStopFailed(string reason)
        {
            DebugConsole.LogError("[ServerTransport] " +
                (string.IsNullOrEmpty(reason) ? "Host cleanup failed." : reason));
            // No Stopped event and no retry: an unsuccessful shutdown stays Stopping.
        }

        internal bool TryGetConnection(ulong clientId, out object connection) =>
            connections.TryGetValue(clientId, out connection);

        protected bool OnDisconnect(ulong clientId, object connection)
        {
            if (!connections.TryGetValue(clientId, out var current)
                || !(ReferenceEquals(current, connection)
                    || (current.GetType().IsValueType && current.Equals(connection))))
                return false;

            connections.Remove(clientId);
            Session.RemovePlayer(clientId);
            return true;
        }
    }
}
