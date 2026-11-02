#nullable disable
using System.Reflection;
using ONI_United.Core;
using ONI_United.Misc.World;
using ONI_United.Networking.Runtime;
using ONI_United.Networking.Sessions;
using Xunit;
using static Shared.Tests.Networking.LifecycleTestEnvironment;

namespace Shared.Tests.Networking;

[Collection("Lifecycle")]
public sealed class LifecycleSynchronizationTests : IDisposable
{
    private readonly Lifecycle machine = new();

    public LifecycleSynchronizationTests()
    {
        Reset();
        Invoke("Awake");
        Lifecycle.Initialize();
    }

    private void Invoke(string name) => typeof(Lifecycle).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(machine, null);

    public void Dispose()
    {
        Session.Reset();
        Invoke("OnDestroy");
    }

    private static void JoinSession()
    {
        var client = NetworkRuntime.TransportClient;
        Assert.True(Lifecycle.Handle(Lifecycle.Event.Join));
        NetworkRuntime.TransportEstablished(client);
        Assert.True(Session.AdmitClient(new Player(20), new Player(10)));
        Assert.True(NetworkRuntime.ClientAdmitted(client));
    }

    [Fact]
    public void ClientSynchronizationProducerStillRequiresWorldSpawnReadiness()
    {
        JoinSession();
        Assert.True(GameClient.BeginSynchronization(7));
        Assert.True(GameClient.SubmitSynchronizationSave(7, new WorldSave(), false));
        LoadedWorld = false;
        GameClient.OnWorldSpawnComplete();
        Assert.DoesNotContain("ready:Ready", Trace);
        Assert.False(GameClient.CompleteSynchronization(7));
        Assert.Equal(Lifecycle.Phase.Synchronizing, Lifecycle.Current);
    }

    [Fact]
    public void ClientLoadedWorldDoesNotGrantGameplayBeforeHostCompletion()
    {
        JoinSession();
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartHostSync));
        Assert.True(GameClient.BeginSynchronization(7));
        Assert.False(GameClient.CompleteSynchronization(7));
        Assert.True(GameClient.SubmitSynchronizationSave(7, new WorldSave(), false));
        GameClient.OnWorldSpawnComplete();
        Assert.Contains("ready:Ready", Trace);
        Assert.Equal(Lifecycle.Phase.Synchronizing, Lifecycle.Current);
        Assert.True(GameClient.CompleteSynchronization(7));
        Assert.Equal(Lifecycle.Phase.InGameSession, Lifecycle.Current);
        Assert.True(Session.IsClient);
        Assert.False(Lifecycle.IsHostGameplay);
        Assert.False(Lifecycle.Handle(Lifecycle.Event.StartHostSync));
        Assert.True(GameClient.BeginSynchronization(8));
    }
}
