using System.Collections.Generic;
using Unity.InferenceEngine;
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
    // 3D face: face tracking (FaceLandmarker) finds 468 points on the face in the picture. They line the picture
    // up on the head automatically (autoFitPhoto) and build a real 3D face mesh shaped like the person's
    // (nose, brows, lips, cheeks, jaw), textured with the picture, sitting on the front of the head.
    //
    // Works in edit mode too: assign presetPhoto to see the result in the Scene view.
    public enum HairStyle { Auto, None, Short, Medium, Long, Curly }

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

        [Header("3D face (face tracking)")]
        [Tooltip("Build a 3D face shaped like the person from face tracking. Off = the picture only.")]
        public bool generate3DFace = true;
        [Tooltip("Face landmark network (Boss/AI/face_landmark.onnx).")]
        public ModelAsset landmarkModel;
        [Tooltip("Line the picture up on the head from the face it finds (ignores photoCrop). Untick to frame it by hand.")]
        public bool autoFitPhoto = true;
        [Tooltip("How much of the person's face depth to add (1 = as measured).")]
        [Range(0f, 2f)] public float faceDepth = 0.85f;
        [Tooltip("How far the face sits off the head surface (metres) so the head never shows through.")]
        [Range(0f, 0.01f)] public float faceLift = 0.0015f;
        [Tooltip("How far in from the edge of the face the depth builds up (fraction of the face width). Higher = softer edges.")]
        [Range(0.02f, 0.5f)] public float edgeSoftness = 0.18f;
        [Tooltip("How far in from the face outline the 3D face's picture fades to plain skin (fraction of the face size). Hides stretched pixels on the sides of the jaw.")]
        [Range(0f, 0.3f)] public float edgeFadeInside = 0.12f;
        [Tooltip("Where things land on the head picture (0..1 up the head): eye line and chin.")]
        public float headEyeLine = 0.55f;
        public float headChin = 0.035f;
        [Tooltip("Face width (cheek to cheek) as a fraction of the head's width.")]
        [Range(0.5f, 1f)] public float faceWidthOnHead = 0.8f;

        [Header("Hair")]
        [Tooltip("3D hair added on top of the head. Auto picks one from the photo (bald, short, medium, long or curly).")]
        public HairStyle hairStyle = HairStyle.Auto;
        [Tooltip("Style used with the mask (no photo) when hairStyle is Auto.")]
        public HairStyle maskHairStyle = HairStyle.Short;
        [Tooltip("The strand texture is mid-grey; this brightens the tint so the hair ends up close to the sampled colour.")]
        [Range(0.5f, 2.5f)] public float hairTintBoost = 1.45f;
        [Tooltip("Auto: how busy the hair texture must be to count as curly. Lower it if curly hair isn't detected, raise it if straight hair is called curly.")]
        [Range(0.01f, 0.2f)] public float curlyBusyness = 0.05f;

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
        public HairStyle ShownHairStyle { get; private set; }
        public FaceLandmarks Landmarks { get; private set; }
        public string FaceStatus { get; private set; }
        public string HairReason { get; private set; }
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
            if (faceMesh != null) DestroyImmediate(faceMesh);
            if (faceMeshMaterial != null) DestroyImmediate(faceMeshMaterial);
            if (faceTexture != null) DestroyImmediate(faceTexture);
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

            Rect usedCrop = photoCrop;
            bool usedFlip = false;
            FaceLandmarks lm = null;
            if (photo != null)
            {
                Rect crop = fromScan ? new Rect(0f, 0f, 1f, 1f) : photoCrop;
                bool flip = !fromScan && mirror;
                if (generate3DFace || autoFitPhoto)
                {
                    lm = GetLandmarks(photo);
                    if (lm.Found && autoFitPhoto) crop = CropFromLandmarks(lm);
                }
                usedCrop = crop; usedFlip = flip;
                SkinTone = SampleSkin(photo, crop, flip);
                // With face tracking, sample the hair just above the top of the forehead (the top of the picture may be background).
                float hairV = 0.94f;
                bool bald = false;
                if (lm != null && lm.Found)
                {
                    // Walk up from the top of the forehead until the colour stops being skin: that's the hairline.
                    float u10 = (lm.points[10].x - crop.x) / crop.width; if (flip) u10 = 1f - u10;
                    float v10 = (lm.points[10].y - crop.y) / crop.height;
                    bald = true;
                    for (float v = v10; v < Mathf.Min(1.1f, v10 + 0.16f); v += 0.01f)
                    {
                        Color c = Patch(photo, crop, flip, u10, v, 0.008f) * photoBrightness;
                        if (ColourDistance(c, SkinTone) > 0.15f) { hairV = v + 0.025f; bald = false; break; }
                    }
                }
                HairColour = bald ? SkinTone : SampleHair(photo, crop, flip, hairV);
                bool hasFace = lm != null && lm.Found;
                BuildFromPhoto(photo, crop, flip, hasFace ? HeadOutline(lm, crop, flip, true) : null, hasFace ? HeadOutline(lm, crop, flip, false) : null);
            }
            else
            {
                SkinTone = maskColour;
                HairColour = maskHairColour;
                BuildMask();
            }
            Landmarks = lm;
            if (photo != null && generate3DFace && lm != null && lm.Found) BuildFaceMesh(photo, lm, usedCrop, usedFlip);
            else
            {
                RemoveFaceMesh();
                FaceStatus = photo == null ? "no photo (mask)" : !generate3DFace ? "3D face off" :
                    landmarkModel == null ? "no landmark model assigned" : "no face found in the picture (flat picture only)";
            }

            if (mpb == null) mpb = new MaterialPropertyBlock();
            Set(faceIndex, Color.white, built);
            if (skinIndex >= 0) Set(skinIndex, SkinTone, null);
            if (hairIndex >= 0) Set(hairIndex, HairColour, null);

            HairStyle style = hairStyle;
            if (style == HairStyle.Auto)
            {
                if (photo != null) style = DetectHair(photo, usedCrop, usedFlip);
                else { style = maskHairStyle; HairReason = "no photo"; }
            }
            else HairReason = "set by hand";
            ShowHair(style);
        }

        // ---------- face tracking and the 3D face ----------

        FaceLandmarks cachedLandmarks;
        int cachedPhotoId;
        GameObject faceObject;
        Mesh faceMesh;
        Material faceMeshMaterial;
        Texture2D faceTexture;

        // Cuts the face out of the photo into its own texture. Returns the photo area (UV) it covers.
        Rect BuildFaceTexture(Texture2D photo, FaceLandmarks lm)
        {
            var ids = FaceMeshTopology.Outline;
            var poly = new Vector2[ids.Length];
            Vector2 lo = new Vector2(1e9f, 1e9f), hi = new Vector2(-1e9f, -1e9f);
            for (int i = 0; i < ids.Length; i++)
            {
                poly[i] = new Vector2(lm.points[ids[i]].x, lm.points[ids[i]].y);
                lo = Vector2.Min(lo, poly[i]); hi = Vector2.Max(hi, poly[i]);
            }
            Vector2 size = hi - lo;
            lo -= size * 0.03f; size *= 1.06f;
            float band = Mathf.Max(size.x, size.y) * edgeFadeInside;   // fade width inside the outline
            Color skin = SkinTone / Mathf.Max(0.01f, photoBrightness); skin.a = 1f;

            const int N = 512;
            if (faceTexture == null || faceTexture.width != N)
            {
                if (faceTexture != null) DestroyImmediate(faceTexture);
                faceTexture = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "GeneratedFace texture (memory only)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            }
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2(lo.x + (x + 0.5f) / N * size.x, lo.y + (y + 0.5f) / N * size.y);
                    bool inside;
                    float d = PolygonDistance(p, poly, out inside);
                    float a = inside ? Mathf.SmoothStep(0f, 1f, d / band) : 0f;
                    Color c = Color.Lerp(skin, photo.GetPixelBilinear(p.x, p.y), a);
                    c.a = 1f;
                    px[y * N + x] = c;
                }
            faceTexture.SetPixels32(px);
            faceTexture.Apply(true, false);
            return new Rect(lo, size);
        }

        FaceLandmarks GetLandmarks(Texture2D photo)
        {
            int id = photo.GetInstanceID() ^ (landmarkModel != null ? landmarkModel.GetInstanceID() : 0);
            if (cachedLandmarks != null && cachedPhotoId == id) return cachedLandmarks;
            cachedLandmarks = FaceLandmarker.Find(landmarkModel, photo);
            cachedPhotoId = id;
            return cachedLandmarks;
        }

        // Frame the picture from the face: cheek to cheek = faceWidthOnHead of the head, eyes on headEyeLine, chin on headChin.
        Rect CropFromLandmarks(FaceLandmarks lm)
        {
            Vector3 eyeL = lm.points[33], eyeR = lm.points[263], chin = lm.points[152];
            Vector3 cheekL = lm.points[234], cheekR = lm.points[454];
            float w = Mathf.Abs(cheekR.x - cheekL.x) / faceWidthOnHead;
            float mid = (cheekL.x + cheekR.x) * 0.5f;
            float eyeV = (eyeL.y + eyeR.y) * 0.5f;
            float h = (eyeV - chin.y) / Mathf.Max(0.05f, headEyeLine - headChin);
            return new Rect(mid - w * 0.5f, chin.y - headChin * h, w, h);
        }

        Transform FindBone(string name)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        void RemoveFaceMesh()
        {
            if (faceObject != null) DestroyImmediate(faceObject);
            faceObject = null;
            var stale = new List<GameObject>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "GeneratedFace") stale.Add(t.gameObject);
            foreach (var g in stale) DestroyImmediate(g);
        }

        // Builds the person's face as a mesh on the front of the head. Each landmark is placed on the head where
        // the head picture shows that same spot of the photo, then pushed forward by how much further forward that
        // spot is on the person's face than on the plain head. Its edge sits on the head, so the two join up.
        void BuildFaceMesh(Texture2D photo, FaceLandmarks lm, Rect crop, bool flip)
        {
            var smr = headRenderer as SkinnedMeshRenderer;
            Transform headBone = FindBone("Head");
            if (smr == null || headBone == null) { FaceStatus = "head bone or head mesh not found"; return; }

            // Head surface as it is right now, in world space.
            var baked = new Mesh();
            smr.BakeMesh(baked);
            Matrix4x4 a = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            Matrix4x4 b = smr.transform.localToWorldMatrix;
            var verts = baked.vertices;
            Vector3 ca = Vector3.zero, cb = Vector3.zero;
            foreach (var v in verts) { ca += a.MultiplyPoint3x4(v); cb += b.MultiplyPoint3x4(v); }
            ca /= Mathf.Max(1, verts.Length); cb /= Mathf.Max(1, verts.Length);
            Matrix4x4 toWorld = (ca - smr.bounds.center).sqrMagnitude <= (cb - smr.bounds.center).sqrMagnitude ? a : b;
            for (int i = 0; i < verts.Length; i++) verts[i] = toWorld.MultiplyPoint3x4(verts[i]);
            var uvs = baked.uv;
            var tris = baked.GetTriangles(faceIndex);
            var allTris = baked.triangles;
            DestroyImmediate(baked);
            if (tris.Length == 0 || uvs.Length != verts.Length) { FaceStatus = "head face area not found"; return; }

            // Which way the face looks.
            Vector3 fwd = Vector3.zero;
            for (int t = 0; t < tris.Length; t += 3)
                fwd += Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]);
            if (Vector3.Dot(fwd, transform.forward) < 0f) fwd = -fwd;
            fwd = fwd.sqrMagnitude > 1e-12f ? fwd.normalized : transform.forward;

            // The head picture is a straight-on projection: fit where each picture position lands on a plane facing
            // forward (plane point = O + Eu*u + Ev*v), from the face-area vertices.
            double[,] M = new double[3, 3]; Vector3[] B = new Vector3[3];
            var used = new HashSet<int>(tris);
            foreach (int vi in used)
            {
                Vector3 q = verts[vi] - fwd * Vector3.Dot(verts[vi], fwd);
                float[] x = { 1f, uvs[vi].x, uvs[vi].y };
                for (int r = 0; r < 3; r++) { for (int c = 0; c < 3; c++) M[r, c] += x[r] * x[c]; B[r] += q * x[r]; }
            }
            Vector3[] sol = Solve3(M, B);
            Vector3 O = sol[0], Eu = sol[1], Ev = sol[2];
            float front = float.MinValue;
            foreach (var v in verts) front = Mathf.Max(front, Vector3.Dot(v, fwd));

            int n = FaceMeshTopology.VertexCount;
            var surface = new Vector3[n];
            var found = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float hu = (lm.points[i].x - crop.x) / crop.width;
                float hv = (lm.points[i].y - crop.y) / crop.height;
                if (flip) hu = 1f - hu;
                Vector3 onPlane = O + Eu * hu + Ev * hv;
                Vector3 origin = onPlane + fwd * (front + 1f);
                found[i] = RaycastMesh(origin, -fwd, verts, allTris, out surface[i]);
            }
            // Points that miss the head (rare: past its edge) sit at the outline's average depth.
            float outlineDepth = 0f; int outlineHits = 0;
            foreach (int k in FaceMeshTopology.Outline) if (found[k]) { outlineDepth += Vector3.Dot(surface[k], fwd); outlineHits++; }
            outlineDepth = outlineHits > 0 ? outlineDepth / outlineHits : front;
            for (int i = 0; i < n; i++)
                if (!found[i])
                {
                    float hu = (lm.points[i].x - crop.x) / crop.width, hv = (lm.points[i].y - crop.y) / crop.height;
                    if (flip) hu = 1f - hu;
                    surface[i] = O + Eu * hu + Ev * hv + fwd * outlineDepth;
                }

            // Measured depth (person) vs depth of the plain head, both relative to the face outline.
            float lmOutline = 0f, headOutline = 0f; Vector3 outlineCentre = Vector3.zero;
            foreach (int k in FaceMeshTopology.Outline) { lmOutline += lm.points[k].z; outlineCentre += surface[k]; }
            lmOutline /= FaceMeshTopology.Outline.Length; outlineCentre /= FaceMeshTopology.Outline.Length;
            foreach (int k in FaceMeshTopology.Outline) headOutline += Vector3.Dot(surface[k] - outlineCentre, fwd);
            headOutline /= FaceMeshTopology.Outline.Length;
            float metresPerUnit = Eu.magnitude / Mathf.Max(1e-4f, crop.width);
            float maxLift = Ev.magnitude * 0.3f;

            var outline = new HashSet<int>(FaceMeshTopology.Outline);
            // Ease the extra depth in from the edge of the face, so the sides slope smoothly into the head
            // instead of forming a step.
            var outlinePoly = new Vector2[FaceMeshTopology.Outline.Length];
            for (int k = 0; k < outlinePoly.Length; k++) { var p = lm.points[FaceMeshTopology.Outline[k]]; outlinePoly[k] = new Vector2(p.x, p.y); }
            float faceSize = Mathf.Abs(lm.points[454].x - lm.points[234].x);
            var pos = new Vector3[n];
            var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float person = (lm.points[i].z - lmOutline) * metresPerUnit;
                float head = Vector3.Dot(surface[i] - outlineCentre, fwd) - headOutline;
                bool inOutline;
                float edgeDist = PolygonDistance(new Vector2(lm.points[i].x, lm.points[i].y), outlinePoly, out inOutline);
                float ease = outline.Contains(i) ? 0f : Mathf.SmoothStep(0f, 1f, edgeDist / Mathf.Max(1e-4f, faceSize * edgeSoftness));
                float lift = faceLift * (outline.Contains(i) ? 0.5f : 1f) + ease * Mathf.Clamp((person - head) * faceDepth, 0f, maxLift);
                pos[i] = headBone.InverseTransformPoint(surface[i] + fwd * lift);
                uv[i] = new Vector2(lm.points[i].x, lm.points[i].y);
            }
            // The outermost ring of points sits right on the edge of the face in the photo, where it can pick up
            // background or hair: sample those a little further in so the edge of the 3D face stays skin-coloured.
            // Its own texture: the face cut out of the photo, fading to the skin colour toward the outline,
            // so the edge of the 3D face never shows ears, hair or background.
            Rect box = BuildFaceTexture(photo, lm);
            for (int i = 0; i < n; i++) uv[i] = new Vector2((uv[i].x - box.x) / box.width, (uv[i].y - box.y) / box.height);

            if (faceMesh == null) { faceMesh = new Mesh { name = "GeneratedFace (memory only)", hideFlags = HideFlags.DontSave }; }
            faceMesh.Clear();
            faceMesh.vertices = pos;
            faceMesh.uv = uv;
            var tri = (int[])FaceMeshTopology.Triangles.Clone();
            faceMesh.triangles = tri;
            faceMesh.RecalculateNormals();
            // Make sure the triangles face outward (nose tip normal should point forward).
            Vector3 noseNormal = headBone.TransformDirection(faceMesh.normals[1]);
            if (Vector3.Dot(noseNormal, fwd) < 0f)
            {
                for (int t = 0; t < tri.Length; t += 3) { int s = tri[t]; tri[t] = tri[t + 1]; tri[t + 1] = s; }
                faceMesh.triangles = tri;
                faceMesh.RecalculateNormals();
            }
            faceMesh.RecalculateBounds();

            if (faceObject == null)
            {
                RemoveFaceMesh();
                faceObject = new GameObject("GeneratedFace");
                faceObject.hideFlags = HideFlags.DontSave;
                faceObject.transform.SetParent(headBone, false);
                faceObject.AddComponent<MeshFilter>();
                var mr = faceObject.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            faceObject.transform.localPosition = Vector3.zero;
            faceObject.transform.localRotation = Quaternion.identity;
            faceObject.transform.localScale = Vector3.one;
            faceObject.GetComponent<MeshFilter>().sharedMesh = faceMesh;

            var src = headRenderer.sharedMaterials[faceIndex];
            if (faceMeshMaterial == null || faceMeshMaterial.shader != src.shader)
            {
                faceMeshMaterial = new Material(src) { name = "GeneratedFace (memory only)", hideFlags = HideFlags.DontSave };
            }
            faceMeshMaterial.SetTexture("_BaseMap", faceTexture);
            faceMeshMaterial.mainTexture = faceTexture;
            Color tint = new Color(photoBrightness, photoBrightness, photoBrightness, 1f);
            if (faceMeshMaterial.HasProperty("_BaseColor")) faceMeshMaterial.SetColor("_BaseColor", tint);
            faceObject.GetComponent<MeshRenderer>().sharedMaterial = faceMeshMaterial;

            int missing = 0; foreach (bool f in found) if (!f) missing++;
            FaceStatus = string.Format("3D face built from {0} points (face score {1:0.00}){2}", n, lm.score,
                missing > 0 ? ", " + missing + " points off the head's face area" : "");
        }

        static Vector3[] Solve3(double[,] m, Vector3[] b)
        {
            double det = m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0]) + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
            var inv = new double[3, 3];
            if (System.Math.Abs(det) < 1e-12) return new[] { Vector3.zero, Vector3.right, Vector3.up };
            inv[0, 0] = (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) / det; inv[0, 1] = (m[0, 2] * m[2, 1] - m[0, 1] * m[2, 2]) / det; inv[0, 2] = (m[0, 1] * m[1, 2] - m[0, 2] * m[1, 1]) / det;
            inv[1, 0] = (m[1, 2] * m[2, 0] - m[1, 0] * m[2, 2]) / det; inv[1, 1] = (m[0, 0] * m[2, 2] - m[0, 2] * m[2, 0]) / det; inv[1, 2] = (m[0, 2] * m[1, 0] - m[0, 0] * m[1, 2]) / det;
            inv[2, 0] = (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]) / det; inv[2, 1] = (m[0, 1] * m[2, 0] - m[0, 0] * m[2, 1]) / det; inv[2, 2] = (m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0]) / det;
            var r = new Vector3[3];
            for (int i = 0; i < 3; i++) r[i] = b[0] * (float)inv[i, 0] + b[1] * (float)inv[i, 1] + b[2] * (float)inv[i, 2];
            return r;
        }

        // Nearest hit of a ray on a triangle mesh (Moller-Trumbore).
        static bool RaycastMesh(Vector3 origin, Vector3 dir, Vector3[] verts, int[] tris, out Vector3 hit)
        {
            float best = float.MaxValue; hit = origin;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = verts[tris[t]], e1 = verts[tris[t + 1]] - a, e2 = verts[tris[t + 2]] - a;
                Vector3 p = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-10f) continue;
                float inv = 1f / det;
                Vector3 s = origin - a;
                float u = Vector3.Dot(s, p) * inv; if (u < 0f || u > 1f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(dir, q) * inv; if (v < 0f || u + v > 1f) continue;
                float d = Vector3.Dot(e2, q) * inv;
                if (d > 0f && d < best) best = d;
            }
            if (best == float.MaxValue) return false;
            hit = origin + dir * best;
            return true;
        }

        // Point on the head's face area that shows picture position uv (the face area is a straight-on projection,
        // so this is a 2D lookup in UV space). Falls back to the nearest vertex.
        static bool SurfaceAt(Vector2 p, Vector3[] verts, Vector2[] uvs, int[] tris, out Vector3 point)
        {
            float best = float.MaxValue; point = Vector3.zero;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector2 a = uvs[tris[t]], b = uvs[tris[t + 1]], c = uvs[tris[t + 2]];
                Vector2 v0 = b - a, v1 = c - a, v2 = p - a;
                float d = v0.x * v1.y - v1.x * v0.y;
                if (Mathf.Abs(d) < 1e-12f) continue;
                float w1 = (v2.x * v1.y - v1.x * v2.y) / d;
                float w2 = (v0.x * v2.y - v2.x * v0.y) / d;
                float w0 = 1f - w1 - w2;
                if (w0 >= -1e-4f && w1 >= -1e-4f && w2 >= -1e-4f)
                {
                    point = verts[tris[t]] * w0 + verts[tris[t + 1]] * w1 + verts[tris[t + 2]] * w2;
                    return true;
                }
                float dist = Mathf.Min((p - a).sqrMagnitude, Mathf.Min((p - b).sqrMagnitude, (p - c).sqrMagnitude));
                if (dist < best)
                {
                    best = dist;
                    point = (p - a).sqrMagnitude == dist ? verts[tris[t]] : (p - b).sqrMagnitude == dist ? verts[tris[t + 1]] : verts[tris[t + 2]];
                }
            }
            return false;
        }

        // ---------- 3D hair ----------

        void ShowHair(HairStyle style)
        {
            ShownHairStyle = style;
            Color tint = HairColour * hairTintBoost;
            tint.a = 1f;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!r.name.StartsWith("Hair_")) continue;
                bool on = r.name == "Hair_" + style;
                if (r.enabled != on) r.enabled = on;
                if (!on) continue;
                if (mpb == null) mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", tint);
                mpb.SetColor("_Color", tint);
                r.SetPropertyBlock(mpb);
            }
        }

        // Rough look at the photo to pick a hair style. Hair pixels are ones closer to the sampled hair colour
        // than to the skin colour. Always check the result and set hairStyle by hand if it guessed wrong.
        HairStyle DetectHair(Texture2D photo, Rect crop, bool flip)
        {
            float hairSkin = ColourDistance(HairColour, SkinTone);
            if (hairSkin < 0.12f) { HairReason = "top of the head looks like skin"; return HairStyle.None; }

            float longFrac = (HairFraction(photo, crop, flip, 0.03f, 0.15f, 0.06f, 0.36f) + HairFraction(photo, crop, flip, 0.85f, 0.97f, 0.06f, 0.36f)) * 0.5f;
            float templeFrac = (HairFraction(photo, crop, flip, 0.04f, 0.17f, 0.5f, 0.72f) + HairFraction(photo, crop, flip, 0.83f, 0.96f, 0.5f, 0.72f)) * 0.5f;
            float busy, evenness;
            HairTexture(photo, crop, flip, out busy, out evenness);

            if (busy > curlyBusyness && evenness > 0.65f) { HairReason = string.Format("curly-looking hair (busy {0:0.000}, even {1:0.00})", busy, evenness); return HairStyle.Curly; }
            if (longFrac > 0.45f) { HairReason = string.Format("hair below the ears ({0:0.00})", longFrac); return HairStyle.Long; }
            if (templeFrac > 0.4f) { HairReason = string.Format("hair at the temples ({0:0.00})", templeFrac); return HairStyle.Medium; }
            HairReason = string.Format("little hair at the sides ({0:0.00})", templeFrac);
            return HairStyle.Short;
        }

        static float ColourDistance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        float HairFraction(Texture2D photo, Rect crop, bool flip, float u0, float u1, float v0, float v1)
        {
            int hits = 0, n = 0;
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 10; j++)
                {
                    Color c = Sample(photo, crop, flip, Mathf.Lerp(u0, u1, (i + 0.5f) / 8f), Mathf.Lerp(v0, v1, (j + 0.5f) / 10f)) * photoBrightness;
                    float dh = ColourDistance(c, HairColour), ds = ColourDistance(c, SkinTone);
                    if (dh < 0.6f * ds && dh < 0.3f) hits++;
                    n++;
                }
            return (float)hits / n;
        }

        // How "busy" the hair at the top of the head is. Curls change brightness in every direction; straight hair
        // mostly changes across the strands. Returns the average change and how even it is across directions (0..1).
        void HairTexture(Texture2D photo, Rect crop, bool flip, out float busy, out float evenness)
        {
            float step = 2f / Mathf.Max(64f, photo.width * crop.width);
            float h = 0f, v = 0f; int n = 0;
            for (int i = 0; i < 16; i++)
                for (int j = 0; j < 6; j++)
                {
                    float u = Mathf.Lerp(0.3f, 0.7f, (i + 0.5f) / 16f), w = Mathf.Lerp(0.86f, 0.97f, (j + 0.5f) / 6f);
                    float a = Sample(photo, crop, flip, u, w).grayscale;
                    h += Mathf.Abs(a - Sample(photo, crop, flip, u + step, w).grayscale);
                    v += Mathf.Abs(a - Sample(photo, crop, flip, u, w + step).grayscale);
                    n++;
                }
            h /= n; v /= n;
            busy = (h + v) * 0.5f;
            evenness = h + v > 1e-5f ? 1f - Mathf.Abs(h - v) / (h + v) : 0f;
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
        Color SampleHair(Texture2D photo, Rect crop, bool flip, float topV = 0.94f)
        {
            Color a = Patch(photo, crop, flip, 0.4f, topV - 0.01f, 0.01f);
            Color b = Patch(photo, crop, flip, 0.5f, topV, 0.01f);
            Color c = Patch(photo, crop, flip, 0.6f, topV - 0.01f, 0.01f);
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

        // Outline (in head-picture UV) of the part of the photo worth keeping on the head: the face, plus a column
        // above it up to the crown for the forehead and hair. Everything outside (background, ears, shoulders)
        // is replaced by the skin and hair colours.
        Vector2[] HeadOutline(FaceLandmarks lm, Rect crop, bool flip, bool extendUp)
        {
            var ids = FaceMeshTopology.Outline;
            var pts = new Vector2[ids.Length];
            float eyeV = ((lm.points[33].y + lm.points[263].y) * 0.5f - crop.y) / crop.height;
            float midU = 0f;
            for (int i = 0; i < ids.Length; i++)
            {
                Vector3 p = lm.points[ids[i]];
                float u = (p.x - crop.x) / crop.width, v = (p.y - crop.y) / crop.height;
                if (flip) u = 1f - u;
                pts[i] = new Vector2(u, v);
                midU += u;
            }
            midU /= ids.Length;
            if (!extendUp) return pts;
            for (int i = 0; i < pts.Length; i++)
                if (pts[i].y > eyeV)   // upper half: stretch up to the crown, a little wider for the hair
                {
                    float k = Mathf.InverseLerp(eyeV, eyeV + 0.15f, pts[i].y);
                    pts[i] = new Vector2(midU + (pts[i].x - midU) * (1f + 0.08f * k), Mathf.Lerp(pts[i].y, 1.05f, k));
                }
            return pts;
        }

        static float PolygonDistance(Vector2 p, Vector2[] poly, out bool inside)
        {
            inside = false;
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                if (((b.y > p.y) != (a.y > p.y)) && (p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y) + b.x)) inside = !inside;
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
            }
            return Mathf.Sqrt(best);
        }

        // keep: face plus the column above it (forehead and hair). face: just the face outline.
        // Outside the face, photo pixels that look like neither skin nor hair (background, shirt) are dropped too.
        void BuildFromPhoto(Texture2D photo, Rect crop, bool flip, Vector2[] keep, Vector2[] face)
        {
            float faceEyeV = 0.55f;
            if (face != null) { float lo = 1f, hi = 0f; foreach (var p in face) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); } faceEyeV = Mathf.Lerp(lo, hi, 0.55f); }
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
                    float a = EdgeAlpha(u, v);
                    Color c = edge;
                    if (a > 0f)
                    {
                        c = Sample(photo, crop, flip, u, v) * photoBrightness;
                        if (keep != null)
                        {
                            bool inside;
                            float d = PolygonDistance(new Vector2(u, v), keep, out inside);
                            a *= inside ? 1f : 1f - Mathf.SmoothStep(0f, 1f, d / 0.035f);
                            bool inFace;
                            float dFace = PolygonDistance(new Vector2(u, v), face, out inFace);
                            if (!inFace && v < faceEyeV)
                            {
                                // Beside and below the face (ears, background, neck): skin and hair colours only.
                                a *= 1f - Mathf.SmoothStep(0f, 1f, dFace / 0.015f);
                            }
                            else if (!inFace)
                            {
                                float near = Mathf.Min(ColourDistance(c, SkinTone), ColourDistance(c, HairColour));
                                a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.25f, near));
                            }
                        }
                    }
                    c = Color.Lerp(edge, c, a);
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
