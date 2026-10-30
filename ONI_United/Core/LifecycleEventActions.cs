using System;
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
        internal static void Start()
        {
            if (Session.IsActive || Session.Players.Count != 0
                || GameClient.State != Networking.Sessions.States.ClientState.Disconnected)
                throw new InvalidOperationException("A multiplayer session or connection is already active.");
            if (Utils.IsInGame()) NetworkIdentityRegistry.RebuildFromScene();
            CursorManager.Instance.AssignColor();
            NetworkRuntime.InitializeHostTick();
        }

        internal static void PrepareSession()
        {
            if (!MultiplayerSession.RegisterAsHost())
                throw new InvalidOperationException("The local host Session could not be registered.");
        }

        internal static void Stop()
        {
            MultiplayerSession.Clear();
            NetworkIdentityRegistry.Clear();
            SelectToolPatch.UpdateColor();
            if (Session.IsActive || Session.Players.Count != 0)
                throw new InvalidOperationException("Host membership was not fully cleared.");
        }
    }
}
