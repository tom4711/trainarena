# Design: Erklärungen, Tags, IHK-Prüfungsmodus

**Stand:** 2026-10-12

## Erklärungen (optional)
- Spalte `Questions.Explanation` (TEXT, nullable).
- JSON: `"explanation": "…"`.
- Anzeige nach `QuestionEnded`, wenn nicht leer.
- Im **Prüfungsmodus** erst in der Abschluss-Review (nicht während der Laufzeit).

## Tags
- Spalte `Questions.Tags` — kommagetrennt, normalisiert (trim, lower).
- JSON: `"tags": ["sql", "join"]`.
- Host: beim Raum erstellen Multi-Select „nur Fragen mit Tag …“ (OR).
- Editor: Tags an Frage; Quiz-Übersicht zeigt verfügbare Tags.

## Prüfungsmodus + IHK
- CreateRoom: `exam: { enabled, tags? }`.
- Power-Ups aus (Starter/Host-Events/Streak aus).
- Punkte: kein Speed-Bonus. Jede Frage hat `points` (Gewicht, Default gleichmäßig).
  Summe der Gewichte der **gespielt**en Fragen wird auf **100** normiert.
- Ende: Punkte 0–100, Note nach IHK-Schlüssel, Bestanden ab 50 (Note ≤ 4).

| Punkte | Note |
|--------|------|
| 100–92 | 1 |
| 91–81 | 2 |
| 80–67 | 3 |
| 66–50 | 4 |
| 49–30 | 5 |
| 29–0 | 6 |
