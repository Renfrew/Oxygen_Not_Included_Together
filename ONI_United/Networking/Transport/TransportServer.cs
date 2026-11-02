using System;
using ONI_United.DebugTools;
using ONI_United.Networking.Sessions;
using ONI_United.Core;

namespace ONI_United.Networking.Transport
{
    public abstract class TransportServer
    {
        private Action<bool, string> stopCompletion;
        public string OperationError { get; private set; }

        internal void Start()
        {
            var operation = NetworkRuntime.ActiveOperation;
            if (!NetworkRuntime.IsCurrentOperation(this, operation)
                || Lifecycle.Current != Lifecycle.Phase.StartingHost) return;
            OperationError = null;
            if (!Misc.Utils.IsInGame() || Grid.CellCount <= 0)
            {
                ReportStartupFailure(operation, "Load a colony before starting a multiplayer host.");
                return;
            }
            StartProtocol(operation);
        }

        protected abstract void StartProtocol(object operation);
        protected abstract void CancelProtocol(Action<bool, string> completed);
        protected abstract void DisposeProtocol();
        public virtual void PollCancellation() { }

        protected void ReportTransportStarted(object operation)
        {
            if (!NetworkRuntime.IsCurrentOperation(this, operation)) return;
            Lifecycle.Handle(Lifecycle.Event.PrepareHostSession, () =>
            {
                if (NetworkRuntime.IsCurrentOperation(this, operation) && Session.IsHost
                    && Lifecycle.Current == Lifecycle.Phase.InGameSession)
                    OnSessionPlayerAdmitted(Session.LocalPlayerId);
            });
        }

        protected void ReportStartupFailure(object operation, string reason)
        {
            if (!NetworkRuntime.IsCurrentOperation(this, operation)
                || Lifecycle.Current == Lifecycle.Phase.ClearingSession) return;
            OperationError = string.IsNullOrEmpty(reason) ? "Host startup failed." : reason;
            DebugConsole.LogError("[TransportServer] " + OperationError);
            NetworkRuntime.Stop();
        }

        internal void Stop()
        {
            if (stopCompletion == null)
                stopCompletion = CompleteStop;
            // The protocol keeps the first unresolved completion and coalesces
            // repeated cancellation. A reported failure allows explicit retry.
            CancelProtocol(stopCompletion);
        }

        private void CompleteStop(bool success, string error)
        {
            stopCompletion = null;
            if (!success)
            {
                OperationError = string.IsNullOrEmpty(error) ? "Host cleanup failed." : error;
                DebugConsole.LogError("[TransportServer] " + OperationError);
                return;
            }
            // SessionCleared has no application notification; its EventAction clears ownership.
            if (Lifecycle.Handle(Lifecycle.Event.SessionCleared))
                OperationError = null;
        }

        internal void DisposeHost()
        {
            stopCompletion = null;
            NetworkRuntime.InvalidateOperation(this);
            DisposeProtocol();
        }

        public abstract void CloseConnections();
        public abstract bool TryGetRemotePlayerId(object connection, out ulong playerId);
        public abstract void DisconnectConnection(object connection, string reason);
        public virtual void OnSessionPlayerAdmitted(ulong playerId) { }
        public abstract void Update();
        public abstract void OnMessageRecieved();
        public abstract void KickClient(ulong clientId);
        public virtual float IncomingBandwidth => 0f;
        public virtual float OutgoingBandwidth => 0f;
        public virtual int IncomingPps => 0;
        public virtual int OutgoingPps => 0;
    }
}
