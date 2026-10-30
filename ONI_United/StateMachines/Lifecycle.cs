using System;
using ONI_United.DebugTools;
using UnityEngine;
using ONI_United.Networking.Runtime;

namespace ONI_United.StateMachines
{
    // Persistent component on Multiplayer_Modules.
    internal sealed class Lifecycle : MonoBehaviour
    {
        internal enum Phase
        {
            Uninitialized,
            Idle,
            StartingHost,
            Hosting,
            Stopping
        }
        internal enum Event { Start, Started, Stop, Stopped }

        private static Lifecycle instance;
        private Phase current = Phase.Uninitialized;

        internal static Phase Current => instance.current;
        internal static bool HasHostOperation => Current == Phase.StartingHost
            || Current == Phase.Hosting || Current == Phase.Stopping;

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

        internal static void Initialize()
        {
            if (instance == null)
                throw new InvalidOperationException("The Lifecycle component has not been attached.");

            instance.Transition(Phase.Idle);
        }

        internal static bool Handle(Event evt, System.Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var owner = instance;

            switch (evt)
            {
                case Event.Start:
                    if (owner.current != Phase.Idle) return false;
                    owner.Transition(Phase.StartingHost);
                    break;
                case Event.Started:
                    if (owner.current != Phase.StartingHost) return false;
                    owner.Transition(Phase.Hosting);
                    break;
                case Event.Stop:
                    if (owner.current != Phase.StartingHost && owner.current != Phase.Hosting
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

            // Permissions/state are established before execution. Owners handle failures
            // and continuations; true reports invocation, never operation completion.
            action();
            return true;
        }

        private void Transition(Phase next)
        {
            current = next;
            DebugConsole.Log("[Lifecycle] " + next);
        }

        private void OnApplicationQuit()
        {
            if (ReferenceEquals(instance, this)) GameServer.DisposeLanHost();
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(instance, this)) return;
            try { GameServer.DisposeLanHost(); }
            finally { instance = null; }
        }
    }
}
