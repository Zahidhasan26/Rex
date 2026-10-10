using UnityEngine;
using UnityEngine.XR;

namespace BowGame
{
    // Shows what an arrow did: a burst at the impact, a flash on the dinosaur, a word above it, and a controller buzz.
    // Lives on the Player. Arrows call HitFeedback.Instance.Play(...).
    public class HitFeedback : MonoBehaviour
    {
        public static HitFeedback Instance;

        [Header("Effects (prefabs)")]
        public ParticleSystem hitBurst;        // body and leg hits
        public ParticleSystem headshotBurst;   // head hits
        public ParticleSystem dustBurst;       // ground, rocks, trees

        [Header("Look")]
        public Color headColor = new Color(1f, 0.85f, 0.2f);
        public Color bodyColor = new Color(1f, 0.25f, 0.2f);
        public Color legColor = new Color(0.4f, 0.8f, 1f);
        public bool showWords = true;
        public float flashTime = 0.18f;

        void OnEnable() { Instance = this; }
        void OnDisable() { if (Instance == this) Instance = null; }

        public void Play(Vector3 point, Vector3 normal, bool hitDinosaur, HitZone zone, GameObject target)
        {
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
            var prefab = !hitDinosaur ? dustBurst : zone == HitZone.Head ? headshotBurst : hitBurst;
            if (prefab != null)
            {
                var fx = Instantiate(prefab, point, Quaternion.LookRotation(normal));
                Destroy(fx.gameObject, 3f);
            }
            if (!hitDinosaur) return;

            Color c = zone == HitZone.Head ? headColor : zone == HitZone.Legs ? legColor : bodyColor;
            if (target != null) HitFlash.Flash(target, c, zone == HitZone.Head ? flashTime * 1.6f : flashTime);
            if (showWords)
                FloatingText.Spawn(point + Vector3.up * 0.35f, zone == HitZone.Head ? "HEADSHOT" : zone == HitZone.Legs ? "SLOWED" : "HIT", c, zone == HitZone.Head ? 1.5f : 1f);

            Buzz(XRNode.LeftHand, zone == HitZone.Head ? 0.9f : 0.35f, zone == HitZone.Head ? 0.12f : 0.05f);
            if (zone == HitZone.Head) Buzz(XRNode.RightHand, 0.9f, 0.12f);
            Debug.Log("[Hit] " + zone + " on " + (target != null ? target.name : "?"));
        }

        static void Buzz(XRNode node, float amplitude, float seconds)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid) device.SendHapticImpulse(0u, amplitude, seconds);
        }
    }
}
