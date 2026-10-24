# Design: Quiz-Sounds (Host BGM + Player SFX)

## Ziel

Optionales Audio-Feedback ohne Gameplay-Änderung: ruhige Host-Hintergrundmusik und kurze Player-SFX für Antworten und Power-Ups. LAN/self-host mit lokalen Assets.

## Architektur

- `wwwroot/js/audio.js` — `TrainArenaAudio.createAudioController({ role })`
- Host: BGM-Loop (`/audio/bgm-host.ogg`), Mute + Volume im Footer
- Player: SFX (`correct` / `wrong` / `powerup` / `hit` / `block`), Mute auf Join
- Unlock nach User-Gesture (`Raum erstellen` / `Beitreten` / Mute-Klick) wegen Browser-Autoplay
- Mute/Volume in `localStorage`; Default-BGM ~22 %, SFX ~55 %
- `prefers-reduced-motion` steuert FX-Dauer wie bisher, mutet Audio nicht

## Trigger

| Ereignis | Audio |
|----------|--------|
| Host `RoomCreated` | BGM start/resume |
| Player `AnswerAccepted` ok | `correct` wenn `points > 0`, sonst `wrong` |
| Player `PowerUpUsed` (50/50, Double, Extra-Zeit) | `powerup` |
| Player `PowerUpFx` | `powerup` / `hit` / `block` je nach Kind |

## Assets & Lizenz

Siehe `docs/ATTRIBUTION.md` (CC0 BGM + CC0 TrainArena-SFX).

## Non-Goals

Keine Server-Events für Audio, kein Sync des Host-Mutes zu Spielern, keine Gameplay-/Scoring-Änderungen.
