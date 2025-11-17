namespace OcppWeb.Services;

public sealed class SimulatorOptions
{
    public string? Url { get; set; }

    public string? Identity { get; set; }

    public string? AuthKey { get; set; }

    public string? LogFile { get; set; } = "log.txt";

    public bool SupportSoC { get; set; }

    public bool SupportHeartbeat { get; set; } = false;

    public string? ChargerId { get; set; }

    public string? ChargePointSerialNumber { get; set; }

    public string? ChargeBoxSerialNumber { get; set; }

    public MqttOptions Mqtt { get; set; } = new();
}

public sealed class MqttOptions
{
    public string? Host { get; set; }

    public int Port { get; set; } = 1883;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? ClientId { get; set; }

    public string StartCommandTopic { get; set; } = "charger/commands/start";

    public string StopCommandTopic { get; set; } = "charger/commands/stop";

    public string StatusTopic { get; set; } = "charger/status";

    public string MeterTopic { get; set; } = "charger/meter";
}
