namespace TrainArena.Game.PowerUps;

public sealed class PowerUpRoomConfig
{
    public bool Enabled { get; set; } = true;
    public Dictionary<PowerUpId, int> Starter { get; set; } = new();
    public int StreakRewardEvery { get; set; } = 2;
    public List<PowerUpId> StreakRewardPool { get; set; } = new();
    public int MaxStackPerType { get; set; } = 3;
    public bool HostEventsEnabled { get; set; } = true;
    public int MaxHostEventPerQuestion { get; set; } = 1;

    public static PowerUpRoomConfig CreateDefault() => new()
    {
        Enabled = true,
        Starter = new Dictionary<PowerUpId, int>
        {
            [PowerUpId.FiftyFifty] = 1,
            [PowerUpId.Double] = 1,
            [PowerUpId.ExtraTime] = 0,
            [PowerUpId.Shield] = 1,
        },
        StreakRewardEvery = 2,
        StreakRewardPool = PowerUpCatalog.PlayerIds.ToList(),
        MaxStackPerType = 3,
        HostEventsEnabled = true,
        MaxHostEventPerQuestion = 1,
    };

    public string? Validate()
    {
        if (StreakRewardEvery < 0) return "StreakRewardEvery darf nicht negativ sein.";
        if (MaxStackPerType < 1) return "MaxStackPerType muss ≥ 1 sein.";
        if (MaxHostEventPerQuestion < 0) return "MaxHostEventPerQuestion darf nicht negativ sein.";
        foreach (var (id, n) in Starter)
        {
            if (PowerUpCatalog.Get(id).Kind != PowerUpKind.Player) return "Starter nur Spieler-PowerUps.";
            if (n < 0) return "Starter-Anzahl darf nicht negativ sein.";
        }
        return null;
    }
}
