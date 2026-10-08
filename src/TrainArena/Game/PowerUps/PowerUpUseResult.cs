namespace TrainArena.Game.PowerUps;

public sealed record PowerUpUseResult(
    PowerUpId Id,
    int[]? MaskedWrongIndexes,
    DateTimeOffset? NewEndsAtUtc,
    string? ActorNickname = null,
    string? TargetNickname = null,
    bool BlockedByShield = false,
    string? FxKind = null);
