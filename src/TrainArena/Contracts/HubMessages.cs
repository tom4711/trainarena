namespace TrainArena.Contracts;

public sealed record RoomCreatedMessage(string Code);

public sealed record PlayerJoinedMessage(string Nickname, int ConnectedCount);

public sealed record LobbyPlayerDto(string Nickname);

public sealed record LobbyStateMessage(IReadOnlyList<LobbyPlayerDto> Players, int ConnectedCount);

public sealed record JoinErrorMessage(string Error);
