using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace OcppWeb.Services;

public sealed class SimulatorConfigurationProvider
{
    private const string ConfigSectionName = "Simulator";

    private readonly object _sync = new();
    private readonly List<TaskCompletionSource<SimulatorConfigurationSnapshot>> _waiters = new();
    private readonly string _configFilePath;
    private readonly ChargerCatalog _catalog;

    private SimulatorOptions _current;
    private bool _requiresConfiguration;
    private bool _configFileMissing;
    private int _version;

    public event Action? ConfigurationChanged;

    public SimulatorConfigurationProvider(IConfiguration configuration, string dataDirectory, ChargerCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Data directory must be provided.", nameof(dataDirectory));
        }

        Directory.CreateDirectory(dataDirectory);
        _configFilePath = Path.Combine(dataDirectory, "simulator.json");

        var section = configuration.GetSection(ConfigSectionName);
        var bound = section.Get<SimulatorOptions>();
        _configFileMissing = !File.Exists(_configFilePath);

        _current = Normalize(bound ?? new SimulatorOptions());
        _requiresConfiguration = !HasRequiredValues(_current) || _configFileMissing;
        _version = 0;
    }

    public SimulatorConfigurationSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return new SimulatorConfigurationSnapshot(Clone(_current), _version, _requiresConfiguration, _configFileMissing);
            }
        }
    }

    public bool HasValidConfiguration
    {
        get
        {
            lock (_sync)
            {
                return !_requiresConfiguration;
            }
        }
    }

    public Task<SimulatorConfigurationSnapshot> WaitForValidAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (!_requiresConfiguration)
            {
                return Task.FromResult(new SimulatorConfigurationSnapshot(Clone(_current), _version, _requiresConfiguration, _configFileMissing));
            }

            var tcs = new TaskCompletionSource<SimulatorConfigurationSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            }

            _waiters.Add(tcs);
            return tcs.Task;
        }
    }

    public SimulatorConfigurationSnapshot UpdateCurrent(SimulatorOptions options, bool markConfigFilePresent = true)
    {
        List<TaskCompletionSource<SimulatorConfigurationSnapshot>>? waitersToRelease = null;
        SimulatorConfigurationSnapshot snapshot;
        Action? callback;

        lock (_sync)
        {
            _current = Normalize(options);
            _version++;
            if (markConfigFilePresent)
            {
                _configFileMissing = false;
            }

            _requiresConfiguration = !HasRequiredValues(_current) || _configFileMissing;
            snapshot = new SimulatorConfigurationSnapshot(Clone(_current), _version, _requiresConfiguration, _configFileMissing);

            if (!_requiresConfiguration && _waiters.Count > 0)
            {
                waitersToRelease = new List<TaskCompletionSource<SimulatorConfigurationSnapshot>>(_waiters);
                _waiters.Clear();
            }

            callback = ConfigurationChanged;
        }

        if (waitersToRelease is not null)
        {
            foreach (var waiter in waitersToRelease)
            {
                waiter.TrySetResult(snapshot);
            }
        }

        callback?.Invoke();
        return snapshot;
    }

    public async Task<SimulatorConfigurationSnapshot> PersistAsync(SimulatorOptions options, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(options);
        var payload = new Dictionary<string, object?>
        {
            ["Url"] = normalized.Url,
            ["Identity"] = normalized.Identity,
            ["AuthKey"] = normalized.AuthKey,
            ["LogFile"] = normalized.LogFile,
            ["EnableEnergyActiveImportRegister"] = normalized.EnableEnergyActiveImportRegister,
            ["EnablePowerActiveImport"] = normalized.EnablePowerActiveImport,
            ["EnableFrequency"] = normalized.EnableFrequency,
            ["EnablePowerOffered"] = normalized.EnablePowerOffered,
            ["EnableCurrentOffered"] = normalized.EnableCurrentOffered,
            ["EnableSoC"] = normalized.EnableSoC,
            ["EnableEnergyActiveExportRegister"] = normalized.EnableEnergyActiveExportRegister,
            ["EnablePowerActiveExport"] = normalized.EnablePowerActiveExport,
            ["SupportSoC"] = normalized.SupportSoC,
            ["SupportHeartbeat"] = normalized.SupportHeartbeat,
            ["ChargerId"] = normalized.ChargerId,
            ["ChargePointSerialNumber"] = normalized.ChargePointSerialNumber,
            ["ChargeBoxSerialNumber"] = normalized.ChargeBoxSerialNumber,
            ["MeterValuesSampledData"] = normalized.MeterValuesSampledData,
            ["MeterValueSampleInterval"] = normalized.MeterValueSampleInterval,
            ["ClockAlignedDataInterval"] = normalized.ClockAlignedDataInterval,
        };

        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [ConfigSectionName] = payload,
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
        });

        await File.WriteAllTextAsync(_configFilePath, json, cancellationToken).ConfigureAwait(false);

        return UpdateCurrent(normalized);
    }

    public IDisposable Subscribe(Action callback)
    {
        lock (_sync)
        {
            ConfigurationChanged += callback;
        }

        return new Subscription(this, callback);
    }

    private void Unsubscribe(Action callback)
    {
        lock (_sync)
        {
            ConfigurationChanged -= callback;
        }
    }

    private SimulatorOptions Normalize(SimulatorOptions options)
    {
        var chargerId = string.IsNullOrWhiteSpace(options.ChargerId) ? _catalog.Default.Id : options.ChargerId.Trim();
        if (!_catalog.TryGet(chargerId, out _))
        {
            chargerId = _catalog.Default.Id;
        }

        var chargePointSerial = string.IsNullOrWhiteSpace(options.ChargePointSerialNumber) ? "0" : options.ChargePointSerialNumber.Trim();
        var chargeBoxSerial = string.IsNullOrWhiteSpace(options.ChargeBoxSerialNumber) ? "0" : options.ChargeBoxSerialNumber.Trim();
        var sampledData = string.IsNullOrWhiteSpace(options.MeterValuesSampledData)
            ? SimulatorOptions.DefaultMeterValuesSampledData
            : options.MeterValuesSampledData.Trim();
        var sampleInterval = options.MeterValueSampleInterval <= 0
            ? SimulatorOptions.DefaultMeterValueSampleInterval
            : options.MeterValueSampleInterval;
        var clockAlignedInterval = options.ClockAlignedDataInterval <= 0
            ? SimulatorOptions.DefaultClockAlignedDataInterval
            : options.ClockAlignedDataInterval;

        return new SimulatorOptions
        {
            Url = string.IsNullOrWhiteSpace(options.Url) ? null : options.Url.Trim(),
            Identity = string.IsNullOrWhiteSpace(options.Identity) ? null : options.Identity.Trim(),
            AuthKey = string.IsNullOrWhiteSpace(options.AuthKey) ? null : options.AuthKey.Trim(),
            LogFile = string.IsNullOrWhiteSpace(options.LogFile) ? "log.txt" : options.LogFile.Trim(),
            EnableEnergyActiveImportRegister = options.EnableEnergyActiveImportRegister,
            EnablePowerActiveImport = options.EnablePowerActiveImport,
            EnableFrequency = options.EnableFrequency,
            EnablePowerOffered = options.EnablePowerOffered,
            EnableCurrentOffered = options.EnableCurrentOffered,
            EnableSoC = options.EnableSoC,
            EnableEnergyActiveExportRegister = options.EnableEnergyActiveExportRegister,
            EnablePowerActiveExport = options.EnablePowerActiveExport,
            SupportSoC = options.SupportSoC,
            SupportHeartbeat = options.SupportHeartbeat,
            ChargerId = chargerId,
            ChargePointSerialNumber = chargePointSerial,
            ChargeBoxSerialNumber = chargeBoxSerial,
            MeterValuesSampledData = sampledData,
            MeterValueSampleInterval = sampleInterval,
            ClockAlignedDataInterval = clockAlignedInterval,
        };
    }

    private bool HasRequiredValues(SimulatorOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.Url)
            && !string.IsNullOrWhiteSpace(options.Identity)
            && !string.IsNullOrWhiteSpace(options.AuthKey)
            && _catalog.TryGet(options.ChargerId, out _);
    }

    private static SimulatorOptions Clone(SimulatorOptions options)
    {
        return new SimulatorOptions
        {
            Url = options.Url,
            Identity = options.Identity,
            AuthKey = options.AuthKey,
            LogFile = options.LogFile,
            EnableEnergyActiveImportRegister = options.EnableEnergyActiveImportRegister,
            EnablePowerActiveImport = options.EnablePowerActiveImport,
            EnableFrequency = options.EnableFrequency,
            EnablePowerOffered = options.EnablePowerOffered,
            EnableCurrentOffered = options.EnableCurrentOffered,
            EnableSoC = options.EnableSoC,
            EnableEnergyActiveExportRegister = options.EnableEnergyActiveExportRegister,
            EnablePowerActiveExport = options.EnablePowerActiveExport,
            SupportSoC = options.SupportSoC,
            SupportHeartbeat = options.SupportHeartbeat,
            ChargerId = options.ChargerId,
            ChargePointSerialNumber = options.ChargePointSerialNumber,
            ChargeBoxSerialNumber = options.ChargeBoxSerialNumber,
            MeterValuesSampledData = options.MeterValuesSampledData,
            MeterValueSampleInterval = options.MeterValueSampleInterval,
            ClockAlignedDataInterval = options.ClockAlignedDataInterval,
        };
    }

    private sealed class Subscription : IDisposable
    {
        private SimulatorConfigurationProvider? _owner;
        private readonly Action _callback;

        public Subscription(SimulatorConfigurationProvider owner, Action callback)
        {
            _owner = owner;
            _callback = callback;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Unsubscribe(_callback);
        }
    }
}

public readonly record struct SimulatorConfigurationSnapshot(
    SimulatorOptions Options,
    int Version,
    bool RequiresConfiguration,
    bool ConfigurationFileMissing);
