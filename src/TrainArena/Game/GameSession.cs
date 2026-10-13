using TrainArena.Contracts;
using TrainArena.Game.PowerUps;

namespace TrainArena.Game;

public sealed record AsyncSubmitResult(
    bool Ok,
    string? Error,
    int PointsAwarded,
    bool Correct,
    int CorrectIndex,
    string? Explanation,
    bool PlayerFinished,
    bool RaceFinished,
    DemoQuestion? NextQuestion,
    DateTimeOffset? NextStartedAtUtc,
    DateTimeOffset? NextEndsAtUtc,
    int NextIndex,
    int Total);

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
    private readonly List<QuestionReviewStat> _review = [];
    private bool _roomExtraTimeExtendedThisQuestion;
    private int _hostEventsUsedThisQuestion;
    private bool _boostAllActive;
    private TimeSpan _questionScoreLimit;
    private int[] _ihkPointsPerQuestion = [];
    private readonly Dictionary<string, PlayerRaceState> _races =
        new(StringComparer.OrdinalIgnoreCase);

    public GameSession(
        string code,
        string hostConnectionId,
        Guid quizId,
        PowerUpRoomConfig? powerUpConfig = null,
        AutoAdvanceOptions? autoAdvance = null,
        bool examMode = false,
        bool asyncRaceEnabled = false)
    {
        Code = code;
        HostConnectionId = hostConnectionId;
        QuizId = quizId;
        ExamMode = examMode;
        AsyncRaceEnabled = asyncRaceEnabled;
        var pu = powerUpConfig ?? PowerUpRoomConfig.CreateDefault();
        if (examMode)
        {
            pu.Enabled = false;
            pu.HostEventsEnabled = false;
        }

        PowerUpConfig = pu;
        AutoAdvance = autoAdvance ?? AutoAdvanceOptions.CreateDefault();
        if (asyncRaceEnabled)
        {
            AutoAdvance.Enabled = false;
        }
    }

    public PowerUpRoomConfig PowerUpConfig { get; }
    public AutoAdvanceOptions AutoAdvance { get; }
    public bool ExamMode { get; }
    public bool AsyncRaceEnabled { get; }

    public string Code { get; }
    public string HostConnectionId { get; private set; }
    public Guid QuizId { get; }
    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    public DemoQuestion? CurrentQuestion { get; private set; }
    public DateTimeOffset? QuestionStartedAtUtc { get; private set; }
    public DateTimeOffset? QuestionEndsAtUtc { get; private set; }
    public int QuestionIndex { get; private set; } = -1;

    public IReadOnlyList<QuestionReviewStat> Review
    {
        get
        {
            lock (_gate)
            {
                return _review.ToList();
            }
        }
    }

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
            _review.Clear();
            _ihkPointsPerQuestion = ExamMode
                ? IhkGrading.NormalizeToHundred(_quizQuestions.Select(q => q.Points).ToList())
                : [];
        }
    }

    public int GetIhkPointsForQuestion(int index)
    {
        lock (_gate)
        {
            if (index < 0 || index >= _ihkPointsPerQuestion.Length)
            {
                return 0;
            }

            return _ihkPointsPerQuestion[index];
        }
    }

    public IReadOnlyList<IhkPlayerResult> GetIhkResults()
    {
        lock (_gate)
        {
            return _players
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.Nickname, StringComparer.OrdinalIgnoreCase)
                .Select(p =>
                {
                    var points = Math.Clamp(p.Score, 0, IhkGrading.MaxPoints);
                    var grade = IhkGrading.GradeFromPoints(points);
                    return new IhkPlayerResult(p.Nickname, points, grade, IhkGrading.IsPassed(points));
                })
                .ToList();
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
        var n = question.Options.Length;
        if (n is < 2 or > 6)
        {
            return (false, "Question must have 2–6 options");
        }

        if (question.CorrectIndex < 0 || question.CorrectIndex >= n)
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
        PowerUpId powerUpId,
        string? targetNickname = null)
    {
        lock (_gate)
        {
            if (!PowerUpConfig.Enabled)
            {
                return (false, "Power-ups disabled", null);
            }

            if (AsyncRaceEnabled && Phase == GamePhase.AsyncRacing)
            {
                return TryUsePowerUpAsync(connectionId, powerUpId, targetNickname);
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

            int[]? masked = null;
            DateTimeOffset? newEndsAt = null;
            string? target = null;
            var blocked = false;
            string? fxKind = null;

            if (powerUpId == PowerUpId.Disrupt)
            {
                var resolved = ResolveDisruptTarget(player.Nickname, targetNickname);
                if (resolved.error is not null)
                {
                    return (false, resolved.error, null);
                }

                target = resolved.targetNickname!;
                if (!_powerUps.TryGetValue(target, out var targetState))
                {
                    return (false, "Target not found", null);
                }

                state.Inventory[powerUpId]--;
                if (targetState.HasShield)
                {
                    targetState.HasShield = false;
                    blocked = true;
                    fxKind = "attack_blocked";
                }
                else
                {
                    targetState.DisruptedThisQuestion = true;
                    fxKind = "attack_hit";
                }

                return (
                    true,
                    null,
                    new PowerUpUseResult(
                        powerUpId,
                        null,
                        null,
                        player.Nickname,
                        target,
                        blocked,
                        fxKind));
            }

            if (powerUpId == PowerUpId.FiftyFifty && CurrentQuestion.Options.Length < 3)
            {
                return (false, "50/50 requires at least 3 options", null);
            }

            state.Inventory[powerUpId]--;

            switch (powerUpId)
            {
                case PowerUpId.FiftyFifty:
                {
                    var optionCount = CurrentQuestion.Options.Length;
                    var maskCount = optionCount >= 4 ? 2 : 1;
                    masked = PickWrongIndexes(CurrentQuestion.CorrectIndex, optionCount, maskCount);
                    state.MaskedWrongIndexes = masked;
                    break;
                }
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
                    fxKind = "shield_up";
                    break;
            }

            return (
                true,
                null,
                new PowerUpUseResult(powerUpId, masked, newEndsAt, player.Nickname, null, false, fxKind));
        }
    }

    private (bool ok, string? error, PowerUpUseResult? result) TryUsePowerUpAsync(
        string connectionId,
        PowerUpId powerUpId,
        string? targetNickname)
    {
        var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId && p.IsConnected);
        if (player is null)
        {
            return (false, "Not a player in this room", null);
        }

        if (!_races.TryGetValue(player.Nickname, out var race) || race.Finished)
        {
            return (false, "No active question", null);
        }

        var currentQuestion = race.Current;
        if (currentQuestion is null)
        {
            return (false, "No active question", null);
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

        int[]? masked = null;
        DateTimeOffset? newEndsAt = null;
        string? target = null;
        var blocked = false;
        string? fxKind = null;

        if (powerUpId == PowerUpId.Disrupt)
        {
            var resolved = ResolveAsyncDisruptTarget(player.Nickname, targetNickname);
            if (resolved.error is not null)
            {
                return (false, resolved.error, null);
            }

            target = resolved.targetNickname!;
            if (!_powerUps.TryGetValue(target, out var targetState))
            {
                return (false, "Target not found", null);
            }

            state.Inventory[powerUpId]--;
            if (targetState.HasShield)
            {
                targetState.HasShield = false;
                blocked = true;
                fxKind = "attack_blocked";
            }
            else
            {
                targetState.DisruptedThisQuestion = true;
                fxKind = "attack_hit";
            }

            return (
                true,
                null,
                new PowerUpUseResult(
                    powerUpId,
                    null,
                    null,
                    player.Nickname,
                    target,
                    blocked,
                    fxKind));
        }

        if (powerUpId == PowerUpId.FiftyFifty && currentQuestion.Options.Length < 3)
        {
            return (false, "50/50 requires at least 3 options", null);
        }

        state.Inventory[powerUpId]--;

        switch (powerUpId)
        {
            case PowerUpId.FiftyFifty:
            {
                var optionCount = currentQuestion.Options.Length;
                var maskCount = optionCount >= 4 ? 2 : 1;
                masked = PickWrongIndexes(currentQuestion.CorrectIndex, optionCount, maskCount);
                state.MaskedWrongIndexes = masked;
                break;
            }
            case PowerUpId.Double:
                state.DoubleActive = true;
                break;
            case PowerUpId.ExtraTime:
                state.UsedExtraTimeThisQuestion = true;
                if (race.EndsAtUtc is not null)
                {
                    race.EndsAtUtc = race.EndsAtUtc.Value.AddSeconds(5);
                    newEndsAt = race.EndsAtUtc;
                }

                break;
            case PowerUpId.Shield:
                state.HasShield = true;
                fxKind = "shield_up";
                break;
        }

        return (
            true,
            null,
            new PowerUpUseResult(powerUpId, masked, newEndsAt, player.Nickname, null, false, fxKind));
    }

    public IReadOnlyList<string> GetAsyncDisruptTargets(string connectionId)
    {
        lock (_gate)
        {
            if (!AsyncRaceEnabled || Phase != GamePhase.AsyncRacing)
            {
                return Array.Empty<string>();
            }

            var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId && p.IsConnected);
            if (player is null)
            {
                return Array.Empty<string>();
            }

            return GetAsyncDisruptCandidates(player.Nickname);
        }
    }

    private List<string> GetAsyncDisruptCandidates(string actorNickname) =>
        _players
            .Where(p =>
            {
                if (!p.IsConnected
                    || string.Equals(p.Nickname, actorNickname, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!_races.TryGetValue(p.Nickname, out var race))
                {
                    return false;
                }

                return !race.Finished && race.Current is not null;
            })
            .Select(p => p.Nickname)
            .ToList();

    private (string? targetNickname, string? error) ResolveAsyncDisruptTarget(string actorNickname, string? requested)
    {
        var candidates = GetAsyncDisruptCandidates(actorNickname);

        if (candidates.Count == 0)
        {
            return (null, "No valid target");
        }

        if (string.IsNullOrWhiteSpace(requested))
        {
            return (candidates[Random.Shared.Next(candidates.Count)], null);
        }

        var match = candidates.FirstOrDefault(n =>
            string.Equals(n, requested.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return (null, "Invalid target");
        }

        return (match, null);
    }

    private (string? targetNickname, string? error) ResolveDisruptTarget(string actorNickname, string? requested)
    {
        var candidates = _players
            .Where(p =>
                p.IsConnected
                && !string.Equals(p.Nickname, actorNickname, StringComparison.OrdinalIgnoreCase)
                && !_answersThisQuestion.ContainsKey(p.Nickname))
            .Select(p => p.Nickname)
            .ToList();

        if (candidates.Count == 0)
        {
            return (null, "No valid target");
        }

        if (string.IsNullOrWhiteSpace(requested))
        {
            return (candidates[Random.Shared.Next(candidates.Count)], null);
        }

        var match = candidates.FirstOrDefault(n =>
            string.Equals(n, requested.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return (null, "Invalid target");
        }

        return (match, null);
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

            if (optionIndex < 0 || optionIndex >= CurrentQuestion.Options.Length)
            {
                return (false, "Invalid option", 0);
            }

            _answersThisQuestion[player.Nickname] = optionIndex;
            var correct = optionIndex == CurrentQuestion.CorrectIndex;
            var disrupted = _powerUps.TryGetValue(player.Nickname, out var puState) && puState.DisruptedThisQuestion;
            // Disrupted answers never score and do not count for streak.
            var countsAsCorrect = correct && !disrupted;
            _correctThisQuestion[player.Nickname] = countsAsCorrect;
            var points = ComputeSubmitPoints(
                countsAsCorrect,
                serverUtc,
                QuestionStartedAtUtc.Value,
                _questionScoreLimit,
                puState);

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

    public bool ShouldEndQuestion(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            return CanEndQuestionNow(nowUtc);
        }
    }

    public bool TryEndQuestion(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!CanEndQuestionNow(nowUtc))
            {
                return false;
            }

            TransitionQuestionToReveal();
            return true;
        }
    }

    public bool ForceEndQuestion()
    {
        lock (_gate)
        {
            if (Phase != GamePhase.QuestionActive)
            {
                return false;
            }

            TransitionQuestionToReveal();
            return true;
        }
    }

    private bool CanEndQuestionNow(DateTimeOffset nowUtc)
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

        return true;
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

    public (bool ok, string? error) StartAsyncRace(DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (!AsyncRaceEnabled) return (false, "Kein Async-Modus");
            if (Phase != GamePhase.Lobby) return (false, "Ungültige Phase");
            if (_quizQuestions.Count == 0) return (false, "Keine Fragen");
            if (_players.Count == 0) return (false, "Keine Spieler");

            var rng = Random.Shared;
            _races.Clear();
            foreach (var p in _players)
            {
                var order = ShuffleCopy(_quizQuestions, rng);
                var q0 = order[0];
                var limit = TimeSpan.FromSeconds(q0.TimeLimitSeconds <= 0 ? 20 : q0.TimeLimitSeconds);
                _races[p.Nickname] = new PlayerRaceState
                {
                    Order = order,
                    Index = 0,
                    QuestionStartedAtUtc = nowUtc,
                    EndsAtUtc = nowUtc + limit,
                };
            }

            Phase = GamePhase.AsyncRacing;
            return (true, null);
        }
    }

    public IReadOnlyList<AsyncScoreboardEntryDto> GetAsyncScoreboard()
    {
        lock (_gate)
        {
            return _players
                .Select(p =>
                {
                    var race = _races.GetValueOrDefault(p.Nickname);
                    var total = race?.Order.Count ?? _quizQuestions.Count;
                    var answered = race?.AnsweredCount ?? 0;
                    return new AsyncScoreboardEntryDto(p.Nickname, p.Score, answered, total);
                })
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.Nickname, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public PlayerRaceState? GetRaceState(string nickname)
    {
        lock (_gate)
        {
            return _races.GetValueOrDefault(nickname);
        }
    }

    public AsyncSubmitResult SubmitAsyncAnswer(string nickname, int optionIndex, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (Phase != GamePhase.AsyncRacing)
            {
                return AsyncSubmitFail("No async race in progress");
            }

            var player = FindPlayerByNickname(nickname);
            if (player is null)
            {
                return AsyncSubmitFail("Not a player in this room");
            }

            if (!_races.TryGetValue(player.Nickname, out var race) || race.Finished)
            {
                return AsyncSubmitFail("No active question");
            }

            var question = race.Current;
            if (question is null)
            {
                return AsyncSubmitFail("No active question");
            }

            if (race.EndsAtUtc is not null && nowUtc > race.EndsAtUtc.Value)
            {
                return AsyncSubmitFail("Question already ended");
            }

            if (optionIndex < 0 || optionIndex >= question.Options.Length)
            {
                return AsyncSubmitFail("Invalid option");
            }

            _powerUps.TryGetValue(player.Nickname, out var puState);
            var correct = optionIndex == question.CorrectIndex;
            var disrupted = puState is not null && puState.DisruptedThisQuestion;
            var countsAsCorrect = correct && !disrupted;
            var startedAt = race.QuestionStartedAtUtc ?? nowUtc;
            var limit = QuestionTimeLimit(question);
            var points = ComputeSubmitPoints(countsAsCorrect, nowUtc, startedAt, limit, puState);
            player.Score += points;

            return AdvanceAsyncPlayerAfterAnswer(
                player.Nickname,
                race,
                question,
                nowUtc,
                points,
                correct);
        }
    }

    public (bool timedOut, AsyncSubmitResult? result) TryTimeoutAsyncPlayer(
        string nickname,
        DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (Phase != GamePhase.AsyncRacing)
            {
                return (false, null);
            }

            if (!_races.TryGetValue(nickname, out var race) || race.Finished)
            {
                return (false, null);
            }

            if (race.EndsAtUtc is null || nowUtc < race.EndsAtUtc.Value)
            {
                return (false, null);
            }

            var question = race.Current;
            if (question is null)
            {
                return (false, null);
            }

            var startedAt = race.QuestionStartedAtUtc ?? nowUtc;
            var limit = QuestionTimeLimit(question);
            _powerUps.TryGetValue(nickname, out var puState);
            _ = ComputeSubmitPoints(false, nowUtc, startedAt, limit, puState);

            var result = AdvanceAsyncPlayerAfterAnswer(
                nickname,
                race,
                question,
                nowUtc,
                pointsAwarded: 0,
                correct: false);

            return (true, result);
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
        CaptureReviewStatUnlocked();
        ClearDoubleActiveForAllPlayers();
        Phase = GamePhase.Reveal;
    }

    private void CaptureReviewStatUnlocked()
    {
        if (CurrentQuestion is null)
        {
            return;
        }

        var options = CurrentQuestion.Options;
        var counts = new int[options.Length];
        var correct = 0;
        foreach (var (_, optionIndex) in _answersThisQuestion)
        {
            if (optionIndex >= 0 && optionIndex < counts.Length)
            {
                counts[optionIndex]++;
                if (optionIndex == CurrentQuestion.CorrectIndex)
                {
                    correct++;
                }
            }
        }

        var playerCount = Math.Max(_players.Count(p => p.IsConnected), _answersThisQuestion.Count);
        var ihk = QuestionIndex >= 0 && QuestionIndex < _ihkPointsPerQuestion.Length
            ? _ihkPointsPerQuestion[QuestionIndex]
            : 0;
        _review.Add(new QuestionReviewStat
        {
            QuestionIndex = QuestionIndex,
            Text = CurrentQuestion.Text,
            Options = options.ToArray(),
            CorrectIndex = CurrentQuestion.CorrectIndex,
            Counts = counts,
            AnsweredCount = _answersThisQuestion.Count,
            PlayerCount = playerCount,
            CorrectCount = correct,
            Explanation = CurrentQuestion.Explanation,
            Tags = CurrentQuestion.Tags,
            IhkPoints = ihk,
        });
    }

    private void ClearDoubleActiveForAllPlayers()
    {
        foreach (var state in _powerUps.Values)
        {
            state.DoubleActive = false;
        }
    }

    private static void ClearPerQuestionPowerUpFlagsForState(PlayerPowerUpState state)
    {
        state.DoubleActive = false;
        state.MaskedWrongIndexes = null;
        state.UsedExtraTimeThisQuestion = false;
        state.DisruptedThisQuestion = false;
    }

    private void ClearPerQuestionPowerUpFlagsForPlayer(string nickname)
    {
        if (_powerUps.TryGetValue(nickname, out var state))
        {
            ClearPerQuestionPowerUpFlagsForState(state);
        }
    }

    private void ClearPerQuestionPowerUpFlags()
    {
        foreach (var state in _powerUps.Values)
        {
            ClearPerQuestionPowerUpFlagsForState(state);
        }

        _roomExtraTimeExtendedThisQuestion = false;
        _hostEventsUsedThisQuestion = 0;
        _boostAllActive = false;
    }

    private PlayerInfo? FindPlayerByNickname(string nickname) =>
        _players.FirstOrDefault(p =>
            string.Equals(p.Nickname, nickname, StringComparison.OrdinalIgnoreCase));

    private static TimeSpan QuestionTimeLimit(DemoQuestion question) =>
        TimeSpan.FromSeconds(question.TimeLimitSeconds <= 0 ? 20 : question.TimeLimitSeconds);

    private static AsyncSubmitResult AsyncSubmitFail(string error) =>
        new(false, error, 0, false, 0, null, false, false, null, null, null, 0, 0);

    private int ComputeSubmitPoints(
        bool countsAsCorrect,
        DateTimeOffset serverUtc,
        DateTimeOffset questionStartedAtUtc,
        TimeSpan scoreLimit,
        PlayerPowerUpState? puState)
    {
        int points;
        if (ExamMode)
        {
            var ihk = QuestionIndex >= 0 && QuestionIndex < _ihkPointsPerQuestion.Length
                ? _ihkPointsPerQuestion[QuestionIndex]
                : 0;
            points = countsAsCorrect ? ihk : 0;
            if (puState is not null)
            {
                puState.DisruptedThisQuestion = false;
                puState.DoubleActive = false;
            }
        }
        else
        {
            var elapsed = serverUtc - questionStartedAtUtc;
            points = Scoring.Score(countsAsCorrect, elapsed, scoreLimit);
            if (puState is not null)
            {
                puState.DisruptedThisQuestion = false;
            }

            if (points > 0)
            {
                if (puState is not null && puState.DoubleActive)
                {
                    points *= 2;
                    puState.DoubleActive = false;
                }

                if (_boostAllActive)
                {
                    points = (int)Math.Floor(points * 1.5);
                }
            }
            else if (puState is not null)
            {
                puState.DoubleActive = false;
            }
        }

        return points;
    }

    private AsyncSubmitResult AdvanceAsyncPlayerAfterAnswer(
        string nickname,
        PlayerRaceState race,
        DemoQuestion answeredQuestion,
        DateTimeOffset nowUtc,
        int pointsAwarded,
        bool correct)
    {
        ClearPerQuestionPowerUpFlagsForPlayer(nickname);

        race.Index++;
        var playerFinished = race.Finished;
        DemoQuestion? nextQuestion = null;
        DateTimeOffset? nextStartedAtUtc = null;
        DateTimeOffset? nextEndsAtUtc = null;

        if (!playerFinished)
        {
            nextQuestion = race.Current;
            nextStartedAtUtc = nowUtc;
            race.QuestionStartedAtUtc = nowUtc;
            nextEndsAtUtc = nowUtc + QuestionTimeLimit(nextQuestion!);
            race.EndsAtUtc = nextEndsAtUtc;
        }
        else
        {
            race.QuestionStartedAtUtc = null;
            race.EndsAtUtc = null;
        }

        var raceFinished = TryFinishAsyncRaceIfAllPlayersDone();

        return new AsyncSubmitResult(
            Ok: true,
            Error: null,
            PointsAwarded: pointsAwarded,
            Correct: correct,
            CorrectIndex: answeredQuestion.CorrectIndex,
            Explanation: answeredQuestion.Explanation,
            PlayerFinished: playerFinished,
            RaceFinished: raceFinished,
            NextQuestion: nextQuestion,
            NextStartedAtUtc: nextStartedAtUtc,
            NextEndsAtUtc: nextEndsAtUtc,
            NextIndex: race.Index,
            Total: race.Order.Count);
    }

    private bool TryFinishAsyncRaceIfAllPlayersDone()
    {
        if (_players.Count == 0)
        {
            return false;
        }

        foreach (var p in _players)
        {
            if (!_races.TryGetValue(p.Nickname, out var race) || !race.Finished)
            {
                return false;
            }
        }

        Phase = GamePhase.Finished;
        return true;
    }

    private static List<T> ShuffleCopy<T>(IReadOnlyList<T> source, Random rng)
    {
        var list = source.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    private static int[] PickWrongIndexes(int correctIndex, int optionCount, int maskCount)
    {
        var wrong = Enumerable.Range(0, optionCount).Where(i => i != correctIndex).ToList();
        for (var i = wrong.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (wrong[i], wrong[j]) = (wrong[j], wrong[i]);
        }

        return wrong.Take(maskCount).ToArray();
    }
}
