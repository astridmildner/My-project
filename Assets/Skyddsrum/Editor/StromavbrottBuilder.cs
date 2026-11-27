using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Meny: Tools > Skyddsrum > Uppgifter > Skapa strömavbrott ...
/// Efter vattnet går strömmen: lamporna slocknar (inte kolsvart), fläkten tystnar, ficklampan
/// dyker upp om den var packad, och man vevar tills strömmen kommer tillbaka.
/// </summary>
public static class StromavbrottBuilder
{
    const string Folder = "Assets/Skyddsrum/Uppgifter";
    const string ProfilePath = Folder + "/Stromavbrott.asset";
    const string MatFolder = "Assets/Skyddsrum/Material";
    const string RootName = "Strömavbrott";

    [MenuItem("Tools/Skyddsrum/Uppgifter/Skapa strömavbrott (vevning när strömmen går)")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) { Log("[Ström] Stoppa Play först."); return; }
        var crankTask = Object.FindFirstObjectByType<CrankTask>(FindObjectsInactive.Include);
        var water = Object.FindFirstObjectByType<FillBasinsTask>(FindObjectsInactive.Include);
        var crank = Object.FindFirstObjectByType<VentilationCrank>(FindObjectsInactive.Exclude);
        if (crankTask == null || water == null || crank == null) { Log("[Ström] Behöver vevuppgiften, vattenuppgiften och veven i scenen."); return; }

        GameObject tasks = GameObject.Find("Uppgifter");
        if (tasks == null) { tasks = new GameObject("Uppgifter"); Undo.RegisterCreatedObjectUndo(tasks, "Uppgifter"); }
        var old = tasks.transform.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        foreach (var e in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (e.name == "Nödljus (strömavbrott)") Undo.DestroyObjectImmediate(e.gameObject);
        var oldFan = crank.transform.Find("Fläktljud");
        if (oldFan != null) Undo.DestroyObjectImmediate(oldFan.gameObject);

        var go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Strömavbrott");
        go.transform.SetParent(tasks.transform, false);
        var po = go.AddComponent<PowerOutage>();
        po.startAfterTask = water;
        po.startDelaySeconds = 10f;
        po.crankStartDelay = 5f;
        po.crankSeconds = 40f;
        po.crank = crank;
        po.crankTask = crankTask;
        po.backpack = Object.FindFirstObjectByType<Backpack>(FindObjectsInactive.Include);

        // ---- Lampor: taklampor släcks, "grundljuset" (riktade ljus) dämpas ----
        var off = new List<Light>(); var dim = new List<Light>();
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (l.transform.IsChildOf(tasks.transform)) continue;   // ledtrådslampor
            if (l.type == LightType.Point && l.range < 2f) continue; // levande ljus o.d.
            if (l.type == LightType.Directional) dim.Add(l); else off.Add(l);
        }
        po.lightsOff = off.ToArray();
        po.lightsDim = dim.ToArray();
        po.dimFraction = 0.25f;

        // ---- Svagt grönt sken vid utgångsskyltarna ----
        var em = new List<Light>();
        var signs = GameObject.Find("Skyltar");
        if (signs != null)
            foreach (Transform s in signs.transform)
            {
                if (!s.name.StartsWith("Nödutgång")) continue;
                var lg = new GameObject("Nödljus (strömavbrott)");
                lg.transform.SetParent(s, false);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(0.25f, 1f, 0.45f); l.intensity = 0.6f; l.range = 2.8f; l.shadows = LightShadows.None;
                l.enabled = false;
                em.Add(l);
            }
        po.emergencyLights = em.ToArray();

        // ---- Lite mörkare och gråare bild ----
        po.darknessVolume = MakeVolume(go.transform);

        // ---- Fläktljud ----
        var fanGo = new GameObject("Fläktljud");
        fanGo.transform.SetParent(crank.transform, false);
        fanGo.transform.position = crank.fanRotor != null ? crank.fanRotor.position : crank.transform.position;
        var a = fanGo.AddComponent<AudioSource>();
        a.playOnAwake = false; a.loop = true; a.spatialBlend = 1f; a.minDistance = 1.2f; a.maxDistance = 9f; a.volume = 0f;
        a.rolloffMode = AudioRolloffMode.Linear;
        po.fanSound = a;
        po.fanVolume = 0.3f;

        // ---- Ficklampa ----
        po.flashlight = BuildFlashlight(go.transform);

        LinkCrank(crankTask, po);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = go;
        Log($"[Ström] Strömavbrott skapat: {off.Count} lampor släcks, {dim.Count} dämpas, {em.Count} nödljus, ryggsäck {(po.backpack != null ? "hittad" : "saknas")}. Strömmen går 10 s efter vattnet, vevning i {po.crankSeconds} s. (scenen sparad)");
    }

    /// <summary>Gör så att vevuppgiften startas av strömavbrottet och är klar när strömmen kommer tillbaka.</summary>
    public static void LinkCrank(CrankTask task, PowerOutage po)
    {
        Undo.RecordObject(task, "Koppla vevuppgift");
        task.outage = po;
        task.startOnlyFromScript = true;
        task.startAfterTask = null;
        task.consequenceRampSeconds = 20f;
        task.SetTimes(task.hintAfterSeconds, task.consequenceAfterSeconds);
        EditorUtility.SetDirty(task);
        Undo.RecordObject(po, "Koppla vevuppgift");
        po.crankTask = task;
        EditorUtility.SetDirty(po);
    }

    static Volume MakeVolume(Transform parent)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        if (!profile.TryGet(out ColorAdjustments ca)) ca = profile.Add<ColorAdjustments>(true);
        ca.active = true;
        ca.postExposure.overrideState = true; ca.postExposure.value = -0.4f;
        ca.saturation.overrideState = true; ca.saturation.value = -30f;
        EditorUtility.SetDirty(ca);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var vgo = new GameObject("Mörker vid strömavbrott");
        vgo.transform.SetParent(parent, false);
        var v = vgo.AddComponent<Volume>();
        v.isGlobal = true; v.priority = 20f; v.weight = 0f; v.sharedProfile = profile;
        return v;
    }

    static GameObject BuildFlashlight(Transform parent)
    {
        var black = Mat("Ficklampa_Svart", new Color(0.06f, 0.06f, 0.06f), 0.3f, 0.5f, Color.black);
        var yellow = Mat("Ficklampa_Gul", new Color(0.95f, 0.8f, 0.1f), 0.1f, 0.5f, Color.black);
        var lens = Mat("Ficklampa_Lins", new Color(1f, 0.97f, 0.85f), 0f, 0.9f, new Color(2f, 1.9f, 1.6f));

        var root = new GameObject("Ficklampa (från ryggsäcken)");
        root.transform.SetParent(parent, false);
        // Längs z (framåt) – ljuset lyser åt +z
        Part(PrimitiveType.Cylinder, root.transform, "Skaft", new Vector3(0f, 0f, -0.02f), new Vector3(90f, 0f, 0f), new Vector3(0.035f, 0.07f, 0.035f), black);
        Part(PrimitiveType.Cylinder, root.transform, "Huvud", new Vector3(0f, 0f, 0.065f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.02f, 0.05f), yellow);
        Part(PrimitiveType.Cylinder, root.transform, "Lins", new Vector3(0f, 0f, 0.086f), new Vector3(90f, 0f, 0f), new Vector3(0.042f, 0.002f, 0.042f), lens);
        Part(PrimitiveType.Cube, root.transform, "Knapp", new Vector3(0f, 0.018f, 0.01f), Vector3.zero, new Vector3(0.012f, 0.006f, 0.02f), yellow);

        var lg = new GameObject("Ljuskägla");
        lg.transform.SetParent(root.transform, false);
        lg.transform.localPosition = new Vector3(0f, 0f, 0.09f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Spot; l.color = new Color(1f, 0.95f, 0.85f); l.intensity = 12f; l.range = 10f;
        l.spotAngle = 48f; l.innerSpotAngle = 22f; l.shadows = LightShadows.None;

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0f, 0.03f); col.size = new Vector3(0.06f, 0.06f, 0.2f);
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.3f; rb.isKinematic = true; rb.useGravity = false;
        root.AddComponent<XRGrabInteractable>();
        root.SetActive(false);
        return root;
    }

    static void Part(PrimitiveType t, Transform p, string n, Vector3 pos, Vector3 e, Vector3 s, Material m)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = n;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos; go.transform.localEulerAngles = e; go.transform.localScale = s;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }

    static Material Mat(string name, Color c, float metallic, float smooth, Color emission)
    {
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smooth);
        if (emission.maxColorComponent > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- Test under Play ----------
    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: strömavbrott nu (med ficklampa)")]
    static void TestWith() => Test(1);

    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: strömavbrott nu (utan ficklampa)")]
    static void TestWithout() => Test(0);

    static void Test(int flashlight)
    {
        if (!EditorApplication.isPlaying) { Log("[Ström] Tryck Play först."); return; }
        var po = Object.FindFirstObjectByType<PowerOutage>(); if (po == null) { Log("[Ström] Inget strömavbrott i scenen."); return; }
        po.debugFlashlight = flashlight;
        po.BeginOutage();
        Log("[Ström] Strömavbrott startat (test).");
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: strömmen tillbaka om 2 s")]
    static void TestSkip()
    {
        if (!EditorApplication.isPlaying) { Log("[Ström] Tryck Play först."); return; }
        var po = Object.FindFirstObjectByType<PowerOutage>();
        if (po != null) po.DebugSkipToEnd();
        Log($"[Ström] Testknapp: strömmen tillbaka ({(po != null ? po.Current.ToString() : "inget strömavbrott hittat")}).");
    }

    static void Log(string msg)
    {
        Debug.Log(msg);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }
}
