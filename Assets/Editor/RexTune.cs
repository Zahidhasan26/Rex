using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Temporary helper: reads Temp/rex_tune.txt (key=value lines) and applies look settings to the open scene.
public static class RexTune
{
    static Dictionary<string, string> kv;
    static float F(string k, float d) { return kv.TryGetValue(k, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : d; }
    static bool Has(string k) { return kv.ContainsKey(k); }
    static Color C(string k)
    {
        var p = kv[k].Split(',');
        return new Color(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture), 1f);
    }

    [MenuItem("Rex/Tune From File")]
    public static void Tune()
    {
        string log = "";
        try
        {
            kv = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines("Temp/rex_tune.txt"))
            {
                var t = line.Trim(); if (t.Length == 0 || t.StartsWith("#")) continue;
                var i = t.IndexOf('='); if (i < 0) continue;
                kv[t.Substring(0, i).Trim()] = t.Substring(i + 1).Trim();
            }
            if (Has("fog")) RenderSettings.fog = F("fog", 1) > 0.5f;
            if (Has("fogDensity")) RenderSettings.fogDensity = F("fogDensity", 0.001f);
            if (Has("fogColor")) RenderSettings.fogColor = C("fogColor");
            if (Has("ambSky")) RenderSettings.ambientSkyColor = C("ambSky");
            if (Has("ambEq")) RenderSettings.ambientEquatorColor = C("ambEq");
            if (Has("ambGround")) RenderSettings.ambientGroundColor = C("ambGround");
            var sky = RenderSettings.skybox;
            if (sky != null)
            {
                if (Has("skyTint")) sky.SetColor("_Tint", C("skyTint"));
                if (Has("skyExposure")) sky.SetFloat("_Exposure", F("skyExposure", 1));
                if (Has("skyRotation")) sky.SetFloat("_Rotation", F("skyRotation", 0));
                EditorUtility.SetDirty(sky);
            }
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                if (Has("sunIntensity")) sun.intensity = F("sunIntensity", 2);
                if (Has("sunColor")) sun.color = C("sunColor");
                if (Has("sunPitch") || Has("sunYaw"))
                {
                    var e = sun.transform.eulerAngles;
                    sun.transform.rotation = Quaternion.Euler(F("sunPitch", e.x), F("sunYaw", e.y), 0);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(sun);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sun.transform);
            }
            foreach (var v in Object.FindObjectsByType<Volume>())
            {
                if (v.name != "Dusk Volume" || v.sharedProfile == null) continue;
                var p = v.sharedProfile;
                if (p.TryGet<ColorAdjustments>(out var ca))
                {
                    if (Has("postExposure")) ca.postExposure.Override(F("postExposure", 0));
                    if (Has("contrast")) ca.contrast.Override(F("contrast", 0));
                    if (Has("saturation")) ca.saturation.Override(F("saturation", 0));
                    if (Has("colorFilter")) ca.colorFilter.Override(C("colorFilter"));
                }
                if (p.TryGet<Bloom>(out var bl))
                {
                    if (Has("bloomIntensity")) bl.intensity.Override(F("bloomIntensity", 0.3f));
                    if (Has("bloomThreshold")) bl.threshold.Override(F("bloomThreshold", 1f));
                }
                if (p.TryGet<WhiteBalance>(out var wb))
                {
                    if (Has("temperature")) wb.temperature.Override(F("temperature", 0));
                    if (Has("wbTint")) wb.tint.Override(F("wbTint", 0));
                }
                if (Has("smhReset") && p.TryGet<ShadowsMidtonesHighlights>(out var smh))
                {
                    smh.shadows.Override(new Vector4(1, 1, 1, 0));
                    smh.highlights.Override(new Vector4(1, 1, 1, 0));
                }
                EditorUtility.SetDirty(p);
            }
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (Has("save")) { EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); log += "saved\n"; }
            SceneView.RepaintAll();
            File.WriteAllText("Temp/rex_tune_result.txt", "OK\n" + log);
        }
        catch (System.Exception e) { File.WriteAllText("Temp/rex_tune_result.txt", "ERROR\n" + log + e); Debug.LogError(e); }
    }
}
