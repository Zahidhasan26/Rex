# CHANGELOG — Halar (Dinosaurs + World & Round)

**Changes to things that already exist** (edits, fixes, tuning, replacements).
New things go in `UPDATELOG.md` instead.

Branches: `halar/dinos-world` (merged into main), `halar/dino-anim` (current)
Format: newest entry at the top. One entry per change.

```
## YYYY-MM-DD — short title
- What changed:
- Files:
- Why:
- How to test:
```

---

## 2026-10-10 — Changes for the procedural dinosaur animations (branch `halar/dino-anim`)
- **`Dinosaur.cs` (Zach's script, smallest possible change, Zach informed):**
  - Added `public bool IsAttacking` and three events: `AttackStarted` (raised right before an attack starts), `WasHit(HitZone)` (raised after a hit that does not kill) and `Died` (raised when it dies).
  - Death now raises `Died` first. Zach's tip-over (`FallOver`) still runs as the fallback when nothing listens to `Died`, i.e. for any dinosaur without `ProceduralDinoAnim`. Tested: a Maip without the script still tips over.
  - No other behaviour changed.
- **`Maip.prefab`**: `corpseTime` 4 s → **3 s**.
- **`Gorgosaurus.prefab`**: `moveSpeed` 3 → **4.5** (it was the slow one), `bodyHitsToKill` 5 → **3** (same as the Maip), `corpseTime` 4 s → **3 s**.
- Why: Halar's spec (3 hits kill, disappear after 3 s, slow dinosaur a bit faster).
- How to test: DinoTest, 3 body hits kill either dinosaur; the body disappears 3 s after death.

## 2026-10-10 — Log started
- No existing files changed yet.
- Note: Unity changed `TerrainDemoScene.unity` and `OpenXRPackageSettings.asset` by itself when the project was opened. Both are outside my folders, so they were undone and NOT committed. If they show up again in GitHub Desktop, discard them.
