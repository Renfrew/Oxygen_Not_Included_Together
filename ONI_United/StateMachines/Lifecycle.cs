using System;
using ONI_United.DebugTools;
using UnityEngine;
using ONI_United.Networking.Runtime;
using ONI_United.Networking;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Sessions;
using ONI_United.Patches.ToolPatches;
using ONI_United.Misc;
using ONI_United.Networking.Presentation;

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
            PrepareSession,
            Hosting,
            Stopping
        }
        internal enum Event { Start, TransportStarted, SessionPrepared, Stop, Stopped }

        private static Lifecycle instance;
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

        internal static void Initialize()
        {
            if (instance == null)
                throw new InvalidOperationException("The Lifecycle component has not been attached.");

            instance.Transition(Phase.Idle);
        }

        internal static bool Handle(Event evt, System.Action callback = null)
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
            catch (Exception ex)
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
                    if (Session.IsActive || Session.Players.Count != 0
                        || GameClient.State != Networking.Sessions.States.ClientState.Disconnected)
                        throw new InvalidOperationException("A multiplayer session or connection is already active.");
                    if (Utils.IsInGame()) NetworkIdentityRegistry.RebuildFromScene();
                    CursorManager.Instance.AssignColor();
                    NetworkingComponent.InitializeHostTick();
                    break;
                case Event.TransportStarted:
                    if (!MultiplayerSession.RegisterAsHost())
                        throw new InvalidOperationException("The local host Session could not be registered.");
                    break;
                case Event.Stop:
                    MultiplayerSession.Clear();
                    NetworkIdentityRegistry.Clear();
                    SelectToolPatch.UpdateColor();
                    if (Session.IsActive || Session.Players.Count != 0)
                        throw new InvalidOperationException("Host membership was not fully cleared.");
                    break;
            }
        }

        private void Transition(Phase next)
        {
            current = next;
            DebugConsole.Log("[Lifecycle] " + next);
        }

        private static void DisposeHost()
        {
            if (!HasHostOperation) return;
            // Lifetime release is immediate and repeat-safe, independent of another Update.
            // Drop application continuations even if essential cleanup throws.
            var server = NetworkConfig.TransportServer;
            try { EventAction(Event.Stop); }
            finally { server.DisposeHost(); }
        }

        private void OnApplicationQuit()
        {
            if (ReferenceEquals(instance, this)) DisposeHost();
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(instance, this)) return;
            try { DisposeHost(); }
            finally { instance = null; }
        }
    }
}
