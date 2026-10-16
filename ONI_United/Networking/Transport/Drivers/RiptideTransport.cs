using ONI_United.Networking.Transport.Riptide;
using ONI_United.Networking.Transport;

namespace ONI_United.Networking.Transport.Drivers
{
    public class RiptideTransport : ITransport
    {
        public TransportProtocol Protocol => TransportProtocol.Riptide;
        public string DisplayName => "Riptide (Legacy LAN)";
        public bool SupportsNativeFragmentation => false;
        public bool SupportsNatTraversal => false;

        public TransportServer CreateServer() => new RiptideServer();
        public TransportClient CreateClient() => new RiptideClient();
        public TransportPacketSender CreatePacketSender() => new RiptidePacketSender();

        public void Initialize() { }
        public void Shutdown() { }
    }
}
