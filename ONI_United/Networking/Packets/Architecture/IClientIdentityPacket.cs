namespace ONI_United.Networking.Packets.Architecture
{
	internal interface IClientIdentityPacket
	{
		ulong ClaimedSenderId { get; }
	}
}
