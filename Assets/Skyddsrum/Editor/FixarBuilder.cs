using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Meny: Tools > Skyddsrum > Fixa ljus, golv och ljud (Quest)
/// – slår på extra ljuskällor i Quest-inställningen (ficklampa, lampor, nödljus syns)
/// – tar bort den extra teleportytan på Golv, stänger av gravitationen och håller spelaren på golvhöjd
/// – längre flyglarm, tystare fläktbrus, ficklampa som tänds och lyser när man tar den
/// </summary>
public static class FixarBuilder
{
    const string MeshFolder = "Assets/Skyddsrum/Modeller";
    const string MatFolder = "Assets/Skyddsrum/Material";

    [MenuItem("Tools/Skyddsrum/Fixa ljus, golv och ljud (Quest)")]
    public static void Fix()
    {
        if (EditorApplication.isPlaying) { Log("[Fixa] Stoppa Play först."); return; }
        var sb = new StringBuilder("[Fixa] ");

        // 1. Extra ljuskällor i alla URP-inställningar som används
        var assets = new HashSet<UniversalRenderPipelineAsset>();
        for (int i = 0; i < QualitySettings.names.Length; i++)
            if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset a) assets.Add(a);
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset d) assets.Add(d);
        foreach (var a in assets)
        {
            var so = new SerializedObject(a);
            var mode = so.FindProperty("m_AdditionalLightsRenderingMode");
            var limit = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            var shadows = so.FindProperty("m_AdditionalLightShadowsSupported");
            if (mode != null && mode.intValue == 0) mode.intValue = 1; // 1 = per pixel
            if (limit != null && limit.intValue < 4) limit.intValue = 4;
            if (shadows != null) shadows.boolValue = false;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(a);
            sb.Append($"{a.name}: extraljus={a.additionalLightsRenderingMode}, max {a.maxAdditionalLightsCount}. ");
        }

        // 2. Ta bort teleportytan på Golv
        var golv = GameObject.Find("Golv");
        var ta = golv != null ? golv.GetComponent<TeleportationArea>() : null;
        if (ta != null) { Undo.DestroyObjectImmediate(ta); sb.Append("Teleportytan på Golv borttagen. "); }

        // 3. Ingen gravitation + håll spelaren på golvhöjd
        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
        {
            foreach (var mb in origin.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb.GetType().Name == "GravityProvider" && mb.enabled)
                {
                    Undo.RecordObject(mb, "Stäng av gravitation");
                    mb.enabled = false;
                    EditorUtility.SetDirty(mb);
                    sb.Append("Gravitation avstängd. ");
                }
            var guard = origin.GetComponent<FloorGuard>();
            if (guard == null) guard = Undo.AddComponent<FloorGuard>(origin.gameObject);
            guard.floorY = origin.transform.position.y;
            EditorUtility.SetDirty(guard);
            sb.Append($"Golvvakt på {origin.name} (y={guard.floorY:F2}). ");
        }

        // 4. Längre flyglarm
        var fly = Object.FindFirstObjectByType<flyglarmscript>(FindObjectsInactive.Include);
        if (fly != null)
        {
            Undo.RecordObject(fly, "Längre flyglarm");
            fly.duration = 12f;
            fly.fadeOutSeconds = 1.5f;
            EditorUtility.SetDirty(fly);
            sb.Append("Flyglarm 12 s. ");
        }

        // 5. Tystare fläktbrus + 6. ficklampa
        var po = Object.FindFirstObjectByType<PowerOutage>(FindObjectsInactive.Include);
        if (po != null)
        {
            Undo.RecordObject(po, "Tystare fläkt");
            po.fanVolume = 0.1f;
            EditorUtility.SetDirty(po);
            sb.Append("Fläktbrus 0.1 (var 0.3). ");
            if (po.flashlight != null) { UpgradeFlashlight(po.flashlight); sb.Append("Ficklampan uppgraderad. "); }
        }

        // 7. Nu när extra ljuskällor syns: håll taklamporna svaga (som innan) och nödljuset diskret
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            float target = -1f;
            if (l.transform.parent != null && l.transform.parent.name == "Svaga taklampor") target = 0.3f;
            else if (l.name == "Nödljus (strömavbrott)") target = 0.35f;
            if (target < 0f || l.intensity <= target) continue;
            Undo.RecordObject(l, "Svagare lampa");
            l.intensity = target;
            EditorUtility.SetDirty(l);
        }
        sb.Append("Taklampor 0.3, nödljus 0.35. ");

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Log(sb.Append("(scenen sparad)").ToString());
    }

    /// <summary>Ger ficklampan synlig stråle, sken runt omkring och gör att den tänds när man tar den.</summary>
    public static void UpgradeFlashlight(GameObject root)
    {
        var t = root.transform;
        foreach (var n in new[] { "Sken", "Ljusstråle" })
        {
            var old = t.Find(n);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var spotT = t.Find("Ljuskägla");
        var spot = spotT != null ? spotT.GetComponent<Light>() : null;
        if (spot != null)
        {
            spot.intensity = 6f; spot.range = 10f; spot.spotAngle = 50f; spot.innerSpotAngle = 25f;
            spot.shadows = LightShadows.None;
        }

        var glowGo = new GameObject("Sken");
        glowGo.transform.SetParent(t, false);
        glowGo.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        var glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = new Color(1f, 0.93f, 0.8f); glow.intensity = 0.8f; glow.range = 3f; glow.shadows = LightShadows.None;

        var beamGo = new GameObject("Ljusstråle");
        beamGo.transform.SetParent(t, false);
        beamGo.transform.localPosition = new Vector3(0f, 0f, 0.09f);
        beamGo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(Cone(1.6f, 0.02f, 0.55f, 24), "Ficklampa_Strale");
        var beamR = beamGo.AddComponent<MeshRenderer>();
        beamR.sharedMaterial = BeamMaterial();
        beamR.shadowCastingMode = ShadowCastingMode.Off;
        beamR.receiveShadows = false;

        var f = root.GetComponent<Ficklampa>();
        if (f == null) f = root.AddComponent<Ficklampa>();
        f.spot = spot;
        f.glow = glow;
        f.beam = beamR;
        var lens = t.Find("Lins");
        f.lens = lens != null ? lens.GetComponent<Renderer>() : null;
        f.startOn = false;
        EditorUtility.SetDirty(f);
    }

    static Mesh Cone(float length, float r0, float r1, int seg)
    {
        float[] ts = { 0f, 0.3f, 1f };
        float[] alpha = { 0.45f, 0.2f, 0f };
        var v = new List<Vector3>(); var c = new List<Color>(); var tri = new List<int>();
        foreach (int ring in Enumerable.Range(0, ts.Length))
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                float r = Mathf.Lerp(r0, r1, ts[ring]);
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, ts[ring] * length));
                c.Add(new Color(1f, 1f, 1f, alpha[ring]));
            }
        int row = seg + 1;
        for (int ring = 0; ring < ts.Length - 1; ring++)
            for (int i = 0; i < seg; i++)
            {
                int a = ring * row + i, b = a + 1, cc = a + row, dd = cc + 1;
                tri.AddRange(new[] { a, cc, b, b, cc, dd, a, b, cc, b, dd, cc });
            }
        var m = new Mesh { name = "Ljusstråle" };
        m.SetVertices(v); m.SetColors(c); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Material BeamMaterial()
    {
        string path = MatFolder + "/Ficklampa_Strale.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        if (sh != null && m.shader != sh) m.shader = sh;
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 2f); // additiv
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.One);
        if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", new Color(1f, 0.94f, 0.78f, 0.5f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = $"{MeshFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(m, existing); existing.name = name; EditorUtility.SetDirty(existing); return existing; }
        m.name = name;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: tänd ficklampan")]
    static void TestFlashlight()
    {
        if (!EditorApplication.isPlaying) { Log("[Fixa] Tryck Play först."); return; }
        foreach (var f in Object.FindObjectsByType<Ficklampa>(FindObjectsSortMode.None)) f.SetOn(true);
    }

    [MenuItem("Tools/Skyddsrum/Ge ficklampan på hyllan riktigt ljus")]
    public static void ShelfFlashlight()
    {
        if (EditorApplication.isPlaying) { Log("[Fixa] Stoppa Play först."); return; }
        int n = 0;
        foreach (var item in Object.FindObjectsByType<PackItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.Category != "Ficklampa" && item.displayName != "Ficklampa") continue;
            var root = item.transform;
            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Ficklampa med ljus");
            foreach (var old in new[] { "Ljuspunkt", "Lins" })
            {
                var o = root.Find(old);
                if (o != null) Object.DestroyImmediate(o.gameObject);
            }

            // Lampan står upprätt: huvudet uppåt (+y). Ljuset lyser åt det håll huvudet pekar.
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lens.name = "Lins";
            Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.transform.SetParent(root, false);
            lens.transform.localPosition = new Vector3(0f, 0.1705f, 0f);
            lens.transform.localScale = new Vector3(0.042f, 0.002f, 0.042f);
            var lensMat = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Ficklampa_Lins.mat");
            if (lensMat != null) lens.GetComponent<Renderer>().sharedMaterial = lensMat;

            var lp = new GameObject("Ljuspunkt").transform;
            lp.SetParent(root, false);
            lp.localPosition = new Vector3(0f, 0.172f, 0f);
            lp.localRotation = Quaternion.Euler(-90f, 0f, 0f); // framåt = uppåt längs lampan

            var spot = new GameObject("Ljuskägla").AddComponent<Light>();
            spot.transform.SetParent(lp, false);
            spot.type = LightType.Spot; spot.color = new Color(1f, 0.95f, 0.85f);
            spot.intensity = 6f; spot.range = 10f; spot.spotAngle = 50f; spot.innerSpotAngle = 25f; spot.shadows = LightShadows.None;

            var glow = new GameObject("Sken").AddComponent<Light>();
            glow.transform.SetParent(lp, false);
            glow.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            glow.type = LightType.Point; glow.color = new Color(1f, 0.93f, 0.8f); glow.intensity = 0.8f; glow.range = 3f; glow.shadows = LightShadows.None;

            var beamGo = new GameObject("Ljusstråle");
            beamGo.transform.SetParent(lp, false);
            beamGo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(Cone(1.6f, 0.02f, 0.55f, 24), "Ficklampa_Strale");
            var beamR = beamGo.AddComponent<MeshRenderer>();
            beamR.sharedMaterial = BeamMaterial();
            beamR.shadowCastingMode = ShadowCastingMode.Off;
            beamR.receiveShadows = false;

            var f = root.GetComponent<Ficklampa>();
            if (f == null) f = Undo.AddComponent<Ficklampa>(root.gameObject);
            f.spot = spot; f.glow = glow; f.beam = beamR; f.lens = lens.GetComponent<Renderer>(); f.startOn = false;
            EditorUtility.SetDirty(f);
            n++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Log($"[Fixa] {n} ficklampa/or på hyllan fick riktigt ljus. (scenen sparad)");
    }

    static void Log(string msg)
    {
        Debug.Log(msg);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }
}
