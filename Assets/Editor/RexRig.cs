using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.XR.CoreUtils;

// Temporary helper: makes the player rig measure height from the real floor.
public static class RexRig
{
    [MenuItem("Rex/Set Rig To Floor Height")]
    public static void Run()
    {
        string log = "";
        foreach (var o in Object.FindObjectsByType<XROrigin>())
        {
            o.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
            log += o.name + " -> " + o.RequestedTrackingOriginMode + "\n";
        }
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Temp/rex_rig_result.txt", "OK\n" + log);
    }
}
