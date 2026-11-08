using ONI_United.Networking.Transports.Steamworks;
using ONI_United.Networking.Transports;

namespace ONI_United.Networking.Transports.Drivers
{
    public class SteamworksTransport : ITransport
    {
        public TransportProtocol Protocol => TransportProtocol.Steamworks;
        public string DisplayName => "Steamworks P2P";
        public bool SupportsNativeFragmentation => true;
        public bool SupportsNatTraversal => true;

        public TransportServer CreateServer() => new SteamworksServer();
        public TransportClient CreateClient() => new SteamworksClient();
        public TransportPacketSender CreatePacketSender() => new SteamworksPacketSender();

        public void Initialize() { }
        public void Shutdown() { }
    }
}
