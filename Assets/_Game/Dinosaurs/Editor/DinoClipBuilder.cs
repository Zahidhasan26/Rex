using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BowGame.EditorTools
{
    // Builds real animation clips for the dinosaurs (they ship with only one clip each):
    //   3 deaths (fall left, fall right, collapse forward) and 2 sidesteps (left, right).
    // Poses are made by rotating the bones referenced in ProceduralDinoAnim on top of a base pose taken
    // from the model's own clip, then every bone is keyed, so the clips are complete poses.
    // It also rebuilds each dinosaur's Animator Controller:
    //   Locomotion blend tree on "Strafe" (-1 left, 0 the original clip, +1 right),
    //   AnyState -> Death_n on trigger "Die" + int "DieIndex".
    // Menu: BowGame > Dinosaurs > Build Death and Sidestep Clips. Safe to re-run after tuning numbers here.
    public static class DinoClipBuilder
    {
        const string Root = "Assets/_Game/Dinosaurs/";
        const float Fps = 30f;

        class Config
        {
            public string name, prefab, model, controller;
            public float baseTime;                 // normalized time in the model clip for the base pose
            public bool useIdlePoseLegs;           // Gorgosaurus: second leg from DinoIdlePose
            public string[] frontUpper, frontLower; // optional front legs (L, R)
            public float strafeCycle, legSideSwing, kneeLift, deathDuration, collapseDrop;
        }

        static readonly Config[] Configs =
        {
            new Config { name = "Gorgosaurus", prefab = Root + "Prefabs/Gorgosaurus.prefab", model = Root + "Models/Gorgosaurus/Gorgosaurus.fbx",
                         controller = Root + "Animation/Gorgosaurus.controller", baseTime = 0.5f, useIdlePoseLegs = true,
                         strafeCycle = 1.3f, legSideSwing = 20f, kneeLift = 40f, deathDuration = 2.6f, collapseDrop = 0.62f },
            new Config { name = "Maip", prefab = Root + "Prefabs/Maip.prefab", model = Root + "Models/Maip/Maip_LOD.fbx",
                         controller = Root + "Animation/Maip.controller", baseTime = 0f,
                         frontUpper = new[] { "MAIP_setup:Shoulder_L_bake", "MAIP_setup:Shoulder_R_bake" },
                         frontLower = new[] { "MAIP_setup:Elbow_L_bake", "MAIP_setup:Elbow_R_bake" },
                         strafeCycle = 0.9f, legSideSwing = 22f, kneeLift = 40f, deathDuration = 2.4f, collapseDrop = 0.6f },
        };

        [MenuItem("BowGame/Dinosaurs/Build Death and Sidestep Clips")]
        public static void BuildAll()
        {
            foreach (var c in Configs) Build(c);
            AssetDatabase.SaveAssets();
            Debug.Log("[DinoClipBuilder] Built death and sidestep clips for " + Configs.Length + " dinosaurs.");
        }

        // ---------------------------------------------------------------- per dinosaur

        class Rig
        {
            public GameObject root;
            public Transform animRoot, rootBone, head, neck, jaw, spine, legUL, legUR, legLL, legLR, frontUL, frontUR, frontLL, frontLR;
            public List<Transform> tail = new List<Transform>();
            public Transform[] bones;
            public string[] paths;
            public Quaternion[] baseRot;
            public Vector3[] basePos;
            public SkinnedMeshRenderer[] skins;
            public float height;                  // root bone height above ground in the base pose
            // body frame (changes when the whole body rolls), used for pitch/yaw/roll of the bones
            public Quaternion frame = Quaternion.identity;
        }

        static void Build(Config cfg)
        {
            var root = PrefabUtility.LoadPrefabContents(cfg.prefab);
            try
            {
                var rig = MakeRig(root, cfg);
                string dir = Root + "Animation/Clips";
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root + "Animation", "Clips");

                // Deaths: find how far the body must drop so the lowest point of the mesh ends on the ground.
                float dropSide = GroundDrop(rig, t => DeathSide(rig, cfg, +1f, t, 0f), cfg.deathDuration);
                // Belly flop: the folded legs would otherwise stay the lowest point, so drop by a share of hip height.
                float dropFwd = rig.height * cfg.collapseDrop;

                var deaths = new[]
                {
                    Save(rig, dir + "/" + cfg.name + "_Death_FallLeft.anim",  cfg.deathDuration, false, t => DeathSide(rig, cfg, -1f, t, dropSide)),
                    Save(rig, dir + "/" + cfg.name + "_Death_FallRight.anim", cfg.deathDuration, false, t => DeathSide(rig, cfg, +1f, t, dropSide)),
                    Save(rig, dir + "/" + cfg.name + "_Death_Collapse.anim",  cfg.deathDuration, false, t => DeathForward(rig, cfg, t, dropFwd)),
                };
                var strafeL = Save(rig, dir + "/" + cfg.name + "_StrafeLeft.anim",  cfg.strafeCycle, true, t => Strafe(rig, cfg, -1f, t));
                var strafeR = Save(rig, dir + "/" + cfg.name + "_StrafeRight.anim", cfg.strafeCycle, true, t => Strafe(rig, cfg, +1f, t));

                AnimationClip loop = null;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(cfg.model))
                    if (o is AnimationClip ac && !ac.name.StartsWith("__preview")) loop = ac;

                RebuildController(cfg, loop, strafeL, strafeR, deaths);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Rig MakeRig(GameObject root, Config cfg)
        {
            var rig = new Rig { root = root };
            var animator = root.GetComponentInChildren<Animator>();
            rig.animRoot = animator.transform;
            var all = new List<Transform>(rig.animRoot.GetComponentsInChildren<Transform>(true));
            all.Remove(rig.animRoot);
            rig.bones = all.ToArray();
            rig.paths = new string[rig.bones.Length];
            for (int i = 0; i < rig.bones.Length; i++) rig.paths[i] = AnimationUtility.CalculateTransformPath(rig.bones[i], rig.animRoot);
            rig.skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            var pda = root.GetComponent<ProceduralDinoAnim>();
            rig.head = pda.head; rig.neck = pda.neck; rig.jaw = pda.jaw; rig.spine = pda.spine; rig.tail = new List<Transform>(pda.tail);
            rig.legUL = pda.upperLegL; rig.legUR = pda.upperLegR; rig.legLL = pda.lowerLegL; rig.legLR = pda.lowerLegR;
            foreach (var b in rig.bones) if (b.name.StartsWith("Root_M") || b.name.StartsWith("MAIP_setup:Root_M_bake")) { rig.rootBone = b; break; }
            if (cfg.frontUpper != null)
            {
                rig.frontUL = Find(rig, cfg.frontUpper[0]); rig.frontUR = Find(rig, cfg.frontUpper[1]);
                rig.frontLL = Find(rig, cfg.frontLower[0]); rig.frontLR = Find(rig, cfg.frontLower[1]);
            }

            // Base pose from the model clip (plus the second leg from DinoIdlePose for a planted stance).
            var clip = animator.runtimeAnimatorController.animationClips[0];
            foreach (var c in animator.runtimeAnimatorController.animationClips) if (c.name.EndsWith("_Loop")) { clip = c; break; }
            clip.SampleAnimation(animator.gameObject, cfg.baseTime * clip.length);
            var idle = root.GetComponent<DinoIdlePose>();
            Dictionary<Transform, (Quaternion, Vector3)> legPose = null;
            if (cfg.useIdlePoseLegs && idle != null && idle.secondLegRoot != null)
            {
                var keep = Snapshot(rig);
                clip.SampleAnimation(animator.gameObject, idle.secondLegTime * clip.length);
                legPose = new Dictionary<Transform, (Quaternion, Vector3)>();
                foreach (var b in rig.bones) if (b == idle.secondLegRoot || b.IsChildOf(idle.secondLegRoot)) legPose[b] = (b.localRotation, b.localPosition);
                Restore(rig, keep);
                foreach (var kv in legPose) { kv.Key.localRotation = kv.Value.Item1; kv.Key.localPosition = kv.Value.Item2; }
            }
            rig.baseRot = new Quaternion[rig.bones.Length];
            rig.basePos = new Vector3[rig.bones.Length];
            for (int i = 0; i < rig.bones.Length; i++) { rig.baseRot[i] = rig.bones[i].localRotation; rig.basePos[i] = rig.bones[i].localPosition; }
            rig.height = rig.rootBone != null ? rig.rootBone.position.y - root.transform.position.y : 1f;
            return rig;
        }

        static Transform Find(Rig rig, string prefix) { foreach (var b in rig.bones) if (b.name.StartsWith(prefix)) return b; return null; }

        static (Quaternion[], Vector3[]) Snapshot(Rig rig)
        {
            var r = new Quaternion[rig.bones.Length]; var p = new Vector3[rig.bones.Length];
            for (int i = 0; i < rig.bones.Length; i++) { r[i] = rig.bones[i].localRotation; p[i] = rig.bones[i].localPosition; }
            return (r, p);
        }
        static void Restore(Rig rig, (Quaternion[], Vector3[]) s)
        {
            for (int i = 0; i < rig.bones.Length; i++) { rig.bones[i].localRotation = s.Item1[i]; rig.bones[i].localPosition = s.Item2[i]; }
        }
        static void ResetPose(Rig rig)
        {
            for (int i = 0; i < rig.bones.Length; i++) { rig.bones[i].localRotation = rig.baseRot[i]; rig.bones[i].localPosition = rig.basePos[i]; }
            rig.frame = Quaternion.identity;
        }

        // ---------------------------------------------------------------- pose helpers (dinosaur axes)

        static Vector3 Right(Rig r) => r.frame * r.root.transform.right;
        static Vector3 Up(Rig r) => r.frame * r.root.transform.up;
        static Vector3 Fwd(Rig r) => r.frame * r.root.transform.forward;
        static void Pitch(Rig r, Transform b, float deg) { if (b != null) b.rotation = Quaternion.AngleAxis(deg, Right(r)) * b.rotation; }   // + nose down
        static void Yaw(Rig r, Transform b, float deg) { if (b != null) b.rotation = Quaternion.AngleAxis(deg, Up(r)) * b.rotation; }       // + turn right
        static void Roll(Rig r, Transform b, float deg) { if (b != null) b.rotation = Quaternion.AngleAxis(deg, Fwd(r)) * b.rotation; }      // + foot/limb swings to the right
        // Rotates the whole body around the root bone and updates the body frame for later bone rotations.
        static void BodyRotate(Rig r, Quaternion q)
        {
            if (r.rootBone == null) return;
            r.rootBone.rotation = q * r.rootBone.rotation;
            r.frame = q * r.frame;
        }
        static void BodyMove(Rig r, Vector3 world) { if (r.rootBone != null) r.rootBone.position += world; }

        static float E(float a, float b, float t) { float x = Mathf.Clamp01((t - a) / (b - a)); return x * x * (3f - 2f * x); }
        static float Bump(float a, float b, float t) { float x = Mathf.Clamp01((t - a) / (b - a)); return Mathf.Sin(x * Mathf.PI); }

        // ---------------------------------------------------------------- poses

        // Staggers, legs buckle, rolls onto one side (side = -1 left, +1 right), head hits the ground and bounces, tail flops, last twitch.
        static void DeathSide(Rig r, Config c, float side, float t, float drop)
        {
            ResetPose(r);
            float stagger = E(0f, 0.25f, t) * (1f - E(0.25f, 0.6f, t));
            float buckle = E(0.2f, 0.7f, t);
            float fall = E(0.35f, 1.1f, t);
            float thud = Bump(1.1f, 1.45f, t);
            float twitch = Bump(1.7f, 1.95f, t) + 0.6f * Bump(2.05f, 2.25f, t);

            var w = r.root.transform;
            BodyRotate(r, Quaternion.AngleAxis(-side * 82f * fall, w.forward) * Quaternion.AngleAxis(6f * fall - 4f * stagger, w.right));
            BodyMove(r, Vector3.down * drop * fall + w.right * side * r.height * 0.12f * fall);

            Roll(r, r.spine, side * 6f * stagger);
            Pitch(r, r.neck, -16f * stagger + 26f * fall);
            Yaw(r, r.neck, -side * 10f * fall);
            Pitch(r, r.head, -14f * stagger + 22f * fall - 10f * thud);
            Pitch(r, r.jaw, 22f * stagger + 26f * fall);

            Pitch(r, r.legUL, -18f * buckle + 12f * fall);
            Pitch(r, r.legUR, -10f * buckle + 20f * fall);
            Pitch(r, r.legLL, 42f * buckle - 14f * twitch);
            Pitch(r, r.legLR, 34f * buckle + 8f * twitch);
            Pitch(r, r.frontUL, -20f * buckle); Pitch(r, r.frontUR, -20f * buckle);
            Pitch(r, r.frontLL, 45f * buckle); Pitch(r, r.frontLR, 40f * buckle);

            float whip = E(0.35f, 0.7f, t) * (1f - E(0.7f, 1.3f, t));
            for (int i = 0; i < r.tail.Count; i++)
            {
                float k = (i + 1f) / r.tail.Count;
                Pitch(r, r.tail[i], -6f * fall * k);   // tail points backwards: negative pitch lowers it
                Yaw(r, r.tail[i], side * 16f * whip * k - side * 4f * fall * k);
            }
        }

        // Flinches back, knees fold, drops onto its belly, head slams down and bounces, tail drops, last twitch.
        static void DeathForward(Rig r, Config c, float t, float drop)
        {
            ResetPose(r);
            float flinch = E(0f, 0.2f, t) * (1f - E(0.2f, 0.45f, t));
            float fold = E(0.25f, 0.95f, t);
            float headDown = E(0.6f, 1.1f, t);
            float thud = Bump(1.1f, 1.4f, t);
            float twitch = Bump(1.8f, 2.05f, t);

            var w = r.root.transform;
            BodyRotate(r, Quaternion.AngleAxis(4f * fold - 6f * flinch, w.right) * Quaternion.AngleAxis(9f * fold, w.forward));
            BodyMove(r, Vector3.down * drop * fold);

            Pitch(r, r.legUL, -62f * fold); Pitch(r, r.legUR, -58f * fold);
            Pitch(r, r.legLL, 100f * fold + 8f * twitch); Pitch(r, r.legLR, 96f * fold);
            Pitch(r, r.frontUL, -35f * fold); Pitch(r, r.frontUR, -30f * fold);
            Pitch(r, r.frontLL, 65f * fold); Pitch(r, r.frontLR, 60f * fold);

            Pitch(r, r.spine, -5f * flinch);
            Pitch(r, r.neck, -14f * flinch + 20f * fold + 12f * headDown);
            Pitch(r, r.head, -12f * flinch + 18f * headDown - 9f * thud);
            Yaw(r, r.head, 8f * headDown);
            Pitch(r, r.jaw, 20f * flinch + 24f * headDown);
            for (int i = 0; i < r.tail.Count; i++)
            {
                float k = (i + 1f) / r.tail.Count;
                Pitch(r, r.tail[i], -7f * fold * (0.4f + 0.6f * k));
                Yaw(r, r.tail[i], 6f * Bump(0.3f, 1.0f, t) * k);
            }
        }

        // One full sidestep cycle (side = -1 left, +1 right): legs alternately lift and cross out in the travel
        // direction, body sways and bobs, tail leans against the motion. In place: the NavMeshAgent moves the body.
        static void Strafe(Rig r, Config c, float side, float t)
        {
            ResetPose(r);
            float ph = t / c.strafeCycle * Mathf.PI * 2f;
            var w = r.root.transform;

            BodyRotate(r, Quaternion.AngleAxis(3f * Mathf.Sin(ph), w.forward));
            BodyMove(r, Vector3.down * r.height * 0.015f * (1f - Mathf.Cos(2f * ph)) * 0.5f);

            Leg(r, r.legUL, r.legLL, ph, side, c.legSideSwing, c.kneeLift);
            Leg(r, r.legUR, r.legLR, ph + Mathf.PI, side, c.legSideSwing, c.kneeLift);
            Leg(r, r.frontUL, r.frontLL, ph + Mathf.PI, side, c.legSideSwing * 0.7f, c.kneeLift * 0.8f);   // diagonal pairs
            Leg(r, r.frontUR, r.frontLR, ph, side, c.legSideSwing * 0.7f, c.kneeLift * 0.8f);

            Roll(r, r.spine, -side * 2f * Mathf.Sin(ph));
            Yaw(r, r.neck, -2f * Mathf.Sin(ph));   // keeps the head steady on the player
            for (int i = 0; i < r.tail.Count; i++)
            {
                float k = (i + 1f) / r.tail.Count;
                Yaw(r, r.tail[i], (-side * 5f + 4f * Mathf.Sin(ph)) * k);
            }
        }

        static void Leg(Rig r, Transform upper, Transform lower, float p, float side, float swing, float lift)
        {
            float up = Mathf.Max(0f, Mathf.Cos(p));            // lifted half of the cycle
            Roll(r, upper, side * swing * Mathf.Sin(p));        // foot travels toward the step direction while lifted
            Pitch(r, upper, -10f * up);
            Pitch(r, lower, lift * up);
        }

        // ---------------------------------------------------------------- baking

        static float GroundDrop(Rig r, Action<float> pose, float duration)
        {
            pose(duration);
            float minY = float.MaxValue;
            var mesh = new Mesh();
            foreach (var s in r.skins)
            {
                s.BakeMesh(mesh, false);   // false: vertices come out at world size for these rigs (the Maip has a 0.01 FBX scale under a 7x model)
                var tr = s.transform;
                foreach (var v in mesh.vertices) { float y = (tr.position + tr.rotation * v).y; if (y < minY) minY = y; }
            }
            UnityEngine.Object.DestroyImmediate(mesh);
            ResetPose(r);
            return Mathf.Max(0f, minY - r.root.transform.position.y - 0.03f);
        }

        static AnimationClip Save(Rig r, string path, float length, bool loop, Action<float> pose)
        {
            int frames = Mathf.Max(2, Mathf.RoundToInt(length * Fps)) + 1;
            int n = r.bones.Length;
            var rot = new Quaternion[frames, n];
            var pos = new Vector3[frames, n];
            for (int f = 0; f < frames; f++)
            {
                float t = loop ? (f / (frames - 1f)) * length : Mathf.Min(length, f / Fps);
                pose(t);
                for (int i = 0; i < n; i++) { rot[f, i] = r.bones[i].localRotation; pos[f, i] = r.bones[i].localPosition; }
            }
            ResetPose(r);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.ClearCurves();
            clip.frameRate = Fps;

            for (int i = 0; i < n; i++)
            {
                var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
                bool changes = false;
                Quaternion prev = rot[0, i];
                for (int f = 0; f < frames; f++)
                {
                    var q = rot[f, i];
                    if (Quaternion.Dot(q, prev) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);   // keep the curve continuous
                    prev = q;
                    if (Quaternion.Angle(q, rot[0, i]) > 0.01f) changes = true;
                    float time = loop ? f / (frames - 1f) * length : Mathf.Min(length, f / Fps);
                    cx.AddKey(time, q.x); cy.AddKey(time, q.y); cz.AddKey(time, q.z); cw.AddKey(time, q.w);
                }
                if (!changes) { cx = Flat(rot[0, i].x, length); cy = Flat(rot[0, i].y, length); cz = Flat(rot[0, i].z, length); cw = Flat(rot[0, i].w, length); }
                else { cx = Reduce(cx); cy = Reduce(cy); cz = Reduce(cz); cw = Reduce(cw); }
                Set(clip, r.paths[i], "m_LocalRotation.x", cx); Set(clip, r.paths[i], "m_LocalRotation.y", cy);
                Set(clip, r.paths[i], "m_LocalRotation.z", cz); Set(clip, r.paths[i], "m_LocalRotation.w", cw);

                bool moves = false;
                for (int f = 0; f < frames; f++) if ((pos[f, i] - pos[0, i]).sqrMagnitude > 1e-8f) { moves = true; break; }
                var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
                if (moves)
                    for (int f = 0; f < frames; f++)
                    {
                        float time = loop ? f / (frames - 1f) * length : Mathf.Min(length, f / Fps);
                        px.AddKey(time, pos[f, i].x); py.AddKey(time, pos[f, i].y); pz.AddKey(time, pos[f, i].z);
                    }
                else { px = Flat(pos[0, i].x, length); py = Flat(pos[0, i].y, length); pz = Flat(pos[0, i].z, length); }
                if (moves) { px = Reduce(px); py = Reduce(py); pz = Reduce(pz); }
                Set(clip, r.paths[i], "m_LocalPosition.x", px); Set(clip, r.paths[i], "m_LocalPosition.y", py); Set(clip, r.paths[i], "m_LocalPosition.z", pz);
            }
            clip.EnsureQuaternionContinuity();

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (isNew) AssetDatabase.CreateAsset(clip, path); else EditorUtility.SetDirty(clip);
            return clip;
        }

        // Drops keys that a smooth curve through their neighbours already reproduces (keeps files small).
        static AnimationCurve Reduce(AnimationCurve c, float tolerance = 0.0005f)
        {
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            var keys = new List<Keyframe>(c.keys);
            bool removed = true;
            while (removed && keys.Count > 2)
            {
                removed = false;
                for (int i = 1; i < keys.Count - 1; i++)
                {
                    var trial = new List<Keyframe>(keys);
                    trial.RemoveAt(i);
                    var tc = new AnimationCurve(trial.ToArray());
                    for (int k = 0; k < tc.length; k++) tc.SmoothTangents(k, 0f);
                    if (Mathf.Abs(tc.Evaluate(keys[i].time) - keys[i].value) < tolerance
                        && Mathf.Abs(tc.Evaluate((keys[i - 1].time + keys[i].time) * 0.5f) - c.Evaluate((keys[i - 1].time + keys[i].time) * 0.5f)) < tolerance
                        && Mathf.Abs(tc.Evaluate((keys[i].time + keys[i + 1].time) * 0.5f) - c.Evaluate((keys[i].time + keys[i + 1].time) * 0.5f)) < tolerance)
                    {
                        keys.RemoveAt(i);
                        removed = true;
                        i++;   // skip the neighbour this round to avoid drifting
                    }
                }
            }
            var result = new AnimationCurve(keys.ToArray());
            for (int k = 0; k < result.length; k++) result.SmoothTangents(k, 0f);
            return result;
        }

        static AnimationCurve Flat(float v, float length) => new AnimationCurve(new Keyframe(0f, v), new Keyframe(length, v));
        static void Set(AnimationClip clip, string path, string prop, AnimationCurve c) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), c);

        // ---------------------------------------------------------------- controller

        static void RebuildController(Config cfg, AnimationClip loop, AnimationClip strafeL, AnimationClip strafeR, AnimationClip[] deaths)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(cfg.controller);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (var s in sm.states) sm.RemoveState(s.state);
            foreach (var p in ctrl.parameters) ctrl.RemoveParameter(p);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(cfg.controller))   // old blend trees from a previous build
                if (o is BlendTree) UnityEngine.Object.DestroyImmediate(o, true);

            ctrl.AddParameter("Strafe", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DieIndex", AnimatorControllerParameterType.Int);

            var loco = sm.AddState("Locomotion");
            var tree = new BlendTree { name = "Locomotion", blendParameter = "Strafe", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            tree.AddChild(strafeL, -1f);
            tree.AddChild(loop, 0f);
            tree.AddChild(strafeR, 1f);
            loco.motion = tree;
            sm.defaultState = loco;

            string[] names = { "Death_FallLeft", "Death_FallRight", "Death_Collapse" };
            for (int i = 0; i < deaths.Length; i++)
            {
                var st = sm.AddState(names[i], new Vector3(300f, 120f + 60f * i, 0f));
                st.motion = deaths[i];
                var tr = sm.AddAnyStateTransition(st);
                tr.AddCondition(AnimatorConditionMode.If, 0f, "Die");
                tr.AddCondition(AnimatorConditionMode.Equals, i, "DieIndex");
                tr.hasExitTime = false;
                tr.duration = 0.15f;
                tr.canTransitionToSelf = false;
            }
            EditorUtility.SetDirty(ctrl);
        }
    }
}
