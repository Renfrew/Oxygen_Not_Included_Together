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

        public static int NormalizeLanTransport(int transport) =>
            transport == LiteNetLibTransport ? transport : LiteNetLibTransport;

        public static bool NormalizeTransportSettings(ref int networkTransport, ref int lanTransport)
        {
            int normalizedNetworkTransport = NormalizeNetworkTransport(networkTransport);
            int normalizedLanTransport = NormalizeLanTransport(lanTransport);
            bool changed = normalizedNetworkTransport != networkTransport || normalizedLanTransport != lanTransport;

            networkTransport = normalizedNetworkTransport;
            lanTransport = normalizedLanTransport;
            return changed;
        }
    }
}
