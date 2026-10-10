# Change Log (changes to existing items)

Zach's side of the project: `Assets/_Game/Dinosaurs`, `World`, `Flow`.
This log lists **changes to things that already existed**: edits, fixes, tuning, renames and removals.
New things go in `UPDATELOG.md`. Newest entries at the top.

---

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
