namespace Shared.Networking
{
    public static class TransportConfigurationMigration
    {
        private const int SteamworksTransport = 0;
        private const int LiteNetLibTransport = 1;

        public static int NormalizeNetworkTransport(int transport) => transport switch
        {
            SteamworksTransport => SteamworksTransport,
            LiteNetLibTransport => LiteNetLibTransport,
            _ => LiteNetLibTransport
        };

    }
}
