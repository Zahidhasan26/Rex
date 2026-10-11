using UnityEngine;

namespace BowGame
{
    // Puts a face on the boss's head.
    //
    // The head has three materials: Boss_Face covers the whole front of the head with a straight-on
    // projection (left edge of the picture = one ear, right edge = the other, bottom = chin, top = crown),
    // Boss_Hair is the back of the head above the ears, Boss_Skin is the nape and neck.
    //
    // Where the face comes from, first match wins:
    //   1. a live scan from FaceScanStation (play mode only, kept in memory),
    //   2. presetPhoto: a photo added to the project before the presentation,
    //   3. a generated porcelain mask.
    // The picture fills the whole front. Its edges fade into a hair colour (sampled from the top of the
    // picture) and a skin colour (sampled from the cheeks), and the back of the head and the neck are tinted
    // with those, so there is no visible seam. Nothing is drawn over the face.
    //
    // Works in edit mode too: assign presetPhoto and adjust photoCrop to see the result in the Scene view.
    [ExecuteAlways]
    public class BossFace : MonoBehaviour
    {
        public Renderer headRenderer;
        public string skinMaterial = "Boss_Skin";
        public string faceMaterial = "Boss_Face";
        public string hairMaterial = "Boss_Hair";

        [Header("Pre-taken photo")]
        [Tooltip("A front-on photo of the person, put in Boss/FacePhotos (that folder is kept out of git). Used when there is no live scan.")]
        public Texture2D presetPhoto;
        [Tooltip("Part of the photo that becomes the head (0..1): x,y = lower-left corner, width, height. Frame it ear to ear and chin to the top of the hair, with the eyes about 55% up from the bottom.")]
        public Rect photoCrop = new Rect(0f, 0f, 1f, 1f);
        [Tooltip("Flip the photo left-right (selfies are often mirrored).")]
        public bool mirror = false;

        [Header("Blending")]
        [Tooltip("How far in from the edges of the picture it fades into the hair and skin colours (fraction of the picture).")]
        [Range(0f, 0.3f)] public float edgeFade = 0.1f;
        [Tooltip("Slight darkening so the photo sits in the scene's lighting instead of glowing.")]
        [Range(0.5f, 1.3f)] public float photoBrightness = 0.95f;
        [Tooltip("Height (0..1 up the picture) of the ears: above it the sides fade to hair, below it to skin.")]
        [Range(0f, 1f)] public float earLine = 0.47f;
        public int textureSize = 512;

        [Header("No photo")]
        public Color maskColour = new Color(0.86f, 0.84f, 0.8f);
        public Color maskHairColour = new Color(0.07f, 0.055f, 0.05f);

        Texture2D built;
        MaterialPropertyBlock mpb;
        int skinIndex = -1, faceIndex = -1, hairIndex = -1;
        bool dirty = true;

        public Color SkinTone { get; private set; }
        public Color HairColour { get; private set; }

        void OnEnable()
        {
            FaceScanStore.Changed += MarkDirty;
            dirty = true;
            Apply();
        }

        void OnDisable()
        {
            FaceScanStore.Changed -= MarkDirty;
            if (headRenderer != null) headRenderer.SetPropertyBlock(null);
            for (int i = 0; headRenderer != null && i < headRenderer.sharedMaterials.Length; i++) headRenderer.SetPropertyBlock(null, i);
        }

        void OnDestroy()
        {
            if (built != null) DestroyImmediate(built);
        }

        void OnValidate()
        {
            dirty = true;
#if UNITY_EDITOR
            // Refresh the Scene view preview right after an inspector change.
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Apply(); };
#endif
        }

        void MarkDirty() { dirty = true; Apply(); }

        void Update()
        {
            if (dirty) Apply();
        }

        bool FindSlots()
        {
            if (headRenderer == null)
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && m.name.StartsWith(faceMaterial)) { headRenderer = r; break; }
            if (headRenderer == null) return false;
            var mats = headRenderer.sharedMaterials;
            skinIndex = faceIndex = hairIndex = -1;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (mats[i].name.StartsWith(faceMaterial)) faceIndex = i;
                else if (mats[i].name.StartsWith(skinMaterial)) skinIndex = i;
                else if (mats[i].name.StartsWith(hairMaterial)) hairIndex = i;
            }
            return faceIndex >= 0;
        }

        public void Apply()
        {
            dirty = false;
            if (!FindSlots()) return;

            Texture2D photo = Application.isPlaying && FaceScanStore.Face != null ? FaceScanStore.Face : presetPhoto;
            bool fromScan = photo == FaceScanStore.Face && photo != null;
            if (photo != null && !photo.isReadable)
            {
                Debug.LogWarning("BossFace: '" + photo.name + "' isn't readable. Put it in Boss/FacePhotos (that folder sets Read/Write on import) or tick Read/Write in its import settings.", this);
                photo = null;
            }

            if (photo != null)
            {
                Rect crop = fromScan ? new Rect(0f, 0f, 1f, 1f) : photoCrop;
                bool flip = !fromScan && mirror;
                SkinTone = SampleSkin(photo, crop, flip);
                HairColour = SampleHair(photo, crop, flip);
                BuildFromPhoto(photo, crop, flip);
            }
            else
            {
                SkinTone = maskColour;
                HairColour = maskHairColour;
                BuildMask();
            }

            if (mpb == null) mpb = new MaterialPropertyBlock();
            Set(faceIndex, Color.white, built);
            if (skinIndex >= 0) Set(skinIndex, SkinTone, null);
            if (hairIndex >= 0) Set(hairIndex, HairColour, null);
        }

        void Set(int index, Color c, Texture t)
        {
            headRenderer.GetPropertyBlock(mpb, index);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            if (t != null) { mpb.SetTexture("_BaseMap", t); mpb.SetTexture("_MainTex", t); }
            headRenderer.SetPropertyBlock(mpb, index);
        }

        // ---------- sampling ----------

        static Color Sample(Texture2D photo, Rect crop, bool flip, float u, float v)
        {
            if (flip) u = 1f - u;
            return photo.GetPixelBilinear(crop.x + u * crop.width, crop.y + v * crop.height);
        }

        Color Patch(Texture2D photo, Rect crop, bool flip, float u, float v, float r)
        {
            Color sum = Color.black; int n = 0;
            for (int j = -2; j <= 2; j++)
                for (int k = -2; k <= 2; k++) { sum += Sample(photo, crop, flip, u + j * r, v + k * r); n++; }
            return sum / n;
        }

        // Middle value of both cheeks and the forehead, so a stray patch of hair or shadow doesn't throw it off.
        Color SampleSkin(Texture2D photo, Rect crop, bool flip)
        {
            Color a = Patch(photo, crop, flip, 0.32f, 0.37f, 0.01f);
            Color b = Patch(photo, crop, flip, 0.68f, 0.37f, 0.01f);
            Color c = Patch(photo, crop, flip, 0.5f, 0.68f, 0.01f);
            var t = new Color(Mid(a.r, b.r, c.r), Mid(a.g, b.g, c.g), Mid(a.b, b.b, c.b), 1f) * photoBrightness;
            t.a = 1f;
            return t;
        }

        // Hair colour from the top of the picture (the top of the head).
        Color SampleHair(Texture2D photo, Rect crop, bool flip)
        {
            Color a = Patch(photo, crop, flip, 0.4f, 0.93f, 0.01f);
            Color b = Patch(photo, crop, flip, 0.5f, 0.95f, 0.01f);
            Color c = Patch(photo, crop, flip, 0.6f, 0.93f, 0.01f);
            var t = new Color(Mid(a.r, b.r, c.r), Mid(a.g, b.g, c.g), Mid(a.b, b.b, c.b), 1f) * photoBrightness;
            t.a = 1f;
            return t;
        }

        static float Mid(float a, float b, float c) => Mathf.Max(Mathf.Min(a, b), Mathf.Min(Mathf.Max(a, b), c));

        // ---------- texture building ----------

        // Colour the edges fade into at this point: hair above the ear line and along the top, skin below.
        Color EdgeColour(float v)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(earLine - 0.05f, earLine + 0.05f, v));
            return Color.Lerp(SkinTone, HairColour, k);
        }

        float EdgeAlpha(float u, float v)
        {
            if (edgeFade <= 0f) return 1f;
            float side = Mathf.Min(u, 1f - u) / edgeFade;
            float top = (1f - v) / edgeFade;
            float bottom = v / (edgeFade * 0.5f);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(side, Mathf.Min(top, bottom))));
        }

        void BuildFromPhoto(Texture2D photo, Rect crop, bool flip)
        {
            Prepare();
            int n = textureSize;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                float v = (y + 0.5f) / n;
                Color edge = EdgeColour(v);
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;
                    Color c = Sample(photo, crop, flip, u, v) * photoBrightness;
                    c = Color.Lerp(edge, c, EdgeAlpha(u, v));
                    c.a = 1f;
                    px[y * n + x] = c;
                }
            }
            built.SetPixels32(px);
            built.Apply(true, false);
        }

        // A smooth, expressionless porcelain mask over the whole front, with the hair colour on top.
        void BuildMask()
        {
            Prepare();
            int n = textureSize;
            var px = new Color32[n * n];
            Color dark = new Color(0.05f, 0.04f, 0.05f);
            Color lip = Color.Lerp(maskColour, new Color(0.45f, 0.2f, 0.22f), 0.6f);
            for (int y = 0; y < n; y++)
            {
                float v = (y + 0.5f) / n;
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n;
                    Color c = maskColour;
                    float ex = (u - 0.5f) / 0.32f, ey = (v - 0.52f) / 0.4f;
                    c *= Mathf.Lerp(1.06f, 1f, Mathf.Clamp01(ex * ex + ey * ey));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float dx = (u - (0.5f + s * 0.135f)) / 0.075f, dy = (v - 0.55f) / 0.028f;
                        float d = dx * dx + dy * dy;
                        if (d < 1.6f) c = Color.Lerp(c, c * 0.75f, 1f - d / 1.6f);
                        if (d < 1f) c = Color.Lerp(dark, c, Mathf.SmoothStep(0f, 1f, (d - 0.75f) / 0.25f));
                    }
                    float nx = (u - 0.512f) / 0.022f, ny = (v - 0.46f) / 0.07f;
                    if (nx * nx + ny * ny < 1f) c *= 0.9f;
                    float mx = (u - 0.5f) / 0.1f, my = (v - (0.31f + 0.4f * (u - 0.5f) * (u - 0.5f))) / 0.006f;
                    if (mx * mx + my * my < 1f) c = Color.Lerp(c, lip, 0.85f);
                    // painted hairline: hair colour across the top of the head
                    float hairline = 0.8f + 0.05f * Mathf.Cos((u - 0.5f) * Mathf.PI * 2f);
                    c = Color.Lerp(c, maskHairColour, Mathf.SmoothStep(0f, 1f, (v - hairline) / 0.03f));
                    c = Color.Lerp(EdgeColour(v), c, EdgeAlpha(u, v));
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
            if (built != null) DestroyImmediate(built);
            built = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true);
            built.wrapMode = TextureWrapMode.Clamp;
            built.name = "BossFace (memory only)";
            built.hideFlags = HideFlags.DontSave;
        }
    }
}
