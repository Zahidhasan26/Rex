using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BowGame
{
    // Test-scene helper: keeps a few dinosaurs coming at the player and revives the player after a death.
    // The real game's spawner and round logic will replace this.
    public class CombatTestSpawner : MonoBehaviour
    {
        public GameObject[] dinosaurPrefabs;
        public int maxAlive = 3;
        public float respawnDelay = 3f;
        public float minDistance = 18f;
        public float maxDistance = 30f;
        [Tooltip("Dinosaurs appear inside this arc in front of the spawner (degrees).")]
        public float arcDegrees = 120f;
        public bool revivePlayer = true;
        public float reviveDelay = 3f;

        [Header("Automatic test fire (no headset needed)")]
        [Tooltip("Fires arrows at the nearest dinosaur by itself. For checking hits without a headset.")]
        public bool autoFire;
        public Arrow arrowPrefab;
        public float autoFireInterval = 1.2f;

        readonly List<GameObject> alive = new List<GameObject>();
        float nextSpawn, nextShot;
        int spawned;

        void OnEnable() { GameEvents.DinoDied += OnDinoDied; GameEvents.PlayerDied += OnPlayerDied; }
        void OnDisable() { GameEvents.DinoDied -= OnDinoDied; GameEvents.PlayerDied -= OnPlayerDied; }

        void Start()
        {
#if UNITY_EDITOR
            if (System.IO.File.Exists("Temp/rex_autofire")) autoFire = true;
#endif
            nextSpawn = Time.time + 1f;
            nextShot = Time.time + 3f;
        }

        void Update()
        {
            alive.RemoveAll(d => d == null);
            if (alive.Count < maxAlive && Time.time >= nextSpawn) { Spawn(); nextSpawn = Time.time + respawnDelay; }
            if (autoFire && arrowPrefab != null && Time.time >= nextShot) { nextShot = Time.time + autoFireInterval; AutoShoot(); }
        }

        void Spawn()
        {
            if (dinosaurPrefabs == null || dinosaurPrefabs.Length == 0) return;
            var prefab = dinosaurPrefabs[spawned % dinosaurPrefabs.Length];
            if (prefab == null) return;
            Vector3 centre = GameRefs.PlayerHead != null ? GameRefs.PlayerHead.position : transform.position;
            centre.y = transform.position.y;
            float angle = Random.Range(-arcDegrees * 0.5f, arcDegrees * 0.5f);
            Vector3 pos = centre + Quaternion.Euler(0f, angle, 0f) * transform.forward * Random.Range(minDistance, maxDistance);
            if (!NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas)) return;
            Vector3 look = centre - hit.position; look.y = 0f;
            var dino = Instantiate(prefab, hit.position, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
            alive.Add(dino);
            spawned++;
        }

        void OnDinoDied(GameObject dino)
        {
            alive.Remove(dino);
            nextSpawn = Mathf.Max(nextSpawn, Time.time + respawnDelay);
        }

        void OnPlayerDied() { if (revivePlayer) Invoke(nameof(Revive), reviveDelay); }

        void Revive() { if (PlayerHealth.Instance != null) PlayerHealth.Instance.ResetHealth(); }

        void AutoShoot()
        {
            Transform head = GameRefs.PlayerHead;
            if (head == null) return;
            HitZoneMarker best = null; float bestDist = float.MaxValue;
            foreach (var d in alive)
            {
                if (d == null) continue;
                var zones = d.GetComponentsInChildren<HitZoneMarker>();
                if (zones.Length == 0) continue;
                var z = zones[Random.Range(0, zones.Length)];
                float dist = Vector3.Distance(head.position, z.transform.position);
                if (dist < bestDist) { bestDist = dist; best = z; }
            }
            if (best == null) return;
            var col = best.GetComponent<Collider>();
            Vector3 aim = col != null ? col.bounds.center : best.transform.position;
            Vector3 start = head.position + Vector3.up * 1.4f;
            Vector3 delta = aim - start;
            float time = Mathf.Max(0.05f, delta.magnitude / 60f);
            Vector3 velocity = delta / time + 0.5f * 9.81f * time * Vector3.up;
            var arrow = Instantiate(arrowPrefab, start, Quaternion.LookRotation(velocity));
            arrow.Launch(velocity, head.root, 1f);
        }
    }
}
