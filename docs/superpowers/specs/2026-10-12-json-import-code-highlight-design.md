# Design: JSON-Quiz-Import + Code-Syntax-Highlight

**Stand:** 2026-10-12  
**Ziel:** Quizzes mit Fragetypen und Bildern importierbar machen; Code in Fragetexten lesbar highlighten.

## JSON-Import

- CSV/Kahoot bleibt. Import akzeptiert `.csv` und `.json` (Erkennung per Endung, Fallback: Inhalt beginnt mit `{` / `[`).
- Schema (Quiz-Pack oder Fragen-Array):

```json
{
  "title": "C# Grundlagen — Programmierung",
  "questions": [
    {
      "text": "Was gibt dieser Ausdruck?\n\n```csharp\nint x = 5;\n```\n",
      "displayKind": "mc",
      "options": ["A", "B", "C", "D"],
      "correctIndex": 0,
      "timeLimitSeconds": 25,
      "image": { "fileName": "diagram.png", "contentType": "image/png", "base64": "..." }
    },
    {
      "text": "var leitet den Typ ab.",
      "displayKind": "trueFalse",
      "correctIndex": 0,
      "timeLimitSeconds": 15
    }
  ]
}
```

- `displayKind`: `mc` (Default) | `trueFalse` (Optionen optional → Wahr/Falsch).
- Bilder: optional eingebettet (Base64 → `QuestionImageStore`); max. 2 MB, gleiche MIME-Whitelist.
- Validierung über bestehende `QuizRules`.
- `title` im Pack ist nur Doku/Hinweis; Import hängt Fragen an das geöffnete Quiz.

## Syntax-Highlight

- Konvention im Fragetext: Markdown-Fence ` ```sprache ` … ` ``` ` (Default-Sprache `csharp` wenn leer).
- Host + Player: gemeinsames `question-format.js` — Text escapen, Fences → `<pre><code class="language-…">`, Prism (CDN) highlighten.
- Kein volles Markdown; nur Fences + Zeilenumbrüche außerhalb als `<br>` / Absätze.
- Editor-Liste darf Rohtext zeigen (kein Muss für Highlight).

## Nicht-Ziele

- JSON-Export, ZIP-Packs, Bild-URLs von extern, volle Markdown-Engine.
