using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ONI_United.UI.Screens.NetworkIndicatorsScreen;
using ONI_United.Networking.Runtime;

namespace ONI_United.Networking.Transport
{
    public abstract class TransportClient : ClientTransport
    {
        internal object Operation { get; private set; }
        internal void BeginOperation() => Operation = new object();
        internal bool IsCurrentOperation(object operation) => operation != null && ReferenceEquals(operation, Operation);

        /// <summary>
        /// Continue the connection flow
        /// </summary>
        public System.Action OnContinueConnectionFlow;

        /// <summary>
        /// Request the game state or return to the menu
        /// </summary>
        public System.Action OnRequestStateOrReturn;
        /// <summary>
        /// Request the client to return to the menu
        /// </summary>
        public System.Action<string, string> OnReturnToMenu;

        public abstract void Prepare();

        public void ConnectToHost(string ip, int port) => Core.NetworkRuntime.ConnectClient(ip: ip, port: port);
        internal void StartConnection(string ip, int port) => ConnectProtocol(ip, port);
        protected abstract void ConnectProtocol(string ip, int port);
        protected override void InternalConnect() => GameClient.BeginConnection();
        protected override void InternalDisconnect()
        {
            var operation = Core.NetworkRuntime.ActiveOperation;
            Core.NetworkRuntime.CleanupClientTransport(this, operation, () => OnDisconnected(operation));
        }
        protected override void InternalPoll() => Update();
        public abstract bool TryStop(out string error);

        public abstract object CurrentConnection { get; }

        public abstract bool IsCurrentConnection(object connection);

        public void ReconnectToSession() => Core.NetworkRuntime.ReconnectClient();

        public abstract void Update();

        public abstract void OnMessageRecieved();

        // Network health functions

        public abstract int GetPing();

        public abstract NetworkState GetJitterState();

        public abstract NetworkState GetLatencyState();

        public abstract NetworkState GetPacketlossState();

        public abstract NetworkState GetServerPerformanceState();

        // Bandwidth tracking (bytes/sec, packets/sec)
        public virtual float IncomingBandwidth => 0f;
        public virtual float OutgoingBandwidth => 0f;
        public virtual int IncomingPps => 0;
        public virtual int OutgoingPps => 0;
    }
}
