# Update Log (new items)

Zach's side of the project: `Assets/_Game/Dinosaurs`, `World`, `Flow`.
This log lists **new things that were added**. Changes to things that already existed go in `CHANGELOG.md`.
Newest entries at the top.

---

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
