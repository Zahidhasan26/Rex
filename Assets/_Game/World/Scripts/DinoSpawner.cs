using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BowGame
{
    // Spawns dinosaurs on a ring around the player, mostly outside the player's view,
    // faster and faster over time, with a cap on how many are alive at once.
    // By default it only spawns during GameState.Survive; set Spawn Mode to Always for test scenes.
    public class DinoSpawner : MonoBehaviour
    {
        public enum SpawnMode { OnlyDuringSurvive, Always }

        [System.Serializable]
        public class DinoEntry
        {
            public Dinosaur prefab;
            [Tooltip("Higher = picked more often.")]
            public float weight = 1f;
            [Tooltip("Seconds after spawning starts before this type can appear.")]
            public float unlockAfter = 0f;
            [Tooltip("If none of this type is alive, it is spawned next (guarantees e.g. one biter at all times).")]
            public bool keepOneAlive = false;
        }

        [Header("Opening (alpha)")]
        [Tooltip("Optional. This one dinosaur spawns alone, in front of the player. Nothing else spawns until it is dead.")]
        public Dinosaur openingDino;
        [Tooltip("How far in front of the player the opening dinosaur appears, in metres.")]
        public float openingDistance = 32f;
        [Tooltip("Pause after the opening dinosaur dies before the others start coming.")]
        public float delayAfterOpening = 3f;

        [Header("What to spawn")]
        public List<DinoEntry> dinosaurs = new List<DinoEntry>();

        [Header("When")]
        public SpawnMode mode = SpawnMode.OnlyDuringSurvive;
        [Tooltip("Seconds between spawns at the start.")]
        public float startInterval = 6f;
        [Tooltip("Seconds between spawns once fully ramped up.")]
        public float endInterval = 1.5f;
        [Tooltip("Seconds to go from the start interval to the end interval. Fast mode survive is 30 s, regular is 180 s.")]
        public float rampDuration = 30f;
        [Range(0f, 0.5f)] public float intervalJitter = 0.25f;
        [Tooltip("Delay before the first spawn.")]
        public float firstSpawnDelay = 1.5f;
        [Tooltip("Never more than this many alive at once (protects frame rate).")]
        public int maxAlive = 6;

        [Header("Where")]
        [Tooltip("Spawn distance from the player, in metres (min, max).")]
        public Vector2 spawnDistance = new Vector2(28f, 40f);
        [Tooltip("Don't spawn within this many degrees either side of where the player is looking.")]
        [Range(0f, 170f)] public float hiddenFromViewAngle = 70f;
        [Tooltip("Keep new dinosaurs at least this far from living ones.")]
        public float minSeparation = 6f;
        [Tooltip("How far a picked point may be moved to land on the NavMesh.")]
        public float navMeshSnap = 5f;
        public int placementAttempts = 12;

        [Header("Round end")]
        [Tooltip("Remove all dinosaurs when the round ends (ActOneComplete).")]
        public bool clearOnActOneComplete = true;

        readonly List<Dinosaur> alive = new List<Dinosaur>();
        float elapsed;
        float nextSpawn;
        bool running;
        Dinosaur opening;
        bool openingPending;

        public int AliveCount { get { Prune(); return alive.Count; } }

        void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.DinoDied += OnDinoDied;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.DinoDied -= OnDinoDied;
        }

        void Start()
        {
            if (mode == SpawnMode.Always) Begin();
            else if (GameRefs.State == GameState.Survive) Begin();
        }

        void OnStateChanged(GameState s)
        {
            if (mode == SpawnMode.Always) return;
            if (s == GameState.Survive) Begin();
            else
            {
                running = false;
                // ---- FINAL ACT HOOK: GameFlow decides what happens next; the spawner just stops. ----
                if (s == GameState.ActOneComplete && clearOnActOneComplete) ClearAll();
            }
        }

        void OnDinoDied(GameObject dino)
        {
            var d = dino != null ? dino.GetComponent<Dinosaur>() : null;
            if (d != null) alive.Remove(d);
        }

        public void Begin()
        {
            running = true;
            elapsed = 0f;
            nextSpawn = firstSpawnDelay;
            opening = null;
            openingPending = openingDino != null;
        }

        public void Stop() => running = false;

        void Update()
        {
            if (!running || GameRefs.PlayerHead == null) return;

            // Opening: one dinosaur alone. The clock for everything else starts when it dies.
            if (openingPending)
            {
                if (opening == null)
                {
                    opening = SpawnOpening();
                    if (opening == null) openingPending = false;   // no valid spot: don't block the round
                    return;
                }
                if (!opening.IsDead) return;
                openingPending = false;
                elapsed = 0f;
                nextSpawn = delayAfterOpening;
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed < nextSpawn) return;

            float t = rampDuration > 0f ? Mathf.Clamp01(elapsed / rampDuration) : 1f;
            float interval = Mathf.Lerp(startInterval, endInterval, t) * (1f + Random.Range(-intervalJitter, intervalJitter));
            nextSpawn = elapsed + Mathf.Max(0.2f, interval);

            if (AliveCount < maxAlive) SpawnOne();
        }

        // Spawns one dinosaur now. Returns null if no valid spot or type was found.
        public Dinosaur SpawnOne()
        {
            var entry = PickEntry();
            if (entry == null || entry.prefab == null) return null;
            if (!FindSpawnPoint(out Vector3 pos)) return null;

            Vector3 look = GameRefs.PlayerHead != null ? GameRefs.PlayerHead.position - pos : -pos;
            look.y = 0f;
            Quaternion rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;

            var d = Instantiate(entry.prefab, pos, rot, transform);
            alive.Add(d);
            return d;
        }

        Dinosaur SpawnOpening()
        {
            Transform player = GameRefs.PlayerHead;
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

            // Straight ahead first, then a little to either side if that spot is off the NavMesh.
            foreach (float angle in new[] { 0f, 15f, -15f, 30f, -30f, 50f, -50f })
            {
                foreach (float scale in new[] { 1f, 0.75f, 0.5f })
                {
                    Vector3 p = player.position + Quaternion.Euler(0f, angle, 0f) * forward * (openingDistance * scale);
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, navMeshSnap, NavMesh.AllAreas)) continue;
                    Vector3 look = player.position - hit.position;
                    look.y = 0f;
                    var d = Instantiate(openingDino, hit.position, Quaternion.LookRotation(look), transform);
                    alive.Add(d);
                    return d;
                }
            }
            return null;
        }

        public void ClearAll()
        {
            Prune();
            foreach (var d in alive) if (d != null) Destroy(d.gameObject);
            alive.Clear();
        }

        DinoEntry PickEntry()
        {
            // Guaranteed types first: if none of that kind is alive, it goes next.
            Prune();
            foreach (var e in dinosaurs)
            {
                if (!e.keepOneAlive || e.prefab == null || elapsed < e.unlockAfter) continue;
                bool any = false;
                foreach (var d in alive) if (d != null && d.GetType() == e.prefab.GetType() && d != opening) { any = true; break; }
                if (!any) return e;
            }

            float total = 0f;
            foreach (var e in dinosaurs) if (e.prefab != null && elapsed >= e.unlockAfter) total += Mathf.Max(0f, e.weight);
            if (total <= 0f) return null;

            float r = Random.value * total;
            foreach (var e in dinosaurs)
            {
                if (e.prefab == null || elapsed < e.unlockAfter) continue;
                r -= Mathf.Max(0f, e.weight);
                if (r <= 0f) return e;
            }
            return null;
        }

        bool FindSpawnPoint(out Vector3 result)
        {
            Transform player = GameRefs.PlayerHead;
            Vector3 center = player != null ? player.position : transform.position;
            Vector3 forward = player != null ? player.forward : Vector3.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

            // Allowed arc is everything outside the view cone: from +hidden to 360-hidden degrees.
            for (int i = 0; i < placementAttempts; i++)
            {
                float angle = Random.Range(hiddenFromViewAngle, 360f - hiddenFromViewAngle);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                Vector3 p = center + dir * Random.Range(spawnDistance.x, spawnDistance.y);

                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, navMeshSnap, NavMesh.AllAreas)) continue;
                if (TooCloseToOthers(hit.position)) continue;

                result = hit.position;
                return true;
            }
            result = default;
            return false;
        }

        bool TooCloseToOthers(Vector3 p)
        {
            Prune();
            float min2 = minSeparation * minSeparation;
            foreach (var d in alive)
                if (d != null && (d.transform.position - p).sqrMagnitude < min2) return true;
            return false;
        }

        void Prune() => alive.RemoveAll(d => d == null || d.IsDead);

        void OnDrawGizmosSelected()
        {
            Transform player = GameRefs.PlayerHead;
            Vector3 c = player != null ? player.position : transform.position;
            c.y = transform.position.y;
            Gizmos.color = new Color(1f, 0.5f, 0f);
            DrawCircle(c, spawnDistance.x);
            DrawCircle(c, spawnDistance.y);
        }

        static void DrawCircle(Vector3 c, float r)
        {
            Vector3 prev = c + Vector3.forward * r;
            for (int i = 1; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                Vector3 next = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
