using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Meny: Tools > Skyddsrum > Ventilationsaggregat > ...
/// Bygger ett ventilationsaggregat med vev (som på bilden: blått stativ, filterbehållare,
/// fläkthus, vev, flexslangar) och döljer den gamla veven.
/// </summary>
public static class VentilationBuilder
{
    const string MatFolder = "Assets/Skyddsrum/Material";
    const float Floor = 2.10f;
    const string GroupName = "Ventilationsaggregat";
    static readonly string[] OldNames = { "Roap crank low", "Vev" };
    static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    [MenuItem("Tools/Skyddsrum/Ventilationsaggregat/Bygg nytt (ersätter gamla veven)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Log("[Ventilation] Stoppa Play först."); return; }
        _mats.Clear();
        GameObject old;
        while ((old = GameObject.Find(GroupName)) != null) Undo.DestroyObjectImmediate(old);

        var blue = Mat("Vent_Bla", new Color(0.06f, 0.20f, 0.58f), 0.35f, 0.5f);
        var galv = Mat("Vent_Galvaniserad", new Color(0.74f, 0.76f, 0.77f), 0.9f, 0.5f);
        var foil = Mat("Vent_Folie", new Color(0.80f, 0.81f, 0.83f), 0.75f, 0.35f);
        var hose = Mat("Vent_Slang_Svart", new Color(0.09f, 0.09f, 0.09f), 0.1f, 0.3f);
        var rib = Mat("Vent_Slang_Rand", new Color(0.33f, 0.32f, 0.28f), 0.3f, 0.3f);
        var motor = Mat("Vent_Motor", new Color(0.86f, 0.87f, 0.87f), 0.3f, 0.5f);
        var steel = Mat("Vent_Vev", new Color(0.82f, 0.83f, 0.85f), 0.9f, 0.7f);
        var dark = Mat("Svart_Plast", new Color(0.06f, 0.06f, 0.06f), 0.1f, 0.4f);
        var label = Mat("Etikett_Vit", new Color(0.95f, 0.95f, 0.93f), 0f, 0.3f);

        // Placering: mot södra väggen (z=2.83), där takkanalen kommer ner (x=-1.27)
        var rootGo = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(rootGo, "Ventilationsaggregat");
        var root = rootGo.transform;
        root.SetPositionAndRotation(new Vector3(-1.27f, Floor, 2.55f), Quaternion.identity); // framsidan mot -z (in i rummet)

        // ---- Stativ ----
        const float hx = 0.26f, hz = 0.21f, tableY = 0.95f;
        foreach (var (x, z) in new[] { (hx, hz), (-hx, hz), (hx, -hz), (-hx, -hz) })
            B(root, "Ben", new Vector3(x, tableY / 2f, z), new Vector3(0.04f, tableY, 0.04f), blue);
        B(root, "Bordsskiva", new Vector3(0f, tableY + 0.015f, 0f), new Vector3(0.62f, 0.03f, 0.48f), blue);
        B(root, "Bordskant fram", new Vector3(0f, tableY - 0.02f, -0.235f), new Vector3(0.62f, 0.06f, 0.012f), blue);
        B(root, "Mellanhylla", new Vector3(0f, 0.50f, 0f), new Vector3(0.54f, 0.02f, 0.44f), blue);
        B(root, "Bottenhylla", new Vector3(0f, 0.11f, 0f), new Vector3(0.54f, 0.02f, 0.44f), blue);
        foreach (float y in new[] { 0.11f, 0.50f })
        {
            B(root, "Stag", new Vector3(0f, y - 0.03f, hz), new Vector3(0.54f, 0.04f, 0.02f), blue);
            B(root, "Stag", new Vector3(0f, y - 0.03f, -hz), new Vector3(0.54f, 0.04f, 0.02f), blue);
        }

        // ---- Filterbehållare (galvaniserad) på bottenhyllan ----
        C(root, "Filterbehållare", new Vector3(0f, 0.30f, 0f), Vector3.zero, new Vector3(0.42f, 0.17f, 0.42f), galv);
        C(root, "Kant nere", new Vector3(0f, 0.14f, 0f), Vector3.zero, new Vector3(0.43f, 0.012f, 0.43f), galv);
        C(root, "Kant uppe", new Vector3(0f, 0.465f, 0f), Vector3.zero, new Vector3(0.43f, 0.012f, 0.43f), galv);
        C(root, "Lock", new Vector3(0f, 0.48f, 0f), Vector3.zero, new Vector3(0.36f, 0.01f, 0.36f), galv);
        // Rör från filtret upp till fläkten
        Hose(root, "Rör filter–fläkt", new[] { W(root, 0f, 0.48f, 0f), W(root, 0f, 0.96f, 0f) }, 0.12f, hose, rib, 0.035f);

        // ---- Fläkthus (blått, axel mot rummet) ----
        float fy = tableY + 0.03f + 0.25f;
        C(root, "Fläkthus", new Vector3(0f, fy, 0.03f), new Vector3(90f, 0f, 0f), new Vector3(0.50f, 0.085f, 0.50f), blue);
        B(root, "Fläkthus utlopp", new Vector3(-0.12f, fy + 0.22f, 0.03f), new Vector3(0.20f, 0.14f, 0.17f), blue);
        B(root, "Fläns", new Vector3(-0.12f, fy + 0.30f, 0.03f), new Vector3(0.26f, 0.015f, 0.22f), blue);
        C(root, "Utloppsrör", new Vector3(-0.12f, fy + 0.34f, 0.03f), Vector3.zero, new Vector3(0.15f, 0.04f, 0.15f), galv);
        B(root, "Typskylt", new Vector3(-0.15f, fy + 0.06f, -0.06f), new Vector3(0.12f, 0.07f, 0.004f), label);
        // Kabel (svart ring) på framsidan
        C(root, "Kabelrulle", new Vector3(-0.14f, fy - 0.05f, -0.065f), new Vector3(90f, 0f, 0f), new Vector3(0.16f, 0.01f, 0.16f), dark);
        C(root, "Kabelrulle (hål)", new Vector3(-0.14f, fy - 0.05f, -0.072f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.004f, 0.12f), blue);

        // ---- Växellåda och vev ----
        var gearPos = new Vector3(0.06f, fy - 0.03f, -0.10f);
        B(root, "Växellåda", gearPos, new Vector3(0.15f, 0.17f, 0.12f), motor);
        C(root, "Växellåda lock", gearPos + new Vector3(0f, 0f, -0.065f), new Vector3(90f, 0f, 0f), new Vector3(0.13f, 0.01f, 0.13f), motor);
        B(root, "Växellåda topp", gearPos + new Vector3(0f, 0.11f, 0f), new Vector3(0.12f, 0.05f, 0.09f), motor);

        var pivot = new GameObject("Vevaxel").transform;
        pivot.SetParent(root, false);
        pivot.localPosition = gearPos + new Vector3(0f, 0f, -0.16f);
        C(root, "Axel", gearPos + new Vector3(0f, 0f, -0.11f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.05f, 0.03f), steel);
        // Vevarm (hänger nedåt-åt höger i viloläge) + handtag ut mot spelaren
        const float armLen = 0.36f;
        var arm = new GameObject("Vevarm").transform;
        arm.SetParent(pivot, false);
        arm.localRotation = Quaternion.Euler(0f, 0f, 30f);
        C(arm, "Vevarm rör", new Vector3(0f, -armLen / 2f, 0f), Vector3.zero, new Vector3(0.03f, armLen / 2f, 0.03f), steel, true);
        Sp(arm, "Nav", Vector3.zero, Vector3.one * 0.05f, steel);
        var grip = new GameObject("Handtag").transform;
        grip.SetParent(arm, false);
        grip.localPosition = new Vector3(0f, -armLen, -0.07f);
        C(grip, "Handtagsrör", Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.034f, 0.07f, 0.034f), steel, true);
        C(grip, "Ände", new Vector3(0f, 0f, -0.07f), new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.006f, 0.04f), dark, true);
        var gripCol = grip.gameObject.AddComponent<BoxCollider>();
        gripCol.size = new Vector3(0.09f, 0.09f, 0.18f);
        var interactable = grip.gameObject.AddComponent<XRSimpleInteractable>();

        var crank = rootGo.AddComponent<VentilationCrank>();
        crank.handle = interactable;
        crank.pivot = pivot;
        crank.localAxis = Vector3.forward;

        // ---- Flexslangar ----
        // Aluminiumslang från fläktens utlopp upp till takkanalen (x=-1.27, y=4.0, z=2.45)
        var outlet = W(root, -0.12f, fy + 0.38f, 0.03f);
        Hose(root, "Flexslang till takkanalen", new[] {
            outlet, outlet + new Vector3(0f, 0.15f, -0.02f), new Vector3(-1.30f, 3.93f, 2.48f), new Vector3(-1.27f, 4.05f, 2.45f) },
            0.17f, foil, foil, 0.03f, 1.08f);
        // Svart slang från filtret, längs golvet och upp på väggen (luftintag)
        Hose(root, "Slang luftintag", new[] {
            W(root, 0.21f, 0.30f, 0f), W(root, 0.33f, 0.22f, -0.02f), W(root, 0.40f, 0.07f, 0.05f),
            W(root, 0.44f, 0.07f, 0.22f), W(root, 0.45f, 0.30f, 0.25f), W(root, 0.42f, 1.30f, 0.25f),
            W(root, 0.36f, 1.65f, 0.26f), W(root, 0.30f, 1.72f, 0.28f) },
            0.10f, hose, rib, 0.05f);

        // Kollisionslåda för stativet (man kan inte gå igenom)
        var col = rootGo.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.75f, 0.02f);
        col.size = new Vector3(0.62f, 1.5f, 0.5f);

        // ---- Dölj den gamla veven ----
        var hidden = new List<string>();
        foreach (var n in OldNames)
        {
            var go = GameObject.Find(n);
            if (go == null) continue;
            Undo.RecordObject(go, "Dölj gammal vev");
            go.SetActive(false);
            hidden.Add(n);
        }

        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = rootGo;
        Log($"[Ventilation] Nytt ventilationsaggregat med vev byggt. Dolda gamla objekt: {(hidden.Count > 0 ? string.Join(", ", hidden) : "inga")}. (scenen sparad)");
    }

    [MenuItem("Tools/Skyddsrum/Ventilationsaggregat/Gör metallen ljusare (alla metallmaterial)")]
    public static void BrighterMetal()
    {
        // Utan reflektioner i scenen blir mycket metalliska material nästan svarta.
        var names = new[] {
            "Vent_Galvaniserad", "Vent_Vev", "Vent_Folie", "Ventilationskanal", "Ror_Gra", "Ror_Faste",
            "Ror_Metall", "Pinne", "Metall", "Brits_Ram", "Vent_Bla", "Dorr_Stal", "Dorr_Karm", "Balja_Zink" };
        var folders = new[] { "Assets/Skyddsrum/Material", "Assets/Toalett/Material", "Assets/Forrum/Material", "Assets/Vatten/Material" };
        int n = 0;
        foreach (var f in folders)
            foreach (var name in names)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{f}/{name}.mat");
                if (m == null || !m.HasProperty("_Metallic")) continue;
                if (m.GetFloat("_Metallic") > 0.3f) { m.SetFloat("_Metallic", 0.3f); EditorUtility.SetDirty(m); n++; }
            }
        AssetDatabase.SaveAssets();
        Log($"[Ventilation] {n} metallmaterial gjorda ljusare.");
    }

    [MenuItem("Tools/Skyddsrum/Ventilationsaggregat/Återställ gamla veven")]
    public static void Restore()
    {
        if (EditorApplication.isPlaying) { Log("[Ventilation] Stoppa Play först."); return; }
        var shown = new List<string>();
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (System.Array.IndexOf(OldNames, root.name) >= 0 && !root.activeSelf)
            {
                Undo.RecordObject(root, "Visa gammal vev");
                root.SetActive(true);
                shown.Add(root.name);
            }
            if (root.name == GroupName) Undo.DestroyObjectImmediate(root);
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Log($"[Ventilation] Gamla veven tillbaka ({string.Join(", ", shown)}), nya aggregatet borttaget. (scenen sparad)");
    }

    // ---------- Hjälpfunktioner ----------
    static Vector3 W(Transform root, float x, float y, float z) => root.TransformPoint(new Vector3(x, y, z));

    /// Korrugerad slang genom punkterna (världskoordinater). Ränder med jämna mellanrum.
    static void Hose(Transform parent, string name, Vector3[] pts, float d, Material body, Material ribMat, float ribSpacing, float ribScale = 1.15f)
    {
        var h = new GameObject(name).transform;
        h.SetParent(parent, true);
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1];
            Vector3 dir = b - a; float len = dir.magnitude; if (len < 0.001f) continue;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir / len);
            WorldPrim(PrimitiveType.Cylinder, h, "Del", (a + b) / 2f, rot, new Vector3(d, len / 2f, d), body);
            int ribs = Mathf.FloorToInt(len / ribSpacing);
            for (int k = 1; k <= ribs; k++)
                WorldPrim(PrimitiveType.Cylinder, h, "Rand", a + dir * (k * ribSpacing / len), rot, new Vector3(d * ribScale, 0.005f, d * ribScale), ribMat);
            if (i > 0) WorldPrim(PrimitiveType.Sphere, h, "Böj", a, Quaternion.identity, Vector3.one * d, body);
        }
    }

    static void WorldPrim(PrimitiveType t, Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
    }

    static GameObject Prim(PrimitiveType t, Transform p, string n, Vector3 pos, Vector3 euler, Vector3 s, Material m)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = n;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = s;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }
    static GameObject B(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Cube, p, n, pos, Vector3.zero, s, m);
    static GameObject C(Transform p, string n, Vector3 pos, Vector3 e, Vector3 s, Material m, bool unused = false) => Prim(PrimitiveType.Cylinder, p, n, pos, e, s, m);
    static GameObject Sp(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Sphere, p, n, pos, Vector3.zero, s, m);

    static void Log(string msg)
    {
        Debug.Log(msg);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }

    static Material Mat(string name, Color color, float metallic, float smoothness)
    {
        if (_mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Skyddsrum")) AssetDatabase.CreateFolder("Assets", "Skyddsrum");
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Skyddsrum", "Material");
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(m, path);
        }
        _mats[name] = m;
        return m;
    }
}
