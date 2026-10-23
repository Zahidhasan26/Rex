# Rex

A VR dinosaur survival game you play with your forearm. A MyoWare EMG sensor reads how hard you squeeze your fist, and that squeeze draws the bow. A three-motor haptic vest buzzes on the side a dinosaur bites you from.

Built in one weekend at **Knight Hacks IX** (UCF, October 2026) for the Game Dev track.

## Demo

<!-- Replace VIDEO_ID (twice) with the YouTube video id, e.g. https://youtu.be/dQw4w9WgXcQ -> dQw4w9WgXcQ -->
## Demo

[![Rex gameplay demo](https://img.youtube.com/vi/qgDpLJCkhW0/hqdefault.jpg)](https://youtu.be/qgDpLJCkhW0)

▶ [Watch the gameplay demo on YouTube](https://youtu.be/qgDpLJCkhW0)

Devpost: [link]

---

## How it plays

1. **Tutorial.** You spawn in a clearing, facing north. Floating text explains the bow, then calibrates the armband (relax, then squeeze as hard as you can) and puts up three practice targets.
2. **The alpha.** A large Maip opens the fight. It circles, waits until you look at it, bites, and backs off to charge again. Arrows only stagger it until it has bitten you twice; after that, one arrow kills it.
3. **The pack.** Smaller Maips and a Gorgosaurus come from the front arc for the rest of the 60-second round. Maips sidestep, bite and retreat. The Gorgosaurus keeps its distance and spits, so you dodge with the left stick.
4. **Cutscene.** Survive and the world fades out. A meteor cutscene plays on a large screen anchored in front of you.
5. **Final boss** (separate scene). A 2.5 m man in a suit wearing the player's face, built on-device from a photo or a consented webcam scan.

Health is 250. Every bite buzzes the vest on the side it came from: left, right, or all three motors for a bite from the front.

## Controls

| Input | Action |
| --- | --- |
| Left controller | Holds the bow |
| Right trigger **or** fist squeeze (EMG) | Grab and draw the string; release to shoot. The stronger of the two is used. |
| Left stick | Walk (dodge spit and charges) |
| A (right controller) | Restart after a round |
| Keyboard `T` / `R` / `C` | Skip tutorial / restart / play the cutscene now (testing) |

---

## Hardware

One ESP32 on the player's body runs both the armband and the vest.

```
 MyoWare 2.0 ──ENV──► ESP32 (T-Display) ──UDP 5005, 50 Hz──► Unity (laptop) ──Link cable──► Quest Pro
 (forearm)            USB power bank    ◄──UDP 5006, hits───
                           │
                           └──relay board──► left / right / back vibration motors (own battery)
```

| Part | Notes |
| --- | --- |
| LilyGO T-Display (ESP32) | Any classic ESP32 dev board works. |
| MyoWare 2.0 muscle sensor | Forearm flexors, two snaps along the muscle, reference electrode on the elbow bone. The biceps gave a much weaker signal. |
| 3-channel relay module | Most cheap boards are active-LOW; see `RELAY_ACTIVE_LOW`. |
| 3 vibration motors + battery | Motors run from their own battery, never from the ESP32. |
| USB power bank | Powers the ESP32. |

### Wiring

| From | To |
| --- | --- |
| MyoWare VIN | ESP32 3V (never 5V) |
| MyoWare GND | ESP32 GND |
| MyoWare ENV | ESP32 GPIO 32 |
| Relay IN1 / IN2 / IN3 | GPIO 25 (left) / 26 (right) / 27 (back) |
| Relay VCC / GND | ESP32 5V / GND |

Keep the three sensor wires short and away from the motor and relay wiring. Leave RECT and RAW unconnected.

### Firmware

`Hardware/rex_esp32/rex_esp32.ino`. It needs Arduino IDE with the ESP32 core and no extra libraries.

1. Set `WIFI_NAME` and `WIFI_PASSWORD` at the top. It has to be a 2.4 GHz network shared with the laptop; on an iPhone hotspot, enable *Maximize Compatibility*.
2. Board: **ESP32 Dev Module**. Upload.
3. Serial Monitor at 115200. Type `s` for a status report covering Wi-Fi, the IP address, whether the game has been found, and the live EMG level.

On connect, all three motors give a short buzz. If the motors run at power-up and stop when hit, set `RELAY_ACTIVE_LOW = false`.

`Hardware/emg_test/emg_test.ino` is a bench test with no network involved. It plots the raw sensor signal in the Serial Plotter.

### Protocol

Plain-text UDP, both directions.

| Direction | Port | Payload |
| --- | --- | --- |
| ESP32 → game | 5005 | Raw muscle level `0.000`–`1.000`, about 50/s. Calibration happens in the game. |
| Game → ESP32 | 5006 | `L,200` / `R,200` / `F,200`: side and strength 0–255. `F` fires all three motors. Strength sets buzz length (120–400 ms). |
| Game → ESP32 | 5006 | `P,0` heartbeat, once a second |

The game sends everything from its listening port (5005), and the ESP32 replies to whatever address and port it last heard from. That makes the EMG stream a reply to traffic the laptop started, so Windows Firewall lets it through without an inbound rule. The heartbeat also means the ESP32 finds the laptop immediately instead of waiting for the first hit, and doesn't depend on broadcasts that phone hotspots often drop.

---

## Running the game

### Requirements

- Windows 10/11 with a VR-capable GPU
- Unity **6000.4.10f1**. Other versions will re-serialize assets; please don't upgrade the project.
- Meta Quest Pro (or Quest 2/3) with the Meta Quest Link app, connected by cable
- Git LFS

### Setup

```bash
git lfs install
git clone https://github.com/Zahidhasan26/Rex.git
```

Open the folder from Unity Hub with 6000.4.10f1. The first import takes a while because of the terrain. Start Quest Link, then press Play in one of these scenes:

| Scene | What it is |
| --- | --- |
| `Assets/Scenes/Main.unity` | The full game |
| `Assets/Scenes/DinoShowcase.unity` | Dinosaur showcase plus the **EMG test panel**. Use this first to check the armband. |
| `Assets/_Game/Dinosaurs/Boss/BossTest.unity` | The final boss fight on its own |
| `Assets/Scenes/DinoTest.unity` | Dinosaur AI sandbox (third person, no headset needed) |

The game runs without any hardware. With no armband the right trigger draws the bow, and with no vest hits are logged to the Console.

### EMG test panel

In `DinoShowcase`, the Game view shows exactly what is arriving from the ESP32:

- link status and the sender's address
- packets per second
- the raw and calibrated signal, with a 4-second graph
- a one-line diagnosis

| Key | Action |
| --- | --- |
| `1` | Calibrate relax |
| `2` | Calibrate flex |
| `3` | Reset calibration |
| `L` / `R` / `F` | Buzz the vest |

If the vest buzzes but the panel shows no packets, the laptop and ESP32 are fine on Wi-Fi and something on the laptop is blocking inbound UDP.

### Boss face

For a preset face, put a straight-on photo (whole head, even light) in `Assets/_Game/Dinosaurs/Boss/FacePhotos/` and assign it to **Preset Photo** on the Boss's `BossFace` component. That folder is git-ignored, so photos never leave the machine.

For a live scan, enable `showOnStart` on the `FaceScanStation`. It asks for consent first, keeps the photo in memory only, and `Backspace` forgets it.

Face landmarks come from MediaPipe Face Mesh running locally through Unity's Inference Engine. Nothing is uploaded.

---

## Tuning

Every gameplay number lives in `Assets/_Game/Flow/GameConfig.asset`, so balancing never needs a code change.

| Field | Default | |
| --- | --- | --- |
| `roundSeconds` | 60 | Survival round length, including the alpha |
| `playerHealth` | 250 | |
| `practiceShots` | 3 | Tutorial targets |
| `playAreaRadius` | 25 m | How far the player can walk from the ring during the fight |
| `calmMusic` / `battleMusic` | — | Looping 2D tracks |
| `musicVolume` / `battleVolume` | 0.5 / 0.35 | Battle music sits under the dinosaur audio |
| `cutscene` | `cutscene_meteor.webm` | Played at the end of Act 1 |

Per-dinosaur behaviour (speed, bite range, retreat, sidestep, hits to kill, `fragileAfterBites`) is on the `Dinosaur` component of each prefab in `Assets/_Game/Dinosaurs/Prefabs/`.

## Architecture

Scripts talk through a small set of contracts in `Assets/_Game/Core`, not to each other's classes. That kept four people on four branches out of each other's files.

| Contract | Purpose |
| --- | --- |
| `GameEvents` | `StateChanged`, `PlayerHit`, `PlayerDied` |
| `GameRefs` | Current state, time left, player head transform |
| `IDamageable` + `HitZoneMarker` | Arrow hits by zone (head, body, legs) |
| `Haptics.Hit(side, strength)` | The only way to buzz the vest |
| `EmgInput.Value` / `HardwareConnected` | Calibrated 0–1 draw from the armband |

`GameFlow` runs the whole act as one coroutine state machine:

```
Calibrate (tutorial) → Countdown → Survive → ActOneComplete → cutscene → restart
                                       └──────► Dead ─────────────────────► restart
```

Hardware is optional at every layer. `EmgReceiver` falls back to the trigger after one second without packets, and `Haptics` logs to the Console when no `VestSender` is present.

More detail, including team ownership and the merge rules we worked by, is in [`ARCHITECTURE.md`](ARCHITECTURE.md).

## Repository layout

```
Assets/
  _Game/
    Core/         shared contracts (events, refs, damage, haptics, EMG)
    Player/       VR rig, bow, arrows, health, EMG receiver, vest sender, EMG test panel
    Dinosaurs/    models, AI, procedural animation, audio, the alpha, Boss/
    World/        spawner, arena, ambient audio
    Flow/         GameFlow, GameConfig, cutscene video
    UI/           HUD
  Scenes/         Main, DinoShowcase, DinoTest
Hardware/
  rex_esp32/      armband + vest firmware
  emg_test/       sensor bench test
```

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| EMG panel shows 0 packets, vest buzzes on `L` | The laptop is blocking inbound UDP. Make sure both ends run the current firmware and scripts, which use the reply-port scheme described above. |
| Vest doesn't buzz either | The ESP32 and laptop are on different networks. Their IP addresses should share the same prefix. Type `s` in Serial Monitor. |
| Calibration step never appears | The armband isn't connected. The tutorial only calibrates when EMG data is arriving. |
| Motors run constantly | Flip `RELAY_ACTIVE_LOW`. |
| Serial Monitor blank on a T-Display S3 | Set *USB CDC On Boot: Enabled*. |
| OBS records a black window | Set the capture method to *Windows 10 (1903 and up)*, or run OBS on the same GPU as Unity (Windows Graphics settings). |

## Known limitations

- PC-tethered only. The ESP32 talks to a laptop over UDP, so there is no standalone Quest build yet.
- Relays give on/off haptics. Hit strength is buzz length, not intensity.
- One EMG channel: draw strength only.
- The final boss runs in its own scene and isn't chained after the cutscene in `Main` yet.

---

## Team

| | |
| --- | --- |
| **Zahid Hasan** | Lead. Player rig, bow and arrows, EMG and vest integration, firmware, game flow, HUD |
| **Zachary Touboul** | Dinosaur AI and animation, the alpha, the Gorgosaurus, final boss and face scan |
| **Halar Ahmed** | [role] |
| **Saber** | [role] |

## Credits

Full attribution is in [`CREDITS.md`](CREDITS.md). In short:

- **Maip and Gorgosaurus** models by [Jurassic Radassic](https://sketchfab.com/Radassic), CC BY 4.0. Retopologized and re-animated for this project.
- **Boss body** based on a model by vmmaniac, CC BY 4.0. The original head was removed and replaced; the boss doesn't depict any real person.
- **Face tracking:** MediaPipe Face Mesh, Apache 2.0.
- **Terrain:** a Unity Asset Store package under the Asset Store EULA, built from OpenStreetMap and USGS data.
- **Battle music:** "Into the Deep Jungle", made by the team with Suno.

Asset Store content and third-party models remain under their own licenses and are not covered by this repository's license.

## License

[Add a license.]
