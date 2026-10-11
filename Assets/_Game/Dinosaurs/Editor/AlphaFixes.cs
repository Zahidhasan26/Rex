#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BowGame.EditorTools
{
    // One-time fix for the alpha Maip after the headset test. Runs once, then deletes itself.
    //  1. fragileAfterBites = 2 (it can't die until it has bitten the player twice; then one arrow kills it)
    //  2. a whole-body trigger hitbox on the root, so arrows always register on it
    //  3. lowers the model if it stands higher above the ground than the regular Maip does
    [InitializeOnLoad]
    static class AlphaFixes
    {
        const string SelfPath = "Assets/_Game/Dinosaurs/Editor/AlphaFixes.cs";
        const string AlphaPath = "Assets/_Game/Dinosaurs/Prefabs/Maip_Alpha.prefab";
        const string LightPath = "Assets/_Game/Dinosaurs/Prefabs/Maip.prefab";

        static AlphaFixes() { EditorApplication.delayCall += TryRun; }

        static void TryRun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += TryRun; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= OnPlayMode;
                EditorApplication.playModeStateChanged += OnPlayMode;
                return;
            }
            Run();
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.delayCall += TryRun;
        }

        static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AlphaPath) == null) { Debug.LogError("AlphaFixes: alpha prefab not found."); return; }

            float lightMin = float.NaN;
            var light = PrefabUtility.LoadPrefabContents(LightPath);
            try { lightMin = MeshMinY(light); } finally { PrefabUtility.UnloadPrefabContents(light); }

            var root = PrefabUtility.LoadPrefabContents(AlphaPath);
            try
            {
                // 1. bite rule
                var dino = root.GetComponent<Dinosaur>();
                if (dino != null)
                {
                    var so = new SerializedObject(dino);
                    var p = so.FindProperty("fragileAfterBites");
                    if (p != null) { p.intValue = 2; so.ApplyModifiedPropertiesWithoutUndo(); }
                    else Debug.LogWarning("AlphaFixes: fragileAfterBites field not found (scripts not compiled?).");
                }

                // 3. float fix (before the hitbox, so the box is sized to the final model position)
                float alphaMin = MeshMinY(root);
                Transform model = ModelChild(root);
                float diff = alphaMin - lightMin;
                if (model != null && !float.IsNaN(diff) && diff > 0.02f && diff < 1.5f)
                {
                    model.localPosition -= new Vector3(0f, diff, 0f);
                    Debug.Log($"AlphaFixes: lowered the alpha model by {diff:0.000} m to match the regular Maip.");
                }
                else Debug.Log($"AlphaFixes: height left as is (alpha feet {alphaMin:0.000}, Maip feet {lightMin:0.000}).");

                // 2. fallback hitbox
                var old = root.transform.Find("Hitbox_Fallback");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                Bounds b = RendererBounds(root);
                if (b.size.sqrMagnitude > 0.01f)
                {
                    var go = new GameObject("Hitbox_Fallback");
                    int layer = LayerMask.NameToLayer("Dinosaur");
                    go.layer = layer >= 0 ? layer : root.layer;
                    go.transform.SetParent(root.transform, false);
                    var box = go.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    Vector3 size = b.size * 0.85f;   // a little smaller than the mesh so near misses don't count
                    box.center = root.transform.InverseTransformPoint(b.center);
                    box.size = new Vector3(size.x, b.size.y * 0.9f, size.z);
                    go.AddComponent<HitZoneMarker>().zone = HitZone.Body;
                }

                PrefabUtility.SaveAsPrefabAsset(root, AlphaPath);
                Debug.Log("AlphaFixes: alpha updated (2 bites then one-arrow kill, fallback hitbox).");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            AssetDatabase.DeleteAsset(SelfPath);
        }

        // Child of the root that holds the visible model.
        static Transform ModelChild(GameObject root)
        {
            var r = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (r == null) return null;
            Transform t = r.transform;
            while (t.parent != null && t.parent != root.transform) t = t.parent;
            return t.parent == root.transform ? t : null;
        }

        // Lowest vertex of the model in the prefab's local space (root sits at the origin).
        static float MeshMinY(GameObject root)
        {
            float min = float.PositiveInfinity;
            var tmp = new Mesh();
            foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.sharedMesh == null) continue;
                s.BakeMesh(tmp, true);
                var m = Matrix4x4.TRS(s.transform.position, s.transform.rotation, Vector3.one);
                foreach (var v in tmp.vertices) min = Mathf.Min(min, m.MultiplyPoint3x4(v).y);
            }
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || f.GetComponent<Collider>() != null) continue;
                foreach (var v in f.sharedMesh.vertices) min = Mathf.Min(min, f.transform.TransformPoint(v).y);
            }
            Object.DestroyImmediate(tmp);
            return float.IsInfinity(min) ? float.NaN : min - root.transform.position.y;
        }

        static Bounds RendererBounds(GameObject root)
        {
            bool any = false; Bounds b = default;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }
}
#endif
