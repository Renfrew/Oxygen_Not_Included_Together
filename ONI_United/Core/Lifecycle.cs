using ONI_United.DebugTools;
using UnityEngine;

namespace ONI_United.Core
{
    // Persistent component on Multiplayer_Modules.
    internal sealed class Lifecycle : MonoBehaviour
    {
        internal enum Phase
        {
            Uninitialized,
            Idle,
            StartingHost,
            PrepareSession,
            Hosting,
            Stopping
        }
        internal enum Event { Start, TransportStarted, SessionPrepared, Stop, Stopped }

        private static Lifecycle instance = null!;
        private Phase current = Phase.Uninitialized;

        internal static Phase Current => instance.current;
        internal static bool HasHostOperation => Current == Phase.StartingHost
            || Current == Phase.PrepareSession || Current == Phase.Hosting || Current == Phase.Stopping;

        private void Awake()
        {
            if (instance != null && !ReferenceEquals(instance, this))
            {
                enabled = false;
                Destroy(this);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(instance, this))
                instance = null!;
        }

        internal static void Initialize()
        {
            if (instance == null)
                throw new System.InvalidOperationException("The Lifecycle component has not been attached.");

            instance.Transition(Phase.Idle);
        }

        internal static bool Handle(Event evt, System.Action callback)
        {
            var owner = instance;

            switch (evt)
            {
                case Event.Start:
                    if (owner.current != Phase.Idle) return false;
                    owner.Transition(Phase.StartingHost);
                    break;
                case Event.TransportStarted:
                    if (owner.current != Phase.StartingHost) return false;
                    owner.Transition(Phase.PrepareSession);
                    break;
                case Event.SessionPrepared:
                    if (owner.current != Phase.PrepareSession) return false;
                    owner.Transition(Phase.Hosting);
                    break;
                case Event.Stop:
                    if (owner.current != Phase.StartingHost && owner.current != Phase.PrepareSession && owner.current != Phase.Hosting
                        && owner.current != Phase.Stopping) return false;
                    if (owner.current != Phase.Stopping) owner.Transition(Phase.Stopping);
                    break;
                case Event.Stopped:
                    if (owner.current != Phase.Stopping) return false;
                    owner.Transition(Phase.Idle);
                    break;
                default:
                    return false;
            }

            try
            {
                EventAction(evt);
                callback?.Invoke();
                return true;
            }
            catch (System.Exception ex)
            {
                DebugConsole.LogError($"[Lifecycle] Event {evt} failed in phase {Current}: {ex}");
                throw;
            }
        }

        private static void EventAction(Event evt)
        {
            switch (evt)
            {
                case Event.Start:
                    LifecycleEventActions.Start();
                    break;
                case Event.TransportStarted:
                    LifecycleEventActions.PrepareSession();
                    break;
                case Event.Stop:
                    LifecycleEventActions.Stop();
                    break;
            }
        }

        private void Transition(Phase next)
        {
            current = next;
            DebugConsole.Log("[Lifecycle] " + next);
        }
    }
}
