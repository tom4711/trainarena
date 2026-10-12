# Beispiel-Quizzes

Import im Editor unter einem Quiz: **Import CSV / JSON**.

| Datei | Format | Hinweis |
|-------|--------|---------|
| [csharp-grundlagen-programmierung.json](./csharp-grundlagen-programmierung.json) | JSON | Fragetypen, Code-Fences (` ```csharp `), optional Base64-Bilder |
| [csharp-grundlagen-programmierung.csv](./csharp-grundlagen-programmierung.csv) | CSV | Nur Multiple Choice (ohne Code-Highlight-Fences) |

## JSON-Schema (kurz)

```json
{
  "title": "Optional",
  "questions": [
    {
      "text": "Frage mit optionalem\n\n```csharp\nint x = 1;\n```",
      "displayKind": "mc",
      "options": ["A", "B", "C", "D"],
      "correctIndex": 0,
      "timeLimitSeconds": 20,
      "image": { "fileName": "x.png", "contentType": "image/png", "base64": "..." }
    },
    {
      "text": "Aussage…",
      "displayKind": "trueFalse",
      "correctIndex": 0
    }
  ]
}
```

`displayKind`: `mc` (Default) oder `trueFalse`. Bilder max. 2 MB (JPEG/PNG/WebP/GIF).
