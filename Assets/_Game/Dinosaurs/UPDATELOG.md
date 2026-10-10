# Update Log (new items)

Zach's side of the project: `Assets/_Game/Dinosaurs`, `World`, `Flow`.
This log lists **new things that were added**. Changes to things that already existed go in `CHANGELOG.md`.
Newest entries at the top.

---

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
