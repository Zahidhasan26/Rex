using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace BowGame
{
    // Builds every boss asset from Boss.fbx: import settings, materials, animator controller,
    // orb projectile + impact, and the Boss prefab with hitboxes. Safe to run again after changes.
    // Menu: BowGame/Boss/Build Boss Assets
    public static class BossBuilder
    {
        const string Root = "Assets/_Game/Dinosaurs/Boss";
        const string Fbx = Root + "/Models/Boss.fbx";
        const string MatDir = Root + "/Materials";
        const string TexDir = Root + "/Textures";
        const string AnimDir = Root + "/Animation";
        const string PrefabDir = Root + "/Prefabs";
        const string FxMatDir = "Assets/_Game/Dinosaurs/Materials/FX";

        public const float ModelScale = 1.4f;   // the boss towers over the player

        static readonly string[] LoopClips = { "Idle", "Walk", "Run", "StrafeLeft", "StrafeRight" };

        [MenuItem("BowGame/Boss/Build Boss Assets")]
        public static void BuildAll()
        {
            foreach (var d in new[] { MatDir, AnimDir, PrefabDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            SetupTextures();
            var mats = BuildMaterials();
            SetupImporter(mats);
            var controller = BuildController();
            var orb = BuildOrb(mats["Orb"]);
            BuildBossPrefab(controller, orb, mats["Orb"]);
            AssetDatabase.SaveAssets();
            // Re-import the prefab so open scenes pick up the controller straight away.
            AssetDatabase.ImportAsset(PrefabDir + "/Boss.prefab", ImportAssetOptions.ForceUpdate);
            Debug.Log("BossBuilder: boss assets built.");
        }

        // ---------- textures and materials ----------

        static void SetupTextures()
        {
            foreach (var n in new[] { "Boss_Suit_Normal", "Boss_Hands_Normal" })
            {
                var ti = AssetImporter.GetAtPath(TexDir + "/" + n + ".png") as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            }
        }

        static Dictionary<string, Material> BuildMaterials()
        {
            var m = new Dictionary<string, Material>();
            m["Suit"] = Lit("Boss_Suit", Color.white, 0.35f, TexDir + "/Boss_Suit_Albedo.png", TexDir + "/Boss_Suit_Normal.png");
            m["Hands"] = Lit("Boss_Hands", Color.white, 0.3f, TexDir + "/Boss_Hands_Albedo.png", TexDir + "/Boss_Hands_Normal.png");
            m["Skin"] = Lit("Boss_Skin", new Color(0.86f, 0.84f, 0.8f), 0.3f, null, null);
            m["Face"] = Lit("Boss_Face", Color.white, 0.25f, null, null);
            m["Hair"] = Lit("Boss_Hair", new Color(0.07f, 0.055f, 0.05f), 0.45f, null, null);
            var orb = Lit("Boss_Orb", new Color(0.12f, 0.02f, 0.2f), 0.8f, null, null);
            orb.EnableKeyword("_EMISSION");
            orb.SetColor("_EmissionColor", new Color(0.45f, 0.1f, 1f) * 1.6f);
            orb.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m["Orb"] = orb;
            foreach (var x in m.Values) EditorUtility.SetDirty(x);
            return m;
        }

        static Material Lit(string name, Color colour, float smooth, string albedo, string normal)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", colour);
            mat.SetFloat("_Smoothness", smooth);
            if (albedo != null) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
                mat.EnableKeyword("_NORMALMAP");
            }
            return mat;
        }

        // ---------- model import ----------

        static void SetupImporter(Dictionary<string, Material> mats)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.animationCompression = ModelImporterAnimationCompression.KeyframeReductionAndCompression;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.importCameras = false;
            mi.importLights = false;

            var remap = new Dictionary<string, Material>
            {
                { "material_7", mats["Suit"] }, { "material_0", mats["Hands"] },
                { "Boss_Skin", mats["Skin"] }, { "Boss_Face", mats["Face"] }, { "Boss_Hair", mats["Hair"] },
            };
            foreach (var kv in remap) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);

            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                string n = c.takeName;
                int bar = n.LastIndexOf('|');
                if (bar >= 0) n = n.Substring(bar + 1);
                c.name = n;
                c.loopTime = System.Array.IndexOf(LoopClips, n) >= 0;
                c.loopPose = false;
            }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }

        static AnimationClip Clip(string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Fbx))
                if (o is AnimationClip c && c.name == name && !c.name.StartsWith("__preview")) return c;
            Debug.LogError("BossBuilder: clip not found: " + name);
            return null;
        }

        // ---------- animator ----------

        static AnimatorController BuildController()
        {
            // Rebuild the existing controller in place. Deleting and re-creating it would leave every open
            // scene and prefab instance pointing at a dead controller, so the boss would stand still.
            string path = AnimDir + "/Boss.controller";
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ac == null) ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            else ClearController(ac, path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Strafe", AnimatorControllerParameterType.Float);
            ac.AddParameter("Charging", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("ChargeWindup", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Swing", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("DieIndex", AnimatorControllerParameterType.Int);

            var sm = ac.layers[0].stateMachine;

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "Strafe",
                blendParameterY = "Speed",
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, ac);
            tree.AddChild(Clip("Idle"), new Vector2(0f, 0f));
            tree.AddChild(Clip("Walk"), new Vector2(0f, 1.35f));
            tree.AddChild(Clip("Run"), new Vector2(0f, 3.6f));
            tree.AddChild(Clip("StrafeLeft"), new Vector2(-1f, 1.3f));
            tree.AddChild(Clip("StrafeRight"), new Vector2(1f, 1.3f));
            tree.AddChild(Clip("Walk"), new Vector2(0f, -1.35f));   // walking backwards: the walk played in reverse
            var kids = tree.children;
            kids[kids.Length - 1].timeScale = -1f;
            tree.children = kids;
            var loco = sm.AddState("Locomotion", new Vector3(300, 0, 0));
            loco.motion = tree;
            sm.defaultState = loco;

            var thr = State(sm, "Throw", Clip("Throw"), 300, 120);
            Trigger(loco, thr, "Attack", 0.1f);
            Back(thr, loco, 0.92f);

            var hit = State(sm, "Hit", Clip("Hit"), 560, 0);
            Trigger(loco, hit, "Hit", 0.05f);
            Back(hit, loco, 0.9f);

            var wind = State(sm, "ChargeWindup", Clip("ChargeWindup"), 40, 120);
            Trigger(loco, wind, "ChargeWindup", 0.1f);
            var run = State(sm, "ChargeRun", Clip("Run"), 40, 240);
            run.speed = 1.9f;
            var t = wind.AddTransition(run);
            t.hasExitTime = false; t.duration = 0.12f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Charging");
            var stop = run.AddTransition(loco);
            stop.hasExitTime = false; stop.duration = 0.2f;
            stop.AddCondition(AnimatorConditionMode.IfNot, 0, "Charging");

            var smash = State(sm, "Smash", Clip("Swing"), 300, 300);
            var any = sm.AddAnyStateTransition(smash);
            any.hasExitTime = false; any.duration = 0.1f; any.canTransitionToSelf = false;
            any.AddCondition(AnimatorConditionMode.If, 0, "Swing");
            Back(smash, loco, 0.9f);

            string[] deaths = { "Death_Back", "Death_Forward", "Death_Kneel" };
            for (int i = 0; i < deaths.Length; i++)
            {
                var d = State(sm, deaths[i], Clip(deaths[i]), 600, 140 + i * 70);
                var dt = sm.AddAnyStateTransition(d);
                dt.hasExitTime = false; dt.duration = 0.15f; dt.canTransitionToSelf = false;
                dt.AddCondition(AnimatorConditionMode.If, 0, "Die");
                dt.AddCondition(AnimatorConditionMode.Equals, i, "DieIndex");
            }
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static void ClearController(AnimatorController ac, string path)
        {
            foreach (var p in ac.parameters) ac.RemoveParameter(p);
            var sm = ac.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (var t in sm.entryTransitions) sm.RemoveEntryTransition(t);
            foreach (var st in sm.states) sm.RemoveState(st.state);
            foreach (var sub in sm.stateMachines) sm.RemoveStateMachine(sub.stateMachine);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is BlendTree) Object.DestroyImmediate(o, true);
            while (ac.layers.Length > 1) ac.RemoveLayer(ac.layers.Length - 1);
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, Motion m, float x, float y)
        {
            var s = sm.AddState(name, new Vector3(x, y, 0));
            s.motion = m;
            return s;
        }

        static void Trigger(AnimatorState from, AnimatorState to, string trigger, float dur)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false; t.duration = dur;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }

        static void Back(AnimatorState from, AnimatorState to, float exit)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true; t.exitTime = exit; t.duration = 0.2f;
        }

        // ---------- orb projectile ----------

        static SpitProjectile BuildOrb(Material orbMat)
        {
            var mist = AssetDatabase.LoadAssetAtPath<Material>(FxMatDir + "/Spit_Mist.mat");
            var drop = AssetDatabase.LoadAssetAtPath<Material>(FxMatDir + "/Spit_Droplet.mat");
            Color violet = new Color(0.62f, 0.25f, 1f, 0.9f);
            Color dim = new Color(0.35f, 0.08f, 0.6f, 0.45f);
            int projLayer = LayerMask.NameToLayer("EnemyProjectile");

            // impact burst
            var burst = new GameObject("BossOrbBurst");
            var bps = Particles(burst, drop, false);
            var main = bps.main;
            main.duration = 0.6f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5.5f); main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(violet, Color.white); main.gravityModifier = 0.6f;
            main.stopAction = ParticleSystemStopAction.Destroy; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = bps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var sh = bps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.15f;
            FadeOut(bps);
            var flash = new GameObject("Flash"); flash.transform.SetParent(burst.transform, false);
            var fps = Particles(flash, mist, false);
            var fm = fps.main;
            fm.duration = 0.5f; fm.loop = false; fm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f); fm.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            fm.startColor = new ParticleSystem.MinMaxGradient(dim); fm.simulationSpace = ParticleSystemSimulationSpace.World;
            var fe = fps.emission; fe.rateOverTime = 0; fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
            var fsh = fps.shape; fsh.shapeType = ParticleSystemShapeType.Sphere; fsh.radius = 0.1f;
            FadeOut(fps);
            var burstPrefab = PrefabUtility.SaveAsPrefabAsset(burst, PrefabDir + "/BossOrbBurst.prefab");
            Object.DestroyImmediate(burst);

            // the orb
            var go = new GameObject("BossOrb");
            if (projLayer >= 0) go.layer = projLayer;
            var proj = go.AddComponent<SpitProjectile>();
            proj.speed = 15f; proj.gravity = 2.5f; proj.hitRadius = 0.55f; proj.lifeTime = 6f;
            proj.fadeDistance = 0f; proj.impactPrefab = burstPrefab;

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core"; Object.DestroyImmediate(core.GetComponent<Collider>());
            core.transform.SetParent(go.transform, false); core.transform.localScale = Vector3.one * 0.3f;
            core.GetComponent<MeshRenderer>().sharedMaterial = orbMat;
            core.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var trail = new GameObject("Trail"); trail.transform.SetParent(go.transform, false);
            var tps = Particles(trail, mist, true);
            var tm = tps.main;
            tm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); tm.startSpeed = 0f;
            tm.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); tm.startColor = new ParticleSystem.MinMaxGradient(dim);
            tm.simulationSpace = ParticleSystemSimulationSpace.World;
            var te = tps.emission; te.rateOverTime = 10; te.rateOverDistance = 14;
            var tsh = tps.shape; tsh.shapeType = ParticleSystemShapeType.Sphere; tsh.radius = 0.08f;
            Shrink(tps); FadeOut(tps);

            var sparks = new GameObject("Sparks"); sparks.transform.SetParent(go.transform, false);
            var sps = Particles(sparks, drop, true);
            var spm = sps.main;
            spm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); spm.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
            spm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f); spm.startColor = new ParticleSystem.MinMaxGradient(violet, Color.white);
            spm.simulationSpace = ParticleSystemSimulationSpace.World;
            var spe = sps.emission; spe.rateOverTime = 35;
            var ssh = sps.shape; ssh.shapeType = ParticleSystemShapeType.Sphere; ssh.radius = 0.16f;
            FadeOut(sps);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/BossOrb.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<SpitProjectile>();
        }

        static ParticleSystem Particles(GameObject go, Material mat, bool loop)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = loop; main.playOnAwake = true; main.maxParticles = 200;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static void Shrink(ParticleSystem ps)
        {
            var s = ps.sizeOverLifetime; s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        }

        // ---------- boss prefab ----------

        static void BuildBossPrefab(AnimatorController controller, SpitProjectile orb, Material orbMat)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var root = new GameObject("Boss");
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.55f; agent.height = 2.6f; agent.speed = 1.4f; agent.angularSpeed = 200f;
            agent.acceleration = 8f; agent.stoppingDistance = 1f;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * ModelScale;
            var anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            Transform B(string n) => Find(model.transform, n);
            Transform head = B("Head"), hand = B("RightHand");

            // hitboxes: triggers on the Dinosaur layer, like the dinosaurs
            int dinoLayer = LayerMask.NameToLayer("Dinosaur");
            Sphere(head, "Hitbox_Head", head.position + Vector3.up * 0.1f * ModelScale, 0.15f * ModelScale, HitZone.Head, dinoLayer);
            Capsule(B("Hips"), B("Neck"), "Hitbox_Body", 0.24f * ModelScale, HitZone.Body, dinoLayer, B("Spine1"));
            Capsule(B("LeftUpLeg"), B("LeftFoot"), "Hitbox_Leg_L", 0.1f * ModelScale, HitZone.Legs, dinoLayer, null);
            Capsule(B("RightUpLeg"), B("RightFoot"), "Hitbox_Leg_R", 0.1f * ModelScale, HitZone.Legs, dinoLayer, null);

            // orb held in the throwing hand during the wind-up
            var held = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            held.name = "HeldOrb"; Object.DestroyImmediate(held.GetComponent<Collider>());
            held.GetComponent<MeshRenderer>().sharedMaterial = orbMat;
            held.transform.SetParent(hand, true);
            held.transform.position = hand.position + (hand.position - B("RightForeArm").position).normalized * 0.09f * ModelScale;
            held.transform.localScale = Vector3.one * (0.22f / hand.lossyScale.x);

            var boss = root.AddComponent<BossEnemy>();
            boss.animator = anim;
            boss.bodyHitsToKill = 24; boss.headHitsToKill = 6;
            boss.legSlowMultiplier = 0.6f; boss.legSlowDuration = 2.5f;
            boss.moveSpeed = 1.4f; boss.turnSpeed = 140f;
            boss.speedVariance = 0f; boss.flankAngle = new Vector2(5f, 20f);
            boss.weaveAmplitude = 2f; boss.lookAhead = 6f;
            boss.directApproachDistance = 16f; boss.wanderFadeDistance = 8f;
            boss.pauseChancePerSecond = 0.03f; boss.chargeDistance = 0f; boss.chargeSpeedMultiplier = 1f;
            boss.attackRange = 16f; boss.attackCooldown = 3f; boss.recoverTime = 0.7f;
            boss.mouth = head; boss.faceGap = 0.9f;
            boss.sidestep = true; boss.strafeSpeed = 1.3f; boss.strafeRangeFraction = 0.7f;
            boss.corpseTime = 10f; boss.syncAnimationToSpeed = false; boss.deathVariants = 3;
            boss.projectilePrefab = orb; boss.throwHand = hand; boss.heldOrb = held;

            root.AddComponent<BossFace>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Boss.prefab");
            Object.DestroyImmediate(root);
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
            return null;
        }

        static void Sphere(Transform bone, string name, Vector3 worldCentre, float worldRadius, HitZone zone, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(bone, false);
            go.transform.position = worldCentre;
            if (layer >= 0) go.layer = layer;
            var c = go.AddComponent<SphereCollider>();
            c.isTrigger = true;
            c.radius = worldRadius / go.transform.lossyScale.x;
            go.AddComponent<HitZoneMarker>().zone = zone;
        }

        // Capsule from bone a to bone b, parented to `parent` (or a).
        static void Capsule(Transform a, Transform b, string name, float worldRadius, HitZone zone, int layer, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : a, false);
            Vector3 dir = b.position - a.position;
            go.transform.position = (a.position + b.position) * 0.5f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            if (layer >= 0) go.layer = layer;
            var c = go.AddComponent<CapsuleCollider>();
            c.isTrigger = true;
            c.direction = 1;
            float s = go.transform.lossyScale.x;
            c.radius = worldRadius / s;
            c.height = (dir.magnitude + worldRadius * 2f) / s;
            go.AddComponent<HitZoneMarker>().zone = zone;
        }
    }
}
