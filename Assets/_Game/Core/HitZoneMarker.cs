using UnityEngine;

namespace BowGame
{
    // Put one on every dinosaur hitbox collider. The arrow reads it to know which zone it hit.
    public class HitZoneMarker : MonoBehaviour
    {
        public HitZone zone;
    }
}
