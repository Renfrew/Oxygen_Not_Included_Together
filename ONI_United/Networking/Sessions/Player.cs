namespace ONI_United.Networking.Sessions;

public class Player
{
    internal Player(ulong playerId, string userName = "")
    {
        PlayerId = playerId;
        UserName = userName;
    }

    public ulong PlayerId { get; }
    public string UserName { get; internal set; }
}
