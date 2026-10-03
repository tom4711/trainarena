using TrainArena.Game.PowerUps;

namespace TrainArena.Game;

public sealed class PlayerInfo
{
    public required string Nickname { get; init; }
    public string ConnectionId { get; set; } = "";
    public bool IsConnected { get; set; } = true;
    public int Score { get; set; }
}

/// <summary>
/// In-memory state for one live room.
/// </summary>
public sealed class GameSession
{
    private readonly object _gate = new();
    private readonly List<PlayerInfo> _players = new();
    private readonly Dictionary<string, int> _answersThisQuestion =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlayerPowerUpState> _powerUps =
        new(StringComparer.OrdinalIgnoreCase);
    private List<DemoQuestion> _quizQuestions = new();
    private readonly Dictionary<string, bool> _correctThisQuestion =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _roomExtraTimeExtendedThisQuestion;
    private int _hostEventsUsedThisQuestion;
    private bool _boostAllActive;
    private TimeSpan _questionScoreLimit;

    public GameSession(string code, string hostConnectionId, Guid quizId, PowerUpRoomConfig? powerUpConfig = null)
    {
        Code = code;
        HostConnectionId = hostConnectionId;
        QuizId = quizId;
        PowerUpConfig = powerUpConfig ?? PowerUpRoomConfig.CreateDefault();
    }

    public PowerUpRoomConfig PowerUpConfig { get; }

    public string Code { get; }
    public string HostConnectionId { get; private set; }
    public Guid QuizId { get; }
    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    public DemoQuestion? CurrentQuestion { get; private set; }
    public DateTimeOffset? QuestionStartedAtUtc { get; private set; }
    public DateTimeOffset? QuestionEndsAtUtc { get; private set; }
    public int QuestionIndex { get; private set; } = -1;

    public int QuestionCount
    {
        get
        {
            lock (_gate)
            {
                return _quizQuestions.Count;
            }
        }
    }

    public int ConnectedPlayerCount
    {
        get
        {
            lock (_gate)
            {
                return _players.Count(p => p.IsConnected);
            }
        }
    }

    public void SetQuestions(IReadOnlyList<DemoQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);
        lock (_gate)
        {
            _quizQuestions = questions.ToList();
            QuestionIndex = -1;
        }
    }

    public DemoQuestion? PeekNextQuestion()
    {
        lock (_gate)
        {
            var next = QuestionIndex + 1;
            if (next < 0 || next >= _quizQuestions.Count)
            {
                return null;
            }

            return _quizQuestions[next];
        }
    }

    public bool HasMoreQuestions
    {
        get
        {
            lock (_gate)
            {
                return QuestionIndex + 1 < _quizQuestions.Count;
            }
        }
    }

    public IReadOnlyList<PlayerInfo> Players
    {
        get
        {
            lock (_gate)
            {
                return _players.ToList();
            }
        }
    }

    public (bool ok, string? error) TryJoin(string nickname, string connectionId)
    {
        var trimmed = nickname?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return (false, "Nickname required");
        }

        lock (_gate)
        {
            if (Phase != GamePhase.Lobby)
            {
                return (false, "Game already started");
            }

            var existing = _players.FirstOrDefault(p =>
                string.Equals(p.Nickname, trimmed, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                if (existing.IsConnected)
                {
                    return (false, "Nickname already taken");
                }

                // Soft rejoin via JoinRoom after disconnect.
                existing.ConnectionId = connectionId;
                existing.IsConnected = true;
                EnsurePowerUpState(trimmed);
                return (true, null);
            }

            _players.Add(new PlayerInfo
            {
                Nickname = trimmed,
                ConnectionId = connectionId,
                IsConnected = true
            });
            GrantStarterPowerUps(trimmed);
            return (true, null);
        }
    }

    public (bool ok, string? error) TryRejoin(string nickname, string connectionId)
    {
        var trimmed = nickname?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return (false, "Nickname required");
        }

        lock (_gate)
        {
            var player = _players.FirstOrDefault(p =>
                string.Equals(p.Nickname, trimmed, StringComparison.OrdinalIgnoreCase));
            if (player is null)
            {
                return (false, "Unknown nickname for this room");
            }

            player.ConnectionId = connectionId;
            player.IsConnected = true;
            EnsurePowerUpState(trimmed);
            return (true, null);
        }
    }

    public PlayerPowerUpState? GetPowerUpState(string nickname)
    {
        lock (_gate)
        {
            if (!_powerUps.TryGetValue(nickname, out var state))
            {
                return null;
            }

            return state;
        }
    }

    public bool TryRebindHost(string connectionId)
    {
        lock (_gate)
        {
            HostConnectionId = connectionId;
            return true;
        }
    }

    public bool MarkDisconnected(string connectionId)
    {
        lock (_gate)
        {
            var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player is null)
            {
                return false;
            }

            player.IsConnected = false;
            return true;
        }
    }

    public (bool ok, string? error) StartQuestion(DemoQuestion question, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(question);
        if (question.Options.Length != 4)
        {
            return (false, "Question must have 4 options");
        }

        if (question.CorrectIndex is < 0 or > 3)
        {
            return (false, "Invalid correct index");
        }

        lock (_gate)
        {
            if (_players.Count < 1)
            {
                return (false, "Need at least one player");
            }

            if (Phase is not (GamePhase.Lobby or GamePhase.Leaderboard))
            {
                return (false, "Cannot start question in current phase");
            }

            var limit = TimeSpan.FromSeconds(Math.Max(1, question.TimeLimitSeconds));
            _questionScoreLimit = limit;
            CurrentQuestion = question;
            QuestionIndex++;
            QuestionStartedAtUtc = nowUtc;
            QuestionEndsAtUtc = nowUtc + limit;
            _answersThisQuestion.Clear();
            _correctThisQuestion.Clear();
            ClearPerQuestionPowerUpFlags();
            Phase = GamePhase.QuestionActive;
            return (true, null);
        }
    }

    public (bool ok, string? error, PowerUpUseResult? result) TryUsePowerUp(
        string connectionId,
        PowerUpId powerUpId)
    {
        lock (_gate)
        {
            if (!PowerUpConfig.Enabled)
            {
                return (false, "Power-ups disabled", null);
            }

            if (Phase != GamePhase.QuestionActive || CurrentQuestion is null)
            {
                return (false, "No active question", null);
            }

            var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId && p.IsConnected);
            if (player is null)
            {
                return (false, "Not a player in this room", null);
            }

            if (_answersThisQuestion.ContainsKey(player.Nickname))
            {
                return (false, "Already answered", null);
            }

            if (!_powerUps.TryGetValue(player.Nickname, out var state))
            {
                return (false, "Not a player in this room", null);
            }

            var definition = PowerUpCatalog.Get(powerUpId);
            if (definition.Kind != PowerUpKind.Player)
            {
                return (false, "Not a player power-up", null);
            }

            if (state.Inventory[powerUpId] < 1)
            {
                return (false, "No power-ups remaining", null);
            }

            var isSelfEffect = powerUpId is PowerUpId.FiftyFifty or PowerUpId.Double or PowerUpId.ExtraTime;
            if (isSelfEffect && state.HasSelfEffectActive)
            {
                return (false, "Already used a self-effect this question", null);
            }

            state.Inventory[powerUpId]--;

            int[]? masked = null;
            DateTimeOffset? newEndsAt = null;

            switch (powerUpId)
            {
                case PowerUpId.FiftyFifty:
                    masked = PickTwoWrongIndexes(CurrentQuestion.CorrectIndex);
                    state.MaskedWrongIndexes = masked;
                    break;
                case PowerUpId.Double:
                    state.DoubleActive = true;
                    break;
                case PowerUpId.ExtraTime:
                    state.UsedExtraTimeThisQuestion = true;
                    if (!_roomExtraTimeExtendedThisQuestion)
                    {
                        QuestionEndsAtUtc = QuestionEndsAtUtc!.Value.AddSeconds(5);
                        _roomExtraTimeExtendedThisQuestion = true;
                        newEndsAt = QuestionEndsAtUtc;
                    }

                    break;
                case PowerUpId.Shield:
                    state.HasShield = true;
                    break;
            }

            return (true, null, new PowerUpUseResult(powerUpId, masked, newEndsAt));
        }
    }

    public (bool ok, string? error, int points) SubmitAnswer(
        string connectionId,
        int optionIndex,
        DateTimeOffset serverUtc)
    {
        lock (_gate)
        {
            if (Phase != GamePhase.QuestionActive || CurrentQuestion is null
                || QuestionStartedAtUtc is null || QuestionEndsAtUtc is null)
            {
                return (false, "No active question", 0);
            }

            if (serverUtc > QuestionEndsAtUtc.Value)
            {
                return (false, "Question already ended", 0);
            }

            var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId && p.IsConnected);
            if (player is null)
            {
                return (false, "Not a player in this room", 0);
            }

            if (_answersThisQuestion.ContainsKey(player.Nickname))
            {
                return (false, "Already answered", 0);
            }

            if (optionIndex is < 0 or > 3)
            {
                return (false, "Invalid option", 0);
            }

            _answersThisQuestion[player.Nickname] = optionIndex;
            var correct = optionIndex == CurrentQuestion.CorrectIndex;
            _correctThisQuestion[player.Nickname] = correct;
            var elapsed = serverUtc - QuestionStartedAtUtc.Value;
            var points = Scoring.Score(correct, elapsed, _questionScoreLimit);
            if (points > 0)
            {
                if (_powerUps.TryGetValue(player.Nickname, out var pu))
                {
                    if (pu.DoubleActive)
                    {
                        points *= 2;
                        pu.DoubleActive = false;
                    }
                }

                if (_boostAllActive)
                {
                    points = (int)Math.Floor(points * 1.5);
                }
            }
            else if (_powerUps.TryGetValue(player.Nickname, out var puWrong))
            {
                puWrong.DoubleActive = false;
            }

            player.Score += points;
            return (true, null, points);
        }
    }

    public (bool ok, string? error, DateTimeOffset? newEndsAtUtc) TryHostArenaEvent(
        string hostConnectionId,
        PowerUpId powerUpId)
    {
        lock (_gate)
        {
            if (!PowerUpConfig.Enabled)
            {
                return (false, "Power-ups disabled", null);
            }

            if (!PowerUpConfig.HostEventsEnabled)
            {
                return (false, "Host arena events disabled", null);
            }

            if (Phase != GamePhase.QuestionActive || CurrentQuestion is null)
            {
                return (false, "No active question", null);
            }

            if (!string.Equals(HostConnectionId, hostConnectionId, StringComparison.Ordinal))
            {
                return (false, "Only the host can trigger arena events", null);
            }

            if (_hostEventsUsedThisQuestion >= PowerUpConfig.MaxHostEventPerQuestion)
            {
                return (false, "Host event budget exhausted for this question", null);
            }

            var definition = PowerUpCatalog.Get(powerUpId);
            if (definition.Kind != PowerUpKind.Host)
            {
                return (false, "Not a host arena event", null);
            }

            DateTimeOffset? newEndsAt = null;
            switch (powerUpId)
            {
                case PowerUpId.BoostAll:
                    _boostAllActive = true;
                    break;
                case PowerUpId.TimePlus:
                    QuestionEndsAtUtc = QuestionEndsAtUtc!.Value.AddSeconds(5);
                    newEndsAt = QuestionEndsAtUtc;
                    break;
                default:
                    return (false, "Unknown host arena event", null);
            }

            _hostEventsUsedThisQuestion++;
            return (true, null, newEndsAt);
        }
    }

    public IReadOnlyList<(string Nickname, PowerUpId PowerUpId)> ApplyStreakRewards(Random? rng = null)
    {
        rng ??= Random.Shared;
        lock (_gate)
        {
            var grants = new List<(string Nickname, PowerUpId PowerUpId)>();
            var every = PowerUpConfig.StreakRewardEvery;
            var pool = PowerUpConfig.StreakRewardPool;
            if (!PowerUpConfig.Enabled || every <= 0 || pool.Count == 0)
            {
                _correctThisQuestion.Clear();
                return grants;
            }

            foreach (var player in _players)
            {
                if (!_powerUps.TryGetValue(player.Nickname, out var state))
                {
                    continue;
                }

                var answeredCorrectly = _correctThisQuestion.TryGetValue(player.Nickname, out var ok) && ok;
                if (answeredCorrectly)
                {
                    state.Streak++;
                }
                else
                {
                    state.Streak = 0;
                }

                if (state.Streak > 0 && state.Streak % every == 0)
                {
                    var pick = pool[rng.Next(pool.Count)];
                    if (state.Inventory[pick] < PowerUpConfig.MaxStackPerType)
                    {
                        state.Inventory[pick]++;
                        grants.Add((player.Nickname, pick));
                    }
                }
            }

            _correctThisQuestion.Clear();
            return grants;
        }
    }

    public bool TryEndQuestion(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (Phase != GamePhase.QuestionActive || QuestionEndsAtUtc is null)
            {
                return false;
            }

            var expectedAnswers = Math.Max(1, _players.Count(p => p.IsConnected));
            if (nowUtc < QuestionEndsAtUtc.Value
                && _answersThisQuestion.Count < expectedAnswers)
            {
                return false;
            }

            TransitionQuestionToReveal();
            return true;
        }
    }

    public void ForceEndQuestion()
    {
        lock (_gate)
        {
            if (Phase != GamePhase.QuestionActive)
            {
                return;
            }

            TransitionQuestionToReveal();
        }
    }

    public void ShowLeaderboard()
    {
        lock (_gate)
        {
            if (Phase != GamePhase.Reveal)
            {
                return;
            }

            Phase = GamePhase.Leaderboard;
        }
    }

    public (bool ok, string? error) TryFinish()
    {
        lock (_gate)
        {
            if (Phase != GamePhase.Leaderboard)
            {
                return (false, "Finish only from leaderboard");
            }

            if (QuestionIndex + 1 < _quizQuestions.Count)
            {
                return (false, "More questions remaining");
            }

            Phase = GamePhase.Finished;
            return (true, null);
        }
    }

    public void Finish() => TryFinish();

    public int? CorrectIndex
    {
        get
        {
            lock (_gate)
            {
                return CurrentQuestion?.CorrectIndex;
            }
        }
    }

    public IReadOnlyList<LeaderboardEntry> GetLeaderboard()
    {
        lock (_gate)
        {
            return _players
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.Nickname, StringComparer.OrdinalIgnoreCase)
                .Select(p => new LeaderboardEntry { Nickname = p.Nickname, Score = p.Score })
                .ToList();
        }
    }

    public bool IsHost(string connectionId) =>
        string.Equals(HostConnectionId, connectionId, StringComparison.Ordinal);

    private void GrantStarterPowerUps(string nickname)
    {
        var state = new PlayerPowerUpState();
        state.EnsurePlayerKeys();
        if (PowerUpConfig.Enabled)
        {
            foreach (var id in PowerUpCatalog.PlayerIds)
            {
                if (PowerUpConfig.Starter.TryGetValue(id, out var count))
                {
                    state.Inventory[id] = count;
                }
            }
        }

        _powerUps[nickname] = state;
    }

    private void EnsurePowerUpState(string nickname)
    {
        if (_powerUps.ContainsKey(nickname))
        {
            return;
        }

        var state = new PlayerPowerUpState();
        state.EnsurePlayerKeys();
        _powerUps[nickname] = state;
    }

    private void TransitionQuestionToReveal()
    {
        ClearDoubleActiveForAllPlayers();
        Phase = GamePhase.Reveal;
    }

    private void ClearDoubleActiveForAllPlayers()
    {
        foreach (var state in _powerUps.Values)
        {
            state.DoubleActive = false;
        }
    }

    private void ClearPerQuestionPowerUpFlags()
    {
        foreach (var state in _powerUps.Values)
        {
            state.DoubleActive = false;
            state.MaskedWrongIndexes = null;
            state.UsedExtraTimeThisQuestion = false;
        }

        _roomExtraTimeExtendedThisQuestion = false;
        _hostEventsUsedThisQuestion = 0;
        _boostAllActive = false;
    }

    private static int[] PickTwoWrongIndexes(int correctIndex)
    {
        var wrong = Enumerable.Range(0, 4).Where(i => i != correctIndex).ToList();
        for (var i = wrong.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (wrong[i], wrong[j]) = (wrong[j], wrong[i]);
        }

        return [wrong[0], wrong[1]];
    }
}
