namespace ONI_United.Networking.Transport
{
    public abstract class ClientTransport
    {
        public void Connect()
        {
            InternalConnect();
        }

        public void Disconnect()
        {
            InternalDisconnect();
        }

        public void Poll()
        {
            InternalPoll();
        }

        protected abstract void InternalConnect();
        protected abstract void InternalDisconnect();
        protected abstract void InternalPoll();

        protected void OnConnected(ulong clientId, string playerName)
        {
        }

        protected void OnConnectFail(string reason)
        {
        }

        protected void OnDisconnect()
        {
        }
        protected void OnDisconnected()
        {
        }
    }
}
