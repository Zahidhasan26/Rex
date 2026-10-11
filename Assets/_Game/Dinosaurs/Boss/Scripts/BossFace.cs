using UnityEngine;

namespace BowGame
{
    // Puts the face scan (FaceScanStore) on the boss's head.
    // The head mesh has three materials: Boss_Skin, Boss_Face (front plate with a straight-on UV projection)
    // and Boss_Hair. This builds one face texture: the photo inside a soft-edged oval, blended into a skin
    // colour sampled from the photo's cheeks and forehead, and tints Boss_Skin to the same colour so the
    // edge of the face plate disappears. With no scan, the boss wears a plain porcelain mask.
    // Everything is built in memory at runtime; nothing is saved.
    public class BossFace : MonoBehaviour
    {
        public Renderer headRenderer;
        public string skinMaterial = "Boss_Skin";
        public string faceMaterial = "Boss_Face";

        [Header("Where the photo lands on the face plate (UV 0..1)")]
        [Tooltip("Photo box on the face plate: x,y = lower-left corner, width, height. Matches the scan's eye line to the head's eyes.")]
        public Rect photoBox = new Rect(0.08f, 0.115f, 0.84f, 0.835f);
        [Tooltip("Size of the face oval inside the photo box (1 = touches the box edges). Smaller keeps ears, hair and background out.")]
        public Vector2 ovalSize = new Vector2(0.8f, 0.94f);
        [Tooltip("Soft edge of the oval, as a fraction of its size.")]
        [Range(0.02f, 0.4f)] public float feather = 0.2f;
        public int textureSize = 512;

        [Header("No scan")]
        [Tooltip("Optional face used when there is no scan. Empty = generated porcelain mask.")]
        public Texture2D defaultFace;
        public Color maskColour = new Color(0.86f, 0.84f, 0.8f);

        [Header("Look")]
        [Tooltip("Pull the photo slightly toward the skin tone so lighting differences show less.")]
        [Range(0f, 0.5f)] public float toneBlend = 0.1f;
        [Tooltip("Slight darkening so the photo sits in the scene's lighting instead of glowing.")]
        [Range(0.5f, 1.2f)] public float photoBrightness = 0.92f;

        Material skinMat, faceMat;
        Texture2D built;

        public Color SkinTone { get; private set; }

        void Awake()
        {
            if (headRenderer == null)
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.name.StartsWith(faceMaterial)) { headRenderer = r; break; }
            if (headRenderer == null) { Debug.LogWarning("BossFace: no head renderer with a " + faceMaterial + " material.", this); enabled = false; return; }

            var mats = headRenderer.materials;   // per-boss copies
            foreach (var m in mats)
            {
                if (m.name.StartsWith(skinMaterial)) skinMat = m;
                else if (m.name.StartsWith(faceMaterial)) faceMat = m;
            }
        }

        void OnEnable()
        {
            FaceScanStore.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            FaceScanStore.Changed -= Apply;
        }

        void OnDestroy()
        {
            if (built != null) Destroy(built);
        }

        public void Apply()
        {
            if (faceMat == null) return;
            Texture2D src = FaceScanStore.Face;
            Color tone;
            if (src != null)
            {
                tone = SampleSkin(src);
                Build(src, tone, true);
            }
            else if (defaultFace != null)
            {
                tone = SampleSkin(defaultFace);
                Build(defaultFace, tone, true);
            }
            else
            {
                tone = maskColour;
                BuildMask(tone);
            }
            SkinTone = tone;
            SetColour(faceMat, Color.white);
            SetTexture(faceMat, built);
            if (skinMat != null) SetColour(skinMat, tone);
        }

        // ---------- texture building ----------

        void Build(Texture2D photo, Color tone, bool isPhoto)
        {
            Prepare();
            int n = textureSize;
            var px = new Color32[n * n];
            Color32 toneC = tone;
            bool readable = photo.isReadable;
            if (!readable) Debug.LogWarning("BossFace: face texture is not readable; using the mask.", this);
            for (int y = 0; y < n; y++)
            {
                float v = (y + 0.5f) / n;
                float pv = (v - photoBox.y) / photoBox.height;
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;
                    float pu = (u - photoBox.x) / photoBox.width;
                    float a = readable ? OvalAlpha(pu, pv) : 0f;
                    if (a <= 0f) { px[y * n + x] = toneC; continue; }
                    Color c = photo.GetPixelBilinear(pu, pv) * photoBrightness;
                    c = Color.Lerp(c, tone, toneBlend);
                    c.a = 1f;
                    px[y * n + x] = Color.Lerp(tone, c, a);
                }
            }
            built.SetPixels32(px);
            built.Apply(true, false);
        }

        float OvalAlpha(float pu, float pv)
        {
            float dx = (pu - 0.5f) / (0.5f * ovalSize.x), dy = (pv - 0.5f) / (0.5f * ovalSize.y);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - feather, 1f, d));
        }

        // Average of three skin patches (both cheeks and the forehead), taking the middle value of each channel
        // so a stray patch of hair or background doesn't throw it off.
        Color SampleSkin(Texture2D photo)
        {
            if (!photo.isReadable) return maskColour;
            Vector2[] spots = { new Vector2(0.3f, 0.42f), new Vector2(0.7f, 0.42f), new Vector2(0.5f, 0.76f) };
            Color[] means = new Color[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                Color sum = Color.black; int count = 0;
                for (int j = -2; j <= 2; j++)
                    for (int k = -2; k <= 2; k++)
                    {
                        sum += photo.GetPixelBilinear(spots[i].x + j * 0.012f, spots[i].y + k * 0.012f);
                        count++;
                    }
                means[i] = sum / count;
            }
            Color t = new Color(Mid(means[0].r, means[1].r, means[2].r), Mid(means[0].g, means[1].g, means[2].g), Mid(means[0].b, means[1].b, means[2].b), 1f);
            t = Color.Lerp(t * photoBrightness, t, toneBlend);
            t.a = 1f;
            return t;
        }

        static float Mid(float a, float b, float c) => Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));

        // A smooth, expressionless porcelain mask: dark almond eye holes, a hint of nose and a thin mouth line.
        void BuildMask(Color tone)
        {
            Prepare();
            int n = textureSize;
            var px = new Color32[n * n];
            Color dark = new Color(0.05f, 0.04f, 0.05f);
            Color lip = Color.Lerp(tone, new Color(0.45f, 0.2f, 0.22f), 0.6f);
            for (int y = 0; y < n; y++)
            {
                float v = (y + 0.5f) / n;
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;
                    Color c = tone;
                    // soft highlight in the middle of the face; the edges stay exactly the skin colour so the seam can't show
                    float ex = (u - 0.5f) / 0.4f, ey = (v - 0.52f) / 0.45f;
                    c *= Mathf.Lerp(1.06f, 1f, Mathf.Clamp01(ex * ex + ey * ey));
                    // eye holes
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float dx = (u - (0.5f + s * 0.15f)) / 0.085f, dy = (v - 0.6f) / 0.034f;
                        float d = dx * dx + dy * dy;
                        if (d < 1.6f) c = Color.Lerp(c, c * 0.75f, 1f - d / 1.6f);    // socket shadow
                        if (d < 1f) c = Color.Lerp(dark, c, Mathf.SmoothStep(0f, 1f, (d - 0.75f) / 0.25f));
                    }
                    // nose shadow
                    float nx = (u - 0.515f) / 0.025f, ny = (v - 0.48f) / 0.08f;
                    if (nx * nx + ny * ny < 1f) c *= 0.9f;
                    // mouth line
                    float mx = (u - 0.5f) / 0.11f, my = (v - (0.33f + 0.012f * (u - 0.5f) * (u - 0.5f) * 40f)) / 0.007f;
                    if (mx * mx + my * my < 1f) c = Color.Lerp(c, lip, 0.85f);
                    c.a = 1f;
                    px[y * n + x] = c;
                }
            }
            built.SetPixels32(px);
            built.Apply(true, false);
        }

        void Prepare()
        {
            if (built != null && built.width == textureSize) return;
            if (built != null) Destroy(built);
            built = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);
            built.wrapMode = TextureWrapMode.Clamp;
            built.name = "BossFace (memory only)";
        }

        static void SetTexture(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            m.mainTexture = t;
        }

        static void SetColour(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.color = c;
        }
    }
}
