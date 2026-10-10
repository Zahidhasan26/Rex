# UPDATELOG — Halar (Dinosaurs + World & Round)

**New things added** (new scripts, prefabs, scenes, models, folders, settings assets).
Changes to things that already exist go in `CHANGELOG.md` instead.

Branch: `halar/dinos-world`
Format: newest entry at the top. One entry per new item.

```
## YYYY-MM-DD — short title
- What was added:
- Files:
- Why:
- How to test:
```

---

## 2026-10-10 — Dinosaur model check (nothing imported yet)
- Checked both chosen models before importing them (ARCHITECTURE.md, Dinosaurs task 1).
- **Maip** (Amazing Dinoworld Maip Macrothorax, Jurassic Radassic, CC BY 4.0): rigged, FBX, 2K textures, only **1 animation (idle)**. No walk, attack or death.
- **Gorgosaurus** (Walking With Dinosaurs 3D Female Gorgosaurus, Jurassic Radassic, CC BY 4.0): rigged, GLB only (needs converting to FBX for Unity), about 15k triangles, only **1 animation (run, 1.5 s)**. No attack or death.
- Also: model names suggest they come from commercial games, so the licence may not be valid. Check hackathon rules.
- Decision needed from the team: switch to Quaternius Animated Dinosaurs (CC0, full idle/walk/run/attack/death) or keep and fake missing animations.

## 2026-10-10 — Branch and logs created
- What was added: branch `halar/dinos-world`; this file and `CHANGELOG.md`.
- Files: `Assets/_Game/Flow/Docs/CHANGELOG.md`, `Assets/_Game/Flow/Docs/UPDATELOG.md`
- Why: keep my side of the project (Dinosaurs + World & Round) separate from main and track every change.
