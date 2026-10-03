namespace TrainArena.Game.PowerUps;

public sealed record PowerUpUseResult(
    PowerUpId Id,
    int[]? MaskedWrongIndexes,
    DateTimeOffset? NewEndsAtUtc);
