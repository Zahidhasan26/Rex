# Update Log (new items)

Zach's side of the project: `Assets/_Game/Dinosaurs`, `World`, `Flow`.
This log lists **new things that were added**. Changes to things that already existed go in `CHANGELOG.md`.
Newest entries at the top.

---

## 2026-10-11 (per-dinosaur variation)

- **`Scripts/DinoVariation.cs`** (on both dinosaur prefabs): picks small random differences once, when a dinosaur spawns.
  - **Size:** 0.9–1.1× scale. Hitboxes and the NavMeshAgent scale with it; attack reach, footstep stride and voice pitch are adjusted to match (bigger means a longer reach, longer strides and a slightly deeper voice).
  - **Colour:** a 0.82–1.08 brightness multiplier, plus up to 60% blend toward one of five tints (olive, reddish-brown, slate, sandy, dark). Applied with a MaterialPropertyBlock, so the shared materials are untouched.
  - The player's hit flash (Zahid's `HitFlash`) also writes the colour and clears it afterwards, so `DinoVariation` puts the tint back whenever no flash is running.
- Tested: 8 dinosaurs spawned side by side came out at sizes 0.91–1.07 with visibly different tints. No errors.

## 2026-10-11 (death variants and sidestepping)

### Animation clips (`Animation/Clips/`): real Unity clips, keyed on every bone
- `<Dino>_Death_FallLeft`, `<Dino>_Death_FallRight`: staggers with the head thrown up, the legs buckle, rolls about 82° onto that side and slides a little, the head hits the ground and bounces, the tail whips then goes limp, and there is a last leg twitch. The body ends lying on the ground (the drop is measured from the mesh).
- `<Dino>_Death_Collapse`: flinches back, the knees fold, flops onto its belly, the head slams down and bounces, the tail drops, and a final twitch.
- `<Dino>_StrafeLeft`, `<Dino>_StrafeRight` (looping): sideways steps. The legs alternately lift and cross toward the step direction (the Maip's front legs move in diagonal pairs), the body sways and bobs, and the tail leans against the motion. The clips play in place; the NavMeshAgent moves the body.
- Made for both the Maip and the Gorgosaurus (10 clips).

### Tools
- **`Editor/DinoClipBuilder.cs`**: menu **BowGame > Dinosaurs > Build Death and Sidestep Clips**. It rebuilds all the clips and both Animator Controllers. All timing and angle numbers live in this file, so they can be tuned and re-run.

### Animator Controllers (`Maip.controller`, `Gorgosaurus.controller`), rebuilt
- `Locomotion` blend tree on the float `Strafe`: −1 = StrafeLeft, 0 = the model's original clip, +1 = StrafeRight.
- `Death_FallLeft`, `Death_FallRight`, `Death_Collapse`: entered from Any State on the trigger `Die` plus the int `DieIndex` (0, 1, 2), with a 0.15 s blend.

### Behaviour (`Dinosaur.cs`): "Sidestepping" settings
- **Between attacks**, instead of standing still in range, dinosaurs circle the player sideways while facing it (at 85% of attack range). They change direction at random every 1–2.6 s, sometimes stop briefly to watch (20%), and turn around if blocked. When the attack is ready they plant and attack.
- **Far-away pauses** become a sideways shuffle 65% of the time instead of standing.
- Strafe speed is 1.8 m/s (slowed by leg hits too).
- The `Strafe` animator value comes from the actual sideways speed, so the clip matches the movement.
- **On death** a random variant is picked (`deathVariants` = 3) and the Animator runs at normal speed.

### Tested (Play mode, DinoTest)
- Both dinosaurs alternate: sidestep left, sidestep right, short hold, attack (Maip bite / Gorgosaurus spit). The `Strafe` value tracks the movement (±1 while sidestepping, 0 when attacking).
- Six dinosaurs killed at once played FallLeft, FallRight and Collapse as rolled, at normal speed, and ended lying on the ground.
- No errors or warnings.

## 2026-10-11 (Gorgosaurus idle)

- **`Scripts/DinoIdlePose.cs`** (on `Gorgosaurus.prefab`): the Gorgosaurus only has a run clip, so when standing it used to run in place, including while spitting. Now, when it stands still or attacks, it blends (0.25 s) into a planted standing pose. The pose is built from two frames of the run clip: the body and left leg at 50% (left foot down), and the right leg at 0% (right foot down). It also gets slow breathing in the chest (±1.5°, 2.6 s) and a lazy tail sway (±2.5°, 3.6 s).
  - It runs before `ProceduralDinoAnim`, so Halar's open-mouth attack, hit flinch and death play on top of the standing pose.
  - Tested: while spitting, both back feet are on the ground (0.30 / 0.23 m, same as the ground contact in the run), the mouth is open and the spit leaves the jaws.

## 2026-10-11 (spray-style acid spit)

### New
- `Textures/FX/`: `spit_droplet.png` (irregular wet droplet), `spit_mist.png` (cloudy noise), `spit_splat.png` (splash with streaks and specks). These are procedurally generated grayscale-plus-alpha textures, tinted by the particle colour.
- `Materials/FX/`: `Spit_Droplet`, `Spit_Mist`, `Spit_Splat` (URP Particles/Unlit, transparent).
- **`Prefabs/SpitSplash.prefab`**: impact effect.
  - A burst of droplets thrown outward along the surface normal, with gravity.
  - A small mist puff.
  - An acid splat lying flat on the surface that fades out over ~3 s.
  - It removes itself when finished.

## 2026-10-10 (sound)

### Audio files (`Dinosaurs/Audio`): made from Zach's "Good" folder (originals untouched)
- Cleaned up: silence trimmed, short fades, peak matched, mono 48 kHz `.wav` for 3D sounds. Arrow sounds are in `Bow/` (`arrow_whoosh`, `arrow_hit_flesh_01`, `arrow_hit_flesh_02`, `arrow_hit_head`); dinosaur sounds are in `Dinosaurs/Shared/` (`dino_growl`, `dino_footstep_01`, `dino_collapse`).
- **Split:** `EFX SD Monster Voices 11 A` → `dino_roar_A_01` … `_05` (2.4, 5.1, 0.7, 0.6, 0.6 s). `Rage Monster 13 B` is a single roar, kept as `dino_roar_B_01` (3.4 s).
- **Ambient:** the 107 MB, 96 kHz drone → `Ambience/amb_tension_drone_loop.ogg` (~3 MB, 172 s). Silent start and end trimmed, a 3 s crossfade makes it loop seamlessly, and it is raised to −6 dB peak. Imported as Streaming / Vorbis.
- Short clips import as Decompress On Load / ADPCM / Force To Mono.

### Scripts
- **`Dinosaurs/Scripts/DinoAudio.cs`**: dinosaur sound player. It uses a "Voice" source on the head bone and a "Body" source on the root, both 3D. It plays:
  - footsteps by distance walked (stride length)
  - a spawn call (60% chance)
  - a growl on pauses
  - a charge roar
  - an attack snarl
  - hurt sounds on body and leg hits
  - a death roar and the collapse thud
  - arrow impacts at the hit point (a special sound for headshots)
  - Each sound gets a random pitch variation around a base pitch.
- **`World/Scripts/AmbientAudio.cs`**: looping 2D ambience with a fade-in.
- `Dinosaur.cs` now raises local events (`Spawned`, `Paused`, `ChargeStarted`, `AttackStarted`, `Hit`, `Died`, `Collapsed`) for components on the same dinosaur. This adds no new team contract.

### Prefabs
- `Maip.prefab` and `Gorgosaurus.prefab`: `DinoAudio` added and wired.
  - Spawn: roar A_01 or B_01. Charge: B_01 or A_01. Pause: growl. Attack: A_03. Hurt: A_04 or A_05. Death: A_02. Steps: footstep_01. Collapse: collapse. Impacts: flesh_01/02 and head.
  - Maip: pitch 1.15, stride 1.6 m, quieter steps. Gorgosaurus: pitch 0.8, stride 3.2 m, louder steps.
- **`World/Prefabs/AmbientAudio.prefab`**: drone loop at volume 0.35, 3 s fade-in. Added to `DinoTest`.

### Tested (Play mode, DinoTest)
- The ambience fades in and loops. The dinosaurs' voice and body sounds play while they spawn and move, and forced hits produce impact sounds. No errors.

### Not wired (Zahid's bow code)
- `Bow/arrow_whoosh.wav` and `Bow/shoot.ogg` are ready for the bow release and arrow flight.

## 2026-10-10 (dinosaur spawner)

### World (`Assets/_Game/World`)
- **`Scripts/DinoSpawner.cs`**: spawns dinosaurs on a ring around `GameRefs.PlayerHead`.
  - Spawns 28–40 m away, never within 70° of where the player is looking (so they appear behind and to the sides), snapped onto the NavMesh, at least 6 m from other living dinosaurs. They spawn facing the player.
  - The interval ramps from 6 s down to 1.5 s over `rampDuration` (30 s, matching fast mode), with ±25% randomness. The first spawn comes after 1.5 s.
  - `maxAlive` = 6.
  - Weighted random type, with an optional unlock time per type.
  - `mode`:
    - `OnlyDuringSurvive` (default) starts when `GameEvents.StateChanged` reports `Survive` and stops on any other state. On `ActOneComplete` it clears all dinosaurs (marked FINAL ACT HOOK).
    - `Always` is for test scenes without GameFlow.
  - Public methods for GameFlow: `Begin()`, `Stop()`, `SpawnOne()`, `ClearAll()`, and the `AliveCount` property.
  - Draws the spawn ring as an orange gizmo when selected.
- **`Prefabs/DinoSpawner.prefab`**: Maip (weight 3, from the start) and Gorgosaurus (weight 1, unlocks after 8 s).

### Tested (Play mode, DinoTest, spawner added temporarily, mode Always)
- 6 Maips spawned 28–40 m out, all behind or beside the player, and surrounded it. The cap of 6 held.
- Forced 40 spawns: 12 Gorgosaurus and 28 Maip, 0 failed placements. No errors.

## 2026-10-10 (audio folder)

- `Dinosaurs/Audio/`: a staging folder for all sound effects, with the subfolders `Bow`, `Dinosaurs/Maip`, `Dinosaurs/Gorgosaurus`, `Player`, `UI`, `Ambience` and `Music`.
- `Dinosaurs/Audio/SOUNDS.md`: a checklist of every sound needed, with file names, the format (`.wav`, mono for world sounds) and what matters most for the demo.

## 2026-10-10 (DinoTest setup)

### Scene `Assets/Scenes/DinoTest` (lock held by Zach)
- `NavMesh` object with a `NavMeshSurface` (Ground layer, physics colliders) and a baked NavMesh (`Assets/Scenes/DinoTest/NavMesh-NavMesh.asset`).
- `Maip` instance at (-6, 0, 20) and `Gorgosaurus` instance at (-4.9, 0, 5).
- `DinoHitTester` on the Main Camera: in Play mode, left-click a dinosaur in the Game view to hit it.

### Tested in Play mode
- The Maip chases the `DummyPlayer`, stops in range and bites every ~1.6 s (10 damage each).
- The Gorgosaurus holds its position and spits every ~3 s (15 damage each).
- 3 body hits kill the Maip, a head hit kills the Gorgosaurus, and dead dinosaurs tip over and are removed after 4 s.
- No errors in the Console.

## 2026-10-10 (prefabs)

### Prefabs (`Dinosaurs/Prefabs`)
- **`Maip.prefab`**: built from `Maip_LOD.fbx` scaled 7× (~6.5 m long, ~2.3 m tall), on the `Dinosaur` layer.
  - Components: `NavMeshAgent` (radius 0.78), `Maip` script (attack range 3.8 m, 3 body hits, speed 6, 10 damage), and an Animator on the `Model` child.
  - Hitboxes are trigger box colliders with a `HitZoneMarker`, each parented to a bone so it follows the animation:
    - `Hitbox_Head`: head bone
    - `Hitbox_Body`: root bone
    - `Hitbox_Leg_L` / `Hitbox_Leg_R`: hip bones
    - `Hitbox_Leg_FrontL` / `Hitbox_Leg_FrontR`: shoulder bones
- **`Gorgosaurus.prefab`**: built from `Gorgosaurus.fbx` at real size (~9 m long, ~3 m tall), on the `Dinosaur` layer.
  - Components: `NavMeshAgent` (radius 1.08), `Gorgosaurus` script (range 14 m, 5 body hits, speed 3, 15 damage), with the spit fired from the head bone.
  - Hitboxes: head, body, and the left and right back legs (bone-parented triggers).
- **`Spit.prefab`**: a 0.35 m green glowing sphere with a short trail and `SpitProjectile`, on the `EnemyProjectile` layer, with no collider.

### Animation (`Dinosaurs/Animation`)
- `Maip.controller` and `Gorgosaurus.controller`: one looping state each. They have no parameters on purpose, so the script uses its fallbacks: the dinosaur tips over on death and the attacks are timed.

### Materials (`Dinosaurs/Materials`)
- `Spit.mat`: URP Unlit, green.

## 2026-10-10

### Scripts (`Dinosaurs/Scripts`)
- **`Dinosaur.cs`**: shared base class for every dinosaur. Implements `IDamageable`.
  - Chases `GameRefs.PlayerHead` with a `NavMeshAgent` and recalculates its path every 0.25 s.
  - Attacks when the player is in range, then waits out a cooldown.
  - Damage rules: a head hit kills instantly, the body takes `bodyHitsToKill` hits, and a leg hit slows it (multiplier and duration are set in the Inspector).
  - On death: stops moving, turns off its colliders, raises `GameEvents.RaiseDinoDied`, and removes the body after `corpseTime`.
  - The Animator is optional. It sets `Speed`, `Attack`, `Hit` and `Die` only if those parameters exist, and tips the body over if there is no death animation.
  - Goes idle when `GameRefs.State` is `Dead` or `ActOneComplete`.
- **`Maip.cs`**: fast melee attacker. The bite lands after `biteDelay` and misses if the player has moved out of reach. Defaults: speed 6, range 2.2 m, 3 body hits, 10 damage.
- **`Gorgosaurus.cs`**: slow, tough, ranged attacker. It only spits when nothing blocks its view of the player and walks closer if the view is blocked. Defaults: speed 3, range 14 m, 5 body hits, 15 damage.
- **`SpitProjectile.cs`**: the Gorgosaurus spit. It flies in an arc toward the player and calls `GameEvents.RaisePlayerHit` if it passes within `hitRadius` of the head. It disappears when it hits the ground or after `lifeTime`.
- **`DinoHitTester.cs`**: test-scene helper only. Left-click a dinosaur hitbox in the Game view to hit that zone, the same way an arrow would. Uses the Input System.

### Models (`Dinosaurs/Models`)
- **`Gorgosaurus/Gorgosaurus.fbx`**: converted from the Sketchfab glTF in Blender (a stray icosphere was removed).
  - Generic rig, ~15k triangles, ~9 m long.
  - One 1.5 s **run** clip (imports as "Scene").
  - `Textures/`: base colour plus a normal map, extracted from the FBX.
- **`Maip/Maip_LOD.fbx`**: converted from the Sketchfab glTF and reduced from ~151k to ~29k triangles for VR.
  - Generic rig, ~0.9 m long (needs scaling up about 10×).
  - One 10 s showcase loop (imports as "Scene").
  - `Textures/`: base colour, extracted from the FBX.
- Credits: both models are by Jurassic Radassic (Sketchfab), CC-BY-4.0. The credit lines are in each zip's `license.txt` and still need adding to `CREDITS.md`.

### Docs
- `UPDATELOG.md` (this file) and `CHANGELOG.md`.

### Not committed on purpose
- `Models/*.zip` (the original downloads), `Maip/Maip.fbx` (the full 151k-triangle version) and `Maip/Maip_*_2K_v02.png`. These are only kept locally as source files.
