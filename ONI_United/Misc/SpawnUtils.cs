using ONI_United.Networking.Session;
using ONI_United.Networking.Identity;
using ONI_United.Networking.Packets.World;
using UnityEngine;
using ONI_United.Networking.Packets.Architecture;

namespace ONI_United.Misc;

public static class SpawnUtils
{
    private static NetworkIdentity AssignIdentity(GameObject go)
    {
        var identity = go.AddOrGet<NetworkIdentity>();
        if (identity.NetId == 0)
            identity.RegisterIdentity();
        return identity;
    }
    
    /// <summary>
    /// Gives an object the host has already spawned a <see cref="NetworkIdentity"/> and broadcasts a
    /// <see cref="SpawnPrefabPacket"/> so the clients spawn it too. For objects the game creates
    /// itself, e.g. <c>Scenario.SpawnPrefab</c> (see ScenarioSpawnPrefabPatch).
    /// </summary>
    /// <param name="go">The GameObject the host has spawned.</param>
    /// <param name="isActive">Whether the clients should spawn it active.</param>
    /// <returns>The object's NetId, or 0 if it could not be registered or not the host.</returns>
    public static int BroadcastSpawn(GameObject go, bool isActive = true)
    {
        if (!MultiplayerSession.IsHost) return 0;
        
        if (go == null)
            return 0;

        var identity = AssignIdentity(go);
        if (identity.NetId == 0)
            return 0;

        SpawnPrefabPacket packet = new SpawnPrefabPacket(identity.NetId, go.PrefabID().GetHashCode(), go.transform.position);
        packet.IsActive = isActive;
        packet.SetPrimaryData(go.GetComponent<PrimaryElement>());
        PacketSender.SendToAllClients(packet);
        return identity.NetId;
    }

}