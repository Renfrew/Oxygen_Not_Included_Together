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

    [Fact]
    public void PreservesLiteNetLibLanTransport()
    {
        Assert.Equal(1, TransportConfigurationMigration.NormalizeLanTransport(1));
    }

    [Fact]
    public void MigratesLegacyLanRiptideTransportToLiteNetLib()
    {
        Assert.Equal(1, TransportConfigurationMigration.NormalizeLanTransport(2));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public void FallsBackToLiteNetLibForUnknownLanTransport(int transport)
    {
        Assert.Equal(1, TransportConfigurationMigration.NormalizeLanTransport(transport));
    }

    [Fact]
    public void NormalizationIsIdempotentForBothPersistedSettings()
    {
        int networkTransport = 2;
        int lanTransport = 2;

        Assert.True(TransportConfigurationMigration.NormalizeTransportSettings(ref networkTransport, ref lanTransport));
        Assert.Equal(1, networkTransport);
        Assert.Equal(1, lanTransport);
        Assert.False(TransportConfigurationMigration.NormalizeTransportSettings(ref networkTransport, ref lanTransport));
    }

    [Theory]
    [InlineData(2, 1, 1, 1)]
    [InlineData(0, 2, 0, 1)]
    [InlineData(99, -1, 1, 1)]
    public void ReportsChangesForMigratedSettings(int networkTransport, int lanTransport, int expectedNetworkTransport, int expectedLanTransport)
    {
        Assert.True(TransportConfigurationMigration.NormalizeTransportSettings(ref networkTransport, ref lanTransport));
        Assert.Equal(expectedNetworkTransport, networkTransport);
        Assert.Equal(expectedLanTransport, lanTransport);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void ReportsNoChangesForValidSettings(int networkTransport, int lanTransport)
    {
        Assert.False(TransportConfigurationMigration.NormalizeTransportSettings(ref networkTransport, ref lanTransport));
        Assert.Equal(networkTransport, TransportConfigurationMigration.NormalizeNetworkTransport(networkTransport));
        Assert.Equal(lanTransport, TransportConfigurationMigration.NormalizeLanTransport(lanTransport));
    }
}
