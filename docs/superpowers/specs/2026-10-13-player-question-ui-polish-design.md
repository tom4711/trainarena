# Design: Spieler Frage-Screen — Meta + Power-Ups Polish

**Datum:** 2026-10-13  
**Status:** Draft for review  
**Ziel:** Visuelles Polish am Spieler-Frage-Screen (Meta-Leiste + Power-Ups), Flows unverändert.

## Entscheidungen (aus Brainstorming)

| Frage | Wahl |
|-------|------|
| Art der Optimierung | A — Visuelles Polish, gleiche Flows |
| Fokus-Screens | B — Frage + Antworten (Kern-Gameplay) |
| Priorität im Frage-Screen | Meta-Leiste (Timer/Fortschritt) + Power-Ups |
| Ansatz | 1 — Meta-Bar + Chip-Power-Ups (CSS-first) |

## Scope

**In**

- `#question`-Panel in `wwwroot/player/`
- Meta: Fortschritt, Timer, Timer-Fortschrittsbalken
- Power-Ups: Chip-Layout, Count-Badge, bestehende Active-/Disabled-/Motion-Zustände schärfen
- Dateien: `player.css`, `index.html` (minimal), `player.js` (minimal: Track-Update, Button-Markup)

**Out**

- Join-, Warte-, Ranglisten-Redesign
- Antwort-Button-Redesign (Funktion/Look der Answers unverändert)
- Neue Power-Up-Logik, Hub-/Server-Änderungen
- Host-/Editor-UI
- Theme-Wechsel weg von Amber (Spieler behält Arena-Identität)

## Layout (Frage-Panel, oben → unten)

1. Meta-Leiste: `Frage x/y` links · Timer rechts  
2. Timer-Track (volle Breite, ~4px): Füllstand = Restzeit / Limit  
3. Fragetext (+ Bild/Code wie bisher)  
4. Power-Up-Chips (nur wenn sichtbar)  
5. Antwort-Buttons (unverändert)

## Meta-Leiste & Timer

- `.q-meta` bleibt Flex (Progress links, Timer rechts); Progress muted/kompakt, Timer tabular-nums in Amber.
- Neuer Track unter der Meta-Leiste; Füllfarbe folgt bestehenden Stufen `is-urgent` / `is-critical`.
- JS: vorhandener Timer-Tick (`startedAt` / `endsAt`) setzt zusätzlich `--timer-pct` (oder äquivalent) am Track — **kein** zweites Interval.
- Extra-Zeit: bestehendes `timer.is-extended` bleibt; Track folgt dem neuen `endsAt`.
- `prefers-reduced-motion`: kritische Timer-Pulse aus (wie bei anderen FX); Track nur statisch.

## Power-Up-Chips

- Position: unter Frage/Bild, vor Antworten; Sichtbarkeitsregel unverändert (Stock oder Active).
- Layout: Chips mit `width: auto`, Wrap, gleichmäßiger Gap — kein 50%-Flex-Grid mehr.
- Markup: Label + Count als Badge (statt `(n)` im Fließtext) in `ensurePowerUpButtons`.
- Zustände behalten: `disabled`, `is-active` (inkl. Shield/Double-Farben), `is-activating`, Count-Consume-Animation; nur Form/Spacing/Badge-Typografie.
- Touch: min. ~2.6rem Höhe, etwas mehr Horizontal-Padding.
- Target-Picker (Störimpuls) unverändert.

## Fehlerbehandlung / Regression

- Keine neuen API-Fehlerpfade.
- Power-Up- und Antwort-Flows müssen bitgleich funktionieren (IDs, Events, Inventory).
- Bei fehlendem Track-Element im DOM: Timer-Text weiter wie heute (defensiv optional).

## Abnahme

- Manuell: Timer inkl. urgent/critical + Balken; Power-Ups Count/Active/Disabled; Extra-Zeit; reduced-motion.
- Automatisiert: bestehende Hub-/Power-Up-Tests unverändert grün; keine Pflicht für neue Unit-Tests (reines UI-Polish).
- Erfolg: Meta und Power-Ups wirken aufgeräumter; gleiche Flows; keine Regression bei Antworten/Reveal.

## Nicht-Ziele (explizit)

- Sticky Header beim Scrollen  
- Angleichung an Host-Cyan  
- Große Antwort-Button-Überarbeitung in diesem Slice  
