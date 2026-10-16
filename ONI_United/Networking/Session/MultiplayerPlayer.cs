using ONI_United.Misc;
using ONI_United.Networking;
using ONI_United.Networking.Session.States;
using Steamworks;

namespace ONI_United.Networking.Session;

public class MultiplayerPlayer
{
	public ulong PlayerId { get; private set; }
	public string PlayerName { get; set; }
	public bool IsLocal => PlayerId == NetworkConfig.GetLocalID();

	public int AvatarImageId { get; private set; } = -1;
	public object Connection { get; set; } = null;
	public bool IsConnected => Connection != null;
	public bool ProtocolVerified { get; set; }

	public ClientReadyState readyState = ClientReadyState.Unready;

    public MultiplayerPlayer(ulong playerId)
	{
		PlayerId = playerId;
		if (playerId == MultiplayerSession.HostUserID)
			readyState = ClientReadyState.Ready;
		ProtocolVerified = IsLocal;
		if(NetworkConfig.IsLanConfig())
		{
            PlayerName = $"Player {playerId}";
            return;
        }

		PlayerName = Utils.TrucateName(SteamFriends.GetFriendPersonaName(playerId.AsCSteamID()));
		AvatarImageId = SteamFriends.GetLargeFriendAvatar(playerId.AsCSteamID());
	}

	public override string ToString()
	{
		return $"{PlayerName} ({PlayerId})";
	}
}
