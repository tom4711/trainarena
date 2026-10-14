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

public sealed record QuestionEndedMessage(
    int CorrectIndex,
    int Index,
    int TotalQuestions,
    string? Explanation = null);

public sealed record ScoreUpdateMessage(string Nickname, int Score, int PointsAwarded);

public sealed record LeaderboardEntryDto(string Nickname, int Score);

public sealed record LeaderboardMessage(
    IReadOnlyList<LeaderboardEntryDto> Entries,
    int QuestionIndex,
    int TotalQuestions,
    bool HasMoreQuestions);

public sealed record QuestionReviewDto(
    int Index,
    string Text,
    string[] Options,
    int CorrectIndex,
    int[] Counts,
    int AnsweredCount,
    int PlayerCount,
    int CorrectCount,
    string? Explanation = null,
    IReadOnlyList<string>? Tags = null,
    int IhkPoints = 0);

public sealed record IhkResultDto(string Nickname, int Points, int Grade, bool Passed);

public sealed record GameFinishedMessage(
    IReadOnlyList<LeaderboardEntryDto> Entries,
    IReadOnlyList<QuestionReviewDto> Review,
    bool ExamMode = false,
    IReadOnlyList<IhkResultDto>? IhkResults = null);

public sealed record ExamConfigDto(bool Enabled, IReadOnlyList<string>? Tags = null);

public sealed record AsyncRaceConfigDto(bool Enabled);

public sealed record AsyncScoreboardEntryDto(string Nickname, int Score, int AnsweredCount, int Total);

public sealed record AsyncScoreboardMessage(IReadOnlyList<AsyncScoreboardEntryDto> Entries);

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
    DateTimeOffset? EndsAtUtc,
    string? ActorNickname = null,
    string? TargetNickname = null,
    bool BlockedByShield = false,
    string? FxKind = null);

public sealed record ArenaEventMessage(
    string PowerUpId,
    DateTimeOffset? EndsAtUtc);

/// <summary>
/// Full-screen / arena FX for competitive power-ups.
/// Kind: shield_up | shield_break | attack_launch | attack_hit | attack_blocked
/// </summary>
public sealed record PowerUpFxMessage(
    string Kind,
    string PowerUpId,
    string? ActorNickname,
    string? TargetNickname);

public sealed record PowerUpErrorMessage(string Error);

public sealed record AutoAdvanceConfigDto(bool Enabled, int DelaySeconds);

public sealed record AutoAdvanceScheduledMessage(DateTimeOffset AdvancesAtUtc);

public sealed record AutoAdvanceCancelledMessage();
