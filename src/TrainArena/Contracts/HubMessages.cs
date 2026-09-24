namespace TrainArena.Contracts;

public sealed record RoomCreatedMessage(string Code);

public sealed record PlayerJoinedMessage(string Nickname, int ConnectedCount);

public sealed record LobbyPlayerDto(string Nickname);

public sealed record LobbyStateMessage(IReadOnlyList<LobbyPlayerDto> Players, int ConnectedCount);

public sealed record JoinErrorMessage(string Error);

public sealed record AnswerAcceptedMessage(bool Ok, string? Error, int Points);

public sealed record QuestionStartedMessage(
    int Index,
    string Text,
    string[] Options,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndsAtUtc,
    string? ImageUrl);

public sealed record QuestionEndedMessage(int CorrectIndex);

public sealed record ScoreUpdateMessage(string Nickname, int Score, int PointsAwarded);

public sealed record LeaderboardEntryDto(string Nickname, int Score);

public sealed record LeaderboardMessage(IReadOnlyList<LeaderboardEntryDto> Entries);

public sealed record GameFinishedMessage(IReadOnlyList<LeaderboardEntryDto> Entries);
