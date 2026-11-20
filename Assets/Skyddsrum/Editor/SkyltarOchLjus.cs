using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Meny: Tools > Skyddsrum > ...
///  - Skyltar: skyddsrumsskylten vid den låsta dörren + gröna nödutgångsskyltar
///  - Belysning: dämpad (nödbelysning) eller normal
/// </summary>
public static class SkyltarOchLjus
{
    const string Root = "Assets/Skyddsrum";
    const string MatFolder = Root + "/Material";
    const float Floor = 2.10f;
    static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    // ======================= SKYLTAR =======================
    [MenuItem("Tools/Skyddsrum/Sätt upp skyltar")]
    public static void BuildSigns()
    {
        _mats.Clear();
        GameObject old;
        while ((old = GameObject.Find("Skyltar")) != null) Undo.DestroyObjectImmediate(old);
        var group = new GameObject("Skyltar");
        Undo.RegisterCreatedObjectUndo(group, "Skyltar");
        var g = group.transform;

        // Skyddsrumsskylten: förrummets västvägg (x=0.70), söder om den låsta dörren, vänd in mot förrummet
        BuildShelterSign(g, new Vector3(0.705f, Floor + 1.75f, 1.05f), Vector3.left);

        // Gröna nödutgångsskyltar (vända in mot respektive rum)
        BuildExitSign(g, "Nödutgång – förrummets ingång", new Vector3(3.2f, Floor + 2.28f, 2.80f), Vector3.forward);
        BuildExitSign(g, "Nödutgång – skyddsrumsdörren (insidan)", new Vector3(0.475f, Floor + 2.28f, 1.84f), Vector3.right);
        BuildExitSign(g, "Nödutgång – toalettrummet", new Vector3(0.725f, Floor + 2.28f, -1.2f), Vector3.left);

        Save(group, "[Skyltar] Skyddsrumsskylt och tre nödutgångsskyltar uppsatta (scenen sparad).");
    }

    /// <param name="intoWall">Riktningen in i väggen (skylten syns från motsatt håll)</param>
    static void BuildShelterSign(Transform parent, Vector3 pos, Vector3 intoWall)
    {
        var orange = Mat("Skylt_Orange", new Color(1f, 0.45f, 0.0f));
        var blue = Mat("Skylt_Bla", new Color(0.0f, 0.22f, 0.65f));
        var white = Mat("Skylt_Vit", new Color(0.97f, 0.97f, 0.97f));

        var root = new GameObject("Skyddsrumsskylt").transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(pos, Quaternion.LookRotation(intoWall));

        const float S = 0.36f;      // skyltens bredd
        const float strip = 0.09f;  // textremsan
        // Orange platta (överdel) och vit textremsa (underdel)
        Quad(root, "Orange platta", new Vector3(0f, strip / 2f, 0f), new Vector3(S, S, 0.012f), orange);
        Quad(root, "Textremsa", new Vector3(0f, -S / 2f, 0f), new Vector3(S, strip, 0.012f), white);

        // Blå liksidig triangel
        var tri = new GameObject("Blå triangel");
        tri.transform.SetParent(root, false);
        tri.transform.localPosition = new Vector3(0f, strip / 2f, -0.007f);
        tri.AddComponent<MeshFilter>().sharedMesh = TriangleMesh();
        tri.AddComponent<MeshRenderer>().sharedMaterial = blue;

        var text = Text(root, "SKYDDSRUM", new Vector3(0f, -S / 2f, -0.008f), new Vector2(S - 0.02f, strip - 0.01f), 0.6f);
        text.color = Color.black;
        text.fontStyle = FontStyles.Bold;
    }

    static void BuildExitSign(Transform parent, string name, Vector3 pos, Vector3 intoWall)
    {
        var green = Glow("Nodutgang_Gron", new Color(0.0f, 0.62f, 0.25f), 1.4f);
        var whiteGlow = Glow("Nodutgang_Vit", new Color(1f, 1f, 1f), 1.2f);

        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(pos, Quaternion.LookRotation(intoWall));

        Quad(root, "Grön låda", Vector3.zero, new Vector3(0.40f, 0.15f, 0.04f), green);
        // Vit pil uppåt (rakt fram) till vänster
        Quad(root, "Pil – skaft", new Vector3(-0.14f, -0.015f, -0.021f), new Vector3(0.018f, 0.07f, 0.004f), whiteGlow);
        var h1 = Quad(root, "Pil – spets V", new Vector3(-0.152f, 0.022f, -0.021f), new Vector3(0.018f, 0.045f, 0.004f), whiteGlow);
        h1.transform.localEulerAngles = new Vector3(0f, 0f, -40f); // vänster del av pilspetsen
        var h2 = Quad(root, "Pil – spets H", new Vector3(-0.128f, 0.022f, -0.021f), new Vector3(0.018f, 0.045f, 0.004f), whiteGlow);
        h2.transform.localEulerAngles = new Vector3(0f, 0f, 40f);  // höger del av pilspetsen
        var t = Text(root, "NÖDUTGÅNG", new Vector3(0.035f, 0f, -0.022f), new Vector2(0.28f, 0.1f), 0.55f);
        t.color = Color.white;
        t.fontStyle = FontStyles.Bold;
    }

    // ======================= BELYSNING =======================
    [MenuItem("Tools/Skyddsrum/Belysning/Dämpad (nödbelysning)")]
    public static void DimLights()
    {
        var log = new StringBuilder("[Belysning] ");
        var state = Object.FindFirstObjectByType<BelysningsLage>();
        if (state != null) { RestoreLights(); }

        var go = new GameObject("Belysning (dämpad)");
        Undo.RegisterCreatedObjectUndo(go, "Dämpad belysning");
        state = go.AddComponent<BelysningsLage>();
        state.originalAmbient = RenderSettings.ambientLight;
        state.originalAmbientIntensity = RenderSettings.ambientIntensity;

        // Dämpa alla befintliga ljus (utom små ljuskällor som ljuslågan)
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Point && l.range < 2f) continue;
            Undo.RecordObject(l, "Dämpa ljus");
            state.dimmedLights.Add(l);
            state.originalIntensities.Add(l.intensity);
            l.intensity *= 0.3f;
            EditorUtility.SetDirty(l);
            log.Append($"{l.name} {state.originalIntensities[state.originalIntensities.Count - 1]:F2}→{l.intensity:F2}. ");
        }
        RenderSettings.ambientLight = state.originalAmbient * 0.6f;

        // Svaga, varma taklampor i varje rum
        var lamps = new GameObject("Svaga taklampor").transform;
        lamps.SetParent(go.transform, false);
        var golv = GameObject.Find("Golv");
        Bounds b = golv != null && golv.GetComponent<Renderer>() != null ? golv.GetComponent<Renderer>().bounds
                 : new Bounds(new Vector3(0f, Floor, 0f), new Vector3(8f, 0f, 6f));
        float y = Floor + 2.3f;
        Lamp(lamps, "Förrummet", new Vector3(2.3f, y, 1.85f));
        Lamp(lamps, "Toalettrummet", new Vector3(2.3f, y, -1.15f));
        float bx = (b.min.x + 0.5f) / 2f;
        float zq = (b.max.z - b.min.z) / 4f;
        Lamp(lamps, "Stora rummet norr", new Vector3(bx, y, b.max.z - zq));
        Lamp(lamps, "Stora rummet söder", new Vector3(bx, y, b.min.z + zq));
        log.Append("4 svaga taklampor tillagda.");
        Save(go, log.ToString());
    }

    [MenuItem("Tools/Skyddsrum/Belysning/Normal (som innan)")]
    public static void RestoreLightsMenu()
    {
        string msg = RestoreLights() ? "[Belysning] Återställd till normal." : "[Belysning] Belysningen var inte dämpad.";
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Log(msg);
    }

    static bool RestoreLights()
    {
        var state = Object.FindFirstObjectByType<BelysningsLage>();
        if (state == null) return false;
        for (int i = 0; i < state.dimmedLights.Count; i++)
        {
            var l = state.dimmedLights[i];
            if (l == null) continue;
            Undo.RecordObject(l, "Återställ ljus");
            l.intensity = state.originalIntensities[i];
            EditorUtility.SetDirty(l);
        }
        RenderSettings.ambientLight = state.originalAmbient;
        RenderSettings.ambientIntensity = state.originalAmbientIntensity;
        Undo.DestroyObjectImmediate(state.gameObject);
        return true;
    }

    static void Lamp(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject("Lampa – " + name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.86f, 0.68f);
        l.intensity = 0.9f;
        l.range = 4.5f;
        l.shadows = LightShadows.None;
    }

    // ======================= HJÄLPFUNKTIONER =======================
    static void Save(GameObject select, string msg)
    {
        EditorSceneManager.MarkSceneDirty(select.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = select;
        Log(msg);
    }

    static void Log(string msg)
    {
        Debug.Log(msg);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }

    static GameObject Quad(Transform parent, string name, Vector3 localPos, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    static TextMeshPro Text(Transform parent, string text, Vector3 localPos, Vector2 size, float fontSize)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.1f;
        tmp.fontSizeMax = fontSize;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static Mesh TriangleMesh()
    {
        string path = MatFolder + "/Triangel.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        EnsureFolders();
        const float side = 0.29f;
        float h = side * Mathf.Sqrt(3f) / 2f;
        mesh = new Mesh { name = "Triangel" };
        mesh.vertices = new[] { new Vector3(0f, h * 2f / 3f, 0f), new Vector3(side / 2f, -h / 3f, 0f), new Vector3(-side / 2f, -h / 3f, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 }; // båda sidor
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "Skyddsrum");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder(Root, "Material");
    }

    static Material Glow(string name, Color c, float strength)
    {
        var m = Mat(name, c);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * strength);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
        }
        return m;
    }

    static Material Mat(string name, Color color)
    {
        if (_mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            EnsureFolders();
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(m, path);
        }
        _mats[name] = m;
        return m;
    }
}
