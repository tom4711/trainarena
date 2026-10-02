namespace TrainArena.Game;

public sealed class PlayerInfo
{
    public required string Nickname { get; init; }
    public required string ConnectionId { get; init; }
    public int Score { get; set; }
}

/// <summary>
/// In-memory state for one live room.
/// </summary>
public sealed class GameSession
{
    private readonly object _gate = new();
    private readonly List<PlayerInfo> _players = new();
    private readonly Dictionary<string, int> _answersThisQuestion = new(StringComparer.Ordinal);
    private List<DemoQuestion> _quizQuestions = new();

    public GameSession(string code, string hostConnectionId, Guid quizId)
    {
        Code = code;
        HostConnectionId = hostConnectionId;
        QuizId = quizId;
    }

    public string Code { get; }
    public string HostConnectionId { get; }
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

            if (_players.Any(p => string.Equals(p.Nickname, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, "Nickname already taken");
            }

            _players.Add(new PlayerInfo
            {
                Nickname = trimmed,
                ConnectionId = connectionId
            });
            return (true, null);
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
            CurrentQuestion = question;
            QuestionIndex++;
            QuestionStartedAtUtc = nowUtc;
            QuestionEndsAtUtc = nowUtc + limit;
            _answersThisQuestion.Clear();
            Phase = GamePhase.QuestionActive;
            return (true, null);
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

            var player = _players.FirstOrDefault(p => p.ConnectionId == connectionId);
            if (player is null)
            {
                return (false, "Not a player in this room", 0);
            }

            if (_answersThisQuestion.ContainsKey(connectionId))
            {
                return (false, "Already answered", 0);
            }

            if (optionIndex is < 0 or > 3)
            {
                return (false, "Invalid option", 0);
            }

            _answersThisQuestion[connectionId] = optionIndex;
            var correct = optionIndex == CurrentQuestion.CorrectIndex;
            var elapsed = serverUtc - QuestionStartedAtUtc.Value;
            var limit = QuestionEndsAtUtc.Value - QuestionStartedAtUtc.Value;
            var points = Scoring.Score(correct, elapsed, limit);
            player.Score += points;
            return (true, null, points);
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

            if (nowUtc < QuestionEndsAtUtc.Value
                && _answersThisQuestion.Count < _players.Count)
            {
                return false;
            }

            Phase = GamePhase.Reveal;
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

            Phase = GamePhase.Reveal;
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

    /// <summary>Legacy helper — prefer <see cref="TryFinish"/>.</summary>
    public void Finish()
    {
        TryFinish();
    }

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
}
