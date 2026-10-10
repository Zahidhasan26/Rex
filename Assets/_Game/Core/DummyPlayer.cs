using UnityEngine;

namespace BowGame
{
    // Stand-in for the real player in test scenes, so dinosaurs have something to chase without a headset.
    public class DummyPlayer : MonoBehaviour
    {
        void OnEnable()
        {
            GameRefs.PlayerHead = transform;
            GameEvents.PlayerHit += OnHit;
        }

        void OnDisable()
        {
            GameEvents.PlayerHit -= OnHit;
            if (GameRefs.PlayerHead == transform) GameRefs.PlayerHead = null;
        }

        void OnHit(float damage, Vector3 from)
        {
            Debug.Log("[DummyPlayer] hit for " + damage + " from " + from);
        }
    }
}
