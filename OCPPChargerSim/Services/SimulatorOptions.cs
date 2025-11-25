using System;
using System.Collections.Generic;

namespace OcppWeb.Services;

public sealed class SimulatorOptions
{
    public const string DefaultMeterValuesSampledData = "Energy.Active.Import.Register,Power.Active.Import,Frequency,Power.Offered,Current.Offered,Energy.Active.Export.Register,Power.Active.Export";
    public const int DefaultMeterValueSampleInterval = 15;
    public const int DefaultClockAlignedDataInterval = 60;

    public bool EnableEnergyActiveImportRegister { get; set; } = true;

    public bool EnablePowerActiveImport { get; set; } = true;

    public bool EnableFrequency { get; set; } = true;

    public bool EnablePowerOffered { get; set; } = true;

    public bool EnableCurrentOffered { get; set; } = true;

    public bool EnableSoC { get; set; } = true;

    public bool EnableEnergyActiveExportRegister { get; set; } = true;

    public bool EnablePowerActiveExport { get; set; } = true;

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
        return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["Energy.Active.Import.Register"] = EnableEnergyActiveImportRegister,
            ["Power.Active.Import"] = EnablePowerActiveImport,
            ["Frequency"] = EnableFrequency,
            ["Power.Offered"] = EnablePowerOffered,
            ["Current.Offered"] = EnableCurrentOffered,
            ["SoC"] = EnableSoC,
            ["Energy.Active.Export.Register"] = EnableEnergyActiveExportRegister,
            ["Power.Active.Export"] = EnablePowerActiveExport,
        };
    }
}
