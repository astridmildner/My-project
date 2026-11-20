using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Meny: Tools > Skyddsrum > Uppgifter > ...
/// Skapar uppgiften "Fyll baljorna" och synpåverkan (Volume med efterbehandling).
/// </summary>
public static class UppgiftsBuilder
{
    const string Folder = "Assets/Skyddsrum/Uppgifter";
    const string ProfilePath = Folder + "/Synpaverkan.asset";

    [MenuItem("Tools/Skyddsrum/Uppgifter/Skapa uppgift: fyll baljorna")]
    public static void CreateFillBasins()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Uppgifter] Stoppa Play först."); return; }
        var basins = Object.FindObjectsByType<WaterBasin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .OrderBy(b => b.name).ToArray();
        var tap = Object.FindFirstObjectByType<WaterTap>();
        var door = Object.FindFirstObjectByType<ShelterDoor>();
        if (basins.Length == 0) { Log("[Uppgifter] Hittade inga baljor."); return; }

        GameObject root = GameObject.Find("Uppgifter");
        if (root == null) { root = new GameObject("Uppgifter"); Undo.RegisterCreatedObjectUndo(root, "Uppgifter"); }
        var old = root.transform.Find("Fyll baljorna");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = new GameObject("Fyll baljorna");
        Undo.RegisterCreatedObjectUndo(go, "Uppgift");
        go.transform.SetParent(root.transform, false);
        var task = go.AddComponent<FillBasinsTask>();
        task.title = "Fyll baljorna med vatten";
        task.basins = basins;
        task.startWhenDoorOpens = door;

        // Det som ska lysa: baljorna och kranen (inte vattenstrålar och vattenytor)
        var glow = new List<Renderer>();
        foreach (var b in basins) glow.AddRange(b.GetComponentsInChildren<Renderer>(true).Where(r => !IsWater(r)));
        if (tap != null) glow.AddRange(tap.GetComponentsInChildren<Renderer>(true).Where(r => !IsWater(r)));
        task.glowTargets = glow.ToArray();

        // Blinkande lampa ovanför baljorna
        Vector3 mid = Vector3.zero; foreach (var b in basins) mid += b.transform.position; mid /= basins.Length;
        var lightGo = new GameObject("Ledtrådslampa");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.position = mid + Vector3.up * 0.9f;
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.3f); l.range = 1.8f; l.intensity = 1f; l.shadows = LightShadows.None;
        task.hintLight = l;

        EnsureVisionEffect();
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = go;
        Log($"[Uppgifter] 'Fyll baljorna' skapad: {basins.Length} baljor, {glow.Count} delar lyser, startar när dörren öppnas{(door == null ? " (ingen dörr hittades – startar direkt)" : "")}. Synpåverkan klar. (scenen sparad)");
    }

    static bool IsWater(Renderer r)
    {
        string n = r.name;
        return n.Contains("Vatten") || n.Contains("stråle") || n.Contains("Stråle");
    }

    static void EnsureVisionEffect()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Skyddsrum")) AssetDatabase.CreateFolder("Assets", "Skyddsrum");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Skyddsrum", "Uppgifter");

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var vig = profile.Add<Vignette>();
            vig.intensity.Override(0.6f);
            vig.smoothness.Override(0.7f);
            vig.color.Override(Color.black);

            var ca = profile.Add<ColorAdjustments>();
            ca.saturation.Override(-65f);
            ca.postExposure.Override(-0.7f);

            var dof = profile.Add<DepthOfField>();
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0.3f);
            dof.gaussianEnd.Override(2.0f);
            dof.gaussianMaxRadius.Override(1.5f);
            dof.highQualitySampling.Override(false);

            foreach (var c in profile.components) { c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        var existing = Object.FindFirstObjectByType<VisionEffect>(FindObjectsInactive.Include);
        if (existing != null) return;
        var go = new GameObject("Synpåverkan");
        Undo.RegisterCreatedObjectUndo(go, "Synpåverkan");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 50f;
        vol.weight = 0f;
        vol.sharedProfile = profile;
        go.AddComponent<VisionEffect>();
    }

    // ---- Testning ----
    [MenuItem("Tools/Skyddsrum/Uppgifter/Testtider (ledtråd 10 s, syn 20 s)")]
    public static void TestTimes() => SetTimes(10f, 20f);

    [MenuItem("Tools/Skyddsrum/Uppgifter/Vanliga tider (ledtråd 60 s, syn 120 s)")]
    public static void NormalTimes() => SetTimes(60f, 120f);

    static void SetTimes(float hint, float cons)
    {
        var ve = Object.FindFirstObjectByType<VisionEffect>(FindObjectsInactive.Include);
        if (ve != null) ve.debugSeverity = -1f;
        foreach (var t in Object.FindObjectsByType<ShelterTask>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(t, "Tider");
            t.SetTimes(hint, cons);
            EditorUtility.SetDirty(t);
        }
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        Log($"[Uppgifter] Tider: ledtråd efter {hint} s, synpåverkan efter {cons} s.");
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: starta uppgifterna nu")]
    public static void StartNow()
    {
        if (!EditorApplication.isPlaying) { Log("[Uppgifter] Tryck Play först."); return; }
        foreach (var t in Object.FindObjectsByType<ShelterTask>(FindObjectsSortMode.None)) t.StartTask();
        Log("[Uppgifter] Alla uppgifter startade.");
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: visa sämsta synen")]
    public static void DebugWorst()
    {
        if (!EditorApplication.isPlaying) { Log("[Uppgifter] Tryck Play först – testet fungerar bara medan spelet körs."); return; }
        var v = Object.FindFirstObjectByType<VisionEffect>(); if (v) v.debugSeverity = 1f;
        Log("[Uppgifter] Test: sämsta synen PÅ (välj 'normal syn' för att stänga av).");
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: normal syn")]
    public static void DebugOff()
    {
        var v = Object.FindFirstObjectByType<VisionEffect>(FindObjectsInactive.Include); if (v) v.debugSeverity = -1f;
        Log("[Uppgifter] Test: synen styrs av uppgifterna igen.");
    }

    static void Log(string msg)
    {
        Debug.Log(msg);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }
}
