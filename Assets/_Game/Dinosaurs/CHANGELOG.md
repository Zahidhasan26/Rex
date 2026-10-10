# Change Log (changes to existing items)

Zach's side of the project: `Assets/_Game/Dinosaurs`, `World`, `Flow`.
This log lists **changes to things that already existed**: edits, fixes, tuning, renames and removals.
New things go in `UPDATELOG.md`. Newest entries at the top.

---

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
