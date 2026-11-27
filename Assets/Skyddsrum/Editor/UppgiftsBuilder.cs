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

    [MenuItem("Tools/Skyddsrum/Uppgifter/Skapa uppgift: stäng skyddsrumsdörren (första)")]
    public static void CreateCloseDoorTask()
    {
        if (EditorApplication.isPlaying) { Log("[Uppgifter] Stoppa Play först."); return; }
        var door = Object.FindFirstObjectByType<ShelterDoor>();
        if (door == null) { Log("[Uppgifter] Hittade ingen skyddsrumsdörr."); return; }
        if (door.handles == null || door.handles.Length == 0) { Log("[Uppgifter] Dörren har inga handtag – bygg den verklighetstrogna dörren först."); return; }

        GameObject root = GameObject.Find("Uppgifter");
        if (root == null) { root = new GameObject("Uppgifter"); Undo.RegisterCreatedObjectUndo(root, "Uppgifter"); }
        var old = root.transform.Find("Stäng dörren");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = new GameObject("Stäng dörren");
        Undo.RegisterCreatedObjectUndo(go, "Uppgift");
        go.transform.SetParent(root.transform, false);
        go.transform.SetAsFirstSibling();
        var task = go.AddComponent<CloseDoorTask>();
        task.title = "Stäng skyddsrumsdörren";
        task.door = door;
        task.startWhenDoorOpens = door;
        task.requirePlayerInside = true;

        var water = Object.FindFirstObjectByType<FillBasinsTask>(FindObjectsInactive.Include);
        if (water != null) task.SetTimes(water.hintAfterSeconds, water.consequenceAfterSeconds);

        // Det som lyser: handtagen
        var glow = new List<Renderer>();
        Vector3 mid = Vector3.zero;
        foreach (var h in door.handles)
        {
            if (h == null) continue;
            glow.AddRange(h.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponent<TMPro.TextMeshPro>() == null && !r.name.StartsWith("Pil")));
            mid += h.transform.position;
        }
        mid /= door.handles.Length;
        task.glowTargets = glow.ToArray();

        var lightGo = new GameObject("Ledtrådslampa");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.position = mid + door.transform.forward * 0.6f;
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.3f); l.range = 1.6f; l.intensity = 1f; l.shadows = LightShadows.None;
        task.hintLight = l;

        if (water != null)
        {
            Undo.RecordObject(water, "Koppla vattenuppgift");
            water.startAfterTask = task;
            water.startDelaySeconds = 5f;
            EditorUtility.SetDirty(water);
        }
        EnsureVisionEffect();
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = go;
        Log($"[Uppgifter] 'Stäng skyddsrumsdörren' skapad som första uppgift ({glow.Count} delar lyser, ledtråd {task.hintAfterSeconds} s, syn {task.consequenceAfterSeconds} s). Vattnet startar 5 s efter att dörren är tät{(water == null ? " (ingen vattenuppgift hittades)" : "")}. (scenen sparad)");
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: stäng dörren tätt")]
    public static void DebugSealDoor()
    {
        if (!EditorApplication.isPlaying) { Log("[Uppgifter] Tryck Play först."); return; }
        var d = Object.FindFirstObjectByType<ShelterDoor>(); if (d == null) return;
        var t = Object.FindFirstObjectByType<CloseDoorTask>(); if (t != null) t.StartTask();
        d.DebugSeal();
        Log("[Uppgifter] Handtagen nere (test) – dörren stängs.");
    }

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
        var closeDoor = Object.FindFirstObjectByType<CloseDoorTask>(FindObjectsInactive.Include);
        if (closeDoor != null) { task.startAfterTask = closeDoor; task.startDelaySeconds = 5f; }

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
        foreach (var ct in Object.FindObjectsByType<CrankTask>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Undo.RecordObject(ct, "Koppla vev-uppgift");
            if (ct.startOnlyFromScript) continue; // startas av strömavbrottet
            ct.startAfterTask = task;
            EditorUtility.SetDirty(ct);
        }
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = go;
        Log($"[Uppgifter] 'Fyll baljorna' skapad: {basins.Length} baljor, {glow.Count} delar lyser, startar när dörren öppnas{(door == null ? " (ingen dörr hittades – startar direkt)" : "")}. Synpåverkan klar. (scenen sparad)");
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Skapa uppgift: veva ventilationen")]
    public static void CreateCrankTask()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Uppgifter] Stoppa Play först."); return; }
        var crank = Object.FindFirstObjectByType<VentilationCrank>();
        var water = Object.FindFirstObjectByType<FillBasinsTask>();
        if (crank == null) { Log("[Uppgifter] Hittade inget ventilationsaggregat. Bygg det först."); return; }
        if (water == null) { Log("[Uppgifter] Skapa uppgiften 'fyll baljorna' först."); return; }

        GameObject root = GameObject.Find("Uppgifter");
        if (root == null) { root = new GameObject("Uppgifter"); Undo.RegisterCreatedObjectUndo(root, "Uppgifter"); }
        var old = root.transform.Find("Veva ventilationen");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = new GameObject("Veva ventilationen");
        Undo.RegisterCreatedObjectUndo(go, "Uppgift");
        go.transform.SetParent(root.transform, false);
        var task = go.AddComponent<CrankTask>();
        task.title = "Veva ventilationen";
        task.crank = crank;
        task.startAfterTask = water;
        task.startDelaySeconds = 10f;
        task.startWhenDoorOpens = water.startWhenDoorOpens;
        task.SetTimes(water.hintAfterSeconds, water.consequenceAfterSeconds);
        task.consequenceRampSeconds = water.consequenceRampSeconds;
        task.pulsesPerSecond = water.pulsesPerSecond;

        // Lika lång tid som det tar att fylla baljorna
        float liters = 0f;
        foreach (var b in water.basins) if (b != null) liters += b.capacityLiters * water.requiredFill;
        var tap = Object.FindFirstObjectByType<WaterTap>();
        float flow = tap != null && tap.flowLitersPerSecond > 0f ? tap.flowLitersPerSecond : 3f;
        task.requiredCrankSeconds = Mathf.Round(liters / flow);

        // Det som lyser: veven, växellådan och fläkthuset
        var glow = new List<Renderer>();
        foreach (var r in crank.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name;
            bool partOfCrank = crank.pivot != null && r.transform.IsChildOf(crank.pivot);
            if (partOfCrank || n.StartsWith("Växellåda") || n.StartsWith("Fläkthus") || n == "Axel") glow.Add(r);
        }
        task.glowTargets = glow.ToArray();

        var lightGo = new GameObject("Ledtrådslampa");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.position = (crank.pivot != null ? crank.pivot.position : crank.transform.position) + new Vector3(0f, 0.35f, -0.25f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.3f); l.range = 1.8f; l.intensity = 1f; l.shadows = LightShadows.None;
        task.hintLight = l;

        AddRing(task, go.transform);
        var outage = Object.FindFirstObjectByType<PowerOutage>(FindObjectsInactive.Include);
        if (outage != null) StromavbrottBuilder.LinkCrank(task, outage);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = go;
        Log($"[Uppgifter] 'Veva ventilationen' skapad: startar {task.startDelaySeconds} s efter vattnet, kräver {task.requiredCrankSeconds} s vevande, {glow.Count} delar lyser, tider {task.hintAfterSeconds}/{task.consequenceAfterSeconds} s. (scenen sparad)");
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Lägg till förloppscirkel för veven")]
    public static void AddRingMenu()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Uppgifter] Stoppa Play först."); return; }
        var task = Object.FindFirstObjectByType<CrankTask>(FindObjectsInactive.Include);
        if (task == null) { Log("[Uppgifter] Skapa uppgiften 'veva ventilationen' först."); return; }
        AddRing(task, task.transform);
        EditorSceneManager.MarkSceneDirty(task.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Log("[Uppgifter] Förloppscirkel tillagd för veven. (scenen sparad)");
    }

    static void AddRing(CrankTask task, Transform parent)
    {
        var old = parent.Find("Vevmätare");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject("Vevmätare");
        Undo.RegisterCreatedObjectUndo(go, "Vevmätare");
        go.transform.SetParent(parent, false);
        var ring = go.AddComponent<ProgressRing>();
        ring.task = task;
        ring.ringMaterial = UnlitMaterial();
        MakeOverlay();
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Visa text och cirkel ovanpå allt")]
    public static void MakeOverlay()
    {
        var ring = UnlitMaterial();
        SetOverlay(ring);
        // Textmaterial som ritas ovanpå allt, i en Resources-mapp så att det alltid följer med i bygget
        const string resFolder = Folder + "/Resources";
        if (!AssetDatabase.IsValidFolder(resFolder)) AssetDatabase.CreateFolder(Folder, "Resources");
        const string textPath = resFolder + "/UppgiftText_Overlay.mat";
        var textMat = AssetDatabase.LoadAssetAtPath<Material>(textPath);
        var overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (textMat == null && overlayShader != null && TMPro.TMP_Settings.defaultFontAsset != null)
        {
            textMat = new Material(TMPro.TMP_Settings.defaultFontAsset.material) { shader = overlayShader };
            textMat.renderQueue = 4000;
            AssetDatabase.CreateAsset(textMat, textPath);
        }
        AssetDatabase.SaveAssets();
        Log($"[Uppgifter] Text och cirkel ritas nu ovanpå allt (text: {(textMat != null ? "ok" : "saknas – hittade inte TMP-overlay-shadern")}).");
    }

    static void SetOverlay(Material m)
    {
        var overlay = Shader.Find("Skyddsrum/OverlayUnlit");
        if (overlay != null && m.shader != overlay) m.shader = overlay;
        if (m.HasProperty("_ZTest")) m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.renderQueue = 4000;
        EditorUtility.SetDirty(m);
    }

    static Material UnlitMaterial()
    {
        const string path = Folder + "/Ring_Unlit.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Skyddsrum", "Uppgifter");
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // båda sidor
        AssetDatabase.CreateAsset(m, path);
        return m;
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

    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: veva 8 sekunder")]
    public static void DebugCrank()
    {
        if (!EditorApplication.isPlaying) { Log("[Uppgifter] Tryck Play först."); return; }
        var t = Object.FindFirstObjectByType<CrankTask>(); if (t != null) t.DebugAddCrankSeconds(8f);
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
