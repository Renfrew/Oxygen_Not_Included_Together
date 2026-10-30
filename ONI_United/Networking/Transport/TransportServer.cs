using System;
using ONI_United.DebugTools;
using ONI_United.Networking.Sessions;
using ONI_United.Core;

namespace ONI_United.Networking.Transport
{
    public abstract class TransportServer
    {
        private System.Action hostReady;
        private System.Action afterStop;
        private Action<bool, string> stopCompletion;
        public string OperationError { get; private set; }

        public bool Start(System.Action onStarted = null, System.Action configure = null)
        {
            return Lifecycle.Handle(Lifecycle.Event.Start, () =>
            {
                OperationError = null;
                hostReady = onStarted;
                configure?.Invoke();
                StartProtocol();
            });
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
                    var ready = hostReady;
                    hostReady = null;
                    ready?.Invoke();
                });
            });
        }

        protected void ReportStartupFailure(string reason)
        {
            OperationError = string.IsNullOrEmpty(reason) ? "Host startup failed." : reason;
            DebugConsole.LogError("[TransportServer] " + OperationError);
            Stop();
        }

        public bool Stop(System.Action onStopped = null)
        {
            try
            {
                return Lifecycle.Handle(Lifecycle.Event.Stop, () =>
                {
                    hostReady = null;
                    if (stopCompletion == null)
                    {
                        afterStop = onStopped;
                        Action<bool, string> completion = null;
                        completion = (success, error) => CompleteStop(completion, success, error);
                        stopCompletion = completion;
                    }
                    // The protocol keeps the first unresolved completion and coalesces
                    // repeated cancellation. A reported failure allows explicit retry.
                    CancelProtocol(stopCompletion);
                });
            }
            catch
            {
                // An essential cleanup failure invalidates even an older in-flight
                // completion: its later native success cannot establish complete cleanup.
                stopCompletion = null;
                afterStop = null;
                hostReady = null;
                throw;
            }
        }

        public bool StopAndLoad(string filename) => Stop(() =>
        {
            MultiplayerSession.ShouldHostAfterLoad = true;
            LoadScreen.DoLoad(filename);
        });

        private void CompleteStop(Action<bool, string> completion, bool success, string error)
        {
            if (!ReferenceEquals(stopCompletion, completion)) return;
            stopCompletion = null;
            var continuation = afterStop;
            afterStop = null;
            if (!success)
            {
                OperationError = string.IsNullOrEmpty(error) ? "Host cleanup failed." : error;
                DebugConsole.LogError("[TransportServer] " + OperationError);
                return;
            }
            Lifecycle.Handle(Lifecycle.Event.Stopped, () =>
            {
                OperationError = null;
                continuation?.Invoke();
            });
        }

        internal void DisposeHost()
        {
            hostReady = null;
            afterStop = null;
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
