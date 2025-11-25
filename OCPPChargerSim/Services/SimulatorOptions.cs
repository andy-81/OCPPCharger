using System;
using System.Collections.Generic;

namespace OcppWeb.Services;

public sealed class SimulatorOptions
{
    public const string DefaultMeterValuesSampledData = "Energy.Active.Import.Register,Power.Active.Import,Frequency,Power.Offered,Current.Offered,Energy.Active.Export.Register,Power.Active.Export";
    public const int DefaultMeterValueSampleInterval = 15;
    public const int DefaultClockAlignedDataInterval = 60;

    public string EnergyActiveImportRegisterMetric { get; set; } = "Energy.Active.Import.Register";

    public string PowerActiveImportMetric { get; set; } = "Power.Active.Import";

    public string FrequencyMetric { get; set; } = "Frequency";

    public string PowerOfferedMetric { get; set; } = "Power.Offered";

    public string CurrentOfferedMetric { get; set; } = "Current.Offered";

    public string SoCMetric { get; set; } = "SoC";

    public string EnergyActiveExportRegisterMetric { get; set; } = "Energy.Active.Export.Register";

    public string PowerActiveExportMetric { get; set; } = "Power.Active.Export";

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

    public Dictionary<string, bool> GetMeterValueMetricToggles()
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        void AddIfPresent(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                result[value] = true;
            }
        }

        AddIfPresent(EnergyActiveImportRegisterMetric);
        AddIfPresent(PowerActiveImportMetric);
        AddIfPresent(FrequencyMetric);
        AddIfPresent(PowerOfferedMetric);
        AddIfPresent(CurrentOfferedMetric);
        AddIfPresent(SoCMetric);
        AddIfPresent(EnergyActiveExportRegisterMetric);
        AddIfPresent(PowerActiveExportMetric);

        return result;
    }
}
