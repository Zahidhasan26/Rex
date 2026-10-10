using UnityEngine;

namespace BowGame
{
    // Briefly tints everything on a dinosaur when it is hit. Added automatically the first time it is needed.
    public class HitFlash : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Renderer[] renderers;
        MaterialPropertyBlock block;
        Color color;
        float duration, timeLeft;

        public static void Flash(GameObject target, Color c, float seconds)
        {
            if (target == null) return;
            var f = target.GetComponent<HitFlash>();
            if (f == null) f = target.AddComponent<HitFlash>();
            f.color = c; f.duration = Mathf.Max(0.01f, seconds); f.timeLeft = f.duration;
            f.enabled = true;
        }

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            timeLeft -= Time.deltaTime;
            float k = Mathf.Clamp01(timeLeft / duration);
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (k <= 0f) { r.SetPropertyBlock(null); continue; }
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(Color.white, color, k));
                r.SetPropertyBlock(block);
            }
            if (k <= 0f) enabled = false;
        }
    }
}
