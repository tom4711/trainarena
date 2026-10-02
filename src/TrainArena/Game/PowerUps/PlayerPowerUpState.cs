namespace TrainArena.Game.PowerUps;

public sealed class PlayerPowerUpState
{
    public Dictionary<PowerUpId, int> Inventory { get; } = new();
    public int Streak { get; set; }
    public bool HasShield { get; set; }
    public bool DoubleActive { get; set; }
    public int[]? MaskedWrongIndexes { get; set; } // length 2 when fifty_fifty applied

    public void EnsurePlayerKeys()
    {
        foreach (var id in PowerUpCatalog.PlayerIds)
        {
            if (!Inventory.ContainsKey(id))
            {
                Inventory[id] = 0;
            }
        }
    }
}
