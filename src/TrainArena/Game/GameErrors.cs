namespace TrainArena.Game;

/// <summary>German user-facing error strings from the domain (Hub may pass through).</summary>
public static class GameErrors
{
    public const string NicknameRequired = "Nickname erforderlich";
    public const string NicknameTaken = "Nickname bereits vergeben";
    public const string UnknownNickname = "Unbekannter Nickname in diesem Raum";
    public const string GameAlreadyStarted = "Spiel bereits gestartet";
    public const string NeedPlayer = "Mindestens ein Spieler nötig";
    public const string NoPlayers = "Keine Spieler";
    public const string NoQuestions = "Keine Fragen";
    public const string NoActiveQuestion = "Keine aktive Frage";
    public const string QuestionEnded = "Frage bereits beendet";
    public const string NotInRoom = "Nicht in diesem Raum";
    public const string AlreadyAnswered = "Bereits geantwortet";
    public const string InvalidOption = "Ungültige Antwortoption";
    public const string PowerUpsDisabled = "Power-Ups sind deaktiviert";
    public const string HostEventsDisabled = "Host-Arena-Events sind deaktiviert";
    public const string NotAPlayer = "Kein Spieler in diesem Raum";
    public const string NoPowerUpsLeft = "Keine Power-Ups übrig";
    public const string NotPlayerPowerUp = "Kein Spieler-Power-Up";
    public const string NotHostEvent = "Kein Host-Arena-Event";
    public const string HostOnly = "Nur der Host kann Arena-Events auslösen";
    public const string HostBudgetExhausted = "Host-Event-Kontingent für diese Frage aufgebraucht";
    public const string UnknownHostEvent = "Unbekanntes Host-Arena-Event";
    public const string UnknownPowerUp = "Unbekanntes Power-Up";
    public const string PowerUpFailed = "Power-Up fehlgeschlagen";
    public const string ArenaEventFailed = "Arena-Event fehlgeschlagen";
    public const string SelfEffectAlreadyUsed = "Selbst-Effekt in dieser Frage bereits genutzt";
    public const string TargetNotFound = "Ziel nicht gefunden";
    public const string NoValidTarget = "Kein gültiges Ziel";
    public const string InvalidTarget = "Ungültiges Ziel";
    public const string FiftyFiftyNeedsOptions = "50/50 braucht mindestens 3 Optionen";
    public const string WrongPhase = "In dieser Phase nicht möglich";
    public const string FinishOnlyLeaderboard = "Abschluss nur von der Rangliste";
    public const string MoreQuestions = "Es sind noch Fragen übrig";
    public const string NoAsyncMode = "Kein Async-Modus";
    public const string NoAsyncRace = "Kein Async-Rennen aktiv";
    public const string QuestionAlreadyAdvanced = "Frage bereits weitergeschaltet";
    public const string InvalidPhase = "Ungültige Phase";
    public const string InvalidCorrectIndex = "Ungültiger Index der richtigen Antwort";
    public const string OptionsRange = "Frage muss 2–6 Optionen haben";
    public const string CannotStartQuestion = "Frage kann in der aktuellen Phase nicht gestartet werden";
    public const string CannotStart = "Start nicht möglich";
    public const string NoQuestionAvailable = "Keine Frage verfügbar";
    public const string RoomNotFound = "Raum nicht gefunden";
    public const string QuizNotFoundOrEmpty = "Quiz nicht gefunden oder leer";
    public const string QuizHasNoQuestions = "Quiz hat keine Fragen";
    public const string JoinFailed = "Beitritt fehlgeschlagen";
    public const string ReconnectFailed = "Reconnect fehlgeschlagen";
    public const string FinishFailed = "Abschluss nicht möglich";
    public const string AsyncExamIncompatible = "Async-Rennen und Prüfungsmodus sind nicht kombinierbar.";
    public const string AsyncAutoAdvanceIncompatible = "Async-Rennen und Auto-Advance sind nicht kombinierbar.";
    public const string NoQuestionsForTags = "Keine Fragen für die gewählten Tags.";
}
