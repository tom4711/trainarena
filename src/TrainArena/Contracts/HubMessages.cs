namespace TrainArena.Contracts;

public sealed record RoomCreatedMessage(string Code);

public sealed record PlayerJoinedMessage(string Nickname, int ConnectedCount);

public sealed record LobbyPlayerDto(string Nickname, bool IsConnected);

public sealed record LobbyStateMessage(IReadOnlyList<LobbyPlayerDto> Players, int ConnectedCount);

public sealed record JoinErrorMessage(string Error);

public sealed record AnswerAcceptedMessage(bool Ok, string? Error, int Points);

public sealed record QuestionStartedMessage(
    int Index,
    int TotalQuestions,
    string Text,
    string[] Options,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndsAtUtc,
    string? ImageUrl);

public sealed record QuestionEndedMessage(int CorrectIndex, int Index, int TotalQuestions);

public sealed record ScoreUpdateMessage(string Nickname, int Score, int PointsAwarded);

public sealed record LeaderboardEntryDto(string Nickname, int Score);

public sealed record LeaderboardMessage(
    IReadOnlyList<LeaderboardEntryDto> Entries,
    int QuestionIndex,
    int TotalQuestions,
    bool HasMoreQuestions);

public sealed record GameFinishedMessage(IReadOnlyList<LeaderboardEntryDto> Entries);

public sealed record PowerUpConfigDto(
    bool Enabled,
    Dictionary<string, int>? Starter,
    int StreakRewardEvery,
    int MaxStackPerType,
    bool HostEventsEnabled,
    int MaxHostEventPerQuestion);

public sealed record InventoryUpdateMessage(
    Dictionary<string, int> Counts,
    int Streak);

public sealed record PowerUpUsedMessage(
    string PowerUpId,
    bool Ok,
    int[]? MaskedWrongIndexes,
    DateTimeOffset? EndsAtUtc);

public sealed record ArenaEventMessage(
    string PowerUpId,
    DateTimeOffset? EndsAtUtc);

public sealed record PowerUpErrorMessage(string Error);

public sealed record AutoAdvanceConfigDto(bool Enabled, int DelaySeconds);

public sealed record AutoAdvanceScheduledMessage(DateTimeOffset AdvancesAtUtc);

public sealed record AutoAdvanceCancelledMessage();
