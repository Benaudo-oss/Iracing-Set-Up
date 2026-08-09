using IracingSetupManager.Core.Setups;
using IracingSetupManager.Integrations.Garage61;
using Xunit;

namespace IracingSetupManager.Core.Tests;

public sealed class Garage61ExporterTests
{
    [Fact]
    public async Task SendsApprovedValidatedSetupsUsingSimulatedClient()
    {
        var client = new SimulatedGarage61Client();
        var setups = new[] { CreateSetup(), CreateSetup() };
        await new Garage61Exporter(client, new Garage61ExportPolicy()).ExportValidatedSetupsAsync(setups);
        Assert.Equal(setups.Select(item => item.Id), client.Uploaded.Select(item => item.Id));
    }

    [Fact]
    public async Task RejectsEntireBatchBeforeSendingIfOneSetupIsNotAllowed()
    {
        var client = new SimulatedGarage61Client();
        var invalid = CreateSetup() with { IsPrivate = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Garage61Exporter(client, new Garage61ExportPolicy()).ExportValidatedSetupsAsync([CreateSetup(), invalid]));
        Assert.Empty(client.Uploaded);
    }

    private static SetupFile CreateSetup() => new(Guid.NewGuid(), "race.sto", new string('a', 64), 10, @"C:\Archive\race.sto", SetupStatus.Valide, DateTimeOffset.UtcNow, SetupSourceKind.OfficialProviderApplication, Garage61ExportApproved: true);

    private sealed class SimulatedGarage61Client : IGarage61Client
    {
        public List<SetupFile> Uploaded { get; } = [];
        public Task UploadAsync(SetupFile setup, CancellationToken cancellationToken = default) { Uploaded.Add(setup); return Task.CompletedTask; }
    }
}
