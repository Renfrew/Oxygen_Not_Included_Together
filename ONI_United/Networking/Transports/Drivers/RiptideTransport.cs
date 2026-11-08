using ONI_United.Networking.Transports.Riptide;
using ONI_United.Networking.Transports;

namespace ONI_United.Networking.Transports.Drivers
{
    public class RiptideTransport : ITransport
    {
        public TransportProtocol Protocol => TransportProtocol.Riptide;
        public string DisplayName => "Riptide (Legacy LAN)";
        public bool SupportsNativeFragmentation => false;
        public bool SupportsNatTraversal => false;

        public TransportServer CreateServer() => new RiptideServerLegacy();
        public TransportClient CreateClient() => new RiptideClientLegacy();
        public TransportPacketSender CreatePacketSender() => new RiptidePacketSenderLegacy();

        public void Initialize() { }
        public void Shutdown() { }
    }
}
