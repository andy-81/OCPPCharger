namespace OcppWeb.Services;

public sealed class SimulatorOptions
{
    public const string DefaultMeterValuesSampledData = "Energy.Active.Import.Register,Power.Active.Import,Frequency,Power.Offered,Current.Offered,Energy.Active.Export.Register,Power.Active.Export";
    public const int DefaultMeterValueSampleInterval = 15;
    public const int DefaultClockAlignedDataInterval = 60;

    public string? Url { get; set; }

    public string? Identity { get; set; }

    public string? AuthKey { get; set; }

    public string? LogFile { get; set; } = "log.txt";

    public bool SupportSoC { get; set; }

    public bool SupportHeartbeat { get; set; } = false;

    public string? ChargerId { get; set; }

    public string? ChargePointSerialNumber { get; set; }

    public string? ChargeBoxSerialNumber { get; set; }

    public string MeterValuesSampledData { get; set; } = DefaultMeterValuesSampledData;

    public int MeterValueSampleInterval { get; set; } = DefaultMeterValueSampleInterval;

    public int ClockAlignedDataInterval { get; set; } = DefaultClockAlignedDataInterval;
}
