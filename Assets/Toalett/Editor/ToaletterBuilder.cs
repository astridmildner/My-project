using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Meny: Tools > Skyddsrum > Bygg toaletter i rummet nere till höger
/// Mäter upp rummet och bygger tre toalettbås med skynken längs högra (östra) väggen,
/// plus rör i taket.
/// </summary>
public static class ToaletterBuilder
{
    const string Root = "Assets/Toalett";
    const string MatFolder = Root + "/Material";
    const string GroupName = "Toaletter";
    static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    [MenuItem("Tools/Skyddsrum/Bygg toaletter i rummet nere till höger")]
    public static void Build() => BuildAt(new Vector3(2.3f, 0f, -2.4f));

    public static void BuildAt(Vector3 probe)
    {
        _mats.Clear();
        var log = new StringBuilder("[Toaletter] ");
        GameObject old;
        while ((old = GameObject.Find(GroupName)) != null) Undo.DestroyObjectImmediate(old);
        Physics.SyncTransforms();
        var cam = Camera.main;
        Transform rig = cam != null ? cam.transform.root : null;

        // ---- Mät rummet ----
        float floor = 2.1f;
        if (Ray(new Vector3(probe.x, 3.6f, probe.z), Vector3.down, rig, out var fh)) floor = fh.point.y;
        float ceil = floor + 2.5f;
        if (Ray(new Vector3(probe.x, floor + 0.5f, probe.z), Vector3.up, rig, out var ch)) ceil = ch.point.y;
        Vector3 c = new Vector3(probe.x, floor + 1.0f, probe.z);
        float xmin = probe.x - 1.5f, xmax = probe.x + 1.5f, zmin = probe.z - 1.5f, zmax = probe.z + 1.5f;
        if (Ray(c, Vector3.left, rig, out var h1)) xmin = h1.point.x;
        if (Ray(c, Vector3.right, rig, out var h2)) xmax = h2.point.x;
        if (Ray(c, Vector3.back, rig, out var h3)) zmin = h3.point.z;
        if (Ray(c, Vector3.forward, rig, out var h4)) zmax = h4.point.z;
        log.Append($"Rum: golv={floor:F2} tak={ceil:F2} x={xmin:F2}..{xmax:F2} z={zmin:F2}..{zmax:F2}. ");

        // ---- Material ----
        var green = Mat("Skarm_Gron", new Color(0.18f, 0.32f, 0.26f), 0f, 0.2f);
        var cloth = Mat("Skynke_Vitt", new Color(0.93f, 0.93f, 0.9f), 0f, 0.1f);
        var metal = Mat("Ror_Metall", new Color(0.72f, 0.74f, 0.76f), 0.9f, 0.6f);
        var rodMat = Mat("Pinne", new Color(0.55f, 0.55f, 0.57f), 0.8f, 0.5f);
        var plastic = Mat("Hink_Vit", new Color(0.95f, 0.95f, 0.93f), 0f, 0.5f);
        var seatMat = Mat("Sits_Ljusgra", new Color(0.86f, 0.87f, 0.88f), 0f, 0.6f);
        var darkMat = Mat("Fotter_Mork", new Color(0.15f, 0.15f, 0.15f), 0.3f, 0.3f);

        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Bygg toaletter");
        var g = group.transform;

        // ---- Bås längs östra väggen ----
        const float D = 1.2f;          // djup
        const float panelH = 1.8f;     // skärmväggarnas höjd
        const float gap = 0.1f;        // glipa vid golvet
        float rodY = floor + 2.0f;
        float roomLen = zmax - zmin;
        float W = Mathf.Min(0.95f, (roomLen - 0.4f) / 3f);
        float zc = (zmin + zmax) / 2f;
        float wallX = xmax;
        float frontX = wallX - D;
        log.Append($"Båsbredd {W:F2} m. ");

        var stalls = new GameObject("Bås").transform;
        stalls.SetParent(g, false);
        for (int k = 0; k <= 3; k++)
        {
            float z = zc - 1.5f * W + k * W;
            var panel = Box(stalls, $"Skärmvägg {k + 1}", new Vector3(wallX - D / 2f, floor + gap + panelH / 2f, z), new Vector3(D, panelH, 0.03f), green, true);
            // fötter
            Box(stalls, $"Fot {k + 1}a", new Vector3(frontX + 0.08f, floor + 0.025f, z), new Vector3(0.12f, 0.05f, 0.35f), darkMat, false);
            Box(stalls, $"Fot {k + 1}b", new Vector3(wallX - 0.12f, floor + 0.025f, z), new Vector3(0.12f, 0.05f, 0.35f), darkMat, false);
            // stolpe som håller pinnen
            Cyl(stalls, $"Stolpe {k + 1}", new Vector3(frontX, floor + gap + panelH + (rodY - floor - gap - panelH) / 2f, z), Vector3.zero,
                new Vector3(0.02f, (rodY - floor - gap - panelH) / 2f + 0.02f, 0.02f), rodMat, false);
        }
        // Den smala pinnen som skynkena hänger på
        Cyl(stalls, "Pinne för skynken", new Vector3(frontX, rodY, zc), new Vector3(90f, 0f, 0f), new Vector3(0.022f, 1.5f * W + 0.03f, 0.022f), rodMat, false);

        for (int s = 0; s < 3; s++)
        {
            float z = zc - W + s * W;
            BuildCurtain(stalls, $"Skynke {s + 1}", new Vector3(frontX - 0.015f, rodY - 0.015f, z), W - 0.04f, rodY - 0.015f - (floor + 0.15f), cloth);
            BuildToilet(stalls, $"Toalett {s + 1}", new Vector3(wallX - 0.38f, floor, z), plastic, seatMat);
        }

        // ---- Rör i taket ----
        var pipes = new GameObject("Rör i taket").transform;
        pipes.SetParent(g, false);
        float len = roomLen;
        Cyl(pipes, "Rör 1 (stort)", new Vector3(wallX - 0.45f, ceil - 0.25f, zc), new Vector3(90f, 0f, 0f), new Vector3(0.14f, len / 2f, 0.14f), metal, false);
        Cyl(pipes, "Rör 2", new Vector3(wallX - 0.2f, ceil - 0.18f, zc), new Vector3(90f, 0f, 0f), new Vector3(0.08f, len / 2f, 0.08f), metal, false);
        float lenX = xmax - xmin;
        Cyl(pipes, "Rör 3 (tvärs)", new Vector3((xmin + xmax) / 2f, ceil - 0.32f, zmax - 0.35f), new Vector3(0f, 0f, 90f), new Vector3(0.1f, lenX / 2f, 0.1f), metal, false);
        // Fästen som håller rören
        for (float z = zmin + 0.5f; z < zmax - 0.3f; z += 1.0f)
        {
            Box(pipes, "Fäste", new Vector3(wallX - 0.45f, ceil - 0.09f, z), new Vector3(0.03f, 0.18f, 0.03f), metal, false);
            Box(pipes, "Fäste", new Vector3(wallX - 0.2f, ceil - 0.07f, z), new Vector3(0.025f, 0.14f, 0.025f), metal, false);
        }

        EditorSceneManager.MarkSceneDirty(group.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = group;
        log.Append("Tre bås med skynken och rör i taket byggda (scenen sparad).");
        Debug.Log(log.ToString());
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), log.ToString());
    }

    static void BuildCurtain(Transform parent, string name, Vector3 pos, float width, float height, Material clothMat)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.right)); // tyget vänt mot rummet
        var box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(width, height, 0.08f);
        box.center = new Vector3(0f, -height / 2f, 0f);

        var clothGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        clothGo.name = "Tyg";
        Object.DestroyImmediate(clothGo.GetComponent<Collider>());
        clothGo.transform.SetParent(root.transform, false);
        clothGo.GetComponent<Renderer>().sharedMaterial = clothMat;

        var rollGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rollGo.name = "Rulle";
        Object.DestroyImmediate(rollGo.GetComponent<Collider>());
        rollGo.transform.SetParent(root.transform, false);
        rollGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        rollGo.GetComponent<Renderer>().sharedMaterial = clothMat;

        var curtain = root.AddComponent<ToiletCurtain>();
        curtain.cloth = clothGo.transform;
        curtain.roll = rollGo.transform;
        curtain.width = width;
        curtain.fullHeight = height;
        // Ställ in tyget direkt så att det syns rätt i editorn
        clothGo.transform.localScale = new Vector3(width, height, 0.01f);
        clothGo.transform.localPosition = new Vector3(0f, -height / 2f, 0f);
        rollGo.transform.localScale = new Vector3(0.03f, width / 2f, 0.03f);
        rollGo.transform.localPosition = new Vector3(0f, -height, 0f);
    }

    static void BuildToilet(Transform parent, string name, Vector3 pos, Material plastic, Material seat)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = pos;
        // Hink
        Cyl(t, "Hink", new Vector3(0f, 0.19f, 0f), Vector3.zero, new Vector3(0.33f, 0.19f, 0.33f), plastic, true, true);
        Cyl(t, "Kant", new Vector3(0f, 0.385f, 0f), Vector3.zero, new Vector3(0.36f, 0.01f, 0.36f), plastic, false, true);
        // Handtag på sidorna
        Box(t, "Handtag V", new Vector3(0f, 0.33f, 0.185f), new Vector3(0.08f, 0.02f, 0.03f), plastic, false, true);
        Box(t, "Handtag H", new Vector3(0f, 0.33f, -0.185f), new Vector3(0.08f, 0.02f, 0.03f), plastic, false, true);
        // Sits och uppfällt lock (mot väggen)
        Cyl(t, "Sits", new Vector3(-0.01f, 0.405f, 0f), Vector3.zero, new Vector3(0.38f, 0.012f, 0.36f), seat, false, true);
        Cyl(t, "Lock", new Vector3(0.19f, 0.6f, 0f), new Vector3(0f, 0f, 80f), new Vector3(0.38f, 0.01f, 0.36f), seat, false, true);
    }

    // ---------- Hjälpfunktioner ----------
    static bool Ray(Vector3 o, Vector3 d, Transform rig, out RaycastHit hit)
    {
        hit = default; float best = float.MaxValue; bool any = false;
        foreach (var h in Physics.RaycastAll(o, d, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (rig != null && h.transform.IsChildOf(rig)) continue;
            if (h.distance < best) { best = h.distance; hit = h; any = true; }
        }
        return any;
    }

    // local = false: pos är en världsposition. local = true: pos är relativ till parent.
    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider, bool local = false)
        => Prim(PrimitiveType.Cube, parent, name, pos, Vector3.zero, scale, m, collider, !local);

    static GameObject Cyl(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material m, bool collider, bool local = false)
        => Prim(PrimitiveType.Cylinder, parent, name, pos, euler, scale, m, collider, !local);

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material m, bool collider, bool worldPos)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        if (worldPos) go.transform.position = pos; else go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Material Mat(string name, Color color, float metallic, float smoothness)
    {
        if (_mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "Toalett");
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder(Root, "Material");
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
