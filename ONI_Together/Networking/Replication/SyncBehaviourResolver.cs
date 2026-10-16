using ONI_United.Networking.Identity;
using ONI_United.Networking.Presentation;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Synchronization;
using ONI_United.Networking.Replication.Components;

namespace ONI_United.Networking.Replication
{
    /// <summary>
    /// Resolves a behaviour for an inbound OxySync packet, checking the manager registry first
    /// and falls back to the entity's NetworkIdentity components.
    /// </summary>
    internal static class SyncBehaviourResolver
    {
        public static bool TryResolve(int netId, int behaviourId, out ISyncBehaviour behaviour)
        {
            if (OxySyncManager.TryGetSyncBehaviour(netId, behaviourId, out behaviour) && behaviour != null)
                return true;

            // Native fallback: behaviour may not have registered (e.g. spawned before manager).
            // Match by behaviour id: an object usually carries several NetworkBehaviours, and
            // the first one is not the addressee (on a duplicant it is AnimSyncer, which has
            // no sync vars, so a position update handed to it is dropped without a word).
            if (NetworkIdentityRegistry.TryGet(netId, out var identity)
                && !identity.IsNullOrDestroyed() && !identity.gameObject.IsNullOrDestroyed())
            {
                foreach (var native in identity.gameObject.GetComponents<Shared.Replication.NetworkBehaviour>())
                {
                    if (native.IsNullOrDestroyed() || native.BehaviourId != behaviourId)
                        continue;
                    behaviour = new NativeSyncBehaviour(native);
                    return true;
                }
            }
            behaviour = null;
            return false;
        }
    }
}
