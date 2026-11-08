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

        public TransportServer CreateServer() => new SteamworksServerLegacy();
        public TransportClient CreateClient() => new SteamworksClientLegacy();
        public TransportPacketSender CreatePacketSender() => new SteamworksPacketSenderLegacy();

        public void Initialize() { }
        public void Shutdown() { }
    }
}
