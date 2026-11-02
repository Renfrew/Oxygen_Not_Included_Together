using System;
using System.Collections.Generic;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Sessions;
using ONI_United.Patches.ToolPatches;
using ONI_United.Misc;
using ONI_United.Networking.Presentation;

namespace ONI_United.Core
{
    internal static class LifecycleEventActions
    {
        private static readonly Dictionary<Lifecycle.Event, Action<Lifecycle.Phase>> actions = new()
        {
            [Lifecycle.Event.StartHost] = StartHost,
            [Lifecycle.Event.PrepareHostSession] = PrepareHostSession,
            [Lifecycle.Event.PrepareClientSession] = PrepareClientSession,
            [Lifecycle.Event.HostSessionReady] = HostSessionReady,
            [Lifecycle.Event.Join] = Join,
            [Lifecycle.Event.ClearHostSession] = ClearHostSession,
            [Lifecycle.Event.ClearClientSession] = ClearClientSession,
            [Lifecycle.Event.SessionCleared] = SessionCleared
        };

        internal static void Execute(Lifecycle.Event evt, Lifecycle.Phase source)
        {
            if (actions.TryGetValue(evt, out var action))
                action(source);
        }

        private static void StartHost(Lifecycle.Phase source)
        {
            NetworkRuntime.BeginHostOperation();
            var operation = NetworkRuntime.ActiveOperation;
            var owner = NetworkRuntime.TransportServer;
            if (Session.IsActive || Session.Players.Count != 0)
                throw new InvalidOperationException("A multiplayer session or connection is already active.");
            if (Utils.IsInGame()) NetworkIdentityRegistry.RebuildFromScene();
            if (!NetworkRuntime.IsCurrentOperation(owner, operation)) return;
            CursorManager.Instance.AssignColor();
            if (!NetworkRuntime.IsCurrentOperation(owner, operation)) return;
            NetworkRuntime.InitializeHostTick();
            NetworkRuntime.StartHostTransport(operation);
        }

        private static void PrepareHostSession(Lifecycle.Phase source)
        {
            if (source != Lifecycle.Phase.StartingHost)
                throw new InvalidOperationException("Invalid host preparation source: " + source + ".");
            var owner = NetworkRuntime.TransportServer;
            var operation = NetworkRuntime.ActiveOperation;
            if (!MultiplayerSession.RegisterAsHost())
                throw new InvalidOperationException("The local host Session could not be registered.");

            NetworkRuntime.HostSessionReady(owner, operation);
        }

        private static void PrepareClientSession(Lifecycle.Phase source)
        {
            if (source != Lifecycle.Phase.Joining)
                throw new InvalidOperationException("Invalid client preparation source: " + source + ".");
            GameClient.PrepareSession();
        }

        private static void HostSessionReady(Lifecycle.Phase source)
        {
            SelectToolPatch.UpdateColor();
        }

        private static void Join(Lifecycle.Phase source) => NetworkRuntime.BeginClientOperation();

        private static void SessionCleared(Lifecycle.Phase source) => NetworkRuntime.ClearOperation();

        private static void ClearClientSession(Lifecycle.Phase source)
        {
            var owner = NetworkRuntime.TransportClient;
            var operation = NetworkRuntime.ActiveOperation;
            GameClient.ResetSynchronizationState();
            MultiplayerSession.Clear();
            if (!NetworkRuntime.IsCurrentOperation(owner, operation)) return;
            NetworkIdentityRegistry.Clear();
            NetworkRuntime.TransportPacketSender.ClearPending();
            SelectToolPatch.UpdateColor();
        }

        private static void ClearHostSession(Lifecycle.Phase source)
        {
            var owner = NetworkRuntime.TransportServer;
            var operation = NetworkRuntime.ActiveOperation;
            MultiplayerSession.Clear();
            if (!NetworkRuntime.IsCurrentOperation(owner, operation)) return;
            NetworkIdentityRegistry.Clear();
            NetworkRuntime.TransportPacketSender.ClearPending();
            SelectToolPatch.UpdateColor();
            if (Session.IsActive || Session.Players.Count != 0)
                throw new InvalidOperationException("Host membership was not fully cleared.");
        }
    }
}
