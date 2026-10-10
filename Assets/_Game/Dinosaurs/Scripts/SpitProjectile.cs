using UnityEngine;

namespace BowGame
{
    // Gorgosaurus spit. Flies in an arc toward where the player was, hurts the player if it lands close enough.
    // Put it on a small glowing sphere (no collider needed) on the EnemyProjectile layer and make it a prefab.
    public class SpitProjectile : MonoBehaviour
    {
        public float speed = 12f;
        public float gravity = 4f;
        [Tooltip("Hits the player if it passes within this distance of their head.")]
        public float hitRadius = 0.6f;
        public float lifeTime = 5f;

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
                    Finish();
                    return;
                }
            }

            // Hit the ground or a tree?
            if (step.sqrMagnitude > 1e-6f &&
                Physics.Raycast(transform.position, step.normalized, step.magnitude, groundMask, QueryTriggerInteraction.Ignore))
            {
                Finish();
                return;
            }

            transform.position += step;
        }

        void Finish()
        {
            live = false;
            Destroy(gameObject);
        }
    }
}
