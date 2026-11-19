using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Meny: Tools > Vatten > Skapa kran och baljor
/// Bygger en vattenkran på närmaste vägg framför spelaren och två baljor under/bredvid den.
/// </summary>
public static class WaterSetupBuilder
{
    const string Root = "Assets/Vatten";
    const string MatFolder = Root + "/Material";

    [MenuItem("Tools/Vatten/Skapa kran och baljor")]
    public static void Build()
    {
        Physics.SyncTransforms();

        // 1. Hitta spelaren
        Vector3 player = Vector3.zero;
        var cam = Camera.main;
        if (cam != null) player = cam.transform.position;
        Transform rig = cam != null ? cam.transform.root : null;

        // Ta bort en tidigare version så att man kan köra verktyget igen
        GameObject old;
        while ((old = GameObject.Find("Vatten")) != null) Undo.DestroyObjectImmediate(old);
        Physics.SyncTransforms();

        // Golvet: första träffen nedåt som inte är spelaren själv
        float floorY = rig != null ? rig.position.y : player.y;
        var downHits = Physics.RaycastAll(player + Vector3.up * 0.3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float bestDown = float.MaxValue;
        foreach (var h in downHits)
        {
            if (rig != null && h.transform.IsChildOf(rig)) continue;
            if (h.normal.y < 0.7f) continue;
            if (h.distance < bestDown) { bestDown = h.distance; floorY = h.point.y; }
        }

        // 2. Hitta närmaste vägg (helst en del av rummet)
        var room = GameObject.Find("Skyddssrum_layout");
        Vector3 chest = new Vector3(player.x, floorY + 1.0f, player.z);
        bool found = false;
        RaycastHit wall = default;
        float bestDist = float.MaxValue;
        for (int i = 0; i < 16; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
            if (!Physics.Raycast(chest, dir, out var h, 10f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (rig != null && h.transform.IsChildOf(rig)) continue;
            if (h.distance < 0.6f) continue;                       // för nära
            if (Mathf.Abs(h.normal.y) > 0.3f) continue;            // inte en vägg
            if (room != null && !h.transform.IsChildOf(room.transform)) continue;
            if (h.distance < bestDist) { bestDist = h.distance; wall = h; found = true; }
        }

        Vector3 normal, wallPoint;
        if (found)
        {
            normal = Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
            wallPoint = wall.point;
        }
        else
        {
            Debug.LogWarning("[Vatten] Hittade ingen vägg, placerar kranen framför spelaren. Flytta den till en vägg.");
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            normal = -fwd;
            wallPoint = chest + fwd * 1.5f;
        }
        Vector3 right = Vector3.Cross(Vector3.up, normal).normalized;

        // 3. Material
        var metal = GetMat("Metall", new Color(0.75f, 0.76f, 0.78f), false, 0.9f, 0.7f);
        var red = GetMat("Ratt_Rod", new Color(0.75f, 0.08f, 0.06f), false, 0.1f, 0.5f);
        var zinc = GetMat("Balja_Zink", new Color(0.62f, 0.64f, 0.66f), false, 0.7f, 0.35f);
        var water = GetMat("Vatten", new Color(0.25f, 0.55f, 0.85f, 0.55f), true, 0f, 0.95f);

        // 4. Bygg
        var tap = BuildTap(metal, red, water);
        tap.transform.SetPositionAndRotation(
            new Vector3(wallPoint.x, floorY + 0.8f, wallPoint.z),
            Quaternion.LookRotation(normal, Vector3.up));

        Vector3 under = new Vector3(wallPoint.x, floorY, wallPoint.z) + normal * 0.29f;
        var b1 = BuildBasin("Balja 1", zinc, water);
        b1.transform.SetPositionAndRotation(under, Quaternion.LookRotation(normal, Vector3.up));
        var b2 = BuildBasin("Balja 2", zinc, water);
        b2.transform.SetPositionAndRotation(under + right * 0.7f, Quaternion.LookRotation(normal, Vector3.up));

        var group = new GameObject("Vatten");
        group.transform.position = new Vector3(wallPoint.x, floorY, wallPoint.z);
        tap.transform.SetParent(group.transform, true);
        b1.transform.SetParent(group.transform, true);
        b2.transform.SetParent(group.transform, true);

        Undo.RegisterCreatedObjectUndo(group, "Skapa kran och baljor");
        EditorSceneManager.MarkSceneDirty(group.scene);
        Selection.activeGameObject = group;
        Debug.Log($"[Vatten] Kran och två baljor skapade. Golv y={floorY:F2}, vägg {wallPoint} (vägg hittad: {found}).");
    }

    // ---------- Kran ----------
    static GameObject BuildTap(Material metal, Material red, Material water)
    {
        var root = new GameObject("Vattenkran");
        var t = root.transform;

        Prim(PrimitiveType.Cylinder, "Väggfäste", t, new Vector3(0, 0, 0.01f), new Vector3(90, 0, 0), new Vector3(0.07f, 0.01f, 0.07f), metal);
        Prim(PrimitiveType.Cylinder, "Rör", t, new Vector3(0, 0, 0.09f), new Vector3(90, 0, 0), new Vector3(0.03f, 0.08f, 0.03f), metal);
        Prim(PrimitiveType.Cylinder, "Ventilhus", t, new Vector3(0, 0.025f, 0.09f), Vector3.zero, new Vector3(0.045f, 0.025f, 0.045f), metal);
        Prim(PrimitiveType.Cylinder, "Pip", t, new Vector3(0, -0.04f, 0.17f), Vector3.zero, new Vector3(0.03f, 0.04f, 0.03f), metal);
        Prim(PrimitiveType.Sphere, "Böj", t, new Vector3(0, 0, 0.17f), Vector3.zero, Vector3.one * 0.03f, metal);

        // Ratt (det man greppar)
        var handle = new GameObject("Ratt");
        handle.transform.SetParent(t, false);
        handle.transform.localPosition = new Vector3(0, 0.06f, 0.09f);
        var col = handle.AddComponent<BoxCollider>();
        col.size = new Vector3(0.13f, 0.06f, 0.13f);
        var rb = handle.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        var simple = handle.AddComponent<XRSimpleInteractable>();

        var spin = new GameObject("Ratt_Visuell").transform;
        spin.SetParent(handle.transform, false);
        Prim(PrimitiveType.Cylinder, "Nav", spin, Vector3.zero, Vector3.zero, new Vector3(0.025f, 0.015f, 0.025f), red);
        Prim(PrimitiveType.Cube, "Eker1", spin, Vector3.zero, Vector3.zero, new Vector3(0.11f, 0.015f, 0.018f), red);
        Prim(PrimitiveType.Cube, "Eker2", spin, Vector3.zero, Vector3.zero, new Vector3(0.018f, 0.015f, 0.11f), red);

        // Utlopp + stråle
        var outlet = new GameObject("Utlopp");
        outlet.transform.SetParent(t, false);
        outlet.transform.localPosition = new Vector3(0, -0.085f, 0.17f);
        var stream = outlet.AddComponent<WaterStream>();
        stream.visual = Prim(PrimitiveType.Cylinder, "Vattenstråle", outlet.transform, new Vector3(0, -0.3f, 0), Vector3.zero, new Vector3(0.02f, 0.3f, 0.02f), water).transform;
        stream.width = 0.02f;

        var tap = root.AddComponent<WaterTap>();
        tap.handle = simple;
        tap.handleVisual = spin;
        tap.stream = stream;
        return root;
    }

    // ---------- Balja ----------
    static GameObject BuildBasin(string name, Material zinc, Material water)
    {
        const float W = 0.5f, H = 0.25f, T = 0.02f;
        var root = new GameObject(name);
        var t = root.transform;

        Prim(PrimitiveType.Cube, "Botten", t, new Vector3(0, T / 2, 0), Vector3.zero, new Vector3(W, T, W), zinc, true);
        Prim(PrimitiveType.Cube, "Vägg_Fram", t, new Vector3(0, H / 2, W / 2 - T / 2), Vector3.zero, new Vector3(W, H, T), zinc, true);
        Prim(PrimitiveType.Cube, "Vägg_Bak", t, new Vector3(0, H / 2, -W / 2 + T / 2), Vector3.zero, new Vector3(W, H, T), zinc, true);
        Prim(PrimitiveType.Cube, "Vägg_Vänster", t, new Vector3(-W / 2 + T / 2, H / 2, 0), Vector3.zero, new Vector3(T, H, W - 2 * T), zinc, true);
        Prim(PrimitiveType.Cube, "Vägg_Höger", t, new Vector3(W / 2 - T / 2, H / 2, 0), Vector3.zero, new Vector3(T, H, W - 2 * T), zinc, true);
        // Handtag på sidorna
        Prim(PrimitiveType.Cube, "Handtag_V", t, new Vector3(-W / 2 - 0.02f, H - 0.03f, 0), Vector3.zero, new Vector3(0.03f, 0.02f, 0.12f), zinc, true);
        Prim(PrimitiveType.Cube, "Handtag_H", t, new Vector3(W / 2 + 0.02f, H - 0.03f, 0), Vector3.zero, new Vector3(0.03f, 0.02f, 0.12f), zinc, true);

        var waterGo = Prim(PrimitiveType.Cube, "Vattenyta", t, new Vector3(0, T + 0.001f, 0), Vector3.zero, new Vector3(W - 2 * T - 0.002f, 0.002f, W - 2 * T - 0.002f), water);
        waterGo.SetActive(false);

        // Kantpunkter
        var rimParent = new GameObject("Kant").transform;
        rimParent.SetParent(t, false);
        var rims = new Transform[8];
        int i = 0;
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                var p = new GameObject($"Kant_{i}").transform;
                p.SetParent(rimParent, false);
                p.localPosition = new Vector3(x * (W / 2), H, z * (W / 2));
                rims[i++] = p;
            }

        var pour = new GameObject("Hällpunkt");
        pour.transform.SetParent(t, false);
        pour.transform.localPosition = new Vector3(0, H, W / 2);
        var pourStream = pour.AddComponent<WaterStream>();
        pourStream.visual = Prim(PrimitiveType.Cylinder, "Hällstråle", pour.transform, Vector3.zero, Vector3.zero, new Vector3(0.03f, 0.1f, 0.03f), water).transform;
        pourStream.width = 0.035f;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var grab = root.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;

        var basin = root.AddComponent<WaterBasin>();
        basin.capacityLiters = 45f;
        basin.waterSurface = waterGo.transform;
        basin.innerBottomY = T;
        basin.innerTopY = H - 0.01f;
        basin.rimPoints = rims;
        basin.pourStream = pourStream;
        return root;
    }

    // ---------- Hjälpfunktioner ----------
    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material mat, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Material GetMat(string name, Color color, bool transparent, float metallic, float smoothness)
    {
        string path = $"{MatFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "Vatten");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder(Root, "Material");

        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Standard");
        var m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);

        if (transparent)
        {
            if (urp)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_ALPHABLEND_ON");
            }
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
