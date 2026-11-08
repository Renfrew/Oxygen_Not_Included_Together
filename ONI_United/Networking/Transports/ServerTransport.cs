using ONI_United.Core;
using ONI_United.DebugTools;
using ONI_United.Networking.Sessions;

namespace ONI_United.Networking.Transports
{
    public abstract class ServerTransport : Transport
    {
        public void Start()
        {
            if (!Lifecycle.Handle(Lifecycle.Event.StartHost, InternalStart))
                ModLog.Warning("[ServerTransport] Failed to start host.");
        }

        public void Stop()
        {
            if (!Lifecycle.Handle(Lifecycle.Event.ClearHostSession, InternalStop))
                ModLog.Warning("[ServerTransport] Failed to clear host session.");
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
            if (!Lifecycle.Handle(Lifecycle.Event.PrepareHostSession, null))
                ModLog.Warning("[ServerTransport] Failed to prepare host session.");
            
            else if (Lifecycle.Handle(Lifecycle.Event.SessionReady, null))
                ModLog.Info("[ServerTransport] Server Became Ready.");

            else if (!Lifecycle.Handle(Lifecycle.Event.ClearHostSession, InternalStop))
                throw new System.Exception(
                    "[ServerTransport] Cannot clear host session when fail to prepare host session.");
            else
                ModLog.Warning("[ServerTransport] Clearing host session after failed preparation.");
        }

        protected void OnStartFailed(string reason)
        {
            ModLog.Error($"[ServerTransport] {(string.IsNullOrEmpty(reason) ? "Host startup failed." : reason)}");

            if (!Lifecycle.Handle(Lifecycle.Event.ClearHostSession, null))
                throw new System.Exception("[ServerTransport] Cannot clear host session after server start failure.");
            
            if (!Lifecycle.Handle(Lifecycle.Event.SessionCleared, null))
                throw new System.Exception("[ServerTransport] Cannot mark session as cleared after server cleanup.");
        }

        protected void OnStopped()
        {
            if (!Lifecycle.Handle(Lifecycle.Event.SessionCleared, null))
                throw new System.Exception("[ServerTransport] Cannot mark session as cleared on server stopped.");
            
            ModLog.Info("[ServerTransport] Server Stopped.");
        }

        protected void OnStopFailed(string reason)
        {
            throw new System.Exception(
                $"[ServerTransport] {(string.IsNullOrEmpty(reason) ? "Host cleanup failed." : reason)}");
        }

        protected bool OnConnected(ulong clientId, string playerName, object connection)
        {
            if (connection == null)
            {
                ModLog.Warning("[ServerTransport] Connection is null.");
                return false;
            }

            if (TryGetConnection(clientId, out var _))
            {
                ModLog.Warning("[ServerTransport] Client already has a connection.");
                return false;
            }

            if (!Lifecycle.Handle(Lifecycle.Event.AddConnection, null))
            {
                ModLog.Warning("[ServerTransport] Failed to add client into the session.");
                return false;
            }
            // if (connection == null || HasConnection(clientId)
            //     // || !Lifecycle.IsHostGameplay || Lifecycle.Current != Lifecycle.Phase.InGameSession
            //     || !Session.TryAdmitPlayer(clientId, playerName))
            //     return false;

            AddConnection(clientId, connection);
            // Session.TryAddPlayer(clientId, playerName);
            return true;
        }

        protected bool OnDisconnect(ulong clientId, object connection)
        {
            if (!TryGetConnection(clientId, out var current))
            {
                ModLog.Warning("[ServerTransport] Client connection not found or does not match.");
                return false;
            }

            // Steam uses value types for connections, so we need to check for value type equality separately.
            if (!IsSameConnection(current, connection))
            {
                ModLog.Warning("[ServerTransport] Client connection is a value type and matches the provided connection.");
                return false;
            }

            if (!Lifecycle.Handle(Lifecycle.Event.RemoveConnection, null))
            {
                ModLog.Warning("[ServerTransport] Failed to remove client from the session.");
                return false;
            }

            RemoveConnection(clientId);
            // Session.RemovePlayer(clientId);
            return true;
        }
    }
}
