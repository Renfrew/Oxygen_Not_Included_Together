using ONI_United.DebugTools;

namespace ONI_United.Networking.Transport
{
    // Notifications describe native transport outcomes,
    // not Session admission or gameplay readiness. No host Lifecycle events are emitted.
    public abstract class ClientTransport
    {
        // Identity for delayed application callbacks; native handles remain protocol-owned.
        internal object Operation { get; private set; }
        internal void BeginOperation() => Operation = new object();
        internal bool IsCurrentOperation(object operation) => operation != null && ReferenceEquals(operation, Operation);

        public System.Action OnClientConnected;
        public System.Action OnClientDisconnected;
        public System.Action<string> OnConnectionFailed;

        public void Connect() => InternalConnect();
        public void Stop() => InternalStop();
        public void Poll() => InternalPoll();

        // A false result retains native ownership for explicit cleanup retry.
        public abstract bool TryStop(out string error);

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
