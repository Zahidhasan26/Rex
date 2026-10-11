# EMG Bow Game: Architecture and Team Rules (Act 1)

Knight Hacks IX, Game Development track. Team: Zahid, Halar, Zach, Saber.

**Put this file in the root of the repo as `ARCHITECTURE.md`.** Every teammate gives it to their AI tool at the start of every session, with this instruction:

> Read ARCHITECTURE.md fully before doing anything. I am **[name]**. Only work on what my section owns, and follow sections 3, 6, 7 and 8 exactly.

---

## 1. Scope

We finish **Act 1** completely before anything else is started.

| In scope now (Act 1) | Not yet (final act) |
|---|---|
| Calibrate, countdown, survive against dinosaurs, win or die | Meteor cutscene |
| Bow with EMG draw, player movement, player health | Boss and uploaded face |
| Two dinosaur types: spawn, move, attack, die | Crowd engagement |
| Vest hit feedback | |
| HUD, sounds, regular and fast modes | |

**Act 1 is done when:** one person can put on the headset, calibrate, survive the timer in fast mode with dinosaurs attacking and dying correctly, feel hits on the vest, and see a win or death screen, with no red errors in the Console.

## 2. How the game runs

- **PC VR, tethered.** The game runs on Zahid's laptop (Windows) and is shown in a Meta Quest Pro over a Link cable. We do **not** build an Android app.
- **Render pipeline: URP.** Never add HDRP assets or HDRP shaders.
- **VR stack:** OpenXR plus the XR Interaction Toolkit, as set up by Unity's VR template.
- **Network:** the laptop, the EMG armband and the vest are all on one phone hotspot (2.4 GHz).
- **Performance target:** steady 72 frames per second in the headset. If it drops, lower terrain grass and tree density first.

```
EMG armband (ESP32) --UDP 5005--> [ Unity game on laptop ] --UDP 5006--> Haptic vest (ESP32)
                                          |
                                     Link cable
                                          |
                                    Meta Quest Pro
```

## 3. Versions (locked)

**These two files are the source of truth:**

- `ProjectSettings/ProjectVersion.txt` (Unity version)
- `Packages/manifest.json` (package versions)

| Item | Version |
|---|---|
| Unity Editor | **6000.4.10f1** (Unity 6.4) |
| Universal RP (`com.unity.render-pipelines.universal`) | **17.4.0** |
| XR Interaction Toolkit (`com.unity.xr.interaction.toolkit`) | **3.4.1** |
| OpenXR Plugin (`com.unity.xr.openxr`) | **1.16.1** |
| XR Plugin Management (`com.unity.xr.management`) | **4.5.4** |
| Unity OpenXR: Meta (`com.unity.xr.meta-openxr`) | 2.5.0 |
| XR Hands (`com.unity.xr.hands`) | 1.7.3 |
| XR Core Utilities (`com.unity.xr.core-utils`) | 2.6.0 |
| Input System (`com.unity.inputsystem`) | **1.19.0** |
| AI Navigation (`com.unity.ai.navigation`) | **2.0.12** |
| MCP for Unity (`com.coplaydev.unity-mcp`) | Git package, `main` branch; needs Git installed on each laptop |
| Terrain pack | Unity URP Terrain, Terrain Sample Project **1.0.3**, in the repo at `Assets/TerrainDemoScene_URP` (Git LFS) |
| Git LFS | Required. Install Git LFS and run `git lfs install` once **before cloning**, or the terrain files arrive as tiny placeholder files |

Read from the project (VR template) on Oct 10. If this table and the two files ever disagree, the files win.

**Rules for humans and AI:**

1. Install the exact Unity version above through Unity Hub. Never open the project in another version, and never accept an "upgrade project" prompt.
2. **Only Zahid** may change `Packages/manifest.json`, `Packages/packages-lock.json`, or anything in `ProjectSettings/`. If you need a package, a layer, a tag or a setting, ask Zahid.
3. AI: before writing code, read the two source-of-truth files and write code for **those** versions. Do not guess from memory.
4. Code style for this stack:
   - Use the **Input System** package and XR Interaction Toolkit actions. Never use the old `Input.GetKey` / `Input.GetAxis`.
   - Use the XR Interaction Toolkit API that matches the installed version (in 3.x, classes live in sub-namespaces such as `UnityEngine.XR.Interaction.Toolkit.Interactables`, and the rig is called **XR Origin**).
   - Use `FindFirstObjectByType<T>()` or `FindAnyObjectByType<T>()` (not `FindObjectOfType`), `FindObjectsByType<T>()` with **no** `FindObjectsSortMode` argument (it is deprecated in this Unity version), and `Rigidbody.linearVelocity` (not `velocity`).
   - Use `NavMeshAgent` and `NavMeshSurface` from the AI Navigation package for dinosaur movement.
   - All game scripts go in `namespace BowGame`.

## 4. Who owns what

| Person | Owns | Folder they may edit | Test scene |
|---|---|---|---|
| **Zahid** | Project setup, shared Core code, player rig, bow, arrows, movement, player health, EMG and vest scripts in Unity, HUD | `Assets/_Game/Core`, `Assets/_Game/Player`, `Assets/_Game/UI` | `PlayerTest` |
| **Halar and Zach** (shared) | The dinosaurs: importing both models, animation, hit zones, health, movement, attacks, death. The world and the round: `Main` scene after handover, terrain arena, NavMesh, spawner, difficulty ramp, game flow and timers, modes | `Assets/_Game/Dinosaurs`, `Assets/_Game/World`, `Assets/_Game/Flow` | `DinoTest`, `Main` (owners) |
| **Saber** | EMG armband and haptic vest hardware and firmware. After hardware is done: sounds, credits, demo video, submission | `Hardware/` (repo root, outside Unity), later `Assets/_Game/Audio` | none |

**The one rule that prevents lost work:** you only edit files inside your own folders. If you need a change in someone else's folder, ask that person.

**`Main` scene:** Zahid creates it, adds the terrain and VR rig, then hands it to **Halar and Zach**. From that moment only they save `Main`. Zahid delivers the `Player` prefab, and they place it.

**Halar and Zach share folders, so they follow the lock rule:**

- A scene or prefab can only be edited by **one of them at a time**. Before opening `Main`, `DinoTest` or a shared prefab to change it, post in the team chat: "I have Main" (or the prefab's name). When done: commit, push, then post "Main is free".
- The other person pulls before taking the lock.
- Scripts are safer than scenes. Each of them writes their **own script files** and does not edit a script the other is working on. At the start of each work block, agree in chat who is doing which task from the list below.
- To work at the same time, one takes the lock on `Main` and the other works in `DinoTest` or in a personal scene (`HalarTest`, `ZachTest`).

### Zahid's tasks

1. Create the project, fill in section 3, push the first commit.
2. Do the pre-handover checklist in section 9.
3. `Player` prefab: XR Origin, left-thumbstick movement, snap turn.
4. Bow on the left controller. Draw strength comes from `EmgInput.Value`. Arrow fires when the value falls after being drawn. Arrow speed scales with draw strength.
5. Arrow: on hit, find `HitZoneMarker` and call `IDamageable.TakeHit` (section 6).
6. Player health: listens to `GameEvents.PlayerHit`, reduces health, works out which side the hit came from, calls `Haptics.Hit`, raises `GameEvents.PlayerDied` at zero.
7. `EmgInput` (UDP listener on port 5005, right trigger as fallback) and `Haptics` (UDP sender to port 5006, Console log as fallback).
8. HUD: health, draw meter, time left, calibrate prompt, win and death screens.
9. Sets `GameRefs.PlayerHead` when the rig starts.

### Halar and Zach's tasks

They split these between themselves and say in the team chat who has which. A sensible starting split is one person on **Dinosaurs** and the other on **World and round**, swapping or pairing as needed.

**Dinosaurs**

1. Import both models into `Assets/_Game/Dinosaurs/Models`. **First check each one has animations** (walk or run, attack, death). If a model is rigged but has no animations, stop and tell the team before building on it.
2. Convert materials to URP if they show pink. Fix scale so the dinosaurs are the right size next to the player.
3. One prefab per dinosaur, each with: `NavMeshAgent`, Animator, a script implementing `IDamageable`, and three hitbox colliders (head, body, legs), each with a `HitZoneMarker`, on the `Dinosaur` layer.
4. Behaviour: move toward `GameRefs.PlayerHead`, attack in range, play death animation, then remove the body after a few seconds.
5. Damage rules: head = instant kill, body = 3 hits, legs = slowed.
6. Suggested roles (team can change): **Maip** is the fast melee attacker; **Gorgosaurus** is slower, tougher, and spits a visible projectile from range.
7. Attacks call `GameEvents.RaisePlayerHit(damage, attackerPosition)`. Never touch the player's scripts directly.
8. On death, call `GameEvents.RaiseDinoDied(gameObject)`.
9. Test in `DinoTest` using a `DummyPlayer` object that sets `GameRefs.PlayerHead`, so no headset is needed.

**World and round**

10. Take over `Main`. Choose a play area on the terrain and keep the player inside it (invisible walls or a clearing).
11. Bake the NavMesh on the terrain with `NavMeshSurface`. Re-bake whenever the terrain changes.
12. Spawner: spawn points in a ring around the play area, out of the player's direct view. Spawn rate rises over time. Cap the number alive at once (start at 6) to protect the frame rate. Built: `Assets/_Game/World/Scripts/DinoSpawner.cs` and `Assets/_Game/World/Prefabs/DinoSpawner.prefab` (set Spawn Mode to Always in test scenes).
13. `GameFlow`: the state machine in section 5. Sets `GameRefs.State` and `GameRefs.StateTimeLeft`, raises `GameEvents.StateChanged`.
14. `GameConfig` asset holding every number for regular and fast mode, so tuning never needs a code change.
15. When the survive timer ends: stop spawning, clear the remaining dinosaurs, go to `ActOneComplete`. Leave a clearly marked hook here for the final act.
16. Lighting, sky, and terrain performance (grass and tree density).

### Saber's tasks

1. Build the EMG armband and the haptic vest from the two hardware briefs.
2. Keep firmware in `Hardware/emg_armband` and `Hardware/haptic_vest` in the repo.
3. Follow the network contract in section 6 exactly.
4. Test each device against a laptop on the hotspot before handing to Zahid.
5. Afterward: sound effects (bow, roars, hits, win, death), `CREDITS.md`, demo video, submission page.

## 5. Act 1 game flow

| State | Regular | Fast | What happens |
|---|---|---|---|
| `Calibrate` | 5 s | 5 s | HUD tells the player to relax, then flex. Operator presses the armband's calibrate button. |
| `Countdown` | 3 s | 3 s | Player is on the land. Bow is active, no dinosaurs yet. |
| `Survive` | 180 s | 30 s | Dinosaurs spawn, increasing over time. |
| `ActOneComplete` | | | Timer reached zero. Dinosaurs cleared. Win screen (later: cutscene). |
| `Dead` | | | Player health reached zero. Death screen, option to restart. |

Fast mode is the demo mode. Build and test it first.

## 6. Contracts (shared code and signals)

These names are fixed. Everyone's code talks to other people's code **only** through them. Zahid commits these files in `Assets/_Game/Core` before anyone else starts. Changing them needs the whole team to agree.

```csharp
// Assets/_Game/Core/GameContracts.cs
using System;
using UnityEngine;

namespace BowGame
{
    public enum GameState { Calibrate, Countdown, Survive, ActOneComplete, Dead }
    public enum HitZone   { Head, Body, Legs }
    public enum HitSide   { Left, Right, Front }

    // Implemented by the dinosaur script (Halar and Zach). Called by Zahid's arrow.
    public interface IDamageable
    {
        void TakeHit(HitZone zone, float drawStrength01, Vector3 hitPoint);
    }

    public static class GameEvents
    {
        public static event Action<GameState> StateChanged;      // raised by GameFlow (Halar and Zach)
        public static event Action<float, Vector3> PlayerHit;    // damage, world position of attacker (raised by dinosaurs)
        public static event Action PlayerDied;                   // raised by player health (Zahid)
        public static event Action<GameObject> DinoDied;         // raised by dinosaurs (Halar and Zach)

        public static void RaiseStateChanged(GameState s)          => StateChanged?.Invoke(s);
        public static void RaisePlayerHit(float dmg, Vector3 from) => PlayerHit?.Invoke(dmg, from);
        public static void RaisePlayerDied()                       => PlayerDied?.Invoke();
        public static void RaiseDinoDied(GameObject dino)          => DinoDied?.Invoke(dino);
    }

    public static class GameRefs
    {
        public static Transform PlayerHead;     // set by the player rig, or by DummyPlayer in test scenes
        public static GameState State;          // set by GameFlow
        public static float StateTimeLeft;      // set by GameFlow every frame
    }
}
```

```csharp
// Assets/_Game/Core/HitZoneMarker.cs  -- put one on every dinosaur hitbox collider
using UnityEngine;

namespace BowGame
{
    public class HitZoneMarker : MonoBehaviour
    {
        public HitZone zone;
    }
}
```

Arrow hit logic (Zahid): `var marker = collider.GetComponent<HitZoneMarker>();` then `collider.GetComponentInParent<IDamageable>()?.TakeHit(marker.zone, draw, point);`

Also in Core, written by Zahid:

- `EmgInput.Value` : static float, 0 to 1. Everyone reads flex strength from here only.
- `Haptics.Hit(HitSide side, float strength01)` : the only way to buzz the vest.

**Event rule:** subscribe in `OnEnable`, unsubscribe in `OnDisable`. Forgetting to unsubscribe causes errors after a scene reload.

**Layers and tags** (created once by Zahid, nobody else adds any):

| Layer | Used for |
|---|---|
| `Player` | Player body collider |
| `Dinosaur` | Dinosaur hitboxes |
| `Arrow` | Arrows |
| `EnemyProjectile` | Spit projectiles |
| `Ground` | Terrain |

**Network contract** (Saber and Zahid):

| Link | Direction | Format |
|---|---|---|
| EMG | Armband to laptop, UDP port **5005**, broadcast | Text number `0.00` to `1.00`, about 50 per second |
| Vest | Laptop to vest, UDP port **5006**, broadcast | Text `L,200`, `R,200` or `F,200` (side, strength 0 to 255) |

Saber: check this against the vest brief you were sent. If the brief says something different, tell Zahid and we pick one.

## 7. Folder layout

```
Assets/
  _Game/
    Core/        shared contracts            (Zahid, changes need team agreement)
    Player/      rig, bow, arrows, health    (Zahid)
    UI/          HUD and screens             (Zahid)
    Dinosaurs/   models, prefabs, AI         (Halar and Zach)
    World/       terrain edits, spawner      (Halar and Zach)
    Flow/        GameFlow, GameConfig        (Halar and Zach)
    Audio/       sounds                      (Saber)
  Scenes/        Main, Main_Daylight (backup of the midday look), PlayerTest, DinoTest, CombatTest, DinoShowcase (test scenes)
  TerrainDemoScene_URP/   terrain pack, stored in Git LFS; avoid editing its files, copy what you need into your own folder
Hardware/        ESP32 firmware              (Saber)
ARCHITECTURE.md
CREDITS.md
```

Inside each folder use `Scripts/`, `Prefabs/`, `Models/`, `Materials/` as needed.

**Audio:** sounds are currently staged in `Assets/_Game/Dinosaurs/Audio` (checklist in `SOUNDS.md`; `Bow/shoot.ogg` is already there). `Assets/_Game/Audio` is still empty. Before Saber starts the audio pass, agree which folder holds the final sounds. Move files only inside the Unity Editor so the `.meta` links survive.

## 8. Rules for AI tools

Give these to your AI as hard rules.

**Files**

1. Only create or edit files inside the folders owned by the person you are working for (section 4).
2. Never edit `Packages/`, `ProjectSettings/`, `Assets/_Game/Core/`, the terrain pack, or another person's folder. If the task seems to need that, stop and say what change is needed and who owns it.
3. Never install, remove or update a package. Never change the render pipeline or XR settings.
4. Never edit `.unity`, `.prefab`, `.asset` or `.meta` files as text. Change scenes and prefabs only through the Unity Editor (or the MCP bridge).
5. Never move, rename or delete files outside the Unity Editor. Doing it in the file system breaks the `.meta` links.
6. Only a scene's owners save it. Deliver work to other owners as prefabs. If the human shares folders with a teammate (Halar and Zach), ask the human to confirm they hold the lock (section 4) before changing any scene or shared prefab, and never edit a script the teammate is working on.
7. Do not modify the VR template's XR Origin internals. Add new components and child objects instead.

**Code**

8. Use the exact names in section 6. Do not invent alternatives such as a second event system or a new health interface.
9. Never reference another person's concrete scripts. Talk through `GameEvents`, `GameRefs`, `IDamageable`, `HitZoneMarker`, `EmgInput`, `Haptics`.
10. Every feature must work in its test scene with stand-ins (trigger for EMG, Console log for vest, `DummyPlayer` for the player).
11. Put tunable numbers in `GameConfig` or serialized fields, not hard-coded.
12. Give full Inspector setup steps with every script: which object it goes on and which fields to assign.
13. One feature at a time. After each, state how to test it.

**Git**

14. Each person works on their own branch named `name/topic` (for example `zach/dinosaurs`, `halar/dino-anim`). Nobody pushes to `main` directly; Zahid merges finished branches into `main`. Before starting a new piece of work, merge the latest `main` into your branch so you have everyone else's changes.
14a. Before every commit, discard files Unity changed by itself outside your folders. The usual ones are terrain materials under `Assets/TerrainDemoScene_URP`, `Assets/Scenes/BasicScene/Grid.mat`, `ProjectSettings/VersionControlSettings.asset` and `Assets/XR/Settings/OpenXRPackageSettings.asset`. Committing them can strip textures from the terrain plants or switch the project's version-control mode.
15. Commit small and often, one feature per commit. Message format: `[player] bow fires arrow`, `[dino] maip walk and attack`, `[world] spawner ring`, `[hw] emg calibration`.
16. Before committing, list the changed files. If any file is outside the owner's folders, do not commit it; undo that change.
17. Always commit `.meta` files together with their assets.
18. Never commit `Library/`, `Temp/`, `Logs/`, `Obj/`, `UserSettings/` or build output. The Unity `.gitignore` must stay in place.
19. Never force push, never rewrite history, never run `git reset --hard` or `git clean` without the human confirming.
20. On a merge conflict in a `.unity`, `.prefab` or `.asset` file: stop. Do not try to merge it. Keep the file owner's version and tell the human.
21. Only push when the project opens with no red Console errors.

## 9. Zahid's pre-handover checklist

Do all of this in the first commits, before Halar and Zach start, because these are shared files only Zahid may change.

- [x] Project created from the VR template; OpenXR enabled for Windows; sample scene runs in the headset over Link.
- [x] Unity `.gitignore` in place.
- [x] Git LFS turned on for large files (`.fbx`, `.png`, `.tga`, `.exr`, `.wav`, terrain data).
- [x] URP terrain imported. `Assets/Scenes/Main.unity` created from the demo scene with the VR rig (`Player (XR Origin)`) standing on the terrain; demo camera and flythrough timeline switched off.
- [x] `Main` checked in the headset; restyled to a late-afternoon look, grass thinned and shortened, PC quality level ("Ultra") selected.
- [x] AI Navigation package and MCP for Unity added.
- [x] Layers from section 6 created.
- [x] `Assets/_Game` folders created; Core contract files committed.
- [x] `Main`, `PlayerTest`, `DinoTest` scenes created in `Assets/Scenes`.
- [x] Section 3 version table filled in.
- [x] Teammates added as collaborators on GitHub.
- [x] `ARCHITECTURE.md` committed. Message the team: "Main now belongs to Halar and Zach."

## 9b. First-time setup for Halar, Zach and Saber

1. Install **Git** and **Git LFS**, then run `git lfs install` once.
2. Clone `https://github.com/Zahidhasan26/Rex`. The terrain is about 4 GB, so expect a long first download.
3. In Unity Hub, click Add, pick the cloned folder, and install Unity **6000.4.10f1** when offered.
4. Open the project. The first open takes several minutes.
5. Open `Assets/Scenes/DinoTest` to start. Open `Main` only when you hold the lock (section 4).
6. Check the terrain shows trees and textures in `Main`. If it is blank or pink, Git LFS was not set up before cloning: run `git lfs pull` in the project folder.

## 10. Order of work

| Step | What | Who |
|---|---|---|
| 0 | Pre-handover checklist | Zahid |
| 1 | Each lane works alone with stand-ins: bow fires in `PlayerTest`; one dinosaur walks, attacks and dies in `DinoTest`; spawner and timer run in `Main` with placeholder cubes | Zahid, Halar, Zach |
| 2 | First merge: `Player` prefab and one dinosaur prefab placed in `Main`; arrows kill dinosaurs; dinosaurs hurt the player | Halar or Zach places (whoever holds the lock), all test |
| 3 | Second dinosaur, ranged attack, difficulty ramp, HUD | Halar, Zach, Zahid |
| 4 | Real EMG and vest connected | Saber, Zahid |
| 5 | Fast mode played start to finish by someone outside the team; fix what confuses them | All |
| 6 | Act 1 frozen. Only then start the final act | All |

## 11. Things to watch

- **Dinosaur animations.** The Maip model only had an idle and the Gorgosaurus only a run, so attack, hit, death and the Maip's walk are done in code by rotating bones from a `ProceduralDinoAnim` script (Halar). It is merged into `main`. Re-check the animations after any change to a dinosaur model or prefab.
- **Model origin and credit.** The Gorgosaurus is listed as CC BY 4.0, which requires credit, and its page says it comes from a commercial *Walking with Dinosaurs* game. Put both models' authors and links in `CREDITS.md`, and check the event rules on third-party assets.
- **Windows firewall.** The first time the game listens on the network, Windows asks for permission: allow Unity on private networks, and set the hotspot as a Private network. Otherwise EMG data silently never arrives.
- **EMG arm and controller.** The right arm wears the sensor, so keep the right hand's buttons unused. Gripping a controller tenses the forearm and can fire arrows by accident.
- **Comfort.** Smooth thumbstick movement makes some first-time players queasy. Keep speed low and offer snap turn.
- **Laptop on mains power** during demos; laptops slow their graphics on battery.
- **AI disclosure.** Be ready to tell judges which tools helped and to explain your own code.
- **Recalibrate** the armband for every new player.
