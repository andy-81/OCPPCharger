using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OcppWeb.Services;

public sealed class ChargerCatalog
{
    private readonly Dictionary<string, ChargerDescriptor> _chargers;

    public ChargerCatalog(IEnumerable<ChargerDescriptor> chargers)
    {
        var list = chargers?.ToList() ?? new List<ChargerDescriptor>();
        if (!list.Any())
        {
            throw new InvalidOperationException("No chargers defined in catalog.");
        }

        _chargers = list.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        Chargers = list.AsReadOnly();
        Default = Chargers.First();
    }

    public IReadOnlyList<ChargerDescriptor> Chargers { get; }

    public ChargerDescriptor Default { get; }

    public bool TryGet(string? id, out ChargerDescriptor descriptor)
    {
        if (!string.IsNullOrWhiteSpace(id) && _chargers.TryGetValue(id, out descriptor))
        {
            return true;
        }

        descriptor = default;
        return false;
    }

    public ChargerDescriptor GetOrDefault(string? id)
    {
        return TryGet(id, out var descriptor) ? descriptor : Default;
    }

    public static ChargerCatalog Load(string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "chargers.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Chargers configuration file not found.", path);
        }

        using var stream = File.OpenRead(path);
        var chargers = JsonSerializer.Deserialize<List<ChargerDescriptor>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (chargers is null || chargers.Count == 0)
        {
            throw new InvalidOperationException("Chargers configuration file is empty or invalid.");
        }

        return new ChargerCatalog(chargers);
    }
}

public readonly record struct ChargerDescriptor(
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
