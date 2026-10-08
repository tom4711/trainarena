namespace TrainArena.Game.PowerUps;

public sealed record PowerUpDefinition(PowerUpId Id, PowerUpKind Kind, string DisplayNameDe, bool IsCompetitiveTargeting);

public static class PowerUpCatalog
{
    private static readonly Dictionary<PowerUpId, PowerUpDefinition> Map = new()
    {
        [PowerUpId.FiftyFifty] = new(PowerUpId.FiftyFifty, PowerUpKind.Player, "50/50", false),
        [PowerUpId.Double] = new(PowerUpId.Double, PowerUpKind.Player, "Double", false),
        [PowerUpId.ExtraTime] = new(PowerUpId.ExtraTime, PowerUpKind.Player, "Extra-Zeit", false),
        [PowerUpId.Shield] = new(PowerUpId.Shield, PowerUpKind.Player, "Shield", false),
        [PowerUpId.Disrupt] = new(PowerUpId.Disrupt, PowerUpKind.Player, "Störimpuls", true),
        [PowerUpId.BoostAll] = new(PowerUpId.BoostAll, PowerUpKind.Host, "Team-Boost", false),
        [PowerUpId.TimePlus] = new(PowerUpId.TimePlus, PowerUpKind.Host, "Zeit +5", false),
    };

    public static PowerUpDefinition Get(PowerUpId id) => Map[id];

    public static IReadOnlyList<PowerUpId> PlayerIds { get; } =
        Map.Values.Where(d => d.Kind == PowerUpKind.Player).Select(d => d.Id).ToList();
}
