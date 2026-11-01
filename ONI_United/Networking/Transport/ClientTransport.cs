using ONI_United.DebugTools;

namespace ONI_United.Networking.Transport
{
    // INACTIVE MIGRATION CONTRACT. Notifications describe native transport outcomes,
    // not Session admission or gameplay readiness. No host Lifecycle events are emitted.
    public abstract class ClientTransport
    {
        public event System.Action OnClientConnected;
        public event System.Action OnClientDisconnected;
        public event System.Action<string> OnConnectionFailed;

        public void Connect() => InternalConnect();
        public void Stop() => InternalStop();
        public void Poll() => InternalPoll();

        protected abstract void InternalConnect();
        protected abstract void InternalStop();
        protected abstract void InternalPoll();

        protected void OnConnected() => OnClientConnected?.Invoke();
        protected void OnDisconnected() => OnClientDisconnected?.Invoke();

        protected void OnConnectFailed(string reason)
        {
            DebugConsole.LogError("[ClientTransport] " + reason);
            OnConnectionFailed?.Invoke(reason);
        }

        protected void OnStopFailed(string reason)
        {
            DebugConsole.LogError("[ClientTransport] Native cleanup failed: " + reason);
        }
    }
}
