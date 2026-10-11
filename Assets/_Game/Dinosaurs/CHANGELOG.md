# Change Log (changes to existing items)

Zach's side of the project: `Assets/_Game/Dinosaurs`, `World`, `Flow`.
This log lists **changes to things that already existed**: edits, fixes, tuning, renames and removals.
New things go in `UPDATELOG.md`. Newest entries at the top.

---

## 2026-10-11 (spit: earlier, more texture, brown)

### Changed
- **Spit comes out earlier:** `Gorgosaurus.spitDelay` changed from 0.5 s to 0.15 s, so it fires as the jaw finishes opening (Halar's attack opens the mouth over 0.15 s). It now leaves from a new `SpitMouth` point inside the front of the mouth (child of the head bone), not from the middle of the head. Measured in Play mode: 0.15 s from attack start to spit.
- **More texture:**
  - New 256 px droplet texture: viscous body with grain, a darker thick edge and a glossy highlight.
  - New 256 px wispy mist texture and new 512 px splat texture (darker rim, foam bubbles, more streaks and specks).
  - More particles: mouth burst 40–52 droplets (was 24–32), 45 droplets per metre of flight (was 34), 13–16 clump pieces (was 9–12), more mist; ground splash 38–48 droplets.
  - Droplets streak a bit more.
- **Colour: muddy brown instead of acid green.** Droplets and the clump are amber to dark brown, the mist is a dusty brown haze, and the splat is wet dark brown and slightly bigger (1.0–1.7 m).

## 2026-10-11 (spray-style acid spit)

### Changed
- **`Spit.prefab` is now a spray instead of one glowing ball.** The sphere mesh, material and trail were removed and replaced by three particle systems that face the direction of travel:
  - `Droplets`: a burst of ~28 droplets fanning out of the mouth, then more droplets streaming off as it flies. They fall with gravity and inherit the spit's speed.
  - `Mist`: soft acid haze along the path.
  - `Clump`: 9–12 wobbling droplets that make up the main body, so it reads as a textured clump rather than a perfect sphere.
- `SpitProjectile.cs`:
  - It now turns to face its velocity every frame, so the spray points the right way.
  - New `impactPrefab` field: `SpitSplash` is spawned at the hit point, aligned to the ground or tree surface. When it hits the player, the splash is half size and has no ground splat.
  - On impact, the droplet and mist trails detach and fade out naturally instead of vanishing.
  - Damage, speed, arc and hit radius are unchanged.

### Tested
- Rendered frames in Play mode: the spray leaving the Gorgosaurus mouth, droplets and mist along the arc, the splat on the ground, and the small splash on a player hit. No errors.

## 2026-10-10 (stuck-on-spawn fix)

### Fixed
- **Gorgosaurus froze right after spawning.** Its NavMeshAgent stopping distance was `attackRange × 0.8` = 11.2 m (spit range 14 m), but while circling in, each move target is only `lookAhead` = 8 m ahead. The agent counted every waypoint as already reached and never moved. This started when the Gorgosaurus range went back to 14 m with the spit; the Maip (3.8 m range) was unaffected.
  - While circling in, the stopping distance is now 0.5 m. The attack-range stopping distance only applies on the final direct approach.
  - The direct approach now starts at `max(directApproachDistance, attackRange)`, so a long-range attacker never circles inside its own attack range.

### Tested (DinoTest, 100 s, spawner sped up, dinosaurs killed as they arrived)
- 94 spawned (Maip and Gorgosaurus). 0 stuck. Every one reached attack range 5–12 s after spawning. No errors.

## 2026-10-10 (project check fixes)

### Fixed
- `Dinosaur.cs` death fallback: the tip-over only ran when nothing listened to `Died`. Since `DinoAudio` listens, a dinosaur without `ProceduralDinoAnim` would freeze upright on death. It now checks for `ProceduralDinoAnim` instead.
- `DinoSpawner.cs` opening dinosaur: if the opening dinosaur was removed without dying (`ClearAll`, destroyed), the spawner spawned a new opening dinosaur every frame. It now spawns the opening dinosaur once and treats a removed one as finished.
- `Gorgosaurus.prefab`: `Dinosaur.syncAnimationToSpeed` turned off, because `ProceduralDinoAnim` (match anim speed) already drives the Animator speed. Two scripts were setting it every frame.

### Checked
- Compiles with no errors.
- No missing scripts or broken references in any `_Game` prefab except `Player.prefab` (see the note below).
- Ran DinoTest, CombatTest, PlayerTest and Main in Play mode with no errors.
- Opening dinosaur, then normal spawns, then `ClearAll` during the opening: behaves correctly.

## 2026-10-10 (merged latest main)

### Merge
- Merged `main` into `zach/dinosaurs`. That brings in Halar's procedural animations (`ProceduralDinoAnim`), Zahid's alpha hit-and-retreat, the restored Gorgosaurus acid spit, the opening dinosaur in the spawner, the bow and player updates, and the new architecture rules (per-person branches, rule 14a on stray Unity files).
- Conflicts:
  - `Dinosaur.cs`: kept main's version and added three sound hooks: `Paused`, `ChargeStarted` and `Hit(zone, point)`. Main's `AttackStarted`, `WasHit` and `Died` are reused instead of duplicates.
  - `Maip.prefab`, `Gorgosaurus.prefab`: kept main's version (rule 20). The sound setup was re-added through Unity (below).

### Changed after the merge
- `DinoAudio.cs` now uses main's events:
  - The spawn call plays from its own `Start`.
  - The hurt cry is on `WasHit` (hits that don't kill).
  - The collapse thud plays `collapseDelay` seconds after `Died`, because Halar's procedural death replaced the tip-over.
  - Impacts are on `Hit`.
- `Maip.prefab`, `Gorgosaurus.prefab`: `DinoAudio` re-added on main's versions (same clips, pitch and strides as before, with the voice on the head bone). Collapse delay: Maip 0.8 s, Gorgosaurus 1.1 s.

### Tested
- `DinoTest` in Play mode with the merged code: dinosaurs spawn, move, attack and play sounds. Forced leg, body and head hits caused no errors.

## 2026-10-10 (sound)

### Changed
- `Dinosaur.cs`: added the events listed in UPDATELOG (sound hooks). Behaviour is unchanged.
- `Maip.prefab`, `Gorgosaurus.prefab`: added the `DinoAudio` component, two AudioSources and a `Voice` child under the head bone.
- `DinoTest`: added the `AmbientAudio` prefab.

## 2026-10-10 (DinoTest uses the spawner)

### Changed
- `DinoTest`: removed the two hand-placed dinosaurs (Maip and Gorgosaurus). Added a `DinoSpawner` prefab instance at the origin with **Mode = Always**, so dinosaurs start spawning around the `DummyPlayer` as soon as you press Play (first one after ~1.5 s, up to 6 alive).

## 2026-10-10 (cleanup, test positions)

### Fixed
- The commit "Zach dinotest Branch" accidentally included files outside our folders. They are reverted to their previous versions:
  - `ProjectSettings/VersionControlSettings.asset`
  - the terrain pack's grass, bush and water materials
  - `AndroidXRSettingsInitializer`
  - `BasicScene/Grid.mat`
  - `Assets/Editor.meta` (removed)
- Removed the full 151k-triangle `Maip.fbx` and its two 2K source textures (one has the NHK logo) from the repo and from the project; the local copies went to the Recycle Bin. `Maip_LOD.fbx` is the model we use.

### Changed
- `DinoTest`: the test dinosaurs are further out. The Maip moved from (-6, 0, 20) to (-15, 0, 40), and the Gorgosaurus from (-4.9, 0, 5) to (18, 0, 42), so you can watch their whole approach.

## 2026-10-10 (melee only, natural movement)

### Removed
- Spit attack: `SpitProjectile.cs`, `Spit.prefab`, `Spit.mat` and the empty `Materials` folder were deleted (through Unity).

### Changed
- **Both dinosaurs are melee now.** The bite attack moved into `Dinosaur.cs` and is shared. `Maip.cs` and `Gorgosaurus.cs` now only hold default numbers.
  - Gorgosaurus: bite range 5 m, 0.6 s wind-up, 2.2 s cooldown, 15 damage, 5 body hits, speed 3.
  - Maip: unchanged (range 3.8 m, 10 damage, 3 body hits, speed 6).
- **Movement is no longer a straight line at the player.** Far away, each dinosaur:
  - circles in from a random side (10–35°, chosen once per dinosaur)
  - weaves left and right (smooth noise, wider for the Maip, lazier for the Gorgosaurus)
  - drifts in speed, and has its own ±15% speed
  - sometimes pauses for 0.6–1.8 s, like it's sniffing or looking around
  - Within 9 m it stops circling and goes straight in. Within the charge distance it speeds up (Maip ×1.5, Gorgosaurus ×1.3) and brakes so it stops at biting range.
- The path is recalculated at random intervals (0.3–0.7 s) instead of a fixed 0.25 s.
- The first bite and each cooldown get a little randomness.
- Getting shot interrupts a pause.
- Animation playback speed follows movement speed, so it slows when the dinosaur slows.
- On death the animation freezes, and the body tips over to a random side.
- NavMeshAgent turning is slower and smoother (Maip 200°/s, Gorgosaurus 120°/s).
- All the new numbers are Inspector fields under **Natural approach**, **Pauses** and **Charge**.

### Tested (Play mode, DinoTest, dinosaurs moved far out for the test only)
- The Maip, starting 43 m away, curved in and reached biting range in ~10 s.
- The Gorgosaurus, starting 46 m away, circled in from the side and reached biting range in ~18 s.
- Both stop at their bite range and hit the `DummyPlayer`. No errors.

## 2026-10-10 (DinoTest setup)

### Fixed
- `Gorgosaurus.cs` line-of-sight check: the player's own body collider counted as "blocking", so the Gorgosaurus walked into the player instead of spitting. It now ignores anything that is part of the player, plus the `Dinosaur`, `Player`, `EnemyProjectile` and `Arrow` layers.

## 2026-10-10 (prefabs)

### Changed
- `Gorgosaurus.fbx` and `Maip_LOD.fbx` import settings: the animation clip is renamed from "Scene" to `Gorgosaurus_Loop` / `Maip_Loop` and set to **Loop Time**.

## 2026-10-10

### Changed
- `Models/Gorgosaurus/Textures/Material.003_normal.png`: Texture Type set to **Normal map**, so lighting reads the normal map correctly.
- `Models/Gorgosaurus/Gorgosaurus.fbx` and `Models/Maip/Maip_LOD.fbx`: import settings changed so the textures are extracted into a `Textures/` folder and the materials pick them up (they showed untextured before).

### Not touched (for the record)
- Nothing outside `Dinosaurs/`, `World/` or `Flow/` was edited.
- Some files outside these folders show as modified, but Unity changed them by itself. They are not committed and belong to Zahid:
  - `Assets/XR/Settings/OpenXRPackageSettings.asset`
  - `Assets/XR/AndroidXR/AndroidXRSettingsInitializer`
  - `ProjectSettings/ShaderGraphSettings.asset`
  - `Assets/TerrainDemoScene_URP/.../Cypress_Forest_Desktop.mat`
  - `Assets/Editor.meta`
