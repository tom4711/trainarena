using Microsoft.EntityFrameworkCore;
using TrainArena.Data.Entities;
using TrainArena.Game;

namespace TrainArena.Data;

/// <summary>
/// Seeds starter quizzes and exposes the legacy single demo question.
/// </summary>
public static class SeedData
{
    public static readonly Guid AusbildungBasicsQuizId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid CsharpBasicsQuizId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>First question of the Ausbildung seed quiz — kept for Phase 0 tests.</summary>
    public static DemoQuestion DemoQuestion { get; } = new()
    {
        Text = "Wie viele Bundesländer hat Deutschland?",
        Options = ["14", "15", "16", "17"],
        CorrectIndex = 2,
        TimeLimitSeconds = 20
    };

    public static async Task EnsureSeededAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.EnsureSchemaAsync(ct);

        await EnsureQuizAsync(db, BuildAusbildungBasics(), ct);
        await EnsureQuizAsync(db, BuildCsharpBasics(), ct);
    }

    private static async Task EnsureQuizAsync(AppDbContext db, Quiz quiz, CancellationToken ct)
    {
        if (await db.Quizzes.AnyAsync(q => q.Id == quiz.Id, ct))
        {
            return;
        }

        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync(ct);
    }

    private static Quiz BuildAusbildungBasics() => new()
    {
        Id = AusbildungBasicsQuizId,
        Title = "Ausbildung Basics",
        Questions =
        [
            Q("11111111-1111-1111-1111-111111111101", 0, 20,
                "Wie viele Bundesländer hat Deutschland?",
                ["14", "15", "16", "17"], 2),
            Q("11111111-1111-1111-1111-111111111102", 1, 20,
                "Was bedeutet die Abkürzung „Azubi“?",
                ["Auszubildender", "Außerordentlicher Bilanzierer", "Arbeitszeitbilanz", "Ausbildungszuschuss intern"], 0),
            Q("11111111-1111-1111-1111-111111111103", 2, 25,
                "Wer schließt den Ausbildungsvertrag?",
                [
                    "Nur der Ausbilder",
                    "Ausbildungsbetrieb und Auszubildende/r (ggf. gesetzliche Vertretung)",
                    "Nur die Berufsschule",
                    "Die Industrie- und Handelskammer allein"
                ], 1),
            Q("11111111-1111-1111-1111-111111111104", 3, 20,
                "Wofür steht „BBiG“?",
                [
                    "Betriebs- und Bilanzgesetz",
                    "Berufsbildungsgesetz",
                    "Bundesbildungsinitiative Gesetz",
                    "Betriebsstätten-Bauordnung intern generell"
                ], 1),
        ]
    };

    /// <summary>
    /// Allgemeine Programmiergrundlagen mit C#-Bezug:
    /// Datentypen, Variablen, Verzweigungen, Schleifen, Funktionen.
    /// </summary>
    private static Quiz BuildCsharpBasics() => new()
    {
        Id = CsharpBasicsQuizId,
        Title = "C# Grundlagen — Programmierung",
        Questions =
        [
            // --- Datentypen ---
            Q("22222222-2222-2222-2222-222222222201", 0, 20,
                "Welcher C#-Datentyp speichert ganze Zahlen ohne Nachkommastellen?",
                ["int", "double", "bool", "string"], 0),
            Q("22222222-2222-2222-2222-222222222202", 1, 22,
                "Welcher Typ speichert die Wahrheitswerte true und false?",
                ["char", "bool", "byte", "decimal"], 1),
            Q("22222222-2222-2222-2222-222222222203", 2, 25,
                "Welcher Typ eignet sich in C# besonders für Geldbeträge (exakte Dezimalzahlen)?",
                ["float", "double", "decimal", "int"], 2),
            Q("22222222-2222-2222-2222-222222222204", 3, 22,
                "Was beschreibt am besten den Typ string in C#?",
                [
                    "Ein Werttyp für einzelne Zeichen",
                    "Ein Verweistyp für Textfolgen",
                    "Ein ganzzahliger Typ mit 64 Bit",
                    "Ein Typ nur für Zahlen mit Komma"
                ], 1),

            // --- Variablen ---
            Q("22222222-2222-2222-2222-222222222205", 4, 22,
                "Welches Schlüsselwort lässt den Compiler den Typ einer lokalen Variable ableiten?",
                ["object", "dynamic", "var", "typeof"], 2),
            Q("22222222-2222-2222-2222-222222222206", 5, 22,
                "Was gilt für eine mit const deklarierte Konstante?",
                [
                    "Sie kann zur Laufzeit beliebig geändert werden",
                    "Ihr Wert muss zur Compile-Zeit feststehen",
                    "Sie darf nur in Schleifen verwendet werden",
                    "Sie speichert immer einen string"
                ], 1),
            Q("22222222-2222-2222-2222-222222222207", 6, 25,
                "Was ist nach int x = 5; int y = x; y = 9; der Wert von x?",
                ["9", "5", "0", "Der Code kompiliert nicht"], 1),

            // --- Verzweigungen ---
            Q("22222222-2222-2222-2222-222222222208", 7, 22,
                "Welche Kontrollstruktur prüft eine Bedingung und führt dann einen von zwei Zweigen aus?",
                ["for", "if / else", "foreach", "using"], 1),
            Q("22222222-2222-2222-2222-222222222209", 8, 22,
                "Wofür eignet sich switch in C# besonders gut?",
                [
                    "Zum Öffnen von Dateien",
                    "Zur Mehrfachauswahl anhand eines Ausdrucks",
                    "Zum Definieren von Klassen",
                    "Zum Beenden des Programms"
                ], 1),
            Q("22222222-2222-2222-2222-222222222210", 9, 25,
                "Was liefert der Ausdruck (score >= 50) ? \"Bestanden\" : \"Nicht bestanden\", wenn score = 40?",
                ["Bestanden", "Nicht bestanden", "true", "40"], 1),

            // --- Schleifen ---
            Q("22222222-2222-2222-2222-222222222211", 10, 22,
                "Welche Schleife eignet sich besonders zum Durchlaufen aller Elemente einer Liste?",
                ["goto", "foreach", "switch", "lock"], 1),
            Q("22222222-2222-2222-2222-222222222212", 11, 25,
                "Was ist der Hauptunterschied zwischen while und do-while?",
                [
                    "do-while gibt es in C# nicht",
                    "do-while prüft die Bedingung erst nach dem ersten Durchlauf",
                    "while kann keine Variablen ändern",
                    "while läuft immer genau einmal"
                ], 1),
            Q("22222222-2222-2222-2222-222222222213", 12, 22,
                "Was bewirkt continue in einer Schleife?",
                [
                    "Beendet die gesamte Methode",
                    "Beendet die Schleife komplett",
                    "Springt zum nächsten Schleifendurchlauf",
                    "Löscht die Schleifenvariable"
                ], 2),

            // --- Funktionen / Methoden ---
            Q("22222222-2222-2222-2222-222222222214", 13, 22,
                "Was bedeutet der Rückgabetyp void bei einer Methode?",
                [
                    "Die Methode gibt einen leeren string zurück",
                    "Die Methode gibt keinen Wert zurück",
                    "Die Methode darf keine Parameter haben",
                    "Die Methode ist veraltet"
                ], 1),
            Q("22222222-2222-2222-2222-222222222215", 14, 25,
                "Was macht return in einer Methode mit Rückgabetyp int?",
                [
                    "Beendet nur die aktuelle Schleife",
                    "Liefert einen int-Wert an den Aufrufer und verlässt die Methode",
                    "Deklariert eine neue Variable",
                    "Startet die Methode erneut"
                ], 1),
            Q("22222222-2222-2222-2222-222222222216", 15, 25,
                "Wann spricht man in C# von Methodenüberladung (Overloading)?",
                [
                    "Wenn eine Methode sich selbst aufruft",
                    "Wenn mehrere Methoden denselben Namen, aber unterschiedliche Parameterlisten haben",
                    "Wenn eine Methode als async markiert ist",
                    "Wenn eine Methode private ist"
                ], 1),
        ]
    };

    private static Question Q(
        string id,
        int sortOrder,
        int timeLimitSeconds,
        string text,
        string[] options,
        int correctIndex) =>
        new()
        {
            Id = Guid.Parse(id),
            Text = text,
            Option0 = options.ElementAtOrDefault(0) ?? "",
            Option1 = options.ElementAtOrDefault(1) ?? "",
            Option2 = options.ElementAtOrDefault(2) ?? "",
            Option3 = options.ElementAtOrDefault(3) ?? "",
            CorrectIndex = correctIndex,
            TimeLimitSeconds = timeLimitSeconds,
            SortOrder = sortOrder,
            DisplayKind = QuestionDisplayKind.Mc,
        };
}
