# UPDATELOG — Halar (Dinosaurs + World & Round)

**New things added** (new scripts, prefabs, scenes, models, folders, settings assets).
Changes to things that already exist go in `CHANGELOG.md` instead.

Branches: `halar/dinos-world` (merged into main), `halar/dino-anim` (current)
Format: newest entry at the top. One entry per new item.

```
## YYYY-MM-DD — short title
- What was added:
- Files:
- Why:
- How to test:
```

---

## 2026-10-10 — ProceduralDinoAnim: death, attack, hit flinch, Maip walk (branch `halar/dino-anim`)
- What was added: `ProceduralDinoAnim.cs`, a script that adds the moves our models are missing by rotating bones in `LateUpdate`, on top of whatever the Animator plays. Added to both dinosaur prefabs with their bones assigned.
  - **Death**: falls onto its side, neck/head/tail droop, knees fold, jaw drops, then sinks into the ground just before it disappears (3 s).
  - **Attack**: mouth opens wide in a scary way, head and neck rear back, small head shake. Opens fast, closes smoothly. Follows `Dinosaur.IsAttacking`.
  - **Hit flinch**: every non-lethal hit snaps the spine, neck and head back, jerks the head to a random side, opens the jaw and whips the tail.
  - **Maip walk**: back legs swing in an alternating stride with knee lift, body rocks, tail and head sway. Stride follows movement speed, fades out when it stops.
  - **Gorgosaurus run speed**: its run clip now plays faster or slower to match how fast it moves (1.5x at its new 4.5 m/s), so the feet don't slide.
  - Every angle and timing is an Inspector slider. Any empty bone slot is skipped.
- Bones used:
  - Maip: head `Head_M_bake_076`, neck `Neck3_M_bake_071`, jaw `jaw01_bon_bake_087`, spine `Spine2_M_bake_064`, tail `tailSkin01–08`, upper legs `Hip_L/R`, lower legs `Knee_L/R`.
  - Gorgosaurus: head `Head_M_13`, neck `Neck_M_15`, jaw `Jaw_M_4`, spine `BackB_M_27`, tail `Tail0–6`, upper legs `backHip_L/R`, lower legs `backKnee_L/R`.
- Files: `Assets/_Game/Dinosaurs/Scripts/ProceduralDinoAnim.cs` (+ .meta), `Prefabs/Maip.prefab`, `Prefabs/Gorgosaurus.prefab`
- Why: the Maip model only has an idle and the Gorgosaurus only a run (ARCHITECTURE.md section 11).
- How to test: open `DinoTest`, press Play. Watch the Maip's legs while it runs at the DummyPlayer and both mouths open when they attack. Left-click a dinosaur (Zach's `DinoHitTester`): body hits 1 and 2 flinch, hit 3 kills; a head hit kills at once. The body falls over and is gone after 3 s.
- Commits: `[dino] procedural death`, `[dino] procedural attack`, `[dino] procedural hit`, `[dino] procedural maip walk`.

## 2026-10-10 — Dinosaur model check (nothing imported yet)
- Checked both chosen models before importing them (ARCHITECTURE.md, Dinosaurs task 1).
- **Maip** (Amazing Dinoworld Maip Macrothorax, Jurassic Radassic, CC BY 4.0): rigged, FBX, 2K textures, only **1 animation (idle)**. No walk, attack or death.
- **Gorgosaurus** (Walking With Dinosaurs 3D Female Gorgosaurus, Jurassic Radassic, CC BY 4.0): rigged, GLB only (needs converting to FBX for Unity), about 15k triangles, only **1 animation (run, 1.5 s)**. No attack or death.
- Decision (team, ARCHITECTURE.md section 11): keep both models and add the missing moves in code with `ProceduralDinoAnim` (see entry above).

## 2026-10-10 — Branch and logs created
- What was added: branch `halar/dinos-world`; this file and `CHANGELOG.md`.
- Files: `Assets/_Game/Flow/Docs/CHANGELOG.md`, `Assets/_Game/Flow/Docs/UPDATELOG.md`
- Why: keep my side of the project (Dinosaurs + World & Round) separate from main and track every change.
