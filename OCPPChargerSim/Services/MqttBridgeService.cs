using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Packets;
using OcppSimulator;

namespace OcppWeb.Services;

public sealed class MqttBridgeService : IHostedService, IDisposable
{
    private readonly SimulatorConfigurationProvider _configurationProvider;
    private readonly SimulatorCoordinator _coordinator;
    private readonly ILogger<MqttBridgeService> _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private IMqttClient? _client;
    private IDisposable? _configurationSubscription;
    private MqttOptions _currentOptions = new();

    public MqttBridgeService(
        SimulatorConfigurationProvider configurationProvider,
        SimulatorCoordinator coordinator,
        ILogger<MqttBridgeService> logger)
    {
        _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _configurationSubscription = _configurationProvider.Subscribe(() => _ = ApplyConfigurationAsync(_configurationProvider.Snapshot.Options.Mqtt, CancellationToken.None));
        await ApplyConfigurationAsync(_configurationProvider.Snapshot.Options.Mqtt, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _configurationSubscription?.Dispose();
        await DisconnectAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishVehicleStateAsync(string state, CancellationToken cancellationToken)
    {
        var topic = _currentOptions.StatusTopic;
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        await PublishAsync(topic, state, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishMeterSampleAsync(MeterSample sample, CancellationToken cancellationToken)
    {
        var topic = _currentOptions.MeterTopic;
        if (string.IsNullOrWhiteSpace(topic))
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            energyWh = sample.EnergyWh,
            powerKw = sample.PowerKw,
            currentAmps = sample.CurrentAmps,
            stateOfCharge = sample.StateOfCharge >= 0 ? sample.StateOfCharge : (double?)null,
            timestamp = sample.Timestamp,
        });

        await PublishAsync(topic, payload, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _configurationSubscription?.Dispose();
        _sync.Dispose();
        _client?.Dispose();
    }

    private async Task ApplyConfigurationAsync(MqttOptions? options, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _currentOptions = CloneOptions(options);
            if (string.IsNullOrWhiteSpace(_currentOptions.Host))
            {
                await DisconnectInternalAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var client = await EnsureClientAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureConnectionAsync(client, _currentOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure MQTT bridge");
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task<IMqttClient> EnsureClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return _client;
        }

        var factory = new MqttFactory();
        var client = factory.CreateMqttClient();
        client.ApplicationMessageReceivedAsync += HandleMessageAsync;
        client.DisconnectedAsync += async args =>
        {
            _logger.LogWarning("MQTT client disconnected: {Reason}", args.Reason);
            try
            {
                await ApplyConfigurationAsync(_currentOptions, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect MQTT client");
            }
        };
        _client = client;
        return client;
    }

    private async Task ConfigureConnectionAsync(IMqttClient client, MqttOptions options, CancellationToken cancellationToken)
    {
        if (client.IsConnected)
        {
            await client.DisconnectAsync().ConfigureAwait(false);
        }

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(options.Host!, options.Port > 0 ? options.Port : 1883);

        if (!string.IsNullOrWhiteSpace(options.ClientId))
        {
            builder.WithClientId(options.ClientId);
        }

        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            builder.WithCredentials(options.Username, options.Password);
        }

        await client.ConnectAsync(builder.Build(), cancellationToken).ConfigureAwait(false);

        await SubscribeCommandTopicsAsync(client, options, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", options.Host, options.Port);
    }

    private async Task SubscribeCommandTopicsAsync(IMqttClient client, MqttOptions options, CancellationToken cancellationToken)
    {
        var filters = new List<MqttTopicFilter>();
        if (!string.IsNullOrWhiteSpace(options.StartCommandTopic))
        {
            filters.Add(new MqttTopicFilterBuilder().WithTopic(options.StartCommandTopic).Build());
        }

        if (!string.IsNullOrWhiteSpace(options.StopCommandTopic))
        {
            filters.Add(new MqttTopicFilterBuilder().WithTopic(options.StopCommandTopic).Build());
        }

        if (filters.Count == 0)
        {
            return;
        }

        await client.SubscribeAsync(filters, cancellationToken).ConfigureAwait(false);
    }

    private Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic ?? string.Empty;
        if (string.Equals(topic, _currentOptions.StartCommandTopic, StringComparison.OrdinalIgnoreCase))
        {
            return HandleStartCommandAsync(args.CancellationToken);
        }

        if (string.Equals(topic, _currentOptions.StopCommandTopic, StringComparison.OrdinalIgnoreCase))
        {
            return HandleStopCommandAsync(args.CancellationToken);
        }

        return Task.CompletedTask;
    }

    private async Task HandleStartCommandAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _coordinator.SendManualStatusAsync("Charging", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start charging from MQTT command");
        }
    }

    private async Task HandleStopCommandAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _coordinator.SendManualStatusAsync("Available", cancellationToken).ConfigureAwait(false);
            var client = _coordinator.GetClient();
            if (client is not null)
            {
                await client.StopManualSimulationAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop charging from MQTT command");
        }
    }

    private async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken)
    {
        IMqttClient? client;
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            client = _client;
        }
        finally
        {
            _sync.Release();
        }

        if (client is null || !client.IsConnected)
        {
            return;
        }

        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .Build();

            await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT message to {Topic}", topic);
        }
    }

    private async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task DisconnectInternalAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        if (_client.IsConnected)
        {
            try
            {
                await _client.DisconnectAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while disconnecting from MQTT broker");
            }
        }

        _client.Dispose();
        _client = null;
    }

    private static MqttOptions CloneOptions(MqttOptions? options)
    {
        if (options is null)
        {
            return new MqttOptions();
        }

        return new MqttOptions
        {
            Host = options.Host,
            Port = options.Port,
            Username = options.Username,
            Password = options.Password,
            ClientId = options.ClientId,
            StartCommandTopic = options.StartCommandTopic,
            StopCommandTopic = options.StopCommandTopic,
            StatusTopic = options.StatusTopic,
            MeterTopic = options.MeterTopic,
        };
    }
}
