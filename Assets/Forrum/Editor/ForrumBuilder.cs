using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Meny: Tools > Förrum > Bygg förrum
/// Bygger förrummet framför spelarens startplats: hylla med saker, packbord med ryggsäck,
/// checklista, incheckningshylla och en låst dörr. Flytta sedan gruppen "Förrum" så att
/// dörren hamnar i ingången till skyddsrummet.
/// </summary>
public static class ForrumBuilder
{
    const string Root = "Assets/Forrum";
    const string MatFolder = Root + "/Material";
    const string GroupName = "Förrum";

    static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
    static float _shelfWidth = 1.8f;
    static float _doorWidth = 0.9f;
    static bool _tempWalls = true;

    // ---------------- Förrummet uppe till höger ----------------
    [MenuItem("Tools/Förrum/Bygg förrum i rummet uppe till höger")]
    public static void BuildInRoom()
    {
        _shelfWidth = 1.6f; _doorWidth = 1.0f; _tempWalls = false;
        try { Build(); }
        finally { _shelfWidth = 1.8f; _doorWidth = 0.9f; _tempWalls = true; }

        // Rummet: golv y=2.10, x 0.70–3.95, z 0.84–2.83.
        // Dörröppning till skyddsrummet i västväggen (x≈0.60) z 1.31–2.37. Ingång utifrån i norr x 2.75–3.65.
        const float F = 2.10f;
        var g = GameObject.Find(GroupName).transform;
        g.SetPositionAndRotation(new Vector3(0f, F, 0f), Quaternion.identity);
        Place(g, "Skyddsrumsdörr", new Vector3(0.60f, F, 1.84f), Vector3.left);
        Place(g, "Incheckningshylla", new Vector3(0.98f, F, 2.55f), Vector3.left);
        Place(g, "Hylla med saker", new Vector3(3.73f, F, 1.75f), Vector3.right);
        Place(g, "Packbord", new Vector3(2.3f, F, 1.75f), Vector3.left);
        Place(g, "Ryggsäck", new Vector3(2.3f, F + 0.78f, 1.75f), Vector3.left);
        Place(g, "Checklista", new Vector3(2.3f, F, 0.98f), Vector3.back);
        Place(g, "Hund", new Vector3(3.0f, F + 0.01f, 1.1f), Vector3.left);
        var suitcase = FindDeep(g, "Stor resväska");
        if (suitcase != null)
        {
            suitcase.SetParent(g, true);
            suitcase.SetPositionAndRotation(new Vector3(1.35f, F + 0.01f, 1.05f), Quaternion.LookRotation(Vector3.right));
        }

        // Dölj rummodellens öppna dörr – den låsbara dörren tar dess plats
        var oldDoor = GameObject.Find("Dörr_V1_vid_rum");
        if (oldDoor != null)
        {
            Undo.RecordObject(oldDoor, "Dölj gammal dörr");
            oldDoor.SetActive(false);
        }
        EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
        Debug.Log("[Förrum] Byggt i rummet uppe till höger.");
    }

    static void Place(Transform g, string name, Vector3 worldPos, Vector3 forward)
    {
        var t = g.Find(name);
        if (t == null) { Debug.LogWarning("[Förrum] Hittade inte " + name); return; }
        t.SetPositionAndRotation(worldPos, Quaternion.LookRotation(forward.normalized, Vector3.up));
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }

    // ---------------- Huvudmeny ----------------
    [MenuItem("Tools/Förrum/Bygg förrum")]
    public static void Build()
    {
        _mats.Clear();
        GameObject old;
        while ((old = GameObject.Find(GroupName)) != null) Undo.DestroyObjectImmediate(old);
        Physics.SyncTransforms();

        // Placering: framför spelaren, på golvet
        var cam = Camera.main;
        Vector3 player = cam != null ? cam.transform.position : Vector3.zero;
        Transform rig = cam != null ? cam.transform.root : null;
        float floorY = rig != null ? rig.position.y : 0f;
        float best = float.MaxValue;
        foreach (var h in Physics.RaycastAll(player + Vector3.up * 0.3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (rig != null && h.transform.IsChildOf(rig)) continue;
            if (h.normal.y < 0.7f) continue;
            if (h.distance < best) { best = h.distance; floorY = h.point.y; }
        }
        Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
        fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize();

        var group = new GameObject(GroupName);
        group.transform.SetPositionAndRotation(new Vector3(player.x, floorY, player.z) + fwd * 1.0f, Quaternion.LookRotation(fwd));
        var g = group.transform;

        // Material
        var wood = Mat("Tra", new Color(0.55f, 0.4f, 0.25f));
        var darkWood = Mat("Tra_Mork", new Color(0.35f, 0.25f, 0.15f));
        var board = Mat("Tavla", new Color(0.12f, 0.16f, 0.14f));
        var doorMat = Mat("Dorr_Stal", new Color(0.36f, 0.42f, 0.38f), 0.7f, 0.4f);
        var frameMat = Mat("Dorr_Karm", new Color(0.25f, 0.27f, 0.28f), 0.6f, 0.4f);
        var wallMat = Mat("Vagg_Tillfallig", new Color(0.78f, 0.78f, 0.75f));
        var lampMat = Mat("Lampa", new Color(0.9f, 0.7f, 0.1f), 0f, 0.8f);
        var panelMat = Mat("Lista_Rad", new Color(0.15f, 0.18f, 0.22f));

        // ---- Hylla med saker ----
        var shelf = BuildShelf(g, new Vector3(-1.4f, 0f, 1.3f), wood);
        PlaceItems(shelf, g);

        // ---- Packbord + ryggsäck ----
        var table = BuildTable(g, "Packbord", new Vector3(0.1f, 0f, 0.6f), new Vector3(1.0f, 0.75f, 0.6f), darkWood);
        var backpack = BuildBackpack(g, new Vector3(0.1f, 0.78f, 0.6f), panelMat);

        // ---- Checklista ----
        var listGo = new GameObject("Checklista");
        listGo.transform.SetParent(g, false);
        listGo.transform.localPosition = new Vector3(0.1f, 0f, 1.25f);
        Prim(PrimitiveType.Cube, "Ben", listGo.transform, new Vector3(0, 0.85f, 0.03f), Vector3.zero, new Vector3(0.06f, 1.7f, 0.06f), frameMat, true);
        Prim(PrimitiveType.Cube, "Tavla", listGo.transform, new Vector3(0, 2.05f, 0), Vector3.zero, new Vector3(0.9f, 0.7f, 0.03f), board, true);
        var listText = Text(listGo.transform, "Text", new Vector3(0, 2.05f, -0.02f), new Vector2(0.82f, 0.64f), 0.42f, "Packa för skyddsrummet");
        var checklist = listGo.AddComponent<ChecklistBoard>();
        checklist.backpack = backpack;
        checklist.text = listText;

        // ---- Incheckningshylla ----
        var checkGo = new GameObject("Incheckningshylla");
        checkGo.transform.SetParent(g, false);
        checkGo.transform.localPosition = new Vector3(1.3f, 0f, 1.2f);
        BuildTable(checkGo.transform, "Hylla", Vector3.zero, new Vector3(0.55f, 0.85f, 0.45f), wood);
        var zone = checkGo.AddComponent<BoxCollider>();
        zone.center = new Vector3(0, 1.1f, 0);
        zone.size = new Vector3(0.55f, 0.5f, 0.45f);
        zone.isTrigger = true;
        zone.enabled = false; // används bara som område
        Prim(PrimitiveType.Cube, "Skyltstolpe", checkGo.transform, new Vector3(0.24f, 1.2f, 0.18f), Vector3.zero, new Vector3(0.04f, 0.7f, 0.04f), frameMat, true);
        Prim(PrimitiveType.Cube, "Skylt", checkGo.transform, new Vector3(0, 1.62f, 0.18f), Vector3.zero, new Vector3(0.6f, 0.26f, 0.02f), board, true);
        var checkText = Text(checkGo.transform, "Skylttext", new Vector3(0, 1.62f, 0.165f), new Vector2(0.56f, 0.24f), 0.26f, "Incheckning");
        var checkLamp = Prim(PrimitiveType.Sphere, "Lampa", checkGo.transform, new Vector3(0, 1.82f, 0.18f), Vector3.zero, Vector3.one * 0.07f, lampMat);
        var check = checkGo.AddComponent<CheckShelf>();
        check.backpack = backpack;
        check.zone = zone;
        check.minCorrect = 5;
        check.sign = checkText;
        check.lamp = checkLamp.GetComponent<Renderer>();
        checklist.checkShelf = check;

        // ---- Dörr ----
        var door = BuildDoor(g, new Vector3(2.3f, 0f, 1.5f), doorMat, frameMat, wallMat, lampMat, board);
        door.backpack = backpack;
        door.checkShelf = check;

        Undo.RegisterCreatedObjectUndo(group, "Bygg förrum");
        EditorSceneManager.MarkSceneDirty(group.scene);
        Selection.activeGameObject = group;
        Debug.Log($"[Förrum] Byggt vid {group.transform.position}. Flytta gruppen '{GroupName}' så att dörren hamnar i ingången till skyddsrummet.");
    }

    // ---------------- Sakerna ----------------
    struct P
    {
        public PrimitiveType t; public Vector3 pos, rot, scale; public Color c; public bool glow;
        public P(PrimitiveType t, Vector3 pos, Vector3 rot, Vector3 scale, Color c, bool glow = false)
        { this.t = t; this.pos = pos; this.rot = rot; this.scale = scale; this.c = c; this.glow = glow; }
    }

    class ItemDef
    {
        public string name, category, whyNot; public bool allowed; public float mass = 0.3f;
        public P[] parts; public bool light;
    }

    static P Cyl(float x, float y, float z, float d, float h, Color c, Vector3 rot = default) => new P(PrimitiveType.Cylinder, new Vector3(x, y, z), rot, new Vector3(d, h / 2f, d), c);
    static P Box(float x, float y, float z, float sx, float sy, float sz, Color c) => new P(PrimitiveType.Cube, new Vector3(x, y, z), Vector3.zero, new Vector3(sx, sy, sz), c);
    static P Ball(float x, float y, float z, float sx, float sy, float sz, Color c, bool glow = false) => new P(PrimitiveType.Sphere, new Vector3(x, y, z), Vector3.zero, new Vector3(sx, sy, sz), c, glow);

    static ItemDef Ok(string name, string cat, params P[] parts) => new ItemDef { name = name, category = cat, allowed = true, parts = parts };
    static ItemDef No(string name, string why, params P[] parts) => new ItemDef { name = name, whyNot = why, allowed = false, parts = parts };

    static readonly Color Black = new Color(0.08f, 0.08f, 0.08f), White = new Color(0.95f, 0.95f, 0.95f), Grey = new Color(0.5f, 0.5f, 0.52f),
        Silver = new Color(0.78f, 0.78f, 0.8f), Red = new Color(0.8f, 0.1f, 0.1f), Yellow = new Color(0.95f, 0.8f, 0.1f),
        Blue = new Color(0.2f, 0.4f, 0.85f), LightBlue = new Color(0.6f, 0.8f, 0.95f), Brown = new Color(0.45f, 0.28f, 0.15f),
        Green = new Color(0.2f, 0.5f, 0.25f), Navy = new Color(0.12f, 0.16f, 0.35f), Orange = new Color(0.95f, 0.5f, 0.1f),
        Pink = new Color(0.95f, 0.6f, 0.75f), Tan = new Color(0.8f, 0.65f, 0.4f), Cyan = new Color(0.2f, 0.75f, 0.8f), DogBrown = new Color(0.6f, 0.4f, 0.2f);

    // Rätt saker
    static ItemDef Ficklampa() => Ok("Ficklampa", "Ficklampa", Cyl(0, 0.07f, 0, 0.035f, 0.14f, Black), Cyl(0, 0.155f, 0, 0.05f, 0.03f, Yellow));
    static ItemDef Vattenflaska() => Ok("Vattenflaska", "Vatten och mugg", Cyl(0, 0.11f, 0, 0.07f, 0.22f, LightBlue), Cyl(0, 0.23f, 0, 0.03f, 0.02f, Blue));
    static ItemDef Mugg() => Ok("Mugg / kåsa", "Vatten och mugg", Cyl(0, 0.045f, 0, 0.08f, 0.09f, Brown), Box(0.05f, 0.045f, 0, 0.015f, 0.05f, 0.02f, Brown));
    static ItemDef Konserv() => Ok("Konservburk", "Mat som håller", Cyl(0, 0.055f, 0, 0.075f, 0.11f, Silver), Cyl(0, 0.055f, 0, 0.077f, 0.07f, Red));
    static ItemDef Tandborste() => Ok("Tandborste", "Tandborste", Box(0, 0.008f, 0, 0.015f, 0.015f, 0.18f, Cyan), Box(0, 0.02f, 0.075f, 0.015f, 0.015f, 0.03f, White));
    static ItemDef Toapapper() => Ok("Toalettpapper", "Toalettpapper", Cyl(0, 0.05f, 0, 0.11f, 0.1f, White));
    static ItemDef ForstaHjalpen() => Ok("Första hjälpen", "Första hjälpen", Box(0, 0.06f, 0, 0.2f, 0.12f, 0.08f, Red),
        Box(0, 0.06f, -0.041f, 0.08f, 0.025f, 0.005f, White), Box(0, 0.06f, -0.041f, 0.025f, 0.08f, 0.005f, White));
    static ItemDef Dunjacka() { var d = Ok("Dunjacka", "Varma kläder", Box(0, 0.06f, 0, 0.4f, 0.12f, 0.3f, Navy), Box(0, 0.13f, 0.1f, 0.2f, 0.04f, 0.08f, Navy)); d.mass = 0.8f; return d; }
    static ItemDef Planbok() => Ok("Plånbok (ID, pengar, kort)", "Värdehandlingar", Box(0, 0.01f, 0, 0.11f, 0.02f, 0.09f, Brown), Box(0.03f, 0.021f, 0, 0.03f, 0.003f, 0.05f, Tan));
    static ItemDef Nycklar() => Ok("Nycklar", "Nycklar", Cyl(0, 0.003f, 0, 0.035f, 0.006f, Silver), Box(0.015f, 0.003f, 0.03f, 0.012f, 0.004f, 0.05f, Yellow), Box(-0.015f, 0.003f, 0.03f, 0.012f, 0.004f, 0.045f, Silver));
    static ItemDef Mobil() => Ok("Mobil och powerbank", "Mobil och laddare", Box(0, 0.005f, 0, 0.075f, 0.01f, 0.15f, Black), Box(0.09f, 0.012f, 0, 0.06f, 0.024f, 0.1f, Grey));
    static ItemDef Radio() => Ok("Vevradio", "Radio", Box(0, 0.07f, 0, 0.2f, 0.14f, 0.08f, Orange), Cyl(-0.04f, 0.07f, -0.041f, 0.08f, 0.005f, Black, new Vector3(90, 0, 0)),
        Box(0.11f, 0.07f, 0, 0.02f, 0.08f, 0.02f, Black), Cyl(0.07f, 0.2f, 0.02f, 0.008f, 0.14f, Silver));
    static ItemDef Liggunderlag() { var d = Ok("Liggunderlag", "Liggunderlag", Cyl(0, 0.08f, 0, 0.16f, 0.6f, Green, new Vector3(0, 0, 90))); d.mass = 0.6f; return d; }
    static ItemDef Horselkapor() => Ok("Hörselkåpor", "Hörselskydd", Ball(0, 0.05f, 0.07f, 0.08f, 0.1f, 0.05f, Yellow), Ball(0, 0.05f, -0.07f, 0.08f, 0.1f, 0.05f, Yellow), Box(0, 0.12f, 0, 0.03f, 0.02f, 0.15f, Black));

    // Fel saker
    static ItemDef Hund()
    {
        var d = No("Hund", "Husdjur får inte följa med in i skyddsrummet.",
            Box(0, 0.25f, 0, 0.22f, 0.2f, 0.45f, DogBrown), Box(0, 0.38f, 0.28f, 0.16f, 0.16f, 0.18f, DogBrown), Box(0, 0.35f, 0.39f, 0.08f, 0.07f, 0.07f, Tan),
            Ball(0, 0.37f, 0.43f, 0.03f, 0.03f, 0.03f, Black), Box(0.07f, 0.47f, 0.25f, 0.04f, 0.06f, 0.03f, Brown), Box(-0.07f, 0.47f, 0.25f, 0.04f, 0.06f, 0.03f, Brown),
            Cyl(0.08f, 0.08f, 0.16f, 0.05f, 0.16f, DogBrown), Cyl(-0.08f, 0.08f, 0.16f, 0.05f, 0.16f, DogBrown), Cyl(0.08f, 0.08f, -0.16f, 0.05f, 0.16f, DogBrown), Cyl(-0.08f, 0.08f, -0.16f, 0.05f, 0.16f, DogBrown),
            Box(0, 0.33f, -0.26f, 0.03f, 0.03f, 0.12f, DogBrown));
        d.mass = 5f; return d;
    }
    static ItemDef Stormkok() => No("Stormkök", "Öppen eld och gas är farligt i ett stängt rum.", Cyl(0, 0.05f, 0, 0.18f, 0.1f, new Color(0.3f, 0.3f, 0.3f)), Cyl(0, 0.13f, 0, 0.16f, 0.06f, Silver));
    static ItemDef Ljus() { var d = No("Tänt ljus", "Öppen eld förbrukar syre och kan orsaka brand.", Cyl(0, 0.08f, 0, 0.04f, 0.16f, White), Ball(0, 0.18f, 0, 0.02f, 0.04f, 0.02f, Orange, true)); d.light = true; return d; }
    static ItemDef Mjolk() => No("Mjölkpaket", "Mjölk blir dålig utan kylskåp.", Box(0, 0.1f, 0, 0.07f, 0.2f, 0.07f, White), Box(0, 0.19f, 0, 0.072f, 0.03f, 0.072f, Blue));
    static ItemDef Resvaska() { var d = No("Stor resväska", "Det finns inte plats för stora väskor i skyddsrummet.", Box(0, 0.33f, 0, 0.45f, 0.62f, 0.25f, new Color(0.45f, 0.08f, 0.1f)), Box(0, 0.68f, 0, 0.12f, 0.03f, 0.03f, Black)); d.mass = 4f; return d; }
    static ItemDef Notter() => No("Påse med nötter", "Nötter kan ge svåra allergiska reaktioner hos andra.", Box(0, 0.06f, 0, 0.12f, 0.12f, 0.05f, Tan), Box(0, 0.13f, 0, 0.12f, 0.02f, 0.05f, Brown));
    static ItemDef Spraydeo() => No("Spraydeo", "Starka dofter och spray är jobbigt i ett trångt rum.", Cyl(0, 0.075f, 0, 0.05f, 0.15f, LightBlue), Cyl(0, 0.165f, 0, 0.045f, 0.03f, Red));
    static ItemDef Parfym() => No("Parfym", "Starka dofter är jobbigt i ett trångt rum och kan ge allergi.", Box(0, 0.04f, 0, 0.06f, 0.08f, 0.03f, Pink), Cyl(0, 0.095f, 0, 0.025f, 0.03f, Silver));

    static void PlaceItems(Transform shelf, Transform group)
    {
        // Hyllplan (överkant) i hyllans lokala koordinater
        const float bottom = 0.33f, middle = 0.78f, top = 1.23f;
        void Row(float y, params ItemDef[] items)
        {
            float span = _shelfWidth - 0.2f;
            for (int i = 0; i < items.Length; i++)
            {
                float x = -span / 2f + span * (i + 0.5f) / items.Length;
                CreateItem(items[i], shelf, new Vector3(x, y + 0.01f, -0.02f), 0f);
            }
        }
        Row(bottom, Dunjacka(), Stormkok(), Liggunderlag());
        Row(middle, Ficklampa(), Mjolk(), Vattenflaska(), Ljus(), Mugg(), Konserv(), Spraydeo(), Tandborste(), Toapapper());
        Row(top, ForstaHjalpen(), Notter(), Planbok(), Radio(), Parfym(), Nycklar(), Mobil(), Horselkapor());

        // På golvet bredvid hyllan
        CreateItem(Resvaska(), shelf, new Vector3(_shelfWidth / 2f + 0.3f, 0.01f, -0.1f), 160f);
        CreateItem(Hund(), group, new Vector3(-0.6f, 0.01f, 0.3f), 230f);
    }

    static GameObject CreateItem(ItemDef def, Transform parent, Vector3 localPos, float yaw)
    {
        var root = new GameObject(def.name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = Quaternion.Euler(0, yaw, 0);

        int n = 0;
        foreach (var p in def.parts)
        {
            var m = p.glow ? Glow("Glod_" + ColorKey(p.c), p.c) : Mat("Sak_" + ColorKey(p.c), p.c);
            Prim(p.t, "Del" + (n++), root.transform, p.pos, p.rot, p.scale, m, !p.glow);
        }
        if (def.light)
        {
            var l = new GameObject("Låga_ljus").AddComponent<Light>();
            l.transform.SetParent(root.transform, false);
            l.transform.localPosition = new Vector3(0, 0.2f, 0);
            l.type = LightType.Point; l.range = 1.2f; l.intensity = 0.6f; l.color = new Color(1f, 0.7f, 0.3f);
            l.shadows = LightShadows.None;
        }

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = def.mass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var grab = root.AddComponent<XRGrabInteractable>();
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;

        var item = root.AddComponent<PackItem>();
        item.displayName = def.name;
        item.category = def.category ?? "";
        item.allowed = def.allowed;
        item.whyNot = def.whyNot ?? "";
        return root;
    }

    // ---------------- Möbler ----------------
    static Transform BuildShelf(Transform g, Vector3 pos, Material m)
    {
        var shelf = new GameObject("Hylla med saker").transform;
        shelf.SetParent(g, false);
        shelf.localPosition = pos;
        float W = _shelfWidth; const float D = 0.4f, H = 1.6f, T = 0.03f;
        foreach (float y in new[] { 0.3f, 0.75f, 1.2f, H })
            Prim(PrimitiveType.Cube, "Hyllplan", shelf, new Vector3(0, y + T / 2f - (y == H ? T : 0), 0), Vector3.zero, new Vector3(W, T, D), m, true);
        Prim(PrimitiveType.Cube, "Sida_V", shelf, new Vector3(-W / 2f, H / 2f, 0), Vector3.zero, new Vector3(T, H, D), m, true);
        Prim(PrimitiveType.Cube, "Sida_H", shelf, new Vector3(W / 2f, H / 2f, 0), Vector3.zero, new Vector3(T, H, D), m, true);
        Prim(PrimitiveType.Cube, "Bakstycke", shelf, new Vector3(0, H / 2f, D / 2f), Vector3.zero, new Vector3(W, H, T), m, true);
        return shelf;
    }

    static Transform BuildTable(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        Prim(PrimitiveType.Cube, "Skiva", t, new Vector3(0, size.y - 0.02f, 0), Vector3.zero, new Vector3(size.x, 0.04f, size.z), m, true);
        float lx = size.x / 2f - 0.04f, lz = size.z / 2f - 0.04f;
        foreach (var (x, z) in new[] { (lx, lz), (-lx, lz), (lx, -lz), (-lx, -lz) })
            Prim(PrimitiveType.Cube, "Ben", t, new Vector3(x, (size.y - 0.04f) / 2f, z), Vector3.zero, new Vector3(0.05f, size.y - 0.04f, 0.05f), m, true);
        return t;
    }

    static Backpack BuildBackpack(Transform g, Vector3 pos, Material panelMat)
    {
        var bagCol = Mat("Ryggsack", new Color(0.18f, 0.35f, 0.22f));
        var strap = Mat("Ryggsack_Rem", new Color(0.1f, 0.1f, 0.1f));
        var root = new GameObject("Ryggsäck");
        root.transform.SetParent(g, false);
        root.transform.localPosition = pos;
        // Remmarna (+z) bort från spelaren, fickan mot spelaren
        var t = root.transform;
        const float W = 0.34f, H = 0.45f, D = 0.22f, T = 0.02f;

        // Öppen låda: botten + fyra väggar
        Prim(PrimitiveType.Cube, "Botten", t, new Vector3(0, T / 2f, 0), Vector3.zero, new Vector3(W, T, D), bagCol, true);
        Prim(PrimitiveType.Cube, "Framsida", t, new Vector3(0, H / 2f, -D / 2f + T / 2f), Vector3.zero, new Vector3(W, H, T), bagCol, true);
        Prim(PrimitiveType.Cube, "Baksida", t, new Vector3(0, H / 2f, D / 2f - T / 2f), Vector3.zero, new Vector3(W, H, T), bagCol, true);
        Prim(PrimitiveType.Cube, "Sida_V", t, new Vector3(-W / 2f + T / 2f, H / 2f, 0), Vector3.zero, new Vector3(T, H, D - 2 * T), bagCol, true);
        Prim(PrimitiveType.Cube, "Sida_H", t, new Vector3(W / 2f - T / 2f, H / 2f, 0), Vector3.zero, new Vector3(T, H, D - 2 * T), bagCol, true);
        Prim(PrimitiveType.Cube, "Ficka", t, new Vector3(0, 0.15f, -D / 2f - 0.03f), Vector3.zero, new Vector3(0.24f, 0.18f, 0.06f), bagCol, true);
        Prim(PrimitiveType.Cube, "Rem_V", t, new Vector3(-0.09f, 0.25f, D / 2f + 0.01f), Vector3.zero, new Vector3(0.04f, 0.38f, 0.02f), strap);
        Prim(PrimitiveType.Cube, "Rem_H", t, new Vector3(0.09f, 0.25f, D / 2f + 0.01f), Vector3.zero, new Vector3(0.04f, 0.38f, 0.02f), strap);
        Prim(PrimitiveType.Cube, "Bärhandtag", t, new Vector3(0, H + 0.02f, D / 2f - 0.03f), Vector3.zero, new Vector3(0.1f, 0.03f, 0.02f), strap);
        var lid = Prim(PrimitiveType.Cube, "Lock (stängd)", t, new Vector3(0, H + 0.01f, 0), Vector3.zero, new Vector3(W + 0.01f, 0.03f, D + 0.01f), bagCol);

        // Öppningen: trigger som fångar saker man släpper i väskan
        var openingGo = new GameObject("Öppning");
        openingGo.transform.SetParent(t, false);
        var opening = openingGo.AddComponent<BoxCollider>();
        opening.center = new Vector3(0, H - 0.05f, 0);
        opening.size = new Vector3(W - 2 * T, 0.25f, D - 2 * T);
        opening.isTrigger = true;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var grab = root.AddComponent<XRGrabInteractable>();
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;

        var bp = root.AddComponent<Backpack>();
        bp.opening = opening;
        bp.closedLid = lid;
        bp.panelMaterial = panelMat;
        bp.panelHeightAboveBag = 0.85f;
        bp.rowsPerColumn = 8;
        lid.SetActive(false);
        return bp;
    }

    static ShelterDoor BuildDoor(Transform g, Vector3 pos, Material doorMat, Material frameMat, Material wallMat, Material lampMat, Material boardMat)
    {
        var root = new GameObject("Skyddsrumsdörr");
        root.transform.SetParent(g, false);
        root.transform.localPosition = pos;
        var t = root.transform;
        float OW = _doorWidth; const float OH = 2.05f;

        // Karm
        Prim(PrimitiveType.Cube, "Karm_V", t, new Vector3(-OW / 2f - 0.05f, (OH + 0.1f) / 2f, 0), Vector3.zero, new Vector3(0.1f, OH + 0.1f, 0.2f), frameMat, true);
        Prim(PrimitiveType.Cube, "Karm_H", t, new Vector3(OW / 2f + 0.05f, (OH + 0.1f) / 2f, 0), Vector3.zero, new Vector3(0.1f, OH + 0.1f, 0.2f), frameMat, true);
        Prim(PrimitiveType.Cube, "Karm_Topp", t, new Vector3(0, OH + 0.05f, 0), Vector3.zero, new Vector3(OW + 0.2f, 0.1f, 0.2f), frameMat, true);

        // Tillfälliga väggar så att man inte kan gå runt dörren (ta bort när dörren sitter i skyddsrummet)
        var walls = new GameObject("Tillfälliga väggar (ta bort sen)").transform;
        if (!_tempWalls) walls.gameObject.SetActive(false);
        walls.SetParent(t, false);
        Prim(PrimitiveType.Cube, "Vägg_V", walls, new Vector3(-OW / 2f - 0.1f - 0.5f, 1.25f, 0), Vector3.zero, new Vector3(1.0f, 2.5f, 0.15f), wallMat, true);
        Prim(PrimitiveType.Cube, "Vägg_H", walls, new Vector3(OW / 2f + 0.1f + 0.5f, 1.25f, 0), Vector3.zero, new Vector3(1.0f, 2.5f, 0.15f), wallMat, true);
        Prim(PrimitiveType.Cube, "Vägg_Ovan", walls, new Vector3(0, OH + 0.1f + (2.5f - OH - 0.1f) / 2f, 0), Vector3.zero, new Vector3(OW + 0.2f, 2.5f - OH - 0.1f, 0.15f), wallMat, true);

        // Dörrblad på gångjärn (öppnas inåt, bort från spelaren)
        var hinge = new GameObject("Gångjärn").transform;
        hinge.SetParent(t, false);
        hinge.localPosition = new Vector3(-OW / 2f, 0, 0.03f);
        Prim(PrimitiveType.Cube, "Dörrblad", hinge, new Vector3(OW / 2f, OH / 2f, 0), Vector3.zero, new Vector3(OW - 0.01f, OH - 0.01f, 0.06f), doorMat, true);
        Prim(PrimitiveType.Cube, "Handtag", hinge, new Vector3(OW - 0.12f, 1.0f, -0.05f), Vector3.zero, new Vector3(0.03f, 0.25f, 0.03f), frameMat);

        // Skylt och lampa (på spelarens sida)
        Prim(PrimitiveType.Cube, "Skylt", t, new Vector3(0, 1.55f, -0.02f), Vector3.zero, new Vector3(0.7f, 0.3f, 0.02f), boardMat, true);
        var sign = Text(t, "Skylttext", new Vector3(0, 1.55f, -0.035f), new Vector2(0.66f, 0.28f), 0.24f, "Dörren är låst");
        sign.transform.SetParent(hinge, true); // skylten följer med dörren
        t.Find("Skylt").SetParent(hinge, true);
        var lamp = Prim(PrimitiveType.Sphere, "Lampa", t, new Vector3(OW / 2f + 0.05f, 1.6f, -0.12f), Vector3.zero, Vector3.one * 0.07f, lampMat);

        // Zoner (används bara som områden)
        var approach = NewZone(t, "Zon framför dörren", new Vector3(0, 1.3f, -0.8f), new Vector3(1.6f, 2.6f, 1.3f));
        var doorway = NewZone(t, "Zon i dörröppningen", new Vector3(0, 1.3f, 0.1f), new Vector3(OW, 2.6f, 0.5f));
        var ret = new GameObject("Återlämningsplats").transform;
        ret.SetParent(t, false);
        ret.localPosition = new Vector3(0.6f, 0.5f, -0.9f);

        var door = root.AddComponent<ShelterDoor>();
        door.hinge = hinge;
        door.approachZone = approach;
        door.doorwayZone = doorway;
        door.returnPoint = ret;
        door.sign = sign;
        door.lamp = lamp.GetComponent<Renderer>();
        door.enableWhenOpen = new GameObject[0];
        door.disableWhenOpen = new GameObject[0];
        return door;
    }

    static BoxCollider NewZone(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var b = go.AddComponent<BoxCollider>();
        b.center = center;
        b.size = size;
        b.isTrigger = true;
        b.enabled = false;
        return b;
    }

    // ---------------- Hjälpfunktioner ----------------
    static TextMeshPro Text(Transform parent, string name, Vector3 pos, Vector2 size, float fontSize, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.rectTransform.sizeDelta = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = fontSize * 0.4f;
        tmp.fontSizeMax = fontSize;
        return tmp;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (!keepCollider) Object.DestroyImmediate(col);
        else if (col is MeshCollider mc) mc.convex = true;
        return go;
    }

    static string ColorKey(Color c) => ColorUtility.ToHtmlStringRGB(c);

    static Material Glow(string name, Color c)
    {
        var m = Mat(name, c);
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 3f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
        }
        return m;
    }

    static Material Mat(string name, Color color, float metallic = 0f, float smoothness = 0.3f)
    {
        if (_mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatFolder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "Forrum");
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
