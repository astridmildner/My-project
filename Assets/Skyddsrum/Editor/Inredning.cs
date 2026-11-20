using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Meny: Tools > Skyddsrum > Inredning
///  1. Betongytor: texturer + riktig texturmappning på väggar, golv och tak (kan återställas)
///  2. Inredning: rör i taket, britsar, bänk, brandsläckare, första hjälpen-skåp, klocka
/// </summary>
public static class Inredning
{
    const string Root = "Assets/Skyddsrum";
    const string MatFolder = Root + "/Material";
    const string TexFolder = Root + "/Texturer";
    const string MeshFolder = Root + "/Meshar";
    const float Floor = 2.10f;
    const float Ceil = 4.60f;
    static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    // =====================================================================
    // 1. BETONGYTOR
    // =====================================================================
    [MenuItem("Tools/Skyddsrum/Inredning/1. Betong på väggar, golv och tak")]
    public static void ApplySurfaces()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Inredning] Stoppa Play först."); return; }
        _mats.Clear();
        var log = new StringBuilder("[Betong] ");
        var layout = GameObject.Find("Skyddssrum_layout");
        if (layout == null) { Log("[Betong] Hittade inte Skyddssrum_layout."); return; }
        EnsureFolders();

        // Texturer (skapas en gång)
        var wallAlb = Tex("Vagg_Farg", 1024, WallColor, false);
        var wallNrm = Tex("Vagg_Normal", 1024, (u, v) => NormalFromHeight(WallHeight, u, v, 1024, 2.0f), true);
        var floorAlb = Tex("Golv_Farg", 1024, FloorColor, false);
        var floorNrm = Tex("Golv_Normal", 1024, (u, v) => NormalFromHeight(FloorHeight, u, v, 1024, 1.2f), true);
        var ceilAlb = Tex("Tak_Farg", 1024, CeilColor, false);
        var ceilNrm = Tex("Tak_Normal", 1024, (u, v) => NormalFromHeight(CeilHeight, u, v, 1024, 2.5f), true);

        var wallMat = TexMat("Betong_Vagg", wallAlb, wallNrm, 0.18f, 1.0f);
        var floorMat = TexMat("Betong_Golv", floorAlb, floorNrm, 0.42f, 0.6f);
        var ceilMat = TexMat("Betong_Tak", ceilAlb, ceilNrm, 0.1f, 1.0f);

        // Spara originalen första gången
        var orig = Object.FindFirstObjectByType<OriginalUtseende>(FindObjectsInactive.Include);
        if (orig == null)
        {
            var go = new GameObject("Utseende (original – för återställning)");
            Undo.RegisterCreatedObjectUndo(go, "Original");
            orig = go.AddComponent<OriginalUtseende>();
        }

        int n = 0;
        foreach (var r in layout.GetComponentsInChildren<MeshRenderer>(true))
        {
            string name = r.name;
            int mode; Material mat;
            if (name == "Golv") { mode = 0; mat = floorMat; }
            else if (name == "Tak") { mode = 1; mat = ceilMat; }
            else if ((name.StartsWith("Vägg_") || name.StartsWith("Yttervägg_")) && r.sharedMaterial != null && r.sharedMaterial.name != "Dörr") { mode = 2; mat = wallMat; }
            else continue;

            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (!orig.renderers.Contains(r))
            {
                orig.renderers.Add(r);
                orig.meshes.Add(mf.sharedMesh);
                orig.materials.Add(r.sharedMaterial);
            }
            int idx = orig.renderers.IndexOf(r);
            var uvMesh = MakeUVMesh(mf, orig.meshes[idx], mode);
            Undo.RecordObject(mf, "UV");
            Undo.RecordObject(r, "Material");
            mf.sharedMesh = uvMesh;
            var arr = r.sharedMaterials;
            for (int i = 0; i < arr.Length; i++) arr[i] = mat;
            r.sharedMaterials = arr;
            n++;
        }
        EditorUtility.SetDirty(orig);
        log.Append($"{n} ytor fick betong (väggar med målad sockel, golv, tak).");
        SaveScene(log.ToString());
    }

    [MenuItem("Tools/Skyddsrum/Inredning/Återställ väggar, golv och tak")]
    public static void RestoreSurfaces()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Inredning] Stoppa Play först."); return; }
        var orig = Object.FindFirstObjectByType<OriginalUtseende>(FindObjectsInactive.Include);
        if (orig == null) { Log("[Betong] Inget att återställa."); return; }
        for (int i = 0; i < orig.renderers.Count; i++)
        {
            var r = orig.renderers[i]; if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            Undo.RecordObject(mf, "Återställ"); Undo.RecordObject(r, "Återställ");
            mf.sharedMesh = orig.meshes[i];
            var arr = r.sharedMaterials; for (int k = 0; k < arr.Length; k++) arr[k] = orig.materials[i]; r.sharedMaterials = arr;
        }
        Undo.DestroyObjectImmediate(orig.gameObject);
        SaveScene("[Betong] Väggar, golv och tak återställda.");
    }

    /// mode 0 = golv, 1 = tak, 2 = vägg
    static Mesh MakeUVMesh(MeshFilter mf, Mesh src, int mode)
    {
        var m = Object.Instantiate(src);
        m.name = src.name + "_uv";
        var v = src.vertices;
        var nrm = src.normals;
        if (nrm == null || nrm.Length != v.Length) { m.RecalculateNormals(); nrm = m.normals; }
        var M = mf.transform.localToWorldMatrix;
        var uv = new Vector2[v.Length];
        float T = mode == 2 ? 2.5f : 2.0f;
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 w = M.MultiplyPoint3x4(v[i]);
            Vector3 wn = M.MultiplyVector(nrm[i]).normalized;
            float ax = Mathf.Abs(wn.x), ay = Mathf.Abs(wn.y), az = Mathf.Abs(wn.z);
            if (ay >= ax && ay >= az) uv[i] = new Vector2(w.x / T, w.z / T);
            else if (ax >= az) uv[i] = new Vector2(w.z / T, mode == 2 ? (w.y - Floor) / T : w.y / T);
            else uv[i] = new Vector2(w.x / T, mode == 2 ? (w.y - Floor) / T : w.y / T);
        }
        m.uv = uv;
        m.RecalculateTangents();
        string path = $"{MeshFolder}/{mf.gameObject.name}_uv.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ---------- Procedurella texturer (sömlösa) ----------
    static float Noise(float u, float v, float freq, int seed)
    {
        // Sömlös brus: blandar fyra förskjutna sampel
        float x = u * freq, y = v * freq, f = freq;
        float s = seed * 17.31f;
        float a = Mathf.PerlinNoise(x + s, y + s);
        float b = Mathf.PerlinNoise(x - f + s, y + s);
        float c = Mathf.PerlinNoise(x + s, y - f + s);
        float d = Mathf.PerlinNoise(x - f + s, y - f + s);
        return a * (1 - u) * (1 - v) + b * u * (1 - v) + c * (1 - u) * v + d * u * v;
    }
    static float Fbm(float u, float v, float baseFreq, int seed, int oct = 4)
    {
        float sum = 0, amp = 0.5f, freq = baseFreq, norm = 0;
        for (int i = 0; i < oct; i++) { sum += Noise(u, v, freq, seed + i) * amp; norm += amp; amp *= 0.5f; freq *= 2f; }
        return sum / norm;
    }
    static float Hash(int x, int y, int seed)
    {
        unchecked { int h = x * 374761393 + y * 668265263 + seed * 2147483647; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xFFFF) / 65535f; }
    }
    static float Pores(float u, float v, int seed, float density)
    {
        int px = Mathf.FloorToInt(u * 1024), py = Mathf.FloorToInt(v * 1024);
        return Hash(px, py, seed) > 1f - density ? 1f : 0f;
    }

    // Vägg: 1 textur = 2.5 m bred × hela vägghöjden (2.5 m). v=0 är golvet.
    static Color WallColor(float u, float v)
    {
        float y = v * 2.5f;
        Color baseC;
        if (y < 0.08f) baseC = new Color(0.30f, 0.31f, 0.30f);          // golvsockel
        else if (y < 1.10f) baseC = new Color(0.55f, 0.60f, 0.56f);     // gröngrå bröstning
        else if (y < 1.115f) baseC = new Color(0.33f, 0.36f, 0.34f);    // linje
        else baseC = new Color(0.86f, 0.85f, 0.81f);                   // ljus målad betong
        float mott = 0.92f + 0.12f * Fbm(u, v, 3f, 1);
        float dirt = 1f - 0.10f * Mathf.Clamp01(1f - y / 1.2f) * Fbm(u, v, 6f, 7);
        float p = Pores(u, v, 3, 0.004f);
        Color c = baseC * mott * dirt * (1f - 0.25f * p);
        c.a = 1f; return c;
    }
    static float WallHeight(float u, float v) => 0.6f * Fbm(u, v, 24f, 11, 3) - 0.8f * Pores(u, v, 3, 0.004f);

    // Golv: 1 textur = 2 × 2 m, målad betong med fläckar och fogar
    static Color FloorColor(float u, float v)
    {
        Color baseC = new Color(0.40f, 0.43f, 0.42f);
        float mott = 0.85f + 0.25f * Fbm(u, v, 2f, 21);
        float worn = Mathf.SmoothStep(0.55f, 0.75f, Fbm(u, v, 4f, 23)) * 0.12f;
        float fleck = Pores(u, v, 25, 0.02f) * (Hash((int)(u * 1024), (int)(v * 1024), 26) > 0.5f ? 0.25f : -0.2f);
        float joint = (u < 0.002f || u > 0.998f || v < 0.002f || v > 0.998f) ? 0.55f : 1f; // fog var 2:a meter
        Color c = (baseC * mott + new Color(worn, worn, worn)) * (1f + fleck) * joint;
        c.a = 1f; return c;
    }
    static float FloorHeight(float u, float v)
    {
        float joint = (u < 0.003f || u > 0.997f || v < 0.003f || v > 0.997f) ? -1f : 0f;
        return 0.3f * Fbm(u, v, 30f, 31, 3) + joint;
    }

    // Tak: rå betong med formbrädor (0.2 m breda) längs u
    static Color CeilColor(float u, float v)
    {
        float board = (v * 10f) % 1f;
        float line = board < 0.02f ? 0.8f : 1f;
        float boardTone = 0.95f + 0.08f * (Hash(Mathf.FloorToInt(v * 10f), 0, 41) - 0.5f);
        float grain = 0.93f + 0.1f * Fbm(u * 0.3f + v, v, 8f, 43);
        Color baseC = new Color(0.60f, 0.60f, 0.58f);
        Color c = baseC * line * boardTone * grain * (0.9f + 0.15f * Fbm(u, v, 2f, 45));
        c *= 1f - 0.3f * Pores(u, v, 47, 0.006f);
        c.a = 1f; return c;
    }
    static float CeilHeight(float u, float v)
    {
        float board = (v * 10f) % 1f;
        return (board < 0.02f ? -0.8f : 0f) + 0.4f * Fbm(u * 0.2f, v, 20f, 49, 3) - 0.7f * Pores(u, v, 47, 0.006f);
    }

    static Color NormalFromHeight(System.Func<float, float, float> h, float u, float v, int size, float strength)
    {
        float d = 1f / size;
        float hx = h((u + d) % 1f, v) - h((u - d + 1f) % 1f, v);
        float hy = h(u, (v + d) % 1f) - h(u, (v - d + 1f) % 1f);
        Vector3 n = new Vector3(-hx * strength, -hy * strength, 1f).normalized;
        return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
    }

    static Texture2D Tex(string name, int size, System.Func<float, float, Color> f, bool normal)
    {
        string path = $"{TexFolder}/{name}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false, normal);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            if (y % 128 == 0) EditorUtility.DisplayProgressBar("Skapar texturer", name, y / (float)size);
            float v = (y + 0.5f) / size;
            for (int x = 0; x < size; x++) px[y * size + x] = f((x + 0.5f) / size, v);
        }
        EditorUtility.ClearProgressBar();
        t.SetPixels(px);
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.maxTextureSize = 1024;
        imp.mipmapEnabled = true;
        if (normal) imp.textureType = TextureImporterType.NormalMap;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material TexMat(string name, Texture2D albedo, Texture2D normal, float smooth, float bump)
    {
        var m = Mat(name, Color.white, 0f, smooth);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", albedo);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", albedo);
        if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
        if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bump);
        EditorUtility.SetDirty(m);
        return m;
    }

    // =====================================================================
    // 2. INREDNING
    // =====================================================================
    [MenuItem("Tools/Skyddsrum/Inredning/2. Rör, britsar, brandsläckare, första hjälpen och klocka")]
    public static void BuildFurniture()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Inredning] Stoppa Play först."); return; }
        _mats.Clear();
        EnsureFolders();
        GameObject old;
        while ((old = GameObject.Find("Inredning")) != null) Undo.DestroyObjectImmediate(old);
        var group = new GameObject("Inredning");
        Undo.RegisterCreatedObjectUndo(group, "Inredning");
        var g = group.transform;

        BuildPipes(g);
        var frame = Mat("Brits_Ram", new Color(0.24f, 0.29f, 0.26f), 0.6f, 0.35f);
        var mattress = Mat("Madrass", new Color(0.20f, 0.27f, 0.30f), 0f, 0.15f);
        var pillow = Mat("Kudde", new Color(0.82f, 0.82f, 0.80f), 0f, 0.1f);
        var blanket = Mat("Filt", new Color(0.40f, 0.38f, 0.33f), 0f, 0.05f);
        var beds = new GameObject("Britsar").transform; beds.SetParent(g, false);
        Bunk(beds, "Brits 1", new Vector3(-3.44f, Floor, -1.92f), 0f, frame, mattress, pillow, blanket);
        Bunk(beds, "Brits 2", new Vector3(-3.44f, Floor, 0.05f), 0f, frame, mattress, pillow, blanket);
        Bunk(beds, "Brits 3", new Vector3(-2.00f, Floor, -2.50f), 90f, frame, mattress, pillow, blanket);
        Bench(g, new Vector3(-2.85f, Floor, 2.62f));

        var ext = new GameObject("Brandsläckare").transform; ext.SetParent(g, false);
        Extinguisher(ext, "Brandsläckare – skyddsrummet", new Vector3(0.41f, Floor, 2.65f), Vector3.left);
        Extinguisher(ext, "Brandsläckare – förrummet", new Vector3(2.05f, Floor, 2.74f), Vector3.back);
        FirstAid(g, new Vector3(-0.5f, Floor + 1.55f, -2.92f + 0.076f), Vector3.forward);
        Clock(g, new Vector3(-2.6f, Floor + 1.95f, 2.83f - 0.03f), Vector3.back);

        Selection.activeGameObject = group;
        SaveScene("[Inredning] Rör i taket, 3 britsar, bänk, 2 brandsläckare, första hjälpen-skåp och klocka tillagda.");
    }

    static void BuildPipes(Transform g)
    {
        var duct = Mat("Ventilationskanal", new Color(0.72f, 0.74f, 0.75f), 0.85f, 0.55f);
        var pipe = Mat("Ror_Gra", new Color(0.52f, 0.54f, 0.55f), 0.75f, 0.45f);
        var bracket = Mat("Ror_Faste", new Color(0.25f, 0.25f, 0.26f), 0.6f, 0.3f);
        var p = new GameObject("Rör i taket").transform; p.SetParent(g, false);

        // Stor ventilationskanal från ventilationsaggregatet längs rummet
        float dx = -1.27f, dy = Ceil - 0.22f, d = 0.315f;
        CylZ(p, "Ventilationskanal", dx, dy, -2.85f, 2.45f, d, duct);
        for (float z = -2.6f; z < 2.4f; z += 1.0f) CylZ(p, "Skarv", dx, dy, z - 0.02f, z + 0.02f, d + 0.02f, duct);
        Cyl(p, "Kanal ner till aggregatet", new Vector3(dx, (dy + 4.0f) / 2f, 2.45f), Vector3.zero, new Vector3(d, (dy - 4.0f) / 2f + 0.05f, d), duct);
        Ball(p, "Böj", new Vector3(dx, dy, 2.45f), d * 1.05f, duct);
        for (float z = -2.3f; z < 2.4f; z += 1.2f)
        {
            Box(p, "Upphängning", new Vector3(dx, (dy + Ceil) / 2f, z), new Vector3(0.02f, Ceil - dy, 0.02f), bracket);
            Box(p, "Bygel", new Vector3(dx, dy + d / 2f + 0.01f, z), new Vector3(d + 0.06f, 0.02f, 0.04f), bracket);
        }
        // Mindre rör
        CylX(p, "Rör längs norra väggen", -3.80f, 0.45f, Ceil - 0.18f, -2.70f, 0.11f, pipe);
        CylZ(p, "Rör längs västra väggen", -3.62f, Ceil - 0.15f, -2.85f, 2.78f, 0.08f, pipe);
        CylX(p, "Rör längs södra väggen", -3.80f, 0.45f, Ceil - 0.12f, 2.65f, 0.06f, pipe);
        CylZ(p, "Rör längs östra väggen", 0.30f, Ceil - 0.07f, -2.85f, 2.78f, 0.07f, pipe);
        for (float x = -3.4f; x < 0.4f; x += 1.0f)
        {
            Box(p, "Fäste", new Vector3(x, Ceil - 0.09f, -2.70f), new Vector3(0.025f, 0.18f, 0.025f), bracket);
            Box(p, "Fäste", new Vector3(x, Ceil - 0.06f, 2.65f), new Vector3(0.02f, 0.12f, 0.02f), bracket);
        }
        for (float z = -2.4f; z < 2.7f; z += 1.0f)
        {
            Box(p, "Fäste", new Vector3(-3.62f, Ceil - 0.075f, z), new Vector3(0.02f, 0.15f, 0.02f), bracket);
            Box(p, "Fäste", new Vector3(0.30f, Ceil - 0.02f, z), new Vector3(0.02f, 0.04f, 0.02f), bracket);
        }
    }

    static void Bunk(Transform parent, string name, Vector3 pos, float yaw, Material frame, Material mattress, Material pillow, Material blanket)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        const float W = 0.8f, L = 1.9f, H = 1.75f, P = 0.045f;
        float hx = W / 2f - P / 2f, hz = L / 2f - P / 2f;
        foreach (var (x, z) in new[] { (hx, hz), (-hx, hz), (hx, -hz), (-hx, -hz) })
            LBox(t, "Stolpe", new Vector3(x, H / 2f, z), new Vector3(P, H, P), frame);
        foreach (float y in new[] { 0.32f, 1.32f })
        {
            LBox(t, "Sidoram", new Vector3(hx, y, 0f), new Vector3(P, 0.06f, L), frame);
            LBox(t, "Sidoram", new Vector3(-hx, y, 0f), new Vector3(P, 0.06f, L), frame);
            LBox(t, "Gavel", new Vector3(0f, y, hz), new Vector3(W, 0.06f, P), frame);
            LBox(t, "Gavel", new Vector3(0f, y, -hz), new Vector3(W, 0.06f, P), frame);
            LBox(t, "Botten", new Vector3(0f, y + 0.02f, 0f), new Vector3(W - 0.08f, 0.02f, L - 0.08f), frame);
            LBox(t, "Madrass", new Vector3(0f, y + 0.085f, 0f), new Vector3(W - 0.1f, 0.11f, L - 0.1f), mattress);
            LBox(t, "Kudde", new Vector3(0f, y + 0.175f, L / 2f - 0.28f), new Vector3(0.5f, 0.08f, 0.3f), pillow);
            LBox(t, "Filt", new Vector3(0f, y + 0.155f, -L / 2f + 0.35f), new Vector3(W - 0.12f, 0.04f, 0.45f), blanket);
        }
        LBox(t, "Skyddsräcke", new Vector3(hx, 1.58f, 0.2f), new Vector3(P * 0.8f, 0.04f, L * 0.6f), frame);
        // Stege vid fotänden
        for (int i = 0; i < 3; i++) LBox(t, "Stegpinne", new Vector3(hx + 0.01f, 0.6f + i * 0.25f, -hz), new Vector3(0.03f, 0.03f, 0.03f), frame);
        var col = t.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, H / 2f, 0f);
        col.size = new Vector3(W, H, L);
    }

    static void Bench(Transform g, Vector3 pos)
    {
        var wood = Mat("Bank_Tra", new Color(0.55f, 0.42f, 0.28f), 0f, 0.3f);
        var leg = Mat("Brits_Ram", new Color(0.24f, 0.29f, 0.26f), 0.6f, 0.35f);
        var t = new GameObject("Bänk").transform; t.SetParent(g, false); t.position = pos;
        LBox(t, "Sits", new Vector3(0f, 0.45f, 0f), new Vector3(1.7f, 0.04f, 0.35f), wood);
        LBox(t, "Ben", new Vector3(-0.75f, 0.215f, 0f), new Vector3(0.05f, 0.43f, 0.3f), leg);
        LBox(t, "Ben", new Vector3(0.75f, 0.215f, 0f), new Vector3(0.05f, 0.43f, 0.3f), leg);
        var col = t.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.235f, 0f); col.size = new Vector3(1.7f, 0.47f, 0.35f);
    }

    static void Extinguisher(Transform parent, string name, Vector3 floorPos, Vector3 intoWall)
    {
        var red = Mat("Brandslackare_Rod", new Color(0.78f, 0.06f, 0.05f), 0.3f, 0.6f);
        var black = Mat("Svart_Plast", new Color(0.06f, 0.06f, 0.06f), 0.1f, 0.4f);
        var white = Mat("Etikett_Vit", new Color(0.95f, 0.95f, 0.93f), 0f, 0.3f);
        var t = new GameObject(name).transform; t.SetParent(parent, false);
        t.SetPositionAndRotation(floorPos, Quaternion.LookRotation(intoWall));
        LBox(t, "Väggfäste", new Vector3(0f, 0.75f, 0.075f), new Vector3(0.08f, 0.12f, 0.02f), black);
        LCyl(t, "Behållare", new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.15f, 0.23f, 0.15f), red);
        LCyl(t, "Etikett", new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.152f, 0.06f, 0.152f), white);
        LBall(t, "Topp", new Vector3(0f, 0.78f, 0f), new Vector3(0.15f, 0.06f, 0.15f), red);
        LCyl(t, "Ventil", new Vector3(0f, 0.83f, 0f), Vector3.zero, new Vector3(0.045f, 0.035f, 0.045f), black);
        LBox(t, "Handtag", new Vector3(0f, 0.88f, -0.03f), new Vector3(0.03f, 0.015f, 0.1f), black);
        LCyl(t, "Slang", new Vector3(-0.085f, 0.62f, -0.02f), new Vector3(0f, 0f, 8f), new Vector3(0.022f, 0.2f, 0.022f), black);
        LCyl(t, "Munstycke", new Vector3(-0.1f, 0.4f, -0.02f), Vector3.zero, new Vector3(0.03f, 0.03f, 0.03f), black);
    }

    static void FirstAid(Transform g, Vector3 pos, Vector3 facing)
    {
        var white = Mat("Skap_Vit", new Color(0.94f, 0.94f, 0.92f), 0.2f, 0.5f);
        var green = Mat("Forsta_Hjalpen_Gron", new Color(0.0f, 0.55f, 0.25f), 0f, 0.4f);
        var signWhite = Mat("Etikett_Vit", new Color(0.95f, 0.95f, 0.93f), 0f, 0.3f);
        var t = new GameObject("Första hjälpen-skåp").transform; t.SetParent(g, false);
        t.SetPositionAndRotation(pos, Quaternion.LookRotation(-facing)); // framsidan (-z) mot rummet
        LBox(t, "Skåp", Vector3.zero, new Vector3(0.40f, 0.45f, 0.15f), white);
        LBox(t, "Dörrspringa", new Vector3(0f, 0f, -0.0755f), new Vector3(0.004f, 0.43f, 0.002f), Mat("Svart_Plast", new Color(0.06f, 0.06f, 0.06f), 0.1f, 0.4f));
        LBox(t, "Grön skylt", new Vector3(0f, 0.05f, -0.077f), new Vector3(0.22f, 0.22f, 0.004f), green);
        LBox(t, "Kors lodrätt", new Vector3(0f, 0.05f, -0.0795f), new Vector3(0.05f, 0.15f, 0.002f), signWhite);
        LBox(t, "Kors vågrätt", new Vector3(0f, 0.05f, -0.0795f), new Vector3(0.15f, 0.05f, 0.002f), signWhite);
        var txt = Text(t, "FÖRSTA HJÄLPEN", new Vector3(0f, -0.15f, -0.078f), new Vector2(0.36f, 0.06f), 0.35f);
        txt.color = new Color(0f, 0.45f, 0.2f);
        txt.fontStyle = FontStyles.Bold;
    }

    static void Clock(Transform g, Vector3 pos, Vector3 facing)
    {
        var white = Mat("Klocka_Vit", new Color(0.97f, 0.97f, 0.95f), 0f, 0.6f);
        var black = Mat("Svart_Plast", new Color(0.06f, 0.06f, 0.06f), 0.1f, 0.4f);
        var red = Mat("Brandslackare_Rod", new Color(0.78f, 0.06f, 0.05f), 0.3f, 0.6f);
        var t = new GameObject("Klocka").transform; t.SetParent(g, false);
        t.SetPositionAndRotation(pos, Quaternion.LookRotation(-facing)); // urtavlan (-z) mot rummet
        LCyl(t, "Ram", Vector3.zero, new Vector3(90f, 0f, 0f), new Vector3(0.34f, 0.025f, 0.34f), black);
        LCyl(t, "Urtavla", new Vector3(0f, 0f, -0.026f), new Vector3(90f, 0f, 0f), new Vector3(0.31f, 0.002f, 0.31f), white);
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            var tick = LBox(t, "Streck", new Vector3(Mathf.Sin(a) * 0.13f, Mathf.Cos(a) * 0.13f, -0.029f),
                i % 3 == 0 ? new Vector3(0.012f, 0.03f, 0.002f) : new Vector3(0.006f, 0.018f, 0.002f), black);
            tick.transform.localEulerAngles = new Vector3(0f, 0f, -i * 30f);
        }
        Transform Hand(string n, float len, float w, float z, Material m)
        {
            var pivot = new GameObject(n).transform; pivot.SetParent(t, false); pivot.localPosition = new Vector3(0f, 0f, z);
            LBox(pivot, n + " (visare)", new Vector3(0f, len / 2f - 0.015f, 0f), new Vector3(w, len, 0.002f), m);
            return pivot;
        }
        var clock = t.gameObject.AddComponent<ClockHands>();
        clock.hourHand = Hand("Timvisare", 0.08f, 0.012f, -0.031f, black);
        clock.minuteHand = Hand("Minutvisare", 0.12f, 0.008f, -0.033f, black);
        clock.secondHand = Hand("Sekundvisare", 0.13f, 0.003f, -0.035f, red);
        LCyl(t, "Mitt", new Vector3(0f, 0f, -0.036f), new Vector3(90f, 0f, 0f), new Vector3(0.015f, 0.003f, 0.015f), black);
    }

    // =====================================================================
    // HJÄLPFUNKTIONER
    // =====================================================================
    static void SaveScene(string msg)
    {
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Log(msg + " (scenen sparad)");
    }

    static void Log(string msg)
    {
        Debug.Log(msg);
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "Skyddsrum");
        foreach (var f in new[] { "Material", "Texturer", "Meshar" })
            if (!AssetDatabase.IsValidFolder($"{Root}/{f}")) AssetDatabase.CreateFolder(Root, f);
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material m, bool world)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        if (world) go.transform.position = pos; else go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }
    // Världskoordinater (föräldrar utan rotation)
    static GameObject Box(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Cube, p, n, pos, Vector3.zero, s, m, true);
    static GameObject Cyl(Transform p, string n, Vector3 pos, Vector3 e, Vector3 s, Material m) => Prim(PrimitiveType.Cylinder, p, n, pos, e, s, m, true);
    static GameObject Ball(Transform p, string n, Vector3 pos, float d, Material m) => Prim(PrimitiveType.Sphere, p, n, pos, Vector3.zero, Vector3.one * d, m, true);
    static GameObject CylZ(Transform p, string n, float x, float y, float z0, float z1, float d, Material m)
        => Cyl(p, n, new Vector3(x, y, (z0 + z1) / 2f), new Vector3(90f, 0f, 0f), new Vector3(d, (z1 - z0) / 2f, d), m);
    static GameObject CylX(Transform p, string n, float x0, float x1, float y, float z, float d, Material m)
        => Cyl(p, n, new Vector3((x0 + x1) / 2f, y, z), new Vector3(0f, 0f, 90f), new Vector3(d, (x1 - x0) / 2f, d), m);
    // Lokala koordinater
    static GameObject LBox(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Cube, p, n, pos, Vector3.zero, s, m, false);
    static GameObject LCyl(Transform p, string n, Vector3 pos, Vector3 e, Vector3 s, Material m) => Prim(PrimitiveType.Cylinder, p, n, pos, e, s, m, false);
    static GameObject LBall(Transform p, string n, Vector3 pos, Vector3 s, Material m) => Prim(PrimitiveType.Sphere, p, n, pos, Vector3.zero, s, m, false);

    static TextMeshPro Text(Transform parent, string text, Vector3 localPos, Vector2 size, float fontSize)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = size;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.1f; tmp.fontSizeMax = fontSize;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static Material Mat(string name, Color color, float metallic, float smoothness)
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
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(m, path);
        }
        _mats[name] = m;
        return m;
    }
}
