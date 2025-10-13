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
}
