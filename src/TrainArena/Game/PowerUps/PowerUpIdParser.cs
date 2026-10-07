namespace TrainArena.Game.PowerUps;

public static class PowerUpIdParser
{
    private static readonly Dictionary<string, PowerUpId> WireToId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fifty_fifty"] = PowerUpId.FiftyFifty,
        ["double"] = PowerUpId.Double,
        ["extra_time"] = PowerUpId.ExtraTime,
        ["shield"] = PowerUpId.Shield,
        ["disrupt"] = PowerUpId.Disrupt,
        ["boost_all"] = PowerUpId.BoostAll,
        ["time_plus"] = PowerUpId.TimePlus,
    };

    private static readonly Dictionary<PowerUpId, string> IdToWire = WireToId.ToDictionary(
        kv => kv.Value,
        kv => kv.Key);

    public static bool TryParse(string? wire, out PowerUpId id)
    {
        if (wire is not null && WireToId.TryGetValue(wire.Trim(), out id))
        {
            return true;
        }

        id = default;
        return false;
    }

    public static string ToWire(PowerUpId id) => IdToWire[id];
}
