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
            OperationError = null;
            StartProtocol();
        }

        protected abstract void StartProtocol();
        protected abstract void CancelProtocol(Action<bool, string> completed);
        protected abstract void DisposeProtocol();
        public virtual void PollCancellation() { }

        protected void ReportTransportStarted()
        {
            Lifecycle.Handle(Lifecycle.Event.TransportStarted, () =>
            {
                // Registration notifications may synchronously Stop. Admission of the
                // following event, rather than a duplicate phase check, decides completion.
                Lifecycle.Handle(Lifecycle.Event.SessionPrepared, () =>
                {
                    OnSessionPlayerAdmitted(Session.LocalPlayerId);
                });
            });
        }

        protected void ReportStartupFailure(string reason)
        {
            OperationError = string.IsNullOrEmpty(reason) ? "Host startup failed." : reason;
            DebugConsole.LogError("[TransportServer] " + OperationError);
            NetworkRuntime.Stop();
        }

        internal void Stop()
        {
            if (stopCompletion == null)
            {
                Action<bool, string> completion = null;
                completion = (success, error) => CompleteStop(completion, success, error);
                stopCompletion = completion;
            }
            // The protocol keeps the first unresolved completion and coalesces
            // repeated cancellation. A reported failure allows explicit retry.
            CancelProtocol(stopCompletion);
        }

        private void CompleteStop(Action<bool, string> completion, bool success, string error)
        {
            if (!ReferenceEquals(stopCompletion, completion)) return;
            stopCompletion = null;
            if (!success)
            {
                OperationError = string.IsNullOrEmpty(error) ? "Host cleanup failed." : error;
                DebugConsole.LogError("[TransportServer] " + OperationError);
                return;
            }
            Lifecycle.Handle(Lifecycle.Event.Stopped, () =>
            {
                OperationError = null;
            });
        }

        internal void DisposeHost()
        {
            stopCompletion = null;
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
