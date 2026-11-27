using UnityEditor;
using UnityEngine;

/// <summary>Hjälpverktyg: flyttar Scene-vyns kamera så att man ser in i rummen.</summary>
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

    [MenuItem("Tools/Skyddsrum/Visa i Game-vyn (under Play)/Förrummet")]
    public static void PlayAnteroom() => MovePlayer(new Vector3(2.4f, 2.1f, 1.8f), -90f);

    [MenuItem("Tools/Skyddsrum/Visa i Game-vyn (under Play)/Vid skyddsrumsdörren – insidan")]
    public static void PlayDoorInside() => MovePlayer(new Vector3(-1.75f, 2.1f, 1.75f), 90f);

    // ---- Under Play: flytta spelaren (återställs när Play stoppas) ----
    [MenuItem("Tools/Skyddsrum/Visa i Game-vyn (under Play)/Stora rummet – mot britsarna")]
    public static void PlayBigRoomWest() => MovePlayer(new Vector3(-1.2f, 2.1f, 1.6f), -125f);

    [MenuItem("Tools/Skyddsrum/Visa i Game-vyn (under Play)/Stora rummet – mot ventilationen")]
    public static void PlayBigRoomSouth() => MovePlayer(new Vector3(-1.8f, 2.1f, -1.2f), 15f);

    [MenuItem("Tools/Skyddsrum/Visa i Game-vyn (under Play)/Toalettrummet")]
    public static void PlayToilets() => MovePlayer(new Vector3(1.4f, 2.1f, -1.2f), 90f);

    static void MovePlayer(Vector3 pos, float yaw)
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Visa] Tryck Play först."); return; }
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
