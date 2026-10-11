# Change Log (changes to existing items)

Zach's side of the project: `Assets/_Game/Dinosaurs`, `World`, `Flow`.
This log lists **changes to things that already existed**: edits, fixes, tuning, renames and removals.
New things go in `UPDATELOG.md`. Newest entries at the top.

---

## 2026-10-11 (VR in DinoTest)

### Changed
- **`Assets/Scenes/DinoTest.unity` now runs in VR.** Zahid's `Player` prefab (XR rig with the bow) is in the scene at the origin, so with the headset connected you see the dinosaurs at real scale and can shoot them (hold the right trigger, pull back, let go).
- The old desktop setup (Main Camera with the click-to-hit tester, and `DummyPlayer`) is kept but switched off under **"Desktop Test (off - tick to use without headset)"**. To test without the headset, tick that object and untick `Player`.

## 2026-10-11 (boss animation fix)

### Fixed
- **The boss stood still with no animation.** Re-running `BossBuilder` deleted and re-created `Boss.controller`, which left the open scene and the prefab pointing at a controller that no longer existed. The builder now rebuilds the controller in place and re-imports the prefab at the end. BossTest now plays idle, walk, sidestep, backing away, throw, charge wind-up, run, smash and the deaths.
- Two boss script updates hadn't reached the project (the copy on disk was older). They are in now: the attack cooldown starts when an attack ends, so a charge-smash is no longer followed straight away by a second smash, and the orb glow is toned down.

## 2026-10-11 (final boss)

### Changed
- No existing dinosaur files changed. The boss reuses `Dinosaur.cs` and `SpitProjectile.cs` as they are, and the orb's particles reuse the `Spit_Mist` and `Spit_Droplet` materials.

## 2026-10-11 (death variants and sidestepping)

### Changed
- `Dinosaur.cs`: sidestepping behaviour and the `Strafe` / `DieIndex` animator parameters (details in UPDATELOG). Speed sync plays the sidestep clips near full speed.
- **`ProceduralDinoAnim.cs` (Halar's script, two small changes):**
  - If the Animator has a `Die` trigger, the death clip plays the fall. The script no longer freezes the Animator or rolls the body; it only sinks the body before it is removed.
  - The procedural Maip walk now uses forward speed only, so it doesn't fight the sidestep clips.
- `DinoIdlePose.cs`: fades out on death so the death clip shows (it used to hold the standing pose). It picks the `_Loop` clip for its pose now that the controller holds several clips.
- `Maip.controller`, `Gorgosaurus.controller`: rebuilt by `DinoClipBuilder` (the old single "Loop" state is now the middle of the Locomotion blend tree).

## 2026-10-11 (lighter spit, idle while spitting)

### Changed
- **Less smoky spit.** The mist is fainter (alpha 0.2–0.3, was 0.45–0.55), smaller, fewer puffs and shorter-lived, and it no longer hangs in the air. The splash mist is lighter too.
- **No more cone shape.** Mist and droplets now scatter in a loose sphere around the travelling core (random directions) and inherit its speed, so the spray is an irregular cloud that moves with the spit instead of a widening cone from the mouth.
- **Fades over distance.**
  - `SpitProjectile` has new fields `fadeDistance` (14 m) and `alphaAtFadeDistance` (0.2). Particles emitted further from the mouth start fainter.
  - Every particle also fades over its own lifetime.
  - The core (`Clump`, now using the droplet texture) fades from full to 25% as it flies.

## 2026-10-11 (misty spit)

### Changed
- **Spit is now mostly mist.**
  - `Mist` sprays out of the mouth in a 22° cone (16–22 puffs), keeps billowing along the arc (32 puffs per metre) and slows down so it hangs in the air. Puffs grow up to 2.4× their size and drift with noise. It is denser and darker brown, fading in and out smoothly.
  - `Clump` now uses the mist texture instead of droplets, so the travelling core is a dense brown cloud rather than solid blobs.
  - `Droplets` are fine specks (1.5–5 cm, fewer) inside the mist.
- `SpitSplash`: a bigger, longer-lasting mist puff on impact (12–16 puffs, 1–2 s, expanding) and finer droplets. The ground splat is unchanged.
- `spit_mist.png`: denser cloud texture with more fine detail.

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
