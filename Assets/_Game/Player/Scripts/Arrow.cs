using UnityEngine;

namespace BowGame
{
    // A fired arrow. Flies in an arc, reports what it hits through the shared contracts, then sticks in.
    public class Arrow : MonoBehaviour
    {
        public float gravity = 9.81f;
        public float lifeTime = 8f;
        public float stuckTime = 6f;
        public float embedDepth = 0.12f;     // how far the tip sinks into what it hits
        public float halfLength = 0.37f;     // distance from the arrow's centre to its tip
        public TrailRenderer trail;

        Vector3 velocity;
        Transform owner;
        float strength;
        bool flying;
        int mask;
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public void Launch(Vector3 startVelocity, Transform ownerRoot, float drawStrength01)
        {
            velocity = startVelocity;
            owner = ownerRoot;
            strength = drawStrength01;
            flying = true;
            mask = Physics.DefaultRaycastLayers;
            foreach (var n in new[] { "Arrow", "Player", "UI" })
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) mask &= ~(1 << l);
            }
            if (velocity.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(velocity);
            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            if (!flying) return;
            float dt = Time.deltaTime;
            Vector3 g = Vector3.down * gravity;
            Vector3 tip = transform.position + transform.forward * halfLength;
            Vector3 step = velocity * dt + 0.5f * g * dt * dt;
            velocity += g * dt;
            float dist = step.magnitude;
            if (dist < 1e-5f) return;
            Vector3 dir = step / dist;

            int n = Physics.RaycastNonAlloc(tip, dir, hits, dist, mask, QueryTriggerInteraction.Collide);
            int best = -1; float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null) continue;
                if (owner != null && c.transform.IsChildOf(owner)) continue;
                if (c.GetComponentInParent<Arrow>() != null) continue;
                if (c.isTrigger && c.GetComponent<HitZoneMarker>() == null) continue;   // ignore unrelated trigger volumes
                if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = i; }
            }

            if (best >= 0) { Stick(hits[best], dir); return; }

            transform.position += step;
            if (velocity.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(velocity);
        }

        void Stick(RaycastHit hit, Vector3 dir)
        {
            flying = false;
            transform.rotation = Quaternion.LookRotation(dir);
            transform.position = hit.point - dir * (halfLength - embedDepth);
            transform.SetParent(hit.collider.transform, true);
            if (trail != null) trail.emitting = false;

            var marker = hit.collider.GetComponent<HitZoneMarker>();
            var target = hit.collider.GetComponentInParent<IDamageable>();
            var zone = marker != null ? marker.zone : HitZone.Body;
            var targetComponent = target as Component;
            if (HitFeedback.Instance != null)
                HitFeedback.Instance.Play(hit.point, hit.normal, target != null, zone, targetComponent != null ? targetComponent.gameObject : null);
            if (target != null) target.TakeHit(zone, strength, hit.point);

            CancelInvoke();
            Destroy(gameObject, stuckTime);
        }
    }
}
