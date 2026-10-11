using UnityEditor;
using UnityEngine;

namespace BowGame.EditorTools
{
    // One-time helper: puts Zach's bow sounds on the Player prefab's Bow (release = shoot.ogg,
    // flight = arrow_whoosh.wav). It only fills empty slots, so nothing set by hand is replaced.
    // Once both sounds are wired it deletes itself. Everything is saved by Unity, not edited as text.
    [InitializeOnLoad]
    static class BowSoundWiring
    {
        const string PlayerPrefab = "Assets/_Game/Player/Prefabs/Player.prefab";
        const string ReleasePath = "Assets/_Game/Dinosaurs/Audio/Bow/shoot.ogg";
        const string FlightPath = "Assets/_Game/Dinosaurs/Audio/Bow/arrow_whoosh.wav";

        static BowSoundWiring()
        {
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            var release = AssetDatabase.LoadAssetAtPath<AudioClip>(ReleasePath);
            var flight = AssetDatabase.LoadAssetAtPath<AudioClip>(FlightPath);   // arrives with Zach's sound branch
            if (release == null && flight == null) return;

            bool changed = false;
            bool done = false;
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var bow = root.GetComponentInChildren<Bow>(true);
                if (bow == null)
                {
                    Debug.LogWarning("BowSoundWiring: no Bow found in " + PlayerPrefab + ".");
                    return;
                }
                if (bow.audioSource == null)
                {
                    bow.audioSource = bow.gameObject.AddComponent<AudioSource>();
                    bow.audioSource.playOnAwake = false;
                    bow.audioSource.spatialBlend = 1f;
                    changed = true;
                }
                if (release != null && bow.releaseClip == null) { bow.releaseClip = release; changed = true; }
                if (flight != null && bow.flightClip == null) { bow.flightClip = flight; changed = true; }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
                done = bow.releaseClip != null && bow.flightClip != null;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (changed) Debug.Log("BowSoundWiring: bow sounds added to the Player prefab.");
            if (!done) return;

            EditorApplication.delayCall += () =>
            {
                foreach (var guid in AssetDatabase.FindAssets("BowSoundWiring t:MonoScript"))
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            };
        }
    }
}
