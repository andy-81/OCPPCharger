using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using OcppWeb.Services;
using OcppSimulator;

namespace OcppWeb.Hubs;

public interface ISimulatorClient
{
    Task LogAdded(string message);

    Task LogSnapshot(IReadOnlyList<string> entries);

    Task VehicleStateChanged(string state);

    Task ConfigurationUpdated(string key, string value);

    Task ConfigurationSnapshot(IDictionary<string, string> configuration);

    Task MeterValuesUpdated(MeterSnapshotDto snapshot);
}

public sealed class SimulatorHub : Hub<ISimulatorClient>
{
    private readonly SimulatorState _state;

    public SimulatorHub(SimulatorState state)
    {
        _state = state;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.LogSnapshot(_state.GetLogs()).ConfigureAwait(false);
        await Clients.Caller.VehicleStateChanged(_state.VehicleState).ConfigureAwait(false);
        var configuration = new Dictionary<string, string>(_state.GetConfiguration());
        await Clients.Caller.ConfigurationSnapshot(configuration).ConfigureAwait(false);
        var sample = _state.LatestSample;
        await Clients.Caller.MeterValuesUpdated(new MeterSnapshotDto(sample.EnergyWh, sample.PowerKw, sample.CurrentAmps, sample.StateOfCharge >= 0 ? sample.StateOfCharge : null, sample.Timestamp)).ConfigureAwait(false);

        await base.OnConnectedAsync().ConfigureAwait(false);
    }
}

public readonly record struct MeterSnapshotDto(double EnergyWh, double PowerKw, double CurrentAmps, double? StateOfCharge, DateTimeOffset Timestamp);
