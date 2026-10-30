using ONI_United.Core;
using ONI_United.Misc;
using ONI_United.Networking;
using ONI_United.Networking.Sessions.States;
using Steamworks;

namespace ONI_United.Networking.Sessions;

public class MultiplayerPlayer : Player
{
	public string PlayerName
	{
		get => UserName;
		set => UserName = value ?? string.Empty;
	}
	public bool IsLocal => PlayerId == NetworkRuntime.GetLocalID();

	public int AvatarImageId { get; private set; } = -1;
	public object Connection { get; set; } = null;
	public bool IsConnected => Connection != null;
	public bool ProtocolVerified { get; set; }

	public ClientReadyState readyState = ClientReadyState.Unready;

    public MultiplayerPlayer(ulong playerId)
		: base(playerId)
	{
		if (playerId == MultiplayerSession.HostUserID)
			readyState = ClientReadyState.Ready;
		ProtocolVerified = IsLocal;
		if(NetworkRuntime.IsLanConfig())
		{
            PlayerName = $"Player {playerId}";
            return;
        }

		PlayerName = Utils.TrucateName(SteamFriends.GetFriendPersonaName(playerId.AsCSteamID()));
		AvatarImageId = SteamFriends.GetLargeFriendAvatar(playerId.AsCSteamID());
	}

	internal MultiplayerPlayer(ulong playerId, string userName)
		: this(playerId)
	{
		UserName = userName ?? string.Empty;
	}

	public override string ToString()
	{
		return $"{PlayerName} ({PlayerId})";
	}
}
