namespace TrainArena.Game.PowerUps;

public sealed class PlayerPowerUpState
{
    public Dictionary<PowerUpId, int> Inventory { get; } = new();
    public int Streak { get; set; }
    public bool HasShield { get; set; }
    public bool DoubleActive { get; set; }
    public int[]? MaskedWrongIndexes { get; set; } // length 1 or 2 when fifty_fifty applied
    public bool UsedExtraTimeThisQuestion { get; set; }
    /// <summary>Next answer this question scores 0 and counts as wrong for streak.</summary>
    public bool DisruptedThisQuestion { get; set; }

    public bool HasSelfEffectActive =>
        DoubleActive || MaskedWrongIndexes is not null || UsedExtraTimeThisQuestion;

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
