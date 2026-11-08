using ONI_United.Core;
using ONI_United.DebugTools;

namespace ONI_United.Networking.Transports
{
    public abstract class ClientTransport : Transport
    {
        public void Connect()
        {
            ModLog.Info("[ClientTransport] Attempting to connect to host.");
            if (!Lifecycle.Handle(Lifecycle.Event.Join, InternalConnect))
                ModLog.Warning("[ClientTransport] Failed to join to host.");
        }

        public void Disconnect()
        {
            ModLog.Info("[ClientTransport] Attempting to disconnect from host.");
            if (!Lifecycle.Handle(Lifecycle.Event.ClearClientSession, InternalDisconnect))
                ModLog.Warning("[ClientTransport] Failed to disconnect from host.");
        }

        public void Poll()
        {
            InternalPoll();
        }

        protected abstract void InternalConnect();
        protected abstract void InternalDisconnect();
        protected abstract void InternalPoll();

        protected void OnConnected(ulong clientId, string playerName, object connection)
        {
            if (connection == null)
                return;
            
            if (TryGetConnection(clientId, out var current) && IsSameConnection(current, connection))
                return;
            
            if (current != null)
            {
                ModLog.Warning($"[ClientTransport] Client already has a different connection: {clientId}:{playerName}");
                return;
            }

            // TODO: update session
            if (!Lifecycle.Handle(Lifecycle.Event.PrepareClientSession))
            {
                ModLog.Warning("[ClientTransport] Failed to prepare client session.");
                return;
            }

            AddConnection(clientId, connection);

            ModLog.Info($"[ClientTransport] Connected to host.");
        }

        protected void OnConnectFail(string reason)
        {
            ModLog.Warning($"[ClientTransport] Failed to connect to host: {reason}");

            if (!Lifecycle.Handle(Lifecycle.Event.ClearClientSession))
                ModLog.Warning("[ClientTransport] Failed to clear client session.");
            
            if (!Lifecycle.Handle(Lifecycle.Event.SessionCleared))
                ModLog.Warning("[ClientTransport] Failed to handle session cleared.");
        }

        protected void OnDisconnect()
        {
            ModLog.Info("[ClientTransport] Connection lost from host.");

            if (!Lifecycle.Handle(Lifecycle.Event.ClearClientSession))
                ModLog.Warning("[ClientTransport] Failed to clear client session.");

            // Handle session cleared after disconnection initiated from the protocol layer
            if (!Lifecycle.Handle(Lifecycle.Event.SessionCleared))
                ModLog.Warning("[ClientTransport] Failed to handle disconnection.");
        }
        protected void OnDisconnected()
        {
            if (!Lifecycle.Handle(Lifecycle.Event.SessionCleared))
                ModLog.Warning("[ClientTransport] Failed to handle session cleared.");
            
            ModLog.Info("[ClientTransport] Disconnected from host.");
        }
    }
}
