using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Meny: Tools > Skyddsrum > Skyddsrumsdörr > ...
/// Bygger en tjock, verklighetstrogen skyddsrumsdörr (målat stål, rundade hörn, tre gångjärn,
/// karmfläns) med två handtag på insidan märkta Ö/S. Den gamla dörren döljs och kan återställas.
/// Dörren fungerar som innan (öppnas när man packat rätt) – handtagen används inifrån.
/// </summary>
public static class SkyddsrumsdorrBuilder
{
    const string MatFolder = "Assets/Skyddsrum/Material";
    const string MeshFolder = "Assets/Skyddsrum/Modeller";
    const string NewHingeName = "Ny dörr – gångjärn";
    const string NewFixedName = "Ny dörr – karm och tappar";
    const string OldHingeName = "Gångjärn";

    // Mått (meter). Dörröppningen är 1.0 × 2.05.
    const float OW = 1.0f, OH = 2.05f;
    const float W = OW + 0.10f;      // dörrbladet täcker öppningen + 5 cm runt om
    const float LeafBottom = 0.02f, LeafTop = OH + 0.07f;
    const float T = 0.10f;           // tjocklek
    const float Corner = 0.09f;      // hörnradie

    static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    [MenuItem("Tools/Skyddsrum/Skyddsrumsdörr/Bygg verklighetstrogen dörr")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Log("[Dörr] Stoppa Play först."); return; }
        var door = Object.FindFirstObjectByType<ShelterDoor>(FindObjectsInactive.Include);
        if (door == null) { Log("[Dörr] Hittade ingen ShelterDoor i scenen."); return; }
        _mats.Clear();
        var root = door.transform;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Ny skyddsrumsdörr");

        // Ta bort en tidigare byggd ny dörr (men flytta tillbaka skylten först)
        var oldHinge = root.Find(OldHingeName);
        var prevNew = root.Find(NewHingeName);
        if (prevNew != null) { MoveSign(door, prevNew, oldHinge); Object.DestroyImmediate(prevNew.gameObject); }
        var prevFixed = root.Find(NewFixedName);
        if (prevFixed != null) Object.DestroyImmediate(prevFixed.gameObject);

        var paint = Mat("Dorr_Kaki", new Color(0.86f, 0.78f, 0.55f), 0.15f, 0.62f, true);
        var letters = Mat("Dorr_Markning", new Color(0.13f, 0.10f, 0.07f), 0f, 0.3f, true);
        var plate = Mat("Dorr_Skylt_Metall", new Color(0.80f, 0.80f, 0.82f), 0.9f, 0.6f);

        // Var sitter väggens insida? (mät med en stråle, annars 0.10)
        float wallZ = MeasureWallZ(root);
        float back = wallZ + 0.012f, front = back + T;
        var pivot = new Vector3(-W / 2f - 0.03f, 0f, front + 0.025f);

        // ---------- Fasta delar: karmfläns och gångjärnstappar ----------
        var fixedT = new GameObject(NewFixedName).transform;
        fixedT.SetParent(root, false);
        float fz = wallZ + 0.006f;
        Box(fixedT, "Karmfläns_V", new Vector3(OW / 2f + 0.065f, (OH + 0.12f) / 2f, fz), new Vector3(0.13f, OH + 0.12f, 0.012f), paint);
        Box(fixedT, "Karmfläns_H", new Vector3(-OW / 2f - 0.065f, (OH + 0.12f) / 2f, fz), new Vector3(0.13f, OH + 0.12f, 0.012f), paint);
        Box(fixedT, "Karmfläns_Topp", new Vector3(0f, OH + 0.06f, fz), new Vector3(OW + 0.26f, 0.12f, 0.012f), paint);
        float[] hingeY = { 0.32f, 1.0f, 1.70f };
        foreach (float y in hingeY)
        {
            Cyl(fixedT, "Gångjärnstapp", pivot + new Vector3(0f, y, 0f), Vector3.zero, new Vector3(0.045f, 0.065f, 0.045f), paint);
            Box(fixedT, "Gångjärnsfäste", new Vector3(pivot.x - 0.012f, y, (wallZ + pivot.z) / 2f), new Vector3(0.03f, 0.10f, pivot.z - wallZ), paint);
        }

        // ---------- Rörliga delar: dörrblad på gångjärn ----------
        var hinge = new GameObject(NewHingeName).transform;
        hinge.SetParent(root, false);
        hinge.localPosition = pivot;
        // Dörrbladets framsida (mot skyddsrummet) i gångjärnets koordinater:
        float fl = front - pivot.z;              // = -0.025
        float leafX0 = 0.03f;                    // gångjärnssidan
        float cx = leafX0 + W / 2f, cy = (LeafBottom + LeafTop) / 2f, H = LeafTop - LeafBottom;

        var leaf = new GameObject("Dörrblad");
        leaf.transform.SetParent(hinge, false);
        leaf.transform.localPosition = new Vector3(cx, cy, fl - T / 2f);
        leaf.AddComponent<MeshFilter>().sharedMesh = SaveMesh(RoundedSlab(W, H, T, Corner, 8), "Dorrblad_Skyddsrum");
        leaf.AddComponent<MeshRenderer>().sharedMaterial = paint;
        leaf.AddComponent<BoxCollider>().size = new Vector3(W, H, T);

        // Gångjärnsband på dörrbladet
        foreach (float y in hingeY)
            Box(hinge, "Gångjärnsband", new Vector3(0.10f, y, fl + 0.006f), new Vector3(0.17f, 0.075f, 0.012f), paint);

        // Liten metallskylt (som på bilden)
        Box(hinge, "Typskylt", new Vector3(cx + 0.05f, 1.80f, fl + 0.002f), new Vector3(0.07f, 0.025f, 0.004f), plate);

        // Handtag med Ö / S och pilar. Handtagssidan är längst från gångjärnen.
        float hx = leafX0 + W - 0.10f;
        var arrowMesh = SaveMesh(ArcArrow(0.36f, 12f, 78f, 0.009f), "Dorr_Pil");
        var top = BuildHandle(hinge, door, "Övre handtag", new Vector3(hx, 1.38f, fl), paint, letters, arrowMesh);
        var bottom = BuildHandle(hinge, door, "Nedre handtag", new Vector3(hx, 0.40f, fl), paint, letters, arrowMesh);

        // Flytta låsskylten (på förrummets sida) till nya dörrbladets baksida
        MoveSign(door, hinge, null, back - pivot.z);

        if (oldHinge != null) oldHinge.gameObject.SetActive(false);
        door.hinge = hinge;
        door.handles = new[] { top, bottom };
        EditorUtility.SetDirty(door);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Log($"[Dörr] Ny skyddsrumsdörr byggd (väggens insida z={wallZ:0.000}). Gamla dörrbladet dolt. (scenen sparad)");
    }

    [MenuItem("Tools/Skyddsrum/Skyddsrumsdörr/Återställ gamla dörren")]
    public static void Restore()
    {
        if (EditorApplication.isPlaying) { Log("[Dörr] Stoppa Play först."); return; }
        var door = Object.FindFirstObjectByType<ShelterDoor>(FindObjectsInactive.Include);
        if (door == null) return;
        var root = door.transform;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Återställ dörr");
        var oldHinge = root.Find(OldHingeName);
        var nh = root.Find(NewHingeName);
        if (nh != null) { MoveSign(door, nh, oldHinge); Object.DestroyImmediate(nh.gameObject); }
        var nf = root.Find(NewFixedName);
        if (nf != null) Object.DestroyImmediate(nf.gameObject);
        if (oldHinge != null) { oldHinge.gameObject.SetActive(true); door.hinge = oldHinge; }
        door.handles = new ShelterDoorHandle[0];
        EditorUtility.SetDirty(door);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Log("[Dörr] Gamla dörren återställd. (scenen sparad)");
    }

    // ---------- Test under Play ----------
    [MenuItem("Tools/Skyddsrum/Skyddsrumsdörr/Test under Play: lås upp dörren")]
    static void TestUnlock()
    {
        var d = PlayDoor(); if (d == null) return;
        d.DebugUnlock();
        Log("[Dörr] Dörren upplåst (test).");
    }

    [MenuItem("Tools/Skyddsrum/Skyddsrumsdörr/Test under Play: tryck på övre handtaget")]
    static void TestTop() => PressHandle(0);

    [MenuItem("Tools/Skyddsrum/Skyddsrumsdörr/Test under Play: tryck på nedre handtaget")]
    static void TestBottom() => PressHandle(1);

    static void PressHandle(int i)
    {
        var d = PlayDoor(); if (d == null) return;
        if (d.handles == null || d.handles.Length <= i || d.handles[i] == null) { Log("[Dörr] Handtaget saknas – bygg dörren först."); return; }
        d.handles[i].Press();
        Log($"[Dörr] Tryckte på handtag {i + 1}. Stängd: {d.IsLeafClosed}, tät: {d.IsSealed}");
    }

    static ShelterDoor PlayDoor()
    {
        if (!EditorApplication.isPlaying) { Log("[Dörr] Starta Play först."); return null; }
        return Object.FindFirstObjectByType<ShelterDoor>();
    }

    // ---------- Delar ----------
    static ShelterDoorHandle BuildHandle(Transform hinge, ShelterDoor door, string name, Vector3 pos, Material paint, Material letters, Mesh arrow)
    {
        const float L = 0.30f;
        var root = new GameObject(name);
        root.transform.SetParent(hinge, false);
        root.transform.localPosition = pos;

        // Nav (fäste i dörren)
        Cyl(root.transform, "Nav", new Vector3(0f, 0f, 0.012f), new Vector3(90f, 0f, 0f), new Vector3(0.065f, 0.012f, 0.065f), paint);

        // Hävarmen som vrids
        var lever = new GameObject("Hävarm").transform;
        lever.SetParent(root.transform, false);
        lever.localPosition = new Vector3(0f, 0f, 0.034f);
        var arm = Box(lever, "Arm", new Vector3(0f, L / 2f - 0.02f, 0f), new Vector3(0.042f, L, 0.032f), paint);
        var knob = Sphere(lever, "Knopp", Vector3.zero, new Vector3(0.07f, 0.07f, 0.05f), paint);
        var grip = Cyl(lever, "Grepp", new Vector3(0f, L - 0.03f, 0f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), paint);
        var col = lever.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, L / 2f - 0.02f, 0f);
        col.size = new Vector3(0.10f, L + 0.08f, 0.09f);

        // Märkning Ö och S med böjd pil (målat på dörren)
        Letter(root.transform, "Ö", Polar(0.39f, -4f), letters);
        Letter(root.transform, "S", Polar(0.34f, 100f), letters);
        var a = new GameObject("Pil Ö till S");
        a.transform.SetParent(root.transform, false);
        a.transform.localPosition = new Vector3(0f, 0f, 0.0015f);
        a.AddComponent<MeshFilter>().sharedMesh = arrow;
        a.AddComponent<MeshRenderer>().sharedMaterial = letters;

        var h = root.AddComponent<ShelterDoorHandle>();
        h.door = door;
        h.lever = lever;
        h.highlight = new[] { arm.GetComponent<Renderer>(), knob.GetComponent<Renderer>(), grip.GetComponent<Renderer>() };
        var si = root.GetComponent<XRSimpleInteractable>();
        if (si == null) si = root.AddComponent<XRSimpleInteractable>();
        return h;
    }

    /// <summary>Punkt på en cirkel runt handtagets nav. 0° = rakt upp, 90° = mot dörrens mitt (S).</summary>
    static Vector3 Polar(float r, float deg)
    {
        float a = deg * Mathf.Deg2Rad;
        return new Vector3(-r * Mathf.Sin(a), r * Mathf.Cos(a), 0f);
    }

    static void Letter(Transform parent, string s, Vector3 pos, Material mat)
    {
        var go = new GameObject("Märkning " + s);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos + new Vector3(0f, 0f, 0.002f);
        go.transform.localEulerAngles = new Vector3(0f, 180f, 0f); // läsbar inifrån skyddsrummet
        var t = go.AddComponent<TextMeshPro>();
        t.text = s;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(0.13f, 0.10f, 0.07f);
        t.rectTransform.sizeDelta = new Vector2(0.09f, 0.09f);
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.05f;
        t.fontSizeMax = 2f;
        t.textWrappingMode = TextWrappingModes.NoWrap;
    }

    static void MoveSign(ShelterDoor door, Transform to, Transform fallback, float zInHinge = float.NaN)
    {
        var target = to != null ? to : fallback;
        if (target == null) return;
        var root = door.transform;
        var sign = door.sign != null ? door.sign.transform : null;
        var board = FindDeep(root, "Skylt");
        foreach (var t in new[] { board, sign })
        {
            if (t == null) continue;
            var dest = float.IsNaN(zInHinge) ? fallback : to;
            if (dest == null) continue;
            t.SetParent(dest, false);
            if (float.IsNaN(zInHinge))
            {
                // tillbaka till gamla dörrbladet
                bool isBoard = t == board;
                t.localPosition = dest.InverseTransformPoint(root.TransformPoint(new Vector3(0f, 1.55f, isBoard ? -0.02f : -0.035f)));
            }
            else
            {
                bool isBoard = t == board;
                // på nya dörrbladets baksida (mot förrummet)
                Vector3 p = dest.InverseTransformPoint(root.TransformPoint(new Vector3(0f, 1.55f, 0f)));
                p.z = zInHinge - (isBoard ? 0.011f : 0.024f);
                t.localPosition = p;
            }
            t.localRotation = Quaternion.identity;
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        foreach (Transform c in t)
        {
            if (c.name == name) return c;
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    static float MeasureWallZ(Transform root)
    {
        var hits = new List<float>();
        foreach (float x in new[] { -0.85f, 0.85f })
        foreach (float y in new[] { 0.8f, 1.5f })
        {
            Vector3 o = root.TransformPoint(new Vector3(x, y, 1.0f));
            foreach (var h in Physics.RaycastAll(o, -root.forward, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(root)) continue;
                float z = root.InverseTransformPoint(h.point).z;
                if (z > 0.03f && z < 0.25f) { hits.Add(z); break; }
            }
        }
        if (hits.Count == 0) return 0.10f;
        hits.Sort();
        return hits[hits.Count - 1];
    }

    // ---------- Mesher ----------
    static Mesh RoundedSlab(float w, float h, float t, float r, int seg)
    {
        var o = new List<Vector2>();
        var c = new[] { new Vector2(w / 2 - r, h / 2 - r), new Vector2(-w / 2 + r, h / 2 - r), new Vector2(-w / 2 + r, -h / 2 + r), new Vector2(w / 2 - r, -h / 2 + r) };
        for (int k = 0; k < 4; k++)
            for (int i = 0; i <= seg; i++)
            {
                float a = (90f * k + 90f * i / seg) * Mathf.Deg2Rad;
                o.Add(c[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        int n = o.Count;
        var v = new List<Vector3>(); var nr = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        float hz = t / 2f;
        // Fram (+z) och bak (-z)
        foreach (int side in new[] { 1, -1 })
        {
            int b = v.Count;
            v.Add(new Vector3(0, 0, hz * side)); nr.Add(new Vector3(0, 0, side)); uv.Add(new Vector2(0.5f, 0.5f));
            foreach (var p in o) { v.Add(new Vector3(p.x, p.y, hz * side)); nr.Add(new Vector3(0, 0, side)); uv.Add(new Vector2(p.x / w + 0.5f, p.y / h + 0.5f)); }
            for (int i = 0; i < n; i++)
            {
                int a = b + 1 + i, d = b + 1 + (i + 1) % n;
                if (side > 0) { tr.Add(b); tr.Add(a); tr.Add(d); } else { tr.Add(b); tr.Add(d); tr.Add(a); }
            }
        }
        // Kanter
        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = o[i], p1 = o[(i + 1) % n];
            Vector2 e = (p1 - p0).normalized;
            var nn = new Vector3(e.y, -e.x, 0f);
            int b = v.Count;
            v.Add(new Vector3(p0.x, p0.y, hz)); v.Add(new Vector3(p1.x, p1.y, hz)); v.Add(new Vector3(p1.x, p1.y, -hz)); v.Add(new Vector3(p0.x, p0.y, -hz));
            for (int k = 0; k < 4; k++) { nr.Add(nn); uv.Add(new Vector2(k < 2 ? 0 : 1, (k == 1 || k == 2) ? 1 : 0)); }
            tr.Add(b); tr.Add(b + 2); tr.Add(b + 1);
            tr.Add(b); tr.Add(b + 3); tr.Add(b + 2);
        }
        var m = new Mesh { name = "Dörrblad" };
        m.SetVertices(v); m.SetNormals(nr); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    /// <summary>Böjd pil (båge + pilspets) i xy-planet, synlig från båda håll.</summary>
    static Mesh ArcArrow(float r, float fromDeg, float toDeg, float width)
    {
        var v = new List<Vector3>(); var tr = new List<int>();
        int seg = 20;
        for (int i = 0; i <= seg; i++)
        {
            float d = Mathf.Lerp(fromDeg, toDeg, i / (float)seg);
            v.Add(Polar(r - width / 2f, d)); v.Add(Polar(r + width / 2f, d));
            if (i > 0) { int b = v.Count - 4; Quad(tr, b, b + 1, b + 3, b + 2); }
        }
        float a = toDeg * Mathf.Deg2Rad;
        var tangent = new Vector3(-Mathf.Cos(a), -Mathf.Sin(a), 0f);
        int t0 = v.Count;
        v.Add(Polar(r - 0.022f, toDeg)); v.Add(Polar(r + 0.022f, toDeg)); v.Add(Polar(r, toDeg) + tangent * 0.04f);
        tr.Add(t0); tr.Add(t0 + 1); tr.Add(t0 + 2); tr.Add(t0); tr.Add(t0 + 2); tr.Add(t0 + 1);
        var m = new Mesh { name = "Pil" };
        m.SetVertices(v); m.SetTriangles(tr, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static void Quad(List<int> tr, int a, int b, int c, int d)
    {
        tr.Add(a); tr.Add(b); tr.Add(c); tr.Add(a); tr.Add(c); tr.Add(d);
        tr.Add(a); tr.Add(c); tr.Add(b); tr.Add(a); tr.Add(d); tr.Add(c);
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Skyddsrum")) AssetDatabase.CreateFolder("Assets", "Skyddsrum");
        if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/Skyddsrum", "Modeller");
        string path = $"{MeshFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(m, existing); existing.name = name; EditorUtility.SetDirty(existing); return existing; }
        m.name = name;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---------- Hjälpfunktioner ----------
    static GameObject Prim(PrimitiveType type, Transform p, string n, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = n;
        go.transform.SetParent(p, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static GameObject Box(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Cube, p, n, pos, Vector3.zero, s, m);
    static GameObject Cyl(Transform p, string n, Vector3 pos, Vector3 e, Vector3 s, Material m) => Prim(PrimitiveType.Cylinder, p, n, pos, e, s, m);
    static GameObject Sphere(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Sphere, p, n, pos, Vector3.zero, s, m);

    static void Log(string msg)
    {
        Debug.Log(msg);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }

    static Material Mat(string name, Color color, float metallic, float smoothness, bool twoSided = false)
    {
        if (_mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Skyddsrum")) AssetDatabase.CreateFolder("Assets", "Skyddsrum");
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Skyddsrum", "Material");
            Shader shader = GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (twoSided && m.HasProperty("_Cull")) { m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; }
        EditorUtility.SetDirty(m);
        _mats[name] = m;
        return m;
    }
}
