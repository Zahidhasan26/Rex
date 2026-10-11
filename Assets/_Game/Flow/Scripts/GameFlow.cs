using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BowGame
{
    // Act 1 from start to finish:
    //   spawn in the ring -> tutorial (controls, calibration if the armband is connected, 3 practice arrows,
    //   walk forward then backward with the left joystick) -> face the front -> countdown -> fight (alpha first)
    //   -> survived / died -> restart.
    // Sets GameRefs.State / StateTimeLeft and raises GameEvents.StateChanged; the spawner, dinosaurs and HUD react to that.
    // Keyboard for testing without the headset: T = skip the tutorial, R = restart after the round.
    public class GameFlow : MonoBehaviour
    {
        public GameConfig config;
        public DinoSpawner spawner;
        public Material ringMaterial;
        public Material targetMaterial;

        public static GameFlow Instance;

        XROrigin origin;
        AudioSource calm, battle;
        InputAction restartAction, skipAction;
        Coroutine flow;
        string message = "";
        float messageRefreshAt;
        int shots;
        bool skipRequested;
        bool clampToArena;
        Vector3 ringCenter;
        LineRenderer ring, frontArrow, guideArrow;
        readonly List<GameObject> targets = new List<GameObject>();

        // ---------------------------------------------------------------- setup

        void Awake()
        {
            Instance = this;
            SpotTeleporter.Enabled = false;   // the dev teleport tool stays off during real rounds
        }

        void OnEnable()
        {
            GameEvents.PlayerDied += OnPlayerDied;
            Bow.Fired += OnBowFired;
            Dinosaur.BecameFragile += OnDinoFragile;
            restartAction = new InputAction("Restart", InputActionType.Button);
            restartAction.AddBinding("<XRController>{RightHand}/primaryButton");
            restartAction.AddBinding("<Keyboard>/r");
            skipAction = new InputAction("SkipTutorial", InputActionType.Button);
            skipAction.AddBinding("<Keyboard>/t");
            restartAction.Enable();
            skipAction.Enable();
        }

        void OnDisable()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
            Bow.Fired -= OnBowFired;
            Dinosaur.BecameFragile -= OnDinoFragile;
            restartAction?.Disable(); restartAction?.Dispose();
            skipAction?.Disable(); skipAction?.Dispose();
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            if (config == null) { Debug.LogError("GameFlow: no GameConfig assigned."); yield break; }
            origin = FindAnyObjectByType<XROrigin>();
            if (FindAnyObjectByType<JoystickMove>() == null) gameObject.AddComponent<JoystickMove>();   // left joystick always walks
            calm = MakeMusic("Music_Calm", config.calmMusic);
            battle = MakeMusic("Music_Battle", config.battleMusic);
            ringCenter = Ground(config.ringSpot.x, config.ringSpot.y);
            BuildVisuals();
            if (spawner != null)
            {
                spawner.useFrontArc = true;
                spawner.frontDirection = YawDir(config.frontYaw);
            }
            yield return null;   // let the VR rig start tracking
            TeleportToRing();
            SetRingVisible(true);
            flow = StartCoroutine(RunRound(true));
        }

        // ---------------------------------------------------------------- the round

        IEnumerator RunRound(bool tutorial)
        {
            SetState(GameState.Calibrate);
            ApplyHealth();
            FadeTo(calm);
            skipRequested = false;

            if (tutorial)
            {
                Say("Hold the bow in your LEFT hand.\nRIGHT trigger: grab the string,\npull your hand back, let go to shoot.");
                yield return WaitOrSkip(config.controlsSeconds);

                if (EmgInput.HardwareConnected && !skipRequested)
                {
                    Say("Calibration: relax your arm");
                    yield return WaitOrSkip(config.relaxSeconds);
                    Say("Now flex hard!");
                    yield return WaitOrSkip(config.flexSeconds);
                    Say("Calibrated");
                    yield return WaitOrSkip(1.2f);
                }

                if (!skipRequested)
                {
                    SpawnTargets();
                    shots = 0;
                    while (shots < config.practiceShots && !skipRequested)
                    {
                        Say("Practice: shoot " + config.practiceShots + " arrows at the targets\n(" + shots + "/" + config.practiceShots + ")");
                        yield return null;
                    }
                    ClearTargets();
                    Say("Nice shooting!");
                    yield return WaitOrSkip(1.2f);
                }

                clampToArena = true;
                if (!skipRequested)
                    yield return WalkPractice("Push the LEFT JOYSTICK forward to walk", +1f, config.walkForwardSeconds);
                if (!skipRequested)
                    yield return WalkPractice("Now pull it back to walk backward", -1f, config.walkBackSeconds);
                if (!skipRequested)
                {
                    Say("Good! Head back into the ring");
                    while (!InRing() && !skipRequested) { UpdateGuide(true); yield return null; }
                    UpdateGuide(false);
                }
                if (skipRequested) TeleportToRing();
            }
            else
            {
                TeleportToRing();
            }
            SetRingVisible(true);

            clampToArena = true;
            while (!FacingFront())
            {
                Say("Turn to face the arrow:\nthe dinosaurs come from there");
                yield return null;
            }
            SetRingVisible(false);

            SetState(GameState.Countdown);
            Say("Get ready");
            float t = config.countdownSeconds;
            while (t > 0f) { GameRefs.StateTimeLeft = t; t -= Time.deltaTime; yield return null; }

            FadeTo(battle);
            SetState(GameState.Survive);
            Say("Survive!", 2f);
            t = config.roundSeconds;
            while (t > 0f) { GameRefs.StateTimeLeft = t; t -= Time.deltaTime; yield return null; }
            GameRefs.StateTimeLeft = 0f;

            SetState(GameState.ActOneComplete);
            FadeTo(calm);
            clampToArena = false;
            // ---- PART 2 HOOK: the meteor cutscene video plays here (ARCHITECTURE.md section 5b). ----
            Say("You survived!\nPress A to play again");
            yield return WaitForRestart();
            Restart();
        }

        void OnPlayerDied()
        {
            if (GameRefs.State != GameState.Survive && GameRefs.State != GameState.Countdown) return;
            if (flow != null) StopCoroutine(flow);
            SetState(GameState.Dead);
            FadeTo(calm);
            clampToArena = false;
            Say("You died\nPress A to try again");
            flow = StartCoroutine(DeadThenRestart());
        }

        IEnumerator DeadThenRestart()
        {
            yield return WaitForRestart();
            Restart();
        }

        IEnumerator WaitForRestart()
        {
            yield return new WaitForSeconds(1.5f);   // so a held button doesn't restart instantly
            while (!restartAction.WasPressedThisFrame()) yield return null;
        }

        void Restart()
        {
            if (flow != null) StopCoroutine(flow);
            if (spawner != null) spawner.ClearAll();
            ClearTargets();
            flow = StartCoroutine(RunRound(!config.skipTutorialOnRestart));
        }

        void OnBowFired() { shots++; }

        void OnDinoFragile(Dinosaur d)
        {
            if (GameRefs.State == GameState.Survive) Say("It's weak now!\nOne arrow finishes it!", 3f);
        }

        // Waits until the player has walked in one direction (+1 forward, -1 backward, relative to where they look)
        // for about `seconds` in total. The time isn't shown to the player.
        IEnumerator WalkPractice(string text, float dir, float seconds)
        {
            Say(text);
            if (origin == null) yield break;
            float walked = 0f;
            Vector3 last = origin.transform.position;
            while (walked < seconds && !skipRequested)
            {
                yield return null;
                Vector3 now = origin.transform.position;
                Vector3 f = origin.Camera.transform.forward; f.y = 0f; f.Normalize();
                Vector3 step = now - last; step.y = 0f;
                last = now;
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                if (Vector3.Dot(step, f) * dir / dt > 0.3f) walked += dt;   // moving the right way at a real walking pace
            }
            Say("Nice!");
            yield return WaitOrSkip(1f);
        }

        IEnumerator WaitOrSkip(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !skipRequested) yield return null;
        }

        void Update()
        {
            if (skipAction != null && skipAction.WasPressedThisFrame()) skipRequested = true;

            // Keep the message on the HUD (the HUD's own state messages would otherwise replace it).
            if (PlayerHud.Instance != null && Time.time >= messageRefreshAt && !string.IsNullOrEmpty(message))
            {
                PlayerHud.Instance.Show(message, 0.6f);
                messageRefreshAt = Time.time + 0.25f;
            }
            if (guideArrow != null && guideArrow.enabled) UpdateGuide(true);
        }

        void LateUpdate()
        {
            if (!clampToArena || origin == null) return;
            Vector3 head = origin.Camera.transform.position;
            Vector3 d = head - ringCenter; d.y = 0f;
            float over = d.magnitude - config.playAreaRadius;
            if (over <= 0f) return;
            MoveRig(-d.normalized * over);
        }

        // ---------------------------------------------------------------- helpers

        void SetState(GameState s)
        {
            GameRefs.State = s;
            GameRefs.StateTimeLeft = 0f;
            GameEvents.RaiseStateChanged(s);
        }

        void Say(string text, float seconds = -1f)
        {
            message = text;
            messageRefreshAt = 0f;
            if (seconds > 0f) StartCoroutine(ClearMessageLater(text, seconds));
        }

        IEnumerator ClearMessageLater(string text, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (message == text) message = "";
        }

        void ApplyHealth()
        {
            var h = PlayerHealth.Instance;
            if (h == null) return;
            h.maxHealth = config.playerHealth;
            h.ResetHealth();
        }

        bool InRing()
        {
            if (origin == null) return true;
            Vector3 d = origin.Camera.transform.position - ringCenter; d.y = 0f;
            return d.magnitude <= config.ringRadius;
        }

        bool FacingFront()
        {
            if (origin == null) return true;
            Vector3 f = origin.Camera.transform.forward; f.y = 0f;
            return Vector3.Angle(f, YawDir(config.frontYaw)) <= config.facingTolerance;
        }

        static Vector3 YawDir(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        void TeleportToRing() => Teleport(config.ringSpot, config.frontYaw);

        void Teleport(Vector2 xz, float yaw)
        {
            if (origin == null) return;
            var cam = origin.Camera.transform;
            var cc = origin.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            origin.RotateAroundCameraUsingOriginUp(Mathf.DeltaAngle(cam.eulerAngles.y, yaw));
            Vector3 feet = FindClearSpot(xz.x, xz.y);
            Vector3 off = cam.position - origin.transform.position; off.y = 0f;
            origin.transform.position = feet + Vector3.up * 0.05f - off;
            if (cc != null) cc.enabled = true;
        }

        void MoveRig(Vector3 delta)
        {
            var cc = origin.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            origin.transform.position += delta;
            if (cc != null) cc.enabled = true;
        }

        // ---------------------------------------------------------------- visuals

        void BuildVisuals()
        {
            ring = MakeLine("ArenaRing", 0.08f, true);
            const int n = 64;
            ring.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float x = ringCenter.x + Mathf.Cos(a) * config.ringRadius, z = ringCenter.z + Mathf.Sin(a) * config.ringRadius;
                ring.SetPosition(i, new Vector3(x, Ground(x, z).y + 0.06f, z));
            }

            frontArrow = MakeLine("FrontArrow", 0.12f, false);
            Vector3 f = YawDir(config.frontYaw), r = Vector3.Cross(Vector3.up, f);
            Vector3 tail = ringCenter + f * 0.6f, tip = ringCenter + f * 2.2f;
            SetArrow(frontArrow, tail, tip, r);

            guideArrow = MakeLine("GuideArrow", 0.1f, false);
            SetRingVisible(false);
            guideArrow.enabled = false;
        }

        LineRenderer MakeLine(string name, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = width;
            lr.material = ringMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            return lr;
        }

        void SetArrow(LineRenderer lr, Vector3 tail, Vector3 tip, Vector3 right)
        {
            Vector3 back = (tail - tip).normalized * 0.5f;
            Vector3 lift = Vector3.up * 0.08f;
            lr.positionCount = 5;
            lr.SetPosition(0, Lift(tail) + lift);
            lr.SetPosition(1, Lift(tip) + lift);
            lr.SetPosition(2, Lift(tip + back + right * 0.35f) + lift);
            lr.SetPosition(3, Lift(tip) + lift);
            lr.SetPosition(4, Lift(tip + back - right * 0.35f) + lift);
        }

        Vector3 Lift(Vector3 p) => new Vector3(p.x, Ground(p.x, p.z).y, p.z);

        void SetRingVisible(bool on)
        {
            if (ring != null) ring.enabled = on;
            if (frontArrow != null) frontArrow.enabled = on;
        }

        void UpdateGuide(bool on)
        {
            if (guideArrow == null) return;
            guideArrow.enabled = on && origin != null;
            if (!guideArrow.enabled) return;
            Vector3 head = origin.Camera.transform.position;
            Vector3 to = ringCenter - head; to.y = 0f;
            if (to.magnitude < config.ringRadius + 1f) { guideArrow.enabled = false; return; }
            to.Normalize();
            Vector3 start = head + to * 1.2f;
            SetArrow(guideArrow, start, start + to * 1.3f, Vector3.Cross(Vector3.up, to));
        }

        void SpawnTargets()
        {
            ClearTargets();
            if (origin == null) return;
            Vector3 head = origin.Camera.transform.position;
            Vector3 f = origin.Camera.transform.forward; f.y = 0f; f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 p = head + f * config.targetDistance + r * (i * config.targetSpacing);
                p.y = Ground(p.x, p.z).y + 1.4f;
                var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                t.name = "PracticeTarget";
                t.transform.position = p;
                t.transform.rotation = Quaternion.LookRotation(Vector3.up, -f) * Quaternion.identity;
                t.transform.up = (head - p).normalized;
                t.transform.localScale = new Vector3(0.9f, 0.04f, 0.9f);
                if (targetMaterial != null) t.GetComponent<Renderer>().sharedMaterial = targetMaterial;
                t.AddComponent<HitZoneMarker>().zone = HitZone.Body;
                t.AddComponent<TestTarget>();
                targets.Add(t);
            }
        }

        void ClearTargets()
        {
            foreach (var t in targets) if (t != null) Destroy(t);
            targets.Clear();
        }

        // ---------------------------------------------------------------- music

        AudioSource MakeMusic(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            src.volume = 0f;
            return src;
        }

        void FadeTo(AudioSource target)
        {
            StartCoroutine(Fade(calm, target == calm ? config.musicVolume : 0f));
            StartCoroutine(Fade(battle, target == battle ? config.musicVolume : 0f));
        }

        IEnumerator Fade(AudioSource src, float to)
        {
            if (src == null || src.clip == null) yield break;
            if (to > 0f && !src.isPlaying) src.Play();
            float from = src.volume, t = 0f, dur = Mathf.Max(0.01f, config.musicFade);
            while (t < dur) { t += Time.deltaTime; src.volume = Mathf.Lerp(from, to, t / dur); yield return null; }
            src.volume = to;
            if (to <= 0f) src.Stop();
        }

        // ---------------------------------------------------------------- terrain

        static Terrain TerrainAt(float x, float z)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var p = t.GetPosition(); var s = t.terrainData.size;
                if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z) return t;
            }
            return null;
        }

        static Vector3 Ground(float x, float z)
        {
            var t = TerrainAt(x, z);
            float y = t != null ? t.SampleHeight(new Vector3(x, 0f, z)) + t.GetPosition().y : 0f;
            return new Vector3(x, y, z);
        }

        // Nearest spot (up to ~15 m away) that isn't inside a tree, rock or bush and isn't steep.
        static Vector3 FindClearSpot(float x, float z)
        {
            int mask = ~0;
            foreach (var n in new[] { "Player", "Ground", "Arrow" })
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) mask &= ~(1 << l);
            }
            for (float r = 0f; r <= 15f; r += 1.5f)
            {
                int steps = r == 0f ? 1 : 12;
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    float px = x + Mathf.Cos(a) * r, pz = z + Mathf.Sin(a) * r;
                    var t = TerrainAt(px, pz);
                    if (t == null) continue;
                    var tp = t.GetPosition(); var size = t.terrainData.size;
                    var normal = t.terrainData.GetInterpolatedNormal((px - tp.x) / size.x, (pz - tp.z) / size.z);
                    if (normal.y < 0.9f) continue;
                    var feet = new Vector3(px, t.SampleHeight(new Vector3(px, 0f, pz)) + tp.y, pz);
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.7f, 0.5f, mask, QueryTriggerInteraction.Ignore)) continue;
                    return feet;
                }
            }
            return Ground(x, z);
        }
    }
}
