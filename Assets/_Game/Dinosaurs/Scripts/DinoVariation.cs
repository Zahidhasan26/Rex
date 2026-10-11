using UnityEngine;

namespace BowGame
{
    // Small per-dinosaur differences so a wave of the same species doesn't look copy-pasted:
    // a random size and a slight colour tint, picked once when the dinosaur spawns.
    // Size scales the whole dinosaur (hitboxes and NavMeshAgent come along); attack reach, stride and
    // voice pitch are adjusted to match. Runs before the other dinosaur scripts.
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Dinosaur))]
    public class DinoVariation : MonoBehaviour
    {
        [Header("Size")]
        [Tooltip("Random uniform scale range.")]
        public Vector2 scaleRange = new Vector2(0.9f, 1.1f);

        [Header("Colour")]
        [Tooltip("Random brightness multiplier range.")]
        public Vector2 brightnessRange = new Vector2(0.82f, 1.08f);
        [Tooltip("Tints to drift toward. One is picked at random and blended in by up to maxTint.")]
        public Color[] tints =
        {
            new Color(0.70f, 0.80f, 0.52f),   // olive
            new Color(0.92f, 0.68f, 0.55f),   // warm reddish-brown
            new Color(0.66f, 0.72f, 0.86f),   // cool slate
            new Color(0.94f, 0.82f, 0.60f),   // sandy
            new Color(0.62f, 0.60f, 0.58f),   // dark
        };
        [Range(0f, 1f)] public float maxTint = 0.6f;

        public float Scale { get; private set; } = 1f;
        public Color Tint { get; private set; } = Color.white;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock block;
        Behaviour hitFlash;

        void Awake()
        {
            // ---- size
            Scale = Random.Range(scaleRange.x, scaleRange.y);
            transform.localScale *= Scale;

            var dino = GetComponent<Dinosaur>();
            dino.attackRange *= Scale;   // a bigger head reaches further

            var audio = GetComponent<DinoAudio>();
            if (audio != null)
            {
                audio.strideLength *= Scale;
                audio.pitch /= Mathf.Sqrt(Scale);   // bigger sounds a little deeper
            }

            // ---- colour
            float b = Random.Range(brightnessRange.x, brightnessRange.y);
            Color t = tints != null && tints.Length > 0 ? tints[Random.Range(0, tints.Length)] : Color.white;
            Tint = Color.Lerp(Color.white, t, Random.Range(0f, maxTint)) * b;
            Tint = new Color(Tint.r, Tint.g, Tint.b, 1f);

            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMaterial;
                baseColors[i] = m != null && m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : Color.white;
            }
            ApplyTint();
        }

        // The player's hit flash (HitFlash) also writes the colour and clears it when done,
        // so the tint is put back every frame whenever no flash is running.
        void LateUpdate()
        {
            if (hitFlash == null) hitFlash = GetComponent("HitFlash") as Behaviour;
            if (hitFlash != null && hitFlash.enabled) return;
            ApplyTint();
        }

        void ApplyTint()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, baseColors[i] * Tint);
                r.SetPropertyBlock(block);
            }
        }
    }
}
