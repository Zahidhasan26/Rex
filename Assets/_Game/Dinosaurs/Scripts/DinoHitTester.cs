using UnityEngine;
using UnityEngine.InputSystem;

namespace BowGame
{
    // Test helper for DinoTest (no headset, no bow). Left-click a dinosaur in the Game view
    // to hit the zone under the cursor, exactly like an arrow would. Not used in the real game.
    public class DinoHitTester : MonoBehaviour
    {
        public Camera cam;
        [Range(0f, 1f)] public float drawStrength = 1f;
        public float maxDistance = 200f;

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)) return;

            var marker = hit.collider.GetComponent<HitZoneMarker>();
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (marker == null || target == null) return;

            Debug.Log("[DinoHitTester] hit " + hit.collider.GetComponentInParent<Dinosaur>()?.name + " in the " + marker.zone);
            target.TakeHit(marker.zone, drawStrength, hit.point);
        }
    }
}
