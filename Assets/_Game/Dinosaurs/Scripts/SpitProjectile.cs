using UnityEngine;

namespace BowGame
{
    // Gorgosaurus spit. Flies in an arc toward where the player was, hurts the player if it lands close enough.
    // The look is a spray, not a ball: child particle systems (mouth burst, droplets, mist, a wobbling clump)
    // follow the travel direction. On impact the world-space trails are left behind to fade out, and
    // impactPrefab (splash + ground splat) is spawned where it hit. No collider needed; EnemyProjectile layer.
    public class SpitProjectile : MonoBehaviour
    {
        public float speed = 12f;
        public float gravity = 4f;
        [Tooltip("Hits the player if it passes within this distance of their head.")]
        public float hitRadius = 0.6f;
        public float lifeTime = 5f;
        [Tooltip("Splash spawned where the spit lands or hits the player.")]
        public GameObject impactPrefab;
        [Tooltip("The spray thins out as it travels: new particles get fainter until this distance (metres).")]
        public float fadeDistance = 14f;
        [Range(0f, 1f)] public float alphaAtFadeDistance = 0.2f;

        ParticleSystem[] systems;
        ParticleSystem.MinMaxGradient[] baseColors;
        Vector3 startPos;

        Transform target;
        Transform shooter;
        float damage;
        Vector3 velocity;
        bool live;
        int groundMask;

        public void Launch(Transform player, float dmg, Transform from)
        {
            target = player;
            shooter = from;
            damage = dmg;

            // Aim slightly below the head (chest) and lead upward so gravity brings it down on target.
            Vector3 aim = player.position + Vector3.down * 0.3f;
            Vector3 delta = aim - transform.position;
            float time = Mathf.Max(0.1f, delta.magnitude / speed);
            velocity = delta / time + 0.5f * gravity * time * Vector3.up;

            groundMask = Physics.DefaultRaycastLayers;
            foreach (var n in new[] { "Dinosaur", "EnemyProjectile", "Player", "Arrow" })
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) groundMask &= ~(1 << l);
            }

            live = true;
            startPos = transform.position;
            systems = GetComponentsInChildren<ParticleSystem>();
            baseColors = new ParticleSystem.MinMaxGradient[systems.Length];
            for (int i = 0; i < systems.Length; i++) baseColors[i] = systems[i].main.startColor;
            if (velocity.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(velocity);
            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            if (!live) return;
            float dt = Time.deltaTime;
            velocity += Vector3.down * gravity * dt;
            Vector3 step = velocity * dt;

            // Hit the player?
            if (target != null)
            {
                Vector3 p = transform.position;
                Vector3 closest = p + Vector3.Project(target.position - p, step.normalized);
                bool between = Vector3.Dot(closest - p, step) >= 0f && (closest - p).sqrMagnitude <= step.sqrMagnitude;
                Vector3 check = between ? closest : p + step;
                if (Vector3.Distance(check, target.position) <= hitRadius)
                {
                    GameEvents.RaisePlayerHit(damage, shooter != null ? shooter.position : transform.position - velocity);
                    Finish(check, -velocity.normalized, onPlayer: true);
                    return;
                }
            }

            // Hit the ground or a tree?
            if (step.sqrMagnitude > 1e-6f &&
                Physics.Raycast(transform.position, step.normalized, out RaycastHit hit, step.magnitude, groundMask, QueryTriggerInteraction.Ignore))
            {
                Finish(hit.point, hit.normal, onPlayer: false);
                return;
            }

            transform.position += step;
            if (velocity.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(velocity);
            FadeWithDistance();
        }

        // Particles emitted further from the mouth start fainter, so the spray thins out over distance.
        void FadeWithDistance()
        {
            if (systems == null || fadeDistance <= 0f) return;
            float t = Mathf.Clamp01(Vector3.Distance(startPos, transform.position) / fadeDistance);
            float k = Mathf.Lerp(1f, alphaAtFadeDistance, t);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                var b = baseColors[i];
                var main = systems[i].main;
                if (b.mode == ParticleSystemGradientMode.TwoColors)
                    main.startColor = new ParticleSystem.MinMaxGradient(Fade(b.colorMin, k), Fade(b.colorMax, k));
                else if (b.mode == ParticleSystemGradientMode.Color)
                    main.startColor = new ParticleSystem.MinMaxGradient(Fade(b.color, k));
            }
        }

        static Color Fade(Color c, float k) { c.a *= k; return c; }

        void Finish(Vector3 point, Vector3 normal, bool onPlayer)
        {
            live = false;

            if (impactPrefab != null)
            {
                var fx = Instantiate(impactPrefab, point + normal * 0.03f, Quaternion.LookRotation(normal));
                // A splash in the player's face is smaller and has no ground splat.
                if (onPlayer)
                {
                    fx.transform.localScale *= 0.5f;
                    var splat = fx.transform.Find("Splat");
                    if (splat != null) splat.gameObject.SetActive(false);
                }
            }

            // Let the droplet and mist trails finish in the air instead of popping out of existence.
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                if (main.simulationSpace != ParticleSystemSimulationSpace.World) continue;
                main.stopAction = ParticleSystemStopAction.Destroy;
                ps.transform.SetParent(null, true);
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            Destroy(gameObject);
        }
    }
}
