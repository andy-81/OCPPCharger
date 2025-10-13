using System;

namespace OcppSimulator;

public readonly record struct MeterSample(double EnergyWh, double PowerKw, double CurrentAmps, double StateOfCharge, DateTimeOffset Timestamp)
{
    public static readonly MeterSample Empty = new(0, 0, 0, -1, DateTimeOffset.MinValue);
}
