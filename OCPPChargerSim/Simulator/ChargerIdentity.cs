namespace OcppSimulator;

public readonly record struct ChargerIdentity(
    string Id,
    string Make,
    string Model,
    string ChargePointModel,
    string ChargePointVendor,
    string ChargePointSerialNumber,
    string ChargeBoxSerialNumber,
    string FirmwareVersion,
    string MeterType,
    string MeterSerialNumber,
    string Iccid,
    string Imsi);
