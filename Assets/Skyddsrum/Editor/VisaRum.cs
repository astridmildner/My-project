using UnityEditor;
using UnityEngine;

/// <summary>
/// Hjälpverktyg: flyttar Scene-vyns kamera, och väljer var spelaren börjar/står i Game-vyn.
/// Valet av startplats sparas bara i Unity på den här datorn – bygget till Questen påverkas inte.
/// </summary>
[InitializeOnLoad]
public static class VisaRum
{
    [MenuItem("Tools/Skyddsrum/Visa i Scene-vyn/Stora rummet")]
    public static void BigRoom() => Show(new Vector3(-2.9f, 3.1f, 0.2f), Quaternion.Euler(12f, -120f, 0f), 0.4f);

    [MenuItem("Tools/Skyddsrum/Visa i Scene-vyn/Toalettrummet")]
    public static void Toilets() => Show(new Vector3(3.0f, 3.0f, -1.2f), Quaternion.Euler(10f, 90f, 0f), 0.4f);

    [MenuItem("Tools/Skyddsrum/Visa i Scene-vyn/Förrummet")]
    public static void Anteroom() => Show(new Vector3(1.6f, 3.1f, 1.8f), Quaternion.Euler(10f, -90f, 0f), 0.4f);

    [MenuItem("Tools/Skyddsrum/Visa i Scene-vyn/Skyddsrumsdörren – insidan")]
    public static void DoorInside() => Show(new Vector3(0.45f, 3.1f, 1.84f), Quaternion.Euler(6f, 90f, 0f), 0.9f);

    [MenuItem("Tools/Skyddsrum/Visa i Scene-vyn/Skyddsrumsdörren – utsidan (förrummet)")]
    public static void DoorOutside() => Show(new Vector3(0.75f, 3.1f, 1.84f), Quaternion.Euler(6f, -90f, 0f), 0.9f);

    // ---- Game-vyn: välj innan Play = startplats, välj under Play = flytta dit direkt ----
    const string Menu = "Tools/Skyddsrum/Visa i Game-vyn/";
    const string Key = "Skyddsrum_Startplats";
    static readonly (string name, Vector3 pos, float yaw)[] Spots =
    {
        ("Förrummet", new Vector3(2.4f, 2.1f, 1.8f), -90f),
        ("Vid skyddsrumsdörren – insidan", new Vector3(-1.75f, 2.1f, 1.75f), 90f),
        ("Stora rummet – mot britsarna", new Vector3(-1.2f, 2.1f, 1.6f), -125f),
        ("Stora rummet – mot ventilationen", new Vector3(-1.8f, 2.1f, -1.2f), 15f),
        ("Toalettrummet", new Vector3(1.4f, 2.1f, -1.2f), 90f),
    };

    static VisaRum() => EditorApplication.playModeStateChanged += OnPlayMode;

    [MenuItem(Menu + "Förrummet", false, 1)] static void S0() => Go(0);
    [MenuItem(Menu + "Vid skyddsrumsdörren – insidan", false, 2)] static void S1() => Go(1);
    [MenuItem(Menu + "Stora rummet – mot britsarna", false, 3)] static void S2() => Go(2);
    [MenuItem(Menu + "Stora rummet – mot ventilationen", false, 4)] static void S3() => Go(3);
    [MenuItem(Menu + "Toalettrummet", false, 5)] static void S4() => Go(4);

    [MenuItem(Menu + "Förrummet", true)] static bool V0() => Check(0);
    [MenuItem(Menu + "Vid skyddsrumsdörren – insidan", true)] static bool V1() => Check(1);
    [MenuItem(Menu + "Stora rummet – mot britsarna", true)] static bool V2() => Check(2);
    [MenuItem(Menu + "Stora rummet – mot ventilationen", true)] static bool V3() => Check(3);
    [MenuItem(Menu + "Toalettrummet", true)] static bool V4() => Check(4);

    [MenuItem(Menu + "Vanlig start (ingen startplats)", false, 20)]
    static void ResetStart()
    {
        EditorPrefs.DeleteKey(Key);
        Debug.Log("[Visa] Ingen startplats – du börjar där spelet normalt börjar.");
    }

    static bool Check(int i)
    {
        UnityEditor.Menu.SetChecked(Menu + Spots[i].name, EditorPrefs.GetInt(Key, -1) == i);
        return true;
    }

    static void Go(int i)
    {
        if (EditorApplication.isPlaying)
        {
            MovePlayer(Spots[i].pos, Spots[i].yaw);
            return;
        }
        EditorPrefs.SetInt(Key, i);
        Debug.Log($"[Visa] Startplats: {Spots[i].name}. Du börjar här när du trycker Play (bara i Unity, inte på Questen).");
    }

    static double _moveAt;

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !EditorPrefs.HasKey(Key)) return;
        _moveAt = EditorApplication.timeSinceStartup + 0.5; // vänta tills XR-riggen startat
        EditorApplication.update -= WaitAndMove;
        EditorApplication.update += WaitAndMove;
    }

    static void WaitAndMove()
    {
        if (EditorApplication.timeSinceStartup < _moveAt) return;
        EditorApplication.update -= WaitAndMove;
        if (!EditorApplication.isPlaying) return;
        int i = EditorPrefs.GetInt(Key, -1);
        if (i < 0 || i >= Spots.Length) return;
        MovePlayer(Spots[i].pos, Spots[i].yaw);
        Debug.Log($"[Visa] Startade vid: {Spots[i].name}");
    }

    static void MovePlayer(Vector3 pos, float yaw)
    {
        var cam = Camera.main; if (cam == null) return;
        var rig = cam.transform.root;
        foreach (var door in Object.FindObjectsByType<ShelterDoor>(FindObjectsSortMode.None))
        {
            var z = door.transform.Find("Spärrzoner"); if (z != null) z.gameObject.SetActive(false);
        }
        var cc = rig.GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;
        rig.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        if (cc != null) cc.enabled = true;
    }

    static void Show(Vector3 pivot, Quaternion rot, float size)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        sv.orthographic = false;
        sv.LookAt(pivot, rot, size);
        sv.Repaint();
    }
}
