using System.Collections.Generic;
using ONI_United.DebugTools;
using ONI_United.Networking.Sessions;
using UnityEngine;

namespace ONI_United.Core
{
    // Persistent component on Multiplayer_Modules.
    internal sealed partial class Lifecycle : MonoBehaviour
    {
        internal enum Phase
        {
            Uninitialized,
            Idle,
            StartingHost,
            PreparingSession,
            InGameSession,
            Joining,
            Joined,
            Synchronizing,
            ClearingSession
        }

        private static readonly Dictionary<(Lifecycle.Event, Phase), Phase> TransitionLookup = new();

        static Lifecycle()
        {
            foreach (var entry in Lifecycle.Transitions)
            {
                if (!System.Enum.IsDefined(typeof(Lifecycle.Event), entry.Key))
                    throw new System.InvalidOperationException("Undefined Lifecycle event in transition table.");
                foreach (var transition in entry.Value)
                {
                    if (transition.From.Length == 0 || !System.Enum.IsDefined(typeof(Phase), transition.To))
                        throw new System.InvalidOperationException("Invalid Lifecycle transition for " + entry.Key + ".");
                    foreach (var source in transition.From)
                    {
                        if (!System.Enum.IsDefined(typeof(Phase), source))
                            throw new System.InvalidOperationException("Undefined Lifecycle source phase for " + entry.Key + ".");
                        TransitionLookup.Add((entry.Key, source), transition.To);
                    }
                }
            }
        }

        private static Lifecycle instance = null!;
        private Phase current = Phase.Uninitialized;

        internal static bool IsAvailable => instance != null;
        internal static Phase Current => instance.current;
        internal static bool CanSelectTransport => Current == Phase.Idle;
        internal static bool IsHostGameplay => Session.IsHost
            && (Current == Phase.InGameSession || Current == Phase.Synchronizing);

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

            if (instance.current != Phase.Uninitialized)
                throw new System.InvalidOperationException("Lifecycle was already initialized.");
            Handle(Event.Initialize);
        }

        internal static bool CanHandle(Event evt) =>
            TransitionLookup.ContainsKey((evt, instance.current));

        private static readonly System.Action<System.Action> InvokeAction = static action => action?.Invoke();
        internal static bool Handle(Event evt, System.Action afterAction = null) =>
            Handle(evt, InvokeAction, afterAction);

        // No beforeAction is required: readiness/admission producers already own their
        // prerequisites before submitting an event. Preserve the existing callback-last API.
        internal static bool Handle<T>(Event evt, System.Action<T> afterAction, T arg = default)
        {
            var owner = instance;
            var source = owner.current;
            if (!TransitionLookup.TryGetValue((evt, source), out var destination)) return false;

            switch (evt)
            {
                case Event.StartHost:
                case Event.Join:
                    if (Session.IsActive || Session.Players.Count != 0) return false;
                    break;
                case Event.SessionReady:
                    if (!Session.IsHost) return false;
                    break;
                case Event.ClientAdmitted:
                    if (!Session.IsClient) return false;
                    break;
                case Event.StartHostSync:
                    if (!Session.IsHost) return false;
                    break;
                case Event.StartClientSync:
                    if (!Session.IsClient) return false;
                    break;
                case Event.CompleteSync:
                    if (!(Session.IsHost || Session.IsClient)) return false;
                    break;
            }

            // Repeated clears are self-transitions and emit no additional phase-change log.
            if (source != destination) owner.Transition(destination);

            try
            {
                Execute(evt, source);
                afterAction?.Invoke(arg);
                return true;
            }
            catch (System.Exception ex)
            {
                DebugConsole.LogError($"[Lifecycle] Event {evt} failed in phase {Current}: {ex}");
                throw;
            }
        }

        private void Transition(Phase next)
        {
            current = next;
            DebugConsole.Log("[Lifecycle] " + next);
        }
    }
}
