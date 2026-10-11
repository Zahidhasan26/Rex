using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BowGame.EditorTools
{
    // Builds Assets/_Game/Player/Resources/Mount_Gorgosaurus.prefab (the ride) from the raw Gorgosaurus model.
    // Runs once automatically if the prefab is missing; rebuild any time from Tools > BowGame > Rebuild Mount Prefab.
    // Works in a hidden preview scene, so no open scene is touched or marked as changed.
    [InitializeOnLoad]
    static class MountPrefabBuilder
    {
        const string ModelPath = "Assets/_Game/Dinosaurs/Models/Gorgosaurus/Gorgosaurus.fbx";
        const string ControllerPath = "Assets/_Game/Dinosaurs/Animation/Gorgosaurus.controller";
        const string ParentFolder = "Assets/_Game/Player";
        const string Folder = ParentFolder + "/Resources";
        const string PrefabPath = Folder + "/" + RideMount.ResourceName + ".prefab";

        static MountPrefabBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        static void BuildIfMissing()
        {
            if (!RideMount.FeatureEnabled) return;              // ride is switched off: don't auto-create the prefab
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += BuildIfMissing;   // try again once Unity is idle
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            Build();
        }

        [MenuItem("Tools/BowGame/Rebuild Mount Prefab")]
        static void RebuildFromMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play mode before rebuilding the mount prefab."); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
                !EditorUtility.DisplayDialog("Rebuild mount prefab",
                    "This replaces " + PrefabPath + " and resets its tuned values (seat height, offsets). Continue?",
                    "Rebuild", "Cancel"))
                return;
            Build();
        }

        static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogWarning("MountPrefabBuilder: model not found at " + ModelPath + ". Mount prefab not created.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(ParentFolder, "Resources");

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                instance.name = RideMount.ResourceName;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var animator = instance.GetComponentInChildren<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                if (controller != null) animator.runtimeAnimatorController = controller;
                else Debug.LogWarning("MountPrefabBuilder: " + ControllerPath + " not found. The mount will not animate until you assign a controller.");
                animator.applyRootMotion = false;

                var mount = instance.GetComponent<RideMount>();
                if (mount == null) mount = instance.AddComponent<RideMount>();
                mount.animator = animator;

                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool saved);
                if (saved) Debug.Log("MountPrefabBuilder: created " + PrefabPath + ". Press Play in a scene with an XR Origin (e.g. Main) to ride.");
                else Debug.LogError("MountPrefabBuilder: could not save " + PrefabPath + ".");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
