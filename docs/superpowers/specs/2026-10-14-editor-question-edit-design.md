# Design: Bestehende Fragen im Quiz-Editor bearbeiten

**Datum:** 2026-10-14  
**Status:** Implemented  
**Ziel:** Im Quiz-Editor bestehende Fragen inline anpassen können (nicht nur hinzufügen/löschen).

## Entscheidungen (Brainstorming)

| Thema | Wahl |
|-------|------|
| UX | Inline in der Fragenliste: „Bearbeiten“ öffnet das Formular in der Karte |
| Bild | Optional neues Upload ersetzt alt; Checkbox „Bild entfernen“ |
| Fragetyp | Wechsel MC ↔ Wahr/Falsch erlaubt (wie beim Anlegen) |
| Reihenfolge | Nicht in diesem Slice (SortOrder unverändert) |

## Scope

**In**

- Button **Bearbeiten** pro Frage neben **Löschen**
- Edit-Modus: vorausgefülltes Formular (gleiche Felder wie „Frage hinzufügen“): Text, DisplayKind, Optionen A–F bzw. Wahr/Falsch, Bild, TimeLimit, Explanation, Tags, Points
- Speichern → `OnPostUpdateQuestionAsync`; Validierung über bestehende `QuizRules`
- Abbrechen → zurück zur Listenansicht ohne Speichern
- Nur **eine** Frage gleichzeitig im Edit-Modus (Query `?edit={questionId}` oder Page-Property `EditingQuestionId`)
- Bild: Vorschau wenn vorhanden; neues File ersetzt und löscht alte Datei; `removeImage` entfernt Bild ohne Ersatz

**Out (MVP)**

- Drag-and-drop / SortOrder ändern
- Bulk-Edit, Duplizieren
- Separate Question-Detail-Seite oder Modal
- Versionierung / Undo-Historie

## UI (`Edit.cshtml`)

- Standard: Frage-Karte wie heute (Text, Optionen, Meta) + **Bearbeiten** + **Löschen**
- Wenn `EditingQuestionId == q.Id`: Karte zeigt Edit-Formular statt Read-only-Inhalt
- Form: `method="post" asp-page-handler="UpdateQuestion"`, Hidden `id` (Quiz), `questionId`
- Buttons: **Änderungen speichern**, **Abbrechen** (Link zurück zu `/Editor/Edit/{quizId}` ohne `edit`)
- JS für MC/TF-Umschaltung wiederverwenden (IDs ggf. pro Formular uniquen oder gemeinsames Script parametrisieren)

## Server (`Edit.cshtml.cs`)

```csharp
OnPostUpdateQuestionAsync(
    Guid id, Guid questionId,
    string text, string? displayKind,
    string? option0…option5, int correctIndex,
    int timeLimitSeconds, string? explanation, string? tags, int points,
    IFormFile? image, bool removeImage = false)
```

Ablauf:

1. Frage laden (`QuizId == id && Id == questionId`); sonst NotFound
2. DisplayKind + Optionen wie bei Add (TF setzt Wahr/Falsch)
3. `QuizRules.ValidateQuestion` — bei Fehler `ErrorMessage`, Seite mit `EditingQuestionId` neu rendern
4. Bild:
   - `removeImage` → `TryDelete` + `ImagePath = null`
   - neues `image` → speichern, altes löschen, `ImagePath` setzen
   - sonst `ImagePath` unverändert
5. Felder updaten, `SortOrder` **nicht** ändern
6. `SaveChanges` → Redirect zu Edit ohne `edit`-Query

`OnGetAsync`: optional `Guid? edit` → setzt `EditingQuestionId` wenn Frage zum Quiz gehört.

## Tests

- Persistence/integration: Update ändert Text/Optionen/CorrectIndex; Bild entfernen setzt `ImagePath` null
- Optional: Typwechsel MC → TF schreibt Wahr/Falsch-Optionen
- Bestehende Add/Delete/Import-Tests bleiben grün

## Abnahme

1. Bestehende Frage öffnen → Bearbeiten → Text ändern → speichern → Liste zeigt neuen Text  
2. Bild ersetzen und entfernen funktioniert  
3. Typ MC → Wahr/Falsch und zurück möglich  
4. Abbrechen verwirft Änderungen  
5. Löschen und Hinzufügen unverändert  
