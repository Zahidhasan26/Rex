# Update Log (new items)

Zach's side of the project: `Assets/_Game/Dinosaurs`, `World`, `Flow`.
This log lists **new things that were added**. Changes to things that already existed go in `CHANGELOG.md`.
Newest entries at the top.

---

## 2026-10-11 (face tracking and a generated 3D face)

- **Face tracking** (`Boss/Scripts/FaceLandmarker.cs`): finds **468 points** on the face in the picture (eye corners, brows, nose, lips, jaw line, depth) with the MediaPipe Face Mesh network (`Boss/AI/face_landmark.onnx`, Apache 2.0, credits in `Boss/AI/CREDITS.txt`).
  - It runs locally on the CPU through Unity's **Inference Engine** package (`com.unity.ai.inference` 2.6.1, newly added to the project). The picture never leaves the computer.
  - It re-centres on the face over three passes, so the face doesn't need to be in the middle of the photo.
- **Generated 3D face** (`BossFace`): from those points it builds a real **3D face mesh** shaped like the person (nose, brows, cheekbones, lips, chin) and puts it on the front of the boss's head, on the Head bone, so it moves with the head.
  - Each point is placed on the head and pushed out by how far the person's face sticks out compared with the plain head. The depth eases in from the edge so the face blends into the head.
  - It has its own texture, cut out of the photo, which fades to the skin colour at the edges.
  - The mesh uses the face-mesh triangles in `Scripts/FaceMeshTopology.cs`. It is built at runtime and never saved.
- **Automatic framing** (`autoFitPhoto`, on by default): the photo is lined up on the head from the face it finds, so there is no Photo Crop to adjust.
  - Everything outside the face is replaced by the skin and hair colours (background, ears, shoulders). The forehead and hair above the face are kept.
  - The hair colour is taken just above the hairline, found by walking up from the forehead until the colour stops being skin. If it never stops being skin, the head counts as bald.
- New `BossFace` settings: `generate3DFace`, `landmarkModel`, `autoFitPhoto`, `faceDepth`, `faceLift`, `edgeSoftness`, `edgeFadeInside`, `faceWidthOnHead`, `headEyeLine`, `headChin`. A read-only `FaceStatus` says what it did (e.g. "3D face built from 468 points").
- Works with the preset photo and with the live webcam scan, in edit mode (Scene view) and in Play mode. Building takes about 1-1.5 s, once per photo.
- Tested on the drawn test heads and on the photo in `FacePhotos`: face found (score 1.00), 3D face built, and the hair style and colour picked. No errors.

## 2026-10-11 (3D hair presets for the boss)

- **Four 3D hair meshes in `Boss.fbx`**, on the head bone: `Hair_Short`, `Hair_Medium`, `Hair_Long` (with a sheet falling to the shoulders, which follows the upper body), and `Hair_Curly` (big rounded volume with bumps). Plus "None" for bald.
  - Each stops at the hairline, well above the forehead, and its thickness tapers to nothing at the edge, so it blends into the photo and never covers the face.
- **Material `Boss_HairStrands`**: a generated strand texture (`Textures/Hair_Strands.png`), drawn on both sides. `BossFace` tints it with the hair colour sampled from the photo.
- **`BossFace` picks the style** (`hairStyle`, default **Auto**), and the inspector shows what it chose and why:
  - bald if the top of the picture is skin-coloured;
  - Long if there's hair below the ears at the sides;
  - Curly if the hair at the top has a busy texture in every direction (tunable with `curlyBusyness`);
  - Medium if there's hair at the temples;
  - otherwise Short.
  - Without a photo, the mask gets `maskHairStyle` (Short).
  - Set `hairStyle` by hand to override.
- Tested with drawn test heads (no real photos): short → Short, medium → Medium, long → Long, bald → None. The drawn curly head came out Short, because flat cartoon curls don't have photo texture; with a real photo, check the result and set Curly by hand if needed. All styles render and follow the head. No errors.

## 2026-10-11 (face photos folder)

- **`Boss/FacePhotos/`**: drop the presenter's photo here.
  - `Editor/FacePhotoImporter.cs` imports anything in this folder ready for `BossFace` (readable, uncompressed, no mipmaps).
  - A `.gitignore` inside keeps the photos on this computer only, so they are never pushed to GitHub. Only `README.txt` (how to take and frame the photo) is in git.
- Tested in edit mode and Play mode with a drawn cartoon head (no real photo): the face fills the front, the back of the head and the neck match the sampled hair and skin colours, and the boss fight runs normally. No errors.

## 2026-10-11 (boss fight in VR)

- **`Boss/BossTest.unity` now runs in VR.** Zahid's `Player` prefab (the VR rig with the bow) stands at the origin, and the boss starts 18 m in front. The old flat-screen setup (camera and `DummyPlayer`) is kept but switched off under "Desktop Test (off - tick to use without headset)".
- **`Scripts/BossHealthBar.cs`** (added to the Boss prefab by the builder): a world-space bar above the boss's head that always turns toward the headset and slides down as it takes hits. It is built from two quads, so no Canvas is needed in VR. It turns red when the boss enrages and hides when it dies.
- **`Scripts/BossTestRestart.cs`** (test scenes only): press **A on the right controller** (or R on the keyboard) to restart the fight once the boss or the player is dead, without taking the headset off. It also stands in for GameFlow: the fight is "on" at the start, and the boss stops when the player dies.
- Tested with the VR rig (no headset connected, so it ran on the flat screen): the boss targets the headset camera, its attacks lower the real `PlayerHealth`, and the player's death stops the boss. 8 of 8 arrows fired from head height landed in the body hitbox. No errors from our scripts.

## 2026-10-11 (final boss with face scan)

New folder **`Boss/`** for the last act's boss: a tall man in a suit (2.5 m) whose face is the player's own photo.

### Model (`Boss/Models/Boss.fbx`, textures in `Boss/Textures/`)
- Body from "GEORGE CLOON(EY)" by vmmaniac (CC-BY-4.0, credit in `Boss/CREDITS.txt`). **The original head is removed completely** (face, eyes, brows, teeth, tongue). The boss does not look like any real person.
- New generic head built for the scan: skin, a front **face plate** with a straight-on UV projection (materials `Boss_Skin`, `Boss_Face`), and a clean-cut slicked-back **hair cap** (`Boss_Hair`). The head is skinned to the neck and head bones.
- Bones renamed to plain Mixamo names (`Hips`, `Spine1`, `Head`, `RightHand`, ...).
- **Animation clips (12):** Idle (original), plus new ones: Walk, Run, StrafeLeft, StrafeRight, Throw (orb released at 0.62 s), ChargeWindup (chest-out roar), Swing (two-handed overhead smash, lands at 0.40 s), Hit, Death_Back, Death_Forward, Death_Kneel. The deaths end lying on the ground (checked against the mesh).

### Face scan (memory only)
- **`Scripts/FaceScanStation.cs`**: pre-game booth on the PC screen. Consent first: nothing is captured until the person presses Y. The screen then shows a live mirrored camera view with an oval guide and an eye line; Space starts a 3 s countdown and takes the photo; Enter keeps it, R retakes, N skips (the boss wears a mask instead), Backspace forgets the scan at any time. Public methods (`Agree`, `TakePhoto`, `Keep`, `Retake`, `Skip`, `Forget`) are there for VR or UI buttons, and an `onFinished` event lets the flow continue.
- **`Scripts/FaceScanStore.cs`**: holds the photo for this play session only. It is never saved to disk or uploaded, and is destroyed when it is replaced, forgotten, or the game quits.
- **`Scripts/BossFace.cs`**: builds the face texture at runtime. The photo sits in a soft-edged oval that blends into a skin colour sampled from the photo's cheeks and forehead; `Boss_Skin` is tinted the same colour so the edge of the face plate doesn't show. It updates live if the scan changes. With no scan, the boss gets a generated porcelain mask (dark eye holes and a thin mouth).

### Boss behaviour (`Scripts/BossEnemy.cs`, extends `Dinosaur`)
- Uses the shared approach, sidestepping, hit zones and death variants.
- **Throw:** at range it winds up and hurls a glowing orb from its right hand (12 damage). The orb is `Prefabs/BossOrb.prefab`, which reuses `SpitProjectile`; its impact is `BossOrbBurst.prefab`.
- **Charge and smash:** from 5 m or further (40% chance), it plants its feet and roars, then sprints at 7.5 m/s. It steers only 50°/s, so the player can sidestep. It finishes with the overhead smash (25 damage if within 2.6 m). Close up it smashes without the run.
- **Health:** 24 body hits or 6 headshots. Below 50% health it **enrages**: shorter cooldowns and more charges.
- The cooldown starts when an attack ends. Flinches don't queue up during attacks. Backing away plays the walk in reverse.
- Events: `BossEnemy.Defeated` (static), and `ThrowStarted`, `ChargeAnnounced`, `SmashStarted`, `Enraged` for sound and UI.

### Assets and tools
- **`Editor/BossBuilder.cs`**, menu **BowGame > Boss > Build Boss Assets**: sets the FBX import (generic rig, clip names, loops, material remap), makes the materials, the Animator Controller (`Animation/Boss.controller`), both orb prefabs and `Prefabs/Boss.prefab` (NavMeshAgent, trigger hitboxes on the Dinosaur layer for the head, body and both legs, and an orb held in the hand during the wind-up). Safe to re-run.
- **`Boss/BossTest.unity`**: a copy of DinoTest with the boss 18 m in front of the player and a FaceScanStation (tick `showOnStart` to try the scan).

### Tested
- In Play mode: walk, charge roar, run, smash, throw wind-up and release, and death all play and read correctly from the side. Throws and smashes hit the dummy player.
- The face texture was checked with a drawn cartoon face (no real photo) and with the default mask. No errors.

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
