using Shared.Networking;
using Xunit;

namespace Shared.Tests.Configuration;

public sealed class TransportConfigurationMigrationTests
{
    [Fact]
    public void PreservesSteamNetworkTransport()
    {
        Assert.Equal(0, TransportConfigurationMigration.NormalizeNetworkTransport(0));
    }

    [Fact]
    public void PreservesLiteNetLibNetworkTransport()
    {
        Assert.Equal(1, TransportConfigurationMigration.NormalizeNetworkTransport(1));
    }

    [Fact]
    public void MigratesLegacyNetworkRiptideTransportToLiteNetLib()
    {
        Assert.Equal(1, TransportConfigurationMigration.NormalizeNetworkTransport(2));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(99)]
    [InlineData(-1)]
    public void FallsBackToLiteNetLibForUnknownNetworkTransport(int transport)
    {
        Assert.Equal(1, TransportConfigurationMigration.NormalizeNetworkTransport(transport));
    }

}
